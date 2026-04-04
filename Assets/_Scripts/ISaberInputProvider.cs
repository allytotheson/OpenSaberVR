using UnityEngine;

/// <summary>
/// Unified contract for any source that can drive a saber transform.
/// <see cref="SaberMotionController"/> picks the highest-priority available provider each frame.
/// </summary>
public interface ISaberInputProvider
{
    /// <summary>True when this provider has valid data and can drive the saber.</summary>
    bool IsAvailable { get; }

    /// <summary>Higher values win. VR = 100, IMU = 50, Desktop = 10.</summary>
    int Priority { get; }

    /// <summary>Apply this frame's motion to the saber transform.</summary>
    void UpdateSaber(Transform saberTransform, SaberMotionController.SaberHand hand);

    /// <summary>Angular velocity magnitude (rad/s) for <see cref="SwingDetector"/>.</summary>
    float GetAngularVelocityMagnitude();

    /// <summary>World-space tip linear velocity for <see cref="SwingDetector"/>.</summary>
    Vector3 GetTipVelocity();
}
