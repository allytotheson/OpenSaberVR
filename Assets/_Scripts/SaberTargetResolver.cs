using UnityEngine;

/// <summary>
/// Shared demon / color queries for saber alignment and hit logic.
/// </summary>
public static class SaberTargetResolver
{
    public static bool IsRedDemon(DemonHandling d, Transform spawner)
    {
        if (d == null) return true;
        foreach (var r in d.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null) continue;
            string n = m.name.ToLowerInvariant();
            if (n.Contains("red"))
                return true;
            if (n.Contains("blue"))
                return false;
        }

        if (spawner != null)
            return d.transform.position.x < spawner.position.x;
        return d.transform.position.x <= 0f;
    }
}
