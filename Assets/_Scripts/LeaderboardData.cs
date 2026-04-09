using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists the top 10 scores in a CSV file at <see cref="Application.persistentDataPath"/>/leaderboard.csv.
/// Thread-safe for single-thread Unity usage.
/// </summary>
public static class LeaderboardData
{
    public const int MaxEntries = 10;
    const string FILE_NAME = "leaderboard.csv";

    [Serializable]
    public struct Entry
    {
        public int score;
        public string songName;
        public string difficulty;
        public string playerName;
    }

    static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    /// <summary>
    /// Adds a score, keeps the list sorted descending and trimmed to top 10, then writes to disk.
    /// Returns the 0-based rank if the score made the list, or -1 otherwise.
    /// </summary>
    public static int AddScore(int score, string songName, string difficulty, string playerName = "")
    {
        var list = GetTopScores();
        var entry = new Entry
        {
            score = score,
            songName = SanitizeCsvField(songName ?? "Unknown"),
            difficulty = SanitizeCsvField(difficulty ?? ""),
            playerName = SanitizeCsvField(playerName ?? "")
        };

        int insertIdx = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            if (score >= list[i].score)
            {
                insertIdx = i;
                break;
            }
        }

        list.Insert(insertIdx, entry);
        if (list.Count > MaxEntries)
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);

        WriteCsv(list);

        return insertIdx < MaxEntries ? insertIdx : -1;
    }

    /// <summary>Updates the player name for a row by 0-based rank in the sorted leaderboard.</summary>
    public static void SetPlayerNameForRank(int rankIndex, string playerName)
    {
        var list = GetTopScores();
        if (rankIndex < 0 || rankIndex >= list.Count)
            return;

        string name = SanitizeCsvField(playerName ?? "");
        if (string.IsNullOrWhiteSpace(name))
            name = "Anonymous";

        var e = list[rankIndex];
        e.playerName = name;
        list[rankIndex] = e;
        WriteCsv(list);
    }

    public static List<Entry> GetTopScores()
    {
        var list = new List<Entry>();
        string path = FilePath;
        if (!File.Exists(path))
            return list;

        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2)
                return list;

            for (int i = 1; i < lines.Length; i++) // skip header
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(',');
                if (parts.Length < 4) continue;

                if (!int.TryParse(parts[0].Trim(), out int score))
                    continue;

                // Column 4 was "Date" in older builds; we now persist playerName (legacy rows still load).
                list.Add(new Entry
                {
                    score = score,
                    songName = parts[1].Trim(),
                    difficulty = parts[2].Trim(),
                    playerName = parts[3].Trim()
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LeaderboardData] Failed to read {path}: {e.Message}");
        }

        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > MaxEntries)
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);

        return list;
    }

    static void WriteCsv(List<Entry> list)
    {
        try
        {
            using (var sw = new StreamWriter(FilePath, false))
            {
                sw.WriteLine("Score,SongName,Difficulty,PlayerName");
                foreach (var e in list)
                    sw.WriteLine($"{e.score},{e.songName},{e.difficulty},{e.playerName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LeaderboardData] Failed to write leaderboard: {e.Message}");
        }
    }

    static string SanitizeCsvField(string field)
    {
        if (field == null) return "";
        return field.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }
}
