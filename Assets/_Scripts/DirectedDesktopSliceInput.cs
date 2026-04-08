using UnityEngine;

/// <summary>
/// Non-VR desktop: Q/Z/O/M (JSON <c>_cutDirection</c> 0 = down/BOTTOM, 1 = up/TOP; <c>_type</c> 0 = left, 1 = right).
/// Q/Z = left saber, O/M = right. Cut direction is <b>flipped</b> vs “top-row = up”: Q/O match <c>_cutDirection</c> 1, Z/M match 0.
/// <list type="bullet">
/// <item>Q — left, <c>_cutDirection</c> 1 (up)</item>
/// <item>Z — left, <c>_cutDirection</c> 0 (down)</item>
/// <item>O — right, <c>_cutDirection</c> 1 (up)</item>
/// <item>M — right, <c>_cutDirection</c> 0 (down)</item>
/// </list>
/// Notes must be near the hit plane and within <see cref="maxDistanceFromPlayerMeters"/> of the gameplay camera.
/// Keyboard pulse hits are handled only here; <see cref="DemonHitDetector"/> skips destroying while <see cref="SwingDetector.IsKeyboardPulseSwinging"/>.
/// </summary>
[DefaultExecutionOrder(298)]
public sealed class DirectedDesktopSliceInput : MonoBehaviour
{
    [Header("Directed hit window (tune here)")]
    [Tooltip("Beyond this |signed distance| from the hit plane the slice does not register (larger = easier timing).")]
    public float maxPlaneDistanceMeters = 0.66f;

    [Tooltip("Note must be at most this far from the gameplay camera (meters) to register.")]
    public float maxDistanceFromPlayerMeters = 4.5f;

    [Tooltip("Pulse length passed to SwingDetector (visual / overlap gate).")]
    public float keyboardSlashPulseSeconds = 0.42f;

    [Tooltip("Disable DesktopSaberTestInput auto-pulse near plane so only directed keys cut.")]
    public bool disableAutoPulseNearNote = true;

    ScoreManager _score;
    Saber _leftSaberHaptics;
    Saber _rightSaberHaptics;

    void Awake()
    {
        if (GameplayCameraEnsurer.IsXrDeviceActive())
            enabled = false;
    }

    void Start()
    {
        _score = FindAnyObjectByType<ScoreManager>();

        if (disableAutoPulseNearNote && !GameplayCameraEnsurer.IsXrDeviceActive())
        {
            foreach (var d in FindObjectsByType<DesktopSaberTestInput>(FindObjectsInactive.Include))
            {
                if (d != null)
                    d.autoPulseSwingNearNote = false;
            }
        }

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
#if UNITY_EDITOR || UNITY_STANDALONE
        if (GameplayCameraEnsurer.IsXrDeviceActive())
            return;

        // Q,Z = left; O,M = right. Flipped cut vs geometric row: Q/O → JSON 1, Z/M → JSON 0.
        if (Input.GetKeyDown(KeyCode.Q))
            TryDirectedSlice(leftHand: true, cutUp: true);
        if (Input.GetKeyDown(KeyCode.Z))
            TryDirectedSlice(leftHand: true, cutUp: false);
        if (Input.GetKeyDown(KeyCode.O))
            TryDirectedSlice(leftHand: false, cutUp: true);
        if (Input.GetKeyDown(KeyCode.M))
            TryDirectedSlice(leftHand: false, cutUp: false);
#endif
    }

    /// <summary>Left hand, down — <c>_cutDirection</c> 0 (keyboard: Z).</summary>
    public void SimHardwareLeftDown() => TryDirectedSlice(true, cutUp: false);

    /// <summary>Left hand, up — <c>_cutDirection</c> 1 (keyboard: Q).</summary>
    public void SimHardwareLeftUp() => TryDirectedSlice(true, cutUp: true);

    /// <summary>Right hand, down — <c>_cutDirection</c> 0 (keyboard: M).</summary>
    public void SimHardwareRightDown() => TryDirectedSlice(false, cutUp: false);

    /// <summary>Right hand, up — <c>_cutDirection</c> 1 (keyboard: O).</summary>
    public void SimHardwareRightUp() => TryDirectedSlice(false, cutUp: true);

    void TryDirectedSlice(bool leftHand, bool cutUp)
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

        NotesSpawner.CutDirection expected = cutUp ? NotesSpawner.CutDirection.TOP : NotesSpawner.CutDirection.BOTTOM;

        DemonHandling bestDh = null;
        float bestAbs = float.MaxValue;

        foreach (var dh in FindObjectsByType<DemonHandling>(FindObjectsInactive.Exclude))
        {
            if (dh == null || !dh.enabled)
                continue;

            var side = dh.GetComponent<SpawnedNoteSaberSide>();
            var cut = dh.GetComponent<SpawnedNoteCutDirection>();
            if (side == null || cut == null)
                continue;

            if (side.isLeftHandSaber != leftHand)
                continue;
            if (cut.CutDirection != expected)
                continue;

            Vector3 sample = DemonHitDetectorSampleUtil.SampleNotePoint(dh.transform);
            float s = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(sample);
            float abs = Mathf.Abs(s);
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
                bestDh = dh;
            }
        }

        if (bestDh == null)
            return;

        var slice = hand.GetComponentInChildren<Slice>(true);
        int points = ComputeAccuracyPoints(bestAbs);
        DirectedSliceHitEffects.ApplyHit(bestDh, slice, _score, points,
            leftHand ? _leftSaberHaptics : _rightSaberHaptics);
        DirectedSwipeCornerHud.FlashRegisteredSwipe(leftHand, cutUp);
    }

    int ComputeAccuracyPoints(float absPlaneDistance)
    {
        float t = Mathf.Clamp01(absPlaneDistance / Mathf.Max(1e-4f, maxPlaneDistanceMeters));
        float inv = 1f - t;
        // Beat Saber–like band: ~50 worst in window, 115 at perfect
        return Mathf.Clamp(Mathf.RoundToInt(50f + inv * inv * 65f), 1, 115);
    }

    static bool TryGetGameplayCamera(out Camera cam)
    {
        if (GameplayCameraEnsurer.TryGetPreferredCamera(out cam) && cam != null)
            return true;
        cam = Camera.main;
        return cam != null;
    }
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
        var go = root.gameObject;

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
                var debris = Object.Instantiate(go);
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
