using UnityEngine;

/// <summary>
/// Orchestrator: each frame picks the highest-priority <see cref="ISaberInputProvider"/> on this
/// GameObject (or children) and delegates saber driving. Falls back to built-in IMU integration
/// when no providers are attached. Exposes angular/tip velocity for <see cref="SwingDetector"/>.
/// </summary>
public class SaberMotionController : MonoBehaviour
{
    public enum SaberHand { Left, Right }

    [Header("Saber Assignment")]
    public SaberHand hand = SaberHand.Left;

    [Header("References")]
    [Tooltip("UDP receiver (Pico W Wi-Fi). Used by built-in fallback and ImuSaberInputProvider.")]
    public UDPSaberReceiver receiver;
    [Tooltip("USB serial receiver. When set and enabled, overrides UDP.")]
    public SerialSaberReceiver serialReceiver;
    public Transform saberTransform;

    [Header("Calibration (built-in fallback)")]
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

    [Header("UDP Joystick (optional)")]
    public bool applyJoystickNudgeFromUdp;
    public float joystickNudgeMeters = 0.4f;

    [Header("Bounds")]
    public bool clampPosition = true;
    public float maxPositionRadius = 1.5f;

    [Tooltip("Blade length in meters (hilt to tip along local forward).")]
    public float bladeLength = 0.5f;

    [Header("Debug")]
    [Tooltip("Force a specific provider type (0 = auto, 1 = IMU, 2 = VR, 3 = Desktop).")]
    public int forceProviderIndex;

    [Header("IMU Fusion (built-in fallback)")]
    [Range(0.9f, 0.999f)]
    [Tooltip("Complementary filter alpha: 1 = pure gyro, lower = more accel trust.")]
    public float complementaryAlpha = 0.98f;
    [Tooltip("Estimate and subtract gyro bias when saber is stationary.")]
    public bool estimateGyroBias = true;
    [Range(0f, 0.01f)]
    public float gyroBiasLearnRate = 0.001f;

    private Quaternion _fallbackRotation = Quaternion.identity;
    private Vector3 _fallbackPosition;
    private Vector3 _fallbackPreviousPosition;
    private Vector3 _fallbackAngularVelocity;
    private Vector3 _fallbackGyroBias;

    private ISaberInputProvider _activeProvider;
    private ISaberInputProvider[] _providers;
    private bool _fallbackInitialized;

    /// <summary>Currently active provider, or null if using built-in fallback.</summary>
    public ISaberInputProvider ActiveProvider => _activeProvider;

    void Start()
    {
        if (receiver == null) receiver = Object.FindAnyObjectByType<UDPSaberReceiver>();
        if (serialReceiver == null) serialReceiver = Object.FindAnyObjectByType<SerialSaberReceiver>();
        if (saberTransform == null) saberTransform = transform;
        GameplayCameraEnsurer.Ensure();
        if (playerRoot == null && GameplayCameraEnsurer.TryGetPreferredCamera(out Camera cam))
            playerRoot = cam.transform;

        RefreshProviders();
        InitFallback();
    }

    void RefreshProviders()
    {
        var list = new System.Collections.Generic.List<ISaberInputProvider>();
        foreach (var p in GetComponents<ISaberInputProvider>())
            list.Add(p);
        foreach (var p in GetComponentsInChildren<ISaberInputProvider>())
        {
            if (!list.Contains(p))
                list.Add(p);
        }
        _providers = list.ToArray();
    }

    void InitFallback()
    {
        Vector3 startPos = basePosition;
        if (hand == SaberHand.Left) startPos.x = -Mathf.Abs(startPos.x);
        _fallbackRotation = saberTransform != null ? saberTransform.rotation : Quaternion.identity;
        _fallbackPosition = playerRoot != null ? playerRoot.TransformPoint(startPos) : startPos;
        _fallbackPreviousPosition = _fallbackPosition;

        if (CalibrationData.TryGet(hand, out CalibrationData.HandCalibration cal))
        {
            _fallbackGyroBias = cal.gyroBias;
            _fallbackRotation = cal.restOrientation;
        }

        _fallbackInitialized = true;
    }

    /// <summary>Active IMU source for consumers that still read packets directly (menu nav, swing bridge).</summary>
    public IImuSaberReceiver GetActiveImuSource()
    {
        if (serialReceiver != null && serialReceiver.isActiveAndEnabled)
            return serialReceiver;
        if (receiver != null)
            return receiver;
        return ImuSourceResolver.GetActiveSource();
    }

    void Update()
    {
        if (_providers == null || _providers.Length == 0)
            RefreshProviders();

        _activeProvider = PickProvider();

        if (_activeProvider != null)
        {
            _activeProvider.UpdateSaber(saberTransform, hand);
            SyncDesktopTestInputEnabled();
            return;
        }

        UpdateFallback();
        SyncDesktopTestInputEnabled();
    }

