#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ensures the flat gameplay camera runs URP post-processing (bloom on the default volume profile).
/// Cameras created or chosen at runtime often have <see cref="UniversalAdditionalCameraData.renderPostProcessing"/> off.
/// </summary>
[DefaultExecutionOrder(70)]
public sealed class DesktopUrpGameplayPostProcessing : MonoBehaviour
{
    void LateUpdate()
    {
        if (!RenderingShaderUtil.UsesScriptableRenderPipeline || GameplayCameraEnsurer.IsXrDeviceActive())
            return;
        if (!GameplayCameraEnsurer.TryGetPreferredCamera(out Camera cam) || cam == null)
            return;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data == null)
            return;
        if (!data.renderPostProcessing)
            data.renderPostProcessing = true;
    }
}
#endif
