using UnityEngine;

/// <summary>
/// Two additive-style rings around the blade mount (left = red, right = blue) for team-color halos.
/// </summary>
public static class DesktopSaberHandHalo
{
    public const string HaloRootName = "DesktopSaberHalo";

    /// <summary>World-space ring radii before compensating for tiny Slice lossy scale.</summary>
    public static float innerRingRadiusWorld = 0.14f;

    public static float outerRingRadiusWorld = 0.3f;

    public static float innerLineWidthWorld = 0.034f;

    public static float outerLineWidthWorld = 0.052f;

    public static void EnsureAtBladeMount(Transform mount, bool isLeft)
    {
        if (mount == null || GameplayCameraEnsurer.IsXrDeviceActive())
            return;

        RemoveAllHalosUnder(mount);

        var root = new GameObject(HaloRootName);
        root.transform.SetParent(mount, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Color core = isLeft ? new Color(1f, 0.12f, 0.22f, 0.98f) : new Color(0.12f, 0.48f, 1f, 0.98f);
        Color glow = isLeft ? new Color(1f, 0.4f, 0.5f, 0.62f) : new Color(0.38f, 0.72f, 1f, 0.62f);

        float u = UniformLossy(mount);
        float rIn = innerRingRadiusWorld / u;
        float rOut = outerRingRadiusWorld / u;
        float wIn = innerLineWidthWorld / u;
        float wOut = outerLineWidthWorld / u;

        BuildRing(root.transform, "HaloInner", rIn, wIn, core, 48);
        BuildRing(root.transform, "HaloOuter", rOut, wOut, glow, 64);
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
