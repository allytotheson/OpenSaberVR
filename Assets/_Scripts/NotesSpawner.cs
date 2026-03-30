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
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

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
        Songsettings = GameObject.FindGameObjectWithTag("SongSettings").GetComponent<SongSettings>();
        SceneHandling = GameObject.FindGameObjectWithTag("SceneHandling").GetComponent<SceneHandling>();
        string path = Songsettings.CurrentSong.Path;
        if (Directory.Exists(path))
        {
            if (Directory.GetFiles(path, "info.dat").Length > 0)
            {
                JSONObject infoFile = JSONObject.Parse(File.ReadAllText(Path.Combine(path, "info.dat")));
                TryResolveBeatmap(infoFile, path, Songsettings.CurrentSong.SelectedDifficulty, out audioFilePath, out jsonString);
            }
        }

        audioSource = GetComponent<AudioSource>();

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

        GameplayDebugHud.EnsureCreated(transform);
        if (FindAnyObjectByType<SaberNearestBlockAlignmentProvider>() == null)
            gameObject.AddComponent<SaberNearestBlockAlignmentProvider>();
    }

    private IEnumerator LoadAudio()
    {
        var downloadHandler = new DownloadHandlerAudioClip(Songsettings.CurrentSong.AudioFilePath, AudioType.OGGVORBIS);
        downloadHandler.compressed = false;
        downloadHandler.streamAudio = true;
        var uwr = new UnityWebRequest(
                Songsettings.CurrentSong.AudioFilePath,
                UnityWebRequest.kHttpVerbGET,
                downloadHandler,
                null);

        var request = uwr.SendWebRequest();
        while (!request.isDone)
            yield return null;

        audioSource.clip = DownloadHandlerAudioClip.GetContent(uwr);
        audioLoaded = true;
    }

    void Update()
    {
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
        yield return new WaitForSeconds(5);

        yield return SceneHandling.LoadScene("Menu", LoadSceneMode.Additive);
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

        if (demon.GetComponent<NoteMissDetector>() == null)
            demon.AddComponent<NoteMissDetector>();
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



