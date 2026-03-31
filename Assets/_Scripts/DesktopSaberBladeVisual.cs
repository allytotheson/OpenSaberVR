using UnityEngine;

/// <summary>
/// Visible proxy blade + slash streak. Quads billboard toward the gameplay camera so they are not edge-on/invisible.
/// </summary>
[DefaultExecutionOrder(400)]
[DisallowMultipleComponent]
public class DesktopSaberBladeVisual : MonoBehaviour
{
    public Vector2 bladeSize = new Vector2(0.2f, 1.65f);
    public float slashTravel = 0.95f;
    public float slashDuration = 0.14f;

    private SwingDetector swing;
    private Transform bladeQuad;
    private Transform slashQuad;
    private Material bladeMaterial;
    private Material slashMaterial;
    private bool wasSwinging;
    private float slashT = 1f;
    private float _quadScaleCompX = 1f;
    private float _quadScaleCompY = 1f;

    void OnEnable()
    {
        if (bladeQuad == null || slashQuad == null)
            BuildBlades();
    }

    void Start()
    {
        BuildBlades();
    }

    void BuildBlades()
    {
        if (bladeQuad != null && slashQuad != null)
            return;

        swing = GetComponent<SwingDetector>();
        if (swing == null)
            swing = GetComponentInParent<SwingDetector>();

        bool left = ResolveIsLeftSaberHand(transform);
        Color bladeColor = left ? new Color(1f, 0.15f, 0.2f, 0.95f) : new Color(0.15f, 0.45f, 1f, 0.95f);
        Color slashColor = left ? new Color(1f, 0.5f, 0.55f, 0.75f) : new Color(0.45f, 0.75f, 1f, 0.75f);

        if (bladeQuad == null)
            bladeQuad = BuildQuad("ProxyBlade", bladeColor, 15, out bladeMaterial);
        if (slashQuad == null)
            slashQuad = BuildQuad("SlashStreak", slashColor, 14, out slashMaterial);

        UpdateScaleCompensation();
        if (bladeQuad != null)
        {
            bladeQuad.localScale = new Vector3(_quadScaleCompX, _quadScaleCompY, 1f);
            bladeQuad.localPosition = Vector3.zero;
            bladeQuad.localRotation = Quaternion.identity;
        }
        if (slashQuad != null)
        {
            slashQuad.localScale = new Vector3(_quadScaleCompX * 0.32f, _quadScaleCompY * 0.52f, 1f);
            slashQuad.localRotation = Quaternion.identity;
            slashQuad.gameObject.SetActive(false);
        }
    }

    void DestroyDuplicateProxyChildren()
    {
        int pb = 0;
        int ss = 0;
        foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == transform)
                continue;
            string n = t.name;
            if (n == "ProxyBlade" || n.StartsWith("ProxyBlade", System.StringComparison.Ordinal))
            {
                pb++;
                if (pb > 1)
                    Destroy(t.gameObject);
            }
            else if (n == "SlashStreak" || n.StartsWith("SlashStreak", System.StringComparison.Ordinal))
            {
                ss++;
                if (ss > 1)
                    Destroy(t.gameObject);
            }
        }
    }

    /// <summary>
    /// Slice transforms often use tiny local scale (e.g. 0.05) on the mesh; without this, proxy quads shrink to invisibility.
    /// </summary>
    void UpdateScaleCompensation()
    {
        Vector3 ls = transform.lossyScale;
        _quadScaleCompX = bladeSize.x / Mathf.Max(Mathf.Abs(ls.x), 0.02f);
        _quadScaleCompY = bladeSize.y / Mathf.Max(Mathf.Abs(ls.y), 0.02f);
    }

    /// <summary>Slice child may be untagged; walk parents for LeftSaber / RightSaber.</summary>
    static bool ResolveIsLeftSaberHand(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            if (x.CompareTag("LeftSaber")) return true;
            if (x.CompareTag("RightSaber")) return false;
        }
        return t.CompareTag("LeftSaber");
    }

    static Shader ResolveBillboardShader() => RenderingShaderUtil.UnlitForWorldMeshes();

    Transform BuildQuad(string objectName, Color color, int renderQueue, out Material mat)
    {
        mat = null;
        Shader sh = ResolveBillboardShader();
        if (sh == null)
            return null;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = objectName;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.identity;

        mat = new Material(sh);
        mat.color = color;
        mat.renderQueue = renderQueue;
        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static void BillboardQuadTowardCamera(Transform quad, Camera cam)
    {
        if (quad == null || cam == null) return;
        Vector3 toCam = cam.transform.position - quad.position;
        if (toCam.sqrMagnitude < 1e-8f)
            return;
        quad.rotation = Quaternion.LookRotation(toCam.normalized, cam.transform.up);
    }

    void LateUpdate()
    {
        UpdateScaleCompensation();
        if (bladeQuad != null)
            bladeQuad.localScale = new Vector3(_quadScaleCompX, _quadScaleCompY, 1f);

        if (swing != null && slashQuad != null)
        {
            bool sw = swing.IsSwinging;
            if (sw && !wasSwinging)
            {
                slashT = 0f;
                slashQuad.gameObject.SetActive(true);
            }
            wasSwinging = sw;

            if (slashT < 1f)
            {
                slashT += Time.deltaTime / Mathf.Max(0.02f, slashDuration);
                float u = Mathf.SmoothStep(0f, 1f, slashT);
                float y = Mathf.Lerp(-slashTravel * 0.42f, slashTravel * 0.42f, u);
                slashQuad.localPosition = new Vector3(0f, y, -0.012f);
                float wobble = 1f + 0.35f * Mathf.Sin(u * Mathf.PI);
                slashQuad.localScale = new Vector3(
                    _quadScaleCompX * 0.32f * (1.05f - 0.2f * u),
                    _quadScaleCompY * 0.52f * wobble,
                    1f);
            }
            else if (slashQuad.gameObject.activeSelf && !sw)
                slashQuad.gameObject.SetActive(false);
        }

        if (!DesktopGameplayCamera.TryGet(out Camera cam))
            return;

        if (bladeQuad != null)
            BillboardQuadTowardCamera(bladeQuad, cam);
        if (slashQuad != null && slashQuad.gameObject.activeSelf)
            BillboardQuadTowardCamera(slashQuad, cam);
    }

    void OnDestroy()
    {
        if (bladeMaterial != null) Destroy(bladeMaterial);
        if (slashMaterial != null) Destroy(slashMaterial);
    }
}
