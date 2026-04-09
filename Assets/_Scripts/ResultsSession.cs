/// <summary>
/// One-shot payload for the post-game scoreboard (shown over OpenSaber) or the optional <c>Results</c> scene in the editor.
/// </summary>
public static class ResultsSession
{
    static bool _pending;
    static bool _scoreboardBrowseOnly;
    static int _finalScore;
    static int _cutScore;
    static int _bonusScore;
    static string _songName;
    static string _difficulty;
    static int _highlightRank;

    /// <summary>
    /// Records the run in the leaderboard CSV and queues data for the Results scene.
    /// </summary>
    public static void PublishRunForResultsScene(int totalScore, int cutScore, int bonusScore, string songName, string difficulty)
    {
        _scoreboardBrowseOnly = false;
        _finalScore = totalScore;
        _cutScore = cutScore;
        _bonusScore = bonusScore;
        _songName = songName ?? "Unknown";
        _difficulty = difficulty ?? "";
        _highlightRank = LeaderboardData.AddScore(totalScore, _songName, _difficulty);
        _pending = true;
    }

    public static bool TryConsume(out bool scoreboardBrowseOnly, out int finalScore, out int cutScore, out int bonusScore,
        out string songName, out string difficulty, out int highlightRank)
    {
        if (!_pending)
        {
            scoreboardBrowseOnly = false;
            finalScore = 0;
            cutScore = 0;
            bonusScore = 0;
            songName = "";
            difficulty = "";
            highlightRank = -1;
            return false;
        }

        scoreboardBrowseOnly = _scoreboardBrowseOnly;
        finalScore = _finalScore;
        cutScore = _cutScore;
        bonusScore = _bonusScore;
        songName = _songName;
        difficulty = _difficulty;
        highlightRank = _highlightRank;
        _pending = false;
        return true;
    }
}