    ISaberInputProvider PickProvider()
    {
        if (_providers == null) return null;

        if (forceProviderIndex > 0)
        {
            foreach (var p in _providers)
            {
                if (forceProviderIndex == 1 && p is ImuSaberInputProvider) return p;
                if (forceProviderIndex == 2 && p is VrtkSaberInputProvider) return p;
                if (forceProviderIndex == 3 && p is DesktopSaberInputProvider) return p;
            }
        }

        ISaberInputProvider best = null;
        int bestPri = int.MinValue;
        foreach (var p in _providers)
        {
            var mb = p as MonoBehaviour;
            if (mb != null && !mb.isActiveAndEnabled) continue;
            if (!p.IsAvailable) continue;
            if (p.Priority > bestPri)
            {
                bestPri = p.Priority;
                best = p;
            }
        }
        return best;
    }

    /// <summary>
    /// Once any hand detects valid IMU data, latch <see cref="DesktopSaberTestInput.preferUdpImuWhenValid"/>
    /// to true. The per-saber check in <see cref="DesktopSaberTestInput.ShouldUseKeyboard"/> handles
    /// individual hand fallback when one controller is offline.
    /// </summary>
    void SyncDesktopTestInputEnabled()
    {
        var desktop = GetComponentInParent<DesktopSaberTestInput>();
        if (desktop == null) desktop = Object.FindAnyObjectByType<DesktopSaberTestInput>();
        if (desktop == null) return;

        if (desktop.preferUdpImuWhenValid)
            return;

        bool imuDriving = _activeProvider != null && !(_activeProvider is DesktopSaberInputProvider);
        if (imuDriving)
            desktop.preferUdpImuWhenValid = true;
    }

    // ---- Built-in fallback (original SaberMotionController logic) ----

    void UpdateFallback()
    {
        if (!_fallbackInitialized) InitFallback();

        var imu = GetActiveImuSource();
        if (imu == null || saberTransform == null) return;

        var data = hand == SaberHand.Left ? imu.LeftSaberData : imu.RightSaberData;
        if (!data.valid)
        {
            saberTransform.SetPositionAndRotation(_fallbackPosition, _fallbackRotation);
            return;
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
                _fallbackGyroBias = Vector3.Lerp(_fallbackGyroBias, rawGyro, gyroBiasLearnRate);
        }

        _fallbackAngularVelocity = (rawGyro - _fallbackGyroBias) * Mathf.Deg2Rad * gyroScale;

        Quaternion delta = Quaternion.Euler(_fallbackAngularVelocity * Mathf.Rad2Deg * Time.deltaTime);
        Quaternion gyroOrientation = _fallbackRotation * delta;

        if (data.acceleration.sqrMagnitude > 0.01f)
        {
            Vector3 gyroUp = gyroOrientation * Vector3.up;
            Vector3 accelUp = data.acceleration.normalized;
            Quaternion tiltCorrection = Quaternion.FromToRotation(gyroUp, accelUp);
            Quaternion tiltCorrected = tiltCorrection * gyroOrientation;
            _fallbackRotation = Quaternion.Slerp(tiltCorrected, gyroOrientation, complementaryAlpha);
        }
        else
        {
            _fallbackRotation = gyroOrientation;
        }

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
        _fallbackPreviousPosition = _fallbackPosition;
        _fallbackPosition = worldBase + stickShift + _fallbackRotation * accelOffset;

        if (clampPosition)
        {
            _fallbackPosition.y = Mathf.Clamp(_fallbackPosition.y, worldBase.y - maxPositionRadius, worldBase.y + maxPositionRadius);
            float xz = new Vector2(_fallbackPosition.x - worldBase.x, _fallbackPosition.z - worldBase.z).magnitude;
            if (xz > maxPositionRadius)
            {
                var diff = new Vector2(_fallbackPosition.x - worldBase.x, _fallbackPosition.z - worldBase.z);
                diff = diff.normalized * maxPositionRadius;
                _fallbackPosition.x = worldBase.x + diff.x;
                _fallbackPosition.z = worldBase.z + diff.y;
            }
        }

        saberTransform.SetPositionAndRotation(_fallbackPosition, _fallbackRotation);
    }

    public float GetAngularVelocityMagnitude()
    {
        if (_activeProvider != null)
            return _activeProvider.GetAngularVelocityMagnitude();
        return _fallbackAngularVelocity.magnitude;
    }

    public Vector3 GetTipVelocity()
    {
        if (_activeProvider != null)
            return _activeProvider.GetTipVelocity();
        if (saberTransform == null) return Vector3.zero;
        Vector3 tipOffsetWorld = saberTransform.forward * bladeLength;
        Vector3 rotational = Vector3.Cross(_fallbackAngularVelocity, tipOffsetWorld);
        float dt = Mathf.Max(Time.deltaTime, 0.001f);
        Vector3 translational = (_fallbackPosition - _fallbackPreviousPosition) / dt;
        return rotational + translational;
    }
}
