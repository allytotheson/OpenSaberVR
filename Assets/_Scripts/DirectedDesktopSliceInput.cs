using UnityEngine;

/// <summary>
/// Detects saber slices from IMU angular velocity (gyroscope magnitude) on COM6/COM7 serial controllers.
/// Replaces keyboard-based Q/Z/O/M input. When the gyro magnitude of a hand exceeds
/// <see cref="sliceGyroThreshold"/> (deg/s), the nearest qualifying note for that hand is cut.
/// All blocks are treated as non-directional — only the correct hand (left/right) is matched.
/// </summary>
[DefaultExecutionOrder(298)]
public sealed class DirectedDesktopSliceInput : MonoBehaviour
{
    [Header("IMU Slice Detection")]
    [Tooltip("Gyroscope magnitude (deg/s) that counts as a slice. Lower = more sensitive. Tune between 120-250.")]
    public float sliceGyroThreshold = 175f;

    [Tooltip("Minimum seconds between two slices on the same hand (prevents double-hits when SwingDetector and gyro both fire).")]
    public float sliceCooldown = 0.55f;

    [Header("Note hit window")]
    [Tooltip("Max |signed distance| from the hit plane for a note to be hittable (meters). Smaller = must time cuts closer to the plane.")]
    public float maxPlaneDistanceMeters = 3.0f;

    [Tooltip("Max distance from the gameplay camera to the note (meters).")]
    public float maxDistanceFromPlayerMeters = 4.5f;

    [Tooltip("Pulse length passed to SwingDetector (drives visual/overlap gate).")]
    public float keyboardSlashPulseSeconds = 0.42f;

    // Per-hand cooldown timers
    float _leftCooldown;
    float _rightCooldown;

    // Rising-edge detection: were we above threshold last frame?
    bool _prevLeftAbove;
    bool _prevRightAbove;

    ScoreManager _score;
    Saber _leftSaberHaptics;
    Saber _rightSaberHaptics;

    void Awake()
    {
        // This component still operates in non-XR desktop/IMU mode.
        // Keep enabled even if XR device is active so it can work with the serial controllers.
    }

    void Start()
    {
        _score = FindAnyObjectByType<ScoreManager>();
        CacheSaberHaptics();
    }

    void CacheSaberHaptics()
    {
        if (!DesktopSaberTestInput.TryResolveSabers(out GameObject left, out GameObject right))
            return;
        if (left != null)
            _leftSaberHaptics = left.GetComponentInParent<Saber>();
        if (right != null)
            _rightSaberHaptics = right.GetComponentInParent<Saber>();
    }

    void LateUpdate()
    {
        // Count down cooldown timers.
        if (_leftCooldown > 0f)  _leftCooldown  -= Time.deltaTime;
        if (_rightCooldown > 0f) _rightCooldown -= Time.deltaTime;

        var imu = ImuSourceResolver.GetActiveSource();
        if (imu == null)
            return;

        float leftGyro  = imu.LeftSaberData.valid  ? imu.LeftSaberData.angularVelocity.magnitude  : 0f;
        float rightGyro = imu.RightSaberData.valid ? imu.RightSaberData.angularVelocity.magnitude : 0f;

        // Rising edge: above threshold this frame but not last frame.
        bool leftAbove  = leftGyro  >= sliceGyroThreshold;
        bool rightAbove = rightGyro >= sliceGyroThreshold;

        if (leftAbove && !_prevLeftAbove && _leftCooldown <= 0f)
            TrySlice(leftHand: true);

        if (rightAbove && !_prevRightAbove && _rightCooldown <= 0f)
            TrySlice(leftHand: false);

        _prevLeftAbove  = leftAbove;
        _prevRightAbove = rightAbove;

#if UNITY_EDITOR || UNITY_STANDALONE
        // Keep keyboard fallback for editor testing (no hardware attached).
        if (Input.GetKeyDown(KeyCode.Q)) TrySlice(true);
        if (Input.GetKeyDown(KeyCode.Z)) TrySlice(true);
        if (Input.GetKeyDown(KeyCode.O)) TrySlice(false);
        if (Input.GetKeyDown(KeyCode.M)) TrySlice(false);
#endif
    }

