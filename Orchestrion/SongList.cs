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

public class SongList
{
    private const string SheetPath = @"https://docs.google.com/spreadsheets/d/1gGNCu85sjd-4CDgqw-K5tefTe4HYuDK38LkRyvx_fEc/gviz/tq?tqx=out:csv&sheet=main";
    private const string SheetFileName = "xiv_bgm.csv";
    
    private Dictionary<int, Song> Songs { get; }

    public SongList(OrchestrionPlugin plugin)
    {
        var sheetPath = Path.Join(plugin.PluginInterface.AssemblyLocation.DirectoryName, SheetFileName);
        Songs = new Dictionary<int, Song>();
        
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
    private void LoadSheet(string sheetText)
    {
        Songs.Clear();
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

            Songs[id] = song;
        }
    }
    
    public Song GetSong(int id, out Song song)
    {
        return Songs.TryGetValue(id, out song) ? song : default;
    }

    public bool TryGetSong(int id, out Song song)
    {
        return Songs.TryGetValue(id, out song);
    }

    public string GetSongName(int id)
    {
        return Songs.TryGetValue(id, out var song) ? song.Name : "";
    }
}