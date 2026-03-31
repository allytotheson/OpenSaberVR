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
    [Header("UDP Sabers (assign or will find by tag)")]
    public GameObject LeftSaber;
    public GameObject RightSaber;

    private void Awake()
    {
        SuppressVrtkSdkLoadWhenNoHeadset();
        GameplayCameraEnsurer.Ensure();
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
        DesktopSaberHandHalo.DestroyAllWorldHalos();
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

    /// <summary>Unloads <c>OpenSaber</c>, reloads <c>Menu</c> additively, stops gameplay audio, restores menu UI.</summary>
    public void ReturnToMenuFromGameplay()
    {
        StartCoroutine(ReturnToMenuFromGameplayRoutine());
    }

    IEnumerator ReturnToMenuFromGameplayRoutine()
    {
        DesktopSaberHandHalo.DestroyAllWorldHalos();

        var spawner = FindAnyObjectByType<NotesSpawner>();
        if (spawner != null)
        {
            spawner.enabled = false;
            var audio = spawner.GetComponent<AudioSource>();
            if (audio != null)
                audio.Stop();
        }

        if (IsSceneLoaded("OpenSaber"))
            yield return UnloadScene("OpenSaber");

        if (!IsSceneLoaded("Menu"))
            yield return LoadScene("Menu", LoadSceneMode.Additive);

        MenuSceneLoaded();

        var mainMenu = FindAnyObjectByType<MainMenu>();
        if (mainMenu != null)
            mainMenu.RestoreUiAfterLeavingGameplay();
    }
}
