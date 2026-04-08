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
    /// <summary>Unity tags on <c>Main_Menu</c> panels (see TagManager + prefab). Used when Inspector references are missing.</summary>
    public const string TagSongChooser = "MenuSongChooser";
    public const string TagTitle = "MenuTitle";
    public const string TagLevelChooser = "MenuLevelChooser";
    public const string TagAreYouSure = "MenuAreYouSure";
    public const string TagNoSongsFound = "MenuNoSongsFound";
    public const string TagHomeStart = "MenuHomeStart";
    public const string TagHomeExit = "MenuHomeExit";
    public const string TagLevelButtonTemplate = "MenuLevelButtonTemplate";

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
    GameObject _titleScoreboardBtnRoot;

    AudioClip PreviewAudioClip = null;
    bool PlayNewPreview = false;

    private void Awake()
    {
        ResolveMainMenuUiReferences();

        Songsettings = UnityEngine.Object.FindAnyObjectByType<SongSettings>(FindObjectsInactive.Include);
        if (Songsettings == null)
        {
            var songGo = GameObject.FindGameObjectWithTag("SongSettings");
            if (songGo != null)
                Songsettings = songGo.GetComponent<SongSettings>();
        }
        if (Songsettings == null)
            Debug.LogError(
                "[MainMenu] SongSettings not found. Enter play mode from PersistentScene (build index 0) so SongSettings exists, " +
                "or add SongSettings to this scene.");

        SceneHandling = UnityEngine.Object.FindAnyObjectByType<SceneHandling>(FindObjectsInactive.Include);
        if (SceneHandling == null)
        {
            var shGo = GameObject.FindGameObjectWithTag("SceneHandling");
            if (shGo != null)
                SceneHandling = shGo.GetComponent<SceneHandling>();
        }
        if (SceneHandling == null)
            Debug.LogError(
                "[MainMenu] SceneHandling not found. Enter play mode from PersistentScene (build index 0), " +
                "or add SceneHandling to this scene.");

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

    void Start()
    {
        SyncDesktopMenuCameraToCurrentPanel();
    }

    void SyncDesktopMenuCameraToCurrentPanel()
    {
        var rig = DesktopPlayerViewRig.FindInstance();
        if (rig == null)
            return;
        if (Title != null && Title.activeSelf)
            rig.SetMenuCameraMode(DesktopMenuCameraMode.TitleFloor, animated: false);
        else if (UsesSongBrowseMenuCamera())
            rig.SetMenuCameraMode(DesktopMenuCameraMode.SongBrowse, animated: false);
    }

    bool UsesSongBrowseMenuCamera()
    {
        return (SongChooser != null && SongChooser.activeSelf)
               || (LevelChooser != null && LevelChooser.activeSelf)
               || (NoSongsFound != null && NoSongsFound.activeSelf)
               || (PanelAreYouSure != null && PanelAreYouSure.activeSelf);
    }

    void ApplyDesktopMenuCamera(DesktopMenuCameraMode mode, bool animated)
    {
        var rig = DesktopPlayerViewRig.FindInstance();
        if (rig != null)
            rig.SetMenuCameraMode(mode, animated);
    }

    /// <summary>Fills missing serialized references from tags or direct children of this canvas (same names as in Main_Menu prefab).</summary>
    void ResolveMainMenuUiReferences()
    {
        Transform root = transform;

        if (SongChooser == null)
            SongChooser = ResolveByTagOrChildName(root, TagSongChooser, "SongChooser");
        if (Title == null)
            Title = ResolveByTagOrChildName(root, TagTitle, "Title");
        if (LevelChooser == null)
            LevelChooser = ResolveByTagOrChildName(root, TagLevelChooser, "DifficultChooser");
        if (PanelAreYouSure == null)
            PanelAreYouSure = ResolveByTagOrChildName(root, TagAreYouSure, "AreYouSurePanel");
        if (NoSongsFound == null)
            NoSongsFound = ResolveByTagOrChildName(root, TagNoSongsFound, "NoSongsFound");
        if (LevelButtonTemplate == null)
            LevelButtonTemplate = ResolveByTagOrChildName(root, TagLevelButtonTemplate, "ButtonTemplate");
        if (HomeScreenButtonStart == null)
            HomeScreenButtonStart = ResolveByTagOrChildName(root, TagHomeStart, "Btn_Start");
        if (HomeScreenButtonExit == null)
            HomeScreenButtonExit = ResolveByTagOrChildName(root, TagHomeExit, "Btn_Exit");

        if (SongInfos == null)
            SongInfos = GetComponent<LoadSongInfos>();
        if (SongPreview == null)
            SongPreview = GetComponent<AudioSource>();
    }

    static GameObject ResolveByTagOrChildName(Transform canvasRoot, string tag, string childName)
    {
        Transform t = canvasRoot.Find(childName);
        if (t != null)
            return t.gameObject;

        if (!string.IsNullOrEmpty(tag))
        {
            try
            {
                var byTag = GameObject.FindGameObjectWithTag(tag);
                if (byTag != null)
                    return byTag;
            }
            catch (UnityException)
            {
                // Tag not defined in Tag Manager.
            }
        }

        return null;
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
        _startRt.localPosition = new Vector3(lp.x, lp.y, 0.15f);
    }

    /// <summary>Title: large START only at bottom center; no EXIT.</summary>
    void SetTitleScreenLayout()
    {
        ApplyStartTitleBottomCenter();
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(true);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(false);
        EnsureTitleScoreboardButton();
        if (_titleScoreboardBtnRoot != null)
            _titleScoreboardBtnRoot.SetActive(true);
    }

    /// <summary>No songs: no START, no on-screen EXIT (Esc / Backspace still backs out).</summary>
    void SetFlowScreenLayout()
    {
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(false);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(false);
        if (_titleScoreboardBtnRoot != null)
            _titleScoreboardBtnRoot.SetActive(false);
    }

    /// <summary>Song browser: screen-space EXIT top center (matches OpenSaber HUD).</summary>
    void SetSongChooserScreenLayout()
    {
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(false);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(true);
        if (_titleScoreboardBtnRoot != null)
            _titleScoreboardBtnRoot.SetActive(false);
    }

    /// <summary>Difficulty list: same screen-space EXIT as song browser.</summary>
    void SetLevelChooserScreenLayout()
    {
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(false);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(false);
        SetMenuExitOverlayVisible(true);
        if (_titleScoreboardBtnRoot != null)
            _titleScoreboardBtnRoot.SetActive(false);
    }

    void EnsureTitleScoreboardButton()
    {
        if (_titleScoreboardBtnRoot != null || Title == null)
            return;

        Font font = MenuExitScreenHud.ResolveMenuFont();
        _titleScoreboardBtnRoot = new GameObject("Btn_Scoreboard");
        _titleScoreboardBtnRoot.transform.SetParent(Title.transform, false);
        var rt = _titleScoreboardBtnRoot.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -420f);
        rt.sizeDelta = new Vector2(300f, 56f);

        var img = _titleScoreboardBtnRoot.AddComponent<Image>();
        img.color = new Color(0.12f, 0.18f, 0.32f, 0.95f);

        var btn = _titleScoreboardBtnRoot.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.2f, 0.35f, 0.55f, 1f);
        colors.pressedColor = new Color(0.08f, 0.12f, 0.22f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(OpenScoreboardFromTitle);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(_titleScoreboardBtnRoot.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6f, 4f);
        textRt.offsetMax = new Vector2(-6f, -4f);
        var label = textGo.AddComponent<Text>();
        if (font != null) label.font = font;
        label.fontSize = 28;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(0.85f, 0.9f, 1f, 1f);
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.text = "SCOREBOARD";
    }

    /// <summary>Title screen: opens <c>Results</c> in browse mode (top 10 only, no new score row).</summary>
    public void OpenScoreboardFromTitle()
    {
        if (SceneHandling == null)
            return;
        StartCoroutine(OpenScoreboardFromTitleRoutine());
    }

    IEnumerator OpenScoreboardFromTitleRoutine()
    {
        var existing = SceneManager.GetSceneByName("Results");
        if (existing.IsValid() && existing.isLoaded)
            yield break;

        ResultsSession.PublishScoreboardBrowse();
        yield return SceneHandling.LoadScene("Results", LoadSceneMode.Additive);
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
        ApplyDesktopMenuCamera(DesktopMenuCameraMode.SongBrowse, animated: true);

        if (SongInfos == null || SongInfos.AllSongs == null || SongInfos.AllSongs.Count == 0)
        {
            if (Title != null) Title.SetActive(false);
            if (NoSongsFound != null) NoSongsFound.SetActive(true);
            SetFlowScreenLayout();
            return;
        }

        EnsureCurrentSongReference();

        if (Title != null) Title.SetActive(false);
        if (PanelAreYouSure != null) PanelAreYouSure.SetActive(false);
        if (LevelChooser != null) LevelChooser.SetActive(false);
        if (SongChooser != null) SongChooser.SetActive(true);

        SetSongChooserScreenLayout();

        var song = SongInfos.GetCurrentSong();
        if (song == null)
        {
            Debug.LogError("[MainMenu] No current song after EnsureCurrentSongReference.");
            return;
        }

        SongInfos.BindSongChooserRows(song);

        byte[] byteArray = File.ReadAllBytes(song.CoverImagePath);
        Texture2D sampleTexture = new Texture2D(2, 2);
        bool isLoaded = sampleTexture.LoadImage(byteArray);

        if (isLoaded && SongInfos.Cover != null)
            SongInfos.Cover.texture = sampleTexture;

        if (Songsettings != null && Songsettings.CurrentSong != null)
            StartCoroutine(PreviewSong(Songsettings.CurrentSong.AudioFilePath));
    }

    public IEnumerator PreviewSong(string audioFilePath)
    {
        if (SongPreview != null)
            SongPreview.Stop();
        PreviewAudioClip = null;
        PlayNewPreview = true;

        yield return null;

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
        while(!request.isDone)
            yield return null;

        if (uwr.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[MainMenu] Failed to load preview audio: {uwr.error} (URI: {audioUri})");
            yield break;
        }

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
        if (PreviewAudioClip != null && PlayNewPreview && SongPreview != null)
        {
            PlayNewPreview = false;
            SongPreview.Stop();
            SongPreview.clip = PreviewAudioClip;
            // Skip intro (40s) when possible; clamp so we never seek past clip length (invalid seek in FMOD).
            float clipLen = PreviewAudioClip.length;
            float maxSeek = clipLen > 0.002f ? clipLen - 0.001f : 0f;
            SongPreview.time = Mathf.Clamp(40f, 0f, maxSeek);
            SongPreview.Play();
        }
    }

    public void NextSong()
    {
        var song = SongInfos.NextSong();
        if (song == null)
            return;

        SongInfos.BindSongChooserRows(song);

        byte[] byteArray = File.ReadAllBytes(song.CoverImagePath);
        Texture2D sampleTexture = new Texture2D(2, 2);
        bool isLoaded = sampleTexture.LoadImage(byteArray);

        if (isLoaded && SongInfos.Cover != null)
            SongInfos.Cover.texture = sampleTexture;

        if (Songsettings != null && Songsettings.CurrentSong != null)
            StartCoroutine(PreviewSong(Songsettings.CurrentSong.AudioFilePath));
    }

    public void PreviousSong()
    {
        var song = SongInfos.PreviousSong();
        if (song == null)
            return;

        SongInfos.BindSongChooserRows(song);

        byte[] byteArray = File.ReadAllBytes(song.CoverImagePath);
        Texture2D sampleTexture = new Texture2D(2, 2);
        bool isLoaded = sampleTexture.LoadImage(byteArray);

        if (isLoaded && SongInfos.Cover != null)
            SongInfos.Cover.texture = sampleTexture;

        if (Songsettings != null && Songsettings.CurrentSong != null)
            StartCoroutine(PreviewSong(Songsettings.CurrentSong.AudioFilePath));
    }

    public void LoadSong()
    {
        if (SongPreview != null)
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
        if (SceneHandling == null)
        {
            Debug.LogError("[MainMenu] Cannot load OpenSaber: SceneHandling is missing.");
            yield break;
        }

        EnsureCurrentSongReference();

        yield return SceneHandling.LoadScene("OpenSaber", LoadSceneMode.Additive);
        yield return SceneHandling.UnloadScene("Menu");
    }

    public void AreYouSure()
    {
        ApplyDesktopMenuCamera(DesktopMenuCameraMode.SongBrowse, animated: true);
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
        ApplyDesktopMenuCamera(DesktopMenuCameraMode.TitleFloor, animated: true);
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
        ApplyDesktopMenuCamera(DesktopMenuCameraMode.TitleFloor, animated: true);
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
        ApplyDesktopMenuCamera(DesktopMenuCameraMode.SongBrowse, animated: false);

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

        SongInfos.BindSongChooserRows(song);

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
