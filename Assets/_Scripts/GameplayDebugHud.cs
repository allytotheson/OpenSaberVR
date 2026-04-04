using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Screen-space EXIT bar shown during OpenSaber gameplay. Developer panel and auto-slice have been removed.
/// </summary>
public class GameplayDebugHud : MonoBehaviour
{
    /// <summary>Always false — auto-slice debug feature removed. Kept as a compile-time constant so existing
    /// call sites in DemonHitDetector compile without changes.</summary>
    public static bool AutoSliceNotes => false;

    /// <summary>Kept for API compatibility; not driven by a developer panel anymore.</summary>
    public static bool AutoAlignSabersToNotes = true;

    GameObject _exitBar;

    public static void EnsureCreated(Transform parent)
    {
        if (parent == null)
            return;
        if (FindAnyObjectByType<GameplayDebugHud>(FindObjectsInactive.Include) != null)
            return;
        var root = new GameObject(nameof(GameplayDebugHud));
        root.transform.SetParent(parent, false);
        root.AddComponent<GameplayDebugHud>();
    }

    void Awake()
    {
        var canvasGo = MenuExitScreenHud.CreateOverlayCanvasRoot(transform, "GameplayHudCanvas");
        Font font = MenuExitScreenHud.ResolveMenuFont();
        _exitBar = MenuExitScreenHud.CreateExitBar(canvasGo.transform, font, OnGameplayExitClicked);
        _exitBar.SetActive(false);
    }

    void Update()
    {
        var openSaber = SceneManager.GetSceneByName("OpenSaber");
        bool inGameplay = openSaber.IsValid() && openSaber.isLoaded;
        if (_exitBar != null)
            _exitBar.SetActive(inGameplay);
    }

    void OnGameplayExitClicked()
    {
        var sh = FindAnyObjectByType<SceneHandling>();
        if (sh != null)
            sh.ReturnToMenuFromGameplay();
    }
}
