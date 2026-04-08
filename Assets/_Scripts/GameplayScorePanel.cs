using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-game score readout: multiplier + total score (left), consecutive hits (right), on a frosted panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayScorePanel : MonoBehaviour
{
    [Header("Scene UI (assign in editor on OpenSaber — leave empty for auto-generated HUD on DDOL canvas)")]
    [SerializeField] Text _sceneMultiplierText;
    [SerializeField] Text _sceneScoreText;
    [SerializeField] Text _sceneStreakValueText;
    [Tooltip("Optional; if unset, uses CanvasGroup on this object.")]
    [SerializeField] CanvasGroup _sceneCanvasGroup;

    /// <summary>True when multiplier/score/streak texts are wired in the scene (see OpenSaber editor menu).</summary>
    public bool IsSceneAuthored =>
        _sceneMultiplierText != null && _sceneScoreText != null && _sceneStreakValueText != null;

    const float PanelWidth = 520f;
    const float PanelHeight = 132f;
    /// <summary>Offset from screen center (reference resolution): positive = upward, keeps HUD in upper-middle.</summary>
    const float AnchoredOffsetFromCenterY = 140f;
    const float CornerRadiusPx = 22f;

    Text _multiplierText;
    Text _scoreText;
    Text _streakValueText;
    CanvasGroup _canvasGroup;
    int _lastScore = int.MinValue;
    int _lastCombo = int.MinValue;
    int _lastTier = int.MinValue;

    public static void EnsureOnCanvas(Transform canvasTransform, Font font)
    {
        if (canvasTransform == null)
            return;
        if (OpenSaberHasSceneAuthoredPanel())
            return;
        if (canvasTransform.GetComponentInChildren<GameplayScorePanel>(true) != null)
            return;
        // UI roots must have RectTransform; plain GameObject only has Transform.
        var host = new GameObject(nameof(GameplayScorePanel), typeof(RectTransform));
        host.transform.SetParent(canvasTransform, false);
        var panel = host.AddComponent<GameplayScorePanel>();
        panel.Build(font);
    }

    /// <summary>
    /// Ensures the score panel exists on the gameplay overlay canvas. Call after <see cref="GameplayDebugHud.EnsureCreated"/>
    /// so older DDOL HUD instances (created before the score UI existed) still get a panel.
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

    /// <summary>True when the <c>OpenSaber</c> scene is loaded and contains a scene-wired score panel.</summary>
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

    void Awake()
    {
        if (!IsSceneAuthored)
            return;
        _multiplierText = _sceneMultiplierText;
        _scoreText = _sceneScoreText;
        _streakValueText = _sceneStreakValueText;
        _canvasGroup = _sceneCanvasGroup != null
            ? _sceneCanvasGroup
            : GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        _multiplierText.text = "1×";
        _scoreText.text = "0";
        _streakValueText.text = "0";
        _lastScore = _lastCombo = _lastTier = int.MinValue;
    }

    void Build(Font font)
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        var root = gameObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, AnchoredOffsetFromCenterY);
        root.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bg = bgGo.AddComponent<Image>();
        bg.sprite = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(PanelWidth),
            Mathf.RoundToInt(PanelHeight),
            CornerRadiusPx,
            new Color(1f, 1f, 1f, 0.22f));
        bg.type = Image.Type.Simple;
        bg.raycastTarget = false;

        var borderGo = new GameObject("Border");
        borderGo.transform.SetParent(transform, false);
        var borderRt = borderGo.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-2f, -2f);
        borderRt.offsetMax = new Vector2(2f, 2f);
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(PanelWidth + 4f),
            Mathf.RoundToInt(PanelHeight + 4f),
            CornerRadiusPx + 2f,
            new Color(1f, 1f, 1f, 0.45f));
        borderImg.type = Image.Type.Simple;
        borderImg.raycastTarget = false;
        borderGo.transform.SetAsFirstSibling();

        _multiplierText = CreateGlowText(transform, "MultiplierText", font, 40, FontStyle.Bold,
            TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(200f, 48f));

        _scoreText = CreateGlowText(transform, "ScoreText", font, 34, FontStyle.Bold,
            TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -58f), new Vector2(260f, 44f));

        CreateGlowText(transform, "StreakLabel", font, 18, FontStyle.Normal,
            TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(200f, 28f)).text = "IN A ROW";

        _streakValueText = CreateGlowText(transform, "StreakValue", font, 44, FontStyle.Bold,
            TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(200f, 56f));

        _multiplierText.text = "1×";
        _scoreText.text = "0";
        _streakValueText.text = "0";
        _lastScore = _lastCombo = _lastTier = int.MinValue;
    }

    static Text CreateGlowText(Transform parent, string name, Font font, int size, FontStyle style,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var t = go.AddComponent<Text>();
        if (font != null)
            t.font = font;
        else
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.supportRichText = false;
        t.alignment = alignment;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.92f);
        outline.effectDistance = new Vector2(2.2f, -2.2f);

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(1f, 1f, 1f, 0.5f);
        shadow.effectDistance = new Vector2(5f, -5f);

        return t;
    }

    void LateUpdate()
    {
        if (!IsSceneAuthored && OpenSaberHasSceneAuthoredPanel())
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            return;
        }

        var openSaber = SceneManager.GetSceneByName("OpenSaber");
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
        int tier = sm.ScoreMultiplierTier;
        if (score == _lastScore && combo == _lastCombo && tier == _lastTier)
            return;
        _lastScore = score;
        _lastCombo = combo;
        _lastTier = tier;

        _multiplierText.text = FormatMultiplierLabel(tier);
        _scoreText.text = score.ToString("N0");
        _streakValueText.text = combo.ToString("N0");
    }

    static string FormatMultiplierLabel(int tier)
    {
        if (tier <= 1)
            return "1×";
        return tier + "×";
    }
}

/// <summary>Runtime sprite for a rounded rectangle (UI Image background).</summary>
public static class RoundedRectSpriteUtility
{
    public static Sprite CreateRoundedRectSprite(int width, int height, float radiusPx, Color fill)
    {
        width = Mathf.Max(8, width);
        height = Mathf.Max(8, height);
        float r = Mathf.Min(radiusPx, Mathf.Min(width, height) * 0.5f - 1f);
        r = Mathf.Max(2f, r);

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var clear = Color.clear;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, clear);
        }

        float w = width;
        float h = height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (InsideRoundedRect(x + 0.5f, y + 0.5f, w, h, r))
                    tex.SetPixel(x, y, fill);
            }
        }

        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool InsideRoundedRect(float px, float py, float w, float h, float r)
    {
        if (px < r && py < r)
            return DistSq(px - r, py - r) <= r * r;
        if (px > w - r && py < r)
            return DistSq(px - (w - r), py - r) <= r * r;
        if (px < r && py > h - r)
            return DistSq(px - r, py - (h - r)) <= r * r;
        if (px > w - r && py > h - r)
            return DistSq(px - (w - r), py - (h - r)) <= r * r;
        return true;
    }

    static float DistSq(float dx, float dy)
    {
        return dx * dx + dy * dy;
    }
}
