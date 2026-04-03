using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires desktop gameplay: <see cref="SwingDetector"/> + <see cref="DemonHitDetector"/> on blade objects
/// (with <see cref="Slice"/>), tags hand roots, fills <see cref="SceneHandling"/> refs, and adds
/// <see cref="DesktopSaberTestInput"/> when not in XR. Safe to call multiple times.
/// </summary>
public static class SaberGameplayBootstrap
{
    public static void EnsureAfterGameplayLoad()
    {
        Scene open = SceneManager.GetSceneByName("OpenSaber");
        bool openLoaded = open.IsValid() && open.isLoaded;

        Slice[] slices;
        GameObject leftHand;
        GameObject rightHand;

        if (openLoaded)
        {
            GameObject lRoot = SceneHandling.ResolveSaberRootInScene(open, true);
            GameObject rRoot = SceneHandling.ResolveSaberRootInScene(open, false);
            EnsureSliceOnPrimaryBlade(lRoot);
            EnsureSliceOnPrimaryBlade(rRoot);

            slices = ListSlicesInScene(open);
            if (slices.Length < 2)
                return;

            leftHand = SceneHandling.ResolveSaberRootInScene(open, true);
            rightHand = SceneHandling.ResolveSaberRootInScene(open, false);
            if (leftHand == null || rightHand == null)
                return;
        }
        else
        {
            slices = Object.FindObjectsByType<Slice>(FindObjectsInactive.Include);
            if (slices == null || slices.Length == 0)
                return;
            if (!TryBuildHandsFromSliceParents(slices, out leftHand, out rightHand))
                return;
        }

        TrySetTag(leftHand, "LeftSaber");
        TrySetTag(rightHand, "RightSaber");

        foreach (var s in slices)
        {
            if (s == null || s.transform.parent == null) continue;
            GameObject blade = s.gameObject;
            RemoveDuplicateComponents<SwingDetector>(blade);
            RemoveDuplicateComponents<DemonHitDetector>(blade);
            RemoveDuplicateComponents<DesktopSaberBladeVisual>(blade);

            if (blade.GetComponent<SwingDetector>() == null)
                blade.AddComponent<SwingDetector>();
            if (blade.GetComponent<DemonHitDetector>() == null)
                blade.AddComponent<DemonHitDetector>();

#if UNITY_EDITOR || UNITY_STANDALONE
            if (!GameplayCameraEnsurer.IsXrDeviceActive())
            {
                var sw = blade.GetComponent<SwingDetector>();
                if (sw != null)
                    sw.minSwingVelocity = Mathf.Min(sw.minSwingVelocity, 0.85f);
            }
#endif
        }

        var sh = Object.FindAnyObjectByType<SceneHandling>();
        if (sh != null)
        {
            sh.LeftSaber = leftHand;
            sh.RightSaber = rightHand;
            sh.RefreshGameplaySabers();
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (!GameplayCameraEnsurer.IsXrDeviceActive())
        {
            foreach (var s in slices)
            {
                if (s == null) continue;
                if (s.GetComponent<DesktopSaberBladeVisual>() == null)
                    s.gameObject.AddComponent<DesktopSaberBladeVisual>();
            }

            var spawner = Object.FindAnyObjectByType<NotesSpawner>();
            if (spawner != null)
            {
                RemoveDuplicateComponents<DesktopSaberTestInput>(spawner.gameObject);
                RemoveDuplicateComponents<DesktopCameraMountSaberVisual>(spawner.gameObject);
                RemoveDuplicateComponents<DesktopAutoSliceHits>(spawner.gameObject);
                RemoveDuplicateComponents<UdpSelectToSwingBridge>(spawner.gameObject);
            }

            if (spawner != null && spawner.GetComponent<DesktopSaberTestInput>() == null)
                spawner.gameObject.AddComponent<DesktopSaberTestInput>();

            if (spawner != null)
                DesktopImportedBladeMount.AttachFromNotesSpawner(spawner);

            if (spawner != null && spawner.GetComponent<DesktopCameraMountSaberVisual>() == null)
                spawner.gameObject.AddComponent<DesktopCameraMountSaberVisual>();

            if (spawner != null && spawner.GetComponent<DesktopAutoSliceHits>() == null)
                spawner.gameObject.AddComponent<DesktopAutoSliceHits>();

            if (spawner != null && Object.FindAnyObjectByType<UDPSaberReceiver>() != null &&
                spawner.GetComponent<UdpSelectToSwingBridge>() == null)
                spawner.gameObject.AddComponent<UdpSelectToSwingBridge>();
        }
#endif
    }

    static Slice[] ListSlicesInScene(Scene scene)
    {
        var list = new List<Slice>();
        foreach (var s in Object.FindObjectsByType<Slice>(FindObjectsInactive.Include))
        {
            if (s != null && s.gameObject.scene == scene)
                list.Add(s);
        }
        return list.ToArray();
    }

    static bool TryBuildHandsFromSliceParents(Slice[] slices, out GameObject leftHand, out GameObject rightHand)
    {
        leftHand = rightHand = null;
        var roots = new List<Transform>();
        foreach (var s in slices)
        {
            if (s == null) continue;
            Transform hand = s.transform.parent;
            if (hand == null) continue;
            bool dup = false;
            foreach (var r in roots)
            {
                if (r == hand) { dup = true; break; }
            }
            if (!dup) roots.Add(hand);
        }

        if (roots.Count < 2)
            return false;

        roots.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        leftHand = roots[0].gameObject;
        rightHand = roots[roots.Count - 1].gameObject;
        return true;
    }

    /// <summary>Adds <see cref="Slice"/> on the same object as the first <see cref="Renderer"/> under the hand if missing.</summary>
    static void EnsureSliceOnPrimaryBlade(GameObject handRoot)
    {
        if (handRoot == null)
            return;
        if (handRoot.GetComponentInChildren<Slice>(true) != null)
            return;

        var r = handRoot.GetComponentInChildren<Renderer>(true);
        if (r == null)
            return;
        if (r.gameObject.GetComponent<Slice>() == null)
            r.gameObject.AddComponent<Slice>();
    }

    static void RemoveDuplicateComponents<T>(GameObject go) where T : Component
    {
        if (go == null)
            return;
        var arr = go.GetComponents<T>();
        if (arr == null || arr.Length <= 1)
            return;
        for (int i = 1; i < arr.Length; i++)
        {
            if (arr[i] != null)
                Object.Destroy(arr[i]);
        }
    }

    static void TrySetTag(GameObject go, string tag)
    {
        try
        {
            go.tag = tag;
        }
        catch
        {
            /* Tag missing in project — ignore */
        }
    }
}
