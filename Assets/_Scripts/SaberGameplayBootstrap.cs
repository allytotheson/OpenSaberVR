using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires gameplay components on blade objects: <see cref="SwingDetector"/> + <see cref="DemonHitDetector"/>
/// (all platforms, including VR), tags hand roots, fills <see cref="SceneHandling"/> refs, ensures
/// <see cref="UDPSaberReceiver"/> + <see cref="SaberMotionController"/> + <see cref="ImuSaberInputProvider"/>
/// so IMU data from Pico W drives saber transforms. Safe to call multiple times.
/// </summary>
public static class SaberGameplayBootstrap
{
    public static void EnsureAfterGameplayLoad()
    {
        EnsureUdpReceiver();
        EnsureSerialReceiver();

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

        EnsureImuInputOnHand(leftHand, SaberMotionController.SaberHand.Left);
        EnsureImuInputOnHand(rightHand, SaberMotionController.SaberHand.Right);

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
            var spawner = Object.FindAnyObjectByType<NotesSpawner>();
            if (spawner != null)
                RemoveDuplicateComponents<UdpSelectToSwingBridge>(spawner.gameObject);

            bool hasImuSource = Object.FindAnyObjectByType<UDPSaberReceiver>() != null ||
                                Object.FindAnyObjectByType<SerialSaberReceiver>() != null;

            if (spawner != null && hasImuSource &&
                spawner.GetComponent<UdpSelectToSwingBridge>() == null)
                spawner.gameObject.AddComponent<UdpSelectToSwingBridge>();

            if (spawner != null && spawner.GetComponent<DirectedDesktopSliceInput>() == null)
                spawner.gameObject.AddComponent<DirectedDesktopSliceInput>();

            if (spawner != null && spawner.GetComponent<DesktopSaberVisualHider>() == null)
                spawner.gameObject.AddComponent<DesktopSaberVisualHider>();
        }
#endif
    }

    /// <summary>
    /// Creates a persistent <see cref="UDPSaberReceiver"/> if none exists in any loaded scene.
    /// Call from calibration (before OpenSaber) so <see cref="ImuSourceResolver"/> sees UDP during IMU calibration.
    /// This allows Pico W controllers to send IMU data over UDP even when only
    /// <see cref="SerialSaberReceiver"/> was placed in the scene.
    /// </summary>
    public static void EnsureUdpReceiver()
    {
        if (Object.FindAnyObjectByType<UDPSaberReceiver>() != null)
            return;

        var go = new GameObject("[UDPSaberReceiver]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<UDPSaberReceiver>();
        Debug.Log("[SaberGameplayBootstrap] Created persistent UDPSaberReceiver (ports 5000/5001) for Pico W IMU input.");
    }

    /// <summary>
    /// Creates a persistent <see cref="SerialSaberReceiver"/> (COM6 left, COM7 right) if none
    /// exists. Serial takes priority over UDP in <see cref="ImuSourceResolver"/> when a port is open.
    /// </summary>
    public static void EnsureSerialReceiver()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (Object.FindAnyObjectByType<SerialSaberReceiver>() != null)
            return;

        var go = new GameObject("[SerialSaberReceiver]");
        Object.DontDestroyOnLoad(go);
        var serial = go.AddComponent<SerialSaberReceiver>();
        serial.leftSaberComPort = "COM6";
        serial.rightSaberComPort = "COM7";
        Debug.Log("[SaberGameplayBootstrap] Created persistent SerialSaberReceiver (COM6 left, COM7 right).");
#endif
    }

    /// <summary>
    /// Adds <see cref="SaberMotionController"/>, <see cref="ImuSaberInputProvider"/>, and
    /// <see cref="DesktopSaberInputProvider"/> on a saber hand root so the priority system
    /// (IMU 50 > Desktop 10) drives the transform from real controller data when available.
    /// </summary>
    static void EnsureImuInputOnHand(GameObject hand, SaberMotionController.SaberHand saberHand)
    {
        if (hand == null)
            return;

        // ImuCalibrationController must only live in the Calibration scene.
        // Remove it from saber hand roots to prevent broken Skip flows and duplicate UIs.
        foreach (var cal in hand.GetComponents<ImuCalibrationController>())
        {
            Debug.LogWarning($"[SaberGameplayBootstrap] Removing ImuCalibrationController from '{hand.name}' " +
                             $"— it belongs in the Calibration scene, not on a saber object.");
            Object.Destroy(cal);
        }

        var mc = hand.GetComponent<SaberMotionController>();
        if (mc == null)
        {
            mc = hand.AddComponent<SaberMotionController>();
            mc.hand = saberHand;
        }

        if (hand.GetComponent<ImuSaberInputProvider>() == null)
            hand.AddComponent<ImuSaberInputProvider>();

#if UNITY_EDITOR || UNITY_STANDALONE
        if (!GameplayCameraEnsurer.IsXrDeviceActive())
        {
            if (hand.GetComponent<DesktopSaberInputProvider>() == null)
                hand.AddComponent<DesktopSaberInputProvider>();
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
