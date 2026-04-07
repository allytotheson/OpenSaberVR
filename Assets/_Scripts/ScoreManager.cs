using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks score with combo multiplier (2 base pts, multiplier 1x/2x/4x/8x).
/// Builds a Beat Saber-style overlay HUD: combo + score on the left, multiplier on the right.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    const int BASE_POINTS = 2;

    int _score;
    int _combo;
    int _multiplier = 1;
    int _misses;

    public int Score => _score;
    public int Combo => _combo;
    public int Multiplier => _multiplier;
    public int Misses => _misses;

    // HUD elements (built once at Start)
    Text _comboLabel;
    Text _comboNumber;
    Text _scoreNumber;
    Text _multiplierText;
    Image _multiplierRing;
    GameObject _hudRoot;

    void Start()
    {
        _score = 0;
        _combo = 0;
        _multiplier = 1;
        _misses = 0;
        BuildHUD();
        UpdateUI();
    }

    public void RegisterHit()
    {
        _combo++;
        if (_combo >= 4) _multiplier = 8;
        else if (_combo >= 3) _multiplier = 4;
        else if (_combo >= 2) _multiplier = 2;
        else _multiplier = 1;

        _score += BASE_POINTS * _multiplier;
        UpdateUI();
    }

    public void RegisterMiss()
    {
        _combo = 0;
        _multiplier = 1;
        _misses++;
        UpdateUI();
    }

    public void ResetScore()
    {
        _score = 0;
        _combo = 0;
        _multiplier = 1;
        _misses = 0;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (_comboNumber != null)
            _comboNumber.text = _combo.ToString();
        if (_scoreNumber != null)
            _scoreNumber.text = FormatScore(_score);
        if (_multiplierText != null)
            _multiplierText.text = "x" + _multiplier;
        if (_multiplierRing != null)
            _multiplierRing.color = MultiplierColor(_multiplier);

        bool showCombo = _combo >= 2;
        if (_comboLabel != null) _comboLabel.gameObject.SetActive(showCombo);
        if (_comboNumber != null) _comboNumber.gameObject.SetActive(showCombo);

        bool showMult = _multiplier >= 2;
        if (_multiplierText != null) _multiplierText.transform.parent.gameObject.SetActive(showMult);
    }

    static string FormatScore(int score)
    {
        if (score >= 1000)
            return score.ToString("N0");
        return score.ToString();
    }

    static Color MultiplierColor(int mult)
    {
        switch (mult)
        {
            case 2: return new Color(0.2f, 0.7f, 1f, 0.6f);
            case 4: return new Color(0.4f, 1f, 0.4f, 0.6f);
            case 8: return new Color(1f, 0.6f, 0.1f, 0.6f);
            default: return new Color(0.4f, 0.4f, 0.5f, 0.4f);
        }
    }

    // ---- Procedural HUD ----

    void BuildHUD()
    {
        if (_hudRoot != null)
            return;

        Font font = GetDefaultFont();

        _hudRoot = new GameObject("ScoreHUD");
        _hudRoot.transform.SetParent(transform);
        var canvas = _hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = _hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _hudRoot.AddComponent<GraphicRaycaster>();

        // --- Left side: COMBO label, combo count, score ---
        var leftGroup = CreateAnchoredGroup(_hudRoot.transform, "LeftGroup",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(160f, 0f), new Vector2(260f, 220f));

        _comboLabel = CreateLabel(leftGroup.transform, "ComboLabel", font, "COMBO", 22,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -20f), new Vector2(260f, 30f));
        _comboLabel.color = new Color(0.85f, 0.85f, 0.85f);
        _comboLabel.fontStyle = FontStyle.Bold;

        _comboNumber = CreateLabel(leftGroup.transform, "ComboNumber", font, "0", 64,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -65f), new Vector2(260f, 70f));
        _comboNumber.fontStyle = FontStyle.Bold;

        _scoreNumber = CreateLabel(leftGroup.transform, "ScoreNumber", font, "0", 36,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -130f), new Vector2(260f, 50f));

        // --- Right side: multiplier ring + text ---
        var rightGroup = CreateAnchoredGroup(_hudRoot.transform, "RightGroup",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-130f, 20f), new Vector2(130f, 130f));

        var ringGo = new GameObject("MultiplierRing");
        ringGo.transform.SetParent(rightGroup.transform, false);
        var ringRt = ringGo.AddComponent<RectTransform>();
        ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
        ringRt.anchoredPosition = Vector2.zero;
        ringRt.sizeDelta = new Vector2(110f, 110f);
        _multiplierRing = ringGo.AddComponent<Image>();
        _multiplierRing.type = Image.Type.Filled;
        _multiplierRing.fillMethod = Image.FillMethod.Radial360;
        _multiplierRing.fillAmount = 1f;
        _multiplierRing.color = new Color(0.4f, 0.4f, 0.5f, 0.4f);

        _multiplierText = CreateLabel(ringGo.transform, "MultiplierText", font, "x1", 42,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(110f, 110f));
        _multiplierText.fontStyle = FontStyle.Bold;
    }

    static GameObject CreateAnchoredGroup(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return go;
    }

    static Text CreateLabel(Transform parent, string name, Font font, string content, int size,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
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
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = alignment;
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
