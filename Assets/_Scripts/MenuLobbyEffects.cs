using UnityEngine;

/// <summary>
/// Menu-only: rolling exponential fog + layered soft "nebula" particles (Beat Saber–style mid-ground haze),
/// sparks, and occasional distant glow bursts. Black sky stays visible at zenith; density is strongest toward the horizon band.
/// </summary>
[DefaultExecutionOrder(-20)]
public class MenuLobbyEffects : MonoBehaviour
{
    static readonly Color AccentPurple = new Color(206f / 255f, 123f / 255f, 220f / 255f, 1f);
    static readonly Color AccentBlue = new Color(49f / 255f, 70f / 255f, 158f / 255f, 1f);

    [SerializeField] Color fogColorDeep = new Color(0.04f, 0.02f, 0.08f, 1f);
    [SerializeField] Color fogColorBright = new Color(0.38f, 0.16f, 0.42f, 1f);
    [SerializeField] float fogDensity = 0.042f;
    [SerializeField] float fogRollSpeed = 0.09f;

    ParticleSystem _sparks;
    ParticleSystem _nebulaMain;
    ParticleSystem _nebulaDetail;
    ParticleSystem _plasma;
    float _nextPlasma;

    void Awake()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = fogDensity;

        _sparks = BuildSparks(transform);
        _nebulaMain = BuildNebulaMain(transform);
        _nebulaDetail = BuildNebulaDetail(transform);
        _plasma = BuildPlasma(transform);
        _nextPlasma = Time.unscaledTime + Random.Range(4f, 9f);
    }

    void OnDestroy()
    {
        if (_sparks != null) Destroy(_sparks.gameObject);
        if (_nebulaMain != null) Destroy(_nebulaMain.gameObject);
        if (_nebulaDetail != null) Destroy(_nebulaDetail.gameObject);
        if (_plasma != null) Destroy(_plasma.gameObject);
    }

    void Update()
    {
        float slow = Mathf.Sin(Time.unscaledTime * fogRollSpeed) * 0.5f + 0.5f;
        float fast = Mathf.Sin(Time.unscaledTime * (fogRollSpeed * 2.17f) + 1.1f) * 0.5f + 0.5f;
        float roll = Mathf.SmoothStep(0f, 1f, slow * 0.65f + fast * 0.35f);

        Color deepTint = Color.Lerp(fogColorDeep, AccentBlue * 0.22f, 0.35f);
        Color brightTint = Color.Lerp(fogColorBright, Color.Lerp(AccentPurple, AccentBlue, 0.45f) * 0.55f, 0.5f);
        RenderSettings.fogColor = Color.Lerp(deepTint, brightTint, roll);
        float densityRoll = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * (fogRollSpeed * 0.85f) + 0.4f);
        RenderSettings.fogDensity = fogDensity * densityRoll;

        if (Time.unscaledTime >= _nextPlasma)
        {
            EmitPlasmaBurst();
            _nextPlasma = Time.unscaledTime + Random.Range(5f, 11f);
        }
    }

    static Material TryParticleMaterial()
    {
        var s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null)
            s = Shader.Find("Particles/Standard Unlit");
        if (s == null)
            return null;
        var m = new Material(s);
        m.SetColor("_BaseColor", Color.white);
        m.enableInstancing = true;
        return m;
    }

    static void ApplySoftBillboardRenderer(ParticleSystem ps)
    {
        var rnd = ps.GetComponent<ParticleSystemRenderer>();
        rnd.renderMode = ParticleSystemRenderMode.Billboard;
        var mat = TryParticleMaterial();
        if (mat != null)
            rnd.sharedMaterial = mat;
    }

    /// <summary>Unity may start simulating immediately on AddComponent; stop before changing duration / loop, etc.</summary>
    static void StopParticleSystemForSetup(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
    }

    static ParticleSystem BuildSparks(Transform parent)
    {
        var go = new GameObject("LobbySparks");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        StopParticleSystemForSetup(ps);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f);
        main.startColor = new Color(0.88f, 0.72f, 1f, 0.42f);
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.rateOverTime = 4f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 48f;
        sh.radiusThickness = 0.75f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.1f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ApplySoftBillboardRenderer(ps);
        return ps;
    }

    /// <summary>Large slow billboards for wispy nebula mass behind the UI.</summary>
    static ParticleSystem BuildNebulaMain(Transform parent)
    {
        var go = new GameObject("LobbyNebulaMain");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 2f, 38f);
        var ps = go.AddComponent<ParticleSystem>();
        StopParticleSystemForSetup(ps);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.15f, 0.55f, 0.5f),
            new Color(0.2f, 0.28f, 0.65f, 0.58f));
        main.maxParticles = 280;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var em = ps.emission;
        em.rateOverTime = 13f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 42f;
        sh.radiusThickness = 0.55f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.32f, 0.62f);
        noise.frequency = 0.1f;
        noise.scrollSpeed = 0.05f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.High;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.2f, 0.65f), 0f),
                new GradientColorKey(new Color(0.25f, 0.35f, 0.75f), 0.4f),
                new GradientColorKey(new Color(0.55f, 0.35f, 0.85f), 0.72f),
                new GradientColorKey(new Color(0.18f, 0.22f, 0.55f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.4f, 0.12f),
                new GradientAlphaKey(0.28f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.65f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.75f)));

        ApplySoftBillboardRenderer(ps);
        return ps;
    }

    static ParticleSystem BuildNebulaDetail(Transform parent)
    {
        var go = new GameObject("LobbyNebulaDetail");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 5f, 52f);
        var ps = go.AddComponent<ParticleSystem>();
        StopParticleSystemForSetup(ps);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 20f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 4.5f);
        main.startColor = new Color(0.72f, 0.55f, 0.95f, 0.38f);
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.rateOverTime = 17f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Hemisphere;
        sh.radius = 38f;
        sh.radiusThickness = 0.4f;

        var n2 = ps.noise;
        n2.enabled = true;
        n2.strength = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        n2.frequency = 0.2f;
        n2.scrollSpeed = 0.1f;
        n2.damping = true;
        n2.quality = ParticleSystemNoiseQuality.Medium;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.75f, 0.45f, 0.95f), 0f),
                new GradientColorKey(new Color(0.35f, 0.4f, 0.85f), 0.55f),
                new GradientColorKey(new Color(0.22f, 0.3f, 0.62f), 1f)
            },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.52f, 0.15f), new GradientAlphaKey(0.14f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ApplySoftBillboardRenderer(ps);
        return ps;
    }

    static ParticleSystem BuildPlasma(Transform parent)
    {
        var go = new GameObject("LobbyDistantGlow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 4f, 72f);
        var ps = go.AddComponent<ParticleSystem>();
        StopParticleSystemForSetup(ps);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startColor = new Color(0.55f, 0.35f, 0.9f, 0.16f);
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 8f;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.75f, 0.5f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.35f, 0.75f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.26f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ApplySoftBillboardRenderer(ps);
        return ps;
    }

    void EmitPlasmaBurst()
    {
        if (_plasma == null) return;
        _plasma.transform.position = new Vector3(Random.Range(-60f, 60f), Random.Range(3f, 16f), Random.Range(40f, 110f));
        _plasma.Emit(Random.Range(2, 5));
    }
}
