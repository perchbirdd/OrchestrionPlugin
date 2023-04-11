﻿using Dalamud.Game;
using Dalamud.Logging;
using Orchestrion.Game.BGMSystem;
using Orchestrion.Ipc;
using Orchestrion.Struct;

namespace Orchestrion.Game;

public static class BGMManager
{
	private static readonly BGMController _bgmController;
    private static readonly OrchestrionIpcManager _ipcManager;
    
    private static bool _isPlayingReplacement;
	
    public delegate void SongChanged(int oldSong, int currentSong, bool playedByOrchestrion);
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
        Stop();
        _bgmController.Dispose();
    }

    private static void IpcUpdate(int oldSong, int newSong, bool playedByOrch)
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
        
        if (PlayingSongId != 0 && !_isPlayingReplacement) return; // manually playing track
        if (secondChanged && !currentChanged && !_isPlayingReplacement) return; // don't care about behind song if not playing replacement
        if (!newHasReplacement) // user isn't playing and no replacement at all
        {
            if (PlayingSongId != 0)
                Stop();
            else
                // This is the only place in this method where we invoke OnSongChanged, as Play and Stop do it themselves
                OnSongChanged?.Invoke(oldSong, newSong, playedByOrchestrion: false);
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
        var oldSongId = CurrentAudibleSong;
        if (oldSongId == songId) return;

        PluginLog.Debug($"[Play] Playing {songId}");
        OnSongChanged?.Invoke(oldSongId, songId, playedByOrchestrion: true);
        _bgmController.SetSong((ushort)songId);
        _isPlayingReplacement = isReplacement;
    }

    public static void Stop()
    {
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

        // If there was no replacement involved, we don't need to do anything else, just stop
        OnSongChanged?.Invoke(PlayingSongId, CurrentSongId, playedByOrchestrion: false);
        _bgmController.SetSong(0);
    }
    
    public static void PlayRandomSong(bool restrictToFavorites = false)
    {
        if (SongList.Instance.TryGetRandomSong(restrictToFavorites, out var randomFavoriteSong))
            Play(randomFavoriteSong);
        else
            DalamudApi.ChatGui.PrintError("No possible songs found.");
    }
}