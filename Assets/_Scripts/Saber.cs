using UnityEngine;
using VRTK;

/// <summary>
/// VRTK haptics for saber hits. Hit detection is now unified through
/// <see cref="SwingDetector"/> + <see cref="DemonHitDetector"/> for all input modes.
/// Call <see cref="TriggerHitHaptic"/> from <see cref="DemonHitDetector"/> on successful hits.
/// </summary>
public class Saber : MonoBehaviour
{
    [SerializeField] private float impactMagnifier = 120f;
    [SerializeField] private float maxCollisionForce = 4000f;

    private VRTK_ControllerReference controllerReference;

    void Start()
    {
        var controllerEvent = GetComponentInChildren<VRTK_ControllerEvents>(true);
        if (controllerEvent != null && controllerEvent.gameObject != null)
            controllerReference = VRTK_ControllerReference.GetControllerReference(controllerEvent.gameObject);
    }

    /// <summary>
    /// Triggers a haptic pulse scaled by controller velocity. Called by <see cref="DemonHitDetector"/> on hit.
    /// </summary>
    public void TriggerHitHaptic()
    {
        if (VRTK_ControllerReference.IsValid(controllerReference))
        {
            float force = VRTK_DeviceFinder.GetControllerVelocity(controllerReference).magnitude * impactMagnifier;
            float hapticStrength = force / maxCollisionForce;
            VRTK_ControllerHaptics.TriggerHapticPulse(controllerReference, hapticStrength, 0.5f, 0.01f);
        }
        else
        {
            var controllerEvent = GetComponentInChildren<VRTK_ControllerEvents>();
            if (controllerEvent != null && controllerEvent.gameObject != null)
                controllerReference = VRTK_ControllerReference.GetControllerReference(controllerEvent.gameObject);
        }
    }
}
