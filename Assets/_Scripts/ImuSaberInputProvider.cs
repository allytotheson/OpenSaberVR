using UnityEngine;

/// <summary>
/// Drives a saber from IMU data (gyro integration + accel tilt correction).
/// Implements <see cref="ISaberInputProvider"/> so <see cref="SaberMotionController"/> can select it
/// alongside VR and desktop providers. Reads from <see cref="IImuSaberReceiver"/> (UDP or serial).
/// </summary>
public class ImuSaberInputProvider : MonoBehaviour, ISaberInputProvider
{
    [Header("IMU Source")]
    [Tooltip("Explicit IMU receiver. If null, resolved via ImuSourceResolver.")]
    public MonoBehaviour imuSourceOverride;

    [Header("Calibration")]
    [Tooltip("Position offset from origin (player space). Left hand uses negative X.")]
    public Vector3 basePosition = new Vector3(0.25f, 1.5f, 0f);
    [Tooltip("Optional parent for local-space positioning (e.g. Main Camera).")]
    public Transform playerRoot;
    [Tooltip("Initial forward direction (saber blade).")]
    public Vector3 baseForward = Vector3.forward;
    [Tooltip("Scale gyro deg/s to Unity angular velocity.")]
    public float gyroScale = 1f;
    [Tooltip("Scale accel (g) to position influence.")]
    public float accelPositionScale = 0.01f;

    [Header("Startup Auto-Calibration")]
    [Tooltip("Seconds after first valid packet to silently collect gyro/accel samples and compute bias + rest orientation. 0 = disabled.")]
    public float startupCalibrationDuration = 2.5f;

    [Header("IMU Fusion")]
    [Range(0.9f, 0.999f)]
    [Tooltip("Complementary filter: 1 = pure gyro, lower = more accel trust.")]
    public float complementaryAlpha = 0.98f;
    [Tooltip("Estimate and subtract gyro bias when saber is stationary.")]
    public bool estimateGyroBias = true;
    [Range(0f, 0.01f)]
    public float gyroBiasLearnRate = 0.001f;

    [Header("UDP Joystick (optional)")]
    public bool applyJoystickNudge;
    public float joystickNudgeMeters = 0.4f;

    [Header("Bounds")]
    public bool clampPosition = true;
    public float maxPositionRadius = 1.5f;

    [Tooltip("Blade length in meters (hilt to tip along local forward).")]
    public float bladeLength = 0.5f;

    public int Priority => 50;

    public bool IsAvailable
    {
        get
        {
            var imu = ResolveImuSource();
            if (imu == null) return false;
            var p = _lastHand == SaberMotionController.SaberHand.Left ? imu.LeftSaberData : imu.RightSaberData;
            return p.valid;
        }
    }

    private Quaternion _currentRotation = Quaternion.identity;
    private Vector3 _currentPosition;
    private Vector3 _previousPosition;
    private Vector3 _angularVelocity;
    private Vector3 _gyroBias;
    private SaberMotionController.SaberHand _lastHand;
    private bool _initialized;

    // Startup auto-calibration state
    private bool _startupCalDone;
    private float _startupCalTimer;
    private int _startupCalCount;
    private Vector3 _startupGyroAccum;
    private Vector3 _startupAccelAccum;

    public IImuSaberReceiver ResolveImuSource()
    {
        if (imuSourceOverride is IImuSaberReceiver src)
            return src;
        return ImuSourceResolver.GetActiveSource();
    }

