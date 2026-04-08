#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds a screen-space score HUD under <c>GameplayScoreHud</c> so you can move RectTransforms in the OpenSaber scene.
/// Menu: <b>OpenSaber / Add Gameplay Score HUD to OpenSaber scene</b> (saves the scene).
/// </summary>
public static class GameplayScoreHudSceneSetup
{
    public const string OpenSaberScenePath = "Assets/_Scenes/OpenSaber.unity";

    const int UiLayer = 5;
    const float PanelW = 520f;
    const float PanelH = 132f;
    const float Radius = 22f;

    [MenuItem("OpenSaber/Add Gameplay Score HUD to OpenSaber scene", priority = 52)]
    public static void AddToOpenSaberAndSave()
    {
        if (!System.IO.File.Exists(OpenSaberScenePath))
        {
            EditorUtility.DisplayDialog("Gameplay Score HUD", "Scene not found:\n" + OpenSaberScenePath, "OK");
            return;
        }

        var scene = EditorSceneManager.OpenScene(OpenSaberScenePath, OpenSceneMode.Single);
        if (SceneAlreadyHasAuthoredPanel())
        {
            EditorUtility.DisplayDialog(
                "Gameplay Score HUD",
                "A scene-authored GameplayScorePanel already exists. Delete the GameplayScoreHud root first if you want a fresh copy.",
                "OK");
            return;
        }

        CreateHudHierarchy();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GameplayScoreHud] Saved GameplayScoreHud root to OpenSaber scene. Move the Canvas or ScorePanel RectTransforms as needed.");
    }

    /// <summary>Batch: <c>-batchmode -quit -projectPath ... -executeMethod GameplayScoreHudSceneSetup.CommandLineAddGameplayScoreHudToOpenSaber</c></summary>
    public static void CommandLineAddGameplayScoreHudToOpenSaber()
    {
        if (!System.IO.File.Exists(OpenSaberScenePath))
        {
            Debug.LogError("[GameplayScoreHud] Scene not found: " + OpenSaberScenePath);
            EditorApplication.Exit(1);
            return;
        }

        var scene = EditorSceneManager.OpenScene(OpenSaberScenePath, OpenSceneMode.Single);
        if (SceneAlreadyHasAuthoredPanel())
        {
            Debug.Log("[GameplayScoreHud] Scene already has HUD — exiting.");
            EditorApplication.Exit(0);
            return;
        }

        CreateHudHierarchy();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[GameplayScoreHud] SaveScene failed.");
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    [MenuItem("OpenSaber/Add Gameplay Score HUD to active scene", priority = 53)]
    public static void AddToActiveSceneOnly()
    {
        if (SceneAlreadyHasAuthoredPanel())
        {
            EditorUtility.DisplayDialog(
                "Gameplay Score HUD",
                "A scene-authored GameplayScorePanel already exists in the active scene.",
                "OK");
            return;
        }

        CreateHudHierarchy();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static bool SceneAlreadyHasAuthoredPanel()
    {
        foreach (var p in Object.FindObjectsByType<GameplayScorePanel>(FindObjectsInactive.Include))
        {
            if (p != null && p.IsSceneAuthored)
                return true;
        }

        return false;
    }

    static void CreateHudHierarchy()
    {
        var font = MenuExitScreenHud.ResolveMenuFont();

        var root = new GameObject("GameplayScoreHud", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create GameplayScoreHud");
        SetLayerRecursively(root, UiLayer);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 360;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        rootRt.localScale = Vector3.one;

        var panel = new GameObject("ScorePanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panel, "Create ScorePanel");
        panel.transform.SetParent(root.transform, false);
        SetLayerRecursively(panel, UiLayer);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(PanelW, PanelH);
        panelRt.anchoredPosition = new Vector2(0f, 140f);

        var cg = panel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var driver = panel.AddComponent<GameplayScorePanel>();

        var borderGo = new GameObject("Border", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(borderGo, "Border");
        borderGo.transform.SetParent(panel.transform, false);
        SetLayerRecursively(borderGo, UiLayer);
        var borderRt = borderGo.GetComponent<RectTransform>();
        StretchFull(borderRt);
        borderRt.offsetMin = new Vector2(-2f, -2f);
        borderRt.offsetMax = new Vector2(2f, 2f);
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(PanelW + 4f), Mathf.RoundToInt(PanelH + 4f), Radius + 2f,
            new Color(1f, 1f, 1f, 0.45f));
        borderImg.type = Image.Type.Simple;
        borderImg.raycastTarget = false;
        borderGo.transform.SetAsFirstSibling();

        var bgGo = new GameObject("Background", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(bgGo, "Background");
        bgGo.transform.SetParent(panel.transform, false);
        SetLayerRecursively(bgGo, UiLayer);
        StretchFull(bgGo.GetComponent<RectTransform>());
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(PanelW), Mathf.RoundToInt(PanelH), Radius,
            new Color(1f, 1f, 1f, 0.22f));
        bgImg.type = Image.Type.Simple;
        bgImg.raycastTarget = false;

        Text mult = CreateGlowText(panel.transform, "MultiplierText", font, 40, FontStyle.Bold,
            TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -14f), new Vector2(200f, 48f));
        Text score = CreateGlowText(panel.transform, "ScoreText", font, 34, FontStyle.Bold,
            TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -58f), new Vector2(260f, 44f));
        CreateGlowText(panel.transform, "StreakLabel", font, 18, FontStyle.Normal,
            TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -16f), new Vector2(200f, 28f)).text = "IN A ROW";
        Text streak = CreateGlowText(panel.transform, "StreakValue", font, 44, FontStyle.Bold,
            TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20f, 0f), new Vector2(200f, 56f));

        mult.text = "1×";
        score.text = "0";
        streak.text = "0";

        var so = new SerializedObject(driver);
        so.FindProperty("_sceneMultiplierText").objectReferenceValue = mult;
        so.FindProperty("_sceneScoreText").objectReferenceValue = score;
        so.FindProperty("_sceneStreakValueText").objectReferenceValue = streak;
        so.FindProperty("_sceneCanvasGroup").objectReferenceValue = cg;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = panel;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    static Text CreateGlowText(Transform parent, string name, Font font, int size, FontStyle style,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos,
        Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var t = go.AddComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
}
#endif