    /// <param name="notifyDesktopSlashFx">If false, skip comma/keyboard slash events (use when <see cref="SwingDetector"/> already fired <see cref="SwingDetector.SwingStarted"/> for trail).</param>
    public void TrySlice(bool leftHand, bool notifyDesktopSlashFx = true)
    {
        // Debounce all entry points (gyro rising edge, SwingDetector.TrySliceFromMotionSwing, keys)
        // so one physical motion cannot register two cuts in one cooldown window.
        if (leftHand ? _leftCooldown > 0f : _rightCooldown > 0f)
            return;

        if (!DesktopSaberTestInput.TryResolveSabers(out GameObject leftRoot, out GameObject rightRoot))
            return;

        GameObject hand = leftHand ? leftRoot : rightRoot;
        if (hand == null)
            return;

        DesktopSaberTestInput.PulseSaberHand(hand, keyboardSlashPulseSeconds);
        if (notifyDesktopSlashFx)
        {
            if (leftHand)
                DesktopSaberTestInput.NotifyLeftKeyboardSlashPressed();
            else
                DesktopSaberTestInput.NotifyRightKeyboardSlashPressed();
        }

        DemonHandling bestDh   = null;
        float         bestAbs  = float.MaxValue;

        foreach (var dh in FindObjectsByType<DemonHandling>(FindObjectsInactive.Exclude))
        {
            if (dh == null || !dh.enabled)
                continue;

            var side = dh.GetComponent<SpawnedNoteSaberSide>();
            if (side == null || side.isLeftHandSaber != leftHand)
                continue;

            // Direction is intentionally NOT checked — all blocks are treated as non-directional.

            Vector3 sample = DemonHitDetectorSampleUtil.SampleNotePoint(dh.transform);
            float   s      = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(sample);
            float   abs    = Mathf.Abs(s);
            if (abs > maxPlaneDistanceMeters)
                continue;

            if (!TryGetGameplayCamera(out Camera cam))
                continue;
            float distSq = (sample - cam.transform.position).sqrMagnitude;
            if (distSq > maxDistanceFromPlayerMeters * maxDistanceFromPlayerMeters)
                continue;

            if (abs < bestAbs)
            {
                bestAbs = abs;
                bestDh  = dh;
            }
        }

        if (bestDh == null)
        {
            if (leftHand) _leftCooldown  = sliceCooldown;
            else           _rightCooldown = sliceCooldown;
            return;
        }

        var slice  = hand.GetComponentInChildren<Slice>(true);
        int points = ComputeAccuracyPoints(bestAbs);
        DirectedSliceHitEffects.ApplyHit(bestDh, slice, _score, points,
            leftHand ? _leftSaberHaptics : _rightSaberHaptics);
        DirectedSwipeCornerHud.FlashRegisteredSwipe(leftHand, cutUp: true);

        if (leftHand) _leftCooldown  = sliceCooldown;
        else           _rightCooldown = sliceCooldown;
    }

    int ComputeAccuracyPoints(float absPlaneDistance)
    {
        float t   = Mathf.Clamp01(absPlaneDistance / Mathf.Max(1e-4f, maxPlaneDistanceMeters));
        float inv = 1f - t;
        return Mathf.Clamp(Mathf.RoundToInt(50f + inv * inv * 65f), 1, 115);
    }

    static bool TryGetGameplayCamera(out Camera cam)
    {
        if (GameplayCameraEnsurer.TryGetPreferredCamera(out cam) && cam != null)
            return true;
        cam = Camera.main;
        return cam != null;
    }

    // ---- Public simulation methods (editor / unit tests) ----

    /// <summary>Same as a key slice but without duplicate slash VFX when <see cref="SwingDetector.SwingStarted"/> already drove the trail.</summary>
    public void TrySliceFromMotionSwing(bool leftHand) => TrySlice(leftHand, false);

    /// <summary>Simulate a left-hand slice (e.g. from external test harness).</summary>
    public void SimHardwareLeft()  => TrySlice(true);

    /// <summary>Simulate a right-hand slice (e.g. from external test harness).</summary>
    public void SimHardwareRight() => TrySlice(false);

    // Legacy names kept so any scene references don't break.
    public void SimHardwareLeftDown()  => TrySlice(true);
    public void SimHardwareLeftUp()    => TrySlice(true);
    public void SimHardwareRightDown() => TrySlice(false);
    public void SimHardwareRightUp()   => TrySlice(false);
}

/// <summary>Shared sampling for hit tests (note collider center or position).</summary>
public static class DemonHitDetectorSampleUtil
{
    public static Vector3 SampleNotePoint(Transform demonRoot)
    {
        var col = demonRoot.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;
        return demonRoot.position;
    }
}

/// <summary>Slice + debris + score + flyout (mirrors <see cref="DemonHitDetector"/> success path).</summary>
public static class DirectedSliceHitEffects
{
    public static void ApplyHit(DemonHandling dh, Slice slicer, ScoreManager scoreManager, int accuracyPoints, Saber vrHaptics)
    {
        if (dh == null || !dh.enabled)
            return;

        var root = dh.transform;
        var go   = root.gameObject;

        dh.enabled = false;

        foreach (var c in root.GetComponentsInChildren<Collider>())
        {
            if (c != null)
                c.enabled = false;
        }

        if (slicer != null)
        {
            GameObject[] cutted = slicer.SliceObject(go);
            if (cutted != null && cutted.Length > 0)
            {
                var debris   = Object.Instantiate(go);
                var debrisDh = debris.GetComponent<DemonHandling>();
                if (debrisDh != null)
                    debrisDh.enabled = false;
                var col = debris.GetComponentInChildren<Collider>();
                if (col != null)
                    col.enabled = false;
                debris.layer = 0;

                foreach (var r in debris.GetComponentsInChildren<Renderer>())
                    r.enabled = false;
                foreach (var cut in cutted)
                {
                    if (cut == null)
                        continue;
                    cut.transform.SetParent(debris.transform);
                    cut.AddComponent<BoxCollider>();
                    var rb = cut.AddComponent<Rigidbody>();
                    rb.useGravity = true;
                }

                debris.transform.SetPositionAndRotation(root.position, root.rotation);
                Object.Destroy(debris, 2f);
            }
        }

        if (scoreManager != null)
        {
            int pts = scoreManager.RegisterDirectedHit(accuracyPoints);
            HitMissFlyout.ShowPointGain(pts);
        }

        if (vrHaptics != null)
            vrHaptics.TriggerHitHaptic();

        Object.Destroy(go);
    }
}
