using UnityEngine;

/// <summary>
/// Destroys demons during active swings. Runs in <see cref="LateUpdate"/> so saber moves (keyboard / auto-track)
/// and <see cref="DemonHandling"/> note motion apply first. Overlap/rays also use the hit plane; optional trigger
/// sphere (requires kinematic Rigidbody on blade + note) can register hits on contact while swinging.
/// </summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public class DemonHitDetector : MonoBehaviour
{
    [Tooltip("Must include layers where note colliders live (Default is typical).")]
    public LayerMask layer = Physics.DefaultRaycastLayers;

    [Tooltip("Ray length along each cast axis from the saber.")]
    public float rayLength = 10f;

    [Tooltip("Sphere around saber during a swing — keep modest so we rely on the hit plane, not huge reach.")]
    public float overlapRadius = 1.95f;

    [Tooltip("Extra radius scale during Z/X keyboard pulse (forgiving timing).")]
    public float keyboardPulseOverlapScale = 2.05f;

    [Tooltip("Block must be within ±this many meters of the cyan hit plane (along camera forward) to count as a hit.")]
    public float hitPlaneDepthHalfWindow = 0.22f;

    [Tooltip("Widens the depth window slightly during Z/X pulse (still must intersect the plane band).")]
    public float hitPlaneDepthHalfWindowPulseScale = 1.45f;

    [Tooltip("Leading AABB corner (toward player) signed distance must be ≤ this vs the hit plane. 0 = at plane; small positive = a few cm before.")]
    public float hitLeadingMaxSigned = 0f;

    [Tooltip("During Z/X keyboard pulse, use this leading cap instead (more lenient).")]
    public float hitLeadingMaxSignedDuringPulse = 0.12f;

    [Tooltip("Extra multiplier on depth half-window during keyboard pulse (notes on RedLayer/BlueLayer vs strict plane).")]
    public float keyboardPulseDepthWindowExtraMul = 2.1f;

    [Header("Trigger contact hits")]
    [Tooltip("Sphere trigger on the blade + kinematic Rigidbody; notes need a kinematic Rigidbody (see DemonHandling).")]
    public bool enableTriggerHitPass = true;

    [Tooltip("If true, trigger hits do not require the strict cyan-plane slab (overlap path still does).")]
    public bool triggerHitsSkipPlaneCheck = true;

    [Tooltip("World-space radius for the hit trigger (centered on the Slice).")]
    public float triggerHitRadius = 2.35f;

    public Vector3 triggerHitCenter = Vector3.zero;

    private SwingDetector swingDetector;
    private Slice slicer;
    private ScoreManager scoreManager;
    private Vector3 previousPos;
    private SphereCollider _hitTrigger;

    private static readonly Vector3[] LocalProbeDirs =
    {
        Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left
    };

    void Awake()
    {
        if (layer.value == 0)
            layer = Physics.DefaultRaycastLayers;
        rayLength = Mathf.Max(rayLength, 6f);
        // Notes use dedicated layers (e.g. RedLayer / BlueLayer); OR them in so overlap/raycasts see them.
        int noteLayers = LayerMask.GetMask("RedLayer", "BlueLayer");
        if (noteLayers != 0)
            layer |= noteLayers;

        if (enableTriggerHitPass)
            EnsureBladeRigidbodyAndHitTrigger();
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
        // Auto-slice pulses are short; saber may sit slightly off the strict plane math — widen reach and relax plane below.
        if (pulse && GameplayDebugHud.AutoSliceNotes)
            radius = Mathf.Max(radius, overlapRadius * 4f);

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

    void OnTriggerStay(Collider other)
    {
        if (!enableTriggerHitPass || other == null)
            return;
        if (swingDetector == null || !swingDetector.IsSwinging)
            return;
        if (!IsDemon(other.transform))
            return;
        TryHitFromTriggerContact(other);
    }

    void EnsureBladeRigidbodyAndHitTrigger()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        _hitTrigger = null;
        foreach (var sc in GetComponents<SphereCollider>())
        {
            if (sc != null && sc.isTrigger)
            {
                _hitTrigger = sc;
                break;
            }
        }

        if (_hitTrigger == null)
        {
            _hitTrigger = gameObject.AddComponent<SphereCollider>();
            _hitTrigger.isTrigger = true;
        }

        _hitTrigger.radius = triggerHitRadius;
        _hitTrigger.center = triggerHitCenter;
    }

    void TryHitFromTriggerContact(Collider demonCol)
    {
        bool pulse = swingDetector != null && swingDetector.IsKeyboardPulseSwinging;
        Transform t = demonCol.transform;

        if (triggerHitsSkipPlaneCheck)
        {
            if (!CheckCutAngle(t, pulse))
                return;
            DestroyDemon(t);
            return;
        }

        if (!IsDemonAtHitPlane(t, pulse, out _))
            return;
        if (!CheckCutAngle(t, pulse))
            return;
        DestroyDemon(t);
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
        if ((cols == null || cols.Length == 0) && keyboardPulse && GameplayDebugHud.AutoSliceNotes)
        {
            cols = Physics.OverlapSphere(transform.position, radiusUsed * 1.65f, ~0, QueryTriggerInteraction.Collide);
        }
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
        if (!PassesPlaneForHit(best.transform, keyboardPulse))
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
            if (!PassesPlaneForHit(hit.transform, keyboardPulse))
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

    /// <summary>
    /// Strict plane + lead cap, or a thick-slab fallback when desktop auto-slice is pulsing (avoids MISS at the cyan line).
    /// </summary>
    bool PassesPlaneForHit(Transform demonTransform, bool keyboardPulse)
    {
        if (IsDemonAtHitPlane(demonTransform, keyboardPulse, out _))
            return true;
        return GameplayDebugHud.AutoSliceNotes && keyboardPulse && PassesAutoSliceRelaxedPlane(demonTransform);
    }

    /// <summary>
    /// Ignores motion-aligned leading-edge cap: any overlap of note bounds with a thick slab around the hit plane counts.
    /// </summary>
    static bool PassesAutoSliceRelaxedPlane(Transform demonTransform)
    {
        var dh = demonTransform.GetComponentInParent<DemonHandling>();
        Transform root = dh != null ? dh.transform : demonTransform;
        if (!BeatSaberHitLineGuide.TryGetGameplayHitPlane(out _, out _))
            return false;

        const float thickHalfMeters = 1.75f;
        if (!BeatSaberHitLineGuide.TryGetNoteVisualSignedExtentsAlongPlane(root, out float minS, out float maxS))
        {
            float s = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(DemonSampleWorldPoint(demonTransform));
            return Mathf.Abs(s) <= thickHalfMeters;
        }

        return !(minS > thickHalfMeters || maxS < -thickHalfMeters);
    }

    private bool IsDemonAtHitPlane(Transform demonTransform, bool keyboardPulse, out float absSignedDistanceToPlane)
    {
        absSignedDistanceToPlane = float.MaxValue;
        float pulseScale = keyboardPulse
            ? hitPlaneDepthHalfWindowPulseScale * Mathf.Max(1f, keyboardPulseDepthWindowExtraMul)
            : 1f;
        float halfW = hitPlaneDepthHalfWindow * pulseScale;
        float leadCap = keyboardPulse
            ? Mathf.Max(hitLeadingMaxSignedDuringPulse, 0.28f)
            : hitLeadingMaxSigned;

        var dh = demonTransform.GetComponentInParent<DemonHandling>();
        var root = dh != null ? dh.transform : demonTransform;

        if (!BeatSaberHitLineGuide.TryGetGameplayHitPlane(out _, out Vector3 nRaw))
            return false;
        Vector3 n = nRaw.normalized;

        // Notes move along -root.forward (see DemonHandling). Plane uses n from the lane; min/max corners only
        // mean “leading” vs “trailing” if motion aligns with -n. Otherwise minS/max were backwards and hits break.
        Vector3 motion = (-root.forward).normalized;
        float motionDotN = Vector3.Dot(motion, n);

        if (!BeatSaberHitLineGuide.TryGetNoteVisualSignedExtentsAlongPlane(root, out float minS, out float maxS))
        {
            float s = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(DemonSampleWorldPoint(demonTransform));
            absSignedDistanceToPlane = Mathf.Abs(s);
            return NoteIntersectsHitSlabScalar(s, halfW, leadCap, motionDotN);
        }

        absSignedDistanceToPlane = Mathf.Min(Mathf.Abs(minS), Mathf.Abs(maxS));
        return NoteIntersectsHitSlabBounds(minS, maxS, halfW, leadCap, motionDotN);
    }

    /// <summary>
    /// Signed interval [minS,maxS] along plane normal n; motionDotN = dot(note travel dir, n). Travel is -demon.forward.
    /// </summary>
    static bool NoteIntersectsHitSlabBounds(float minS, float maxS, float halfW, float leadCap, float motionDotN)
    {
        if (minS > halfW || maxS < -halfW)
            return false;

        if (Mathf.Abs(motionDotN) < 0.12f)
            return minS <= halfW && maxS >= -halfW;

        if (motionDotN < 0f)
        {
            float leadS = minS;
            if (leadS > leadCap)
                return false;
            if (leadS < -halfW)
                return false;
            return true;
        }

        float leadP = maxS;
        if (leadP > leadCap)
            return false;
        if (leadP < -halfW)
            return false;
        return true;
    }

    static bool NoteIntersectsHitSlabScalar(float s, float halfW, float leadCap, float motionDotN)
    {
        if (s > halfW || s < -halfW)
            return false;
        if (Mathf.Abs(motionDotN) < 0.12f)
            return true;
        return s <= leadCap && s >= -halfW;
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
        var dh = demon.GetComponentInParent<DemonHandling>();
        if (dh != null && !dh.enabled)
            return;

        var root = dh != null ? dh.transform : demon;
        var go = root.gameObject;

        if (dh != null)
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
                var debris = Instantiate(go);
                var debrisDh = debris.GetComponent<DemonHandling>();
                if (debrisDh != null) debrisDh.enabled = false;
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

        if (DeveloperGameplayMode.Enabled)
            DeveloperHitFeedback.SpawnBurst(root.position, DeveloperHitFeedback.ApproximateDemonTint(root));

        Destroy(go);
    }
}
