using UnityEngine;

/// <summary>
/// Post-run scoreboard is shown <b>over the OpenSaber scene</b> (stadium backdrop) while gameplay has stopped.
/// The overlay is parented to <see cref="SceneHandling"/> in PersistentScene so it survives until the player dismisses it.
/// </summary>
public static class PostGameScoreboardFlow
{
    public static void PresentAfterRun(SceneHandling sceneHandling)
    {
        if (sceneHandling == null)
        {
            Debug.LogError("[PostGameScoreboardFlow] SceneHandling is missing; cannot show scoreboard.");
            return;
        }

        if (!ResultsSession.TryConsume(out bool browseOnly, out int finalScore, out int cutScore, out int bonusScore,
                out string songName, out string difficulty, out int highlightRank))
            return;

        ScoreboardOverlayUI.Build(sceneHandling.transform, browseOnly, finalScore, cutScore, bonusScore, songName,
            difficulty, highlightRank, () => sceneHandling.StartPostGameScoreboardExitRoutine());
    }
}
