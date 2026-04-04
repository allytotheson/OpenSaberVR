using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject SongChooser;
    public LoadSongInfos SongInfos;
    public GameObject PanelAreYouSure;
    public GameObject LevelChooser;
    public GameObject LevelButtonTemplate;
    public GameObject Title;
    public GameObject NoSongsFound;
    public AudioSource SongPreview;
    [Tooltip("Title: START only. Song/difficulty: screen-space EXIT (same as OpenSaber HUD). World-space Btn_Exit is hidden.")]
    public GameObject HomeScreenButtonStart;
    public GameObject HomeScreenButtonExit;

    private SongSettings Songsettings;
    private SceneHandling SceneHandling;
    RectTransform _startRt;
    GameObject _menuExitHudRoot;
    GameObject _menuExitBar;

    AudioClip PreviewAudioClip = null;
    bool PlayNewPreview = false;

    private void Awake()
    {
        Songsettings = GameObject.FindGameObjectWithTag("SongSettings").GetComponent<SongSettings>();
        SceneHandling = GameObject.FindGameObjectWithTag("SceneHandling").GetComponent<SceneHandling>();

        var cv = GetComponent<Canvas>();
        if (cv != null && cv.worldCamera == null && Camera.main != null)
            cv.worldCamera = Camera.main;

        if (HomeScreenButtonStart != null)
            _startRt = HomeScreenButtonStart.GetComponent<RectTransform>();

        SyncCanvasScalerWithSharedExitLayout();

        if (Title != null && Title.activeSelf)
            SetTitleScreenLayout();
        else if (SongChooser != null && SongChooser.activeSelf)
            SetSongChooserScreenLayout();
        else if (NoSongsFound != null && NoSongsFound.activeSelf)
            SetFlowScreenLayout();
        else if (LevelChooser != null && LevelChooser.activeSelf)
            SetLevelChooserScreenLayout();
    }

    void SyncCanvasScalerWithSharedExitLayout()
    {
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(
            SharedExitButtonLayout.ReferenceResolutionX,
            SharedExitButtonLayout.ReferenceResolutionY);
        scaler.matchWidthOrHeight = SharedExitButtonLayout.MatchWidthOrHeight;
    }

    void SetHomeScreenButtonsVisible(bool visible)
    {
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(visible);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(visible);
    }

    void EnsureMenuExitOverlay()
    {
        if (_menuExitHudRoot != null)
            return;

        Transform parent = transform.parent != null ? transform.parent : transform;
        _menuExitHudRoot = MenuExitScreenHud.CreateOverlayCanvasRoot(parent, "MenuFlowExitHud");
        Font font = MenuExitScreenHud.ResolveMenuFont();
        _menuExitBar = MenuExitScreenHud.CreateExitBar(_menuExitHudRoot.transform, font, ExitApplication);
        _menuExitBar.SetActive(false);
    }

    void SetMenuExitOverlayVisible(bool visible)
    {
        if (!visible)
        {
            if (_menuExitBar != null)
                _menuExitBar.SetActive(false);
            return;
        }

        EnsureMenuExitOverlay();
        if (_menuExitBar != null)
            _menuExitBar.SetActive(true);
    }

    void ApplyStartTitleBottomCenter()
    {
        if (_startRt == null) return;
        _startRt.anchorMin = new Vector2(0.5f, 0.5f);
        _startRt.anchorMax = new Vector2(0.5f, 0.5f);
        _startRt.pivot = new Vector2(0.5f, 0.5f);
        _startRt.anchoredPosition = new Vector2(0f, -298f);
        var lp = _startRt.localPosition;
        _startRt.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    /// <summary>Title: large START only at bottom center; no EXIT.</summary>
    void SetTitleScreenLayout()
    {
        ApplyStartTitleBottomCenter();
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(true);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(false);
    }

    /// <summary>No songs: no START, no on-screen EXIT (Esc / Backspace still backs out).</summary>
    void SetFlowScreenLayout()
    {
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(false);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(false);
    }

    /// <summary>Song browser: screen-space EXIT top center (matches OpenSaber HUD).</summary>
    void SetSongChooserScreenLayout()
    {
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(false);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(true);
    }

    /// <summary>Difficulty list: same screen-space EXIT as song browser.</summary>
    void SetLevelChooserScreenLayout()
    {
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(false);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(true);
    }

    /// <summary>
    /// Assigns <see cref="SongSettings.CurrentSong"/> from <see cref="LoadSongInfos.AllSongs"/> using <see cref="SongSettings.CurrentSongIndex"/>.
    /// Call before any path that loads OpenSaber or reads the current row (handles list rebuilds in <see cref="LoadSongInfos.OnEnable"/>).
    /// </summary>
    void EnsureCurrentSongReference()
    {
        if (Songsettings == null || SongInfos == null || SongInfos.AllSongs == null || SongInfos.AllSongs.Count == 0)
            return;
        int idx = Mathf.Clamp(Songsettings.CurrentSongIndex, 0, SongInfos.AllSongs.Count - 1);
        Songsettings.CurrentSong = SongInfos.AllSongs[idx];
    }

    public void ShowSongs()
    {
        if (SongInfos.AllSongs.Count == 0)
        {
            Title.gameObject.SetActive(false);
            NoSongsFound.gameObject.SetActive(true);
            SetFlowScreenLayout();
            return;
        }

        EnsureCurrentSongReference();

        Title.gameObject.SetActive(false);
        PanelAreYouSure.gameObject.SetActive(false);
        LevelChooser.gameObject.SetActive(false);
        SongChooser.gameObject.SetActive(true);
        var song = SongInfos.GetCurrentSong();

        SongInfos.SongName.text = song.Name;
        SongInfos.Artist.text = song.AuthorName;
        SongInfos.BPM.text = song.BPM;
        SongInfos.Levels.text = song.Difficulties.Count.ToString();

        SetSongChooserScreenLayout();

        byte[] byteArray = File.ReadAllBytes(song.CoverImagePath);
        Texture2D sampleTexture = new Texture2D(2, 2);
        bool isLoaded = sampleTexture.LoadImage(byteArray);

        if (isLoaded)
        {
            SongInfos.Cover.texture = sampleTexture;
        }

        StartCoroutine(PreviewSong(Songsettings.CurrentSong.AudioFilePath));
    }

    public IEnumerator PreviewSong(string audioFilePath)
    {
        SongPreview.Stop();
        PreviewAudioClip = null;
        PlayNewPreview = true;

        yield return null;

        var downloadHandler = new DownloadHandlerAudioClip(Songsettings.CurrentSong.AudioFilePath, AudioType.OGGVORBIS);
        downloadHandler.compressed = false;
        downloadHandler.streamAudio = true;
        var uwr = new UnityWebRequest(
                Songsettings.CurrentSong.AudioFilePath,
                UnityWebRequest.kHttpVerbGET,
                downloadHandler,
                null);

        var request = uwr.SendWebRequest();
        while(!request.isDone)
            yield return null;

        PreviewAudioClip = DownloadHandlerAudioClip.GetContent(uwr);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            if (PanelAreYouSure != null && PanelAreYouSure.activeSelf)
            {
                No();
                return;
            }
            if (Title != null && Title.activeSelf)
                return;
            if ((SongChooser != null && SongChooser.activeSelf) ||
                (NoSongsFound != null && NoSongsFound.activeSelf) ||
                (LevelChooser != null && LevelChooser.activeSelf))
                ExitApplication();
        }
    }

    private void FixedUpdate()
    {
        if (PreviewAudioClip != null && PlayNewPreview)
        {
            PlayNewPreview = false;
            SongPreview.Stop();
            SongPreview.clip = PreviewAudioClip;
            SongPreview.time = 40f;
            SongPreview.Play();
        }
    }

    public void NextSong()
    {
        var song = SongInfos.NextSong();

        SongInfos.SongName.text = song.Name;
        SongInfos.Artist.text = song.AuthorName;
        SongInfos.BPM.text = song.BPM;
        SongInfos.Levels.text = song.Difficulties.Count.ToString();

        byte[] byteArray = File.ReadAllBytes(song.CoverImagePath);
        Texture2D sampleTexture = new Texture2D(2, 2);
        bool isLoaded = sampleTexture.LoadImage(byteArray);

        if (isLoaded)
        {
            SongInfos.Cover.texture = sampleTexture;
        }

        StartCoroutine(PreviewSong(Songsettings.CurrentSong.AudioFilePath));
    }

    public void PreviousSong()
    {
        var song = SongInfos.PreviousSong();

        SongInfos.SongName.text = song.Name;
        SongInfos.Artist.text = song.AuthorName;
        SongInfos.BPM.text = song.BPM;
        SongInfos.Levels.text = song.Difficulties.Count.ToString();

        byte[] byteArray = File.ReadAllBytes(song.CoverImagePath);
        Texture2D sampleTexture = new Texture2D(2, 2);
        bool isLoaded = sampleTexture.LoadImage(byteArray);

        if (isLoaded)
        {
            SongInfos.Cover.texture = sampleTexture;
        }

        StartCoroutine(PreviewSong(Songsettings.CurrentSong.AudioFilePath));
    }

    public void LoadSong()
    {
        SongPreview.Stop();
        EnsureCurrentSongReference();
        var song = SongInfos.GetCurrentSong();
        if (song == null)
        {
            Debug.LogError("[MainMenu] No current song. Open the song list from the title screen first.");
            return;
        }

        if(song.Difficulties.Count > 1)
        {
            foreach (var gameObj in LevelChooser.GetComponentsInChildren<Button>(true))
            {
                if (gameObj.gameObject.name == "ButtonTemplate")
                    continue;

                Destroy(gameObj.gameObject);
            }

            SongChooser.gameObject.SetActive(false);
            PanelAreYouSure.gameObject.SetActive(false);
            LevelChooser.gameObject.SetActive(true);
            SetLevelChooserScreenLayout();

            var buttonsCreated = new List<GameObject>();

            foreach (var difficulty in song.Difficulties)
            {
                var button = GameObject.Instantiate(LevelButtonTemplate, LevelChooser.transform);

                button.GetComponentInChildren<Text>().text = difficulty;
                button.GetComponentInChildren<Button>().onClick.AddListener(() => StartSceneWithDifficulty(difficulty));
                button.SetActive(true);
                buttonsCreated.Add(button);
            }

            switch (buttonsCreated.Count)
            {
                case 2:
                    buttonsCreated[0].GetComponent<RectTransform>().localPosition = new Vector3(-287, buttonsCreated[0].GetComponent<RectTransform>().localPosition.y);
                    buttonsCreated[1].GetComponent<RectTransform>().localPosition = new Vector3(287, buttonsCreated[1].GetComponent<RectTransform>().localPosition.y);
                    break;
                case 3:
                    buttonsCreated[0].GetComponent<RectTransform>().localPosition = new Vector3(-287, buttonsCreated[0].GetComponent<RectTransform>().position.y);
                    buttonsCreated[1].GetComponent<RectTransform>().localPosition = new Vector3(0, buttonsCreated[1].GetComponent<RectTransform>().position.y);
                    buttonsCreated[2].GetComponent<RectTransform>().localPosition = new Vector3(287, buttonsCreated[2].GetComponent<RectTransform>().position.y);
                    break;
                case 4:
                    buttonsCreated[0].GetComponent<RectTransform>().localPosition = new Vector3(-430, buttonsCreated[0].GetComponent<RectTransform>().localPosition.y);
                    buttonsCreated[1].GetComponent<RectTransform>().localPosition = new Vector3(-144, buttonsCreated[1].GetComponent<RectTransform>().localPosition.y);
                    buttonsCreated[2].GetComponent<RectTransform>().localPosition = new Vector3(144, buttonsCreated[2].GetComponent<RectTransform>().localPosition.y);
                    buttonsCreated[3].GetComponent<RectTransform>().localPosition = new Vector3(430, buttonsCreated[3].GetComponent<RectTransform>().localPosition.y);
                    break;
                default:
                    break;
            }
        }
        else
        {
            StartSceneWithDifficulty(song.Difficulties[0]);
        }
    }

    private void StartSceneWithDifficulty(string difficulty)
    {
        EnsureCurrentSongReference();
        var song = SongInfos.GetCurrentSong();
        if (song == null)
        {
            Debug.LogError("[MainMenu] No current song to play. Open the song list from the title screen first, or add maps under Assets/Playlists.");
            return;
        }

        song.SelectedDifficulty = difficulty;
        StartCoroutine(LoadSongScene());
    }

    private IEnumerator LoadSongScene()
    {
        EnsureCurrentSongReference();

        // IMU receivers live in OpenSaber — show calibration after load if any receiver exists (non-VR).
        GameplayCalibrationGate.BlocksNoteTimeline = true;
        yield return SceneHandling.LoadScene("OpenSaber", LoadSceneMode.Additive);

        bool anyImuComponent = Object.FindAnyObjectByType<SerialSaberReceiver>() != null
                               || Object.FindAnyObjectByType<UDPSaberReceiver>() != null;
        if (anyImuComponent && !GameplayCameraEnsurer.IsXrDeviceActive())
            CalibrationSceneBootstrap.EnsureCalibrationScene();
        else
            GameplayCalibrationGate.BlocksNoteTimeline = false;

        yield return SceneHandling.UnloadScene("Menu");
    }

    public void AreYouSure()
    {
        SetHomeScreenButtonsVisible(false);
        SetMenuExitOverlayVisible(false);
        NoSongsFound.gameObject.SetActive(false);
        Title.gameObject.SetActive(false);
        SongChooser.gameObject.SetActive(false);
        LevelChooser.gameObject.SetActive(false);
        PanelAreYouSure.gameObject.SetActive(true);
    }

    public void No()
    {
        PanelAreYouSure.gameObject.SetActive(false);
        Title.gameObject.SetActive(true);
        SetTitleScreenLayout();
    }

    public void Yes()
    {
        QuitNow();
    }

    /// <summary>
    /// From flow screens: back toward home or song list (Esc / Backspace, UDP, or legacy references). Title screen does not invoke quit.
    /// </summary>
    public void ExitApplication()
    {
        if (PanelAreYouSure != null && PanelAreYouSure.activeSelf)
            return;

        if (Title != null && Title.activeSelf)
            return;

        if (SongChooser != null && SongChooser.activeSelf)
        {
            BackToTitleScreen();
            return;
        }

        if (NoSongsFound != null && NoSongsFound.activeSelf)
        {
            BackToTitleScreen();
            return;
        }

        if (LevelChooser != null && LevelChooser.activeSelf)
        {
            BackToSongListFromDifficulty();
            return;
        }

        BackToTitleScreen();
    }

    void BackToTitleScreen()
    {
        if (SongPreview != null)
            SongPreview.Stop();
        PlayNewPreview = false;
        PreviewAudioClip = null;

        if (SongChooser != null) SongChooser.SetActive(false);
        if (LevelChooser != null) LevelChooser.SetActive(false);
        if (PanelAreYouSure != null) PanelAreYouSure.SetActive(false);
        if (NoSongsFound != null) NoSongsFound.SetActive(false);
        if (Title != null) Title.SetActive(true);
        SetTitleScreenLayout();
    }

    void BackToSongListFromDifficulty()
    {
        RestoreUiAfterLeavingGameplay();
    }

    /// <summary>Called when leaving <c>OpenSaber</c> so the player returns to the song browser.</summary>
    public void RestoreUiAfterLeavingGameplay()
    {
        if (LevelChooser != null) LevelChooser.SetActive(false);
        if (PanelAreYouSure != null) PanelAreYouSure.SetActive(false);
        if (SongChooser != null) SongChooser.SetActive(true);
        if (Title != null) Title.SetActive(false);
        SetSongChooserScreenLayout();

        if (SongPreview != null)
            SongPreview.Stop();
        PlayNewPreview = false;
        PreviewAudioClip = null;

        RefreshSongChooserTextsAndCover();

        if (Songsettings != null && Songsettings.CurrentSong != null)
            StartCoroutine(PreviewSong(Songsettings.CurrentSong.AudioFilePath));
    }

    /// <summary>
    /// After the Menu scene loads again, <see cref="RawImage.texture"/> is a new UI element with no texture (reads as white).
    /// Rebind the current song row from <see cref="SongSettings"/> and the rebuilt <see cref="LoadSongInfos.AllSongs"/> list.
    /// </summary>
    void RefreshSongChooserTextsAndCover()
    {
        if (SongInfos == null || Songsettings == null || SongInfos.AllSongs.Count == 0)
            return;

        int idx = Mathf.Clamp(Songsettings.CurrentSongIndex, 0, SongInfos.AllSongs.Count - 1);
        Songsettings.CurrentSong = SongInfos.AllSongs[idx];
        Song song = SongInfos.AllSongs[idx];

        SongInfos.SongName.text = song.Name;
        SongInfos.Artist.text = song.AuthorName;
        SongInfos.BPM.text = song.BPM;
        SongInfos.Levels.text = song.Difficulties.Count.ToString();

        if (SongInfos.Cover == null || string.IsNullOrEmpty(song.CoverImagePath) || !File.Exists(song.CoverImagePath))
            return;

        var old = SongInfos.Cover.texture;
        byte[] bytes = File.ReadAllBytes(song.CoverImagePath);
        var tex = new Texture2D(2, 2);
        if (tex.LoadImage(bytes))
        {
            SongInfos.Cover.texture = tex;
            if (old != null)
                Destroy(old);
        }
    }

    private void QuitNow()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
