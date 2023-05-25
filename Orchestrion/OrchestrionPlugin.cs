using System.IO;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System.Linq;
using System.Reflection;
using CheapLoc;
using Dalamud.Game;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.BGMSystem;
using Orchestrion.Persistence;
using Orchestrion.UI.Windows;
using MainWindow = Orchestrion.UI.Windows.MainWindow.MainWindow;

namespace Orchestrion;

// ReSharper disable once ClassNeverInstantiated.Global
public class OrchestrionPlugin : IDalamudPlugin
{
	private const string ConstName = "Orchestrion";
	private const string CommandName = "/porch";
	private const string NativeNowPlayingPrefix = "♪ ";

	public static ImFontPtr LargeFont { get; private set; }

	public string Name => ConstName;

	private readonly WindowSystem _windowSystem;
	private readonly MiniPlayerWindow _miniPlayerWindow;
	private readonly MainWindow _mainWindow;
	private readonly SettingsWindow _settingsWindow;

	private readonly DtrBarEntry _dtrEntry;

	private SeString _songEchoMsg;

	public OrchestrionPlugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
	{
		DalamudApi.Initialize(pluginInterface);
		LanguageChanged(pluginInterface.UiLanguage);

		if (Configuration.Instance.ShowSongInNative)
		{
			_dtrEntry = DalamudApi.DtrBar.Get(ConstName);
		}

		BGMAddressResolver.Init();
		BGMManager.OnSongChanged += OnSongChanged;

		_windowSystem = new WindowSystem();
		_mainWindow = new MainWindow(this);
		_settingsWindow = new SettingsWindow();
		_miniPlayerWindow = new MiniPlayerWindow();

		_windowSystem.AddWindow(_mainWindow);
		_windowSystem.AddWindow(_settingsWindow);
		_windowSystem.AddWindow(_miniPlayerWindow);

		DalamudApi.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = Loc.Localize("HelpMessage", "Displays the Orchestrion window, to view, change, or stop in-game BGM."),
		});

		DalamudApi.PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
		DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += OpenSettingsWindow;

		DalamudApi.Framework.Update += OrchestrionUpdate;
		DalamudApi.ClientState.Logout += ClientStateOnLogout;

		DalamudApi.PluginInterface.UiBuilder.BuildFonts += BuildFonts;
		DalamudApi.PluginInterface.UiBuilder.RebuildFonts();

		DalamudApi.PluginInterface.LanguageChanged += LanguageChanged;
	}

	private void LanguageChanged(string code)
	{
		var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Orchestrion.Loc.{code}.json");
		if (stream == null) return;
		var content = new StreamReader(stream).ReadToEnd();
		Loc.Setup(content);
	}

	private void BuildFonts()
	{
		LargeFont = DalamudApi.PluginInterface.UiBuilder.GetGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 24 * ImGuiHelpers.GlobalScale)).ImFont;
	}

	public void Dispose()
	{
		_mainWindow.Dispose();
		DalamudApi.Framework.Update -= OrchestrionUpdate;
		DalamudApi.PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
		DalamudApi.PluginInterface.UiBuilder.BuildFonts -= BuildFonts;
		DalamudApi.CommandManager.RemoveHandler(CommandName);
		_dtrEntry?.Dispose();
		PlaylistManager.Dispose();
		BGMManager.Dispose();
	}

	private void OrchestrionUpdate(Framework ignored)
	{
		if (_songEchoMsg == null || IsLoadingScreen()) return;

		DalamudApi.ChatGui.PrintChat(new XivChatEntry
		{
			Message = _songEchoMsg,
			Type = DalamudApi.PluginInterface.GeneralChatType,
		});

		_songEchoMsg = null;
	}

	private void ClientStateOnLogout(object sender, EventArgs e)
	{
		BGMManager.Stop();
	}

	private void OnSongChanged(int oldSong, int newSong, bool playedByOrch)
	{
		UpdateDtr(newSong, playedByOrch: playedByOrch);
		UpdateChat(newSong, playedByOrch: playedByOrch);
	}

	public void OpenMainWindow()
	{
		_mainWindow.IsOpen = true;
	}

	public void OpenSettingsWindow()
	{
		_settingsWindow.IsOpen = true;
	}

	private void OnCommand(string command, string args)
	{
		var argSplit = args.Split(' ');
		var argLen = argSplit.Length;

		// print args to log
		PluginLog.Log($"command: {command} args: {args}");
		var argString = "['" + string.Join("', '", argSplit) + "']";
		PluginLog.Log($"argLen: {argLen} argSplit: {argString}");
		
		var mainArg = argSplit[0].ToLowerInvariant();

		switch (argLen)
		{
			case 1:
				PluginLog.Verbose("case 1");
				switch (mainArg)
				{
					case "":
						_mainWindow.IsOpen = !_mainWindow.IsOpen;
						break;
					case "help":
						PrintHelp();
						break;
					case "stop":
						BGMManager.Stop();
						break;
					case "play":
						DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("MustSpecifySong", "You must specify a song to play.")));
						break;
					case "random":
						BGMManager.PlayRandomSong();
						break;
					case "next":
						if (PlaylistManager.CurrentPlaylist == null)
							DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("NoPlaylistPlaying", "No playlist is currently playing.")));
						else
							PlaylistManager.Next();
						break;
					case "previous":
						if (PlaylistManager.CurrentPlaylist == null)
							DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("NoPlaylistPlaying", "No playlist is currently playing.")));
						else
							PlaylistManager.Previous();
						break;
					case "shuffle":
						DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("NoShuffleModeSpecified", "Please specify a shuffle mode.")));
						break;
					case "repeat":
						DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("NoRepeatModeSpecified", "Please specify a repeat mode.")));
						break;
				}
				break;
			case 2:
				PluginLog.Verbose("case 2");
				var arg2 = argSplit[1].ToLowerInvariant();
				switch (mainArg)
				{
					case "random":
						BGMManager.PlayRandomSong(argSplit[1]);
						break;
					case "play" when int.TryParse(argSplit[1], out var songId):
						if (SongList.Instance.SongExists(songId))
							BGMManager.Play(songId);
						else
							DalamudApi.ChatGui.PrintError(BuildChatMessage(string.Format(Loc.Localize("SongIdNotFound", "Song ID {0} not found."), songId)));
						break;
					case "play" when !int.TryParse(argSplit[1], out var songId):
						PluginLog.Verbose("play by song name");
						HandlePlayBySongName(argSplit);
						break;
					case "shuffle":
						if (!Enum.TryParse<ShuffleMode>(arg2, true, out var shuffleMode))
						{
							DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("InvalidMode", "The specified mode is invalid.")));
							break;
						}
						
						if (PlaylistManager.CurrentPlaylist == null)
							DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("NoPlaylistPlaying", "No playlist is currently playing.")));
						else
							PlaylistManager.CurrentPlaylist.ShuffleMode = shuffleMode;
						break;
					case "repeat":
						if (!Enum.TryParse<RepeatMode>(arg2, true, out var repeatMode))
						{
							DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("InvalidMode", "The specified mode is invalid.")));
							break;
						}
						
						if (PlaylistManager.CurrentPlaylist == null)
						{
							DalamudApi.ChatGui.PrintError(BuildChatMessage(Loc.Localize("NoPlaylistPlaying", "No playlist is currently playing.")));
						}
						else
						{
							PlaylistManager.CurrentPlaylist.ShuffleMode = ShuffleMode.Off;
							PlaylistManager.CurrentPlaylist.RepeatMode = repeatMode;
						}
						break;	
				}
				break;
			case >= 3 when argSplit[1].ToLowerInvariant() == "playlist":
				PluginLog.Verbose("case >= 3 when argSplit[1].ToLowerInvariant() == playlist");
				var playlistName = argSplit.Skip(2).Aggregate((x, y) => $"{x} {y}");
				
				var playlistExists = Configuration.Instance.TryGetPlaylist(playlistName, out var playlist);
				if (!playlistExists)
				{
					DalamudApi.ChatGui.PrintError(BuildChatMessageFormatted(Loc.Localize("PlaylistNotFound", "Playlist <i>{0}</i> not found."), playlistName, false));
					break;
				}

				var arg = argSplit[0].ToLowerInvariant();
				switch (arg)
				{
					case "play":
						PlaylistManager.Play(playlistName);
						break;
					case "shuffle":
						playlist.ShuffleMode = ShuffleMode.On;
						PlaylistManager.Play(playlistName);
						break;
					case "repeat":
						playlist.ShuffleMode = ShuffleMode.Off;
						playlist.RepeatMode = RepeatMode.All;
						PlaylistManager.Play(playlistName);
						break;
				}
				break;
			case >= 2 when argSplit[0].ToLowerInvariant() == "play" && !int.TryParse(argSplit[1], out _):
				PluginLog.Verbose("case >= 2 when argSplit[0].ToLowerInvariant() == play && !int.TryParse(argSplit[1], out _)");
				HandlePlayBySongName(argSplit);
				break;
			default:
				PrintHelp();
				break;
		}
	}

	private void HandlePlayBySongName(string[] argSplit)
	{
		var songName = argSplit.Skip(1).Aggregate((x, y) => $"{x} {y}");
		if (SongList.Instance.TryGetSongByName(songName, out var songIdFromName))
		{
			BGMManager.Play(songIdFromName);
		}
		else
		{
			DalamudApi.ChatGui.PrintError(
				BuildChatMessageFormatted(
					Loc.Localize("SongNameNotFound", "Song <i>{0}</i> not found."),
					songName,
					false)
			);
		}
	}

	private void PrintHelp()
	{
		DalamudApi.ChatGui.Print(BuildChatMessage(Loc.Localize("HelpColon", "Help:")));
		DalamudApi.ChatGui.Print(BuildChatMessage(Loc.Localize("GeneralCommandsColon", "General Commands:")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch help - " + Loc.Localize("HelpDisplayThisMessage", "Display this message")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch - " + Loc.Localize("HelpOpenOrchestrionWindow", "Open the Orchestrion window")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch play [songId] - " + Loc.Localize("HelpPlaySongWithId", "Play the song with the specified ID")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch play [song name] - " + Loc.Localize("HelpPlaySongWithName", "Play the song with the specified name (both English and Japanese titles work)")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch random - " + Loc.Localize("HelpPlayRandomSong", "Play a random song")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch stop - " + Loc.Localize("HelpStopSong", "Stop the current playing song, replacement song, or playlist")));
		DalamudApi.ChatGui.Print(BuildChatMessage(Loc.Localize("PlaylistCommandsColon", "Playlist Commands:")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch random [playlist] - " + Loc.Localize("HelpPlayRandomSongFromPlaylist", "Play a random song from the specified playlist (does not begin the playlist)")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch play playlist [playlist name] - " + Loc.Localize("HelpPlayPlaylist", "Play the specified playlist with its current settings")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch shuffle playlist [playlist name] - " + Loc.Localize("HelpPlayPlaylistShuffle", "Play the specified playlist, changing the playlist's settings to shuffle")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch repeat playlist [playlist name] - " + Loc.Localize("HelpPlayPlaylistRepeat", "Play the specified playlist, changing the playlist's settings to 'repeat all'")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch shuffle [on, off] - " + Loc.Localize("HelpPlaylistShuffle", "Set the current playlist to the specified shuffle mode")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch repeat [all, one, once] - " + Loc.Localize("HelpPlaylistRepeat", "Set the current playlist to the specified repeat mode")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch next - " + Loc.Localize("HelpPlaylistNext", "Play the next song in the current playlist")));
		DalamudApi.ChatGui.Print(BuildChatMessage("/porch previous - " + Loc.Localize("HelpPlaylistPrevious", "Play the previous song in the current playlist")));
	}

	private void UpdateDtr(int songId, bool playedByOrch = false)
	{
		if (!Configuration.Instance.ShowSongInNative) return;
		if (_dtrEntry == null) return;

		if (!SongList.Instance.TryGetSong(songId, out var song))
			return;

		var songName = GetClientSongName(songId);

		if (string.IsNullOrEmpty(songName)) return;

		var suffix = "";
		if (Configuration.Instance.ShowIdInNative)
		{
			if (!string.IsNullOrEmpty(songName))
				suffix = " - ";
			suffix += $"{songId}";
		}

		var text = songName + suffix;

		text = playedByOrch ? $"{NativeNowPlayingPrefix} [{text}]" : $"{NativeNowPlayingPrefix} {text}";
		
		_dtrEntry.Text = text;
	}

	private void UpdateChat(int songId, bool playedByOrch = false)
	{
		if (!Configuration.Instance.ShowSongInChat) return;

		var songName = GetClientSongName(songId);

		// the actual echoing is done during framework update
		if (!string.IsNullOrEmpty(songName))
			_songEchoMsg = BuildChatMessageFormatted(Loc.Localize("NowPlayingEcho", "Now playing <i>{0}</i>."), songName, playedByOrch);
	}

	private string GetClientSongName(int songId)
	{
		if (!SongList.Instance.TryGetSong(songId, out var song))
			return null;
		
		var songName = Configuration.Instance.UseClientLangInServerInfo
			? song.Strings[Util.ClientLangCode()].Name
			: song.Name;

		return songName;
	}

	private unsafe bool IsLoadingScreen()
	{
		var titleCard = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_LocationTitle");
		var blackScreen = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("FadeMiddle");
		return titleCard != null && titleCard->IsVisible || blackScreen != null && blackScreen->IsVisible;
	}

	private SeString BuildChatMessage(string message)
	{
		return new SeStringBuilder()
			.AddUiForeground("[Orchestrion] ", 35)
			.AddText(message)
			.Build();
	}

	private SeString BuildChatMessageFormatted(string message, string param, bool playedByOrch)
	{
		var tmp1 = message.Split("<i>");
		var tmp2 = tmp1[1].Split("</i>");
		var pre = tmp1[0];
		var mid = tmp2[0];
		var post = tmp2[1];

		var midFormatAddtl = playedByOrch ? $"[{mid}]" : $"{mid}";
		
		return new SeStringBuilder()
			.AddUiForeground("[Orchestrion] ", 35)
			.AddText(pre)
			.AddItalics(string.Format(midFormatAddtl, param))
			.AddText(post)
			.Build();
	}
}