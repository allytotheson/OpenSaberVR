using UnityEngine;

/// <summary>
/// When <see cref="GameplayDebugHud.AutoSliceNotes"/> is on, pulses the matching saber as each note crosses the hit plane
/// so <see cref="DemonHitDetector"/> trigger/overlap can register cuts without keyboard input.
/// Uses signed-extent overlap along the plane (not only the leading corner) so fast notes do not skip the pulse window in one frame.
/// </summary>
[DefaultExecutionOrder(299)]
public class DesktopAutoSliceHits : MonoBehaviour
{
    [Tooltip("Signed distance window (meters along plane normal). Any part of the note AABB in [min,max] triggers a pulse. Wider = easier auto-hits.")]
    public float autoSliceLeadMin = -0.72f;

    public float autoSliceLeadMax = 0.42f;

    void LateUpdate()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (GameplayCameraEnsurer.IsXrDeviceActive() || !GameplayDebugHud.AutoSliceNotes)
            return;

        if (!DesktopSaberTestInput.TryResolveSabers(out GameObject left, out GameObject right))
            return;

        float winMin = Mathf.Min(autoSliceLeadMin, autoSliceLeadMax);
        float winMax = Mathf.Max(autoSliceLeadMin, autoSliceLeadMax);

        foreach (var dh in FindObjectsByType<DemonHandling>(FindObjectsInactive.Exclude))
        {
            if (dh == null || !dh.enabled)
                continue;

            var side = dh.GetComponent<SpawnedNoteSaberSide>();
            if (side == null)
                continue;

            if (!TryNoteOverlapsAutoSliceWindow(dh.transform, winMin, winMax))
                continue;

            GameObject hand = side.isLeftHandSaber ? left : right;
            if (hand == null)
                continue;

            foreach (var swing in hand.GetComponentsInChildren<SwingDetector>(true))
            {
                if (swing != null && swing.isActiveAndEnabled)
                    swing.PulseTestSwing(0.95f);
            }
        }
#endif
    }

    /// <summary>
    /// True if the note's signed min/max along the hit plane overlaps the auto-slice window (same basis as <see cref="DemonHitDetector"/>).
    /// </summary>
    static bool TryNoteOverlapsAutoSliceWindow(Transform demonRoot, float winMin, float winMax)
    {
        if (demonRoot == null || !BeatSaberHitLineGuide.TryGetGameplayHitPlane(out _, out _))
            return false;

        float minS, maxS;
        if (!BeatSaberHitLineGuide.TryGetNoteVisualSignedExtentsAlongPlane(demonRoot, out minS, out maxS))
        {
            float s = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(demonRoot.position);
            minS = maxS = s;
        }

        if (maxS < winMin || minS > winMax)
            return false;
        return true;
    }
}
