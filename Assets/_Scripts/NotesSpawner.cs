/*
 * The spawner code and also the correct timing stuff was taken from the project:
 * BeatSaver Viewer (https://github.com/supermedium/beatsaver-viewer) and ported to C#.
 * 
 * To be more precisly most of the code in the Update() method was ported to C# by me 
 * from their project.
 * 
 * Without that project this project won't exist, so thank you very much for releasing 
 * the source code under MIT license!
 */

using Boomlagoon.JSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UObject = UnityEngine.Object;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NotesSpawner : MonoBehaviour
{
    [Tooltip("Demon prefabs: [0]=Left, [1]=Right, [2]=Left NonDirection, [3]=Right NonDirection. Same layout as old Cubes.")]
    public GameObject[] Demons;
    [Tooltip("Legacy fallback if Demons empty. Can use cube prefabs; DemonHandling added at runtime.")]
    public GameObject[] Cubes;
    public GameObject Wall;
    public Transform[] SpawnPoints;

    [Tooltip("Cyan frame in front of the camera marking the rough cut plane (desktop testing).")]
    public bool showHitLineGuide = true;

    [Header("Gameplay environment")]
    [Tooltip("Spawn the same Stadium model used in the Menu as a distant backdrop in OpenSaber.")]
    public bool showMenuStadiumInGameplay = true;

    [Tooltip("Default: Menu uses Assets/_Models/Stadium/source/untitled.obj. Assign here for builds; in_editor fallback loads that path if empty.")]
    public GameObject stadiumModelPrefab;

    [Tooltip("Transform under GameplayStadiumBackdrop root — matches the Menu scene Stadium prefab instance.")]
    public Vector3 stadiumBackdropLocalPosition = new Vector3(0f, -234.23027f, 15391.3955f);

    public Vector3 stadiumBackdropLocalEuler = new Vector3(0f, 180f, 0f);

    public Vector3 stadiumBackdropLocalScale = new Vector3(986.2327f, 986.2327f, 986.2327f);

    [Header("Desktop saber mesh (optional)")]
    [Tooltip("When true, saber blades/capsules/billboards are not drawn; Slice/hits/gameplay stay active. Toggle off to show swords again.")]
    public bool hideDesktopSaberVisuals = true;

    [FormerlySerializedAs("developerSwordVisualPrefab")]
    [Tooltip("Optional override for both hands. If empty: Editor loads Rumi sword from Assets/_Models/Lightsabers/.../Sword_01 (1).fbx; player builds need a prefab under a Resources/Lightsabers/ path (see TryLoadImportedSwordFromResources).")]
    public GameObject importedSwordVisualPrefab;

    [Tooltip("If false, always use the red/blue capsule proxy instead of the imported mesh.")]
    public bool useImportedModelWhenAssigned = true;

    [Tooltip("Desktop: concentric line halos (often look like bullseyes). Off by default — use team tint on the blade instead.")]
    public bool showDesktopRingHalos = false;

    [Tooltip("Desktop: tint and emission on imported sword materials (left red / right blue).")]
    public bool applyImportedBladeTeamTintAndGlow = true;

    [Range(0f, 1f)]
    [Tooltip("How much albedo is replaced with team color before emission.")]
    public float importedBladeTeamTintMix = 0.42f;

    [Tooltip("HDR-style emission strength on team color (needs emission on shader). Same idea as multiplying Emission in Shader Graph.")]
    [Min(0f)]
    public float importedBladeEmissionIntensity = 8f;

    [Tooltip("Oscillates material emission at runtime (0 = off). C# equivalent of sin(Time) * emission.")]
    [Min(0f)]
    public float importedBladeEmissionPulseHz = 0f;

    [Range(0f, 1f)]
    [Tooltip("How strong the pulse modulates emission when pulse Hz > 0.")]
    public float importedBladeEmissionPulseDepth = 0.28f;

    [Tooltip("Desktop: extra semi-transparent quad that billboards toward the camera for a visible glow.")]
    public bool showImportedBladeAdditiveAura = true;

    [Range(0f, 1f)]
    [Tooltip("Lower = less full-screen glow bleed from saber aura quads.")]
    public float importedBladeAuraAlpha = 0.36f;

    [Tooltip("Multiplies combined renderer bounds so the aura slightly wraps the mesh.")]
    [Min(0.5f)]
    public float importedBladeAuraWorldScale = 1.15f;

    [Tooltip("Local pose on the Slice transform.")]
    public Vector3 importedBladeLocalPosition = Vector3.zero;

    public Vector3 importedBladeLocalEuler = Vector3.zero;

    [Tooltip("Try 0.01 if the model is huge (cm units).")]
    public Vector3 importedBladeLocalScale = Vector3.one;

    [Tooltip("Desktop only: extra rotation for halo rings in Slice local space (try e.g. 90,0,0 if rings look edge-on).")]
    public Vector3 importedHaloLocalEuler = Vector3.zero;

    [Tooltip("Desktop only: halo offset in Slice local space (e.g. slide along the blade). Halo follows the same transform as hits.")]
    public Vector3 importedHaloLocalPosition = Vector3.zero;

    public Vector3 fallbackCapsuleLocalOffset = new Vector3(0f, 0f, 0.38f);

    public Vector3 fallbackCapsuleLocalScale = new Vector3(0.05f, 0.5f, 0.05f);

    private string jsonString;
    private string audioFilePath;
    private List<Note> NotesToSpawn = new List<Note>();
    private List<Obstacle> ObstaclesToSpawn = new List<Obstacle>();
    private double BeatsPerMinute;

    private double BeatsTime = 0;
    private double? BeatsPreloadTime = 0;
    private double BeatsPreloadTimeTotal = 0;

    private readonly double beatAnticipationTime = 1.1;
    private readonly double beatSpeed = 8.0;
    private readonly double beatWarmupTime = BeatsConstants.BEAT_WARMUP_TIME / 1000;
    private readonly double beatWarmupSpeed = BeatsConstants.BEAT_WARMUP_SPEED;

    private AudioSource audioSource;

    private SongSettings Songsettings;
    private SceneHandling SceneHandling;
    private bool menuLoadInProgress = false;
    private bool audioLoaded = false;

    /// <summary>
    /// If the menu is still loaded, copy the current row from <see cref="LoadSongInfos"/> into <see cref="SongSettings.CurrentSong"/>.
    /// </summary>
    static void TrySyncCurrentSongFromSongBrowser(SongSettings settings)
    {
        if (settings == null)
            return;
        var infos = UObject.FindAnyObjectByType<LoadSongInfos>();
        if (infos == null || infos.AllSongs == null || infos.AllSongs.Count == 0)
            return;
        int idx = Mathf.Clamp(settings.CurrentSongIndex, 0, infos.AllSongs.Count - 1);
        settings.CurrentSong = infos.AllSongs[idx];
    }

    /// <summary>
    /// Matches menu labels like "ExpertPlus (Standard)" or "ExpertPlus (NoArrows)"; plain "ExpertPlus" picks the first set that contains it.
    /// </summary>
    private static void TryResolveBeatmap(JSONObject infoFile, string songFolder, string selectedDifficulty, out string audioPath, out string beatmapJson)
    {
        audioPath = null;
        beatmapJson = null;

        string wantDiff = selectedDifficulty;
        string wantChar = null;
        int sep = selectedDifficulty.LastIndexOf(" (", StringComparison.Ordinal);
        if (sep > 0 && selectedDifficulty.EndsWith(")", StringComparison.Ordinal))
        {
            wantDiff = selectedDifficulty.Substring(0, sep);
            wantChar = selectedDifficulty.Substring(sep + 2, selectedDifficulty.Length - sep - 3);
        }

        var difficultyBeatmapSets = infoFile.GetArray("_difficultyBeatmapSets");
        string songFile = infoFile.GetString("_songFilename");

        foreach (var beatmapSets in difficultyBeatmapSets)
        {
            string setChar = "Standard";
            try
            {
                setChar = beatmapSets.Obj.GetString("_beatmapCharacteristicName");
            }
            catch
            {
                /* omit */
            }

            if (wantChar != null && !string.Equals(setChar, wantChar, StringComparison.Ordinal))
                continue;

            foreach (var difficultyBeatmaps in beatmapSets.Obj.GetArray("_difficultyBeatmaps"))
            {
                if (difficultyBeatmaps.Obj.GetString("_difficulty") != wantDiff)
                    continue;

                audioPath = Path.Combine(songFolder, songFile);
                beatmapJson = File.ReadAllText(Path.Combine(songFolder, difficultyBeatmaps.Obj.GetString("_beatmapFilename")));
                return;
            }
        }
    }

    void Start()
    {
        if (GameplayCalibrationGate.BlocksNoteTimeline)
        {
            // OpenSaber should not be loaded while calibration is active with the current
            // scene flow, but if it is (e.g. editor test), defer startup until the gate clears.
            StartCoroutine(WaitForCalibrationThenStart());
            return;
        }
        StartInternal();
    }

    IEnumerator WaitForCalibrationThenStart()
    {
        while (GameplayCalibrationGate.BlocksNoteTimeline)
            yield return null;
        StartInternal();
    }

    void StartInternal()
    {
        GameObject songGo = GameObject.FindGameObjectWithTag("SongSettings");
        if (songGo == null)
        {
            Debug.LogError("[NotesSpawner] No GameObject with tag SongSettings. Open gameplay from the menu after choosing a song (do not play OpenSaber in isolation).");
            enabled = false;
            return;
        }

        Songsettings = songGo.GetComponent<SongSettings>();
        if (Songsettings == null)
        {
            Debug.LogError("[NotesSpawner] SongSettings component missing on the SongSettings object.");
            enabled = false;
            return;
        }

        if (Songsettings.CurrentSong == null)
            TrySyncCurrentSongFromSongBrowser(Songsettings);

        if (Songsettings.CurrentSong == null)
        {
            Debug.LogError("[NotesSpawner] No song selected (SongSettings.CurrentSong is null). From the title screen: Start → pick a song → Play. If you use Enter Play Mode Options without Reload Domain, try a full play-mode restart.");
            enabled = false;
            return;
        }

        GameObject sceneGo = GameObject.FindGameObjectWithTag("SceneHandling");
        if (sceneGo == null)
        {
            Debug.LogError("[NotesSpawner] No GameObject with tag SceneHandling.");
            enabled = false;
            return;
        }

        SceneHandling = sceneGo.GetComponent<SceneHandling>();
        if (SceneHandling == null)
        {
            Debug.LogError("[NotesSpawner] SceneHandling component missing on the SceneHandling object.");
            enabled = false;
            return;
        }

        string path = Songsettings.CurrentSong.Path;
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[NotesSpawner] Current song has no folder path.");
            enabled = false;
            return;
        }
        if (Directory.Exists(path))
        {
            if (Directory.GetFiles(path, "info.dat").Length > 0)
            {
                JSONObject infoFile = JSONObject.Parse(File.ReadAllText(Path.Combine(path, "info.dat")));
                TryResolveBeatmap(infoFile, path, Songsettings.CurrentSong.SelectedDifficulty, out audioFilePath, out jsonString);
            }
        }

        audioSource = GetComponent<AudioSource>();

        GameplayDebugHud.EnsureCreated(transform);

        if (string.IsNullOrEmpty(jsonString))
        {
            Debug.LogError("[NotesSpawner] No beatmap for this song/difficulty (missing info.dat map or wrong SelectedDifficulty).");
            enabled = false;
            return;
        }

        StartCoroutine("LoadAudio");

        JSONObject json = JSONObject.Parse(jsonString);

        var bpm = Convert.ToDouble(Songsettings.CurrentSong.BPM);

        //Notes (skip bombs / mines: Beat Saber types other than 0 red / 1 blue)
        var notes = json.GetArray("_notes");
        foreach (var note in notes)
        {
            int type = (int)note.Obj.GetNumber("_type");
            if (type != 0 && type != 1)
                continue;

            var n = new Note
            {
                Hand = (NoteType)type,
                CutDirection = (CutDirection)note.Obj.GetNumber("_cutDirection"),
                LineIndex = (int)note.Obj.GetNumber("_lineIndex"),
                LineLayer = (int)note.Obj.GetNumber("_lineLayer"),
                TimeInSeconds = (note.Obj.GetNumber("_time") / bpm) * 60,
                Time = (note.Obj.GetNumber("_time"))
            };

            NotesToSpawn.Add(n);
        }

        //Obstacles
        //var obstacles = json.GetArray("_obstacles");
        //foreach (var obstacle in obstacles)
        //{
        //    var o = new Obstacle
        //    {
        //        Type = (ObstacleType)obstacle.Obj.GetNumber("_type"),
        //        Duration = obstacle.Obj.GetNumber("_duration"),
        //        LineIndex = (int)obstacle.Obj.GetNumber("_lineIndex"),
        //        TimeInSeconds = (obstacle.Obj.GetNumber("_time") / bpm) * 60,
        //        Time = (obstacle.Obj.GetNumber("_time")),
        //        Width = (obstacle.Obj.GetNumber("_width"))
        //    };

        //    ObstaclesToSpawn.Add(o);
        //}

        BeatsPerMinute = bpm;
        BeatsPreloadTimeTotal = (beatAnticipationTime + beatWarmupTime);

        if (showHitLineGuide && FindAnyObjectByType<BeatSaberHitLineGuide>() == null)
        {
            var guideGo = new GameObject("BeatSaberHitLineGuide");
            guideGo.AddComponent<BeatSaberHitLineGuide>();
        }

        if (UObject.FindAnyObjectByType<SaberNearestBlockAlignmentProvider>() == null)
            gameObject.AddComponent<SaberNearestBlockAlignmentProvider>();

        SaberGameplayBootstrap.EnsureAfterGameplayLoad();
        EnsureStadiumBackdrop();
    }

    void EnsureStadiumBackdrop()
    {
        if (!showMenuStadiumInGameplay)
            return;

        Scene scene = gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == "GameplayStadiumBackdropRoot")
                return;
        }

        GameObject prefab = stadiumModelPrefab;
