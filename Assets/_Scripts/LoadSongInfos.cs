using Boomlagoon.JSON;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class LoadSongInfos : MonoBehaviour
{
    public List<Song> AllSongs = new List<Song>();
    public int CurrentSong
    {
        get
        {
            return Songsettings != null ? Songsettings.CurrentSongIndex : 0;
        }
        set
        {
            if (Songsettings != null)
                Songsettings.CurrentSongIndex = value;
        }
    }

    public RawImage Cover;
    public Text SongName;
    public Text Artist;
    public Text BPM;
    public Text Levels;
    private SongSettings Songsettings;

    /// <summary>World-space two-column layout can read value-before-label; switch to one rich-text line once.</summary>
    bool _songChooserStatsLayoutApplied;

    private void Awake()
    {
        Songsettings = FindSongSettingsInstance();
        if (Songsettings == null)
            Debug.LogError(
                "[LoadSongInfos] SongSettings not found. Open PersistentScene and press Play (it loads Menu additively), " +
                "or add a GameObject with SongSettings to the scene. See Editor → Project Settings → Editor Build Settings: PersistentScene should be index 0.");
        ResolveUiFieldReferences();
    }

    /// <summary>Rebinds text/cover fields if scene overrides cleared Inspector references (same hierarchy as Main_Menu prefab).</summary>
    void ResolveUiFieldReferences()
    {
        if (SongName != null && Artist != null && BPM != null && Levels != null && Cover != null)
            return;

        var songChooser = transform.Find("SongChooser");
        if (songChooser == null)
            return;

        if (SongName == null)
        {
            var t = songChooser.Find("SongName");
            if (t != null) SongName = t.GetComponent<Text>();
        }

        if (Artist == null)
        {
            var t = songChooser.Find("Artist");
            if (t != null) Artist = t.GetComponent<Text>();
        }

        if (BPM == null)
        {
            var t = songChooser.Find("BPM");
            if (t != null) BPM = t.GetComponent<Text>();
        }

        if (Levels == null)
        {
            var t = songChooser.Find("Difficulty");
            if (t != null) Levels = t.GetComponent<Text>();
        }

        if (Cover == null)
        {
            var t = songChooser.Find("Image");
            if (t != null) Cover = t.GetComponent<RawImage>();
        }
    }

    /// <summary>Beat Saber maps use <c>info.dat</c> or <c>Info.dat</c>; match case-insensitively for all platforms.</summary>
    static string FindInfoDatPath(string songDir)
    {
        foreach (var f in Directory.GetFiles(songDir, "*.dat"))
        {
            if (string.Equals(Path.GetFileName(f), "info.dat", StringComparison.OrdinalIgnoreCase))
                return f;
        }
        return null;
    }

    static SongSettings FindSongSettingsInstance()
    {
        var s = UnityEngine.Object.FindAnyObjectByType<SongSettings>(FindObjectsInactive.Include);
        if (s != null)
            return s;
        var go = GameObject.FindGameObjectWithTag("SongSettings");
        return go != null ? go.GetComponent<SongSettings>() : null;
    }

    private void OnEnable()
    {
        ResolveUiFieldReferences();
        AllSongs.Clear();
        string path = Path.Combine(Application.dataPath + "/Playlists");
        if (Directory.Exists(path))
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                string infoPath = FindInfoDatPath(dir);
                if (Directory.Exists(dir) && infoPath != null)
                {
                    JSONObject infoFile = JSONObject.Parse(File.ReadAllText(infoPath));

                    var song = new Song();
                    song.Path = dir;
                    song.Name = infoFile.GetString("_songName");
                    song.AuthorName = infoFile.GetString("_songAuthorName");
                    song.BPM = infoFile.GetNumber("_beatsPerMinute").ToString();
                    song.CoverImagePath = Path.Combine(dir, infoFile.GetString("_coverImageFilename"));
                    song.AudioFilePath = Path.Combine(dir, infoFile.GetString("_songFilename"));
                    song.Difficulties = new List<string>();

                    var difficultyBeatmapSets = infoFile.GetArray("_difficultyBeatmapSets");
                    foreach (var beatmapSets in difficultyBeatmapSets)
                    {
                        string characteristic = "Standard";
                        try
                        {
                            characteristic = beatmapSets.Obj.GetString("_beatmapCharacteristicName");
                        }
                        catch
                        {
                            /* very old info.dat without characteristics */
                        }

                        foreach (var difficultyBeatmaps in beatmapSets.Obj.GetArray("_difficultyBeatmaps"))
                        {
                            string diff = difficultyBeatmaps.Obj.GetString("_difficulty");
                            song.Difficulties.Add($"{diff} ({characteristic})");
                        }
                    }

                    AllSongs.Add(song);
                }
            }
        }
    }

    public Song NextSong()
    {
        if (Songsettings == null || AllSongs.Count == 0)
            return null;
        CurrentSong++;
        if(CurrentSong > AllSongs.Count - 1)
        {
            CurrentSong = 0;
        }

        Songsettings.CurrentSong = AllSongs[CurrentSong];

        return Songsettings.CurrentSong;
    }

    public Song PreviousSong()
    {
        if (Songsettings == null || AllSongs.Count == 0)
            return null;
        CurrentSong--;
        if (CurrentSong < 0)
        {
            CurrentSong = AllSongs.Count - 1;
        }

        Songsettings.CurrentSong = AllSongs[CurrentSong];

        return Songsettings.CurrentSong;
    }

    public Song GetCurrentSong()
    {
        return Songsettings != null ? Songsettings.CurrentSong : null;
    }

    /// <summary>
    /// Fills song / artist / BPM / levels with <c>Label: value</c> order using rich text
    /// (same lighter tone as the old description column). Hides static description labels
    /// and widens value fields so lines do not overlap on the world-space canvas.
    /// </summary>
    public void BindSongChooserRows(Song song)
    {
        if (song == null)
            return;
        ApplySongChooserSingleColumnLayoutOnce();
        EnsureSongStatTextsCenteredInRect();

        if (SongName != null)
            SongName.text = RichSongRow("Song:", song.Name ?? "");
        if (Artist != null)
            Artist.text = RichSongRow("Artist:", song.AuthorName ?? "");
        if (BPM != null)
            BPM.text = RichSongRow("BPM:", song.BPM ?? "");
        if (Levels != null)
            Levels.text = RichSongRow("Levels:", song.Difficulties != null ? song.Difficulties.Count.ToString() : "0");
    }

    static string RichSongRow(string label, string value)
    {
        return $"<color=#F5FAFF>{label}</color> {value}";
    }

    void ApplySongChooserSingleColumnLayoutOnce()
    {
        if (_songChooserStatsLayoutApplied)
            return;

        var songChooser = transform.Find("SongChooser");
        if (songChooser == null)
            return;

        foreach (var name in new[]
                 {
                     "SongNameDescription",
                     "ArtistDescription",
                     "BPMDescription",
                     "DifficultyDescription",
                 })
        {
            var row = songChooser.Find(name);
            if (row != null)
                row.gameObject.SetActive(false);
        }

        WidenSongStatRow(songChooser.Find("SongName")?.GetComponent<Text>());
        WidenSongStatRow(songChooser.Find("Artist")?.GetComponent<Text>());
        WidenSongStatRow(songChooser.Find("BPM")?.GetComponent<Text>());
        WidenSongStatRow(songChooser.Find("Difficulty")?.GetComponent<Text>());

        _songChooserStatsLayoutApplied = true;
    }

    static void WidenSongStatRow(Text txt)
    {
        if (txt == null)
            return;
        var rt = txt.rectTransform;
        rt.sizeDelta = new Vector2(880f, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(-30f, rt.anchoredPosition.y);
    }

    void EnsureSongStatTextsCenteredInRect()
    {
        // Set every bind so alignment stays correct if UI references are rebound.
        foreach (var t in new[] { SongName, Artist, BPM, Levels })
        {
            if (t != null)
                t.alignment = TextAnchor.MiddleCenter;
        }
    }
}

public class Song
{
    public string Path { get; set; }
    public string AudioFilePath { get; set; }
    public string Name { get; set; }
    public string AuthorName { get; set; }
    public string BPM { get; set; }
    public string CoverImagePath { get; set; }
    public List<string> Difficulties { get; set; }
    public string SelectedDifficulty { get; set; }
}

/// <summary><see cref="UnityEngine.Networking.UnityWebRequest"/> needs a URI; raw disk paths must be <c>file://</c> URLs.</summary>
internal static class LocalAudioRequestUri
{
    public static string FromFilesystemPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return path;
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }
}
