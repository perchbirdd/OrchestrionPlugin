using Dalamud.Configuration;
using System.Collections.Generic;
using Newtonsoft.Json;
using Orchestrion.Struct;

namespace Orchestrion;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool ShowSongInTitleBar { get; set; } = true;
    public bool ShowSongInChat { get; set; } = true;
    public bool ShowIdInNative { get; set; } = false;
    public bool ShowSongInNative { get; set; } = true;
    public bool HandleSpecialModes { get; set; } = true;

    public Dictionary<int, SongReplacementEntry> SongReplacements { get; private set; } = new();
    public HashSet<int> FavoriteSongs { get; internal set; } = new();

    private Configuration() { }

    [JsonIgnore]
    private static Configuration _instance;
    
    [JsonIgnore]
    public static Configuration Instance => _instance ??= DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    
    public bool IsFavorite(int songId)
    {
        return FavoriteSongs.Contains(songId); 
    }

    public void AddFavorite(int songId)
    {
        FavoriteSongs.Add(songId);
        Save();
    }

    public void RemoveFavorite(int songId)
    {
        FavoriteSongs.Remove(songId);
        Save();
    }
    
    public void Save()
    {
        DalamudApi.PluginInterface.SavePluginConfig(this);
    }
}