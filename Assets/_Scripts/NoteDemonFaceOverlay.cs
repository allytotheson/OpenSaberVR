using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Demon portrait on the strike face. Uses <see cref="CompositeShaderName"/> so team color + PNG are
/// combined in one <strong>opaque</strong> draw (fixes transparent sorting so backing is visible).
/// Also strips typical green-screen pixels so they show team color instead.
/// </summary>
public static class NoteDemonFaceOverlay
{
    public const string CompositeShaderName = "OpenSaber/DemonFaceComposite";

    /// <summary>Left-hand (purple team) PNGs: <c>Assets/demons/purple</c> and Resources <c>…/purple</c>.</summary>
    public const string DemonsSubfolderPurple = "purple";
    /// <summary>Right-hand (blue team) PNGs: <c>Assets/demons/blue</c> and Resources <c>…/blue</c>.</summary>
    public const string DemonsSubfolderBlue = "blue";

    const float ZEpsilon = 0.004f;
    const float BackingInsetLocalZ = 0.0025f;

    static Material _compositeMat;
    static bool _loggedCompositeMissing;

    static Material _legacyTransparentMat;
    static Material _legacyOpaqueBackingMat;

    static Material CompositeMaterial()
    {
        if (_compositeMat != null)
            return _compositeMat;

        Shader sh = Shader.Find(CompositeShaderName);
        if (sh == null)
            return null;

        _compositeMat = new Material(sh);
        if (_compositeMat.HasProperty("_RemoveGreenScreen"))
            _compositeMat.SetFloat("_RemoveGreenScreen", 1f);
        if (_compositeMat.HasProperty("_ChromaRemoval"))
            _compositeMat.SetFloat("_ChromaRemoval", 0.9f);
        return _compositeMat;
    }

    static Material LegacyTransparentMaterial()
    {
        if (_legacyTransparentMat != null)
            return _legacyTransparentMat;
        Shader sh = RenderingShaderUtil.UnlitForWorldMeshes();
        if (sh == null)
            return null;
        _legacyTransparentMat = new Material(sh);
        if (_legacyTransparentMat.HasProperty("_Surface"))
            _legacyTransparentMat.SetFloat("_Surface", 1f);
        if (_legacyTransparentMat.HasProperty("_Blend"))
            _legacyTransparentMat.SetFloat("_Blend", 0f);
        if (_legacyTransparentMat.HasProperty("_AlphaClip"))
            _legacyTransparentMat.SetFloat("_AlphaClip", 0f);
        if (_legacyTransparentMat.HasProperty("_Cull"))
            _legacyTransparentMat.SetInt("_Cull", (int)CullMode.Off);
        _legacyTransparentMat.renderQueue = 3000;
        return _legacyTransparentMat;
    }

    static Material LegacyOpaqueBackingMaterial()
    {
        if (_legacyOpaqueBackingMat != null)
            return _legacyOpaqueBackingMat;
        Shader sh = RenderingShaderUtil.UnlitForWorldMeshes();
        if (sh == null)
            return null;
        _legacyOpaqueBackingMat = new Material(sh);
        if (_legacyOpaqueBackingMat.HasProperty("_Surface"))
            _legacyOpaqueBackingMat.SetFloat("_Surface", 0f);
        _legacyOpaqueBackingMat.renderQueue = 2990;
        return _legacyOpaqueBackingMat;
    }

    /// <param name="rotationBeforeCut">Demon root world rotation before cut-direction spin.</param>
    /// <param name="faceBackingColor">Team color behind portrait and under transparent / keyed areas.</param>
    /// <param name="greenRemovalStrength">0 = keep greens, 1 = aggressively replace green-screen with backing.</param>
    public static void TryAttach(Transform noteRoot, Texture2D texture, int layer, Quaternion rotationBeforeCut, Color faceBackingColor,
        float greenRemovalStrength = 0.9f)
    {
        if (noteRoot == null || texture == null)
            return;

        var mf = noteRoot.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        Bounds b = mf.sharedMesh.bounds;
        float zFace = b.center.z - b.extents.z;
        Quaternion faceOnMinusZ = Quaternion.Euler(0f, 180f, 0f);
        Quaternion qAfter = noteRoot.rotation;
        Quaternion localOrient = Quaternion.Inverse(qAfter) * rotationBeforeCut * faceOnMinusZ;
        Vector3 baseScale = new Vector3(b.size.x, b.size.y, 1f);

        Material composite = CompositeMaterial();
        if (composite != null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DemonFaceComposite";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(noteRoot, false);
            go.transform.localPosition = new Vector3(b.center.x, b.center.y, zFace - ZEpsilon);
            go.transform.localRotation = localOrient;
            go.transform.localScale = baseScale;
            if (layer >= 0)
                go.layer = layer;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = composite;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var block = new MaterialPropertyBlock();
            if (composite.HasProperty("_BaseMap"))
                block.SetTexture("_BaseMap", texture);
            if (composite.HasProperty("_BackingColor"))
                block.SetColor("_BackingColor", faceBackingColor);
            if (composite.HasProperty("_ChromaRemoval"))
                block.SetFloat("_ChromaRemoval", Mathf.Clamp01(greenRemovalStrength));
            mr.SetPropertyBlock(block);
            return;
        }

        if (!_loggedCompositeMissing)
        {
            _loggedCompositeMissing = true;
            Debug.LogWarning("[NoteDemonFaceOverlay] Shader \"" + CompositeShaderName + "\" not found. " +
                             "Falling back to two quads (backing may not show through some PNGs). Import Assets/_Shaders/DemonFaceComposite.shader.");
        }

        TryAttachLegacyTwoQuad(noteRoot, texture, layer, faceBackingColor, b, zFace, localOrient, baseScale);
    }

