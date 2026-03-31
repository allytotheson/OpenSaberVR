using UnityEngine;

/// <summary>
/// Lightweight procedural burst when <see cref="DeveloperGameplayMode"/> is enabled.
/// </summary>
public static class DeveloperHitFeedback
{
    public static void SpawnBurst(Vector3 position, Color tint)
    {
        if (!DeveloperGameplayMode.Enabled)
            return;

        const int n = 10;
        for (int i = 0; i < n; i++)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            p.transform.position = position;
            p.transform.localScale = Vector3.one * Random.Range(0.04f, 0.09f);
            Object.Destroy(p.GetComponent<Collider>());
            var r = p.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(RenderingShaderUtil.UnlitForWorldMeshes());
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
                r.sharedMaterial = m;
            }
            var rb = p.AddComponent<Rigidbody>();
            rb.linearVelocity = Random.insideUnitSphere * 2.2f;
            Object.Destroy(p, 0.45f);
        }
    }

    public static Color ApproximateDemonTint(Transform root)
    {
        if (DeveloperGameplayMode.Instance != null)
            return DeveloperGameplayMode.Instance.hitBurstColor;
        return new Color(0.5f, 0.95f, 1f, 1f);
    }
}
