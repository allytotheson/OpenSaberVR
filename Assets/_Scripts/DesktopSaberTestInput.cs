using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Desktop / keyboard saber driving: auto-align at the hit plane, optional jitter, Z/X/Space slashes.
/// <see cref="alignSaberRotationToBlockArrow"/> matches block cut direction; turn off for a fixed side-hold pose.
/// </summary>
[DefaultExecutionOrder(60)]
[DisallowMultipleComponent]
public class DesktopSaberTestInput : MonoBehaviour
{
    /// <summary>Fired when comma, directed Z/Q, or Space (left) triggers a left-hand desktop slash pulse.</summary>
    public static event System.Action LeftKeyboardSlashPressed;

    /// <summary>Fired when period, directed M/O, or Space (right) triggers a right-hand desktop slash pulse.</summary>
    public static event System.Action RightKeyboardSlashPressed;

    public static void NotifyLeftKeyboardSlashPressed() => LeftKeyboardSlashPressed?.Invoke();

    public static void NotifyRightKeyboardSlashPressed() => RightKeyboardSlashPressed?.Invoke();

    [Header("Drive source")]
    [Tooltip("If false (default): keyboard + auto-align always drive sabers when this script runs. If true: valid UDP IMU replaces keyboard hand motion; Z/X/Space slash pulses still apply.")]
    public bool preferUdpImuWhenValid = false;

    [Header("Auto-align")]
    [Tooltip("Blend note world position with camera hand anchors so sabers stay in frame.")]
    [Range(0f, 1f)] public float alignBlendFromNote = 0.78f;

    [Tooltip("When align rotation to block is off: how much the block steers saber yaw/pitch/roll. 0 = fixed human side-hold pose.")]
    [Range(0f, 1f)] public float noteRotationBlend = 0f;

    [Tooltip("Match saber orientation to the block (arrow / cut direction). Turn off for the fixed diagonal side-hold pose.")]
    public bool alignSaberRotationToBlockArrow = true;

    [Tooltip("When on, hands follow the nearest note at the hit plane (non-VR desktop). Turn off for pure WASD free placement.")]
    public bool autoAlignSabersToNotes = true;

    [Tooltip("Non-VR desktop: minimum align smoothing (higher = steadier hands).")]
    public float desktopAlignSmoothing = 22f;

    [Tooltip("Non-VR desktop: disable idle Perlin jitter so blades read clearly.")]
    public bool suppressDesktopIdleJitter = true;

    [Tooltip("Diagonal in the vertical swing plane: 45 ≈ top-to-bottom slash feel vs track.")]
    [Range(15f, 75f)] public float slashDiagonalDegrees = 45f;

    [Tooltip("Offset from camera (local space): wider |X| = hilts farther left/right.")]
    public Vector3 leftHandLocal = new Vector3(-0.72f, -0.11f, 0.72f);
    public Vector3 rightHandLocal = new Vector3(0.72f, -0.11f, 0.72f);

    [Header("Desktop scene vs camera")]
    [Tooltip("Non-VR auto-align: if the hand root is farther than this from the camera anchor (e.g. OpenSaber scene pose down-track), snap to target instead of lerping — avoids swords visibly flying in from the track.")]
    [Min(0f)]
    public float desktopSnapIfBeyondMeters = 2.75f;

    [Header("Desktop blade aim")]
    [Range(0f, 1f)]
    [Tooltip("Tips aim toward screen center / blocks; 0 keeps the old mostly-down-track diagonal.")]
    public float desktopBladeTipInwardBlend = 0.58f;

    [Range(0f, 1f)]
    [Tooltip("Caps block-arrow rotation on desktop so sabers keep an inward V. Use 1 for full block matching (old behavior).")]
    public float desktopMaxNoteRotationBlend = 0.48f;

    [Tooltip("Push aligned point slightly toward camera from the block.")]
    public float pullTowardCameraFromBlock = 0.26f;

