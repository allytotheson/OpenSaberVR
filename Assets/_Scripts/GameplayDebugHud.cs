using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Screen-space EXIT and SKIP bars during OpenSaber gameplay.
/// </summary>
public class GameplayDebugHud : MonoBehaviour
{
    /// <summary>Always false — auto-slice debug feature removed. Kept as a compile-time constant so existing
    /// call sites in DemonHitDetector compile without changes.</summary>
    public static bool AutoSliceNotes => false;

    /// <summary>Kept for API compatibility; not driven by a developer panel anymore.</summary>
    public static bool AutoAlignSabersToNotes = true;

    GameObject _exitBar;
    GameObject _skipBar;

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
        var canvasGo = MenuExitScreenHud.CreateOverlayCanvasRoot(transform, "GameplayHudCanvas");
        Font font = MenuExitScreenHud.ResolveMenuFont();
        _exitBar = MenuExitScreenHud.CreateExitBar(canvasGo.transform, font, OnGameplayExitClicked);
        _exitBar.SetActive(false);

        _skipBar = CreateSkipBar(canvasGo.transform, font, OnSkipToResultsClicked);
        _skipBar.SetActive(false);

        GameplayScorePanel.EnsureOnCanvas(canvasGo.transform, font);
    }

    static GameObject CreateSkipBar(Transform canvasTransform, Font font, UnityAction onSkipClick)
    {
        var skipBar = new GameObject("SkipBar");
        skipBar.transform.SetParent(canvasTransform, false);
        var skipBarRt = skipBar.AddComponent<RectTransform>();
        skipBarRt.anchorMin = new Vector2(0.5f, 1f);
        skipBarRt.anchorMax = new Vector2(0.5f, 1f);
        skipBarRt.pivot = new Vector2(0.5f, 1f);
        const float gap = 12f;
        float offsetX = -(SharedExitButtonLayout.Width * SharedExitButtonLayout.UniformScale * 0.5f + gap + SharedExitButtonLayout.Width * SharedExitButtonLayout.UniformScale * 0.5f);
        skipBarRt.anchoredPosition = new Vector2(offsetX, SharedExitButtonLayout.TopOffsetY);
        skipBarRt.sizeDelta = SharedExitButtonLayout.SizeDelta;
        skipBarRt.localScale = new Vector3(
            SharedExitButtonLayout.UniformScale,
            SharedExitButtonLayout.UniformScale,
            SharedExitButtonLayout.UniformScale);

        BuildSkipButton(skipBar.transform, font, onSkipClick);
        return skipBar;
    }

    static void BuildSkipButton(Transform parent, Font font, UnityAction onClick)
    {
        var go = new GameObject("Btn_SkipToResults");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var t = Texture2D.whiteTexture;
        var frame = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);

        var img = go.AddComponent<Image>();
        img.sprite = frame;
        img.type = Image.Type.Simple;
        img.color = new Color(0.08f, 0.12f, 0.2f, 0.96f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.45f, 0.55f, 0.75f, 0.22f);
        colors.highlightedColor = new Color(0.5f, 0.75f, 1f, 0.5f);
        colors.pressedColor = new Color(0.22f, 0.4f, 0.65f, 0.42f);
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        go.AddComponent<MenuButtonHoverFeedback>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 4f);
        textRt.offsetMax = new Vector2(-8f, -4f);

        var label = textGo.AddComponent<Text>();
        if (font != null) label.font = font;
        label.fontSize = 44;
        label.fontStyle = FontStyle.Normal;
        label.color = new Color(0.75f, 0.92f, 1f, 1f);
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.text = "SKIP";
    }

    void Update()
    {
        var openSaber = SceneManager.GetSceneByName("OpenSaber");
        bool inGameplay = openSaber.IsValid() && openSaber.isLoaded;
        if (_exitBar != null)
            _exitBar.SetActive(inGameplay);
        if (_skipBar != null)
            _skipBar.SetActive(inGameplay);
    }

    void OnGameplayExitClicked()
    {
        var sh = FindAnyObjectByType<SceneHandling>();
        if (sh != null)
            sh.ReturnToMenuFromGameplay();
    }

    void OnSkipToResultsClicked()
    {
        var spawner = FindAnyObjectByType<NotesSpawner>();
        if (spawner != null)
            spawner.RequestSkipToResults();
    }
}
