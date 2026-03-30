using UnityEngine;

/// <summary>
/// Destroys demons during active swings. Runs in <see cref="LateUpdate"/> so saber moves (keyboard / auto-track)
/// and <see cref="DemonHandling"/> note motion apply first. Hits require overlap/rays <em>and</em> the demon center
/// within a narrow band of the <see cref="BeatSaberHitLineGuide"/> plane (same depth as the cyan frame).
/// </summary>
[DefaultExecutionOrder(300)]
public class DemonHitDetector : MonoBehaviour
{
    [Tooltip("Must include layers where note colliders live (Default is typical).")]
    public LayerMask layer = Physics.DefaultRaycastLayers;

    [Tooltip("Ray length along each cast axis from the saber.")]
    public float rayLength = 10f;

    [Tooltip("Sphere around saber during a swing — keep modest so we rely on the hit plane, not huge reach.")]
    public float overlapRadius = 1.45f;

    [Tooltip("Extra radius scale during Z/X keyboard pulse (forgiving timing).")]
    public float keyboardPulseOverlapScale = 1.35f;

    [Tooltip("Block must be within ±this many meters of the cyan hit plane (along camera forward) to count as a hit.")]
    public float hitPlaneDepthHalfWindow = 0.22f;

    [Tooltip("Widens the depth window slightly during Z/X pulse (still must intersect the plane band).")]
    public float hitPlaneDepthHalfWindowPulseScale = 1.45f;

    private SwingDetector swingDetector;
    private Slice slicer;
    private ScoreManager scoreManager;
    private Vector3 previousPos;

    private static readonly Vector3[] LocalProbeDirs =
    {
        Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left
    };

    void Awake()
    {
        if (layer.value == 0)
            layer = Physics.DefaultRaycastLayers;
        rayLength = Mathf.Max(rayLength, 6f);
    }

    void Start()
    {
        swingDetector = GetComponent<SwingDetector>();
        if (swingDetector == null) swingDetector = GetComponentInParent<SwingDetector>();
        slicer = GetComponentInChildren<Slice>(true);
        scoreManager = FindAnyObjectByType<ScoreManager>();
        previousPos = transform.position;
    }

    void LateUpdate()
    {
        if (swingDetector != null && !swingDetector.IsSwinging)
        {
            previousPos = transform.position;
            return;
        }

        bool pulse = swingDetector != null && swingDetector.IsKeyboardPulseSwinging;
        float radius = pulse ? overlapRadius * keyboardPulseOverlapScale : overlapRadius;

        if (TryHitWithOverlap(pulse, radius))
        {
            previousPos = transform.position;
            return;
        }

        Vector3 origin = transform.position;
        foreach (Vector3 localDir in LocalProbeDirs)
        {
            Vector3 dir = transform.TransformDirection(localDir).normalized;
            if (dir.sqrMagnitude < 0.01f)
                continue;
            if (TryHitWithRay(origin, dir, pulse))
            {
                previousPos = transform.position;
                return;
            }
        }

        previousPos = transform.position;

#if UNITY_EDITOR
        if (swingDetector != null && swingDetector.IsSwinging)
            DrawDebugSwingProbe();
#endif
    }

#if UNITY_EDITOR
    private void DrawDebugSwingProbe()
    {
        DemonHandling nearest = null;
        float best = float.MaxValue;
        foreach (var d in FindObjectsByType<DemonHandling>(FindObjectsInactive.Exclude))
        {
            if (d == null) continue;
            float s = (d.transform.position - transform.position).sqrMagnitude;
            if (s < best)
            {
                best = s;
                nearest = d;
            }
        }
        if (nearest != null)
            Debug.DrawLine(transform.position, nearest.transform.position, Color.yellow, 0f, false);
        Debug.DrawRay(transform.position, transform.forward * Mathf.Min(rayLength, 3f), Color.cyan, 0f, false);
    }
#endif

