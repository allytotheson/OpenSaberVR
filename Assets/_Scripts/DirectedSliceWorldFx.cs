using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-shot white glowing slash line through a hit note plus a particle burst, for desktop/IMU directed cuts.
/// Direction (along camera up vs down) follows saber tip velocity when strong enough; otherwise defaults up.
/// </summary>
public static class DirectedSliceWorldFx
{
    /// <param name="teamTint">Slash line + sparks (defaults to white if unset).</param>
    /// <param name="perfectBonus">Extra spark burst with emission +1 (brighter glow) when hit points are Perfect (see <see cref="HitMissFlyout.PointGainPerfectThreshold"/>).</param>
    public static void Play(Vector3 worldCenter, Vector3 tipVelocityWorld, Color? teamTint = null, bool perfectBonus = false)
    {
        var go = new GameObject("[DirectedSliceWorldFx]");
        go.transform.position = worldCenter;
        var runner = go.AddComponent<DirectedSliceWorldFxRunner>();
        runner.Begin(worldCenter, tipVelocityWorld, teamTint ?? Color.white, perfectBonus);
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
    ParticleSystem _perfectBonusPs;
    Material _lineMat;
    float _t;

    Color _teamTint;

    public void Begin(Vector3 worldCenter, Vector3 tipVelocityWorld, Color teamTint, bool perfectBonus)
    {
        _teamTint = teamTint;
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
        if (perfectBonus)
            BuildPerfectBonusParticles(worldCenter);

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
        Color lineCore = _teamTint;
        _lineMat.color = new Color(lineCore.r, lineCore.g, lineCore.b, 0.95f);
        if (_lineMat.HasProperty("_Surface"))
            _lineMat.SetFloat("_Surface", 1f);
        if (_lineMat.HasProperty("_Blend"))
            _lineMat.SetFloat("_Blend", 0f);
        _lineMat.renderQueue = 3200;
        if (_lineMat.HasProperty("_Cull"))
            _lineMat.SetInt("_Cull", (int)CullMode.Off);
        if (_lineMat.HasProperty("_BaseColor"))
            _lineMat.SetColor("_BaseColor", new Color(lineCore.r, lineCore.g, lineCore.b, 0.95f));
        _line.material = _lineMat;

        Color lineSoft = Color.Lerp(lineCore, Color.white, 0.35f);
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(lineCore, 0f), new GradientColorKey(lineSoft, 1f) },
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
        // Default playOnAwake runs in Awake before we can set main; stop+clear so main.* can be configured.
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        Color sparkB = Color.Lerp(_teamTint, Color.white, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(_teamTint, new Color(sparkB.r, sparkB.g, sparkB.b, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;
        main.gravityModifier = 0.35f;

        var em = _ps.emission;
        em.rateOverTime = 0f;
        short minB = (short)particleBurst;
        short maxB = (short)(particleBurst + 12);
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, minB, maxB) });

        var shape = _ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;

        var col = _ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(_teamTint, 0f), new GradientColorKey(sparkB, 0.6f) },
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

    /// <summary>Second pop of sparks on Perfect hits (same tint, faster / tighter).</summary>
    void BuildPerfectBonusParticles(Vector3 worldCenter)
    {
        var pGo = new GameObject("PerfectBonusBurst");
        pGo.transform.SetParent(transform, false);
        pGo.transform.position = worldCenter;

        _perfectBonusPs = pGo.AddComponent<ParticleSystem>();
        _perfectBonusPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _perfectBonusPs.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 3.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
        Color sparkB = Color.Lerp(_teamTint, Color.white, 0.45f);
        // Emission +1: stronger HDR tint so perfect bonus reads as a glow (bloom), not extra on-screen text.
        const float emissionPlusOne = 1f;
        Color glowA = HdrEmissionBoost(_teamTint, emissionPlusOne);
        Color glowB = HdrEmissionBoost(sparkB, emissionPlusOne);
        main.startColor = new ParticleSystem.MinMaxGradient(glowA, glowB);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 73;
        main.gravityModifier = 0.42f;

        var em = _perfectBonusPs.emission;
        em.rateOverTime = 0f;
        // +1 to burst counts vs prior perfect-only pop (25–39 vs 24–38).
        em.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 25, 39) });

        var shape = _perfectBonusPs.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.11f;

        var col = _perfectBonusPs.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(HdrEmissionBoost(Color.Lerp(_teamTint, Color.white, 0.25f), emissionPlusOne), 0f), new GradientColorKey(glowB, 0.55f) },
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

        _perfectBonusPs.Play();
    }

    /// <summary>Raises RGB so HDR / bloom picks up a visible glow (+1 emission step).</summary>
    static Color HdrEmissionBoost(Color c, float plusOne)
    {
        float m = 1f + Mathf.Max(0f, plusOne);
        return new Color(c.r * m, c.g * m, c.b * m, c.a);
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
