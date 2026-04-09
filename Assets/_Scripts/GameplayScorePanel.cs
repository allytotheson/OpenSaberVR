using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Two portrait panels: LeftPanel (combo streak + score) and RightPanel (multiplier ×N).
/// In the scene: select <b>LeftPanel</b> or <b>RightPanel</b> under GameplayScoreHud to move them.
/// The GameplayScoreHud root is a Screen Space – Overlay canvas whose Rect Transform is always
/// driven by Unity and cannot be repositioned — move the child panels instead.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayScorePanel : MonoBehaviour
{
    [Header("Scene Texts — wired by the OpenSaber menu, do not edit manually")]
    [SerializeField] Text _sceneMultiplierText;
    [SerializeField] Text _sceneScoreText;
    [SerializeField] Text _sceneStreakValueText;
    [SerializeField] CanvasGroup _sceneCanvasGroup;

    /// <summary>True when all three text references are assigned (scene-authored via the menu).</summary>
    public bool IsSceneAuthored =>
        _sceneMultiplierText != null && _sceneScoreText != null && _sceneStreakValueText != null;

    // Runtime-build layout constants (only used by Build() — not by scene-authored path)
    const float LeftW     = 200f;
    const float LeftH     = 300f;
    const float RightW    = 160f;
    const float RightH    = 260f;
    const float EdgeX     = 44f;
    const float DefaultY  = -200f;  // px below screen centre (1080 ref) — about 67 % down
    const float CornerR   = 18f;

    Text _multiplierText;
    Text _scoreText;
    Text _streakValueText;
    CanvasGroup _canvasGroup;
    int _lastScore = int.MinValue;
    int _lastCombo = int.MinValue;
    int _lastTier  = int.MinValue;

    // ─── Static factory helpers ────────────────────────────────────────────────

    public static void EnsureOnCanvas(Transform canvasTransform, Font font)
    {
        if (canvasTransform == null)
            return;
        if (OpenSaberHasSceneAuthoredPanel())
            return;
        if (canvasTransform.GetComponentInChildren<GameplayScorePanel>(true) != null)
            return;
        var host = new GameObject(nameof(GameplayScorePanel), typeof(RectTransform));
        host.transform.SetParent(canvasTransform, false);
        var panel = host.AddComponent<GameplayScorePanel>();
        panel.Build(font);
    }

    /// <summary>
    /// Ensures the panel exists on the DDOL gameplay canvas.
    /// Skipped when a scene-authored panel is present.
    /// </summary>
    public static void EnsureOnAnyGameplayHud(Font font = null)
    {
        if (OpenSaberHasSceneAuthoredPanel())
            return;
        var hud = Object.FindAnyObjectByType<GameplayDebugHud>(FindObjectsInactive.Include);
        if (hud == null)
            return;
        var canvas = hud.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return;
        if (font == null)
            font = MenuExitScreenHud.ResolveMenuFont();
        EnsureOnCanvas(canvas.transform, font);
    }

    /// <summary>True when the OpenSaber scene is loaded and contains a scene-wired panel.</summary>
    public static bool OpenSaberHasSceneAuthoredPanel()
    {
        var openSaber = SceneManager.GetSceneByName("OpenSaber");
        if (!openSaber.IsValid() || !openSaber.isLoaded)
            return false;
        foreach (var root in openSaber.GetRootGameObjects())
        {
            foreach (var p in root.GetComponentsInChildren<GameplayScorePanel>(true))
            {
                if (p != null && p.IsSceneAuthored)
                    return true;
            }
        }
        return false;
    }

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (!IsSceneAuthored)
            return;
        _multiplierText  = _sceneMultiplierText;
        _scoreText       = _sceneScoreText;
        _streakValueText = _sceneStreakValueText;
        _canvasGroup     = _sceneCanvasGroup != null
            ? _sceneCanvasGroup
            : GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable  = false;
        _multiplierText.text  = "×1";
        _scoreText.text       = "0";
        _streakValueText.text = "0";
        _lastScore = _lastCombo = _lastTier = int.MinValue;

        StripTextGlow(_sceneMultiplierText);
        StripTextGlow(_sceneScoreText);
        StripTextGlow(_sceneStreakValueText);
    }

    static void StripTextGlow(Text t)
    {
        if (t == null)
            return;
        var o = t.GetComponent<Outline>();
        if (o != null)
            Destroy(o);
        var s = t.GetComponent<Shadow>();
        if (s != null)
            Destroy(s);
    }

    void LateUpdate()
    {
        // Hide DDOL copy if a scene-authored panel is active
        if (!IsSceneAuthored && OpenSaberHasSceneAuthoredPanel())
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            return;
        }

        var openSaber  = SceneManager.GetSceneByName("OpenSaber");
        bool inGameplay = openSaber.IsValid() && openSaber.isLoaded;
        if (_canvasGroup != null)
            _canvasGroup.alpha = inGameplay ? 1f : 0f;

        if (!inGameplay)
            return;
        if (_multiplierText == null || _scoreText == null || _streakValueText == null)
            return;

        var sm = FindAnyObjectByType<ScoreManager>(FindObjectsInactive.Include);
        if (sm == null)
            return;

        int score = sm.Score;
        int combo = sm.ComboStreak;
        int tier  = sm.ScoreMultiplierTier;
        if (score == _lastScore && combo == _lastCombo && tier == _lastTier)
            return;
        _lastScore = score;
        _lastCombo = combo;
        _lastTier  = tier;

        _multiplierText.text  = "×" + tier;
        _scoreText.text       = score.ToString("N0");
        _streakValueText.text = combo.ToString("N0");
    }

    // ─── Runtime layout (DDOL auto-build) ─────────────────────────────────────

    void Build(Font font)
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable  = false;

        // Stretch host to fill canvas so children can anchor to screen edges
        var host = gameObject.GetComponent<RectTransform>();
        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = Vector2.zero;
        host.offsetMax = Vector2.zero;
        host.localScale = Vector3.one;

        // Left panel — combo + score
        var leftGo = new GameObject("LeftPanel", typeof(RectTransform));
        leftGo.transform.SetParent(transform, false);
        var leftRt = leftGo.GetComponent<RectTransform>();
        leftRt.anchorMin       = new Vector2(0f, 0.5f);
        leftRt.anchorMax       = new Vector2(0f, 0.5f);
        leftRt.pivot           = new Vector2(0f, 0.5f);
        leftRt.sizeDelta       = new Vector2(LeftW, LeftH);
        leftRt.anchoredPosition = new Vector2(EdgeX, DefaultY);
        BuildBackground(leftGo.transform, LeftW, LeftH);
        BuildLeftContent(leftGo.transform, font);

        // Right panel — multiplier
        var rightGo = new GameObject("RightPanel", typeof(RectTransform));
        rightGo.transform.SetParent(transform, false);
        var rightRt = rightGo.GetComponent<RectTransform>();
        rightRt.anchorMin       = new Vector2(1f, 0.5f);
        rightRt.anchorMax       = new Vector2(1f, 0.5f);
        rightRt.pivot           = new Vector2(1f, 0.5f);
        rightRt.sizeDelta       = new Vector2(RightW, RightH);
        rightRt.anchoredPosition = new Vector2(-EdgeX, DefaultY);
        BuildBackground(rightGo.transform, RightW, RightH);
        BuildRightContent(rightGo.transform, font);

        _multiplierText.text  = "×1";
        _scoreText.text       = "0";
        _streakValueText.text = "0";
        _lastScore = _lastCombo = _lastTier = int.MinValue;
    }

    void BuildBackground(Transform parent, float w, float h)
    {
        var borderGo  = new GameObject("Border");
        borderGo.transform.SetParent(parent, false);
        var borderRt  = borderGo.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-2f, -2f);
        borderRt.offsetMax = new Vector2( 2f,  2f);
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite       = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(w + 4f), Mathf.RoundToInt(h + 4f), CornerR + 2f,
            new Color(1f, 1f, 1f, 0.45f));
        borderImg.type         = Image.Type.Simple;
        borderImg.raycastTarget = false;
        borderGo.transform.SetAsFirstSibling();

        var bgGo  = new GameObject("Background");
        bgGo.transform.SetParent(parent, false);
        var bgRt  = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite       = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(w), Mathf.RoundToInt(h), CornerR,
            new Color(1f, 1f, 1f, 0.18f));
        bgImg.type         = Image.Type.Simple;
        bgImg.raycastTarget = false;
        bgGo.transform.SetSiblingIndex(1);
    }

    void BuildLeftContent(Transform parent, Font font)
    {
        CreateHudText(parent, "ComboLabel", font, 16, FontStyle.Normal,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -14f), new Vector2(180f, 26f)).text = "COMBO";

        CreateDivider(parent, new Vector2(0f, -46f));

        _streakValueText = CreateHudText(parent, "StreakValue", font, 68, FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -54f), new Vector2(180f, 90f));

        CreateDivider(parent, new Vector2(0f, -150f));

        _scoreText = CreateHudText(parent, "ScoreText", font, 30, FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -162f), new Vector2(180f, 44f));
    }

    void BuildRightContent(Transform parent, Font font)
    {
        _multiplierText = CreateHudText(parent, "MultiplierText", font, 64, FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(140f, 90f));
    }

    static void CreateDivider(Transform parent, Vector2 anchoredPos)
    {
        var go  = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 1f);
        rt.anchorMax       = new Vector2(0.5f, 1f);
        rt.pivot           = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = new Vector2(160f, 2f);
        var img = go.AddComponent<Image>();
        img.color          = new Color(1f, 1f, 1f, 0.5f);
        img.raycastTarget  = false;
    }

    static Text CreateHudText(Transform parent, string name, Font font, int size, FontStyle style,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.pivot           = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = sizeDelta;

        var t = go.AddComponent<Text>();
        t.font             = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize         = size;
        t.fontStyle        = style;
        t.supportRichText  = false;
        t.alignment        = alignment;
        t.color            = Color.white;
        t.raycastTarget    = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;

        return t;
    }
}

/// <summary>Runtime sprite for a rounded rectangle (UI Image background).</summary>
public static class RoundedRectSpriteUtility
{
    public static Sprite CreateRoundedRectSprite(int width, int height, float radiusPx, Color fill)
    {
        width  = Mathf.Max(8, width);
        height = Mathf.Max(8, height);
        float r = Mathf.Min(radiusPx, Mathf.Min(width, height) * 0.5f - 1f);
        r = Mathf.Max(2f, r);

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var clear = Color.clear;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, clear);

        float w = width, h = height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (InsideRoundedRect(x + 0.5f, y + 0.5f, w, h, r))
                    tex.SetPixel(x, y, fill);

        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool InsideRoundedRect(float px, float py, float w, float h, float r)
    {
        if (px < r && py < r)         return Dsq(px - r, py - r) <= r * r;
        if (px > w - r && py < r)     return Dsq(px - (w - r), py - r) <= r * r;
        if (px < r && py > h - r)     return Dsq(px - r, py - (h - r)) <= r * r;
        if (px > w - r && py > h - r) return Dsq(px - (w - r), py - (h - r)) <= r * r;
        return true;
    }

    static float Dsq(float dx, float dy) => dx * dx + dy * dy;
}
