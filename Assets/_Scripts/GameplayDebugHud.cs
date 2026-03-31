using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Screen-space UI: exit gameplay (top center), developer tools (top right). DontDestroyOnLoad host from bootstrap.
/// </summary>
public class GameplayDebugHud : MonoBehaviour
{
    public static bool AutoAlignSabersToNotes = true;

    /// <summary>Developer HUD: pulse matching saber at the hit plane so notes cut without keys (desktop).</summary>
    public static bool AutoSliceNotes = false;

    GameObject _exitBar;
    Button _devModeButton;
    Text _devModeButtonText;
    GameObject _expandedBlock;
    Button _autoAlignButton;
    Text _autoAlignButtonText;
    Button _autoSliceButton;
    Text _autoSliceButtonText;
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
        if (Input.GetKeyDown(KeyCode.F3))
        {
            var d = FindAnyObjectByType<DeveloperGameplayMode>(FindObjectsInactive.Include);
            if (d != null)
                d.developerMode = !d.developerMode;
        }

        var openSaber = SceneManager.GetSceneByName("OpenSaber");
        bool inGameplay = openSaber.IsValid() && openSaber.isLoaded;
        if (_exitBar != null)
            _exitBar.SetActive(inGameplay);
    }

    void LateUpdate()
    {
        SyncScreenUiLabels();
    }

    void BuildScreenSpaceUi()
    {
        var canvasGo = new GameObject("GameplayHudCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;
        canvasGo.AddComponent<GraphicRaycaster>();

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _exitBar = new GameObject("ExitBar");
        _exitBar.transform.SetParent(canvasGo.transform, false);
        var exitRt = _exitBar.AddComponent<RectTransform>();
        exitRt.anchorMin = new Vector2(0.5f, 1f);
        exitRt.anchorMax = new Vector2(0.5f, 1f);
        exitRt.pivot = new Vector2(0.5f, 1f);
        exitRt.anchoredPosition = new Vector2(0f, -12f);
        exitRt.sizeDelta = new Vector2(280f, 48f);

        var exitBtn = CreateMenuButton(_exitBar.transform, "ExitToMenuButton", font, "Exit to menu", out var exitTxt);
        exitTxt.text = "Exit to menu";
        Destroy(exitBtn.GetComponent<LayoutElement>());
        var exitBtnRt = exitBtn.GetComponent<RectTransform>();
        exitBtnRt.anchorMin = Vector2.zero;
        exitBtnRt.anchorMax = Vector2.one;
        exitBtnRt.offsetMin = Vector2.zero;
        exitBtnRt.offsetMax = Vector2.zero;
        exitBtn.onClick.AddListener(OnExitToMenuClicked);
        _exitBar.SetActive(false);

        var panel = new GameObject("DevToolsPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = new Vector2(-14f, -14f);
        panelRt.sizeDelta = new Vector2(300f, 248f);

        var panelBg = panel.AddComponent<Image>();
        // Fully opaque so the panel never reads as a milky haze over the scene (Overlay still draws crisp).
        panelBg.color = new Color(0.05f, 0.055f, 0.08f, 1f);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        _devModeButton = CreateMenuButton(panel.transform, "DevModeButton", font, "Developer mode", out _devModeButtonText);
        _devModeButton.onClick.AddListener(ToggleDeveloperMode);

        _expandedBlock = new GameObject("Expanded");
        _expandedBlock.transform.SetParent(panel.transform, false);
        _expandedBlock.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 0f);
        var expVlg = _expandedBlock.AddComponent<VerticalLayoutGroup>();
        expVlg.spacing = 6;
        expVlg.childAlignment = TextAnchor.UpperCenter;
        expVlg.childControlHeight = true;
        expVlg.childControlWidth = true;
        expVlg.childForceExpandWidth = true;
        expVlg.childForceExpandHeight = false;
        _expandedBlock.AddComponent<LayoutElement>().flexibleHeight = 1f;

        _autoAlignButton = CreateMenuButton(_expandedBlock.transform, "AutoAlignButton", font, "Auto-align", out _autoAlignButtonText);
        _autoAlignButton.onClick.AddListener(() =>
        {
            AutoAlignSabersToNotes = !AutoAlignSabersToNotes;
            SyncScreenUiLabels();
        });

        _autoSliceButton = CreateMenuButton(_expandedBlock.transform, "AutoSliceButton", font, "Auto-slice", out _autoSliceButtonText);
        _autoSliceButton.onClick.AddListener(() =>
        {
            AutoSliceNotes = !AutoSliceNotes;
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
        hintText.fontSize = 13;
        hintText.color = new Color(0.7f, 0.73f, 0.8f, 0.85f);
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.raycastTarget = false;
        hintText.text = "F3 dev | Z/, left | X/. right | Space both | Auto-slice = free hits";
        hint.AddComponent<LayoutElement>().preferredHeight = 36f;

        SyncScreenUiLabels();
    }

    void OnExitToMenuClicked()
    {
        var sh = FindAnyObjectByType<SceneHandling>();
        if (sh != null)
            sh.ReturnToMenuFromGameplay();
    }

    static Button CreateMenuButton(Transform parent, string name, Font font, string placeholder, out Text label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.minHeight = 36f;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.17f, 0.24f, 1f);

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

        if (_autoSliceButtonText != null)
            _autoSliceButtonText.text = AutoSliceNotes ? "Auto-slice (hits): ON" : "Auto-slice (hits): OFF";
    }
}