    public void Initialize(SaberMotionController.SaberHand hand, Transform saberTransform)
    {
        _lastHand = hand;
        if (playerRoot == null && GameplayCameraEnsurer.TryGetPreferredCamera(out Camera cam))
            playerRoot = cam.transform;

        // --- Diagnostic log ---
        var imu = ResolveImuSource();
        if (imu == null)
            Debug.LogWarning($"[ImuSaberInputProvider] Hand={hand} on '{gameObject.name}': no IMU source found. " +
                             $"Check UDPSaberReceiver exists and ports 5000/5001 are open in Windows Firewall.");
        else
        {
            var pkt = hand == SaberMotionController.SaberHand.Left ? imu.LeftSaberData : imu.RightSaberData;
            Debug.Log($"[ImuSaberInputProvider] Hand={hand} on '{gameObject.name}': IMU source={imu.GetType().Name}, " +
                      $"current packet valid={pkt.valid}, hasExtras={pkt.hasControllerExtras}.");
        }

        Vector3 startPos = basePosition;
        if (hand == SaberMotionController.SaberHand.Left)
            startPos.x = -Mathf.Abs(startPos.x);

        _currentRotation = saberTransform != null ? saberTransform.rotation : Quaternion.identity;
        _currentPosition = playerRoot != null ? playerRoot.TransformPoint(startPos) : startPos;
        _previousPosition = _currentPosition;

        if (CalibrationData.TryGet(hand, out CalibrationData.HandCalibration cal))
        {
            _gyroBias = cal.gyroBias;
            _currentRotation = cal.restOrientation;
        }

        _initialized = true;
    }

