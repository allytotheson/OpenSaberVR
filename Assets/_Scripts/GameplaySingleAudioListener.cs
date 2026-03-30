using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Desktop + SteamVR rigs often leave multiple <see cref="AudioListener"/> components enabled.
/// Keeps exactly one enabled: on the desktop gameplay camera when available, otherwise <see cref="Camera.main"/>.
/// </summary>
[DefaultExecutionOrder(500)]
public sealed class GameplaySingleAudioListener : MonoBehaviour
{
    const int LateUpdateInterval = 45;
    int _frame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<GameplaySingleAudioListener>(FindObjectsInactive.Include) != null)
            return;
        var host = new GameObject(nameof(GameplaySingleAudioListener));
        DontDestroyOnLoad(host);
        host.AddComponent<GameplaySingleAudioListener>();
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnEnable()
    {
        Enforce();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Enforce();
    }

    void LateUpdate()
    {
        if ((++_frame % LateUpdateInterval) != 0)
            return;
        Enforce();
    }

    public static void Enforce()
    {
        GameplayCameraEnsurer.Ensure();
        AudioListener keep = ResolvePreferredListener();
        if (keep == null)
            return;

        keep.enabled = true;
        foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (al != null && al != keep)
                al.enabled = false;
        }
    }

    static AudioListener FindListenerOnOrNearCamera(GameObject camGo)
    {
        if (camGo == null) return null;
        var al = camGo.GetComponent<AudioListener>();
        if (al != null) return al;
        al = camGo.GetComponentInChildren<AudioListener>(true);
        if (al != null) return al;
        return camGo.GetComponentInParent<AudioListener>();
    }

    static AudioListener ResolvePreferredListener()
    {
        if (GameplayCameraEnsurer.TryGetPreferredCamera(out Camera cam))
        {
            var al = FindListenerOnOrNearCamera(cam.gameObject);
            if (al == null)
                al = cam.gameObject.AddComponent<AudioListener>();
            return al;
        }

        foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (al != null && al.gameObject.activeInHierarchy)
                return al;
        }

        return null;
    }
}
