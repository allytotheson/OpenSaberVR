using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared team tint colors for cube bodies, demon-face backing, slice cross-sections, and hit VFX.
/// Configured from <see cref="NotesSpawner"/> on gameplay load.
/// </summary>
public static class TeamNoteColors
{
    static Color _left = new Color(210f / 255f, 154f / 255f, 220f / 255f, 1f);  // #d29adc
    static Color _right = new Color(138f / 255f, 209f / 255f, 242f / 255f, 1f); // #8ad1f2
    static Material _sliceLeft;
    static Material _sliceRight;

    public static void Configure(Color leftTeam, Color rightTeam)
    {
        _left = leftTeam;
        _right = rightTeam;
        RebuildSliceMaterials();
    }

    public static Color Get(bool isLeftHandNote) => isLeftHandNote ? _left : _right;

    /// <summary>Cross-section material for EzySlice (left = purple-ish, right = blue).</summary>
    public static Material GetSliceCrossSectionMaterial(bool isLeftHandNote)
    {
        if (_sliceLeft == null || _sliceRight == null)
            RebuildSliceMaterials();
        return isLeftHandNote ? _sliceLeft : _sliceRight;
    }

    static void RebuildSliceMaterials()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null)
            sh = RenderingShaderUtil.UnlitForWorldMeshes();

        _sliceLeft = BuildSliceMat(_sliceLeft, sh, _left);
        _sliceRight = BuildSliceMat(_sliceRight, sh, _right);
    }

    static Material BuildSliceMat(Material slot, Shader sh, Color c)
    {
        if (slot == null)
            slot = new Material(sh);
        if (slot.HasProperty("_BaseColor"))
            slot.SetColor("_BaseColor", c);
        if (slot.HasProperty("_Color"))
            slot.color = c;
        if (slot.HasProperty("_Metallic"))
            slot.SetFloat("_Metallic", 0.15f);
        if (slot.HasProperty("_Smoothness"))
            slot.SetFloat("_Smoothness", 0.55f);
        if (slot.HasProperty("_EmissionColor"))
            slot.SetColor("_EmissionColor", new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f));
        if (slot.HasProperty("_Surface"))
            slot.SetFloat("_Surface", 0f);
        return slot;
    }

    public static void ApplyTintToRootCubeBody(GameObject demonRoot, Color tint)
    {
        var mr = demonRoot.GetComponent<MeshRenderer>();
        if (mr == null)
            return;
        var m = mr.material;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color"))
            m.color = tint;
        if (m.HasProperty("_EmissionColor"))
            m.SetColor("_EmissionColor", new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f, 1f));
    }
}
