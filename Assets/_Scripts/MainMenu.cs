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
    [Tooltip("Title: START only. Flow screens: compact EXIT at top (back / quit path). Both hidden on quit confirmation.")]
    public GameObject HomeScreenButtonStart;
    public GameObject HomeScreenButtonExit;
    [Tooltip("Overridden from SharedExitButtonLayout if left at default.")]
    public float exitTopOffsetY = SharedExitButtonLayout.TopOffsetY;
    [Tooltip("Local Z for EXIT so it sorts in front of song/difficulty panels.")]
    public float exitForwardLocalZ = SharedExitButtonLayout.ForwardLocalZ;
    [Tooltip("EXIT rect size on flow screens.")]
    public Vector2 exitFlowSizeDelta = SharedExitButtonLayout.SizeDelta;
    [Tooltip("Uniform scale for EXIT on flow screens.")]
    public float exitFlowScale = SharedExitButtonLayout.UniformScale;

    private SongSettings Songsettings;
    private SceneHandling SceneHandling;
    RectTransform _startRt;
    RectTransform _exitRt;

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
        if (HomeScreenButtonExit != null)
            _exitRt = HomeScreenButtonExit.GetComponent<RectTransform>();

        SyncCanvasScalerWithSharedExitLayout();

        if (Title != null && Title.activeSelf)
            SetTitleScreenLayout();
        else if ((SongChooser != null && SongChooser.activeSelf) ||
                 (NoSongsFound != null && NoSongsFound.activeSelf) ||
                 (LevelChooser != null && LevelChooser.activeSelf))
            SetFlowScreenLayout();
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

    void ApplyExitButtonTopBar()
    {
        if (_exitRt == null) return;
        _exitRt.anchorMin = new Vector2(0.5f, 1f);
        _exitRt.anchorMax = new Vector2(0.5f, 1f);
        _exitRt.pivot = new Vector2(0.5f, 1f);
        _exitRt.sizeDelta = exitFlowSizeDelta;
        _exitRt.localScale = new Vector3(exitFlowScale, exitFlowScale, exitFlowScale);
        _exitRt.anchoredPosition = new Vector2(0f, exitTopOffsetY);
        var lp = _exitRt.localPosition;
        _exitRt.localPosition = new Vector3(lp.x, lp.y, exitForwardLocalZ);
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
    }

    /// <summary>Songs / no-songs / difficulty / return-from-gameplay: EXIT at top; no START.</summary>
    void SetFlowScreenLayout()
    {
        ApplyExitButtonTopBar();
        if (HomeScreenButtonStart != null) HomeScreenButtonStart.SetActive(false);
        if (HomeScreenButtonExit != null) HomeScreenButtonExit.SetActive(true);
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

        Songsettings.CurrentSong = SongInfos.AllSongs[SongInfos.CurrentSong];

        Title.gameObject.SetActive(false);
        PanelAreYouSure.gameObject.SetActive(false);
        LevelChooser.gameObject.SetActive(false);
        SongChooser.gameObject.SetActive(true);
        var song = SongInfos.GetCurrentSong();

        SongInfos.SongName.text = song.Name;
        SongInfos.Artist.text = song.AuthorName;
        SongInfos.BPM.text = song.BPM;
        SongInfos.Levels.text = song.Difficulties.Count.ToString();

        SetFlowScreenLayout();

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
        var song = SongInfos.GetCurrentSong();
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
        SongInfos.GetCurrentSong().SelectedDifficulty = difficulty;
        StartCoroutine(LoadSongScene());
    }

    private IEnumerator LoadSongScene()
    {
        yield return SceneHandling.LoadScene("OpenSaber", LoadSceneMode.Additive);
        yield return SceneHandling.UnloadScene("Menu");
    }

    public void AreYouSure()
    {
        SetHomeScreenButtonsVisible(false);
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
    /// EXIT: from flow screens → back toward home or song list. Title has no EXIT button; quit is not offered from the default menu screen.
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
        SetFlowScreenLayout();

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
