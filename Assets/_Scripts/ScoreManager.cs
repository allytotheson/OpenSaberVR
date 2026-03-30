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

    public int Hits => _hits;
    public int Misses => _misses;

    void Start()
    {
        _hits = 0;
        _misses = 0;
        UpdateUI();
    }

    public void RegisterHit()
    {
        _hits++;
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
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText == null)
            return;
        scoreText.text = $"Hits: {_hits}   Miss: {_misses}";
    }
}
