using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Screen-only (non–XR) projects use the scene <see cref="PrimaryScreenCameraName"/> object—same as a normal Unity game.
/// Leftover SteamVR/VRTK rig cameras are disabled when XR is off. If no camera can render, one is created with
/// that standard name. The old runtime object <c>FallbackCamera_NonVR</c> is removed when a real Main Camera exists.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class GameplayCameraEnsurer : MonoBehaviour
{
    /// <summary>Unity’s default primary camera name; <see cref="PersistentScene"/> includes this for desktop play.</summary>
    public const string PrimaryScreenCameraName = "Main Camera";

    const string LegacyRuntimeFallbackObjectName = "FallbackCamera_NonVR";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.Object.FindAnyObjectByType<GameplayCameraEnsurer>(FindObjectsInactive.Include) != null)
            return;
        var go = new GameObject(nameof(GameplayCameraEnsurer));
        DontDestroyOnLoad(go);
        go.AddComponent<GameplayCameraEnsurer>();
        Ensure();
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();
    }

    void OnSceneUnloaded(Scene scene)
    {
        Ensure();
    }

    /// <summary>True when a headset / XR device is actually loaded and enabled.</summary>
    public static bool IsXrDeviceActive()
    {
        string d = string.IsNullOrEmpty(XRSettings.loadedDeviceName) ? "None" : XRSettings.loadedDeviceName;
        return XRSettings.enabled && d != "None";
    }

    /// <summary>True for known VR rig eye cameras (several SDKs use different object names).</summary>
    public static bool IsHeadsetEyeCamera(Camera c)
    {
        if (c == null)
            return false;
        string n = c.gameObject.name;
        if (n == "Camera (eye)" || n == "Main Camera (eye)")
            return true;
        if (n == "CenterEyeAnchor")
            return true;
        if (n.EndsWith("(eye)", StringComparison.Ordinal) && n.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    /// <summary>
    /// Cameras that belong to SteamVR/OpenVR-style rigs and should not render the flat (non-XR) game view.
    /// </summary>
    public static bool IsVrHeadRigCamera(Camera c)
    {
        if (c == null)
            return false;
        if (IsHeadsetEyeCamera(c))
            return true;
        string n = c.gameObject.name;
        if (n == "Camera (head)" || n == "Main Camera (head)")
            return true;
        if (n == "Camera (ears)" || n == "Main Camera (ears)")
            return true;
        return false;
    }

    public static bool IsRenderable(Camera c)
    {
        return c != null && c.enabled && c.gameObject.activeInHierarchy;
    }

    /// <summary>Call after custom scene churn if you bypass <see cref="SceneManager.sceneLoaded"/>.</summary>
    public static void Ensure()
    {
        if (!ShouldManageCameras())
            return;

        if (!IsXrDeviceActive())
            DisableVrHeadRigCameras();

        DestroyLegacyFallbackIfRedundant();

        if (!HasRenderableCamera())
            ActivateOrCreatePrimaryScreenCamera();

        DestroyLegacyFallbackIfRedundant();

        SyncPrimaryScreenCameraVersusXr();
        BindWorldSpaceCanvasesToPrimaryCamera();
        NotifyDesktopRig();
    }

    static bool ShouldManageCameras()
    {
#if UNITY_SERVER
        return false;
#else
        return true;
#endif
    }

    /// <summary>
    /// Preferred gameplay camera. Non-XR: <see cref="PrimaryScreenCameraName"/> first, then any non–VR-rig camera.
    /// XR: headset eye first, then MainCamera, etc.
    /// </summary>
    public static bool TryGetPreferredCamera(out Camera cam)
    {
        if (IsXrDeviceActive())
            return TryGetPreferredCameraXr(out cam);
        return TryGetPreferredCameraFlat(out cam);
    }

    static bool TryGetPreferredCameraFlat(out Camera cam)
    {
        cam = null;
        var primaryGo = GameObject.Find(PrimaryScreenCameraName);
        var primaryCam = primaryGo != null ? primaryGo.GetComponent<Camera>() : null;
        if (IsRenderable(primaryCam))
        {
            cam = primaryCam;
            return true;
        }

        Camera mainTagged = null;
        Camera anyNonVr = null;
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (!IsRenderable(c) || IsVrHeadRigCamera(c))
                continue;
            if (mainTagged == null && c.CompareTag("MainCamera"))
                mainTagged = c;
            if (anyNonVr == null)
                anyNonVr = c;
        }

        if (mainTagged != null)
        {
            cam = mainTagged;
            return true;
        }

        if (Camera.main != null && IsRenderable(Camera.main) && !IsVrHeadRigCamera(Camera.main))
        {
            cam = Camera.main;
            return true;
        }

        if (anyNonVr != null)
        {
            cam = anyNonVr;
            return true;
        }

        return false;
    }

    static bool TryGetPreferredCameraXr(out Camera cam)
    {
        cam = null;
        Camera mainTagged = null;
        Camera anyRenderable = null;

        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (!IsRenderable(c))
                continue;
            if (IsHeadsetEyeCamera(c))
            {
                cam = c;
                return true;
            }
            if (mainTagged == null && c.CompareTag("MainCamera"))
                mainTagged = c;
            if (anyRenderable == null)
                anyRenderable = c;
        }

        if (mainTagged != null)
        {
            cam = mainTagged;
            return true;
        }

        if (Camera.main != null && IsRenderable(Camera.main))
        {
            cam = Camera.main;
            return true;
        }

        if (anyRenderable != null)
        {
            cam = anyRenderable;
            return true;
        }

        return false;
    }

    static bool HasRenderableCamera()
    {
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (IsRenderable(c))
                return true;
        }
        return false;
    }

    static void DisableVrHeadRigCameras()
    {
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c == null || !IsVrHeadRigCamera(c))
                continue;
            if (c.CompareTag("MainCamera"))
                c.gameObject.tag = "Untagged";
            c.enabled = false;
        }
    }

    static void DestroyLegacyFallbackIfRedundant()
    {
        var legacy = GameObject.Find(LegacyRuntimeFallbackObjectName);
        if (legacy == null)
            return;
        var primary = GameObject.Find(PrimaryScreenCameraName);
        if (primary != null && primary != legacy)
            UnityEngine.Object.Destroy(legacy);
    }

    static void ActivateOrCreatePrimaryScreenCamera()
    {
        var go = GameObject.Find(PrimaryScreenCameraName);
        if (go == null)
            go = new GameObject(PrimaryScreenCameraName);
        go.SetActive(true);
        if (!go.CompareTag("MainCamera"))
            go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        if (cam == null)
            cam = go.AddComponent<Camera>();
        cam.enabled = true;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        if (go.GetComponent<AudioListener>() == null)
            go.AddComponent<AudioListener>();

        var legacy = GameObject.Find(LegacyRuntimeFallbackObjectName);
        if (legacy != null && legacy != go)
            UnityEngine.Object.Destroy(legacy);
    }

    static void SyncPrimaryScreenCameraVersusXr()
    {
        var primaryGo = GameObject.Find(PrimaryScreenCameraName);
        var primaryCam = primaryGo != null ? primaryGo.GetComponent<Camera>() : null;
        if (primaryCam == null)
            return;

        if (!IsXrDeviceActive())
        {
            if (!IsRenderable(primaryCam))
            {
                primaryGo.SetActive(true);
                primaryCam.enabled = true;
            }
            return;
        }

        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c == null || c == primaryCam)
                continue;
            if (IsHeadsetEyeCamera(c) && IsRenderable(c))
            {
                primaryCam.enabled = false;
                return;
            }
        }
    }

    static void BindWorldSpaceCanvasesToPrimaryCamera()
    {
        if (!TryGetPreferredCamera(out Camera cam))
            return;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                continue;
            canvas.worldCamera = cam;
        }
    }

    static void NotifyDesktopRig()
    {
        foreach (var rig in UnityEngine.Object.FindObjectsByType<DesktopPlayerViewRig>(FindObjectsInactive.Include))
        {
            if (rig != null && rig.isActiveAndEnabled)
                rig.ApplyIfNeeded();
        }
    }
}
