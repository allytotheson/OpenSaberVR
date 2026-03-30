using UnityEngine;

/// <summary>
/// When <see cref="GameplayDebugHud.AutoSliceNotes"/> is on, pulses the matching saber as each note crosses the hit plane
/// so <see cref="DemonHitDetector"/> trigger/overlap can register cuts without keyboard input.
/// </summary>
[DefaultExecutionOrder(295)]
public class DesktopAutoSliceHits : MonoBehaviour
{
    [Tooltip("Leading-edge signed distance window (meters along plane normal) for auto pulses.")]
    public float autoSliceLeadMin = -0.32f;

    public float autoSliceLeadMax = 0.14f;

    void LateUpdate()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (GameplayCameraEnsurer.IsXrDeviceActive() || !GameplayDebugHud.AutoSliceNotes)
            return;

        if (!DesktopSaberTestInput.TryResolveSabers(out GameObject left, out GameObject right))
            return;

        foreach (var dh in FindObjectsByType<DemonHandling>(FindObjectsInactive.Exclude))
        {
            if (dh == null || !dh.enabled)
                continue;

            var side = dh.GetComponent<SpawnedNoteSaberSide>();
            if (side == null)
                continue;

            if (!SaberBlockTiming.TryGetLeadTrailSigned(dh.transform, out float lead, out _))
                continue;

            if (lead < autoSliceLeadMin || lead > autoSliceLeadMax)
                continue;

            GameObject hand = side.isLeftHandSaber ? left : right;
            if (hand == null)
                continue;

            var swing = hand.GetComponent<SwingDetector>();
            if (swing == null)
                swing = hand.GetComponentInChildren<SwingDetector>();
            if (swing != null)
                swing.PulseTestSwing(0.48f);
        }
#endif
    }
}
