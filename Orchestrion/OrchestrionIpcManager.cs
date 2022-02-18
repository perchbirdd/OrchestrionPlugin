using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Plugin.Ipc;

namespace Orchestrion;

public class OrchestrionIpcManager : IDisposable
{
    private readonly OrchestrionPlugin _plugin;
    private List<Song> _songListCache;

    private const string IpcDisplayName = "Orchestrion Plugin";
    private const uint WotsitIconId = 67;

    private ICallGateSubscriber<string, string, string, uint, string> _wotsitRegister;
    private ICallGateSubscriber<string, bool> _wotsitUnregister;
    private Dictionary<string, Song> _wotsitSongIpcs;
    private string _wotsitRandomGuid;
    private string _wotsitRandomFavoriteGuid;
    private string _wotsitStopGuid;

    private const string PlayRandom = "Play a random track";
    private const string PlayRandomFavorites = "Play a random track from favorites";
    private const string Stop = "Stop playing";

    private ICallGateProvider<int> _currentSongProvider;
    private ICallGateProvider<int, bool> _playSongProvider;
    private ICallGateProvider<int, bool> _orchSongChangeProvider;
    private ICallGateProvider<int, bool> _songChangeProvider;
    private ICallGateProvider<int, Song> _songInfoProvider;
    private ICallGateProvider<List<Song>> _allSongInfoProvider;

    public OrchestrionIpcManager(OrchestrionPlugin plugin)
    {
        _plugin = plugin;
        _songListCache = SongList.GetSongs().Select(x => x.Value).ToList();

        InitForSelf();

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

    private void InitForSelf()
    {
        _currentSongProvider = OrchestrionPlugin.PluginInterface.GetIpcProvider<int>("Orch.CurrentSong");
        _currentSongProvider.RegisterFunc(CurrentSongFunc);
        
        _playSongProvider = OrchestrionPlugin.PluginInterface.GetIpcProvider<int, bool>("Orch.PlaySong");
        _playSongProvider.RegisterFunc(PlaySongFunc);
        
        _songInfoProvider = OrchestrionPlugin.PluginInterface.GetIpcProvider<int, Song>("Orch.SongInfo");
        _songInfoProvider.RegisterFunc(songId => SongList.SongExists(songId) ? SongList.GetSong(songId) : default);
        
        _allSongInfoProvider = OrchestrionPlugin.PluginInterface.GetIpcProvider<List<Song>>("Orch.AllSongInfo");
        _allSongInfoProvider.RegisterFunc(() => _songListCache);
        
        _orchSongChangeProvider = OrchestrionPlugin.PluginInterface.GetIpcProvider<int, bool>("Orch.OrchSongChange");
        _songChangeProvider = OrchestrionPlugin.PluginInterface.GetIpcProvider<int, bool>("Orch.SongChange");

        PluginLog.Verbose("Firing Orch.Available.");
        var cgAvailable = OrchestrionPlugin.PluginInterface.GetIpcProvider<bool>("Orch.Available");
        cgAvailable.SendMessage();
    }

    private int CurrentSongFunc()
    {
        if (BGMController.PlayingSongId == 0) return BGMController.CurrentSongId;
            
        if (OrchestrionPlugin.Configuration.SongReplacements.TryGetValue(BGMController.CurrentSongId, out var replacement)
            && replacement.ReplacementId == SongReplacement.NoChangeId)
            return BGMController.SecondSongId;
        return BGMController.PlayingSongId;
    }

    private bool PlaySongFunc(int songId)
    {
        if (songId == 0) 
            _plugin.StopSong();
        else
            _plugin.PlaySong(songId);
        return true;
    }

    public void InvokeOrchSongChanged(int song)
    {
        _orchSongChangeProvider.SendMessage(song);
    }

    public void InvokeSongChanged(int song)
    {
        _songChangeProvider.SendMessage(song);
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
            var guid = _wotsitRegister.InvokeFunc(IpcDisplayName, $"Play {song.Value.Name}", GetSearchString(song.Value), WotsitIconId);
            _wotsitSongIpcs.Add(guid, song.Value);  
        }
        
        _wotsitRandomGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, PlayRandom, PlayRandom, WotsitIconId);
        _wotsitRandomFavoriteGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, PlayRandomFavorites, PlayRandomFavorites, WotsitIconId);
        _wotsitStopGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, Stop, Stop, WotsitIconId);

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
        _wotsitUnregister?.InvokeFunc(IpcDisplayName);
        _currentSongProvider?.UnregisterFunc();
        _playSongProvider?.UnregisterFunc();
        _orchSongChangeProvider?.UnregisterFunc();
        _songChangeProvider?.UnregisterFunc();
    }
}