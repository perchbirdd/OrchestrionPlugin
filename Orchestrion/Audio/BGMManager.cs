using CheapLoc;
using Dalamud.Game;
using Dalamud.Logging;
using Orchestrion.BGMSystem;
using Orchestrion.Ipc;
using Orchestrion.Persistence;
using Orchestrion.Struct;

namespace Orchestrion.Audio;

public static class BGMManager
{
	private static readonly BGMController _bgmController;
    private static readonly OrchestrionIpcManager _ipcManager;
    
    private static bool _isPlayingReplacement;
    private static string _ddPlaylist;

    public delegate void SongChanged(int oldSong, int currentSong, int oldSecondSong, int oldCurrentSong, bool oldPlayedByOrch, bool playedByOrchestrion);
    public static event SongChanged OnSongChanged;
    
    public static int CurrentSongId => _bgmController.CurrentSongId;
    public static int PlayingSongId => _bgmController.PlayingSongId;
    public static int CurrentAudibleSong => _bgmController.CurrentAudibleSong;
    public static int PlayingScene => _bgmController.PlayingScene;
    
    static BGMManager()
	{
        _bgmController = new BGMController();
        _ipcManager = new OrchestrionIpcManager();

        DalamudApi.Framework.Update += Update;
        _bgmController.OnSongChanged += HandleSongChanged;
        OnSongChanged += IpcUpdate;
    }

    public static void Dispose()
    {
        DalamudApi.Framework.Update -= Update;
        Stop();
        _bgmController.Dispose();
    }

    private static void IpcUpdate(int oldSong, int newSong, int oldSecondSong, int oldCurrentSong, bool oldPlayedByOrch, bool playedByOrch)
    {
        _ipcManager.InvokeSongChanged(newSong);
        if (playedByOrch) _ipcManager.InvokeOrchSongChanged(newSong);
    }
    
    public static void Update(Framework ignored)
    {
        _bgmController.Update();
    }
    
    private static void HandleSongChanged(int oldSong, int newSong, int oldSecondSong, int newSecondSong)
    {
        var currentChanged = oldSong != newSong;
        var secondChanged = oldSecondSong != newSecondSong;
        
        var newHasReplacement = Configuration.Instance.SongReplacements.TryGetValue(newSong, out var newSongReplacement);
        var newSecondHasReplacement = Configuration.Instance.SongReplacements.TryGetValue(newSecondSong, out var newSecondSongReplacement);
        
        if (currentChanged)
            PluginLog.Debug($"[HandleSongChanged] Current Song ID changed from {_bgmController.OldSongId} to {_bgmController.CurrentSongId}");
        
        if (secondChanged)
            PluginLog.Debug($"[HandleSongChanged] Second song ID changed from {_bgmController.OldSecondSongId} to {_bgmController.SecondSongId}");
        
        if (PlayingSongId != 0 && DeepDungeonModeActive())
        {
            if (!string.IsNullOrEmpty(_ddPlaylist) && !Configuration.Instance.Playlists.ContainsKey(_ddPlaylist)) // user deleted playlist
                Stop();
            else
                PlayRandomSong(_ddPlaylist);
            return;
        }

        if (PlayingSongId != 0 && !_isPlayingReplacement) return; // manually playing track
        if (secondChanged && !currentChanged && !_isPlayingReplacement) return; // don't care about behind song if not playing replacement

        if (!newHasReplacement) // user isn't playing and no replacement at all
        {
            if (PlayingSongId != 0)
                Stop();
            else
                // This is the only place in this method where we invoke OnSongChanged, as Play and Stop do it themselves
                InvokeSongChanged(oldSong, newSong, oldSecondSong, newSecondSong, oldPlayedByOrch: false, playedByOrch: false);
            return;
        }

        var toPlay = 0;
        
        PluginLog.Debug($"[HandleSongChanged] Song ID {newSong} has a replacement of {newSongReplacement.ReplacementId}");
        
        // Handle 2nd changing when 1st has replacement of NoChangeId, only time it matters
        if (newSongReplacement.ReplacementId == SongReplacementEntry.NoChangeId)
        {
            if (secondChanged)
            {
                toPlay = newSecondSong;
                if (newSecondHasReplacement) toPlay = newSecondSongReplacement.ReplacementId;
                if (toPlay == SongReplacementEntry.NoChangeId) return; // give up
            }    
        }
            
        if (newSongReplacement.ReplacementId == SongReplacementEntry.NoChangeId && PlayingSongId == 0)
        {
            toPlay = oldSong; // no net BGM change
        }
        else if (newSongReplacement.ReplacementId != SongReplacementEntry.NoChangeId)
        {
            toPlay = newSongReplacement.ReplacementId;
        }
        
        Play(toPlay, isReplacement: true); // we only ever play a replacement here
    }