    [Header("Motion feel")]
    public float alignSmoothing = 14f;
    public float jitterAmplitude = 0.028f;
    public float jitterFrequency = 2.8f;
    public float rotationJitterDegrees = 2.2f;

    [Header("Manual WASD (when auto-align off)")]
    public float moveSpeed = 3.2f;
    public float yawSpeed = 110f;

    [Tooltip("Optional: custom alignment (e.g. future RPi-fused targets). If null, uses SaberAlignmentQueries.")]
    public MonoBehaviour alignmentProviderBehaviour;

    [Header("Desktop hits")]
    [Tooltip("Brief swing pulses when a matching note is near the hit plane so cuts register without mashing Z/X.")]
    public bool autoPulseSwingNearNote = true;

    [Tooltip("Minimum seconds between auto pulses per hand.")]
    public float autoPulseMinInterval = 0.2f;

    [Header("Keyboard slash")]
    [Tooltip("How long the desktop swing gate stays open after a key press (for DemonHitDetector).")]
    public float keyboardSlashPulseSeconds = 0.42f;

    private float _noiseSeedLeft;
    private float _noiseSeedRight;
    private float _nextAutoPulseLeft;
    private float _nextAutoPulseRight;

    void Awake()
    {
        _noiseSeedLeft = Random.Range(0f, 100f);
        _noiseSeedRight = Random.Range(100f, 200f);
    }

    void Update()
    {
        // Read keys in Update so presses are not missed (LateUpdate ordering / frame drops).
        if (!TryResolveSabers(out GameObject left, out GameObject right))
            return;

        // Directed cuts: Z/Q/M/O are handled by DirectedDesktopSliceInput (left/right × up/down).
        // Comma/period still pulse for slash FX parity.
        if (left != null && Input.GetKeyDown(KeyCode.Comma))
        {
            PulseSwing(left);
            LeftKeyboardSlashPressed?.Invoke();
        }

        if (right != null && Input.GetKeyDown(KeyCode.Period))
        {
            PulseSwing(right);
            RightKeyboardSlashPressed?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (left != null)
            {
                PulseSwing(left);
                LeftKeyboardSlashPressed?.Invoke();
            }

            if (right != null)
            {
                PulseSwing(right);
                RightKeyboardSlashPressed?.Invoke();
            }
        }
    }

    void LateUpdate()
    {
        if (!TryResolveSabers(out GameObject left, out GameObject right))
            return;

        if (alignmentProviderBehaviour == null)
            alignmentProviderBehaviour = UnityEngine.Object.FindAnyObjectByType<SaberNearestBlockAlignmentProvider>();

        Transform cam = DesktopGameplayCamera.TryGetTransform();
        if (cam == null)
            return;

        bool autoAlign = autoAlignSabersToNotes;

        Vector3 flatF = cam.forward; flatF.y = 0f;
        if (flatF.sqrMagnitude < 0.01f) flatF = Vector3.forward;
        flatF.Normalize();
        Vector3 flatR = cam.right; flatR.y = 0f;
        flatR.Normalize();

        if (left != null)
            ApplyHand(left, true, cam, flatF, flatR, autoAlign);
        if (right != null)
            ApplyHand(right, false, cam, flatF, flatR, autoAlign);
    }

    void PulseSwing(GameObject saberHandRoot)
    {
        PulseSaberHand(saberHandRoot, keyboardSlashPulseSeconds);
    }

    /// <summary>Desktop swing gate for <see cref="DemonHitDetector"/> / directed slice.</summary>
    public static void PulseSaberHand(GameObject saberHandRoot, float seconds)
    {
        if (saberHandRoot == null || seconds <= 0f)
            return;
        var swing = saberHandRoot.GetComponent<SwingDetector>();
        if (swing == null)
            swing = saberHandRoot.GetComponentInChildren<SwingDetector>();
        if (swing == null)
            return;
        swing.PulseTestSwing(seconds);
    }

