using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Plugin.Ipc;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Struct;

namespace Orchestrion.Ipc;

public class OrchestrionIpcManager : IDisposable
{
    private const string IpcDisplayName = "Orchestrion Plugin";
    private const string PlayRandom = "Play a random track";
    private const string PlayRandomFavorites = "Play a random track from favorites";
    private const string Stop = "Stop playing";
    private const uint WotsitIconId = 67;
    
    private readonly List<Song> _songListCache;
    
    private ICallGateSubscriber<string, string, string, uint, string> _wotsitRegister;
    private ICallGateSubscriber<string, bool> _wotsitUnregister;
    private Dictionary<string, Song> _wotsitSongIpcs;
    private string _wotsitRandomGuid;
    private string _wotsitRandomFavoriteGuid;
    private string _wotsitStopGuid;
    
    private ICallGateProvider<int> _currentSongProvider;
    private ICallGateProvider<int, bool> _playSongProvider;
    private ICallGateProvider<int, bool> _orchSongChangeProvider;
    private ICallGateProvider<int, bool> _songChangeProvider;
    private ICallGateProvider<int, Song> _songInfoProvider;
    private ICallGateProvider<List<Song>> _allSongInfoProvider;

    public OrchestrionIpcManager()
    {
        _songListCache = SongList.Instance.GetSongs().Select(x => x.Value).ToList();

        InitForSelf();

        try
        {
            InitForWotsit();
        }
        catch (Exception)
        {
            // ignored
        }

        var wotsitAvailable = DalamudApi.PluginInterface.GetIpcSubscriber<bool>("FA.Available");
        wotsitAvailable.Subscribe(InitForWotsit);
    }

    private void InitForSelf()
    {
        _currentSongProvider = DalamudApi.PluginInterface.GetIpcProvider<int>("Orch.CurrentSong");
        _currentSongProvider.RegisterFunc(CurrentSongFunc);
        
        _playSongProvider = DalamudApi.PluginInterface.GetIpcProvider<int, bool>("Orch.PlaySong");
        _playSongProvider.RegisterFunc(PlaySongFunc);
        
        _songInfoProvider = DalamudApi.PluginInterface.GetIpcProvider<int, Song>("Orch.SongInfo");
        _songInfoProvider.RegisterFunc(songId => SongList.Instance.SongExists(songId) ? SongList.Instance.GetSong(songId) : default);
        
        _allSongInfoProvider = DalamudApi.PluginInterface.GetIpcProvider<List<Song>>("Orch.AllSongInfo");
        _allSongInfoProvider.RegisterFunc(() => _songListCache);
        
        _orchSongChangeProvider = DalamudApi.PluginInterface.GetIpcProvider<int, bool>("Orch.OrchSongChange");
        _songChangeProvider = DalamudApi.PluginInterface.GetIpcProvider<int, bool>("Orch.SongChange");

        DalamudApi.PluginLog.Verbose("[InitForSelf] Firing Orch.Available.");
        var cgAvailable = DalamudApi.PluginInterface.GetIpcProvider<bool>("Orch.Available");
        cgAvailable.SendMessage();
    }

    private int CurrentSongFunc()
    {
        return BGMManager.CurrentAudibleSong;
        // if (_bgmController.PlayingSongId == 0) return BGMController.CurrentSongId;
        //     
        // if (OrchestrionPlugin.Configuration.SongReplacements.TryGetValue(BGMController.CurrentSongId, out var replacement)
        //     && replacement.ReplacementId == SongReplacementEntry.NoChangeId)
        //     return BGMController.SecondSongId;
        // return BGMController.PlayingSongId;
    }

    private bool PlaySongFunc(int songId)
    {
        if (songId == 0) 
            BGMManager.Stop();
        else
            BGMManager.Play(songId);
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
        _wotsitRegister = DalamudApi.PluginInterface.GetIpcSubscriber<string, string, string, uint, string>("FA.RegisterWithSearch");
        _wotsitUnregister = DalamudApi.PluginInterface.GetIpcSubscriber<string, bool>("FA.UnregisterAll");
        
        var subscribe = DalamudApi.PluginInterface.GetIpcSubscriber<string, bool>("FA.Invoke");
        subscribe.Subscribe(WotsitInvoke);
        
        _wotsitSongIpcs = new Dictionary<string, Song>();
        
        foreach (var song in _songListCache)
        {
            var guid = _wotsitRegister.InvokeFunc(IpcDisplayName, $"Play {song.Name}", GetSearchString(song), WotsitIconId);
            _wotsitSongIpcs.Add(guid, song);  
        }
        
        _wotsitRandomGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, PlayRandom, PlayRandom, WotsitIconId);
        _wotsitRandomFavoriteGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, PlayRandomFavorites, PlayRandomFavorites, WotsitIconId);
        _wotsitStopGuid = _wotsitRegister.InvokeFunc(IpcDisplayName, Stop, Stop, WotsitIconId);

        DalamudApi.PluginLog.Debug($"[InitForWotsit] Registered {_wotsitSongIpcs.Count} songs with Wotsit");
    }

    private string GetSearchString(Song song)
    {
        return $"Play {song.Name} {song.Locations} {song.AdditionalInfo}";
    }

    private void WotsitInvoke(string guid)
    {
        if (_wotsitSongIpcs.TryGetValue(guid, out var song))
        {
            BGMManager.Play(song.Id);
        }
        else if (guid == _wotsitRandomGuid)
        {
            BGMManager.PlayRandomSong();
        }
        // else if (guid == _wotsitRandomFavoriteGuid)
        // {
        //     BGMManager.PlayRandomSong(restrictToFavorites: true);
        // }
        else if (guid == _wotsitStopGuid)
        {
            BGMManager.Stop();
        }
    }

    public void Dispose()
    {
        try
        {
            _wotsitUnregister?.InvokeFunc(IpcDisplayName);
        }
        catch (Exception)
        {
            // Wotsit was not installed or too early version
        }
        
        _currentSongProvider?.UnregisterFunc();
        _playSongProvider?.UnregisterFunc();
        _orchSongChangeProvider?.UnregisterFunc();
        _songChangeProvider?.UnregisterFunc();
    }
}