using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Dalamud.Logging;

namespace Orchestrion;

public struct Song
{
    public int Id;
    public string Name;
    public string Locations;
    public string AdditionalInfo;
}

public static class SongList
{
    private const string SheetPath = @"https://docs.google.com/spreadsheets/d/1gGNCu85sjd-4CDgqw-K5tefTe4HYuDK38LkRyvx_fEc/gviz/tq?tqx=out:csv&sheet=main";
    private const string SheetFileName = "xiv_bgm.csv";

    // private static string _pluginDirectory;
    private static Dictionary<int, Song> _songs;

    static SongList()
    {
        _songs = new Dictionary<int, Song>();
    }

    public static void Init(string pluginDirectory)
    {
        var sheetPath = Path.Join(pluginDirectory, SheetFileName);
        _songs = new Dictionary<int, Song>();

        var existingText = File.ReadAllText(sheetPath);

        using var client = new WebClient();
        try
        {
            PluginLog.Log("Checking for updated bgm sheet");
            var newText = client.DownloadString(SheetPath);
            LoadSheet(newText);

            // would really prefer some kind of proper versioning here
            if (newText != existingText)
            {
                File.WriteAllText(sheetPath, newText);
                PluginLog.Log("Updated bgm sheet to new version");
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Orchestrion failed to update bgm sheet; using previous version");
            // if this throws, something went horribly wrong and we should just break completely
            LoadSheet(existingText);
        }
    }

    // Attempts to load supplemental bgm data from the csv file
    // This throws all internal errors
    private static void LoadSheet(string sheetText)
    {
        _songs.Clear();
        var sheetLines = sheetText.Split('\n'); // gdocs provides \n
        for (int i = 1; i < sheetLines.Length; i++)
        {
            // The formatting is odd here because gdocs adds quotes around columns and doubles each single quote
            var elements = sheetLines[i].Split(new[] { "\"," }, StringSplitOptions.None);
            var id = int.Parse(elements[0].Substring(1));
            var name = elements[1].Substring(1).Replace("\"\"", "\"");

            // Any track without an official name is "???"
            // While Null BGM tracks are also pretty invalid
            if (string.IsNullOrEmpty(name) || name == "Null BGM") continue;

            var location = elements[2].Substring(1).Replace("\"\"", "\"");
            var additionalInfo = elements[3].Substring(1, elements[3].Substring(1).Length - 1).Replace("\"\"", "\"");

            var song = new Song
            {
                Id = id,
                Name = name,
                Locations = location,
                AdditionalInfo = additionalInfo
            };

            _songs[id] = song;
        }
    }

    public static Dictionary<int, Song> GetSongs()
    {
        return _songs;
    }

    public static Song GetSong(int id, out Song song)
    {
        return _songs.TryGetValue(id, out song) ? song : default;
    }

    public static bool TryGetSong(int id, out Song song)
    {
        return _songs.TryGetValue(id, out song);
    }

    public static string GetSongName(int id)
    {
        return _songs.TryGetValue(id, out var song) ? song.Name : "";
    }

    public string GetSongTitle(int id)
    {
        try
        {
            return songs.ContainsKey(id) ? songs[id].Name : "";
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "GetSongTitle");
        }

        return "";
    }

    public bool SongExists(int id)
    {
        return songs.ContainsKey(id);
    }

    public bool TryGetSongByName(string name, out int songId)
    {
        songId = 0;
        foreach (var song in songs)
        {
            if (string.Equals(song.Value.Name, name, StringComparison.InvariantCultureIgnoreCase))
            {
                songId = song.Key;
                return true;
            }
        }

        return false;
    }

    public bool TryGetRandomSong(bool limitToFavorites, out int songId)
    {
        songId = 0;

        ICollection<int> source = limitToFavorites ? configuration.FavoriteSongs : songs.Keys;
        if (source.Count == 0) return false;

        var max = source.Max();
        var random = new Random();
        var found = false;
        while (!found)
        {
            songId = random.Next(2, max + 1);

            if (!songs.ContainsKey(songId)) continue;
            if (limitToFavorites && !IsFavorite(songId)) continue;

            found = true;
        }

        return found;
    }

    public void AddSongToHistory(int id)
    {
        // Don't add silence
        if (id == 1 || !songs.ContainsKey(id))
            return;

        var newEntry = new SongHistoryEntry
        {
            Id = id,
            TimePlayed = DateTime.Now
        };

        var currentIndex = songHistory.Count - 1;

        // Check if we have history, if yes, then check if ID is the same as previous, if not, add to history
        if (currentIndex < 0 || songHistory[currentIndex].Id != id)
        {
            songHistory.Add(newEntry);
            PluginLog.Debug($"Added {id} to history. There are now {currentIndex + 1} songs in history.");
        }
    }
}

}