    private bool ShouldUseKeyboard(GameObject saber, bool isLeft)
    {
        if (!preferUdpImuWhenValid)
            return true;
        foreach (var motion in saber.GetComponentsInChildren<SaberMotionController>(true))
        {
            if (motion == null)
                continue;
            var imu = motion.GetActiveImuSource();
            if (imu == null)
                continue;
            var p = isLeft ? imu.LeftSaberData : imu.RightSaberData;
            if (p.valid)
                return false;
        }
        return true;
    }

    private void ApplyHand(GameObject saber, bool isLeft, Transform cam, Vector3 flatF, Vector3 flatR, bool autoAlign)
    {
        var swing = saber.GetComponent<SwingDetector>();
        if (swing == null) swing = saber.GetComponentInChildren<SwingDetector>();

        bool keyboardDrives = ShouldUseKeyboard(saber, isLeft);

        if (!keyboardDrives)
            return;

        Transform t = saber.transform;
        float dt = Time.deltaTime;
        bool nonXrDesktop = !GameplayCameraEnsurer.IsXrDeviceActive();
        float effSmoothing = alignSmoothing;
        if (nonXrDesktop)
            effSmoothing = Mathf.Max(effSmoothing, desktopAlignSmoothing);
        float smooth = 1f - Mathf.Exp(-effSmoothing * dt);

        bool calmDesktop = nonXrDesktop && suppressDesktopIdleJitter;

        if (autoAlign)
        {
            Vector3 anchor = cam.TransformPoint(isLeft ? leftHandLocal : rightHandLocal);
            Quaternion humanHold = ComputeHumanSideHoldRotation(isLeft, cam, flatF, flatR);
            Quaternion targetRot = humanHold;
            Vector3 targetPos = anchor;

            if (TryGetAlignmentTarget(isLeft, cam, out Vector3 notePos, out Quaternion noteRot))
            {
                // Keep hands at the cyan plane; only track lateral / vertical motion of the note (no lunge down-track).
                Vector3 noteOnPlane = BeatSaberHitLineGuide.ProjectOntoGameplayHitPlane(notePos);
                Vector3 towardCam = cam.position - noteOnPlane;
                if (towardCam.sqrMagnitude > 1e-6f)
                    towardCam.Normalize();
                else
                    towardCam = -cam.forward;

                Vector3 aligned = noteOnPlane + towardCam * pullTowardCameraFromBlock;
                float blend = alignBlendFromNote;
                if (nonXrDesktop)
                    blend = Mathf.Clamp01(Mathf.Max(blend, 0.52f));
                targetPos = Vector3.Lerp(anchor, aligned, blend);
                float rotBlend = alignSaberRotationToBlockArrow ? 1f : noteRotationBlend;
                if (nonXrDesktop)
                    rotBlend = Mathf.Min(rotBlend, Mathf.Clamp01(desktopMaxNoteRotationBlend));
                targetRot = Quaternion.Slerp(humanHold, noteRot, rotBlend);

                if (autoPulseSwingNearNote && swing != null)
                {
                    float along = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(notePos);
                    if (along > -0.42f && along < 0.2f)
                    {
                        float next = isLeft ? _nextAutoPulseLeft : _nextAutoPulseRight;
                        if (Time.time >= next)
                        {
                            swing.PulseTestSwing(0.36f);
                            if (isLeft)
                                _nextAutoPulseLeft = Time.time + autoPulseMinInterval;
                            else
                                _nextAutoPulseRight = Time.time + autoPulseMinInterval;
                        }
                    }
                }
            }

            float seed = isLeft ? _noiseSeedLeft : _noiseSeedRight;
            float posJitScale = calmDesktop ? 0f : 1f;
            float rotJitScale = calmDesktop ? 0f : 1f;
            Vector3 j = JitterOffset(cam, seed);
            targetPos += j * (jitterAmplitude * posJitScale);

            targetRot = targetRot * Quaternion.Euler(
                (Mathf.PerlinNoise(seed, Time.time * jitterFrequency) - 0.5f) * 2f * rotationJitterDegrees * rotJitScale,
                (Mathf.PerlinNoise(Time.time * jitterFrequency, seed) - 0.5f) * 2f * rotationJitterDegrees * rotJitScale,
                (Mathf.PerlinNoise(seed + 2f, Time.time * jitterFrequency * 0.9f) - 0.5f) * 2f * rotationJitterDegrees * rotJitScale);

            if (nonXrDesktop && desktopSnapIfBeyondMeters > 0f &&
                Vector3.Distance(t.position, targetPos) > desktopSnapIfBeyondMeters)
            {
                t.SetPositionAndRotation(targetPos, targetRot);
            }
            else
            {
                t.position = Vector3.Lerp(t.position, targetPos, smooth);
                t.rotation = Quaternion.Slerp(t.rotation, targetRot, smooth);
            }
        }
        else
        {
            if (isLeft)
            {
                Vector3 delta = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) delta += flatF * moveSpeed * dt;
                if (Input.GetKey(KeyCode.S)) delta -= flatF * moveSpeed * dt;
                if (Input.GetKey(KeyCode.A)) delta -= flatR * moveSpeed * dt;
                if (Input.GetKey(KeyCode.D)) delta += flatR * moveSpeed * dt;
                if (Input.GetKey(KeyCode.R)) delta += Vector3.up * moveSpeed * dt;
                if (Input.GetKey(KeyCode.F)) delta -= Vector3.up * moveSpeed * dt;
                float yaw = 0f;
                if (Input.GetKey(KeyCode.Q)) yaw -= yawSpeed * dt;
                if (Input.GetKey(KeyCode.E)) yaw += yawSpeed * dt;
                t.Rotate(Vector3.up, yaw, Space.World);
                t.position += delta;
            }
            else
            {
                Vector3 delta = Vector3.zero;
                if (Input.GetKey(KeyCode.I)) delta += flatF * moveSpeed * dt;
                if (Input.GetKey(KeyCode.K)) delta -= flatF * moveSpeed * dt;
                if (Input.GetKey(KeyCode.J)) delta -= flatR * moveSpeed * dt;
                if (Input.GetKey(KeyCode.L)) delta += flatR * moveSpeed * dt;
                if (Input.GetKey(KeyCode.U)) delta += Vector3.up * moveSpeed * dt;
                if (Input.GetKey(KeyCode.O)) delta -= Vector3.up * moveSpeed * dt;
                float yaw = 0f;
                if (Input.GetKey(KeyCode.Comma)) yaw -= yawSpeed * dt;
                if (Input.GetKey(KeyCode.Period)) yaw += yawSpeed * dt;
                t.Rotate(Vector3.up, yaw, Space.World);
                t.position += delta;
            }
        }
    }

    private bool TryGetAlignmentTarget(bool isLeft, Transform cam, out Vector3 pos, out Quaternion rot)
    {
        if (alignmentProviderBehaviour is ISaberAlignmentTargetProvider p)
            return p.TryGetTarget(isLeft, cam, out pos, out rot);

        return SaberAlignmentQueries.TryGetNearestBlockForHand(isLeft, cam, BeatSaberHitLineGuide.DefaultDistanceInFrontOfCamera, out pos, out rot);
    }

    /// <summary>
    /// Matches <see cref="Slice"/> using <c>transform.up</c> as slice plane normal: arms to the side, blade in a vertical-ish
    /// plane toward the track so slashes read diagonal (top–bottom) instead of stabbing down the lane.
    /// </summary>
    Quaternion ComputeHumanSideHoldRotation(bool isLeft, Transform cam, Vector3 flatF, Vector3 flatR)
    {
        Vector3 sliceNormal = Vector3.Cross(cam.up, flatF);
        if (sliceNormal.sqrMagnitude < 1e-6f)
            sliceNormal = isLeft ? -flatR : flatR;
        else
        {
            sliceNormal.Normalize();
            if (!isLeft)
                sliceNormal = -sliceNormal;
        }

        Vector3 u = Vector3.ProjectOnPlane(Vector3.up, sliceNormal);
        Vector3 f = Vector3.ProjectOnPlane(flatF, sliceNormal);
        if (u.sqrMagnitude < 1e-6f) u = Vector3.up;
        if (f.sqrMagnitude < 1e-6f) f = flatF;
        u.Normalize();
        f.Normalize();

        float a = slashDiagonalDegrees * Mathf.Deg2Rad;
        Vector3 bladeAlong = (u * Mathf.Cos(a) + f * Mathf.Sin(a)).normalized;
        if (bladeAlong.sqrMagnitude < 1e-6f)
            bladeAlong = flatF;

        float inwardBlend = Mathf.Clamp01(desktopBladeTipInwardBlend);
        if (inwardBlend > 0.001f)
        {
            Vector3 towardCenter = isLeft ? flatR : -flatR;
            Vector3 inwardInPlane = Vector3.ProjectOnPlane(towardCenter, sliceNormal);
            if (inwardInPlane.sqrMagnitude > 1e-6f)
            {
                inwardInPlane.Normalize();
                bladeAlong = Vector3.Slerp(bladeAlong, inwardInPlane, inwardBlend).normalized;
            }
        }

        return Quaternion.LookRotation(bladeAlong, sliceNormal);
    }

    private Vector3 JitterOffset(Transform cam, float seed)
    {
        float t = Time.time * jitterFrequency;
        float nx = Mathf.PerlinNoise(seed, t) - 0.5f;
        float ny = Mathf.PerlinNoise(t, seed + 1f) - 0.5f;
        float nz = Mathf.PerlinNoise(seed * 0.7f, t * 0.8f) - 0.5f;
        return cam.right * nx + cam.up * ny + cam.forward * nz * 0.35f;
    }

    public static bool TryResolveSabers(out GameObject left, out GameObject right)
    {
        left = null;
        right = null;
        var sceneHandling = UnityEngine.Object.FindAnyObjectByType<SceneHandling>();
        if (sceneHandling != null)
        {
            left = sceneHandling.LeftSaber;
            right = sceneHandling.RightSaber;
        }
        if (left == null)
        {
            left = GameObject.FindGameObjectWithTag("LeftSaber");
            if (left == null)
                left = SceneHandling.FindWithTagIncludingInactive("LeftSaber");
        }
        if (right == null)
        {
            right = GameObject.FindGameObjectWithTag("RightSaber");
            if (right == null)
                right = SceneHandling.FindWithTagIncludingInactive("RightSaber");
        }
        if (left == null || right == null)
        {
            foreach (var slice in UnityEngine.Object.FindObjectsByType<Slice>(FindObjectsInactive.Include))
            {
                Transform hand = slice.transform.parent;
                if (hand == null) continue;
                if (left == null && hand.CompareTag("LeftSaber"))
                    left = hand.gameObject;
                if (right == null && hand.CompareTag("RightSaber"))
                    right = hand.gameObject;
            }
        }

        if (left == null || right == null)
        {
            var parents = new List<Transform>();
            foreach (var slice in UnityEngine.Object.FindObjectsByType<Slice>(FindObjectsInactive.Include))
            {
                Transform p = slice.transform.parent;
                if (p == null) continue;
                bool dup = false;
                foreach (var q in parents)
                {
                    if (q == p) { dup = true; break; }
                }
                if (!dup) parents.Add(p);
            }

            if (parents.Count >= 2)
            {
                parents.Sort((a, b) => a.position.x.CompareTo(b.position.x));
                if (left == null)
                    left = parents[0].gameObject;
                if (right == null)
                    right = parents[parents.Count - 1].gameObject;
            }
            else if (parents.Count == 1)
            {
                if (left == null)
                    left = parents[0].gameObject;
                else if (right == null)
                    right = parents[0].gameObject;
            }
        }

        return left != null || right != null;
    }
}
