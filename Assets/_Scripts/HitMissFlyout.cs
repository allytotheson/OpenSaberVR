using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Brief on-screen popups for hit / miss (screen-space, no prefab required).
/// </summary>
public class HitMissFlyout : MonoBehaviour
{
    static HitMissFlyout _instance;

    /// <summary>Left-hand / purple team (#d29adc).</summary>
    public static readonly Color PointFlyoutLeftColor = new Color(210f / 255f, 154f / 255f, 220f / 255f, 1f);
    /// <summary>Right-hand / blue team (#8ad1f2).</summary>
    public static readonly Color PointFlyoutRightColor = new Color(138f / 255f, 209f / 255f, 242f / 255f, 1f);

    public const int PointGainPerfectThreshold = 90;

    Canvas _canvas;
    Text _label;
    CanvasGroup _group;
    Coroutine _running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        if (FindAnyObjectByType<HitMissFlyout>(FindObjectsInactive.Include) != null)
            return;
        var go = new GameObject(nameof(HitMissFlyout));
        DontDestroyOnLoad(go);
        go.AddComponent<HitMissFlyout>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        Build();
    }

    void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var textGo = new GameObject("FlyoutText");
        textGo.transform.SetParent(transform, false);
        var rect = textGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.58f);
        rect.anchorMax = new Vector2(0.5f, 0.58f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800f, 200f);

        _label = textGo.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            _label.font = font;
        _label.fontSize = 72;
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.raycastTarget = false;
        _label.supportRichText = true;
        _label.text = "";
        _label.enabled = false;
    }

    public static void ShowHit()
    {
        Show("HIT", new Color(0.35f, 1f, 0.55f, 1f));
    }

    /// <summary>
    /// Compact popup for points on cut: "Perfect" or "Good" above the +N, tinted by note side (left purple, right blue).
    /// </summary>
    public static void ShowPointGain(int points, bool isLeftNote)
    {
        if (points <= 0)
            return;
        Color c = isLeftNote ? PointFlyoutLeftColor : PointFlyoutRightColor;
        bool perfect = points > PointGainPerfectThreshold;
        string quality = perfect ? "Perfect" : "Good";
        const float qualityBasePx = 32f;
        int qualityPx = Mathf.RoundToInt(qualityBasePx * (perfect ? 1.5f : 1.3f));
        string body =
            $"<size={qualityPx}>{quality}</size>\n<size=50>+{points}</size>";
        ShowRich(body, c, anchorY: 0.52f);
    }

    public static void ShowMiss()
    {
        ShowMiss(new Color(1f, 0.35f, 0.4f, 1f));
    }

    /// <summary>Miss text tinted like the note (Beat Saber: type 0 red, type 1 blue).</summary>
    public static void ShowMiss(Color noteColor)
    {
        Color c = noteColor;
        c.a = 1f;
        Show("MISS", c);
    }

    /// <summary>Debug: show when a motion swing is registered (left vs right tint).</summary>
    public static void ShowSwing(bool isLeftHand)
    {
        Color c = isLeftHand
            ? new Color(1f, 0.75f, 0.35f, 1f)
            : new Color(0.45f, 0.85f, 1f, 1f);
        Show("SWING", c);
    }

    public static void Show(string message, Color color, float fontSize = 72f, float anchorY = 0.58f)
    {
        if (_instance == null)
        {
            var go = new GameObject(nameof(HitMissFlyout));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HitMissFlyout>();
        }
        _instance.Play(message, color, fontSize, anchorY, richText: false);
    }

    static void ShowRich(string message, Color color, float anchorY)
    {
        if (_instance == null)
        {
            var go = new GameObject(nameof(HitMissFlyout));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HitMissFlyout>();
        }
        _instance.Play(message, color, 50f, anchorY, richText: true);
    }

    void Play(string message, Color color, float fontSize, float anchorY, bool richText)
    {
        if (_label == null)
            Build();

        if (_running != null)
            StopCoroutine(_running);
        _running = StartCoroutine(Animate(message, color, fontSize, anchorY, richText));
    }

    IEnumerator Animate(string message, Color color, float fontSize, float anchorY, bool richText)
    {
        _label.supportRichText = richText;
        _label.fontSize = Mathf.RoundToInt(fontSize);
        RectTransform rt = _label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
        rt.sizeDelta = richText ? new Vector2(900f, 260f) : new Vector2(800f, 200f);

        _label.text = message;
        _label.color = color;
        _label.enabled = true;
        _group.alpha = 1f;
        const float dur = 0.55f;
        float t = 0f;
        Vector3 startScale = Vector3.one * 0.55f;
        Vector3 peakScale = Vector3.one * 1.08f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = t / dur;
            float ease = 1f - (1f - u) * (1f - u);
            float punch = u < 0.18f ? Mathf.SmoothStep(0f, 1f, u / 0.18f) : Mathf.SmoothStep(1f, 0f, (u - 0.18f) / 0.82f);
            rt.localScale = Vector3.Lerp(startScale, peakScale, punch);
            _group.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01((u - 0.35f) / 0.65f));
            yield return null;
        }

        _label.enabled = false;
        _running = null;
    }
}
