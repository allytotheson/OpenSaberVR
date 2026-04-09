using UnityEngine;

/// <summary>
/// Miss when the <em>trail</em> of the note (last part still up-track along actual motion) has passed the slice plane
/// by <see cref="missPastPlaneMeters"/>. Uses the same plane as the cyan frame and aligns lead/trail with
/// <c>dot(-forward, planeNormal)</c> like <see cref="DemonHitDetector"/> — otherwise min/max AABB corners mis-label
/// leading vs trailing and MISS fires before the block looks past the line. Prefers <see cref="Renderer.bounds"/>
/// over collider so the test matches the visible cube.
/// </summary>
[DefaultExecutionOrder(380)]
public class NoteMissDetector : MonoBehaviour
{
    [Tooltip("Trail corner must be this far past the plane (toward player, meters) before MISS. Larger = later. If miss feels early compared to the cyan guide, increase this slightly (e.g. 0.52 → 0.62) so the visible block must travel farther past the plane.")]
    public float missPastPlaneMeters = 0.52f;

    [Tooltip("Arms miss logic once the trail is at least this far on the incoming-note side of the plane.")]
    public float minIncomingSignedMeters = 0.05f;

    [Tooltip("Flyout “MISS” tint. Set from spawner from note type (red / blue).")]
    public Color missFlyoutColor = new Color(1f, 0.35f, 0.4f, 1f);

    bool _reported;
    bool _sawIncomingSide;

    void LateUpdate()
    {
        if (_reported)
            return;
        if (!BeatSaberHitLineGuide.TryGetGameplayHitPlane(out Vector3 pp, out Vector3 n))
            return;

        n = n.normalized;
        var dh = GetComponentInParent<DemonHandling>();
        var root = dh != null ? dh.transform : transform;
        Vector3 motion = (-root.forward).normalized;
        float motionDotN = Vector3.Dot(motion, n);

        if (!BeatSaberHitLineGuide.TryGetNoteVisualSignedExtentsAlongPlane(root, out float minS, out float maxS))
            return;

        BeatSaberHitLineGuide.GetLeadTrailSignedFromExtents(minS, maxS, motionDotN, out float leadSigned, out float trailSigned);

        if (!_sawIncomingSide && trailSigned > minIncomingSignedMeters)
            _sawIncomingSide = true;

        if (!_sawIncomingSide)
            return;

        // Trail = last to cross toward the player; MISS only once that part is clearly past the plane.
        if (trailSigned < -missPastPlaneMeters)
        {
            _reported = true;
            // HitMissFlyout.ShowMiss(missFlyoutColor); // suppressed during swing-debug (SWING flyout only)
            var sm = FindAnyObjectByType<ScoreManager>();
            if (sm != null)
                sm.RegisterMiss();
        }
    }
}
