using UnityEngine;

/// <summary>
/// Wires desktop gameplay: <see cref="SwingDetector"/> + <see cref="DemonHitDetector"/> on blade objects
/// (with <see cref="Slice"/>), tags hand roots, fills <see cref="SceneHandling"/> refs, and adds
/// <see cref="DesktopSaberTestInput"/> when not in XR. Safe to call multiple times.
/// </summary>
public static class SaberGameplayBootstrap
{
    public static void EnsureAfterGameplayLoad()
    {
        var slices = Object.FindObjectsByType<Slice>(FindObjectsInactive.Include);
        if (slices == null || slices.Length == 0)
            return;

        var roots = new System.Collections.Generic.List<Transform>();
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
            return;

        roots.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        GameObject leftHand = roots[0].gameObject;
        GameObject rightHand = roots[roots.Count - 1].gameObject;

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
            if (sh.LeftSaber == null) sh.LeftSaber = leftHand;
            if (sh.RightSaber == null) sh.RightSaber = rightHand;
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
