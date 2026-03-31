using UnityEngine;

/// <summary>
/// Soft additive quad billboarding to the gameplay camera — visible team glow even when sword materials
/// do not support emission (common with downloaded PBR sets).
/// </summary>
[DefaultExecutionOrder(425)]
[DisallowMultipleComponent]
public sealed class DesktopImportedBladeAura : MonoBehaviour
{
    const string QuadName = "ImportedBladeAuraQuad";

    bool _isLeft;
    float _alpha = 0.5f;
    float _boundsScale = 1.12f;
    Transform _quad;
    Material _mat;

    public void Configure(bool isLeft, float alpha, float boundsScale)
    {
        _isLeft = isLeft;
        _alpha = Mathf.Clamp01(alpha);
        _boundsScale = Mathf.Max(0.5f, boundsScale);
        if (GameplayCameraEnsurer.IsXrDeviceActive())
        {
            enabled = false;
            return;
        }

        EnsureQuad();
        if (_mat != null)
            ApplyColorToMaterial(_mat);
        FitQuadToBladeBounds();
    }

    void OnEnable()
    {
        if (GameplayCameraEnsurer.IsXrDeviceActive())
            enabled = false;
    }

    void Start()
    {
        EnsureQuad();
        if (_mat != null)
            ApplyColorToMaterial(_mat);
        FitQuadToBladeBounds();
    }

    void LateUpdate()
    {
        if (_quad == null || !DesktopGameplayCamera.TryGet(out Camera cam))
            return;
        Vector3 toCam = cam.transform.position - _quad.position;
        if (toCam.sqrMagnitude < 1e-8f)
            return;
        _quad.rotation = Quaternion.LookRotation(toCam.normalized, cam.transform.up);
    }

    void OnDestroy()
    {
        if (_mat != null)
            Destroy(_mat);
    }

    void EnsureQuad()
    {
        if (_quad != null)
            return;

        var existing = transform.Find(QuadName);
        if (existing != null)
        {
            _quad = existing;
            _mat = _quad.GetComponent<MeshRenderer>()?.material;
            FitQuadToBladeBounds();
            return;
        }

        Shader sh = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null)
            return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = QuadName;
        Destroy(go.GetComponent<Collider>());
        _quad = go.transform;
        _quad.SetParent(transform, false);

        var mr = go.GetComponent<MeshRenderer>();
        _mat = new Material(sh);
        ApplyColorToMaterial(_mat);
        _mat.renderQueue = 3150;
        if (_mat.HasProperty("_Cull"))
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        mr.sharedMaterial = _mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        FitQuadToBladeBounds();
    }

    void ApplyColorToMaterial(Material m)
    {
        if (m == null)
            return;
        Color team = _isLeft ? new Color(1f, 0.22f, 0.32f, _alpha) : new Color(0.22f, 0.55f, 1f, _alpha);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", team);
        if (m.HasProperty("_Color"))
            m.color = team;
    }

    void FitQuadToBladeBounds()
    {
        if (_quad == null)
            return;

        bool any = false;
        Bounds b = default;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.transform == _quad)
                continue;
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else
                b.Encapsulate(r.bounds);
        }

        if (!any)
            b = new Bounds(transform.position, Vector3.one * 0.6f);

        Vector3 worldCenter = b.center;
        Vector3 worldSize = b.size * _boundsScale;
        _quad.position = worldCenter;

        float u = Mathf.Max(Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)),
            Mathf.Abs(transform.lossyScale.z));
        u = Mathf.Max(u, 0.02f);

        float w = Mathf.Max(worldSize.x, worldSize.z, 0.08f);
        float h = Mathf.Max(worldSize.y, 0.08f);
        _quad.localScale = new Vector3(w / u, h / u, 1f);
    }
}
