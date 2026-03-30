#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// After import, assigns <see cref="NotesSpawner.importedSwordVisualPrefab"/> once per domain reload
/// when it is still empty and a matching sword FBX exists under Assets (e.g. _Models/Lightsabers).
/// </summary>
[InitializeOnLoad]
static class OpenSaberDeveloperSwordAutoAssign
{
    static bool _triedThisDomainReload;

    static OpenSaberDeveloperSwordAutoAssign()
    {
        EditorApplication.delayCall += TryAssignAfterImport;
    }

    static void TryAssignAfterImport()
    {
        if (_triedThisDomainReload || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        _triedThisDomainReload = true;

        GameObject model = OpenSaberDeveloperSwordMenu.FindBestSwordModelUnderAssets();
        if (model == null)
            return;

        bool assigned = false;
        foreach (var spawner in Object.FindObjectsByType<NotesSpawner>(FindObjectsInactive.Include))
        {
            if (spawner.importedSwordVisualPrefab != null)
                continue;

            Undo.RecordObject(spawner, "Auto-assign imported sword");
            spawner.importedSwordVisualPrefab = model;
            EditorUtility.SetDirty(spawner);

            assigned = true;
        }

        if (!assigned)
            return;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetSceneAt(i));

        Debug.Log(
            "[OpenSaber] Auto-assigned imported sword model to NotesSpawner.importedSwordVisualPrefab. Save the scene (e.g. OpenSaber) to keep it.");
    }
}
#endif
