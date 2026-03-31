using UnityEngine;

/// <summary>
/// Cyan hit frame + shared hit-plane math. The rectangle is drawn <em>on</em> the gameplay slice plane: you should
/// cut blocks as they pass through that plane (same math as <see cref="DemonHitDetector"/> and <see cref="NoteMissDetector"/>).
/// Anchored to lane geometry (<c>MiddleStripes</c>) by default; falls back to camera-distance mode if the anchor is missing.
/// </summary>
[DefaultExecutionOrder(55)]
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

    [Tooltip("Slide the whole plane along the track direction (anchor.forward). Negative moves the plane toward the player / platform. Changing this moves both the cyan guide and hit/miss math together (spawn timing unchanged). If miss feels early vs the frame, try less negative values (e.g. -1.15 → -0.95) so the plane sits slightly farther up-track.")]
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

    public Color lineColor = new Color(0.2f, 0.95f, 1f, 0.82f);

    [Header("Slice readability (same plane as hits/misses — does not change timing)")]
    [Tooltip("Extra-bright horizontal line along the bottom of the frame (bl–br). Easiest depth cue for “crossed the slice” in perspective.")]
    public bool drawEmphasizedCutFloor = true;

    [Tooltip("Width of the emphasized floor line (meters).")]
    public float cutFloorLineWidth = 0.14f;

    [Tooltip("High-contrast color for the floor cut line.")]
    public Color cutFloorLineColor = new Color(0.4f, 1f, 1f, 0.95f);

    [Tooltip("Slightly dimmer top edge (tl–tr) to bracket the slice height.")]
    public bool drawEmphasizedCutRoof = true;

    public float cutRoofLineWidth = 0.065f;

    public Color cutRoofLineColor = new Color(0.22f, 0.75f, 1f, 0.48f);

    [Header("Plane fill (slice surface)")]
    [Tooltip("Semi-transparent quad on the same plane as the frame. Off by default (wire + cut lines only).")]
    public bool drawPlaneFill = false;

    public Color planeFillColor = new Color(0.12f, 0.75f, 1f, 0.16f);

    [Range(0f, 2f)]
    [Tooltip("Multiply alpha on the bottom edge (bl–br) of the fill.")]
    public float planeFillBottomAlphaScale = 0.55f;

    [Range(0f, 2f)]
    [Tooltip("Multiply alpha on the top edge (tl–tr).")]
    public float planeFillTopAlphaScale = 1f;

    private LineRenderer lr;
    private LineRenderer _cutFloorLr;
    private LineRenderer _cutRoofLr;
    private MeshFilter _fillMf;
    private MeshRenderer _fillMr;
    private Mesh _fillMesh;
    private Material _fillMat;
    private readonly Vector3[] _fillVerts = new Vector3[4];
    private readonly Color[] _fillCols = new Color[4];
    private readonly Vector3[] _fillNorms = new Vector3[4];
    private static readonly int[] FillTrisFwd = { 0, 1, 2, 0, 2, 3 };
    private static readonly int[] FillTrisRev = { 0, 2, 1, 0, 3, 2 };

    /// <summary>
    /// Hit plane from lane geometry only (MiddleStripes, etc.). Does not use the camera.
    /// Use this when moving the gameplay camera from the hit plane would otherwise create a feedback loop.
    /// </summary>
    public static bool TryGetLaneAnchoredGameplayHitPlane(out Vector3 planePoint, out Vector3 planeNormal)
    {
        planePoint = Vector3.zero;
        planeNormal = Vector3.forward;

        var guide = UnityEngine.Object.FindAnyObjectByType<BeatSaberHitLineGuide>();
        if (guide == null || !guide.useLaneAnchoredHitPlane || string.IsNullOrEmpty(guide.laneAnchorObjectName))
            return false;

        var anchor = GameObject.Find(guide.laneAnchorObjectName);
        if (anchor == null)
            return false;

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

    /// <summary>Plane point (on surface) and outward unit normal (toward incoming notes / down track).</summary>
    public static bool TryGetGameplayHitPlane(out Vector3 planePoint, out Vector3 planeNormal)
    {
        if (TryGetLaneAnchoredGameplayHitPlane(out planePoint, out planeNormal))
            return true;

        var guide = UnityEngine.Object.FindAnyObjectByType<BeatSaberHitLineGuide>();
        if (DesktopGameplayCamera.TryGet(out Camera cam))
        {
            float d = guide != null ? guide.distanceInFrontOfCamera : DefaultDistanceInFrontOfCamera;
            planeNormal = cam.transform.forward;
            planePoint = cam.transform.position + planeNormal * d;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Signed distance along <see cref="TryGetGameplayHitPlane"/>'s normal (which points toward incoming notes).
    /// Positive = point is still up-track / before the hit line; negative = past the line toward the player.
    /// </summary>
    public static float SignedDistanceToGameplayHitPlane(Vector3 worldPoint)
    {
        if (!TryGetGameplayHitPlane(out Vector3 pp, out Vector3 n))
            return float.MaxValue;
        return Vector3.Dot(worldPoint - pp, n);
    }

    /// <summary>
    /// Min / max of dot(corner - planePoint, planeNormal) over a world-space AABB's corners.
    /// Which corner is "leading" vs "trail" along travel depends on <c>dot(noteVelocity, n)</c> — use <see cref="GetLeadTrailSignedFromExtents"/>.
    /// </summary>
    public static bool TryGetWorldBoundsSignedMinMaxAlongPlane(Bounds worldBounds, out float minSigned, out float maxSigned)
    {
        minSigned = float.MaxValue;
        maxSigned = float.MinValue;
        if (!TryGetGameplayHitPlane(out Vector3 pp, out Vector3 n))
            return false;
        n = n.normalized;
        Vector3 mn = worldBounds.min, mx = worldBounds.max;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = new Vector3(
                (i & 1) != 0 ? mx.x : mn.x,
                (i & 2) != 0 ? mx.y : mn.y,
                (i & 4) != 0 ? mx.z : mn.z);
            float s = Vector3.Dot(c - pp, n);
            if (s < minSigned) minSigned = s;
            if (s > maxSigned) maxSigned = s;
        }

        return true;
    }

    /// <summary>
    /// Min / max signed distance over collider world AABB corners.
    /// </summary>
    public static bool TryGetColliderSignedMinMaxAlongPlane(Collider col, out float minSigned, out float maxSigned)
    {
        minSigned = float.MaxValue;
        maxSigned = float.MinValue;
        if (col == null)
            return false;
        return TryGetWorldBoundsSignedMinMaxAlongPlane(col.bounds, out minSigned, out maxSigned);
    }

    /// <summary>
    /// Prefer mesh <see cref="Renderer.bounds"/> (what you see); falls back to collider. Tighter than loose AABB when rotation differs from world axes.
    /// </summary>
    public static bool TryGetNoteVisualSignedExtentsAlongPlane(Transform demonRoot, out float minSigned, out float maxSigned)
    {
        minSigned = float.MaxValue;
        maxSigned = float.MinValue;
        var r = demonRoot.GetComponentInChildren<Renderer>();
        if (r != null)
            return TryGetWorldBoundsSignedMinMaxAlongPlane(r.bounds, out minSigned, out maxSigned);
        var col = demonRoot.GetComponentInChildren<Collider>();
        if (col != null)
            return TryGetColliderSignedMinMaxAlongPlane(col, out minSigned, out maxSigned);
        if (!TryGetGameplayHitPlane(out Vector3 pp, out Vector3 n))
            return false;
        n = n.normalized;
        float s = Vector3.Dot(demonRoot.position - pp, n);
        minSigned = maxSigned = s;
        return true;
    }

    /// <summary>
    /// <paramref name="motionDotN"/> = <c>dot(normalized note velocity, planeNormal)</c>; note velocity is <c>-DemonHandling.transform.forward</c>.
    /// </summary>
    public static void GetLeadTrailSignedFromExtents(float minS, float maxS, float motionDotN, out float leadSigned, out float trailSigned)
    {
        if (Mathf.Abs(motionDotN) < 0.12f)
        {
            leadSigned = minS;
            trailSigned = maxS;
            return;
        }

        if (motionDotN < 0f)
        {
            leadSigned = minS;
            trailSigned = maxS;
        }
        else
        {
            leadSigned = maxS;
            trailSigned = minS;
        }
    }

    /// <summary>Cyan frame corners in world space (same rectangle as the <see cref="LineRenderer"/>).</summary>
    public static bool TryGetHitFrameCorners(out Vector3 bl, out Vector3 br, out Vector3 tr, out Vector3 tl)
    {
        bl = br = tr = tl = default;
        var guide = UnityEngine.Object.FindAnyObjectByType<BeatSaberHitLineGuide>();
        if (guide == null || !TryGetGameplayHitPlane(out Vector3 planePoint, out Vector3 planeNormal))
            return false;

        Vector3 n = planeNormal.normalized;
        Vector3 u = Vector3.ProjectOnPlane(Vector3.right, n);
        if (u.sqrMagnitude < 1e-4f)
            u = Vector3.ProjectOnPlane(Vector3.forward, n);
        u.Normalize();

        if (DesktopGameplayCamera.TryGet(out Camera cam))
        {
            Vector3 camR = Vector3.ProjectOnPlane(cam.transform.right, n);
            if (camR.sqrMagnitude > 0.01f)
                u = camR.normalized;
        }

        Vector3 v = Vector3.Cross(n, u).normalized;
        float hw = guide.halfWidth;
        bl = planePoint - u * hw - v * guide.heightBottom;
        br = planePoint + u * hw - v * guide.heightBottom;
        tr = planePoint + u * hw + v * guide.heightTop;
        tl = planePoint - u * hw + v * guide.heightTop;
        return true;
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
        lr.startWidth = 0.055f;
        lr.endWidth = 0.055f;
        lr.useWorldSpace = true;
        Shader sh = RenderingShaderUtil.UnlitForWorldMeshes();
        if (sh != null)
            lr.material = new Material(sh);
        lr.startColor = lineColor;
        lr.endColor = lineColor;

        if (drawEmphasizedCutFloor)
            _cutFloorLr = CreateChildLineRenderer(transform, "CutFloorThreshold", cutFloorLineWidth, cutFloorLineColor, sh);
        if (drawEmphasizedCutRoof)
            _cutRoofLr = CreateChildLineRenderer(transform, "CutRoofBracket", cutRoofLineWidth, cutRoofLineColor, sh);

        if (drawPlaneFill)
            EnsurePlaneFill(sh);
    }

    static LineRenderer CreateChildLineRenderer(Transform parent, string childName, float width, Color color, Shader sh)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        var lrChild = go.AddComponent<LineRenderer>();
        lrChild.loop = false;
        lrChild.positionCount = 2;
        lrChild.useWorldSpace = true;
        lrChild.numCapVertices = 4;
        lrChild.startWidth = width;
        lrChild.endWidth = width;
        lrChild.startColor = color;
        lrChild.endColor = color;
        if (sh != null)
            lrChild.material = new Material(sh);
        return lrChild;
    }

    void EnsurePlaneFill(Shader lineShader)
    {
        var go = new GameObject("HitPlaneFill");
        go.transform.SetParent(transform, false);
        _fillMf = go.AddComponent<MeshFilter>();
        _fillMr = go.AddComponent<MeshRenderer>();
        _fillMesh = new Mesh { name = "HitPlaneFill" };
        _fillMesh.MarkDynamic();
        _fillMf.sharedMesh = _fillMesh;

        Shader sh = lineShader != null ? lineShader : RenderingShaderUtil.UnlitForWorldMeshes();
        _fillMat = new Material(sh);
        if (_fillMat.HasProperty("_Color"))
            _fillMat.SetColor("_Color", planeFillColor);
        if (_fillMat.HasProperty("_BaseColor"))
            _fillMat.SetColor("_BaseColor", planeFillColor);
        if (_fillMat.HasProperty("_Surface"))
            _fillMat.SetFloat("_Surface", 1f);
        if (_fillMat.HasProperty("_Blend"))
            _fillMat.SetFloat("_Blend", 0f);
        if (_fillMat.HasProperty("_Cull"))
            _fillMat.SetFloat("_Cull", 0f);
        _fillMat.renderQueue = 3000;
        _fillMr.sharedMaterial = _fillMat;
        _fillMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _fillMr.receiveShadows = false;
    }

    void UpdatePlaneFill(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl)
    {
        Vector3 center = (bl + br + tr + tl) * 0.25f;
        Vector3 camOff = Vector3.zero;
        if (DesktopGameplayCamera.TryGet(out Camera cam))
        {
            Vector3 toCam = cam.transform.position - center;
            if (toCam.sqrMagnitude > 1e-6f)
                camOff = toCam.normalized * 0.007f;
        }

        _fillVerts[0] = bl + camOff;
        _fillVerts[1] = br + camOff;
        _fillVerts[2] = tr + camOff;
        _fillVerts[3] = tl + camOff;

        Color cb = planeFillColor;
        cb.a *= planeFillBottomAlphaScale;
        Color ct = planeFillColor;
        ct.a *= planeFillTopAlphaScale;
        _fillCols[0] = cb;
        _fillCols[1] = cb;
        _fillCols[2] = ct;
        _fillCols[3] = ct;

        Vector3 nFlat = Vector3.Cross(br - bl, tr - bl).normalized;
        bool rev = false;
        if (DesktopGameplayCamera.TryGet(out Camera cam2))
            rev = Vector3.Dot(nFlat, cam2.transform.position - bl) < 0f;

        Vector3 n = rev ? -nFlat : nFlat;
        _fillNorms[0] = _fillNorms[1] = _fillNorms[2] = _fillNorms[3] = n;

        _fillMesh.SetVertices(_fillVerts);
        _fillMesh.SetColors(_fillCols);
        _fillMesh.SetNormals(_fillNorms);
        _fillMesh.SetTriangles(rev ? FillTrisRev : FillTrisFwd, 0);
        _fillMesh.RecalculateBounds();

        if (_fillMat != null)
        {
            if (_fillMat.HasProperty("_Color"))
                _fillMat.SetColor("_Color", Color.white);
            if (_fillMat.HasProperty("_BaseColor"))
                _fillMat.SetColor("_BaseColor", Color.white);
        }
    }

    void LateUpdate()
    {
        if (!TryGetHitFrameCorners(out Vector3 bl, out Vector3 br, out Vector3 tr, out Vector3 tl))
        {
            lr.enabled = false;
            SetCutLinesActive(false);
            if (_fillMr != null)
                _fillMr.enabled = false;
            return;
        }

        lr.enabled = true;
        lr.startColor = lineColor;
        lr.endColor = lineColor;

        lr.SetPosition(0, bl);
        lr.SetPosition(1, br);
        lr.SetPosition(2, tr);
        lr.SetPosition(3, tl);
        lr.SetPosition(4, bl);

        if (_cutFloorLr != null)
        {
            _cutFloorLr.enabled = drawEmphasizedCutFloor;
            if (drawEmphasizedCutFloor)
            {
                _cutFloorLr.SetPosition(0, bl);
                _cutFloorLr.SetPosition(1, br);
                _cutFloorLr.startWidth = cutFloorLineWidth;
                _cutFloorLr.endWidth = cutFloorLineWidth;
                _cutFloorLr.startColor = cutFloorLineColor;
                _cutFloorLr.endColor = cutFloorLineColor;
            }
        }

        if (_cutRoofLr != null)
        {
            _cutRoofLr.enabled = drawEmphasizedCutRoof;
            if (drawEmphasizedCutRoof)
            {
                _cutRoofLr.SetPosition(0, tl);
                _cutRoofLr.SetPosition(1, tr);
                _cutRoofLr.startWidth = cutRoofLineWidth;
                _cutRoofLr.endWidth = cutRoofLineWidth;
                _cutRoofLr.startColor = cutRoofLineColor;
                _cutRoofLr.endColor = cutRoofLineColor;
            }
        }

        if (_fillMr != null)
        {
            if (!drawPlaneFill)
            {
                _fillMr.enabled = false;
            }
            else
            {
                _fillMr.enabled = true;
                UpdatePlaneFill(bl, br, tr, tl);
            }
        }
    }

    void SetCutLinesActive(bool on)
    {
        if (_cutFloorLr != null) _cutFloorLr.enabled = on && drawEmphasizedCutFloor;
        if (_cutRoofLr != null) _cutRoofLr.enabled = on && drawEmphasizedCutRoof;
    }
}
