using UnityEngine;

/// <summary>
/// Cyan hit frame + shared hit-plane math. By default the plane is anchored to lane geometry (<c>MiddleStripes</c>)
/// so the box sits at the mouth of the vertical rails (Beat Saber–style). Falls back to camera-distance mode if the anchor is missing.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BeatSaberHitLineGuide : MonoBehaviour
{
    [Header("Lane-anchored plane (preferred)")]
    [Tooltip("If true, hit plane uses lane root + offsets so the box lines up with the end of the vertical track lines.")]
    public bool useLaneAnchoredHitPlane = true;

    [Tooltip("Scene object that sits at the lane / vertical stripe root (OpenSaber: MiddleStripes).")]
    public string laneAnchorObjectName = "MiddleStripes";

    [Tooltip("Shift plane center from the anchor pivot toward the play space (local space of the anchor). OpenSaber: pulls X toward spawner at origin.")]
    public Vector3 planeCenterOffsetFromAnchor = new Vector3(-15.05f, 0.66f, 0f);

    [Tooltip("Slide the whole plane along the track direction (anchor.forward). Negative moves the plane toward the player / platform (lines should meet the box here).")]
    public float planeAdvanceAlongLane = -1.15f;

    [Tooltip("After anchor math, re-base the plane on Spawner X/Y so the cyan frame is centered on the play space (matches the converging lane lines).")]
    public bool synchronizePlaneWithSpawner = true;

    [Tooltip("Vertical nudge of plane center vs spawner pivot (meters).")]
    public float planeHeightOffsetFromSpawner = -0.22f;

    [Header("Camera-distance fallback (no anchor)")]
    [Tooltip("Meters along camera forward when lane anchor is unused or missing.")]
    public float distanceInFrontOfCamera = 0.72f;

    public const float DefaultDistanceInFrontOfCamera = 0.72f;

    [Header("Frame size (meters on the plane)")]
    public float halfWidth = 3.6f;

    [Tooltip("Vertical extent below frame center (meters).")]
    public float heightBottom = 1.15f;

    [Tooltip("Vertical extent above frame center (meters).")]
    public float heightTop = 1.75f;

    public Color lineColor = new Color(0.2f, 0.95f, 1f, 0.62f);

    private LineRenderer lr;

    /// <summary>Plane point (on surface) and outward unit normal (toward incoming notes / down track).</summary>
    public static bool TryGetGameplayHitPlane(out Vector3 planePoint, out Vector3 planeNormal)
    {
        planePoint = Vector3.zero;
        planeNormal = Vector3.forward;

        var guide = UnityEngine.Object.FindAnyObjectByType<BeatSaberHitLineGuide>();
        if (guide != null && guide.useLaneAnchoredHitPlane && !string.IsNullOrEmpty(guide.laneAnchorObjectName))
        {
            var anchor = GameObject.Find(guide.laneAnchorObjectName);
            if (anchor != null)
            {
                Transform t = anchor.transform;
                planeNormal = t.forward.sqrMagnitude > 0.01f ? t.forward.normalized : Vector3.forward;
                planePoint = t.position + t.TransformDirection(guide.planeCenterOffsetFromAnchor) + planeNormal * guide.planeAdvanceAlongLane;

                if (guide.synchronizePlaneWithSpawner)
                {
                    var spT = GameObject.Find("Spawner")?.transform;
                    if (spT != null)
                    {
                        float along = Vector3.Dot(planePoint - spT.position, planeNormal);
                        Vector3 onTrack = spT.position + planeNormal * along + Vector3.up * guide.planeHeightOffsetFromSpawner;
                        var plat = GameObject.Find("PlayersPlatform");
                        var pr = plat != null ? plat.GetComponentInChildren<Renderer>() : null;
                        if (pr != null)
                            onTrack.x = pr.bounds.center.x;
                        planePoint = onTrack;
                    }
                }

                return true;
            }
        }

        if (DesktopGameplayCamera.TryGet(out Camera cam))
        {
            float d = guide != null ? guide.distanceInFrontOfCamera : DefaultDistanceInFrontOfCamera;
            planeNormal = cam.transform.forward;
            planePoint = cam.transform.position + planeNormal * d;
            return true;
        }

        return false;
    }

    public static float SignedDistanceToGameplayHitPlane(Vector3 worldPoint)
    {
        if (!TryGetGameplayHitPlane(out Vector3 pp, out Vector3 n))
            return float.MaxValue;
        return Vector3.Dot(worldPoint - pp, n);
    }

    public static Vector3 ProjectOntoGameplayHitPlane(Vector3 worldPoint)
    {
        if (!TryGetGameplayHitPlane(out Vector3 pp, out Vector3 n))
            return worldPoint;
        float s = Vector3.Dot(worldPoint - pp, n);
        return worldPoint - n * s;
    }

    /// <summary>Legacy: use <see cref="SignedDistanceToGameplayHitPlane"/> when possible.</summary>
    public static float GetConfiguredDistanceInFront()
    {
        var g = UnityEngine.Object.FindAnyObjectByType<BeatSaberHitLineGuide>();
        return g != null ? g.distanceInFrontOfCamera : DefaultDistanceInFrontOfCamera;
    }

    public static float SignedDistanceToHitPlane(Vector3 worldPoint, Transform cameraTransform, float distanceInFrontOfCamera)
    {
        if (TryGetGameplayHitPlane(out _, out _))
            return SignedDistanceToGameplayHitPlane(worldPoint);

        Vector3 fwd = cameraTransform.forward;
        Vector3 planePoint = cameraTransform.position + fwd * distanceInFrontOfCamera;
        return Vector3.Dot(worldPoint - planePoint, fwd);
    }

    public static Vector3 ProjectOntoHitPlane(Vector3 worldPoint, Transform cameraTransform, float distanceInFrontOfCamera)
    {
        if (TryGetGameplayHitPlane(out _, out _))
            return ProjectOntoGameplayHitPlane(worldPoint);

        float signed = SignedDistanceToHitPlane(worldPoint, cameraTransform, distanceInFrontOfCamera);
        return worldPoint - cameraTransform.forward * signed;
    }

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.loop = true;
        lr.positionCount = 5;
        lr.startWidth = 0.048f;
        lr.endWidth = 0.048f;
        lr.useWorldSpace = true;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh != null)
            lr.material = new Material(sh);
        lr.startColor = lineColor;
        lr.endColor = lineColor;
    }

    void LateUpdate()
    {
        if (!TryGetGameplayHitPlane(out Vector3 planePoint, out Vector3 planeNormal))
            return;

        Vector3 n = planeNormal.normalized;

        Vector3 u = Vector3.ProjectOnPlane(Vector3.right, n);
        if (u.sqrMagnitude < 1e-4f)
            u = Vector3.ProjectOnPlane(Vector3.forward, n);
        u.Normalize();

        if (DesktopGameplayCamera.TryGet(out Camera cam))
        {
            Vector3 camR = Vector3.ProjectOnPlane(cam.transform.right, n);
            if (camR.sqrMagnitude > 0.01f)
            {
                u = camR.normalized;
            }
        }

        Vector3 v = Vector3.Cross(n, u).normalized;

        Vector3 bl = planePoint - u * halfWidth - v * heightBottom;
        Vector3 br = planePoint + u * halfWidth - v * heightBottom;
        Vector3 tr = planePoint + u * halfWidth + v * heightTop;
        Vector3 tl = planePoint - u * halfWidth + v * heightTop;

        lr.SetPosition(0, bl);
        lr.SetPosition(1, br);
        lr.SetPosition(2, tr);
        lr.SetPosition(3, tl);
        lr.SetPosition(4, bl);
    }
}
