using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-shot white glowing slash line through a hit note plus a particle burst, for desktop/IMU directed cuts.
/// Direction (along camera up vs down) follows saber tip velocity when strong enough; otherwise defaults up.
/// </summary>
public static class DirectedSliceWorldFx
{
    public static void Play(Vector3 worldCenter, Vector3 tipVelocityWorld)
    {
        var go = new GameObject("[DirectedSliceWorldFx]");
        go.transform.position = worldCenter;
        var runner = go.AddComponent<DirectedSliceWorldFxRunner>();
        runner.Begin(worldCenter, tipVelocityWorld);
    }
}

[DisallowMultipleComponent]
sealed class DirectedSliceWorldFxRunner : MonoBehaviour
{
    [SerializeField] float slashHalfLength = 0.62f;
    [SerializeField] float slashDuration = 0.18f;
    [SerializeField] float lineWidth = 0.14f;
    [SerializeField] int particleBurst = 42;

    LineRenderer _line;
    ParticleSystem _ps;
    Material _lineMat;
    float _t;

    public void Begin(Vector3 worldCenter, Vector3 tipVelocityWorld)
    {
        Camera cam = null;
        if (!GameplayCameraEnsurer.TryGetPreferredCamera(out cam) || cam == null)
            cam = Camera.main;

        Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
        float vmag = tipVelocityWorld.magnitude;
        float alongUp = vmag > 0.25f ? Vector3.Dot(tipVelocityWorld.normalized, camUp) : 1f;
        bool slashAlongPositiveUp = alongUp >= 0f;
        Vector3 slashDir = slashAlongPositiveUp ? camUp : -camUp;

        BuildLine(worldCenter, slashDir);
        BuildParticles(worldCenter);

        _t = 0f;
        StartCoroutine(AnimateAndDestroy());
    }

    void BuildLine(Vector3 worldCenter, Vector3 slashDirNormalized)
    {
        var lineGo = new GameObject("SlashLine");
        lineGo.transform.SetParent(transform, false);
        lineGo.transform.position = worldCenter;

        _line = lineGo.AddComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;
        _line.widthMultiplier = 1f;
        _line.startWidth = lineWidth;
        _line.endWidth = lineWidth * 0.35f;
        _line.numCornerVertices = 3;
        _line.numCapVertices = 3;
        _line.shadowCastingMode = ShadowCastingMode.Off;
        _line.receiveShadows = false;

        Vector3 n = slashDirNormalized.normalized;
        Vector3 a = worldCenter - n * slashHalfLength;
        Vector3 b = worldCenter + n * slashHalfLength;
        _line.SetPosition(0, a);
        _line.SetPosition(1, b);

        Shader sh = RenderingShaderUtil.UnlitForWorldMeshes();
        _lineMat = new Material(sh);
        _lineMat.color = new Color(1f, 1f, 1f, 0.95f);
        if (_lineMat.HasProperty("_Surface"))
            _lineMat.SetFloat("_Surface", 1f);
        if (_lineMat.HasProperty("_Blend"))
            _lineMat.SetFloat("_Blend", 0f);
        _lineMat.renderQueue = 3200;
        if (_lineMat.HasProperty("_Cull"))
            _lineMat.SetInt("_Cull", (int)CullMode.Off);
        if (_lineMat.HasProperty("_BaseColor"))
            _lineMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.95f));
        _line.material = _lineMat;

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        _line.colorGradient = grad;
    }

    void BuildParticles(Vector3 worldCenter)
    {
        var pGo = new GameObject("SliceBurst");
        pGo.transform.SetParent(transform, false);
        pGo.transform.position = worldCenter;

        _ps = pGo.AddComponent<ParticleSystem>();
        var main = _ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.85f, 0.95f, 1f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;
        main.gravityModifier = 0.35f;

        var em = _ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleBurst, (short)(particleBurst + 12)) });

        var shape = _ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        var col = _ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.6f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var rend = pGo.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        Shader pSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (pSh == null)
            pSh = Shader.Find("Particles/Standard Unlit");
        if (pSh == null)
            pSh = Shader.Find("Particles/Alpha Blended");
        if (pSh != null)
        {
            var pm = new Material(pSh);
            pm.color = Color.white;
            rend.material = pm;
        }

        _ps.Play();
    }

    IEnumerator AnimateAndDestroy()
    {
        float dur = Mathf.Max(0.05f, slashDuration);
        while (_t < dur)
        {
            _t += Time.deltaTime;
            float u = _t / dur;
            if (_line != null && _lineMat != null)
            {
                float w = Mathf.Lerp(lineWidth, lineWidth * 0.2f, u);
                _line.startWidth = w;
                _line.endWidth = w * 0.35f;
                float a = Mathf.Lerp(0.95f, 0f, u * u);
                if (_lineMat.HasProperty("_BaseColor"))
                {
                    Color bc = _lineMat.GetColor("_BaseColor");
                    bc.a = a;
                    _lineMat.SetColor("_BaseColor", bc);
                }
                else
                {
                    Color c = _lineMat.color;
                    c.a = a;
                    _lineMat.color = c;
                }
            }
            yield return null;
        }

        if (_line != null)
            Destroy(_line.gameObject);
        if (_lineMat != null)
            Destroy(_lineMat);
        yield return new WaitForSeconds(0.65f);
        Destroy(gameObject);
    }
}
