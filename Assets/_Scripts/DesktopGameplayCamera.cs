using UnityEngine;

/// <summary>
/// Resolves the camera used for desktop saber alignment, hit-line guide, and proxy blade billboards.
/// Delegates to <see cref="GameplayCameraEnsurer"/> so naming matches SteamVR / Oculus / OpenXR rigs.
/// </summary>
public static class DesktopGameplayCamera
{
    public static bool TryGet(out Camera cam)
    {
        return GameplayCameraEnsurer.TryGetPreferredCamera(out cam);
    }

    public static Transform TryGetTransform()
    {
        return TryGet(out Camera c) ? c.transform : null;
    }
}