    public void UpdateSaber(Transform saberTransform, SaberMotionController.SaberHand hand)
    {
        _lastHand = hand;
        if (!_initialized)
            Initialize(hand, saberTransform);

        var imu = ResolveImuSource();
        if (imu == null || saberTransform == null) return;

        var data = hand == SaberMotionController.SaberHand.Left ? imu.LeftSaberData : imu.RightSaberData;
        if (!data.valid)
        {
            saberTransform.SetPositionAndRotation(_currentPosition, _currentRotation);
            return;
        }

        // Silent startup auto-calibration: collect samples for the first N seconds of valid data,
        // then commit gyro bias + rest orientation without any UI.
        if (!_startupCalDone && startupCalibrationDuration > 0f)
        {
            _startupCalTimer += Time.deltaTime;
            _startupGyroAccum += data.angularVelocity;
            _startupAccelAccum += data.acceleration;
            _startupCalCount++;

            if (_startupCalTimer >= startupCalibrationDuration && _startupCalCount > 0)
            {
                Vector3 avgGyro  = _startupGyroAccum / _startupCalCount;
                Vector3 avgAccel = _startupAccelAccum / _startupCalCount;

                // Gyro bias is the raw average (subtract to correct future readings).
                Vector3 rawBias = new Vector3(-avgGyro.x, -avgGyro.y, -avgGyro.z);

                // Rest orientation from average gravity direction.
                Vector3 up = avgAccel.normalized;
                Vector3 fwd = Vector3.Cross(up, hand == SaberMotionController.SaberHand.Left ? Vector3.right : -Vector3.right);
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                else fwd.Normalize();
                Quaternion restOri = Quaternion.LookRotation(fwd, up);

                _gyroBias = rawBias;
                _currentRotation = restOri;

                CalibrationData.Set(hand, new CalibrationData.HandCalibration
                {
                    gyroBias = rawBias,
                    restOrientation = restOri,
                    restAccel = avgAccel,
                    isCalibrated = true
                });

                _startupCalDone = true;
                Debug.Log($"[ImuSaberInputProvider] {hand} auto-cal done after {_startupCalCount} samples. " +
                          $"Bias={rawBias:F3}, RestAccel={avgAccel:F3}");
            }
        }
        else
        {
            _startupCalDone = true; // startupCalibrationDuration == 0: skip
        }

        Vector3 rawGyro = new Vector3(
            -data.angularVelocity.x,
            -data.angularVelocity.y,
            -data.angularVelocity.z
        );

        if (estimateGyroBias)
        {
            bool stationary = Mathf.Abs(data.acceleration.magnitude - 1f) < 0.08f
                           && rawGyro.magnitude < 5f;
            if (stationary)
                _gyroBias = Vector3.Lerp(_gyroBias, rawGyro, gyroBiasLearnRate);
        }

        Vector3 correctedGyro = (rawGyro - _gyroBias) * Mathf.Deg2Rad * gyroScale;
        _angularVelocity = correctedGyro;

        Quaternion gyroDelta = Quaternion.Euler(correctedGyro * Mathf.Rad2Deg * Time.deltaTime);
        Quaternion gyroOrientation = _currentRotation * gyroDelta;

        if (data.acceleration.sqrMagnitude > 0.01f)
        {
            Quaternion tiltCorrected = ApplyGravityTiltCorrection(gyroOrientation, data.acceleration.normalized, hand);
            _currentRotation = Quaternion.Slerp(tiltCorrected, gyroOrientation, complementaryAlpha);
        }
        else
        {
            _currentRotation = gyroOrientation;
        }

        Vector3 basePos = basePosition;
        if (hand == SaberMotionController.SaberHand.Left)
            basePos.x = -Mathf.Abs(basePos.x);
        Vector3 worldBase = playerRoot != null ? playerRoot.TransformPoint(basePos) : basePos;

        Vector3 stickShift = Vector3.zero;
        if (applyJoystickNudge && data.hasControllerExtras && playerRoot != null && joystickNudgeMeters > 0f)
        {
            float nx = (data.joystickX - 0.5f) * 2f;
            float ny = (data.joystickY - 0.5f) * 2f;
            stickShift = (playerRoot.right * nx + playerRoot.up * ny) * (joystickNudgeMeters * 0.5f);
        }

        Vector3 accelOffset = (Vector3)data.acceleration * accelPositionScale;
        _previousPosition = _currentPosition;
        _currentPosition = worldBase + stickShift + _currentRotation * accelOffset;

        if (clampPosition)
        {
            _currentPosition.y = Mathf.Clamp(_currentPosition.y, worldBase.y - maxPositionRadius, worldBase.y + maxPositionRadius);
            float xz = new Vector2(_currentPosition.x - worldBase.x, _currentPosition.z - worldBase.z).magnitude;
            if (xz > maxPositionRadius)
            {
                var diff = new Vector2(_currentPosition.x - worldBase.x, _currentPosition.z - worldBase.z);
                diff = diff.normalized * maxPositionRadius;
                _currentPosition.x = worldBase.x + diff.x;
                _currentPosition.z = worldBase.z + diff.y;
            }
        }

        // During startup calibration, suppress reported velocity so SwingDetector
        // doesn't see the initial position/rotation jump as a swing.
        if (!_startupCalDone)
        {
            _angularVelocity = Vector3.zero;
            _previousPosition = _currentPosition;
        }

        saberTransform.SetPositionAndRotation(_currentPosition, _currentRotation);
    }

    /// <summary>
    /// Corrects only pitch and roll from the accelerometer (gravity). Yaw is untouched since
    /// the accelerometer cannot sense rotation about the gravity axis.
    /// </summary>
    Quaternion ApplyGravityTiltCorrection(Quaternion gyroOri, Vector3 accelUp, SaberMotionController.SaberHand hand)
    {
        Vector3 gyroUp = gyroOri * Vector3.up;
        Quaternion tiltCorrection = Quaternion.FromToRotation(gyroUp, accelUp);
        return tiltCorrection * gyroOri;
    }

    public float GetAngularVelocityMagnitude() => _angularVelocity.magnitude;

    public Vector3 GetTipVelocity()
    {
        Vector3 tipOffsetWorld = (_currentRotation * Vector3.forward) * bladeLength;
        Vector3 rotational = Vector3.Cross(_angularVelocity, tipOffsetWorld);
        float dt = Mathf.Max(Time.deltaTime, 0.001f);
        Vector3 translational = (_currentPosition - _previousPosition) / dt;
        return rotational + translational;
    }
}
