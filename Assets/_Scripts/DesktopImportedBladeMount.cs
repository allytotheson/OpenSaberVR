using System.Collections.Generic;
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
        DeduplicateNamedBladesUnderHand(saberHandRoot.transform, ImportedBladeChildName);
        DeduplicateNamedBladesUnderHand(saberHandRoot.transform, CapsuleBladeChildName);

        Transform mountEarly = GetBladeMountTransform(saberHandRoot);

        Transform importedExisting = FindBladeChildRecursive(saberHandRoot.transform, ImportedBladeChildName);
        Transform capsuleExisting = FindBladeChildRecursive(saberHandRoot.transform, CapsuleBladeChildName);
        if (importedExisting != null && !RendererWorldMaxExtentTooSmall(importedExisting))
        {
            SetDesktopBillboardBladeVisible(mountEarly, false);
            EnsureSlashFx(importedExisting.gameObject, isLeft);
            ApplyImportedTeamLook(importedExisting, isLeft, cfg);
            EnsureImportedBladeAura(importedExisting.gameObject, isLeft, cfg);
            EnsureImportedBladeEmissionPulse(importedExisting.gameObject, cfg);
            MaybeRingHalo(mountEarly, isLeft, cfg, true);
            return;
        }

        if (capsuleExisting != null && !RendererWorldMaxExtentTooSmall(capsuleExisting))
        {
            SetDesktopBillboardBladeVisible(mountEarly, true);
            MaybeRingHalo(mountEarly, isLeft, cfg, false);
            return;
        }
        if (importedExisting != null) Object.Destroy(importedExisting.gameObject);
        if (capsuleExisting != null) Object.Destroy(capsuleExisting.gameObject);

        DestroyLegacyDevBlades(saberHandRoot.transform);

        Transform mount = GetBladeMountTransform(saberHandRoot);

        Vector3 lossy = mount.lossyScale;

        if (importedPrefab != null && cfg.useImportedModelWhenAssigned)
        {
            GameObject inst = Object.Instantiate(importedPrefab, mount, false);
            inst.name = ImportedBladeChildName;
            inst.transform.localPosition = cfg.importedBladeLocalPosition;
            inst.transform.localRotation = Quaternion.Euler(cfg.importedBladeLocalEuler);
            inst.transform.localScale = DivideByLossy(cfg.importedBladeLocalScale, lossy);
            BoostImportedModelScaleIfNearlyInvisible(inst.transform);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            foreach (var rend in inst.GetComponentsInChildren<Renderer>(true))
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            SetDesktopBillboardBladeVisible(mount, false);
            ApplyImportedTeamLook(inst.transform, isLeft, cfg);
            EnsureImportedBladeAura(inst, isLeft, cfg);
            EnsureImportedBladeEmissionPulse(inst, cfg);
            MaybeRingHalo(mount, isLeft, cfg, true);
            return;
        }

        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.name = CapsuleBladeChildName;
        Object.Destroy(cap.GetComponent<Collider>());
        cap.transform.SetParent(mount, false);
        cap.transform.localPosition = cfg.fallbackCapsuleLocalOffset;
        cap.transform.localRotation = Quaternion.identity;
        cap.transform.localScale = DivideByLossy(cfg.fallbackCapsuleLocalScale, lossy);

        var r = cap.GetComponent<Renderer>();
        if (r != null)
        {
            Shader sh = RenderingShaderUtil.UnlitForWorldMeshes();
            if (sh != null)
            {
                var m = new Material(sh);
                Color c = isLeft ? new Color(1f, 0.25f, 0.35f, 0.92f) : new Color(0.2f, 0.55f, 1f, 0.92f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                r.sharedMaterial = m;
            }
        }

        SetDesktopBillboardBladeVisible(mount, true);
        MaybeRingHalo(mount, isLeft, cfg, false);
    }

    static void MaybeRingHalo(Transform mount, bool isLeft, NotesSpawner cfg, bool importedMesh)
    {
        if (cfg == null || !cfg.showDesktopRingHalos)
            return;
        if (importedMesh)
            DesktopSaberHandHalo.EnsureAtBladeMount(mount, isLeft, cfg.importedHaloLocalEuler, cfg.importedHaloLocalPosition);
        else
            DesktopSaberHandHalo.EnsureAtBladeMount(mount, isLeft);
    }

    static void ApplyImportedTeamLook(Transform bladeRoot, bool isLeft, NotesSpawner cfg)
    {
        if (cfg == null || !cfg.applyImportedBladeTeamTintAndGlow || bladeRoot == null)
            return;
        EnsureRuntimeMaterialClones(bladeRoot);
        var rends = bladeRoot.GetComponentsInChildren<Renderer>(true);
        DesktopImportedBladeTeamTint.ApplyToRenderers(rends, isLeft, cfg.importedBladeTeamTintMix, cfg.importedBladeEmissionIntensity);
    }

    static void EnsureImportedBladeAura(GameObject importedRoot, bool isLeft, NotesSpawner cfg)
    {
        if (cfg == null || !cfg.applyImportedBladeTeamTintAndGlow || importedRoot == null || !cfg.showImportedBladeAdditiveAura)
            return;
        var aura = importedRoot.GetComponent<DesktopImportedBladeAura>();
        if (aura == null)
            aura = importedRoot.AddComponent<DesktopImportedBladeAura>();
        aura.Configure(isLeft, cfg.importedBladeAuraAlpha, cfg.importedBladeAuraWorldScale);
    }

    static void EnsureImportedBladeEmissionPulse(GameObject importedRoot, NotesSpawner cfg)
    {
        if (cfg == null || importedRoot == null || cfg.importedBladeEmissionPulseHz <= 0f)
            return;
        var pulse = importedRoot.GetComponent<DesktopImportedBladeEmissionPulse>();
        if (pulse == null)
            pulse = importedRoot.AddComponent<DesktopImportedBladeEmissionPulse>();
        pulse.frequencyHz = cfg.importedBladeEmissionPulseHz;
        pulse.depth = cfg.importedBladeEmissionPulseDepth;
        pulse.RebuildFromRenderers();
    }

    static void EnsureRuntimeMaterialClones(Transform bladeRoot)
    {
        if (bladeRoot == null || bladeRoot.GetComponent<DesktopImportedBladeRuntimeMaterials>() != null)
            return;
        foreach (var r in bladeRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null)
                continue;
            var shared = r.sharedMaterials;
            var inst = new Material[shared.Length];
            for (int j = 0; j < shared.Length; j++)
                inst[j] = shared[j] != null ? new Material(shared[j]) : null;
            r.materials = inst;
        }

        bladeRoot.gameObject.AddComponent<DesktopImportedBladeRuntimeMaterials>();
    }

    static void EnsureSlashFx(GameObject importedRoot, bool isLeft)
    {
        var fx = importedRoot.GetComponent<DesktopImportedBladeSlashFx>();
        if (fx == null)
            fx = importedRoot.AddComponent<DesktopImportedBladeSlashFx>();
        fx.Configure(isLeft);
    }

    static void SetDesktopBillboardBladeVisible(Transform mount, bool visible)
    {
        var dbv = mount.GetComponent<DesktopSaberBladeVisual>();
        if (dbv != null)
            dbv.enabled = visible;
    }

    static bool RendererWorldMaxExtentTooSmall(Transform bladeRoot)
    {
        var r = bladeRoot.GetComponentInChildren<Renderer>(true);
        if (r == null)
            return true;
        Vector3 e = r.bounds.extents;
        // FBX models often import in cm/mm — only treat as broken if almost invisible.
        return Mathf.Max(e.x, Mathf.Max(e.y, e.z)) < 0.006f;
    }

    static void BoostImportedModelScaleIfNearlyInvisible(Transform bladeRoot)
    {
        for (int i = 0; i < 4 && RendererWorldMaxExtentTooSmall(bladeRoot); i++)
            bladeRoot.localScale *= 3.5f;
    }

    static Vector3 DivideByLossy(Vector3 localDesired, Vector3 parentLossy)
    {
        return new Vector3(
            localDesired.x / Mathf.Max(Mathf.Abs(parentLossy.x), 0.02f),
            localDesired.y / Mathf.Max(Mathf.Abs(parentLossy.y), 0.02f),
            localDesired.z / Mathf.Max(Mathf.Abs(parentLossy.z), 0.02f));
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

    static void DeduplicateNamedBladesUnderHand(Transform handRoot, string childName)
    {
        bool keep = false;
        foreach (var t in handRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name != childName && !t.name.StartsWith(childName + "(", System.StringComparison.Ordinal))
                continue;
            if (!keep)
            {
                keep = true;
                continue;
            }

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

        // Fill missing hand from slice parents by world X (fixes only LeftSaber assigned in SceneHandling, etc.).
        if (left == null || right == null)
        {
            var parents = new List<Transform>();
            foreach (var slice in Object.FindObjectsByType<Slice>(FindObjectsInactive.Include))
            {
                Transform p = slice.transform.parent;
                if (p == null) continue;
                bool dup = false;
                foreach (var q in parents)
                {
                    if (q == p) { dup = true; break; }
                }
                if (!dup) parents.Add(p);
            }

            if (parents.Count >= 2)
            {
                parents.Sort((a, b) => a.position.x.CompareTo(b.position.x));
                if (left == null)
                    left = parents[0].gameObject;
                if (right == null)
                    right = parents[parents.Count - 1].gameObject;
            }
            else if (parents.Count == 1)
            {
                if (left == null)
                    left = parents[0].gameObject;
                else if (right == null)
                    right = parents[0].gameObject;
            }
        }

        return left != null || right != null;
    }
}
