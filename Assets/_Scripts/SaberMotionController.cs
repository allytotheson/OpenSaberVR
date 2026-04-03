using UnityEngine;

/// <summary>
/// Drives a saber transform from IMU data (accelerometer + gyroscope).
/// Integrates gyroscope for orientation; uses accelerometer for tilt correction.
/// No XR/VR input dependencies - receives data from UDPSaberReceiver.
/// </summary>
public class SaberMotionController : MonoBehaviour
{
    public enum SaberHand { Left, Right }

    [Header("Saber Assignment")]
    public SaberHand hand = SaberHand.Left;

    [Header("References")]
    public UDPSaberReceiver receiver;
    public Transform saberTransform;

    [Header("Calibration")]
    [Tooltip("Position offset from origin (player space). Left hand uses negative X.")]
    public Vector3 basePosition = new Vector3(0.25f, 1.5f, 0f);
    [Tooltip("Optional parent for local-space positioning (e.g. Main Camera)")]
    public Transform playerRoot;
    [Tooltip("Initial forward direction (saber blade)")]
    public Vector3 baseForward = Vector3.forward;
    [Tooltip("Scale gyro deg/s to Unity angular velocity")]
    public float gyroScale = 1f;
    [Tooltip("Scale accel (g) to position influence")]
    public float accelPositionScale = 0.01f;

    [Header("UDP joystick (optional, fields jx,jy in packet)")]
    [Tooltip("Nudge saber position from stick: full deflection moves this many meters (camera right/up).")]
    public bool applyJoystickNudgeFromUdp;
    public float joystickNudgeMeters = 0.4f;

    [Header("Bounds (optional)")]
    public bool clampPosition = true;
    public float maxPositionRadius = 1.5f;

    private Quaternion currentRotation = Quaternion.identity;
    private Vector3 currentPosition;
    private Vector3 angularVelocity;

    void Start()
    {
        if (receiver == null) receiver = FindAnyObjectByType<UDPSaberReceiver>();
        if (saberTransform == null) saberTransform = transform;
        GameplayCameraEnsurer.Ensure();
        if (playerRoot == null && GameplayCameraEnsurer.TryGetPreferredCamera(out Camera playCam))
            playerRoot = playCam.transform;

        Vector3 startPos = basePosition;
        if (hand == SaberHand.Left) startPos.x = -Mathf.Abs(startPos.x);
        currentRotation = saberTransform.rotation;
        currentPosition = playerRoot != null ? playerRoot.TransformPoint(startPos) : startPos;
    }

    void Update()
    {
        if (receiver == null || saberTransform == null) return;

        UDPSaberReceiver.IMUPacket data = hand == SaberHand.Left ? receiver.LeftSaberData : receiver.RightSaberData;
        if (!data.valid)
        {
            // Keep last transform when no data
            saberTransform.SetPositionAndRotation(currentPosition, currentRotation);
            return;
        }

        // Gyroscope: angular velocity (deg/s) -> integrate for orientation
        angularVelocity = new Vector3(
            -data.angularVelocity.x * Mathf.Deg2Rad,
            -data.angularVelocity.y * Mathf.Deg2Rad,
            -data.angularVelocity.z * Mathf.Deg2Rad
        ) * gyroScale;

        Quaternion delta = Quaternion.Euler(angularVelocity * Mathf.Rad2Deg * Time.deltaTime);
        currentRotation = currentRotation * delta;

        // Accelerometer: gravity vector for tilt correction (reduce drift)
        if (data.acceleration.sqrMagnitude > 0.01f)
        {
            Vector3 up = data.acceleration.normalized;
            Vector3 fwd = Vector3.Cross(up, hand == SaberHand.Left ? Vector3.right : -Vector3.right);
            if (fwd.sqrMagnitude < 0.01f) fwd = baseForward;
            else fwd.Normalize();
            Quaternion accelOrientation = Quaternion.LookRotation(fwd, up);
            currentRotation = Quaternion.Slerp(currentRotation, accelOrientation, 0.02f);
        }

        // Position: base + slight offset from accel (optional)
        Vector3 basePos = basePosition;
        if (hand == SaberHand.Left) basePos.x = -Mathf.Abs(basePos.x);
        Vector3 worldBase = playerRoot != null ? playerRoot.TransformPoint(basePos) : basePos;
        Vector3 stickShift = Vector3.zero;
        if (applyJoystickNudgeFromUdp && data.hasControllerExtras && playerRoot != null && joystickNudgeMeters > 0f)
        {
            float nx = (data.joystickX - 0.5f) * 2f;
            float ny = (data.joystickY - 0.5f) * 2f;
            stickShift = (playerRoot.right * nx + playerRoot.up * ny) * (joystickNudgeMeters * 0.5f);
        }
        Vector3 accelOffset = (Vector3)data.acceleration * accelPositionScale;
        currentPosition = worldBase + stickShift + currentRotation * accelOffset;
        if (clampPosition)
        {
            currentPosition.y = Mathf.Clamp(currentPosition.y, worldBase.y - maxPositionRadius, worldBase.y + maxPositionRadius);
            float xz = new Vector2(currentPosition.x - worldBase.x, currentPosition.z - worldBase.z).magnitude;
            if (xz > maxPositionRadius)
            {
                var diff = new Vector2(currentPosition.x - worldBase.x, currentPosition.z - worldBase.z);
                diff = diff.normalized * maxPositionRadius;
                currentPosition.x = worldBase.x + diff.x;
                currentPosition.z = worldBase.z + diff.y;
            }
        }

        saberTransform.SetPositionAndRotation(currentPosition, currentRotation);
    }

    /// <summary>
    /// Returns the current angular velocity magnitude (for swing detection).
    /// </summary>
    public float GetAngularVelocityMagnitude()
    {
        return angularVelocity.magnitude;
    }

    /// <summary>
    /// Returns the world-space velocity of the saber tip (position delta / dt).
    /// </summary>
    public Vector3 GetTipVelocity()
    {
        if (saberTransform == null) return Vector3.zero;
        Vector3 tip = saberTransform.position + saberTransform.forward * 0.5f;
        return angularVelocity.magnitude * 0.5f * saberTransform.forward;
    }
}
