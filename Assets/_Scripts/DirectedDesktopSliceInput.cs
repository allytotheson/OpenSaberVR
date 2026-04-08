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
    public float sliceGyroThreshold = 150f;

    [Tooltip("Minimum seconds between two slices on the same hand (prevents double-hits from one swing).")]
    public float sliceCooldown = 0.3f;

    [Header("Note hit window")]
    [Tooltip("Max |signed distance| from the hit plane for a note to be hittable (meters).")]
    public float maxPlaneDistanceMeters = 0.66f;

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
        {
            TrySlice(leftHand: true);
            _leftCooldown = sliceCooldown;
        }

        if (rightAbove && !_prevRightAbove && _rightCooldown <= 0f)
        {
            TrySlice(leftHand: false);
            _rightCooldown = sliceCooldown;
        }

        _prevLeftAbove  = leftAbove;
        _prevRightAbove = rightAbove;

#if UNITY_EDITOR || UNITY_STANDALONE
        // Keep keyboard fallback for editor testing (no hardware attached).
        if (Input.GetKeyDown(KeyCode.Q)) { TrySlice(true);  _leftCooldown  = sliceCooldown; }
        if (Input.GetKeyDown(KeyCode.Z)) { TrySlice(true);  _leftCooldown  = sliceCooldown; }
        if (Input.GetKeyDown(KeyCode.O)) { TrySlice(false); _rightCooldown = sliceCooldown; }
        if (Input.GetKeyDown(KeyCode.M)) { TrySlice(false); _rightCooldown = sliceCooldown; }
#endif
    }

    void TrySlice(bool leftHand)
    {
        if (!DesktopSaberTestInput.TryResolveSabers(out GameObject leftRoot, out GameObject rightRoot))
            return;

        GameObject hand = leftHand ? leftRoot : rightRoot;
        if (hand == null)
            return;

        DesktopSaberTestInput.PulseSaberHand(hand, keyboardSlashPulseSeconds);
        if (leftHand)
            DesktopSaberTestInput.NotifyLeftKeyboardSlashPressed();
        else
            DesktopSaberTestInput.NotifyRightKeyboardSlashPressed();

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
            return;

        var slice  = hand.GetComponentInChildren<Slice>(true);
        int points = ComputeAccuracyPoints(bestAbs);
        DirectedSliceHitEffects.ApplyHit(bestDh, slice, _score, points,
            leftHand ? _leftSaberHaptics : _rightSaberHaptics);
        DirectedSwipeCornerHud.FlashRegisteredSwipe(leftHand, cutUp: true);
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
            scoreManager.RegisterDirectedHit(accuracyPoints);
        HitMissFlyout.Show($"+{accuracyPoints}", new Color(0.35f, 1f, 0.55f, 1f));

        if (vrHaptics != null)
            vrHaptics.TriggerHitHaptic();

        Object.Destroy(go);
    }
}
