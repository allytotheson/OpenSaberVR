using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Top-center screen-space EXIT bar shared by <see cref="GameplayDebugHud"/> and main menu song/difficulty screens.
/// </summary>
public static class MenuExitScreenHud
{
    static readonly Color MenuExitImageTint = new Color(0.05f, 0.08f, 0.14f, 0.96f);
    static readonly Color MenuExitTextColor = new Color(0.35f, 0.88f, 1f, 1f);

    public const int DefaultSortingOrder = 350;

    public static GameObject CreateOverlayCanvasRoot(Transform parent, string objectName)
    {
        var root = new GameObject(objectName);
        root.transform.SetParent(parent, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = DefaultSortingOrder;
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(
            SharedExitButtonLayout.ReferenceResolutionX,
            SharedExitButtonLayout.ReferenceResolutionY);
        scaler.matchWidthOrHeight = SharedExitButtonLayout.MatchWidthOrHeight;

        return root;
    }

    public static Font ResolveMenuFont()
    {
        Font font = Resources.Load<Font>("Fonts/Gugi-Regular");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    /// <summary>EXIT bar container (anchored top-center); child holds the actual button.</summary>
    public static GameObject CreateExitBar(Transform canvasTransform, Font font, UnityAction onExitClick)
    {
        var exitBar = new GameObject("ExitBar");
        exitBar.transform.SetParent(canvasTransform, false);
        var exitBarRt = exitBar.AddComponent<RectTransform>();
        exitBarRt.anchorMin = new Vector2(0.5f, 1f);
        exitBarRt.anchorMax = new Vector2(0.5f, 1f);
        exitBarRt.pivot = new Vector2(0.5f, 1f);
        exitBarRt.anchoredPosition = new Vector2(0f, SharedExitButtonLayout.TopOffsetY);
        exitBarRt.sizeDelta = SharedExitButtonLayout.SizeDelta;
        exitBarRt.localScale = new Vector3(
            SharedExitButtonLayout.UniformScale,
            SharedExitButtonLayout.UniformScale,
            SharedExitButtonLayout.UniformScale);

        BuildExitButton(exitBar.transform, font, onExitClick);
        return exitBar;
    }

    static void BuildExitButton(Transform parent, Font font, UnityAction onExitClick)
    {
        var go = new GameObject("Btn_Exit_Gameplay");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Sprite frame = Resources.Load<Sprite>("UI/MenuExitFrame");
        if (frame == null)
        {
            var t = Texture2D.whiteTexture;
            frame = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }

        var img = go.AddComponent<Image>();
        img.sprite = frame;
        img.type = Image.Type.Simple;
        img.color = MenuExitImageTint;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.5f, 0.75f, 0.95f, 0.22f);
        colors.highlightedColor = new Color(0.35f, 0.88f, 1f, 0.5f);
        colors.pressedColor = new Color(0.22f, 0.5f, 0.8f, 0.42f);
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.targetGraphic = img;
        btn.onClick.AddListener(onExitClick);

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
        label.fontSize = 52;
        label.fontStyle = FontStyle.Normal;
        label.color = MenuExitTextColor;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.text = "EXIT";
    }
}