    private bool TryHitWithOverlap(bool keyboardPulse, float radiusUsed)
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, radiusUsed, layer, QueryTriggerInteraction.Collide);
        Collider best = null;
        float bestDist = float.MaxValue;
        foreach (Collider c in cols)
        {
            if (c == null) continue;
            if (!IsDemon(c.transform)) continue;
            float d = (c.bounds.center - transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        if (best == null)
            return false;
        if (!IsDemonAtHitPlane(best.transform, keyboardPulse, out _))
            return false;
        if (!CheckCutAngle(best.transform, keyboardPulse))
            return false;
        DestroyDemon(best.transform);
        return true;
    }

    private bool TryHitWithRay(Vector3 origin, Vector3 dir, bool keyboardPulse)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, rayLength, layer, QueryTriggerInteraction.Collide))
        {
            if (!IsDemon(hit.transform))
                return false;
            if (!IsDemonAtHitPlane(hit.transform, keyboardPulse, out _))
                return false;
            if (!CheckCutAngle(hit.transform, keyboardPulse))
                return false;
            DestroyDemon(hit.transform);
            return true;
        }
        return false;
    }

    private bool IsDemon(Transform t)
    {
        if (t.CompareTag("Demon")) return true;
        if (t.CompareTag("CubeNonDirection")) return true;
        var dh = t.GetComponent<DemonHandling>();
        if (dh != null) return true;
        return t.GetComponentInParent<DemonHandling>() != null;
    }

    private static Vector3 DemonSampleWorldPoint(Transform demonTransform)
    {
        var dh = demonTransform.GetComponentInParent<DemonHandling>();
        var root = dh != null ? dh.transform : demonTransform;
        var col = root.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;
        return root.position;
    }

    private bool IsDemonAtHitPlane(Transform demonTransform, bool keyboardPulse, out float absSignedDistanceToPlane)
    {
        absSignedDistanceToPlane = float.MaxValue;
        float halfW = hitPlaneDepthHalfWindow * (keyboardPulse ? hitPlaneDepthHalfWindowPulseScale : 1f);
        float signed = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(DemonSampleWorldPoint(demonTransform));
        absSignedDistanceToPlane = Mathf.Abs(signed);
        return absSignedDistanceToPlane <= halfW;
    }

    private bool CheckCutAngle(Transform demon, bool keyboardPulse)
    {
        if (keyboardPulse)
            return true;

        Vector3 swingDelta = transform.position - previousPos;
        if (swingDelta.sqrMagnitude < 1e-8f)
            return true;

        Vector3 swingDir = swingDelta.normalized;
        // Ignore almost-pure travel-axis motion (notes move along demon.forward)
        float alongTravel = Mathf.Abs(Vector3.Dot(swingDir, demon.forward));
        if (alongTravel > 0.96f)
            return false;

        // Old rule used angle > 130° vs up/down which rejected ~90° horizontal cuts — use a perpendicular band instead
        float au = Vector3.Angle(swingDir, demon.up);
        if (au > 25f && au < 155f)
            return true;
        float ar = Vector3.Angle(swingDir, demon.right);
        return ar > 25f && ar < 155f;
    }

    private void DestroyDemon(Transform demon)
    {
        var root = demon.GetComponentInParent<DemonHandling>() != null
            ? demon.GetComponentInParent<DemonHandling>().transform
            : demon;
        var go = root.gameObject;

        if (slicer != null)
        {
            GameObject[] cutted = slicer.SliceObject(go);
            if (cutted != null && cutted.Length > 0)
            {
                var debris = Instantiate(go);
                var dh = debris.GetComponent<DemonHandling>();
                if (dh != null) dh.enabled = false;
                var col = debris.GetComponentInChildren<Collider>();
                if (col != null) col.enabled = false;
                debris.layer = 0;

                foreach (var r in debris.GetComponentsInChildren<Renderer>()) r.enabled = false;
                foreach (var cut in cutted)
                {
                    if (cut == null) continue;
                    cut.transform.SetParent(debris.transform);
                    cut.AddComponent<BoxCollider>();
                    var rb = cut.AddComponent<Rigidbody>();
                    rb.useGravity = true;
                }
                debris.transform.SetPositionAndRotation(root.position, root.rotation);
                Destroy(debris, 2f);
            }
        }

        if (scoreManager != null)
            scoreManager.RegisterHit();
        HitMissFlyout.ShowHit();

        Destroy(go);
    }
}
