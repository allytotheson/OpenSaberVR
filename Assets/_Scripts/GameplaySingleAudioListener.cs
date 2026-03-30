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
        Camera cam = null;
        Camera anyActive = null;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c == null || !c.isActiveAndEnabled)
                continue;
            if (anyActive == null)
                anyActive = c;
            if (c.gameObject.name == "Camera (eye)")
            {
                cam = c;
                break;
            }
        }
        if (cam == null && Camera.main != null && Camera.main.isActiveAndEnabled)
            cam = Camera.main;
        if (cam == null)
            cam = anyActive;

        var camGo = cam != null ? cam.gameObject : null;
        if (cam != null && cam.isActiveAndEnabled)
        {
            var al = FindListenerOnOrNearCamera(camGo);
            if (al == null)
                al = cam.gameObject.AddComponent<AudioListener>();
            return al;
        }

        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled)
        {
            var al = FindListenerOnOrNearCamera(main.gameObject);
            if (al == null)
                al = main.gameObject.AddComponent<AudioListener>();
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
