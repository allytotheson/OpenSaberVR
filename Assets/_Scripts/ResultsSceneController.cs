using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lives in the authored <c>Results.unity</c> scene. Builds UI from data published via
/// <see cref="ResultsSession"/> before <see cref="NotesSpawner"/> loads this scene additively.
/// </summary>
public class ResultsSceneController : MonoBehaviour
{
    int _finalScore;
    int _cutScore;
    int _bonusScore;
    int _highlightRank = -1;
    string _songName;
    string _difficulty;
    bool _scoreboardBrowseOnly;

    void Awake()
    {
        GameplayCameraEnsurer.Ensure();

        if (ResultsSession.TryConsume(out bool browseOnly, out int score, out int cut, out int bonus, out string sn, out string sd, out int hr))
        {
            _scoreboardBrowseOnly = browseOnly;
            _finalScore = score;
            _cutScore = cut;
            _bonusScore = bonus;
            _songName = sn;
            _difficulty = sd;
            _highlightRank = hr;
        }
        else
        {
            // Opened Results scene directly (e.g. editor) with no prior publish.
            _scoreboardBrowseOnly = false;
            _finalScore = 0;
            _cutScore = 0;
            _bonusScore = 0;
            _songName = "—";
            _difficulty = "";
            _highlightRank = -1;
        }

        BuildUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            GoToMenu();
    }

    void GoToMenu()
    {
        var sh = Object.FindAnyObjectByType<SceneHandling>();
        if (sh != null)
        {
            sh.StartCoroutine(ReturnToMenuViaSceneHandling(sh));
        }
        else
        {
            if (!_scoreboardBrowseOnly && !SceneManager.GetSceneByName("Menu").isLoaded)
                SceneManager.LoadScene("Menu", LoadSceneMode.Additive);
            TryUnloadScene("Results");
        }
    }

    System.Collections.IEnumerator ReturnToMenuViaSceneHandling(SceneHandling sh)
    {
        if (_scoreboardBrowseOnly)
        {
            TryUnloadScene("Results");
            yield break;
        }

        while (sh.IsSceneLoaded("OpenSaber"))
            yield return sh.UnloadScene("OpenSaber");

        if (!sh.IsSceneLoaded("Menu"))
            yield return sh.LoadScene("Menu", LoadSceneMode.Additive);

        TryUnloadScene("Results");

        var mainMenu = Object.FindAnyObjectByType<MainMenu>();
        if (mainMenu != null)
            mainMenu.RestoreUiAfterLeavingGameplay();
    }

    static void TryUnloadScene(string name)
    {
        var scene = SceneManager.GetSceneByName(name);
        if (scene.IsValid() && scene.isLoaded)
            SceneManager.UnloadSceneAsync(name);
    }

    // ---- Procedural UI ----

    void BuildUI()
    {
        Font font = GetDefaultFont();

        var canvasGo = new GameObject("ResultsCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Background
        var bg = MakeImage(canvasGo.transform, "Bg", new Color(0.04f, 0.02f, 0.10f, 1f));
        Stretch(bg);

        string mainTitle = _scoreboardBrowseOnly ? "HIGH SCORES" : "RESULTS";
        MakeText(canvasGo.transform, "Title", font, mainTitle, 48, FontStyle.Bold,
            Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 60f));

        float lbHeaderY = -120f;
        if (!_scoreboardBrowseOnly)
        {
            MakeText(canvasGo.transform, "SongName", font, _songName + "  -  " + _difficulty, 24, FontStyle.Normal,
                new Color(0.7f, 0.7f, 0.8f), new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(800f, 35f));

            MakeText(canvasGo.transform, "CutScoreLabel", font, "CUT SCORE (accuracy)", 22, FontStyle.Normal,
                new Color(0.75f, 0.75f, 0.8f), new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(500f, 30f));
            MakeText(canvasGo.transform, "CutScoreValue", font, _cutScore.ToString("N0"), 34, FontStyle.Bold,
                Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(400f, 40f));

            MakeText(canvasGo.transform, "BonusLabel", font, "COMBO BONUS", 22, FontStyle.Normal,
                new Color(0.75f, 0.75f, 0.8f), new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(500f, 30f));
            MakeText(canvasGo.transform, "BonusValue", font, _bonusScore.ToString("N0"), 34, FontStyle.Bold,
                new Color(0.55f, 0.95f, 0.65f), new Vector2(0.5f, 1f), new Vector2(0f, -251f), new Vector2(400f, 40f));

            MakeText(canvasGo.transform, "TotalLabel", font, "TOTAL SCORE", 26, FontStyle.Normal,
                new Color(0.8f, 0.8f, 0.8f), new Vector2(0.5f, 1f), new Vector2(0f, -295f), new Vector2(400f, 35f));
            MakeText(canvasGo.transform, "TotalValue", font, _finalScore.ToString("N0"), 64, FontStyle.Bold,
                _highlightRank >= 0 && _highlightRank < 3
                    ? new Color(1f, 0.84f, 0f)
                    : Color.white,
                new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(400f, 70f));

            lbHeaderY = -445f;
        }

        MakeText(canvasGo.transform, "LBHeader", font, "TOP 10", 30, FontStyle.Bold,
            new Color(0.9f, 0.7f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0f, lbHeaderY), new Vector2(600f, 40f));

        List<LeaderboardData.Entry> scores = LeaderboardData.GetTopScores();
        float startY = lbHeaderY - 50f;
        float rowHeight = 38f;
        for (int i = 0; i < scores.Count && i < 10; i++)
        {
            bool isHighlight = (i == _highlightRank);
            Color rowColor = isHighlight
                ? new Color(1f, 0.84f, 0f)
                : new Color(0.8f, 0.8f, 0.85f);
            FontStyle style = isHighlight ? FontStyle.Bold : FontStyle.Normal;

            string rank = (i + 1).ToString();
            string entry = $"{rank}.  {scores[i].score,8:N0}   {scores[i].songName}   {scores[i].date}";

            float yPos = startY - i * rowHeight;
            MakeText(canvasGo.transform, "LBRow" + i, font, entry, 22, style,
                rowColor, new Vector2(0.5f, 1f), new Vector2(0f, yPos), new Vector2(700f, 34f));
        }

        if (scores.Count == 0)
        {
            MakeText(canvasGo.transform, "LBEmpty", font, "No scores yet!", 22, FontStyle.Italic,
                new Color(0.6f, 0.6f, 0.6f), new Vector2(0.5f, 1f), new Vector2(0f, startY), new Vector2(400f, 34f));
        }

        // Play Again button
        float btnY = startY - Mathf.Max(scores.Count, 1) * rowHeight - 40f;
        var btnGo = new GameObject("PlayAgainBtn");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 1f);
        btnRt.pivot = new Vector2(0.5f, 1f);
        btnRt.anchoredPosition = new Vector2(0f, btnY);
        btnRt.sizeDelta = new Vector2(300f, 60f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.4f, 0.9f, 1f);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(GoToMenu);

        var colors = btn.colors;
        colors.highlightedColor = new Color(0.25f, 0.55f, 1f);
        colors.pressedColor = new Color(0.1f, 0.3f, 0.7f);
        btn.colors = colors;

        string btnLabel = _scoreboardBrowseOnly ? "BACK" : "PLAY AGAIN";
        MakeText(btnGo.transform, "BtnLabel", font, btnLabel, 28, FontStyle.Bold,
            Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 50f));
    }

    static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static void Stretch(Image img)
    {
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    static Text MakeText(Transform parent, string name, Font font, string content, int size, FontStyle style,
        Color color, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Font GetDefaultFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        return font;
    }
}
