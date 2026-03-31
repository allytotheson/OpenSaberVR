using UnityEngine;

/// <summary>
/// Keeps a world-root halo aligned with <see cref="Slice"/> (same timing idea as
/// <see cref="DesktopCameraMountSaberVisual"/> unparented capsules). Parenting LineRenders under
/// tiny or odd FBX scale often makes rings vanish or look "stuck."
/// </summary>
[DefaultExecutionOrder(455)]
public sealed class DesktopSaberHaloWorldFollower : MonoBehaviour
{
    public Transform follow;

    public Vector3 localPosition;

    public Quaternion localRotation = Quaternion.identity;

    void LateUpdate()
    {
        if (GameplayCameraEnsurer.IsXrDeviceActive())
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        if (follow == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.SetPositionAndRotation(
            follow.TransformPoint(localPosition),
            follow.rotation * localRotation);
        transform.localScale = Vector3.one;
    }
}
