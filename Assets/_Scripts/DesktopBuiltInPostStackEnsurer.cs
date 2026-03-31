#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

/// <summary>
/// Built-in render pipeline: if the gameplay camera has no <see cref="PostProcessingBehaviour"/>, adds one with a
/// minimal profile so <b>material emission</b> can bloom (Unity 6 + PPv2 still used by this project).
/// Does nothing in XR. Skips if you already assigned Post Processing on the camera.
/// </summary>
[DefaultExecutionOrder(-199)]
public sealed class DesktopBuiltInPostStackEnsurer : MonoBehaviour
{
    static PostProcessingProfile _cachedBloomOnlyProfile;

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall();
    }

    void LateUpdate()
    {
        TryInstall();
    }

    void TryInstall()
    {
        if (RenderingShaderUtil.UsesScriptableRenderPipeline)
            return;
        if (GameplayCameraEnsurer.IsXrDeviceActive())
            return;
        if (!GameplayCameraEnsurer.TryGetPreferredCamera(out Camera cam) || cam == null)
            return;
        if (cam.GetComponent<PostProcessingBehaviour>() != null)
            return;

        var pp = cam.gameObject.AddComponent<PostProcessingBehaviour>();
        pp.profile = GetOrCreateBloomOnlyProfile();
    }

    static PostProcessingProfile GetOrCreateBloomOnlyProfile()
    {
        if (_cachedBloomOnlyProfile != null)
            return _cachedBloomOnlyProfile;

        var p = ScriptableObject.CreateInstance<PostProcessingProfile>();
        DisableAllExceptBloom(p);

        p.bloom.enabled = true;
        var bs = p.bloom.settings;
        bs.bloom.intensity = 1.35f;
        bs.bloom.threshold = 0.95f;
        bs.bloom.softKnee = 0.55f;
        bs.bloom.radius = 4.25f;
        bs.bloom.antiFlicker = false;
        p.bloom.settings = bs;

        _cachedBloomOnlyProfile = p;
        return p;
    }

    static void DisableAllExceptBloom(PostProcessingProfile p)
    {
        if (p == null)
            return;
        p.debugViews.enabled = false;
        p.fog.enabled = false;
        p.antialiasing.enabled = false;
        p.ambientOcclusion.enabled = false;
        p.screenSpaceReflection.enabled = false;
        p.depthOfField.enabled = false;
        p.motionBlur.enabled = false;
        p.eyeAdaptation.enabled = false;
        p.colorGrading.enabled = false;
        p.userLut.enabled = false;
        p.chromaticAberration.enabled = false;
        p.grain.enabled = false;
        p.vignette.enabled = false;
        p.dithering.enabled = false;
    }
}
#endif
