using UnityEngine;

/// <summary>
/// Desktop: mirrors <see cref="NotesSpawner.hideDesktopSaberVisuals"/> onto blade components and disables
/// <see cref="Renderer"/>s under saber hand roots (meshes, halos, auras). Runs before
/// <see cref="DesktopSaberBladeVisual"/> / <see cref="DesktopCameraMountSaberVisual"/> so flags apply the same frame.
/// </summary>
[DefaultExecutionOrder(385)]
[DisallowMultipleComponent]
public sealed class DesktopSaberVisualHider : MonoBehaviour
{
    NotesSpawner _spawner;

    void Awake()
    {
        _spawner = GetComponent<NotesSpawner>();
        if (_spawner == null)
            _spawner = FindAnyObjectByType<NotesSpawner>();
    }

    void LateUpdate()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (GameplayCameraEnsurer.IsXrDeviceActive())
            return;

        bool hide = _spawner != null && _spawner.hideDesktopSaberVisuals;

        foreach (var m in Object.FindObjectsByType<DesktopCameraMountSaberVisual>(FindObjectsInactive.Include))
        {
            if (m != null)
                m.hideBladeVisual = hide;
        }

        foreach (var b in Object.FindObjectsByType<DesktopSaberBladeVisual>(FindObjectsInactive.Include))
        {
            if (b != null)
                b.hideBladeProxy = hide;
        }

        ApplyHandRenderers(hide);
#endif
    }

    static void ApplyHandRenderers(bool hide)
    {
        var sh = Object.FindAnyObjectByType<SceneHandling>();
        SetHandHidden(hide, sh != null ? sh.LeftSaber : null);
        SetHandHidden(hide, sh != null ? sh.RightSaber : null);
    }

    static void SetHandHidden(bool hide, GameObject handRoot)
    {
        if (handRoot == null)
            return;
        foreach (var r in handRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null)
                r.enabled = !hide;
        }
    }
}
