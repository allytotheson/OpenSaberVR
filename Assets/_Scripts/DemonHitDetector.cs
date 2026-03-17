using UnityEngine;

/// <summary>
/// Replaces VR saber collision logic. Destroys demons only during active swings.
/// Uses raycast from saber forward. Requires SwingDetector to be active.
/// </summary>
public class DemonHitDetector : MonoBehaviour
{
    public LayerMask layer;
    [Tooltip("Ray length from saber tip")]
    public float rayLength = 1.2f;

    private SwingDetector swingDetector;
    private Slice slicer;
    private ScoreManager scoreManager;
    private Vector3 previousPos;

    void Start()
    {
        swingDetector = GetComponent<SwingDetector>();
        if (swingDetector == null) swingDetector = GetComponentInParent<SwingDetector>();
        slicer = GetComponentInChildren<Slice>(true);
        scoreManager = FindObjectOfType<ScoreManager>();
        previousPos = transform.position;
    }

    void Update()
    {
        if (swingDetector != null && !swingDetector.IsSwinging)
        {
            previousPos = transform.position;
            return;
        }

        RaycastHit hit;
        Vector3 origin = transform.position;
        Vector3 dir = transform.forward;
        if (Physics.Raycast(origin, dir, out hit, rayLength, layer))
        {
            if (IsDemon(hit.transform))
            {
                bool validCut = CheckCutAngle(hit.transform);
                if (validCut)
                {
                    DestroyDemon(hit.transform);
                }
            }
        }

        previousPos = transform.position;
    }

    private bool IsDemon(Transform t)
    {
        if (t.CompareTag("Demon")) return true;
        if (t.CompareTag("CubeNonDirection")) return true; // backward compat
        var dh = t.GetComponent<DemonHandling>();
        if (dh != null) return true;
        return t.GetComponentInParent<DemonHandling>() != null;
    }

    private bool CheckCutAngle(Transform demon)
    {
        Vector3 swingDir = (transform.position - previousPos).normalized;
        if (swingDir.sqrMagnitude < 0.001f) return true;

        // Accept if swing is roughly perpendicular to cube face (angle > 130 deg)
        Vector3 up = demon.up;
        Vector3 right = demon.right;
        float angleUp = Vector3.Angle(swingDir, up);
        float angleDown = Vector3.Angle(swingDir, -up);
        float angleRight = Vector3.Angle(swingDir, right);
        float angleLeft = Vector3.Angle(swingDir, -right);

        if (demon.CompareTag("CubeNonDirection"))
        {
            return angleUp > 130 || angleDown > 130 || angleRight > 130 || angleLeft > 130;
        }
        return angleUp > 130 || angleDown > 130;
    }

    private void DestroyDemon(Transform demon)
    {
        var root = demon.GetComponentInParent<DemonHandling>() != null
            ? demon.GetComponentInParent<DemonHandling>().transform
            : demon;
        var go = root.gameObject;

        if (slicer != null)
        {
            var cutted = slicer.SliceObject(go);
            var debris = Instantiate(go);
            var dh = debris.GetComponent<DemonHandling>();
            if (dh != null) dh.enabled = false;
            var col = debris.GetComponentInChildren<Collider>();
            if (col != null) col.enabled = false;
            debris.layer = 0;

            foreach (var r in debris.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var cut in cutted)
            {
                cut.transform.SetParent(debris.transform);
                cut.AddComponent<BoxCollider>();
                var rb = cut.AddComponent<Rigidbody>();
                rb.useGravity = true;
            }
            debris.transform.SetPositionAndRotation(root.position, root.rotation);
            Destroy(debris, 2f);
        }

        if (scoreManager != null) scoreManager.AddScore(100);

        Destroy(go);
    }
}
