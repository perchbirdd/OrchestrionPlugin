using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Logging;
using Orchestrion.Persistence;
using Orchestrion.Struct;

namespace Orchestrion.Audio;

public static class PlaylistManager
{
	public static bool IsPlaying { get; private set; }
	public static Playlist CurrentPlaylist => Configuration.Instance.Playlists.GetValueOrDefault(_currentPlaylist, null);
	public static int CurrentSongId => CurrentPlaylist?.Songs[_currentSongIndex] ?? 0;
	public static Song CurrentSong => SongList.Instance.GetSong(CurrentPlaylist?.Songs[_currentSongIndex] ?? 0);
	public static TimeSpan ElapsedDuration => TimeSpan.FromMilliseconds(ElapsedMs);
	public static TimeSpan Duration => CurrentSong.Duration;

	private static string _currentPlaylist = string.Empty;
	private static int _currentSongIndex;
	private static int _playlistTrackPlayCount;
	private static long _currentSongStartTime;
	private static long CurrentSongEndTime => IsPlaying ? 0 : _currentSongStartTime + (int) CurrentSong.Duration.TotalMilliseconds;
	private static long ElapsedMs => Environment.TickCount64 - _currentSongStartTime;
	private static long RemainingMs => CurrentSongEndTime - Environment.TickCount64;

	static PlaylistManager()
	{
		DalamudApi.Framework.Update += Update;
	}
	
	public static void Dispose()
	{
		DalamudApi.Framework.Update -= Update;
	}

	private static void Update(Framework ignore)
	{
		try
		{
			if (_currentPlaylist == string.Empty || CurrentPlaylist == null) return;
			if (ElapsedDuration <= Duration) return;
			PluginLog.Debug($"{ElapsedDuration} > {Duration}");

			Next();
		}
		catch (Exception) { }
	}
	
	public static void Play(string playlistName)
	{
		_currentPlaylist = playlistName;
		_currentSongIndex = -1;
		IsPlaying = true;

		Next();
	}

	public static void Next()
	{
		if (_currentPlaylist == string.Empty || CurrentPlaylist == null) return;
		
		var nextSong = GetNextSong();
		if (nextSong == 0)
			Stop();
		else
		{
			PluginLog.Debug($"[PlaylistManager] [Play] Playing {nextSong}");
			BGMManager.Play(nextSong);
			_currentSongStartTime = Environment.TickCount64;
		}
	}
	
	public static void Stop()
	{
		BGMManager.Stop();
		IsPlaying = false;
		_currentPlaylist = "";
		_currentSongIndex = -1;
		_currentSongStartTime = 0;
		_playlistTrackPlayCount = 0;
	}

	private static int GetNextSong()
	{
		if (!IsPlaying) return 0;

		_playlistTrackPlayCount++;

		PluginLog.Debug($"[PlaylistManager] [GetNextSong] CurrentPlaylist.RepeatMode: {CurrentPlaylist?.RepeatMode} CurrentPlaylist.ShuffleMode: {CurrentPlaylist?.ShuffleMode}");
		switch (CurrentPlaylist?.RepeatMode)
		{
			case RepeatMode.One:
				return CurrentPlaylist.Songs[++_currentSongIndex];
			case RepeatMode.All when CurrentPlaylist.ShuffleMode == ShuffleMode.Off:
				_currentSongIndex = ++_currentSongIndex % CurrentPlaylist.Songs.Count;
				return CurrentPlaylist.Songs[_currentSongIndex];
			case RepeatMode.All when CurrentPlaylist.ShuffleMode == ShuffleMode.On:
				_currentSongIndex = Random.Shared.Next(CurrentPlaylist.Songs.Count);
				return CurrentPlaylist.Songs[_currentSongIndex];
			case RepeatMode.Once when _playlistTrackPlayCount < CurrentPlaylist.Songs.Count:
				return CurrentPlaylist.Songs[++_currentSongIndex];
			case RepeatMode.Once when _playlistTrackPlayCount >= CurrentPlaylist.Songs.Count:
				return 0;
			default:
				return 0;
		}
	}

	private static int GetPreviousSong()
	{
		return 0;
	}
}