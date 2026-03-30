#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires imported FBX/GLB sword models to <see cref="NotesSpawner.importedSwordVisualPrefab"/> after you drop them under Assets.
/// </summary>
public static class OpenSaberDeveloperSwordMenu
{
    const string MenuPath = "OpenSaber/Find and assign Rumi / K-pop demon hunters sword to Spawner";

    [MenuItem(MenuPath)]
    static void AssignSwordToSpawners()
    {
        GameObject model = FindBestSwordModelUnderAssets();
        if (model == null)
        {
            EditorUtility.DisplayDialog(
                "OpenSaber",
                "No matching FBX/GLB found under Assets.\n\n" +
                "Import the sword under something like Assets/_Models/Lightsabers/ (name should include rumi, kpop, demon, hunters, or sword). " +
                "Then run this menu again, or drag the model onto Spawner → Notes Spawner → Imported Sword Visual Prefab.",
                "OK");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(model);
        int n = 0;
        foreach (var spawner in Object.FindObjectsByType<NotesSpawner>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(spawner, "Assign imported sword");
            spawner.importedSwordVisualPrefab = model;
            EditorUtility.SetDirty(spawner);
            n++;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetSceneAt(i));

        Debug.Log($"[OpenSaber] Assigned sword model '{model.name}' from '{assetPath}' to {n} NotesSpawner(s). Save the scene to keep the reference.");
    }

    /// <summary>Best FBX/GLB match for the K-pop / Rumi sword (or similar) under <c>Assets</c>.</summary>
    public static GameObject FindBestSwordModelUnderAssets()
    {
        var seen = new HashSet<string>();
        GameObject best = null;
        int bestScore = -1;

        foreach (var token in new[] { "rumi", "kpop", "hunters", "demon", "sword" })
        {
            foreach (var guid in AssetDatabase.FindAssets($"{token} t:GameObject", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path) || ShouldSkipAssetPath(path))
                    continue;
                if (!IsLikelyModelFile(path))
                    continue;

                int score = ScoreModelPath(path);
                if (score <= 0)
                    continue;

                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main is not GameObject go)
                    continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = go;
                }
            }
        }

        return best;
    }

    static bool ShouldSkipAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;
        return path.IndexOf("3rd_PartyAssets", System.StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("PackageCache", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsLikelyModelFile(string path)
    {
        string low = path.ToLowerInvariant();
        return low.EndsWith(".fbx") || low.EndsWith(".glb") || low.EndsWith(".gltf");
    }

    static int ScoreModelPath(string path)
    {
        string low = path.ToLowerInvariant();
        int score = 0;
        if (low.Contains("kpop-demon-hunters-rumis")) score += 28;
        if (low.Contains("_models")) score += 10;
        if (low.Contains("lightsaber")) score += 8;
        if (low.Contains("rumi")) score += 14;
        if (low.Contains("kpop")) score += 12;
        if (low.Contains("demon")) score += 8;
        if (low.Contains("hunters")) score += 8;
        if (low.Contains("sword")) score += 5;
        return score;
    }

    [MenuItem("OpenSaber/Create Resources/Lightsabers folder (optional, for Resources.Load)")]
    static void CreateResourcesLightsabersFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Lightsabers"))
            AssetDatabase.CreateFolder("Assets/Resources", "Lightsabers");
        AssetDatabase.Refresh();
        Debug.Log("[OpenSaber] Created Assets/Resources/Lightsabers — place kpop-demon-hunters-rumis-sword.fbx here for automatic loading.");
    }
}
#endif
