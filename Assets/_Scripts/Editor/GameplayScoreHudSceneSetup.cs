#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates two portrait score panels (LeftPanel + RightPanel) inside a Screen Space – Overlay canvas
/// in the OpenSaber scene and wires the text references into <see cref="GameplayScorePanel"/>.
///
/// Menu: <b>OpenSaber / Add Gameplay Score HUD to OpenSaber scene</b>
///
/// After running, the <b>GameplayScoreHud root canvas cannot be repositioned</b> (its Rect Transform
/// is always driven by Unity for Screen Space – Overlay). To move the HUD elements, select
/// <b>LeftPanel</b> or <b>RightPanel</b> in the hierarchy and adjust their Anchored Position.
/// </summary>
public static class GameplayScoreHudSceneSetup
{
    public const string OpenSaberScenePath = "Assets/_Scenes/OpenSaber.unity";

    const int   UiLayer = 5;

    // Left panel (score + combo) — portrait
    const float LeftW   = 200f;
    const float LeftH   = 300f;

    // Right panel (multiplier) — portrait
    const float RightW  = 160f;
    const float RightH  = 260f;

    const float EdgeX   = 44f;    // distance from screen edge
    const float DefaultY = -200f; // px below screen centre (~67 % down on 1080p)
    const float Radius   = 18f;

    [MenuItem("OpenSaber/Add Gameplay Score HUD to OpenSaber scene", priority = 52)]
    public static void AddToOpenSaberAndSave()
    {
        if (!System.IO.File.Exists(OpenSaberScenePath))
        {
            EditorUtility.DisplayDialog("Gameplay Score HUD", "Scene not found:\n" + OpenSaberScenePath, "OK");
            return;
        }

        EditorSceneManager.OpenScene(OpenSaberScenePath, OpenSceneMode.Single);
        if (SceneAlreadyHasAuthoredPanel())
        {
            EditorUtility.DisplayDialog(
                "Gameplay Score HUD",
                "A GameplayScorePanel already exists.\n\nDelete the GameplayScoreHud root first, then run this menu item again.",
                "OK");
            return;
        }

        CreateHudHierarchy();
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GameplayScoreHud] Done — expand GameplayScoreHud in the Hierarchy and move LeftPanel / RightPanel to reposition.");
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

        EditorSceneManager.OpenScene(OpenSaberScenePath, OpenSceneMode.Single);
        if (SceneAlreadyHasAuthoredPanel())
        {
            Debug.Log("[GameplayScoreHud] Scene already has HUD — skipping.");
            EditorApplication.Exit(0);
            return;
        }

        CreateHudHierarchy();
        var scene = SceneManager.GetActiveScene();
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
                "A GameplayScorePanel already exists in the active scene.\n\nDelete GameplayScoreHud first.",
                "OK");
            return;
        }

        CreateHudHierarchy();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static bool SceneAlreadyHasAuthoredPanel()
    {
        foreach (var p in Object.FindObjectsByType<GameplayScorePanel>(FindObjectsInactive.Include))
            if (p != null && p.IsSceneAuthored)
                return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void CreateHudHierarchy()
    {
        var font = MenuExitScreenHud.ResolveMenuFont();

        // ── Root canvas ──────────────────────────────────────────────────────
        var root = new GameObject("GameplayScoreHud", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create GameplayScoreHud");
        SetLayerRecursively(root, UiLayer);

        var canvas       = root.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 360;

        var scaler                   = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode           = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution   = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight    = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // GameplayScorePanel + CanvasGroup live on the root
        var cg            = root.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        var driver = root.AddComponent<GameplayScorePanel>();

        // ── Left panel — Combo + Score ───────────────────────────────────────
        var leftPanel = MakePanel("LeftPanel", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(EdgeX, DefaultY),
            new Vector2(LeftW, LeftH));

        BuildBackground(leftPanel.transform, LeftW, LeftH);

        Text comboLabel = CreateHudText(leftPanel.transform, "ComboLabel", font,
            16, FontStyle.Normal, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -14f), new Vector2(180f, 26f));
        comboLabel.text = "COMBO";

        CreateDivider(leftPanel.transform, new Vector2(0f, -46f));

        Text streakText = CreateHudText(leftPanel.transform, "StreakValue", font,
            68, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -54f), new Vector2(180f, 90f));

        CreateDivider(leftPanel.transform, new Vector2(0f, -150f));

        Text scoreText = CreateHudText(leftPanel.transform, "ScoreText", font,
            30, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -162f), new Vector2(180f, 44f));

        streakText.text = "0";
        scoreText.text  = "0";

        // ── Right panel — Multiplier ─────────────────────────────────────────
        var rightPanel = MakePanel("RightPanel", root.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-EdgeX, DefaultY),
            new Vector2(RightW, RightH));

        BuildBackground(rightPanel.transform, RightW, RightH);

        Text multText = CreateHudText(rightPanel.transform, "MultiplierText", font,
            64, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(140f, 90f));
        multText.text = "×1";

        // ── Wire text refs into GameplayScorePanel ───────────────────────────
        var so = new SerializedObject(driver);
        so.FindProperty("_sceneMultiplierText").objectReferenceValue  = multText;
        so.FindProperty("_sceneScoreText").objectReferenceValue       = scoreText;
        so.FindProperty("_sceneStreakValueText").objectReferenceValue  = streakText;
        so.FindProperty("_sceneCanvasGroup").objectReferenceValue     = cg;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = root;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    static GameObject MakePanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        SetLayerRecursively(go, UiLayer);
        var rt            = go.GetComponent<RectTransform>();
        rt.anchorMin      = anchorMin;
        rt.anchorMax      = anchorMax;
        rt.pivot          = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta      = size;
        rt.localScale     = Vector3.one;
        return go;
    }

    static void BuildBackground(Transform parent, float w, float h)
    {
        var borderGo  = new GameObject("Border", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(borderGo, "Border");
        borderGo.transform.SetParent(parent, false);
        SetLayerRecursively(borderGo, UiLayer);
        var borderRt  = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-2f, -2f);
        borderRt.offsetMax = new Vector2( 2f,  2f);
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite       = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(w + 4f), Mathf.RoundToInt(h + 4f), Radius + 2f,
            new Color(1f, 1f, 1f, 0.45f));
        borderImg.type         = Image.Type.Simple;
        borderImg.raycastTarget = false;
        borderGo.transform.SetAsFirstSibling();

        var bgGo  = new GameObject("Background", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(bgGo, "Background");
        bgGo.transform.SetParent(parent, false);
        SetLayerRecursively(bgGo, UiLayer);
        var bgRt  = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite       = RoundedRectSpriteUtility.CreateRoundedRectSprite(
            Mathf.RoundToInt(w), Mathf.RoundToInt(h), Radius,
            new Color(1f, 1f, 1f, 0.18f));
        bgImg.type         = Image.Type.Simple;
        bgImg.raycastTarget = false;
        bgGo.transform.SetSiblingIndex(1);
    }

    static void CreateDivider(Transform parent, Vector2 anchoredPos)
    {
        var go  = new GameObject("Divider", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Divider");
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 1f);
        rt.anchorMax       = new Vector2(0.5f, 1f);
        rt.pivot           = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = new Vector2(160f, 2f);
        var img = go.AddComponent<Image>();
        img.color          = new Color(1f, 1f, 1f, 0.5f);
        img.raycastTarget  = false;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    static Text CreateHudText(Transform parent, string name, Font font, int size, FontStyle style,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        var rt = go.GetComponent<RectTransform>();
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
#endif
