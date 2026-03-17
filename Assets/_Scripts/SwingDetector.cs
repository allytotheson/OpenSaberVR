using UnityEngine;

/// <summary>
/// Detects saber swings using velocity threshold.
/// Saber must exceed minSwingVelocity to count as "swinging".
/// Used by DemonHitDetector to only destroy demons during active swings.
/// </summary>
public class SwingDetector : MonoBehaviour
{
    [Header("Velocity Threshold")]
    [Tooltip("Minimum angular velocity (rad/s) to count as a swing")]
    public float minSwingVelocity = 3f;
    [Tooltip("Cooldown after swing ends (seconds) before next swing can be detected")]
    public float swingCooldown = 0.15f;

    [Header("State")]
    [SerializeField] private bool isSwinging;
    [SerializeField] private float lastSwingEndTime;

    private SaberMotionController motionController;
    private Vector3 lastPosition;
    private Vector3 lastForward;

    public bool IsSwinging => isSwinging;

    void Start()
    {
        motionController = GetComponent<SaberMotionController>();
        if (motionController == null) motionController = GetComponentInParent<SaberMotionController>();
        lastPosition = transform.position;
        lastForward = transform.forward;
    }

    void Update()
    {
        float angularVel = motionController != null ? motionController.GetAngularVelocityMagnitude() : 0f;
        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward;

        // Velocity from position delta (fallback if no motion controller)
        float linearVel = 0f;
        if (motionController == null)
        {
            linearVel = Vector3.Distance(pos, lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        }
        else
        {
            linearVel = motionController.GetTipVelocity().magnitude;
        }

        bool aboveThreshold = angularVel >= minSwingVelocity || linearVel >= minSwingVelocity * 2f;
        bool inCooldown = Time.time - lastSwingEndTime < swingCooldown;

        if (aboveThreshold && !inCooldown)
        {
            isSwinging = true;
        }
        else if (isSwinging && !aboveThreshold)
        {
            isSwinging = false;
            lastSwingEndTime = Time.time;
        }

        lastPosition = pos;
        lastForward = fwd;
    }
}
