using UnityEngine;

/// <summary>
/// Lowest-priority provider: desktop keyboard / auto-align driving.
/// Available when XR is not active. The actual motion logic remains in <see cref="DesktopSaberTestInput"/>
/// (LateUpdate), so this provider signals availability and provides fallback velocity from position delta.
/// When this is the active provider, <see cref="DesktopSaberTestInput"/> is left enabled to do its work.
/// </summary>
public class DesktopSaberInputProvider : MonoBehaviour, ISaberInputProvider
{
    public int Priority => 10;

    public bool IsAvailable
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return !GameplayCameraEnsurer.IsXrDeviceActive();
#else
            return false;
#endif
        }
    }

    [Tooltip("Blade length for tip velocity (meters).")]
    public float bladeLength = 0.5f;

    private Vector3 _previousPosition;
    private Vector3 _previousForward;
    private float _linearSpeed;
    private float _angularSpeed;

    public void UpdateSaber(Transform saberTransform, SaberMotionController.SaberHand hand)
    {
        if (saberTransform == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.001f);
        _linearSpeed = Vector3.Distance(saberTransform.position, _previousPosition) / dt;
        _angularSpeed = Vector3.Angle(saberTransform.forward, _previousForward) * Mathf.Deg2Rad / dt;
        _previousPosition = saberTransform.position;
        _previousForward = saberTransform.forward;
    }

    public float GetAngularVelocityMagnitude() => _angularSpeed;

    public Vector3 GetTipVelocity()
    {
        return transform.forward * (_linearSpeed + _angularSpeed * bladeLength);
    }
}
