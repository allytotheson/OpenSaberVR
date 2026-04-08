using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using VRTK;

/// <summary>
/// Scene loading and saber visibility. No XR/VR dependencies - uses UDP-driven sabers.
/// Assign LeftSaber and RightSaber (UDP saber GameObjects) or they will be found by tag.
/// Each must be the <b>hand root</b> that contains a child with <see cref="Slice"/> — desktop input moves this transform.
/// </summary>
public class SceneHandling : MonoBehaviour
{
    [Header("UDP Sabers (assign or will find by tag)")]
    [Tooltip("Root object that is parent of the Slice (blade) object. Not a separate mesh sibling to BladeProxy.")]
    public GameObject LeftSaber;
    [Tooltip("Root object that is parent of the Slice (blade) object. Not a separate mesh sibling to BladeProxy.")]
    public GameObject RightSaber;

    private void Awake()
    {
        SuppressVrtkSdkLoadWhenNoHeadset();
        GameplayCameraEnsurer.Ensure();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "OpenSaber")
            return;
        var sh = Object.FindAnyObjectByType<SceneHandling>();
        if (sh == null)
            return;
        sh.EnsureSaberRefs();
        sh.SaberSceneLoaded();
        // SaberMotionController components may not be attached yet at this callback
        // (SaberGameplayBootstrap adds them from NotesSpawner.Start on the same frame).
        // Do a second pass one frame later to catch anything that was just added.
        sh.StartCoroutine(sh.RefreshSabersNextFrame());
    }

    System.Collections.IEnumerator RefreshSabersNextFrame()
    {
        yield return null;  // wait one frame
        EnsureSaberRefs();
        ApplySaberRootsVisible();
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
        Scene openSaber = SceneManager.GetSceneByName("OpenSaber");
        bool openLoaded = openSaber.IsValid() && openSaber.isLoaded;

        if (openLoaded)
        {
            if (!IsLiveInstanceInScene(LeftSaber, openSaber))
                LeftSaber = null;
            if (!IsLiveInstanceInScene(RightSaber, openSaber))
                RightSaber = null;

            if (LeftSaber == null)
                LeftSaber = ResolveSaberRootInScene(openSaber, true);
            if (RightSaber == null)
                RightSaber = ResolveSaberRootInScene(openSaber, false);
        }
        else
        {
            if (LeftSaber != null && !IsLiveInstanceInAnyLoadedScene(LeftSaber))
                LeftSaber = null;
            if (RightSaber != null && !IsLiveInstanceInAnyLoadedScene(RightSaber))
                RightSaber = null;

            if (LeftSaber == null)
            {
                LeftSaber = GameObject.FindGameObjectWithTag("LeftSaber");
                if (LeftSaber == null)
                    LeftSaber = FindWithTagIncludingInactive("LeftSaber");
            }
            if (RightSaber == null)
            {
                RightSaber = GameObject.FindGameObjectWithTag("RightSaber");
                if (RightSaber == null)
                    RightSaber = FindWithTagIncludingInactive("RightSaber");
            }
        }
    }

    /// <summary>True for an object that exists in a loaded scene (excludes destroyed refs and project prefab assets).</summary>
    public static bool IsLiveInstanceInAnyLoadedScene(GameObject go)
    {
        return go != null && go.scene.IsValid() && go.scene.isLoaded;
    }

    static bool IsLiveInstanceInScene(GameObject go, Scene scene)
    {
        return go != null && go.scene.IsValid() && go.scene.isLoaded && go.scene == scene;
    }

    /// <summary>Left/right saber hand roots inside OpenSaber (tag, name, then <see cref="SaberMotionController"/>).</summary>
    public static GameObject ResolveSaberRootInScene(Scene scene, bool isLeft)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        string tag = isLeft ? "LeftSaber" : "RightSaber";
        GameObject found = FindWithTagInScene(scene, tag);
        if (found != null)
            return found;

        string byName = isLeft ? "LeftSaber" : "RightSaber";
        found = FindByNameInScene(scene, byName);
        if (found != null)
            return found;

        foreach (var smc in Object.FindObjectsByType<SaberMotionController>(FindObjectsInactive.Include))
        {
            if (smc == null || smc.gameObject.scene != scene)
                continue;
            bool matchLeft = smc.hand == SaberMotionController.SaberHand.Left;
            if (isLeft == matchLeft)
                return smc.gameObject;
        }

        return null;
    }

    public static GameObject FindWithTagInScene(Scene scene, string tag)
    {
        if (string.IsNullOrEmpty(tag) || !scene.IsValid() || !scene.isLoaded)
            return null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t != null && t.gameObject.scene == scene && t.CompareTag(tag))
                return t.gameObject;
        }
        return null;
    }

    public static GameObject FindByNameInScene(Scene scene, string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || !scene.IsValid() || !scene.isLoaded)
            return null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t != null && t.gameObject.scene == scene && t.name == objectName)
                return t.gameObject;
        }
        return null;
    }

    /// <summary><see cref="GameObject.FindGameObjectWithTag"/> skips inactive objects; saber roots are often disabled on the menu.</summary>
    public static GameObject FindWithTagIncludingInactive(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t != null && t.CompareTag(tag))
                return t.gameObject;
        }
        return null;
    }

    private void MenuSceneLoaded()
    {
        if (LeftSaber != null && !IsLiveInstanceInAnyLoadedScene(LeftSaber))
            LeftSaber = null;
        if (RightSaber != null && !IsLiveInstanceInAnyLoadedScene(RightSaber))
            RightSaber = null;

        DesktopSaberHandHalo.DestroyAllWorldHalos();
        if (LeftSaber != null) LeftSaber.SetActive(false);
        if (RightSaber != null) RightSaber.SetActive(false);
    }

    private void SaberSceneLoaded()
    {
        EnsureSaberRefs();
        ApplySaberRootsVisible();
    }

    /// <summary>Re-resolve refs and show sabers after <see cref="SaberGameplayBootstrap"/> wires Slice/hands.</summary>
    public void RefreshGameplaySabers()
    {
        EnsureSaberRefs();
        ApplySaberRootsVisible();
    }

    void ApplySaberRootsVisible()
    {
        if (LeftSaber != null)
        {
            EnsureAncestorsActive(LeftSaber.transform);
            LeftSaber.SetActive(true);
        }
        if (RightSaber != null)
        {
            EnsureAncestorsActive(RightSaber.transform);
            RightSaber.SetActive(true);
        }
    }

    /// <summary>SteamVR/VRTK often disable controller branches; children stay inactive even if the saber GO is set active.</summary>
    public static void EnsureAncestorsActive(Transform leaf)
    {
        if (leaf == null) return;
        var chain = new System.Collections.Generic.List<Transform>();
        for (Transform t = leaf; t != null; t = t.parent)
            chain.Add(t);
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (!chain[i].gameObject.activeSelf)
                chain[i].gameObject.SetActive(true);
        }
    }

    internal IEnumerator LoadScene(string sceneName, LoadSceneMode mode)
    {
        if (sceneName == "Menu")
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
        return scene.IsValid() && scene.isLoaded;
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

        // Unload ALL loaded copies of OpenSaber.
        while (IsSceneLoaded("OpenSaber"))
            yield return UnloadScene("OpenSaber");

        while (IsSceneLoaded("Results"))
            yield return UnloadScene("Results");

        if (!IsSceneLoaded("Menu"))
            yield return LoadScene("Menu", LoadSceneMode.Additive);

        MenuSceneLoaded();

        var mainMenu = FindAnyObjectByType<MainMenu>();
        if (mainMenu != null)
            mainMenu.RestoreUiAfterLeavingGameplay();
    }
}
