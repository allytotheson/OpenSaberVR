using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using VRTK;

/// <summary>
/// Scene loading and saber visibility. No XR/VR dependencies - uses UDP-driven sabers.
/// Assign LeftSaber and RightSaber (UDP saber GameObjects) or they will be found by tag.
/// </summary>
public class SceneHandling : MonoBehaviour
{
    const string DesktopFallbackCameraName = "FallbackCamera_NonVR";
    [Header("UDP Sabers (assign or will find by tag)")]
    public GameObject LeftSaber;
    public GameObject RightSaber;

    private void Awake()
    {
        SuppressVrtkSdkLoadWhenNoHeadset();
        EnsureDesktopFallbackCameraIfNoActiveCamera();
    }

    private static void SuppressVrtkSdkLoadWhenNoHeadset()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        var mgr = Object.FindAnyObjectByType<VRTK_SDKManager>();
        if (mgr == null)
            return;
        string device = string.IsNullOrEmpty(XRSettings.loadedDeviceName) ? "None" : XRSettings.loadedDeviceName;
        if (device == "None")
            mgr.autoLoadSetup = false;
#endif
    }

    private static void EnsureDesktopFallbackCameraIfNoActiveCamera()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        string device = string.IsNullOrEmpty(XRSettings.loadedDeviceName) ? "None" : XRSettings.loadedDeviceName;
        if (device != "None")
            return;

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c != null && c.isActiveAndEnabled)
                return;
        }

        var existing = GameObject.Find(DesktopFallbackCameraName);
        var go = existing != null ? existing : new GameObject(DesktopFallbackCameraName);
        go.tag = "MainCamera";
        if (go.GetComponent<Camera>() == null)
            go.AddComponent<Camera>();
        if (go.GetComponent<AudioListener>() == null)
            go.AddComponent<AudioListener>();
        var cam = go.GetComponent<Camera>();
        cam.enabled = true;
        go.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.Euler(5f, 0f, 0f));
#endif
    }

    private void Start()
    {
        if (!IsSceneLoaded("Menu"))
            StartCoroutine(LoadScene("Menu", LoadSceneMode.Additive));

        EnsureSaberRefs();
        MenuSceneLoaded();
    }

    private void EnsureSaberRefs()
    {
        if (LeftSaber == null)
        {
            var left = GameObject.FindGameObjectWithTag("LeftSaber");
            if (left != null) LeftSaber = left;
        }
        if (RightSaber == null)
        {
            var right = GameObject.FindGameObjectWithTag("RightSaber");
            if (right != null) RightSaber = right;
        }
    }

    private void MenuSceneLoaded()
    {
        if (LeftSaber != null) LeftSaber.SetActive(false);
        if (RightSaber != null) RightSaber.SetActive(false);
    }

    private void SaberSceneLoaded()
    {
        EnsureSaberRefs();
        if (LeftSaber != null) LeftSaber.SetActive(true);
        if (RightSaber != null) RightSaber.SetActive(true);
    }

    internal IEnumerator LoadScene(string sceneName, LoadSceneMode mode)
    {
        if (sceneName == "OpenSaber")
            SaberSceneLoaded();
        else if (sceneName == "Menu")
            MenuSceneLoaded();

        yield return SceneManager.LoadSceneAsync(sceneName, mode);
    }

    internal IEnumerator UnloadScene(string sceneName)
    {
        yield return SceneManager.UnloadSceneAsync(sceneName);
    }

    internal bool IsSceneLoaded(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        return scene.name != null;
    }
}
