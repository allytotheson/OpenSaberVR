using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen overlay: single transparent purple glass panel (no stacked Shadow layers), single-column top-10 list.
/// Layout uses top-anchored negative Y so all content stays inside the glass (Unity UI: positive Y from top anchor pushes outside).
/// Editor: OpenSaber / UI / Add Scoreboard Overlay to Scene (editable, not play) spawns the same hierarchy for layout edits without Play mode.
/// </summary>
public static class ScoreboardOverlayUI
{
    public const int SortingOrder = 400;

    /// <summary>Single panel fill — transparent purple glass (no duplicate opaque layers).</summary>
    static readonly Color GlassFill = new Color(0.14f, 0.06f, 0.26f, 0.36f);
    static readonly Color InnerScoreBg = new Color(0.11f, 0.05f, 0.2f, 0.65f);
    static readonly Color TitleAccent = new Color(0.9f, 0.78f, 1f, 1f);
    static readonly Color BorderAccent = new Color(0.72f, 0.48f, 1f, 0.92f);

    /// <param name="forAuthoringPreview">
    /// When true (editor tooling only), skips <see cref="ScoreboardOverlayDriver"/> so the overlay stays in the scene
    /// for layout edits; keyboard / button dismiss are not wired.
    /// </param>
    public static GameObject Build(
        Transform parent,
        bool browseOnly,
        int finalScore,
        int cutScore,
        int bonusScore,
        string songName,
        string difficulty,
        int highlightRank,
        Action onDismiss,
        bool forAuthoringPreview = false)
    {
        _ = cutScore;
        _ = bonusScore;

        Font font = MenuExitScreenHud.ResolveMenuFont();
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var root = MenuExitScreenHud.CreateOverlayCanvasRoot(parent, "ScoreboardOverlay");
        var canvas = root.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = SortingOrder;

        ScoreboardOverlayDriver driver = null;
        if (!forAuthoringPreview)
        {
            driver = root.AddComponent<ScoreboardOverlayDriver>();
            driver.Bind(browseOnly, highlightRank, onDismiss);
        }

        var dim = NewImage(root.transform, "Dim", new Color(0.02f, 0f, 0.06f, 0.42f));
        StretchFull(dim);

        // Single transparent panel + outline only (Unity UI Shadow draws offset copies — removed to avoid ghosted layers).
        var glass = NewImage(root.transform, "Glass", GlassFill);
        var glassRt = glass.GetComponent<RectTransform>();
        glassRt.anchorMin = glassRt.anchorMax = new Vector2(0.5f, 0.5f);
        glassRt.sizeDelta = new Vector2(1136f, 936f);

        var glassOutline = glass.gameObject.AddComponent<Outline>();
        glassOutline.effectColor = BorderAccent;
        glassOutline.effectDistance = new Vector2(2.5f, -2.5f);

        // Content stacks from top — anchor top, negative Y moves down into panel
        float y = 36f;

        string mainTitle = browseOnly ? "HIGH SCORES" : "RESULTS";
        MakeTextTop(glass.transform, "Title", font, mainTitle, 46, FontStyle.Bold,
            TitleAccent, -y, 920f, 56f);
        y += 58f;

        if (!browseOnly)
        {
            MakeTextTop(glass.transform, "SongName", font, songName + "  —  " + difficulty, 21, FontStyle.Normal,
                new Color(0.82f, 0.72f, 0.95f), -y, 960f, 30f);
            y += 36f;

            // Dark inner strip for total
            var totalBg = NewImage(glass.transform, "TotalBg", InnerScoreBg);
            var totalBgRt = totalBg.GetComponent<RectTransform>();
            totalBgRt.anchorMin = new Vector2(0.5f, 1f);
            totalBgRt.anchorMax = new Vector2(0.5f, 1f);
            totalBgRt.pivot = new Vector2(0.5f, 1f);
            totalBgRt.anchoredPosition = new Vector2(0f, -y);
            totalBgRt.sizeDelta = new Vector2(960f, 108f);

            MakeTextTop(glass.transform, "TotalLabel", font, "TOTAL SCORE", 20, FontStyle.Normal,
                new Color(0.75f, 0.78f, 0.88f), -y - 8f, 400f, 26f);
            MakeTextTop(glass.transform, "TotalValue", font, finalScore.ToString("N0"), 48, FontStyle.Bold,
                highlightRank >= 0 && highlightRank < 3
                    ? new Color(1f, 0.88f, 0.35f)
                    : TitleAccent,
                -y - 44f, 520f, 64f);
            y += 120f;
        }
        else
        {
            y += 8f;
        }

        MakeTextTop(glass.transform, "LBHeader", font, "TOP 10", 26, FontStyle.Bold,
            new Color(0.82f, 0.65f, 1f), -y, 600f, 36f);
        y += 40f;

        // Scroll region: keeps rows inside glass; button stays below
        float scrollTop = y;
        BuildLeaderboardScroll(glass.transform, font, highlightRank, scrollTop, browseOnly);

        bool showNameEntry = !browseOnly && highlightRank >= 0 && highlightRank < LeaderboardData.MaxEntries;

        if (showNameEntry)
        {
            var nameBlock = new GameObject("NameEntry");
            nameBlock.transform.SetParent(glass.transform, false);
            var nameBlockRt = nameBlock.AddComponent<RectTransform>();
            nameBlockRt.anchorMin = new Vector2(0.5f, 0f);
            nameBlockRt.anchorMax = new Vector2(0.5f, 0f);
            nameBlockRt.pivot = new Vector2(0.5f, 0f);
            nameBlockRt.anchoredPosition = new Vector2(0f, 172f);
            nameBlockRt.sizeDelta = new Vector2(920f, 92f);

            var hintGo = new GameObject("NameHint");
            hintGo.transform.SetParent(nameBlock.transform, false);
            var hintRt = hintGo.AddComponent<RectTransform>();
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = Vector2.zero;
            hintRt.sizeDelta = new Vector2(900f, 26f);
            var hint = hintGo.AddComponent<Text>();
            hint.font = font;
            hint.text = "Top 10 — enter your name:";
            hint.fontSize = 18;
            hint.fontStyle = FontStyle.Italic;
            hint.color = new Color(0.82f, 0.7f, 0.95f);
            hint.alignment = TextAnchor.MiddleCenter;

            var input = CreateNameInput(nameBlock.transform, font);
            var inputRt = input.GetComponent<RectTransform>();
            inputRt.anchorMin = inputRt.anchorMax = new Vector2(0.5f, 1f);
            inputRt.pivot = new Vector2(0.5f, 1f);
            inputRt.anchoredPosition = new Vector2(0f, -34f);
            inputRt.sizeDelta = new Vector2(480f, 48f);
            if (driver != null)
            {
                driver.SetNameInput(input);
                input.ActivateInputField();
            }
        }

        // Bottom controls (anchored to bottom of glass so they never overlap list)
        var btnGo = new GameObject("DismissBtn");
        btnGo.transform.SetParent(glass.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, showNameEntry ? 100f : 72f);
        btnRt.sizeDelta = new Vector2(340f, 58f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.14f, 0.06f, 0.22f, 0.92f);
        var btnOutline = btnGo.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.65f, 0.42f, 0.95f, 0.9f);
        btnOutline.effectDistance = new Vector2(2f, -2f);

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.22f, 0.1f, 0.34f, 1f);
        colors.pressedColor = new Color(0.1f, 0.04f, 0.16f, 1f);
        btn.colors = colors;

        string btnLabel = browseOnly ? "BACK" : "PLAY AGAIN";
        MakeTextCentered(btnGo.transform, "BtnLabel", font, btnLabel, 27, FontStyle.Bold, TitleAccent);

        if (driver != null)
            btn.onClick.AddListener(driver.DismissFromButton);

        var footGo = new GameObject("FooterHint");
        footGo.transform.SetParent(glass.transform, false);
        var footRt = footGo.AddComponent<RectTransform>();
        footRt.anchorMin = new Vector2(0.5f, 0f);
        footRt.anchorMax = new Vector2(0.5f, 0f);
        footRt.pivot = new Vector2(0.5f, 0f);
        footRt.anchoredPosition = new Vector2(0f, showNameEntry ? 44f : 24f);
        footRt.sizeDelta = new Vector2(520f, 22f);
        var foot = footGo.AddComponent<Text>();
        foot.font = font;
        foot.text = "Enter / Space / Esc — continue";
        foot.fontSize = 15;
        foot.fontStyle = FontStyle.Italic;
        foot.color = new Color(0.55f, 0.45f, 0.68f);
        foot.alignment = TextAnchor.MiddleCenter;

        return root;
    }

    static void BuildLeaderboardScroll(Transform glass, Font font, int highlightRank, float topOffsetDown, bool browseOnly)
    {
        List<LeaderboardData.Entry> scores = LeaderboardData.GetTopScores();

        var scrollGo = new GameObject("LeaderboardScroll");
        scrollGo.transform.SetParent(glass, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.5f, 1f);
        scrollRt.anchorMax = new Vector2(0.5f, 1f);
        scrollRt.pivot = new Vector2(0.5f, 1f);
        float listHeight = browseOnly ? 520f : 380f;
        scrollRt.anchoredPosition = new Vector2(0f, -topOffsetDown);
        scrollRt.sizeDelta = new Vector2(920f, listHeight);

        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0.08f, 0.04f, 0.12f, 0.28f);
        scrollImg.raycastTarget = true;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewport.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;

        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.001f);
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = viewportRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.inertia = false;

        BuildLeaderboardList(content.transform, font, scores, highlightRank, out float contentHeight);
        contentRt.sizeDelta = new Vector2(0f, Mathf.Max(listHeight, contentHeight + 12f));
    }

    static void BuildLeaderboardList(Transform content, Font font, List<LeaderboardData.Entry> scores, int highlightRank,
        out float contentHeight)
    {
        const float rowW = 900f;
        float rowH = 26f;
        float gap = 4f;
        int max = Mathf.Min(scores.Count, LeaderboardData.MaxEntries);

        contentHeight = 0f;

        if (scores.Count == 0)
        {
            var t = MakeTextTop(content, "LBEmpty", font, "No scores yet!", 20, FontStyle.Italic,
                new Color(0.55f, 0.58f, 0.65f), 0f, 400f, 30f);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            contentHeight = 40f;
            return;
        }

        for (int i = 0; i < max; i++)
        {
            bool isHighlight = i == highlightRank;
            Color rowColor = isHighlight
                ? new Color(1f, 0.88f, 0.35f)
                : new Color(0.88f, 0.9f, 0.95f);
            FontStyle style = isHighlight ? FontStyle.Bold : FontStyle.Normal;

            string pname = scores[i].playerName;
            if (string.IsNullOrWhiteSpace(pname))
                pname = "—";

            string line = $"{i + 1,2}.  {scores[i].score,7:N0}  {Trunc(scores[i].songName, 22)}  {Trunc(pname, 18)}";

            var rowRt = MakeTextTopRect(content, "LBRow" + i, font, line, 16, style, rowColor,
                new Vector2(0f, -i * (rowH + gap)), new Vector2(rowW, rowH));
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            var rowText = rowRt.GetComponent<Text>();
            rowText.alignment = TextAnchor.MiddleCenter;

            float h = (i + 1) * (rowH + gap);
            if (h > contentHeight)
                contentHeight = h;
        }
    }

    static string Trunc(string s, int maxChars)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Length <= maxChars)
            return s;
        return s.Substring(0, Mathf.Max(0, maxChars - 1)) + "…";
    }

    static InputField CreateNameInput(Transform parent, Font font)
    {
        var root = new GameObject("NameInput");
        root.transform.SetParent(parent, false);
        root.AddComponent<RectTransform>();

        var img = root.AddComponent<Image>();
        img.color = new Color(0.1f, 0.05f, 0.16f, 0.92f);
        var o = root.AddComponent<Outline>();
        o.effectColor = new Color(0.55f, 0.35f, 0.85f, 0.65f);
        o.effectDistance = new Vector2(1.5f, -1.5f);

        var input = root.AddComponent<InputField>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(root.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 8f);
        textRt.offsetMax = new Vector2(-12f, -8f);
        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.color = Color.white;
        text.fontSize = 22;
        text.supportRichText = false;

        var phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(root.transform, false);
        var phRt = phGo.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(12f, 8f);
        phRt.offsetMax = new Vector2(-12f, -8f);
        var ph = phGo.AddComponent<Text>();
        ph.font = font;
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        ph.fontSize = 22;
        ph.fontStyle = FontStyle.Italic;
        ph.text = "Your name";

        input.textComponent = text;
        input.placeholder = ph;
        input.characterLimit = 24;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static void StretchFull(Image img)
    {
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Text MakeTextTop(Transform parent, string name, Font font, string content, int size, FontStyle style,
        Color color, float yDown, float width, float height, float xOffset = 0f)
    {
        var rt = MakeTextTopRect(parent, name, font, content, size, style, color, new Vector2(xOffset, yDown),
            new Vector2(width, height));
        return rt.GetComponent<Text>();
    }

    static RectTransform MakeTextTopRect(Transform parent, string name, Font font, string content, int size, FontStyle style,
        Color color, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
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
        return rt;
    }

    static void MakeTextCentered(Transform parent, string name, Font font, string content, int size, FontStyle style, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
    }
}

/// <summary>Handles keyboard dismiss and saving the name before <see cref="ScoreboardOverlayUI"/> root is destroyed.</summary>
public class ScoreboardOverlayDriver : MonoBehaviour
{
    bool _browseOnly;
    int _highlightRank = -1;
    Action _onDismiss;
    InputField _nameInput;
    bool _dismissed;

    public void Bind(bool browseOnly, int highlightRank, Action onDismiss)
    {
        _browseOnly = browseOnly;
        _highlightRank = highlightRank;
        _onDismiss = onDismiss;
    }

    public void SetNameInput(InputField input)
    {
        _nameInput = input;
    }

    public void DismissFromButton()
    {
        CommitNameAndDismiss();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CommitNameAndDismiss();
            return;
        }

        if (_nameInput != null && _nameInput.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.Return))
                CommitNameAndDismiss();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            CommitNameAndDismiss();
    }

    void CommitNameAndDismiss()
    {
        if (_dismissed)
            return;
        _dismissed = true;

        if (!_browseOnly && _highlightRank >= 0 && _highlightRank < LeaderboardData.MaxEntries && _nameInput != null)
            LeaderboardData.SetPlayerNameForRank(_highlightRank, _nameInput.text);

        _onDismiss?.Invoke();
        Destroy(gameObject);
    }
}