    public static void Play(int songId, bool isReplacement = false)
    {
        var wasPlaying = PlayingSongId != 0;
        var oldSongId = CurrentAudibleSong;
        var secondSongId = _bgmController.SecondSongId;
        
        PluginLog.Debug($"[Play] Playing {songId}");
        InvokeSongChanged(oldSongId, songId, secondSongId, oldSongId, oldPlayedByOrch: wasPlaying, playedByOrch: true);
        _bgmController.SetSong((ushort)songId);
        _isPlayingReplacement = isReplacement;
    }

    public static void Stop()
    {
        if (PlaylistManager.IsPlaying)
        {
            PluginLog.Debug("[Stop] Stopping playlist...");    
            PlaylistManager.Reset();
        }

        _ddPlaylist = null;
        
        if (PlayingSongId == 0) return;
        PluginLog.Debug($"[Stop] Stopping playing {_bgmController.PlayingSongId}...");

        if (Configuration.Instance.SongReplacements.TryGetValue(_bgmController.CurrentSongId, out var replacement))
        {
            PluginLog.Debug($"[Stop] Song ID {_bgmController.CurrentSongId} has a replacement of {replacement.ReplacementId}...");

            var toPlay = replacement.ReplacementId;
            
            if (toPlay == SongReplacementEntry.NoChangeId)
                toPlay = _bgmController.OldSongId;

            // Play will invoke OnSongChanged for us
            Play(toPlay, isReplacement: true);
            return;
        }

        var second = _bgmController.SecondSongId;
        
        // If there was no replacement involved, we don't need to do anything else, just stop
        InvokeSongChanged(PlayingSongId, CurrentSongId, second, second, oldPlayedByOrch: true, playedByOrch: false);
        _bgmController.SetSong(0);
    }

    private static void InvokeSongChanged(int oldSongId, int newSongId, int oldSecondSongId, int newSecondSongId, bool oldPlayedByOrch, bool playedByOrch)
    {
        PluginLog.Debug($"[InvokeSongChanged] Invoking SongChanged event with {oldSongId} -> {newSongId}, {oldSecondSongId} -> {newSecondSongId} | {oldPlayedByOrch} {playedByOrch}");
        OnSongChanged?.Invoke(oldSongId, newSongId, oldSecondSongId, newSecondSongId, oldPlayedByOrch, playedByOrch);
    }

    public static void PlayRandomSong(string playlistName = "")
    {
        if (SongList.Instance.TryGetRandomSong(playlistName, out var randomFavoriteSong))
            Play(randomFavoriteSong, isReplacement: true);
        else
            DalamudApi.ChatGui.PrintError(Loc.Localize("NoPossibleSongs", "No possible songs found."));
    }

    public static void StartDeepDungeonMode(string playlistName = "")
    {
        _ddPlaylist = playlistName;
        PlayRandomSong(_ddPlaylist);
    }

    public static void StopDeepDungeonMode()
    {
        _ddPlaylist = null;
        Stop();
    }

    public static bool DeepDungeonModeActive()
    {
        return _ddPlaylist != null;
    }
}