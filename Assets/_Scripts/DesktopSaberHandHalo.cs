using UnityEngine;

/// <summary>
/// Two additive-style rings around the blade mount (left = red, right = blue) for team-color halos.
/// </summary>
public static class DesktopSaberHandHalo
{
    public const string HaloRootName = "DesktopSaberHalo";

    /// <summary>Destroys all desktop ring halos (unparented roots). Call when leaving gameplay so they never linger on the menu.</summary>
    public static void DestroyAllWorldHalos()
    {
        var followers = Object.FindObjectsByType<DesktopSaberHaloWorldFollower>(FindObjectsInactive.Include);
        for (int i = 0; i < followers.Length; i++)
        {
            var f = followers[i];
            if (f != null)
                Object.Destroy(f.gameObject);
        }
    }

    /// <summary>World-space ring radii before compensating for tiny Slice lossy scale.</summary>
    public static float innerRingRadiusWorld = 0.14f;

    public static float outerRingRadiusWorld = 0.3f;

    public static float innerLineWidthWorld = 0.034f;

    public static float outerLineWidthWorld = 0.052f;

    /// <summary>Extra wide red (or blue) bloom ring for visibility.</summary>
    public static float bloomRingRadiusWorld = 0.48f;

    public static float bloomLineWidthWorld = 0.09f;

    /// <param name="mount">Slice transform (blade mount). Halo is not parented here — see <see cref="DesktopSaberHaloWorldFollower"/> — so FBX / rig scale does not crush LineRenderer width.</param>
    public static void EnsureAtBladeMount(Transform mount, bool isLeft, Vector3 haloLocalEuler = default, Vector3 haloLocalPosition = default)
    {
        if (mount == null || GameplayCameraEnsurer.IsXrDeviceActive())
            return;

        RemoveAllHalosUnder(mount);
        DestroyWorldHalosFollowing(mount);

        var root = new GameObject(HaloRootName);
        root.transform.SetParent(null, false);

        var follower = root.AddComponent<DesktopSaberHaloWorldFollower>();
        follower.follow = mount;
        follower.localPosition = haloLocalPosition;
        follower.localRotation = Quaternion.Euler(haloLocalEuler);

        Color core = isLeft ? new Color(1f, 0.08f, 0.18f, 1f) : new Color(0.08f, 0.42f, 1f, 1f);
        Color glow = isLeft ? new Color(1f, 0.35f, 0.45f, 0.78f) : new Color(0.32f, 0.68f, 1f, 0.78f);
        Color bloom = isLeft ? new Color(1f, 0.2f, 0.28f, 0.45f) : new Color(0.22f, 0.55f, 1f, 0.45f);

        float u = UniformLossy(mount);
        float rIn = innerRingRadiusWorld / u;
        float rOut = outerRingRadiusWorld / u;
        float rBloom = bloomRingRadiusWorld / u;
        float wIn = innerLineWidthWorld / u;
        float wOut = outerLineWidthWorld / u;
        float wBloom = bloomLineWidthWorld / u;

        BuildRing(root.transform, "HaloInner", rIn, wIn, core, 48);
        BuildRing(root.transform, "HaloOuter", rOut, wOut, glow, 64);
        BuildRing(root.transform, "HaloBloom", rBloom, wBloom, bloom, 72);
    }

    static float UniformLossy(Transform t)
    {
        Vector3 ls = t.lossyScale;
        float u = Mathf.Max(Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y)), Mathf.Abs(ls.z));
        return Mathf.Max(u, 0.02f);
    }

    static void RemoveAllHalosUnder(Transform mount)
    {
        var trs = mount.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            Transform x = trs[i];
            if (x != null && x.name == HaloRootName)
                Object.Destroy(x.gameObject);
        }
    }

    static void DestroyWorldHalosFollowing(Transform mount)
    {
        var followers = Object.FindObjectsByType<DesktopSaberHaloWorldFollower>(FindObjectsInactive.Include);
        for (int i = 0; i < followers.Length; i++)
        {
            var f = followers[i];
            if (f != null && f.follow == mount)
                Object.Destroy(f.gameObject);
        }
    }

    static void BuildRing(Transform parent, string objectName, float radius, float width, Color color, int segments)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var lr = go.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.positionCount = segments;
        lr.widthMultiplier = width;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.sortingOrder = 32000;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            // Rings in plane perpendicular to local +Z (typical blade axis on Slice rigs).
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        Shader sh = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(sh);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.color = color;
        mat.renderQueue = 3100;
        lr.sharedMaterial = mat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }
}
