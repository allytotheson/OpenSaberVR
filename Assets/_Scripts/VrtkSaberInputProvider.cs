using UnityEngine;
using UnityEngine.XR;
using VRTK;

/// <summary>
/// Reads position/rotation and velocity from a VRTK tracked controller.
/// Highest priority when an XR headset is active.
/// </summary>
public class VrtkSaberInputProvider : MonoBehaviour, ISaberInputProvider
{
    public int Priority => 100;

    public bool IsAvailable => GameplayCameraEnsurer.IsXrDeviceActive() && _controllerReference != null
                            && VRTK_ControllerReference.IsValid(_controllerReference);

    [Tooltip("Blade length for tip velocity (meters).")]
    public float bladeLength = 0.5f;

    private VRTK_ControllerReference _controllerReference;
    private Vector3 _velocity;
    private Vector3 _angularVelocity;

    void Start()
    {
        ResolveController();
    }

    void ResolveController()
    {
        var evt = GetComponentInChildren<VRTK_ControllerEvents>(true);
        if (evt != null && evt.gameObject != null)
            _controllerReference = VRTK_ControllerReference.GetControllerReference(evt.gameObject);
    }

    public void UpdateSaber(Transform saberTransform, SaberMotionController.SaberHand hand)
    {
        if (_controllerReference == null || !VRTK_ControllerReference.IsValid(_controllerReference))
        {
            ResolveController();
            return;
        }

        _velocity = VRTK_DeviceFinder.GetControllerVelocity(_controllerReference);
        _angularVelocity = VRTK_DeviceFinder.GetControllerAngularVelocity(_controllerReference);
    }

    public float GetAngularVelocityMagnitude() => _angularVelocity.magnitude;

    public Vector3 GetTipVelocity()
    {
        Vector3 tipOffset = transform.forward * bladeLength;
        Vector3 rotational = Vector3.Cross(_angularVelocity, tipOffset);
        return _velocity + rotational;
    }
}
