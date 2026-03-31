using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Resolves shaders for both Built-in and URP. When an SRP asset is assigned in Graphics settings,
/// URP shader names are tried first so runtime materials are not pink.
/// </summary>
public static class RenderingShaderUtil
{
    /// <summary>Unlit, often used for saber proxies, halos, guides. Transparent-friendly.</summary>
    public static Shader UnlitForWorldMeshes()
    {
        if (UsesScriptableRenderPipeline)
        {
            Shader u = Shader.Find("Universal Render Pipeline/Unlit");
            if (u != null)
                return u;
            Shader h = Shader.Find("HDRP/Unlit");
            if (h != null)
                return h;
        }

        Shader legacy = Shader.Find("Unlit/Color");
        if (legacy != null)
            return legacy;

        Shader sprites = Shader.Find("Sprites/Default");
        if (sprites != null)
            return sprites;

        Shader uFallback = Shader.Find("Universal Render Pipeline/Unlit");
        if (uFallback != null)
            return uFallback;

        return Shader.Find("Hidden/InternalErrorShader");
    }

    public static bool UsesScriptableRenderPipeline => GraphicsSettings.defaultRenderPipeline != null;
}
