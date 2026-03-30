using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left IMGUI strip plus a screen-space Canvas (bottom-right) so developer controls are normal game UI.
/// Lives under the DontDestroyOnLoad <see cref="GameplayCameraEnsurer"/> host when bootstrapped.
/// </summary>
public class GameplayDebugHud : MonoBehaviour
{
    public static bool AutoAlignSabersToNotes = true;

    Button _devModeButton;
    Text _devModeButtonText;
    GameObject _expandedBlock;
    Button _autoAlignButton;
    Text _autoAlignButtonText;
    Button _recenterButton;

    public static void EnsureCreated(Transform parent)
    {
        if (parent == null)
            return;
        if (FindAnyObjectByType<GameplayDebugHud>(FindObjectsInactive.Include) != null)
            return;
        var root = new GameObject(nameof(GameplayDebugHud));
        root.transform.SetParent(parent, false);
        root.AddComponent<GameplayDebugHud>();
    }

    void Awake()
    {
        BuildScreenSpaceUi();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F3))
            return;
        var d = FindAnyObjectByType<DeveloperGameplayMode>(FindObjectsInactive.Include);
        if (d != null)
            d.developerMode = !d.developerMode;
    }

    void LateUpdate()
    {
        SyncScreenUiLabels();
    }

    void BuildScreenSpaceUi()
    {
        var canvasGo = new GameObject("DeveloperModeScreenCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;
        canvasGo.AddComponent<GraphicRaycaster>();

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0f);
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot = new Vector2(1f, 0f);
        panelRt.anchoredPosition = new Vector2(-14f, 14f);
        panelRt.sizeDelta = new Vector2(300f, 200f);

        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.1f, 0.94f);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _devModeButton = CreateMenuButton(panel.transform, "DevModeButton", font, "Developer mode", out _devModeButtonText);
        _devModeButton.onClick.AddListener(ToggleDeveloperMode);

        _expandedBlock = new GameObject("Expanded");
        _expandedBlock.transform.SetParent(panel.transform, false);
        var expRt = _expandedBlock.AddComponent<RectTransform>();
        expRt.sizeDelta = new Vector2(0f, 0f);
        var expVlg = _expandedBlock.AddComponent<VerticalLayoutGroup>();
        expVlg.spacing = 6;
        expVlg.childAlignment = TextAnchor.UpperCenter;
        expVlg.childControlHeight = true;
        expVlg.childControlWidth = true;
        expVlg.childForceExpandWidth = true;
        expVlg.childForceExpandHeight = false;

        var expLe = _expandedBlock.AddComponent<LayoutElement>();
        expLe.flexibleHeight = 1f;

        _autoAlignButton = CreateMenuButton(_expandedBlock.transform, "AutoAlignButton", font, "Auto-align", out _autoAlignButtonText);
        _autoAlignButton.onClick.AddListener(() =>
        {
            AutoAlignSabersToNotes = !AutoAlignSabersToNotes;
            SyncScreenUiLabels();
        });

        _recenterButton = CreateMenuButton(_expandedBlock.transform, "RecenterButton", font, "Recenter camera", out _);
        _recenterButton.onClick.AddListener(() =>
        {
            foreach (var rig in FindObjectsByType<DesktopPlayerViewRig>(FindObjectsInactive.Include))
            {
                if (rig != null)
                    rig.ApplyIfNeeded();
            }
        });

        var hint = new GameObject("Hint");
        hint.transform.SetParent(panel.transform, false);
        var hintText = hint.AddComponent<Text>();
        if (font != null) hintText.font = font;
        hintText.fontSize = 14;
        hintText.color = new Color(0.75f, 0.78f, 0.85f, 0.9f);
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.raycastTarget = false;
        hintText.text = "F3 · IMGUI bottom-left";
        var hintLe = hint.AddComponent<LayoutElement>();
        hintLe.preferredHeight = 22f;

        SyncScreenUiLabels();
    }

    static Button CreateMenuButton(Transform parent, string name, Font font, string placeholder, out Text label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.minHeight = 36f;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.22f, 0.32f, 1f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.28f, 0.34f, 0.48f, 1f);
        colors.pressedColor = new Color(0.35f, 0.42f, 0.55f, 1f);
        btn.colors = colors;
        btn.targetGraphic = img;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 4f);
        textRt.offsetMax = new Vector2(-8f, -4f);

        label = textGo.AddComponent<Text>();
        if (font != null) label.font = font;
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.text = placeholder;

        return btn;
    }

    void ToggleDeveloperMode()
    {
        var d = FindAnyObjectByType<DeveloperGameplayMode>(FindObjectsInactive.Include);
        if (d != null)
            d.developerMode = !d.developerMode;
        SyncScreenUiLabels();
    }

    void SyncScreenUiLabels()
    {
        var dev = FindAnyObjectByType<DeveloperGameplayMode>(FindObjectsInactive.Include);
        bool on = dev != null && dev.developerMode;

        if (_devModeButtonText != null)
            _devModeButtonText.text = on ? "Developer mode: ON" : "Developer mode: OFF";

        if (_expandedBlock != null)
            _expandedBlock.SetActive(on);

        if (_autoAlignButtonText != null)
            _autoAlignButtonText.text = AutoAlignSabersToNotes ? "Auto-align notes: ON" : "Auto-align notes: OFF";
    }

    void OnGUI()
    {
        var dev = FindAnyObjectByType<DeveloperGameplayMode>(FindObjectsInactive.Include);
        if (dev == null)
            return;

        bool on = DeveloperGameplayMode.Enabled;
        float panelH = on ? 168f : 52f;
        GUILayout.BeginArea(new Rect(10f, Screen.height - panelH - 10f, 380f, panelH));
        GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("OpenSaber", GUI.skin.label);
        dev.developerMode = GUILayout.Toggle(dev.developerMode, " Developer mode  (shortcut: F3)");

        if (on)
        {
            AutoAlignSabersToNotes = GUILayout.Toggle(AutoAlignSabersToNotes, "Auto-align sabers to notes");
            if (GUILayout.Button("Recenter desktop camera"))
            {
                foreach (var rig in FindObjectsByType<DesktopPlayerViewRig>(FindObjectsInactive.Include))
                {
                    if (rig != null)
                        rig.ApplyIfNeeded();
                }
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
