using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Non-VR menu flow: title looks at the floor; after Start the camera tilts up to the stage / UI.</summary>
public enum DesktopMenuCameraMode
{
    /// <summary>Gameplay, calibration, or any non-menu view — uses <see cref="DesktopPlayerViewRig.lookDownPitchDegrees"/>.</summary>
    Gameplay,
    /// <summary>Title screen: strong downward pitch.</summary>
    TitleFloor,
    /// <summary>Song list, difficulty, no-songs, quit confirm — same base pitch as gameplay menu browse.</summary>
    SongBrowse,
}

/// <summary>
/// Non-VR "standing on the platform" view: eye height over <see cref="PlayersPlatform"/>,
/// slight pitch down, then nudge toward the hit plane so sabers and the cyan frame stay in frame
/// (no mouse-look). Raspberry Pi builds can reuse the same rig until you need a different offset.
/// </summary>
/// <remarks>
/// Runs after <see cref="BeatSaberHitLineGuide"/> (default 0) so the cyan frame uses the previous camera pose;
/// hit-plane snap for this rig uses lane anchor or a stable synthetic plane (not the live camera) to avoid feedback jitter.
/// </remarks>
[DefaultExecutionOrder(50)]
public class DesktopPlayerViewRig : MonoBehaviour
{
    const string DefaultPlatformName = "PlayersPlatform";

    [Header("Anchor (optional)")]
    [Tooltip("If set, overrides GameObject.Find by name for the platform.")]
    public Transform PlayerPlatform;

    [Tooltip("Scene object name for the player stand when PlayerPlatform is null.")]
    public string platformObjectName = DefaultPlatformName;

    [Header("Standing pose")]
    [Tooltip("Eye height above the top surface of the platform mesh (meters).")]
    public float eyeHeightAbovePlatform = 1.55f;

    [Tooltip("Extra downward pitch so the near edge of the platform is visible (degrees).")]
    public float lookDownPitchDegrees = 8f;

    [Tooltip("Yaw in degrees; 0 = look toward +Z (incoming note travel).")]
    public float yawDegrees = 0f;

    [Header("Desktop play space (non-VR)")]
    [Tooltip("Slide eye along flat horizontal forward toward the lane / cyan frame.")]
    public float eyeForwardTowardPlayMeters = 1.35f;

    [Tooltip("Raise eye in world space toward the hit frame.")]
    public float eyeVerticalBoost = 0.55f;

    [Tooltip("Blend eye toward a point in front of the gameplay hit plane. 1 ≈ at the frame.")]
    [Range(0f, 1f)]
    public float hitPlanePositionBlend = 0.94f;

    [Tooltip("Eye distance in front of the hit plane along the plane normal toward the player (meters).")]
    public float hitPlaneStandoffMeters = 1.05f;

    [Tooltip("Extra pitch down after the above (degrees).")]
    public float extraPitchDegrees = 1f;

    [Header("Stability")]
    [Tooltip("Keep using the same Camera reference until ApplyIfNeeded runs or it is destroyed — avoids flicker when several cameras are eligible.")]
    public bool pinPreferredCamera = true;

    [Header("Non-VR menu camera")]
    [Tooltip("Pitch (degrees) when looking at the floor on the title screen. 90 ≈ straight down.")]
    public float titleScreenPitchDegrees = 82f;

    [Tooltip("Seconds to tilt between title floor pitch and song-browse / gameplay pitch.")]
    public float menuPitchTransitionSeconds = 1.1f;

    Vector3 _cachedEye;
    Quaternion _cachedRot;
    bool _hasCachedPose;
    Camera _pinnedCam;

    float _currentBasePitch;
    bool _pitchLerpActive;
    float _pitchLerpFrom;
    float _pitchLerpTo;
    float _pitchLerpT;

    public static DesktopPlayerViewRig FindInstance() =>
        UnityEngine.Object.FindAnyObjectByType<DesktopPlayerViewRig>(FindObjectsInactive.Include);

    void Awake()
    {
        _currentBasePitch = lookDownPitchDegrees;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyIfNeeded();
        if (GetComponent<DeveloperGameplayMode>() != null)
            GameplayDebugHud.EnsureCreated(transform);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "OpenSaber" || scene.name == "Calibration")
            SetMenuCameraMode(DesktopMenuCameraMode.Gameplay, animated: false);
    }

    /// <summary>Menu / gameplay camera mode for flat desktop only. XR is ignored.</summary>
    public void SetMenuCameraMode(DesktopMenuCameraMode mode, bool animated)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (!IsNonXRDesktop())
            return;

        float target = mode == DesktopMenuCameraMode.TitleFloor
            ? titleScreenPitchDegrees
            : lookDownPitchDegrees;

        if (!animated || !Application.isPlaying)
        {
            _currentBasePitch = target;
            _pitchLerpActive = false;
            ApplyIfNeeded();
            return;
        }

        _pitchLerpFrom = _currentBasePitch;
        _pitchLerpTo = target;
        _pitchLerpT = 0f;
        _pitchLerpActive = true;
