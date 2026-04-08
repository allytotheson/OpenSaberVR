using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hit/miss counters, per-cut points (accuracy on desktop directed hits; default wedge for VR swings),
/// combo streak bonus, and total <see cref="Score"/> for results / leaderboard.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("UI (optional)")]
    public Text scoreText;

    [Header("VR / undirected hits")]
    [Tooltip("Points added per VR saber cut (desktop directed hits use timing-based accuracy instead).")]
    public int defaultVrCutPoints = 85;

    [Header("Combo bonus")]
    [Tooltip("Extra points added for each hit after the first in a streak (resets on miss).")]
    public int comboBonusPerHit = 8;

    int _hits;
    int _misses;
    int _cutScore;
    int _bonusScore;
    int _combo;

    public int Hits => _hits;
    public int Misses => _misses;
    public int CutScore => _cutScore;
    public int BonusScore => _bonusScore;
    public int Score => _cutScore + _bonusScore;

    /// <summary>Consecutive note hits without a miss (same counter used for combo bonus).</summary>
    public int ComboStreak => _combo;

    /// <summary>UI multiplier tier from streak: 1× until 2 hits, then 2×, 4×, 8×.</summary>
    public int ScoreMultiplierTier
    {
        get
        {
            if (_combo >= 8)
                return 8;
            if (_combo >= 4)
                return 4;
            if (_combo >= 2)
                return 2;
            return 1;
        }
    }

    void Start()
    {
        ResetScore();
    }

    public void RegisterHit()
    {
        AddSuccessfulHit(defaultVrCutPoints);
    }

    /// <summary>Desktop directed slice: timing-based accuracy points (same combo rules as VR).</summary>
    public void RegisterDirectedHit(int accuracyPoints)
    {
        AddSuccessfulHit(accuracyPoints);
    }

    void AddSuccessfulHit(int cutPoints)
    {
        _hits++;
        _combo++;
        _cutScore += Mathf.Max(0, cutPoints);
        if (_combo >= 2)
            _bonusScore += comboBonusPerHit;
        UpdateUI();
    }

    public void RegisterMiss()
    {
        _misses++;
        _combo = 0;
        UpdateUI();
    }

    public void ResetScore()
    {
        _hits = 0;
        _misses = 0;
        _cutScore = 0;
        _bonusScore = 0;
        _combo = 0;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText == null)
            return;
        scoreText.text =
            $"Hits: {_hits}   Miss: {_misses}   Cut: {_cutScore}   Bonus: {_bonusScore}   Total: {Score}";
    }
}