    static void TryAttachLegacyTwoQuad(Transform noteRoot, Texture2D texture, int layer, Color faceBackingColor, Bounds b, float zFace,
        Quaternion localOrient, Vector3 baseScale)
    {
        Material backingMat = LegacyOpaqueBackingMaterial();
        Material texMat = LegacyTransparentMaterial();
        if (texMat == null)
            return;

        if (backingMat != null)
        {
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "DemonFaceBacking";
            Object.Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(noteRoot, false);
            back.transform.localPosition = new Vector3(b.center.x, b.center.y, zFace - ZEpsilon + BackingInsetLocalZ);
            back.transform.localRotation = localOrient;
            back.transform.localScale = baseScale;
            if (layer >= 0)
                back.layer = layer;
            var backMr = back.GetComponent<MeshRenderer>();
            backMr.sharedMaterial = backingMat;
            backMr.shadowCastingMode = ShadowCastingMode.Off;
            backMr.receiveShadows = false;
            var backBlock = new MaterialPropertyBlock();
            if (backingMat.HasProperty("_BaseColor"))
                backBlock.SetColor("_BaseColor", faceBackingColor);
            if (backingMat.HasProperty("_Color"))
                backBlock.SetColor("_Color", faceBackingColor);
            backMr.SetPropertyBlock(backBlock);
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "DemonFaceOverlay";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(noteRoot, false);
        go.transform.localPosition = new Vector3(b.center.x, b.center.y, zFace - ZEpsilon);
        go.transform.localRotation = localOrient;
        go.transform.localScale = baseScale;
        if (layer >= 0)
            go.layer = layer;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = texMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var block = new MaterialPropertyBlock();
        if (texMat.HasProperty("_BaseMap"))
            block.SetTexture("_BaseMap", texture);
        if (texMat.HasProperty("_MainTex"))
            block.SetTexture("_MainTex", texture);
        if (texMat.HasProperty("_BaseColor"))
            block.SetColor("_BaseColor", Color.white);
        if (texMat.HasProperty("_Color"))
            block.SetColor("_Color", Color.white);
        mr.SetPropertyBlock(block);
    }

#if UNITY_EDITOR
    const string DemonsEditorRoot = "Assets/demons";

    /// <summary>Loads PNG textures from <c>Assets/demons/&lt;subfolderName&gt;</c> (Editor only).</summary>
    public static Texture2D[] LoadTexturesFromAssetsDemonsSubfolderEditor(string subfolderName)
    {
        if (string.IsNullOrEmpty(subfolderName))
            return null;
        string folder = $"{DemonsEditorRoot}/{subfolderName}";
        if (!AssetDatabase.IsValidFolder(folder))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        var list = new List<Texture2D>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
                list.Add(tex);
        }

        if (list.Count == 0)
            return null;
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list.ToArray();
    }

    /// <summary>
    /// Editor only: PNGs directly under <c>Assets/demons</c> (not in <c>purple</c>/<c>blue</c> subfolders).
    /// Used as a fallback when side folders are empty so older layouts still show faces.
    /// </summary>
    public static Texture2D[] LoadTexturesFromAssetsDemonsRootOnlyEditor()
    {
        if (!AssetDatabase.IsValidFolder(DemonsEditorRoot))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DemonsEditorRoot });
        var list = new List<Texture2D>();
        const string prefix = DemonsEditorRoot + "/";
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith(prefix, System.StringComparison.Ordinal))
                continue;
            string tail = path.Substring(prefix.Length);
            if (tail.IndexOf('/') >= 0)
                continue;
            if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
                list.Add(tex);
        }

        if (list.Count == 0)
            return null;
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list.ToArray();
    }
#endif
}