#endif
    }

    /// <summary>
    /// Call after scene load if you need to re-center the desktop camera (e.g. when toggling VR).
    /// </summary>
    public void ApplyIfNeeded()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (!IsNonXRDesktop())
            return;

        _pinnedCam = null;

        if (!GameplayCameraEnsurer.TryGetPreferredCamera(out Camera cam))
            return;

        var platform = PlayerPlatform != null ? PlayerPlatform : FindPlatform();
        if (platform == null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.Euler(_currentBasePitch, yawDegrees, 0f));
            CachePose(cam);
            return;
        }

        Vector3 feet = platform.position;
        var renderer = platform.GetComponent<Renderer>();
        if (renderer != null)
            feet.y = renderer.bounds.max.y;
        else
            feet.y += 0.02f;

        Vector3 eye = feet + Vector3.up * eyeHeightAbovePlatform;
        eye.x = platform.position.x;
        eye.z = platform.position.z;

        cam.transform.SetPositionAndRotation(eye, Quaternion.Euler(_currentBasePitch, yawDegrees, 0f));
        CachePose(cam);
#endif
    }

    void CachePose(Camera cam)
    {
        _cachedEye = cam.transform.position;
        _cachedRot = cam.transform.rotation;
        _hasCachedPose = true;
    }

#if UNITY_EDITOR || UNITY_STANDALONE
    void LateUpdate()
    {
        if (!IsNonXRDesktop() || !_hasCachedPose)
            return;

        Camera cam = null;
        if (pinPreferredCamera && _pinnedCam != null && _pinnedCam.isActiveAndEnabled)
            cam = _pinnedCam;
        if (cam == null)
        {
            if (!GameplayCameraEnsurer.TryGetPreferredCamera(out cam))
                return;
            if (pinPreferredCamera)
                _pinnedCam = cam;
        }

        if (_pitchLerpActive)
        {
            _pitchLerpT += Time.deltaTime / Mathf.Max(0.01f, menuPitchTransitionSeconds);
            float u = Mathf.Clamp01(_pitchLerpT);
            _currentBasePitch = Mathf.Lerp(_pitchLerpFrom, _pitchLerpTo, Mathf.SmoothStep(0f, 1f, u));
            if (u >= 1f)
                _pitchLerpActive = false;
        }

        Vector3 eye = _cachedEye;
        Quaternion rot = Quaternion.Euler(_currentBasePitch, yawDegrees, 0f);

        eye += Vector3.up * eyeVerticalBoost;

        Vector3 ff = rot * Vector3.forward;
        ff.y = 0f;
        if (ff.sqrMagnitude > 1e-4f)
        {
            ff.Normalize();
            eye += ff * eyeForwardTowardPlayMeters;
        }

        Quaternion rotPitched = rot * Quaternion.Euler(extraPitchDegrees, 0f, 0f);
        Vector3 fwdPitched = rotPitched * Vector3.forward;

        if (hitPlanePositionBlend > 0.001f)
        {
            Vector3 pp;
            Vector3 pn;
            if (BeatSaberHitLineGuide.TryGetLaneAnchoredGameplayHitPlane(out pp, out pn))
            {
                pn = pn.normalized;
            }
            else
            {
                float d = BeatSaberHitLineGuide.GetConfiguredDistanceInFront();
                pn = fwdPitched.normalized;
                pp = eye + pn * d;
            }

            Vector3 snapEye = pp - pn * hitPlaneStandoffMeters;
            eye = Vector3.Lerp(eye, snapEye, Mathf.Clamp01(hitPlanePositionBlend));
        }

        if (DeveloperGameplayMode.Enabled && DeveloperGameplayMode.Instance != null)
        {
            var d = DeveloperGameplayMode.Instance;
            eye += d.devExtraEyeOffsetWorld;
            rotPitched = rotPitched * Quaternion.Euler(d.devExtraPitchDegrees, 0f, 0f);
        }

        cam.transform.SetPositionAndRotation(eye, rotPitched);
    }
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
    static bool IsNonXRDesktop()
    {
        return !GameplayCameraEnsurer.IsXrDeviceActive();
    }

    Transform FindPlatform()
    {
        var go = GameObject.Find(platformObjectName);
        return go != null ? go.transform : null;
    }
#endif
}
