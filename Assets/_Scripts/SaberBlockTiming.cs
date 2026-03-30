using UnityEngine;

/// <summary>
/// Lead/trail signed distance to the gameplay hit plane for a note root (same basis as
/// <see cref="NoteMissDetector"/> / <see cref="DemonHitDetector"/>). Use for automated swing scheduling or RPi fusion.
/// </summary>
public static class SaberBlockTiming
{
    public static bool TryGetLeadTrailSigned(Transform demonRoot, out float leadSigned, out float trailSigned)
    {
        leadSigned = trailSigned = 0f;
        if (demonRoot == null)
            return false;
        if (!BeatSaberHitLineGuide.TryGetGameplayHitPlane(out _, out Vector3 nRaw))
            return false;
        Vector3 n = nRaw.normalized;
        Vector3 motion = (-demonRoot.forward).normalized;
        float motionDotN = Vector3.Dot(motion, n);
        if (!BeatSaberHitLineGuide.TryGetNoteVisualSignedExtentsAlongPlane(demonRoot, out float minS, out float maxS))
            return false;
        BeatSaberHitLineGuide.GetLeadTrailSignedFromExtents(minS, maxS, motionDotN, out leadSigned, out trailSigned);
        return true;
    }
}
