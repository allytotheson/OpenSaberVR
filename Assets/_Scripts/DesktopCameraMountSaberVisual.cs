using UnityEngine;

/// <summary>
/// Desktop-only capsule blades parented to each real <see cref="Slice"/> (same transform as hits).
/// Slash motion runs on keyboard presses (Z/X/comma/period/Space), not on auto-align micro-pulses.
/// </summary>
[DefaultExecutionOrder(450)]
[DisallowMultipleComponent]
public class DesktopCameraMountSaberVisual : MonoBehaviour
{
    [Header("Blade proxy (parent local space; compensated for tiny Slice scale)")]
    public float bladeLengthWorld = 1.05f;

    public float bladeRadiusWorld = 0.055f;

    [Tooltip("Slice mesh is usually long on local Z after rig rotation; capsule height maps there.")]
    public Vector3 capsuleLocalEuler = new Vector3(90f, 0f, 0f);

    public Vector3 capsuleCenterLocal = new Vector3(0f, 0f, 0.38f);

    [Header("Keyboard slash motion")]
    public float slashDuration = 0.14f;

    [Tooltip("Extra pitch (degrees) on proxy during a key slash, eased in/out.")]
    public float slashPitchDegrees = 52f;

    [Tooltip("Hide red/blue capsule proxies when ImportedSaberBlade is parented under the Slice (show Rumi mesh only).")]
    public bool hideProxiesWhenImportedBladePresent = true;

    [Tooltip("No desktop capsule proxies (e.g. driven from NotesSpawner → DesktopSaberVisualHider).")]
    public bool hideBladeVisual = false;

    Transform _leftSlice;
    Transform _rightSlice;
    Transform _leftProxy;
    Transform _rightProxy;
    Transform _leftProxyTargetSlice;
    Transform _rightProxyTargetSlice;
    float _slashLeft;
    float _slashRight;

    void OnEnable()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        DesktopSaberTestInput.LeftKeyboardSlashPressed += OnLeftKeyboardSlash;
        DesktopSaberTestInput.RightKeyboardSlashPressed += OnRightKeyboardSlash;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        DesktopSaberTestInput.LeftKeyboardSlashPressed -= OnLeftKeyboardSlash;
        DesktopSaberTestInput.RightKeyboardSlashPressed -= OnRightKeyboardSlash;
#endif
    }

    void OnDestroy()
    {
        DestroyProxy(ref _leftProxy);
        DestroyProxy(ref _rightProxy);
        _leftProxyTargetSlice = null;
        _rightProxyTargetSlice = null;
    }

    void OnLeftKeyboardSlash() => _slashLeft = 1f;

    void OnRightKeyboardSlash() => _slashRight = 1f;

    void LateUpdate()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (GameplayCameraEnsurer.IsXrDeviceActive())
        {
            SetProxiesActive(false);
            return;
        }

        if (hideBladeVisual)
        {
            SetProxiesActive(false);
            DestroyProxy(ref _leftProxy);
            DestroyProxy(ref _rightProxy);
            _leftProxyTargetSlice = null;
            _rightProxyTargetSlice = null;
            return;
        }

        if (!TryResolveSliceRoots(out _leftSlice, out _rightSlice))
        {
            DestroyProxy(ref _leftProxy);
            DestroyProxy(ref _rightProxy);
            _leftProxyTargetSlice = null;
            _rightProxyTargetSlice = null;
            return;
        }

        if (_leftSlice == null)
        {
            DestroyProxy(ref _leftProxy);
            _leftProxyTargetSlice = null;
        }
        else if (hideProxiesWhenImportedBladePresent && SliceHasImportedBlade(_leftSlice))
        {
            DestroyProxy(ref _leftProxy);
            _leftProxyTargetSlice = null;
        }
        else
            EnsureProxy(ref _leftProxy, ref _leftProxyTargetSlice, _leftSlice, true);

        if (_rightSlice == null)
        {
            DestroyProxy(ref _rightProxy);
            _rightProxyTargetSlice = null;
        }
        else if (hideProxiesWhenImportedBladePresent && SliceHasImportedBlade(_rightSlice))
        {
            DestroyProxy(ref _rightProxy);
            _rightProxyTargetSlice = null;
        }
        else
            EnsureProxy(ref _rightProxy, ref _rightProxyTargetSlice, _rightSlice, false);

        float dt = Time.deltaTime;
        _slashLeft = Mathf.Max(0f, _slashLeft - dt / Mathf.Max(0.02f, slashDuration));
        _slashRight = Mathf.Max(0f, _slashRight - dt / Mathf.Max(0.02f, slashDuration));

        ApplyProxyPose(_leftProxy, _leftSlice, _slashLeft);
        ApplyProxyPose(_rightProxy, _rightSlice, _slashRight);
        SetProxiesActive(true);
