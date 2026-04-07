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
    const int MAX_ENTRIES = 10;
    const string FILE_NAME = "leaderboard.csv";

    [Serializable]
    public struct Entry
    {
        public int score;
        public string songName;
        public string difficulty;
        public string date;
    }

    static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    /// <summary>
    /// Adds a score, keeps the list sorted descending and trimmed to top 10, then writes to disk.
    /// Returns the 0-based rank if the score made the list, or -1 otherwise.
    /// </summary>
    public static int AddScore(int score, string songName, string difficulty)
    {
        var list = GetTopScores();
        var entry = new Entry
        {
            score = score,
            songName = SanitizeCsvField(songName ?? "Unknown"),
            difficulty = SanitizeCsvField(difficulty ?? ""),
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
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
        if (list.Count > MAX_ENTRIES)
            list.RemoveRange(MAX_ENTRIES, list.Count - MAX_ENTRIES);

        WriteCsv(list);

        return insertIdx < MAX_ENTRIES ? insertIdx : -1;
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
            for (int i = 1; i < lines.Length; i++) // skip header
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(',');
                if (parts.Length < 4) continue;

                if (!int.TryParse(parts[0].Trim(), out int score))
                    continue;

                list.Add(new Entry
                {
                    score = score,
                    songName = parts[1].Trim(),
                    difficulty = parts[2].Trim(),
                    date = parts[3].Trim()
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LeaderboardData] Failed to read {path}: {e.Message}");
        }

        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > MAX_ENTRIES)
            list.RemoveRange(MAX_ENTRIES, list.Count - MAX_ENTRIES);

        return list;
    }

    static void WriteCsv(List<Entry> list)
    {
        try
        {
            using (var sw = new StreamWriter(FilePath, false))
            {
                sw.WriteLine("Score,SongName,Difficulty,Date");
                foreach (var e in list)
                    sw.WriteLine($"{e.score},{e.songName},{e.difficulty},{e.date}");
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
