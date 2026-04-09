using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent on-screen overlay showing real-time swing detection diagnostics.
/// Finds all <see cref="SwingDetector"/> instances and displays their velocity,
/// threshold, swing state, and active provider. Remove this component (or set
/// <see cref="showHud"/> to false) when debugging is complete.
/// </summary>
public class SwingDebugHud : MonoBehaviour
{
    [Tooltip("Toggle the HUD at runtime without removing the component.")]
    public bool showHud = true;

    Canvas _canvas;
    Text _label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<SwingDebugHud>(FindObjectsInactive.Include) != null)
            return;
        var go = new GameObject(nameof(SwingDebugHud));
        DontDestroyOnLoad(go);
        go.AddComponent<SwingDebugHud>();
    }

    void Awake()
    {
        if (FindObjectsByType<SwingDebugHud>(FindObjectsInactive.Include).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        Build();
    }

    void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 250;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var textGo = new GameObject("SwingDbgText");
        textGo.transform.SetParent(transform, false);
        var rect = textGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(16f, 16f);
        rect.sizeDelta = new Vector2(700f, 260f);

        _label = textGo.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            _label.font = font;
        _label.fontSize = 22;
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.LowerLeft;
        _label.raycastTarget = false;
        _label.color = new Color(0f, 1f, 0.6f, 0.92f);
        _label.text = "";

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    void LateUpdate()
    {
        if (_label == null)
            return;

        if (!showHud)
        {
            _label.enabled = false;
            return;
        }
        _label.enabled = true;

        var detectors = FindObjectsByType<SwingDetector>(FindObjectsInactive.Exclude);
        if (detectors == null || detectors.Length == 0)
        {
            _label.text = "[SwingDebugHud] No SwingDetector found";
            return;
        }

        var sb = new System.Text.StringBuilder(512);
        sb.AppendLine("=== SWING DEBUG ===");
        foreach (var sd in detectors)
        {
            if (sd == null) continue;
            string hand = sd.gameObject.name;
            string swinging = sd.IsSwinging ? "<color=#FF4444>YES</color>" : "no";
            string pulse = sd.IsKeyboardPulseSwinging ? " [PULSE]" : "";
            float angThresh = sd.minSwingVelocity;
            float linThresh = sd.minSwingVelocity * 2f;

            bool angAbove = sd.debugAngularVel >= angThresh;
            bool linAbove = sd.debugLinearVel >= linThresh;
            string angColor = angAbove ? "#FFFF00" : "#AAAAAA";
            string linColor = linAbove ? "#FFFF00" : "#AAAAAA";

            sb.AppendLine($"<b>{hand}</b>  swing={swinging}{pulse}");
            sb.AppendLine($"  <color={angColor}>angVel={sd.debugAngularVel:F3}</color> / {angThresh:F3}   " +
                          $"<color={linColor}>linVel={sd.debugLinearVel:F3}</color> / {linThresh:F3}");
            sb.AppendLine($"  provider: {sd.debugProviderName}");
        }
        _label.text = sb.ToString();
    }
}