#if UNITY_EDITOR
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Models/Stadium/source/untitled.obj");
        }
#endif
        if (prefab == null)
        {
            Debug.LogWarning(
                "[NotesSpawner] showMenuStadiumInGameplay is enabled but stadiumModelPrefab is not assigned. " +
                "Assign the Stadium model (untitled.obj) on the Spawner, or add an editor Resources fallback.");
            return;
        }

        var backdropRoot = new GameObject("GameplayStadiumBackdropRoot");
        SceneManager.MoveGameObjectToScene(backdropRoot, scene);
        GameObject inst = UObject.Instantiate(prefab, backdropRoot.transform, false);
        inst.name = "Stadium";
        Transform tr = inst.transform;
        tr.localPosition = stadiumBackdropLocalPosition;
        tr.localEulerAngles = stadiumBackdropLocalEuler;
        tr.localScale = stadiumBackdropLocalScale;

        foreach (var cam in inst.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;
        foreach (var light in inst.GetComponentsInChildren<Light>(true))
            light.enabled = false;
    }

    /// <summary>Loads a sword model placed under a <c>Resources</c> folder (path without extension).</summary>
    public static GameObject TryLoadImportedSwordFromResources()
    {
#if UNITY_EDITOR
        const string editorRumiFbx = "Assets/_Models/Lightsabers/kpop-demon-hunters-rumis-sword/source/Sword_01 (1).fbx";
        var rumi = AssetDatabase.LoadAssetAtPath<GameObject>(editorRumiFbx);
        if (rumi != null)
            return rumi;
#endif
        string[] candidates =
        {
            "Lightsabers/kpop-demon-hunters-rumis-sword",
            "Lightsabers/kpop_demon_hunters_rumis_sword",
            "Lightsabers/RumiSwordVisual",
            "Lightsabers/RumisSword",
            "Lightsabers/rumis-sword",
            "Lightsabers/Sword_01 (1)",
            "kpop-demon-hunters-rumis-sword",
        };
        foreach (var path in candidates)
        {
            var go = Resources.Load<GameObject>(path);
            if (go != null)
                return go;
        }
        return null;
    }

    private IEnumerator LoadAudio()
    {
        string audioUri = LocalAudioRequestUri.FromFilesystemPath(Songsettings.CurrentSong.AudioFilePath);
        var downloadHandler = new DownloadHandlerAudioClip(audioUri, AudioType.OGGVORBIS);
        downloadHandler.compressed = false;
        downloadHandler.streamAudio = true;
        var uwr = new UnityWebRequest(
                audioUri,
                UnityWebRequest.kHttpVerbGET,
                downloadHandler,
                null);

        var request = uwr.SendWebRequest();
        while (!request.isDone)
            yield return null;

        if (uwr.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[NotesSpawner] Failed to load gameplay audio: {uwr.error} (URI: {audioUri})");
            yield break;
        }

        audioSource.clip = DownloadHandlerAudioClip.GetContent(uwr);
        audioLoaded = true;
    }

    void Update()
    {
        if (GameplayCalibrationGate.BlocksNoteTimeline)
            return;

        var prevBeatsTime = BeatsTime;

        if (BeatsPreloadTime == null)
        {
            if (!audioSource.isPlaying)
            {
                if (!menuLoadInProgress)
                {
                    menuLoadInProgress = true;
                    StartCoroutine(LoadMenu());
                }
                return;
            }

            BeatsTime = (audioSource.time + beatAnticipationTime + beatWarmupTime) * 1000;
        }
        else
        {
            BeatsTime = BeatsPreloadTime.Value;
        }

        double msPerBeat = 1000 * 60 / BeatsPerMinute;

        //Notes
        for (int i = 0; i < NotesToSpawn.Count; ++i)
        {
            var noteTime = NotesToSpawn[i].Time * msPerBeat;
            if (noteTime > prevBeatsTime && noteTime <= BeatsTime)
            {
                NotesToSpawn[i].Time = noteTime;
                GenerateNote(NotesToSpawn[i]);
            }
        }

        //Obstacles
        for (int i = 0; i < ObstaclesToSpawn.Count; ++i)
        {
            var noteTime = ObstaclesToSpawn[i].Time * msPerBeat;
            if (noteTime > prevBeatsTime && noteTime <= BeatsTime)
            {
                ObstaclesToSpawn[i].Time = noteTime;
                GenerateObstacle(ObstaclesToSpawn[i]);
            }
        }

        if (BeatsPreloadTime == null) { return; }

        if (BeatsPreloadTime.Value >= BeatsPreloadTimeTotal)
        {
            if (audioLoaded)
            {
                // Finished preload.
                BeatsPreloadTime = null;
                audioSource.Play();
            }
        }
        else
        {
            // Continue preload.
            BeatsPreloadTime += Time.deltaTime;
        }
    }

    IEnumerator LoadMenu()
    {
        yield return new WaitForSeconds(2);
        yield return FinishRunAndShowResultsRoutine();
    }

    /// <summary>
    /// Stops the song, saves the score to the leaderboard, loads <c>Results</c>, unloads <c>OpenSaber</c>.
    /// Used at natural end-of-song and from the gameplay SKIP button.
    /// </summary>
    public void RequestSkipToResults()
    {
        if (menuLoadInProgress || SceneHandling == null)
            return;
        menuLoadInProgress = true;
        enabled = false;
        if (audioSource != null)
            audioSource.Stop();
        StartCoroutine(FinishRunAndShowResultsRoutine());
    }

    IEnumerator FinishRunAndShowResultsRoutine()
    {
        if (audioSource != null)
            audioSource.Stop();

        var sm = FindAnyObjectByType<ScoreManager>();
        int finalScore = sm != null ? sm.Score : 0;
        string songName = Songsettings != null && Songsettings.CurrentSong != null
            ? Songsettings.CurrentSong.Name : "Unknown";
        string difficulty = Songsettings != null && Songsettings.CurrentSong != null
            ? Songsettings.CurrentSong.SelectedDifficulty : "";

        ResultsSession.PublishRunForResultsScene(finalScore, songName, difficulty);
        yield return SceneHandling.LoadScene("Results", LoadSceneMode.Additive);
        yield return SceneHandling.UnloadScene("OpenSaber");
    }

    void GenerateNote(Note note)
    {
        int point = 0;

        switch (note.LineLayer)
        {
            case 0:
                point = note.LineIndex;
                break;
            case 1:
                point = note.LineIndex + 4;
                break;
            case 2:
                point = note.LineIndex + 8;
                break;
            default:
                break;
        }

        int saberSideForColor = (int)note.Hand; // 0 = red / left, 1 = blue / right (before nondirection index bump)
        if (note.CutDirection == CutDirection.NONDIRECTION)
        {
            // the nondirection cubes are stored at the index+2 in the array
            note.Hand += 2;
        }

        GameObject[] prefabs = (Demons != null && Demons.Length >= 4) ? Demons : GetFallbackCubes();
        if (prefabs == null || prefabs.Length < 4) { Debug.LogWarning("[NotesSpawner] Assign Demons or Cubes (4 prefabs: Left, Right, Left NonDir, Right NonDir)."); return; }
        GameObject demon = Instantiate(prefabs[(int)note.Hand], SpawnPoints[point]);
        demon.transform.localPosition = Vector3.zero;

        float rotation = 0f;

        switch (note.CutDirection)
        {
            case CutDirection.TOP:
                rotation = 0f;
                break;
            case CutDirection.BOTTOM:
                rotation = 180f;
                break;
            case CutDirection.LEFT:
                rotation = 270f;
                break;
            case CutDirection.RIGHT:
                rotation = 90f;
                break;
            case CutDirection.TOPLEFT:
                rotation = 315f;
                break;
            case CutDirection.TOPRIGHT:
                rotation = 45f;
                break;
            case CutDirection.BOTTOMLEFT:
                rotation = 225f;
                break;
            case CutDirection.BOTTOMRIGHT:
                rotation = 125f;
                break;
            case CutDirection.NONDIRECTION:
                rotation = 0f;
                break;
            default:
                break;
        }

        demon.transform.Rotate(transform.forward, rotation);

        var demonHandling = demon.GetComponent<DemonHandling>();
        if (demonHandling == null) demonHandling = demon.AddComponent<DemonHandling>();
        demonHandling.AnticipationPosition = (float) (-beatAnticipationTime * beatSpeed - BeatsConstants.SWORD_OFFSET);
        demonHandling.Speed = (float)beatSpeed;
        demonHandling.WarmUpPosition = -beatWarmupTime * beatWarmupSpeed;

        try { if (demon.CompareTag("Untagged")) demon.tag = "Demon"; } catch { /* Add "Demon" tag in Project Settings if needed */ }

        var missDet = demon.GetComponent<NoteMissDetector>();
        if (missDet == null)
            missDet = demon.AddComponent<NoteMissDetector>();
        missDet.missFlyoutColor = saberSideForColor == 0
            ? new Color(0.98f, 0.22f, 0.36f, 1f)
            : new Color(0.2f, 0.58f, 1f, 1f);

        var spawnedSide = demon.AddComponent<SpawnedNoteSaberSide>();
        spawnedSide.isLeftHandSaber = saberSideForColor == 0;

        var spawnCut = demon.AddComponent<SpawnedNoteCutDirection>();
        spawnCut.CutDirection = note.CutDirection;
    }

    /// <summary>Fallback to Cubes when Demons is empty (e.g. during migration).</summary>
    private GameObject[] GetFallbackCubes()
    {
        return (Cubes != null && Cubes.Length >= 4) ? Cubes : Demons;
    }

    public void GenerateObstacle(Obstacle obstacle)
    {
        double WALL_THICKNESS = 0.5;

        double durationSeconds = 60 * (obstacle.Duration / BeatsPerMinute);

        GameObject wall = Instantiate(Wall, SpawnPoints[obstacle.LineIndex]);

        var wallHandling = wall.GetComponent<ObstacleHandling>();
        wallHandling.AnticipationPosition = (float)(-beatAnticipationTime * beatSpeed - BeatsConstants.SWORD_OFFSET);
        wallHandling.Speed = (float)beatSpeed;
        wallHandling.WarmUpPosition = -beatWarmupTime * beatWarmupSpeed;
        wallHandling.Width = obstacle.Width * WALL_THICKNESS;
        wallHandling.Ceiling = obstacle.Type == ObstacleType.CEILING;
        wallHandling.Duration = obstacle.Duration;

        //wall.transform.localScale = new Vector3((float)wallHandling.Width, wall.transform.localScale.y, wall.transform.localScale.z);
    }

    public class Note
    {
        public double Time { get; set; }
        public double TimeInSeconds { get; set; }
        public int LineIndex { get; set; }
        public int LineLayer { get; set; }
        public NoteType Hand { get; set; }
        public CutDirection CutDirection { get; set; }

        public override bool Equals(object obj)
        {
            return Time == ((Note)obj).Time && LineIndex == ((Note)obj).LineIndex && LineLayer == ((Note)obj).LineLayer;
        }

        public override int GetHashCode()
        {
            var hashCode = -702342995;
            hashCode = hashCode * -1521134295 + Time.GetHashCode();
            hashCode = hashCode * -1521134295 + TimeInSeconds.GetHashCode();
            hashCode = hashCode * -1521134295 + LineIndex.GetHashCode();
            hashCode = hashCode * -1521134295 + LineLayer.GetHashCode();
            hashCode = hashCode * -1521134295 + Hand.GetHashCode();
            hashCode = hashCode * -1521134295 + CutDirection.GetHashCode();
            return hashCode;
        }
    }

    public enum NoteType
    {
        LEFT = 0,
        RIGHT = 1
    }

    public enum CutDirection
    {
        TOP = 1,
        BOTTOM = 0,
        LEFT = 2,
        RIGHT = 3,
        TOPLEFT = 6,
        TOPRIGHT = 7,
        BOTTOMLEFT = 4,
        BOTTOMRIGHT = 5,
        NONDIRECTION = 8
    }

    public class Obstacle
    {
        internal double TimeInSeconds;
        internal double Time;
        internal int LineIndex;
        internal double Duration;
        internal ObstacleType Type;
        internal double Width;
    }

    public enum ObstacleType
    {
        WALL = 0,
        CEILING = 1
    }
}



