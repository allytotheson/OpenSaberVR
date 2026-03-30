using UnityEngine;

/// <summary>
/// Parents an FBX/GLB sword (or a colored capsule) under each desktop saber’s <see cref="Slice"/> transform.
/// Called from <see cref="NotesSpawner"/> after <see cref="SaberGameplayBootstrap"/>.
/// </summary>
public static class DesktopImportedBladeMount
{
    public const string ImportedBladeChildName = "ImportedSaberBlade";
    public const string CapsuleBladeChildName = "FallbackSaberBladeCapsule";

    public static void AttachFromNotesSpawner(NotesSpawner spawner)
    {
        if (spawner == null || GameplayCameraEnsurer.IsXrDeviceActive())
            return;

        GameObject prefab = spawner.importedSwordVisualPrefab != null
            ? spawner.importedSwordVisualPrefab
            : NotesSpawner.TryLoadImportedSwordFromResources();

        if (!TryResolveSabers(out GameObject leftGo, out GameObject rightGo))
            return;

        if (leftGo != null)
            EnsureBladeForHand(leftGo, true, prefab, spawner);
        if (rightGo != null)
            EnsureBladeForHand(rightGo, false, prefab, spawner);
    }

    static void EnsureBladeForHand(GameObject saberHandRoot, bool isLeft, GameObject importedPrefab, NotesSpawner cfg)
    {
        if (FindBladeChildRecursive(saberHandRoot.transform, ImportedBladeChildName) != null ||
            FindBladeChildRecursive(saberHandRoot.transform, CapsuleBladeChildName) != null)
            return;

        DestroyLegacyDevBlades(saberHandRoot.transform);

        Transform mount = GetBladeMountTransform(saberHandRoot);

        if (importedPrefab != null && cfg.useImportedModelWhenAssigned)
        {
            GameObject inst = Object.Instantiate(importedPrefab, mount, false);
            inst.name = ImportedBladeChildName;
            inst.transform.localPosition = cfg.importedBladeLocalPosition;
            inst.transform.localRotation = Quaternion.Euler(cfg.importedBladeLocalEuler);
            inst.transform.localScale = cfg.importedBladeLocalScale;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
            return;
        }

        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.name = CapsuleBladeChildName;
        Object.Destroy(cap.GetComponent<Collider>());
        cap.transform.SetParent(mount, false);
        cap.transform.localPosition = cfg.fallbackCapsuleLocalOffset;
        cap.transform.localRotation = Quaternion.identity;
        cap.transform.localScale = cfg.fallbackCapsuleLocalScale;

        var r = cap.GetComponent<Renderer>();
        if (r != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color")
                        ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                var m = new Material(sh);
                Color c = isLeft ? new Color(1f, 0.25f, 0.35f, 0.92f) : new Color(0.2f, 0.55f, 1f, 0.92f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                r.sharedMaterial = m;
            }
        }
    }

    static void DestroyLegacyDevBlades(Transform handRoot)
    {
        foreach (var t in handRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            string n = t.name;
            if (n == "DeveloperCustomBlade" || n == "DeveloperDebugBlade")
                Object.Destroy(t.gameObject);
        }
    }

    public static Transform FindBladeChildRecursive(Transform handRoot, string childName)
    {
        foreach (var t in handRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
                return t;
        }
        return null;
    }

    static Transform GetBladeMountTransform(GameObject saberHandRoot)
    {
        var slice = saberHandRoot.GetComponentInChildren<Slice>(true);
        return slice != null ? slice.transform : saberHandRoot.transform;
    }

    static bool TryResolveSabers(out GameObject left, out GameObject right)
    {
        left = null;
        right = null;
        var sceneHandling = Object.FindAnyObjectByType<SceneHandling>();
        if (sceneHandling != null)
        {
            left = sceneHandling.LeftSaber;
            right = sceneHandling.RightSaber;
        }
        if (left == null)
            left = GameObject.FindGameObjectWithTag("LeftSaber");
        if (right == null)
            right = GameObject.FindGameObjectWithTag("RightSaber");
        if (left == null || right == null)
        {
            foreach (var slice in Object.FindObjectsByType<Slice>(FindObjectsInactive.Include))
            {
                Transform hand = slice.transform.parent;
                if (hand == null) continue;
                if (left == null && hand.CompareTag("LeftSaber"))
                    left = hand.gameObject;
                if (right == null && hand.CompareTag("RightSaber"))
                    right = hand.gameObject;
            }
        }
        return left != null || right != null;
    }
}
