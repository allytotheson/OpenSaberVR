using UnityEngine;

/// <summary>
/// Fires once when this note moves past the hit plane without being destroyed (miss).
/// Runs after <see cref="DemonHitDetector"/> so a same-frame hit destroys the note first.
/// </summary>
[DefaultExecutionOrder(380)]
public class NoteMissDetector : MonoBehaviour
{
    [Tooltip("Past-plane threshold along camera forward (meters). Larger = miss registers later.")]
    public float missPastPlaneMeters = 0.28f;

    bool _reported;
    bool _seenApproach;

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

        if (!_seenApproach && Mathf.Abs(signed) > 1.2f)
            _seenApproach = true;

        if (!_seenApproach)
            return;

        // Past the plane toward +normal (down track in OpenSaber); missed slice window.
        if (signed > missPastPlaneMeters)
        {
            _reported = true;
            HitMissFlyout.ShowMiss();
            var sm = FindAnyObjectByType<ScoreManager>();
            if (sm != null)
                sm.RegisterMiss();
        }
    }
}
