using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Newtonsoft.Json;
using Orchestrion.Struct;

namespace Orchestrion.Persistence;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool ShowSongInTitleBar { get; set; } = true;
    public bool ShowSongInChat { get; set; } = true;
    public bool ShowIdInNative { get; set; } = false;
    public bool ShowSongInNative { get; set; } = true;
    public bool HandleSpecialModes { get; set; } = true;
    public bool ShowFilePaths { get; set; } = true;
    public bool PlaylistPaneOpen { get; set; } = true;
    public bool ShowMiniPlayer { get; set; } = false;
    public bool MiniPlayerLock { get; set; } = false;
    public float MiniPlayerOpacity { get; set; } = 1.0f;
    public bool ShowAltLangTitles { get; set; } = false;
    public bool UseClientLangInServerInfo { get; set; } = true;
    public bool UseClientLangInChat { get; set; } = true;

    public Dictionary<int, SongReplacementEntry> SongReplacements { get; private set; } = new();
    
    [Obsolete("Favorites are gone in favor of playlists.")]
    public HashSet<int> FavoriteSongs { get; internal set; } = new();
    
    public Dictionary<string, Playlist> Playlists { get; set; } = new();

    private Configuration()
    {
        
    }

    [JsonIgnore]
    private static Configuration _instance;
    
    [JsonIgnore]
    public static Configuration Instance {
        get
        {
            _instance ??= DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Migrate(_instance);
            return _instance;
        }
    }

    public bool TryGetPlaylist(string playlistName, out Playlist foundPlaylist)
    {
        foundPlaylist = null;
        foreach (var pInfo in Playlists) {
            if (playlistName.Equals(pInfo.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                foundPlaylist = pInfo.Value;
                return true;
            }
        }
        return false;
    }

    public void DeletePlaylist(string playlistName)
    {
        Playlists.Remove(playlistName);
        Save();
    }

    private static void Migrate(Configuration c)
    {
        switch (c.Version)
        {
            case 1:
                c.Version = 2;
                c.Playlists = new Dictionary<string, Playlist>
                {
                    {"Favorites", new Playlist("Favorites", c.FavoriteSongs.ToList())},
                };
                c.Save();
                break;
        }
    }

    public void Save()
    {
        DalamudApi.PluginInterface.SavePluginConfig(this);
    }
}