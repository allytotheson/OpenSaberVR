using UnityEditor;

/// <summary>
/// EzySlice needs readable meshes. Applies to the note demon model under Assets/Demon.
/// </summary>
public sealed class DemonNoteModelReadableImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Demon/") || !p.Contains("textured_mesh"))
            return;
        var mi = assetImporter as ModelImporter;
        if (mi != null)
            mi.isReadable = true;
    }
}
