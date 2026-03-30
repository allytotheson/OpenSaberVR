using UnityEngine;

/// <summary>
/// Fires once when this note crosses the gameplay hit plane (cyan frame) toward the player without being destroyed.
/// Uses the same plane as <see cref="BeatSaberHitLineGuide"/>: plane normal points <em>toward incoming notes</em>, so
/// signed distance is positive before the line and negative after passing toward the player.
/// Runs after <see cref="DemonHitDetector"/> so a same-frame hit destroys the note first.
/// </summary>
[DefaultExecutionOrder(380)]
public class NoteMissDetector : MonoBehaviour
{
    [Tooltip("How far past the hit plane (toward the player, meters) before a miss is registered. Larger = slightly more lenient.")]
    public float missPastPlaneMeters = 0.28f;

    [Tooltip("Note must once be at least this far on the incoming-note side of the plane before miss logic runs (avoids spurious misses if spawn alignment is odd).")]
    public float minIncomingSignedMeters = 0.05f;

    bool _reported;
    bool _sawIncomingSide;

    static Vector3 SamplePoint(Transform demonRoot)
    {
        var col = demonRoot.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;
        return demonRoot.position;
    }

    void LateUpdate()
    {
        if (_reported)
            return;
        float signed = BeatSaberHitLineGuide.SignedDistanceToGameplayHitPlane(SamplePoint(transform));

        if (!_sawIncomingSide && signed > minIncomingSignedMeters)
            _sawIncomingSide = true;

        if (!_sawIncomingSide)
            return;

        // Crossed the line toward the player (Beat Saber / BeatSaver timing: slice at the plane; miss once clearly past it).
        if (signed < -missPastPlaneMeters)
        {
            _reported = true;
            HitMissFlyout.ShowMiss();
            var sm = FindAnyObjectByType<ScoreManager>();
            if (sm != null)
                sm.RegisterMiss();
        }
    }
}
