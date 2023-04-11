using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Orchestrion.Struct;

namespace Orchestrion;

public class SongList
{
    private const string SheetPath = @"https://docs.google.com/spreadsheets/d/1gGNCu85sjd-4CDgqw-K5tefTe4HYuDK38LkRyvx_fEc/gviz/tq?tqx=out:csv&sheet=main";
    private const string SheetFileName = "xiv_bgm.csv";

    private readonly Dictionary<int, Song> _songs;

    private static SongList _instance;
    public static SongList Instance => _instance ??= new SongList();
    
    private SongList()
    {
        _songs = new Dictionary<int, Song>();
        var sheetPath = Path.Join(DalamudApi.PluginInterface.AssemblyLocation.FullName, SheetFileName);
        _songs = new Dictionary<int, Song>();

        var existingText = "";

        try
        {
            existingText = File.ReadAllText(sheetPath);
        }
        catch (Exception)
        {
            // ignore
        }

        using var client = new HttpClient();
        try
        {
            PluginLog.Log("[SongList] Checking for updated bgm sheet");
            var newText = client.GetStringAsync(SheetPath).Result;
            LoadSheet(newText);

            if (newText != existingText)
            {
                File.WriteAllText(sheetPath, newText);
                PluginLog.Log("[SongList] Updated bgm sheet to new version");
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "[SongList] Orchestrion failed to update bgm sheet; using previous version");
            LoadSheet(existingText);
        }
    }

    private void LoadSheet(string sheetText)
    {
        _songs.Clear();
        var bgms = DalamudApi.DataManager.Excel.GetSheet<BGM>()!.ToDictionary(k => k.RowId, v => v);
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

            bgms.TryGetValue((uint)id, out var bgm);
            var song = new Song
            {
                Id = id,
                Name = name,
                Locations = location,
                AdditionalInfo = additionalInfo,
                SpecialMode = bgm?.SpecialMode ?? 0,
                DisableRestart = bgm?.DisableRestart ?? false,
                FileExists = bgm != null && DalamudApi.DataManager.FileExists(bgm.File),
            };

            _songs[id] = song;
        }
    }

    public bool IsDisableRestart(int id)
    {
        return _songs.TryGetValue(id, out var song) && song.DisableRestart;
    }

    public Dictionary<int, Song> GetSongs()
    {
        return _songs;
    }

    public Song GetSong(int id)
    {
        return _songs.TryGetValue(id, out var song) ? song : default;
    }

    public bool TryGetSong(int id, out Song song)
    {
        return _songs.TryGetValue(id, out song);
    }

    public string GetSongTitle(int id)
    {
        return _songs.TryGetValue(id, out var song) ? song.Name : "";
    }

    public bool SongExists(int id)
    {
        return _songs.ContainsKey(id);
    }

    public bool TryGetSongByName(string name, out int songId)
    {
        songId = 0;
        foreach (var song in _songs)
        {
            if (string.Equals(song.Value.Name, name, StringComparison.InvariantCultureIgnoreCase))
            {
                songId = song.Key;
                return true;
            }
        }

        return false;
    }

    public int GetFirstReplacementCandidateId()
    {
        return _songs.Keys.First(x => !Configuration.Instance.SongReplacements.ContainsKey(x));
    }
    
    public bool TryGetRandomSong(bool limitToFavorites, out int songId)
    {
        songId = 0;

        ICollection<int> source = limitToFavorites ? Configuration.Instance.FavoriteSongs : _songs.Keys;
        if (source.Count == 0) return false;

        var max = source.Max();
        var random = new Random();
        var found = false;
        while (!found)
        {
            songId = random.Next(2, max + 1);

            if (!_songs.ContainsKey(songId)) continue;
            if (limitToFavorites && !Configuration.Instance.IsFavorite(songId)) continue;

            found = true;
        }

        return found;
    }
}
