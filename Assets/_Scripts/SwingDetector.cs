using System;
using UnityEngine;

/// <summary>
/// Detects saber swings using velocity threshold.
/// Saber must exceed minSwingVelocity to count as "swinging".
/// Used by DemonHitDetector to only destroy demons during active swings.
/// </summary>
[DefaultExecutionOrder(-20)]
[DisallowMultipleComponent]
public class SwingDetector : MonoBehaviour
{
    [Header("Velocity Threshold")]
    [Tooltip("Minimum angular velocity (rad/s) to count as a swing")]
    public float minSwingVelocity = 3f;
    [Tooltip("Cooldown after swing ends (seconds) before next swing can be detected")]
    public float swingCooldown = 0.15f;

    [Header("Stuck-swing safety")]
    [Tooltip("Max seconds isSwinging can stay true before being forcibly reset. Prevents a stuck-true state from blocking all future swing events.")]
    public float maxSwingDuration = 1.5f;

    [Header("State")]
    [SerializeField] private bool isSwinging;
    [SerializeField] private float lastSwingEndTime;

    private float keyboardTestSwingTimer;
    private float _swingStartTime;

    // Grace period after Start() to ignore transient velocity spikes from
    // initialization (position jumps, uncalibrated IMU data, etc.).
    private float _warmupUntil;
    private const float WarmupSeconds = 0.5f;

    private SaberMotionController motionController;
    private Vector3 lastPosition;
    private Vector3 lastForward;

    private bool _prevMotionSwinging;
    private bool _cachedIsLeft;

    // Diagnostic logging
    private float _nextDiagLogTime;
    private const float DiagLogInterval = 1f;

    // Exposed for debug HUD
    [NonSerialized] public float debugAngularVel;
    [NonSerialized] public float debugLinearVel;
    [NonSerialized] public string debugProviderName = "";

    /// <summary>Fired when velocity-based <see cref="isSwinging"/> becomes true (not keyboard pulse). Parameter: left hand.</summary>
    public static event Action<bool> SwingStarted;

    /// <summary>True during motion swing or a short desktop test pulse (see <see cref="PulseTestSwing"/>).</summary>
    public bool IsSwinging => keyboardTestSwingTimer > 0f || isSwinging;

    /// <summary>Desktop Z/X test swing — used to relax cut-angle checks.</summary>
    public bool IsKeyboardPulseSwinging => keyboardTestSwingTimer > 0f;

    /// <summary>Desktop / keyboard: treat as an active swing for <paramref name="seconds"/> (for <see cref="DemonHitDetector"/>).</summary>
    public void PulseTestSwing(float seconds = 0.22f)
    {
        keyboardTestSwingTimer = Mathf.Max(keyboardTestSwingTimer, seconds);
    }

    void Start()
    {
        motionController = GetComponent<SaberMotionController>();
        if (motionController == null) motionController = GetComponentInParent<SaberMotionController>();
        lastPosition = transform.position;
        lastForward = transform.forward;
        _warmupUntil = Time.time + WarmupSeconds;
        _cachedIsLeft = ResolveIsLeftHand();

        string mcStatus = motionController != null
            ? $"found on '{motionController.gameObject.name}'"
            : "NULL (will use position-delta fallback)";
        Debug.Log($"[SwingDetector] Start() on '{gameObject.name}' hand={(_cachedIsLeft ? "Left" : "Right")} " +
                  $"motionController={mcStatus} minSwingVelocity={minSwingVelocity:F3}");
    }

    bool ResolveIsLeftHand()
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            if (t.CompareTag("LeftSaber"))
                return true;
            if (t.CompareTag("RightSaber"))
                return false;
        }
        return transform.position.x <= 0f;
    }

    void Update()
    {
        if (keyboardTestSwingTimer > 0f)
            keyboardTestSwingTimer -= Time.deltaTime;

        // During warmup, keep tracking position but don't detect motion-based swings.
        if (Time.time < _warmupUntil)
        {
            lastPosition = transform.position;
            lastForward = transform.forward;
            return;
        }

        float angularVel = motionController != null ? motionController.GetAngularVelocityMagnitude() : 0f;
        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward;

        float linearVel = 0f;
        if (motionController == null)
        {
            linearVel = Vector3.Distance(pos, lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        }
        else
        {
            linearVel = motionController.GetTipVelocity().magnitude;
        }

        // Expose for debug HUD
        debugAngularVel = angularVel;
        debugLinearVel = linearVel;
        debugProviderName = motionController != null && motionController.ActiveProvider != null
            ? motionController.ActiveProvider.GetType().Name
            : (motionController != null ? "built-in fallback" : "NO motionController");

        bool aboveThreshold = angularVel >= minSwingVelocity || linearVel >= minSwingVelocity * 2f;
        bool inCooldown = Time.time - lastSwingEndTime < swingCooldown;

        // Stuck-swing safety: force reset if isSwinging has been true too long
        if (isSwinging && maxSwingDuration > 0f && Time.time - _swingStartTime > maxSwingDuration)
        {
            Debug.LogWarning($"[SwingDetector] STUCK SWING RESET on '{gameObject.name}' — isSwinging was true for " +
                             $"{Time.time - _swingStartTime:F2}s (max {maxSwingDuration:F1}s). angVel={angularVel:F3} linVel={linearVel:F3}");
            isSwinging = false;
            lastSwingEndTime = Time.time;
        }

        if (aboveThreshold && !inCooldown)
        {
            if (!isSwinging)
                _swingStartTime = Time.time;
            isSwinging = true;
        }
        else if (isSwinging && !aboveThreshold)
        {
            isSwinging = false;
            lastSwingEndTime = Time.time;
        }

        if (isSwinging && !_prevMotionSwinging)
        {
            Debug.Log($"[SwingDetector] >>> SWING STARTED on '{gameObject.name}' hand={(_cachedIsLeft ? "Left" : "Right")} " +
                      $"angVel={angularVel:F3} linVel={linearVel:F3} threshold={minSwingVelocity:F3} provider={debugProviderName}");
            HitMissFlyout.ShowSwing(_cachedIsLeft);
            SwingStarted?.Invoke(_cachedIsLeft);
            var directed = UnityEngine.Object.FindAnyObjectByType<DirectedDesktopSliceInput>();
            if (directed != null)
                directed.TrySliceFromMotionSwing(_cachedIsLeft);
        }
        else if (!isSwinging && _prevMotionSwinging)
        {
            Debug.Log($"[SwingDetector] <<< SWING ENDED on '{gameObject.name}' hand={(_cachedIsLeft ? "Left" : "Right")} " +
                      $"angVel={angularVel:F3} linVel={linearVel:F3}");
        }

        // Periodic diagnostic log (~every 1s)
        if (Time.time >= _nextDiagLogTime)
        {
            _nextDiagLogTime = Time.time + DiagLogInterval;
            Debug.Log($"[SwingDetector][DIAG] '{gameObject.name}' hand={(_cachedIsLeft ? "L" : "R")} | " +
                      $"angVel={angularVel:F3} linVel={linearVel:F3} threshold={minSwingVelocity:F3} | " +
                      $"isSwinging={isSwinging} prevSwinging={_prevMotionSwinging} cooldown={inCooldown} above={aboveThreshold} | " +
                      $"provider={debugProviderName}");
        }

        _prevMotionSwinging = isSwinging;

        lastPosition = pos;
        lastForward = fwd;
    }
}