#endif
    }

    static bool SliceHasImportedBlade(Transform slice)
    {
        if (slice == null)
            return false;
        return DesktopImportedBladeMount.FindBladeChildRecursive(slice, DesktopImportedBladeMount.ImportedBladeChildName) != null;
    }

    void SetProxiesActive(bool on)
    {
        if (_leftProxy != null)
            _leftProxy.gameObject.SetActive(on);
        if (_rightProxy != null)
            _rightProxy.gameObject.SetActive(on);
    }

    void DestroyProxy(ref Transform proxy)
    {
        if (proxy == null)
            return;
        Destroy(proxy.gameObject);
        proxy = null;
    }

    void EnsureProxy(ref Transform proxy, ref Transform boundTargetSlice, Transform slice, bool isLeft)
    {
        if (slice == null)
            return;

        if (proxy != null && boundTargetSlice != slice)
        {
            Destroy(proxy.gameObject);
            proxy = null;
        }

        boundTargetSlice = slice;

        if (proxy != null)
            return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = isLeft ? "DesktopBladeProxy_Left" : "DesktopBladeProxy_Right";
        Destroy(go.GetComponent<Collider>());
        proxy = go.transform;
        // Unparented so huge lossy scale on the Slice hierarchy does not crush world size to zero.
        proxy.SetParent(null, false);

        var rend = go.GetComponent<MeshRenderer>();
        Shader sh = RenderingShaderUtil.UnlitForWorldMeshes();
        var mat = new Material(sh);
        Color c = isLeft ? new Color(1f, 0.22f, 0.28f, 1f) : new Color(0.22f, 0.58f, 1f, 1f);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", c);
        rend.sharedMaterial = mat;
    }

    void ApplyProxyPose(Transform proxy, Transform slice, float slash01Rem)
    {
        if (proxy == null || slice == null)
            return;

        float slashPhase = 1f - slash01Rem;
        float ease = slashPhase <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, slashPhase);
        float pitch = slashPitchDegrees * Mathf.Sin(ease * Mathf.PI);
        Quaternion slashLocal = Quaternion.Euler(capsuleLocalEuler) * Quaternion.Euler(pitch, 0f, 0f);

        proxy.SetPositionAndRotation(slice.TransformPoint(capsuleCenterLocal), slice.rotation * slashLocal);

        // Primitive capsule: height 2, radius 0.5 at scale 1 (local Y = length). World size independent of Slice scale.
        proxy.localScale = new Vector3(
            bladeRadiusWorld / 0.5f,
            bladeLengthWorld / 2f,
            bladeRadiusWorld / 0.5f);
    }

    static bool TryResolveSliceRoots(out Transform left, out Transform right)
    {
        left = null;
        right = null;
        var sceneHandling = Object.FindAnyObjectByType<SceneHandling>();
        GameObject lgo = sceneHandling != null ? sceneHandling.LeftSaber : null;
        GameObject rgo = sceneHandling != null ? sceneHandling.RightSaber : null;
        if (lgo == null)
        {
            var t = GameObject.FindGameObjectWithTag("LeftSaber");
            if (t != null) lgo = t;
        }

        if (rgo == null)
        {
            var t = GameObject.FindGameObjectWithTag("RightSaber");
            if (t != null) rgo = t;
        }

        if (lgo != null)
        {
            var s = lgo.GetComponentInChildren<Slice>(true);
            if (s != null)
                left = s.transform;
        }

        if (rgo != null)
        {
            var s = rgo.GetComponentInChildren<Slice>(true);
            if (s != null)
                right = s.transform;
        }

        if (left != null || right != null)
            return true;

        var slices = Object.FindObjectsByType<Slice>(FindObjectsInactive.Include);
        if (slices == null || slices.Length == 0)
            return false;

        var parents = new System.Collections.Generic.List<Transform>();
        foreach (var sl in slices)
        {
            if (sl == null) continue;
            Transform p = sl.transform.parent;
            if (p == null) continue;
            bool dup = false;
            foreach (var q in parents)
            {
                if (q == p) { dup = true; break; }
            }
            if (!dup) parents.Add(p);
        }

        if (parents.Count >= 2)
        {
            parents.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            left = parents[0].GetComponentInChildren<Slice>(true)?.transform;
            right = parents[parents.Count - 1].GetComponentInChildren<Slice>(true)?.transform;
        }
        else if (parents.Count == 1)
        {
            left = parents[0].GetComponentInChildren<Slice>(true)?.transform;
        }

        return left != null || right != null;
    }
}
