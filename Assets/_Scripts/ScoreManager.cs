using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple hit/miss counters (+1 per hit, +1 miss count on miss). Optional UI Text.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("UI (optional)")]
    public Text scoreText;

    int _hits;
    int _misses;
    int _directedSliceScore;

    public int Hits => _hits;
    public int Misses => _misses;

    void Start()
    {
        _hits = 0;
        _misses = 0;
        _directedSliceScore = 0;
        UpdateUI();
    }

    public void RegisterHit()
    {
        _hits++;
        UpdateUI();
    }

    /// <summary>Desktop directed slice: adds timing-based points and increments hit count.</summary>
    public void RegisterDirectedHit(int accuracyPoints)
    {
        _hits++;
        _directedSliceScore += Mathf.Max(0, accuracyPoints);
        UpdateUI();
    }

    public void RegisterMiss()
    {
        _misses++;
        UpdateUI();
    }

    public void ResetScore()
    {
        _hits = 0;
        _misses = 0;
        _directedSliceScore = 0;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText == null)
            return;
        scoreText.text = $"Hits: {_hits}   Miss: {_misses}   Score: {_directedSliceScore}";
    }
}
