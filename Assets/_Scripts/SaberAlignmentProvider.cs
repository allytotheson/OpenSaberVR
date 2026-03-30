using UnityEngine;

/// <summary>
/// Pluggable alignment targets for sabers. Default implementation uses nearest notes to the camera hit plane.
/// For Raspberry Pi: either set <see cref="DesktopSaberTestInput.preferUdpImuWhenValid"/> and drive <see cref="SaberMotionController"/>,
/// or implement this interface on a MonoBehaviour and assign it to <see cref="DesktopSaberTestInput.alignmentProviderBehaviour"/> to fuse IMU with note targets.
/// </summary>
public interface ISaberAlignmentTargetProvider
{
    /// <summary>Best approaching block for this hand, or false if none.</summary>
    bool TryGetTarget(bool isLeftHandRed, Transform cameraTransform, out Vector3 worldPosition, out Quaternion worldRotation);
}

/// <summary>
/// Stateless math for “nearest note to hit plane” — usable from providers, tests, or future RPi fusion.
/// </summary>
public static class SaberAlignmentQueries
{
    public static bool TryGetNearestBlockForHand(bool isLeftHandRed, Transform cam, float hitPlaneDistanceFallback, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = default;
        worldRotation = default;
        if (cam == null)
            return false;

        if (!BeatSaberHitLineGuide.TryGetGameplayHitPlane(out Vector3 planePoint, out Vector3 planeNormal))
            return false;

        Transform spawner = GameObject.Find("Spawner")?.transform;
        var demons = UnityEngine.Object.FindObjectsByType<DemonHandling>(FindObjectsInactive.Exclude);
        if (demons == null || demons.Length == 0)
            return false;

        DemonHandling best = null;
        float bestScore = float.MaxValue;

        foreach (var d in demons)
        {
            if (d == null) continue;
            bool red = SaberTargetResolver.IsRedDemon(d, spawner);
            if (red != isLeftHandRed)
                continue;

            Vector3 p = d.transform.position;
            float along = Vector3.Dot(p - planePoint, planeNormal);
            if (along < -24f || along > 36f)
                continue;

            Vector3 lateral = Vector3.ProjectOnPlane(p - planePoint, planeNormal);
            float score = Mathf.Abs(along) * 3.5f + lateral.sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = d;
            }
        }

        if (best == null)
            return false;

        worldPosition = best.transform.position;
        worldRotation = best.transform.rotation;
        return true;
    }

    /// <summary>Uses <see cref="DesktopGameplayCamera"/> so alignment matches the fallback gameplay view.</summary>
    public static bool TryGetNearestBlockForHand(bool isLeftHandRed, float hitPlaneDistanceFallback, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = default;
        worldRotation = default;
        if (!DesktopGameplayCamera.TryGet(out Camera cam))
            return false;
        return TryGetNearestBlockForHand(isLeftHandRed, cam.transform, hitPlaneDistanceFallback, out worldPosition, out worldRotation);
    }
}

/// <summary>
/// Picks red notes for the left saber and blue for the right, biased toward the hit-plane depth.
/// </summary>
public sealed class SaberNearestBlockAlignmentProvider : MonoBehaviour, ISaberAlignmentTargetProvider
{
    public float hitPlaneDistanceFallback = BeatSaberHitLineGuide.DefaultDistanceInFrontOfCamera;

    public bool TryGetTarget(bool isLeftHandRed, Transform cam, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        return SaberAlignmentQueries.TryGetNearestBlockForHand(isLeftHandRed, cam, hitPlaneDistanceFallback, out worldPosition, out worldRotation);
    }
}
