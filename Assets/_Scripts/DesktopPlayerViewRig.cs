using UnityEngine;

/// <summary>
/// Non-VR "standing on the platform" view: eye height over <see cref="PlayersPlatform"/>,
/// slight pitch down so the front edge of the platform stays in frame. Fixed for desktop dev
/// (no mouse-look). Raspberry Pi builds can reuse the same rig until you need a different offset.
/// </summary>
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

    void Awake()
    {
        ApplyIfNeeded();
    }

    /// <summary>
    /// Call after scene load if you need to re-center the desktop camera (e.g. when toggling VR).
    /// </summary>
    public void ApplyIfNeeded()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (!IsNonXRDesktop())
            return;

        if (!GameplayCameraEnsurer.TryGetPreferredCamera(out Camera cam))
            return;

        var platform = PlayerPlatform != null ? PlayerPlatform : FindPlatform();
        if (platform == null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.Euler(lookDownPitchDegrees, yawDegrees, 0f));
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

        cam.transform.SetPositionAndRotation(eye, Quaternion.Euler(lookDownPitchDegrees, yawDegrees, 0f));
#endif
    }

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
