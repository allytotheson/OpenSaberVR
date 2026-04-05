using Boomlagoon.JSON;
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
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "info.dat").Length > 0)
                {
                    JSONObject infoFile = JSONObject.Parse(File.ReadAllText(Path.Combine(dir, "info.dat")));

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
