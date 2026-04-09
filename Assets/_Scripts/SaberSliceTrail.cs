using UnityEngine;

/// <summary>
/// Animated trail arc on each saber that fires when a slice is detected (IMU gyro spike or keyboard slash).
/// A child <see cref="TrailRenderer"/> emitter sits at the blade tip and sweeps through a short arc on
/// slash events, then snaps back to rest. The <see cref="TrailRenderer"/>'s built-in time fade draws
/// the glowing trail. Subscribes to <see cref="DesktopSaberTestInput"/> slash events (same hook as
/// <see cref="DesktopImportedBladeSlashFx"/>).
/// </summary>
[DefaultExecutionOrder(450)]
[DisallowMultipleComponent]
public class SaberSliceTrail : MonoBehaviour
{
    [Header("Trail Arc")]
    [Tooltip("Duration of the sweep arc in seconds.")]
    public float sweepDuration = 0.14f;

    [Tooltip("How far the emitter sweeps from the blade tip (meters, local space).")]
    public float sweepRadius = 0.6f;

    [Tooltip("Offset along the blade axis (local Y) for the trail emitter rest position.")]
    public float tipOffset = 0.8f;

    [Header("Trail Renderer")]
    [Tooltip("How long the trail segment persists before fading.")]
    public float trailTime = 0.25f;

    [Tooltip("Start width of the trail.")]
    public float trailStartWidth = 0.12f;

    [Tooltip("End width of the trail.")]
    public float trailEndWidth = 0.01f;

    bool _isLeft;
    bool _configured;
    float _sweepT = -1f;
    Transform _emitter;
    TrailRenderer _trail;
    Vector3 _restLocalPos;
    bool _sweepDown = true;

    public void Configure(bool isLeftHand)
    {
        UnsubscribeEvents();
        _isLeft = isLeftHand;
        _configured = true;
        BuildEmitter();
        if (isActiveAndEnabled)
            SubscribeEvents();
    }

    void OnEnable()
    {
        if (_configured)
            SubscribeEvents();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    void OnDestroy()
    {
        UnsubscribeEvents();
    }

    void SubscribeEvents()
    {
        if (_isLeft)
            DesktopSaberTestInput.LeftKeyboardSlashPressed += OnSlash;
        else
            DesktopSaberTestInput.RightKeyboardSlashPressed += OnSlash;
        SwingDetector.SwingStarted += OnSwingStarted;
    }

    void UnsubscribeEvents()
    {
        DesktopSaberTestInput.LeftKeyboardSlashPressed -= OnSlash;
        DesktopSaberTestInput.RightKeyboardSlashPressed -= OnSlash;
        SwingDetector.SwingStarted -= OnSwingStarted;
    }

    void OnSwingStarted(bool isLeftHand)
    {
        if (isLeftHand != _isLeft)
            return;
        StartSweep(ComputeSweepDown());
    }

    bool ComputeSweepDown()
    {
        var mc = GetComponentInParent<SaberMotionController>();
        if (mc == null)
            return true;
        Vector3 v = mc.GetTipVelocity();
        if (v.sqrMagnitude < 1e-8f)
            return true;
        return v.y <= 0f;
    }

    void BuildEmitter()
    {
        if (_emitter != null)
            return;

        _restLocalPos = new Vector3(0f, tipOffset, 0f);

        var go = new GameObject("SliceTrailEmitter");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = _restLocalPos;
        go.transform.localRotation = Quaternion.identity;
        _emitter = go.transform;

        _trail = go.AddComponent<TrailRenderer>();
        _trail.time = trailTime;
        _trail.startWidth = trailStartWidth;
        _trail.endWidth = trailEndWidth;
        _trail.numCornerVertices = 4;
        _trail.numCapVertices = 4;
        _trail.receiveShadows = false;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.emitting = false;
        _trail.autodestruct = false;
        _trail.minVertexDistance = 0.01f;

        // Bright white slice trail (hand side still distinguished by sweep direction / motion)
        Color core = new Color(1f, 1f, 1f, 0.92f);

        var colorGrad = new Gradient();
        colorGrad.SetKeys(
            new[] { new GradientColorKey(core, 0f), new GradientColorKey(core, 0.4f), new GradientColorKey(core, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        _trail.colorGradient = colorGrad;

        Shader sh = RenderingShaderUtil.UnlitForWorldMeshes();
        if (sh != null)
        {
            var mat = new Material(sh);
            mat.color = core;
            mat.renderQueue = 3200;
            if (mat.HasProperty("_Cull"))
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _trail.sharedMaterial = mat;
        }
    }

    void OnSlash()
    {
        StartSweep(true);
    }

    void StartSweep(bool sweepDown)
    {
        if (_emitter == null)
            BuildEmitter();
        if (_trail == null)
            return;

        _sweepDown = sweepDown;
        _trail.Clear();
        _trail.emitting = true;
        _sweepT = 0f;
    }

    void LateUpdate()
    {
        if (_sweepT < 0f || _emitter == null)
            return;

        _sweepT += Time.deltaTime / Mathf.Max(0.03f, sweepDuration);

        if (_sweepT >= 1f)
        {
            _sweepT = -1f;
            if (_trail != null)
                _trail.emitting = false;
            _emitter.localPosition = _restLocalPos;
            return;
        }

        float u = Mathf.SmoothStep(0f, 1f, _sweepT);
        float angleStart = _sweepDown ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f;
        float angleEnd   = _sweepDown ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f;
        float angle = Mathf.Lerp(angleStart, angleEnd, u);
        float x = Mathf.Cos(angle) * sweepRadius * (_isLeft ? -1f : 1f);
        float y = tipOffset + Mathf.Sin(angle) * sweepRadius;
        _emitter.localPosition = new Vector3(x, y, 0f);
    }
}
