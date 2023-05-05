using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Orchestrion.Struct;

namespace Orchestrion.Persistence;

public class SongList
{
    private const string SheetPath = @"https://docs.google.com/spreadsheets/d/1qAkxPiXWF-EUHbIXdNcO-Ilo2AwLnqvdpW9tjKPitPY/gviz/tq?tqx=out:csv&sheet={0}";
    private const string SheetFileName = "xiv_bgm_{0}.csv";
    private readonly Dictionary<int, Song> _songs;
    private readonly HttpClient _client = new();

    private static SongList _instance;
    public static SongList Instance => _instance ??= new SongList();

    private SongList()
    {
        _songs = new Dictionary<int, Song>();
        var sheetPath = Path.Join(DalamudApi.PluginInterface.AssemblyLocation.DirectoryName, SheetFileName);

        var existingText = "";

        try
        {
            existingText = File.ReadAllText(sheetPath);
        }
        catch (Exception)
        {
            // ignore
        }
        
        try
        {
            PluginLog.Log("[SongList] Checking for updated bgm sheets");
            
            LoadMetadataSheet();
            LoadLangSheet("en");
            LoadLangSheet("ja");
            LoadLangSheet("fr");
            LoadLangSheet("de");

            // if (newText != existingText)
            // {
            //     File.WriteAllText(sheetPath, newText);
            //     PluginLog.Log("[SongList] Updated bgm sheet to new version");
            // }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "[SongList] Orchestrion failed to update bgm sheet; using previous version");
            // LoadSheet(existingText);
        }
    }

    private void LoadMetadataSheet()
    {
        _songs.Clear();
        var bgms = DalamudApi.DataManager.Excel.GetSheet<BGM>()!.ToDictionary(k => k.RowId, v => v);
        var sheetText = _client.GetStringAsync(string.Format(SheetPath, "metadata")).Result;
        var sheetLines = sheetText.Split('\n'); // gdocs provides \n
        for (int i = 1; i < sheetLines.Length; i++)
        {
            // The formatting is odd here because gdocs adds quotes around columns and doubles each single quote
            var elements = sheetLines[i].Split(new[] { "\"," }, StringSplitOptions.None);
            var id = int.Parse(elements[0].Substring(1));
            var durationStr = elements[1].Substring(1, elements[1].Length - 2).Replace("\"\"", "\"");
            var parsed = double.TryParse(durationStr, out var durationDbl);
            var duration = parsed ? TimeSpan.FromSeconds(durationDbl) : TimeSpan.Zero;
            if (!parsed) PluginLog.Debug($"failed parse {id}: {durationStr}");

            bgms.TryGetValue((uint)id, out var bgm);
            var song = new Song
            {
                Id = id,
                FilePath = bgm?.File ?? "",
                SpecialMode = bgm?.SpecialMode ?? 0,
                DisableRestart = bgm?.DisableRestart ?? false,
                FileExists = bgm != null && DalamudApi.DataManager.FileExists(bgm.File),
                Duration = duration,
            };

            _songs[id] = song;
        }
    }

    private void LoadLangSheet(string code)
    {
        var sheetText = _client.GetStringAsync(string.Format(SheetPath, code)).Result;
        var sheetLines = sheetText.Split('\n'); // gdocs provides \n
        for (int i = 1; i < sheetLines.Length; i++)
        {
            // The formatting is odd here because gdocs adds quotes around columns and doubles each single quote
            var elements = sheetLines[i].Split(new[] { "\"," }, StringSplitOptions.None);
            var id = int.Parse(elements[0].Substring(1));
            var name = elements[1].Substring(1);
            var altName = elements[2].Substring(1);
            var specialName = elements[3].Substring(1);
            var locations = elements[4].Substring(1);
            var addtlInfo = elements[5].Substring(1, elements[5].Length - 2).Replace("\"\"", "\"");

            if (!_songs.TryGetValue(id, out var song))
                continue;

            if (string.IsNullOrEmpty(name) || name == "Null BGM" || name == "test")
                _songs.Remove(id);
            
            song.Strings[code] = new SongStrings
            {
                Name = name,
                AlternateName = altName,
                SpecialModeName = specialName,
                Locations = locations,
                AdditionalInfo = addtlInfo,
            };
        }
    }

    // private void LoadSheet(string metadataText, string enText, string jaText, string frText, string deText)
    // {
    //     _songs.Clear();
    //     var bgms = DalamudApi.DataManager.Excel.GetSheet<BGM>()!.ToDictionary(k => k.RowId, v => v);
    //     var sheetLines = sheetText.Split('\n'); // gdocs provides \n
    //     for (int i = 1; i < sheetLines.Length; i++)
    //     {
    //         // The formatting is odd here because gdocs adds quotes around columns and doubles each single quote
    //         var elements = sheetLines[i].Split(new[] { "\"," }, StringSplitOptions.None);
    //         var id = int.Parse(elements[0].Substring(1));
    //         var name = elements[1].Substring(1).Replace("\"\"", "\"");
    //
    //         // Any track without an official name is "???"
    //         // While Null BGM tracks are also pretty invalid
    //         if (string.IsNullOrEmpty(name) || name == "Null BGM" || name == "test") continue;
    //
    //         var location = elements[2].Substring(1).Replace("\"\"", "\"");
    //         var additionalInfo = elements[3].Substring(1).Replace("\"\"", "\"");
    //
    //         var durationStr = elements[4].Substring(1, elements[4].Length - 2).Replace("\"\"", "\"");
    //         var parsed = double.TryParse(durationStr, out var durationDbl);
    //         var duration = parsed ? TimeSpan.FromSeconds(durationDbl) : TimeSpan.Zero;
    //         if (!parsed) PluginLog.Debug($"failed parse {id} {name}: {durationStr}");
    //
    //         bgms.TryGetValue((uint)id, out var bgm);
    //         var song = new Song
    //         {
    //             Id = id,
    //             Name = name,
    //             Locations = location,
    //             AdditionalInfo = additionalInfo,
    //             FilePath = bgm?.File ?? "",
    //             SpecialMode = bgm?.SpecialMode ?? 0,
    //             DisableRestart = bgm?.DisableRestart ?? false,
    //             FileExists = bgm != null && DalamudApi.DataManager.FileExists(bgm.File),
    //             Duration = duration,
    //         };
    //
    //         _songs[id] = song;
    //     }
    // }

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

    public Song GetSongByIndex(int index)
    {
        return _songs.Values.ElementAt(index);
    }

    public bool TryGetSong(int id, out Song song)
    {
        return _songs.TryGetValue(id, out song);
    }
    
    public bool TryGetSongByIndex(int id, out Song song)
    {
        song = default;
        try
        {
            song = _songs.Values.ElementAt(id);
            return true;
        }
        catch (Exception e)
        {
            
        }
        return false;
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
    
    public bool TryGetRandomSong(string limitToPlaylist, out int songId)
    {
        songId = 0;
        var playlistExists = Configuration.Instance.Playlists.TryGetValue(limitToPlaylist, out var playlist);
        var isAllSongs = limitToPlaylist == string.Empty;
        if (!playlistExists && !isAllSongs) return false;

        ICollection<int> source = !isAllSongs ? playlist.Songs : _songs.Keys;
        if (source.Count == 0) return false;

        var found = false;
        while (!found)
        {
            var index = Random.Shared.Next(source.Count);

            var id = source.ElementAt(index);
            if (!_songs.ContainsKey(id)) continue;

            found = true;
        }

        return found;
    }
}
