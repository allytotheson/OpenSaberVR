using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks score when demons are destroyed.
/// Optional UI Text to display current score.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("UI (optional)")]
    public Text scoreText;

    private int score;
    public int Score => score;

    void Start()
    {
        score = 0;
        UpdateUI();
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateUI();
    }

    public void ResetScore()
    {
        score = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = score.ToString();
    }
}
