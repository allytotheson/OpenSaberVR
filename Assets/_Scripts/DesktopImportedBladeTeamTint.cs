using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies left/right team tint and emission to imported sword materials (desktop).
/// </summary>
public static class DesktopImportedBladeTeamTint
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
    static readonly int EmissiveIntensityId = Shader.PropertyToID("_EmissiveIntensity");

    public static void ApplyToRenderers(Renderer[] renderers, bool isLeft, float tintMix, float emissionIntensity)
    {
        if (renderers == null)
            return;
        Color team = isLeft ? new Color(1f, 0.32f, 0.38f, 1f) : new Color(0.28f, 0.62f, 1f, 1f);
        Color emissionHdr = team * Mathf.Max(0f, emissionIntensity);

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
                ApplyToMaterial(mats[m], team, tintMix, emissionHdr, emissionIntensity);
            r.materials = mats;
        }
    }

    static void ApplyToMaterial(Material m, Color team, float tintMix, Color emissionHdr, float emissionScalar)
    {
        if (m == null)
            return;

        if (m.HasProperty(BaseColorId))
        {
            Color b = m.GetColor(BaseColorId);
            m.SetColor(BaseColorId, Color.Lerp(b, team, tintMix));
        }

        if (m.HasProperty(ColorId))
        {
            Color c = m.GetColor(ColorId);
            m.SetColor(ColorId, Color.Lerp(c, team, tintMix));
        }

        if (m.HasProperty(EmissionColorId))
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor(EmissionColorId, emissionHdr);
        }

        if (m.HasProperty(EmissiveColorId))
        {
            m.SetColor(EmissiveColorId, emissionHdr);
            if (m.HasProperty(EmissiveIntensityId))
                m.SetFloat(EmissiveIntensityId, Mathf.Clamp(emissionScalar, 1.25f, 12f));
        }
    }
}
