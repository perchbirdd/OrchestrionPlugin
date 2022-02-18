using System;
using System.Collections.Generic;
using Dalamud.Logging;
using Dalamud.Plugin.Ipc;

namespace Orchestrion;

public class OrchestrionIpcManager : IDisposable
{
    private readonly OrchestrionPlugin _plugin;

    private const string IpcDisplayName = "Orchestrion Plugin";

    private ICallGateSubscriber<string, string, string, uint, string> _wotsitRegister;
    private ICallGateSubscriber<string, bool> _wotsitUnregister;
    private Dictionary<string, Song> _wotsitSongIpcs;
    private string _wotsitRandomGuid;
    private string _wotsitRandomFavoriteGuid;
    private string _wotsitStopGuid;

    private const string PlayRandom = "Play a random track";
    private const string PlayRandomFavorites = "Play a random track from favorites";
    private const string Stop = "Stop playing";

    public OrchestrionIpcManager(OrchestrionPlugin plugin)
    {
        _plugin = plugin;

        // InitForSelf();

        try
        {
            InitForWotsit();
        }
        catch (Exception)
        {
            // ignored
        }

        var wotsitAvailable = OrchestrionPlugin.PluginInterface.GetIpcSubscriber<bool>("FA.Available");
        wotsitAvailable.Subscribe(InitForWotsit);
    }

    private void InitForWotsit()
    {
        _wotsitRegister = OrchestrionPlugin.PluginInterface.GetIpcSubscriber<string, string, string, uint, string>("FA.RegisterWithSearch");
        _wotsitUnregister = OrchestrionPlugin.PluginInterface.GetIpcSubscriber<string, bool>("FA.UnregisterAll");
        
        var subscribe = OrchestrionPlugin.PluginInterface.GetIpcSubscriber<string, bool>("FA.Invoke");
        subscribe.Subscribe(WotsitInvoke);
        
        _wotsitSongIpcs = new Dictionary<string, Song>();
        
        foreach (var song in SongList.GetSongs())
        {
            var guid = _wotsitRegister.InvokeFunc(IpcDisplayName, $"Play {song.Value.Name}", GetSearchString(song.Value), 67);
            _wotsitSongIpcs.Add(guid, song.Value);  
        }
        
        _wotsitRandomGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, PlayRandom, PlayRandom, 67);
        _wotsitRandomFavoriteGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, PlayRandomFavorites, PlayRandomFavorites, 67);
        _wotsitStopGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, Stop, Stop, 67);

        PluginLog.Debug($"Registered {_wotsitSongIpcs.Count} songs with Wotsit");
    }

    private string GetSearchString(Song song)
    {
        return $"Play {song.Name} {song.Locations} {song.AdditionalInfo}";
    }

    private void WotsitInvoke(string guid)
    {
        if (_wotsitSongIpcs.TryGetValue(guid, out var song))
        {
            _plugin.PlaySong(song.Id);
        }
        else if (guid == _wotsitRandomGuid)
        {
            _plugin.PlayRandomSong();
        }
        else if (guid == _wotsitRandomFavoriteGuid)
        {
            _plugin.PlayRandomSong(restrictToFavorites: true);
        }
        else if (guid == _wotsitStopGuid)
        {
            _plugin.StopSong();
        }
    }

    public void Dispose()
    {
        _wotsitUnregister.InvokeFunc(IpcDisplayName);
    }
}