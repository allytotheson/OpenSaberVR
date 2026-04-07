/// <summary>
/// One-shot payload into the authored <c>Results</c> scene (after a run, or scoreboard-only from the title screen).
/// </summary>
public static class ResultsSession
{
    static bool _pending;
    static bool _scoreboardBrowseOnly;
    static int _finalScore;
    static string _songName;
    static string _difficulty;
    static int _highlightRank;

    /// <summary>
    /// Records the run in the leaderboard CSV and queues data for the Results scene.
    /// </summary>
    public static void PublishRunForResultsScene(int score, string songName, string difficulty)
    {
        _scoreboardBrowseOnly = false;
        _finalScore = score;
        _songName = songName ?? "Unknown";
        _difficulty = difficulty ?? "";
        _highlightRank = LeaderboardData.AddScore(score, _songName, _difficulty);
        _pending = true;
    }

    /// <summary>
    /// Opens Results as a read-only top-10 view (no new CSV row). Used from the main menu title screen.
    /// </summary>
    public static void PublishScoreboardBrowse()
    {
        _scoreboardBrowseOnly = true;
        _finalScore = 0;
        _songName = "";
        _difficulty = "";
        _highlightRank = -1;
        _pending = true;
    }

    public static bool TryConsume(out bool scoreboardBrowseOnly, out int finalScore, out string songName, out string difficulty, out int highlightRank)
    {
        if (!_pending)
        {
            scoreboardBrowseOnly = false;
            finalScore = 0;
            songName = "";
            difficulty = "";
            highlightRank = -1;
            return false;
        }

        scoreboardBrowseOnly = _scoreboardBrowseOnly;
        finalScore = _finalScore;
        songName = _songName;
        difficulty = _difficulty;
        highlightRank = _highlightRank;
        _pending = false;
        return true;
    }
}
