using UnityEngine;

/// <summary>
/// Desktop / keyboard saber driving: auto-align at the hit plane, optional jitter, Z/X slashes.
/// <see cref="alignSaberRotationToBlockArrow"/> matches block cut direction for developer mode; turn off for a fixed side-hold pose.
/// </summary>
[DefaultExecutionOrder(60)]
public class DesktopSaberTestInput : MonoBehaviour
{
    [Header("Drive source")]
    [Tooltip("If false (default): keyboard + auto-align always drive sabers when this script runs. If true: valid UDP IMU replaces keyboard driving (slash keys still work).")]
    public bool preferUdpImuWhenValid = false;

    [Header("Auto-align")]
    [Tooltip("Blend note world position with camera hand anchors so sabers stay in frame.")]
    [Range(0f, 1f)] public float alignBlendFromNote = 0.4f;

    [Tooltip("When align rotation to block is off: how much the block steers saber yaw/pitch/roll. 0 = fixed human side-hold pose.")]
    [Range(0f, 1f)] public float noteRotationBlend = 0f;

    [Tooltip("Developer mode: match saber orientation to the block (arrow / cut direction). Turn off for the fixed diagonal side-hold pose.")]
    public bool alignSaberRotationToBlockArrow = true;

    [Tooltip("Diagonal in the vertical swing plane: 45 ≈ top-to-bottom slash feel vs track.")]
    [Range(15f, 75f)] public float slashDiagonalDegrees = 45f;

    [Tooltip("Offset from camera (local space): wider X = more 'held out to the side'.")]
    public Vector3 leftHandLocal = new Vector3(-0.54f, -0.26f, 0.92f);
    public Vector3 rightHandLocal = new Vector3(0.54f, -0.26f, 0.92f);

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

    private float _noiseSeedLeft;
    private float _noiseSeedRight;

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

        if (left != null && ShouldUseKeyboard(left, true) && Input.GetKeyDown(KeyCode.Z))
            PulseSwing(left);

        if (right != null && ShouldUseKeyboard(right, false) && Input.GetKeyDown(KeyCode.X))
            PulseSwing(right);
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

        bool autoAlign = GameplayDebugHud.AutoAlignSabersToNotes;

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

    private static void PulseSwing(GameObject saber)
    {
        var swing = saber.GetComponent<SwingDetector>();
        if (swing == null) swing = saber.GetComponentInChildren<SwingDetector>();
        swing?.PulseTestSwing(0.24f);
    }

    private bool ShouldUseKeyboard(GameObject saber, bool isLeft)
    {
        var motion = saber.GetComponent<SaberMotionController>();
        if (!preferUdpImuWhenValid || motion == null || motion.receiver == null)
            return true;
        var p = isLeft ? motion.receiver.LeftSaberData : motion.receiver.RightSaberData;
        return !p.valid;
    }

    private void ApplyHand(GameObject saber, bool isLeft, Transform cam, Vector3 flatF, Vector3 flatR, bool autoAlign)
    {
        var motion = saber.GetComponent<SaberMotionController>();
        var swing = saber.GetComponent<SwingDetector>();
        if (swing == null) swing = saber.GetComponentInChildren<SwingDetector>();

        bool keyboardDrives = ShouldUseKeyboard(saber, isLeft);
        if (motion != null)
            motion.enabled = !keyboardDrives;

        if (!keyboardDrives)
            return;

        Transform t = saber.transform;
        float dt = Time.deltaTime;
        float smooth = 1f - Mathf.Exp(-alignSmoothing * dt);

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
                targetPos = Vector3.Lerp(anchor, aligned, alignBlendFromNote);
                float rotBlend = alignSaberRotationToBlockArrow ? 1f : noteRotationBlend;
                targetRot = Quaternion.Slerp(humanHold, noteRot, rotBlend);
            }

            float seed = isLeft ? _noiseSeedLeft : _noiseSeedRight;
            Vector3 j = JitterOffset(cam, seed);
            targetPos += j * jitterAmplitude;

            targetRot = targetRot * Quaternion.Euler(
                (Mathf.PerlinNoise(seed, Time.time * jitterFrequency) - 0.5f) * 2f * rotationJitterDegrees,
                (Mathf.PerlinNoise(Time.time * jitterFrequency, seed) - 0.5f) * 2f * rotationJitterDegrees,
                (Mathf.PerlinNoise(seed + 2f, Time.time * jitterFrequency * 0.9f) - 0.5f) * 2f * rotationJitterDegrees);

            t.position = Vector3.Lerp(t.position, targetPos, smooth);
            t.rotation = Quaternion.Slerp(t.rotation, targetRot, smooth);
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

    private static bool TryResolveSabers(out GameObject left, out GameObject right)
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
            left = GameObject.FindGameObjectWithTag("LeftSaber");
        if (right == null)
            right = GameObject.FindGameObjectWithTag("RightSaber");
        if (left == null || right == null)
        {
            foreach (var slice in UnityEngine.Object.FindObjectsByType<Slice>(FindObjectsInactive.Include))
            {
                var g = slice.gameObject;
                if (left == null && g.CompareTag("LeftSaber"))
                    left = g;
                if (right == null && g.CompareTag("RightSaber"))
                    right = g;
            }
        }
        return left != null || right != null;
    }
}
