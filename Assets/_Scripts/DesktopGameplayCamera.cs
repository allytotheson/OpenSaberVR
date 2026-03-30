using UnityEngine;

/// <summary>
/// Resolves the camera used for desktop saber alignment, hit-line guide, and proxy blade billboards.
/// Prefers SteamVR <see cref="SceneHandling.SteamVrCameraEyeName"/> (HMD eye) for the Game view.
/// </summary>
public static class DesktopGameplayCamera
{
    static Camera ResolveDesktopCamera()
    {
        Camera eye = null;
        Camera mainTagged = null;
        Camera anyActive = null;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c == null || !c.isActiveAndEnabled)
                continue;
            if (anyActive == null)
                anyActive = c;
            if (c.CompareTag("MainCamera") && mainTagged == null)
                mainTagged = c;
            if (c.gameObject.name == "Camera (eye)")
            {
                eye = c;
                break;
            }
        }
        if (eye != null) return eye;
        if (mainTagged != null) return mainTagged;
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;
        return anyActive;
    }

    public static bool TryGet(out Camera cam)
    {
        cam = ResolveDesktopCamera();
        return cam != null;
    }

    public static Transform TryGetTransform()
    {
        return TryGet(out Camera c) ? c.transform : null;
    }
}
