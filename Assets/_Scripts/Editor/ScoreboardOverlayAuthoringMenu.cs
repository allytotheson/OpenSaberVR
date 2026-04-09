using UnityEditor;
using UnityEngine;

/// <summary>
/// Spawns the same hierarchy as <see cref="ScoreboardOverlayUI.Build"/> into the open scene so you can edit
/// RectTransforms, colors, and fonts in the Inspector without entering Play mode. Gameplay still builds the overlay
/// from code unless you change <see cref="PostGameScoreboardFlow"/> to instantiate a saved prefab.
/// </summary>
public static class ScoreboardOverlayAuthoringMenu
{
    const string MenuRoot = "OpenSaber/UI/";
    const string PrefabDir = "Assets/_Prefabs/UI";
    const string PrefabFileName = "ScoreboardOverlay_Authoring.prefab";

    [MenuItem(MenuRoot + "Add Scoreboard Overlay to Scene (editable, not play)", false, 50)]
    public static void AddScoreboardToSceneForEditing()
    {
        var host = new GameObject("ScoreboardOverlay_EDITME");
        Undo.RegisterCreatedObjectUndo(host, "Add Scoreboard Overlay");

        ScoreboardOverlayUI.Build(
            host.transform,
            browseOnly: false,
            finalScore: 123456,
            cutScore: 99999,
            bonusScore: 123,
            songName: "Preview Song",
            difficulty: "Expert+",
            highlightRank: -1,
            onDismiss: () => { },
            forAuthoringPreview: true);

        if (host.transform.childCount > 0)
            Selection.activeGameObject = host.transform.GetChild(0).gameObject;
        else
            Selection.activeGameObject = host;

        EditorGUIUtility.PingObject(Selection.activeGameObject);
    }

    [MenuItem(MenuRoot + "Save Selected as Scoreboard Authoring Prefab", false, 51)]
    public static void SaveSelectionAsAuthoringPrefab()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog(
                "Scoreboard authoring",
                "Select the GameObject to save (e.g. ScoreboardOverlay_EDITME or its child ScoreboardOverlay root).",
                "OK");
            return;
        }

        EnsurePrefabDir();
        string path = PrefabDir + "/" + PrefabFileName;
        PrefabUtility.SaveAsPrefabAsset(go, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
        Debug.Log($"[Scoreboard] Saved authoring prefab: {path}");
    }

    [MenuItem(MenuRoot + "Save Selected as Scoreboard Authoring Prefab", true)]
    public static bool SaveSelectionAsAuthoringPrefabValidate()
    {
        return Selection.activeGameObject != null;
    }

    static void EnsurePrefabDir()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Prefabs"))
            AssetDatabase.CreateFolder("Assets", "_Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets/_Prefabs", "UI");
    }
}
