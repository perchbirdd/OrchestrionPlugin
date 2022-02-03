using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Logging;

namespace Orchestrion;

// TODO:
// try to find what writes to bgm 0, block it if we are playing?
//   or save/restore if we preempt it?
// debug info of which priority is active
//  notifications/logs of changes even to lower priorities?

public class OrchestrionPlugin : IDalamudPlugin
{
    private const string ConstName = "Orchestrion";
    public string Name => ConstName;

    private const string SongListFile = "xiv_bgm.csv";
    private const string CommandName = "/porch";
    private const string NativeNowPlayingPrefix = "♪ ";

    public DalamudPluginInterface PluginInterface { get; }
    public CommandManager CommandManager { get; }
    public ChatGui ChatGui { get; }
    public Framework Framework { get; }
    public DtrBar DtrBar { get; }
    
    public NativeUIUtil NativeUI { get; }
    public Configuration Configuration { get; }
    public SongUI SongUI { get; }

    private readonly TextPayload nowPlayingPayload = new("Orchestrion: Now playing ");
    private readonly TextPayload periodPayload = new(".");
    private readonly TextPayload emptyPayload = new("");
    private readonly TextPayload leftBracketPayload = new("[");
    private readonly TextPayload rightBracketPayload = new("]");

    private bool isPlayingReplacement = false;
    private DtrBarEntry dtrEntry;

    public int CurrentSong => BGMController.PlayingSongId == 0 ? BGMController.CurrentSongId : BGMController.PlayingSongId;

    public OrchestrionPlugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] GameGui gameGui,
        [RequiredVersion("1.0")] ChatGui chatGui,
        [RequiredVersion("1.0")] DtrBar dtrBar,
        [RequiredVersion("1.0")] CommandManager commandManager,
        [RequiredVersion("1.0")] Framework framework,
        [RequiredVersion("1.0")] SigScanner sigScanner
    )
    {
        PluginInterface = pluginInterface;
        DtrBar = dtrBar;
        CommandManager = commandManager;
        ChatGui = chatGui;
        Framework = framework;

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(pluginInterface, this);

        if (Configuration.ShowSongInNative)
        {
            dtrEntry = dtrBar.Get(ConstName);
        }

        SongList.Init(pluginInterface.AssemblyLocation.DirectoryName);
        BGMAddressResolver.Init(sigScanner);
        BGMController.OnSongChanged += HandleSongChanged;
        SongUI = new SongUI(this);

        commandManager.AddHandler(CommandName, new CommandInfo(OnDisplayCommand)
        {
            HelpMessage = "Displays the Orchestrion window, to view, change, or stop in-game BGM."
        });
        pluginInterface.UiBuilder.Draw += Display;
        pluginInterface.UiBuilder.OpenConfigUi += () => SongUI.SettingsVisible = true;
        framework.Update += OrchestrionUpdate;
    }

    private void OrchestrionUpdate(Framework unused)
    {
        BGMController.Update();
    }

    public void Dispose()
    {
        Framework.Update -= OrchestrionUpdate;
        PluginInterface.UiBuilder.Draw -= Display;
        dtrEntry?.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    public void SetNativeDisplay(bool value)
    {
        // Somehow it was set to the same value. This should not occur
        if (value == Configuration.ShowSongInNative) return;

        if (value)
        {
            dtrEntry = DtrBar.Get(ConstName);
            var songName = SongList.GetSongTitle(CurrentSong);
            dtrEntry.Text = NativeNowPlayingPrefix + songName;
        }
        else
            dtrEntry.Dispose();
    }
    
    private void OnCommand(string command, string args)
    {
        var argSplit = args.Split(' ');
        var argLen = argSplit.Length;

        switch (argLen)
        {
            case 1 when string.IsNullOrEmpty(argSplit[0]):
                songList.Visible = !songList.Visible;
                break;
            case 1 when argSplit[0].ToLowerInvariant() == "stop":
                StopSong();
                break;
            case 1 when argSplit[0].ToLowerInvariant() == "play":
                chatGui.PrintError("You must specify a song to play.");
                break;
            case 1 when argSplit[0].ToLowerInvariant() == "random":
                if (songList.TryGetRandomSong(limitToFavorites: false, out var randomSong))
                    PlaySong(randomSong);
                else
                    chatGui.PrintError("No possible songs found."); // This should never happen but...
                break;
            case 2 when argSplit[0].ToLowerInvariant() == "random" && argSplit[1].ToLowerInvariant() == "favorites":
                if (songList.TryGetRandomSong(limitToFavorites: true, out var randomFavoriteSong))
                    PlaySong(randomFavoriteSong);
                else
                    chatGui.PrintError("No possible songs found.");
                break;
            case 2 when argSplit[0].ToLowerInvariant() == "play" && int.TryParse(argSplit[1], out var songId):
                if (songList.SongExists(songId))
                    PlaySong(songId);
                else
                    chatGui.PrintError($"Song {argSplit[1]} not found.");
                break;
            case >= 2 when argSplit[0] == "play".ToLowerInvariant() && !int.TryParse(argSplit[1], out _):
                var songName = argSplit.Skip(1).Aggregate((x, y) => $"{x} {y}");
                if (songList.TryGetSongByName(songName, out var songIdFromName))
                {
                    PlaySong(songIdFromName);
                }
                else
                {
                    var payloads = new List<Payload>
                    {
                        new TextPayload("Orchestrion: Song "),
                        EmphasisItalicPayload.ItalicsOn,
                        new TextPayload(songName),
                        EmphasisItalicPayload.ItalicsOff,
                        new TextPayload(" not found.")
                    };
                    chatGui.PrintError(new SeString(payloads));
                }
                break;
            default:
                if (argSplit[0].ToLowerInvariant() != "help") break;
                chatGui.Print(CommandName + " help: ");
                chatGui.Print("/porch help - Display this message");
                chatGui.Print("/porch - Display the Orchestrion UI");
                chatGui.Print("/porch play [songId] - Play the specified song");
                chatGui.Print("/porch play [song name] - Play the specified song");
                chatGui.Print("/porch random - Play a random song");
                chatGui.Print("/porch random favorites - Play a random song from favorites");
                chatGui.Print("/porch stop - Stop the current playing song or replacement song");
                break;
        }
    }

    private void Display()
    {
        SongUI.Draw();
    }

    private void HandleSongChanged(int oldSongId, int oldPriority, int newSongId, int newPriority)
    {
        PluginLog.Debug($"Song ID changed from {oldSongId} to {newSongId}");

        // The user is playing a track manually, so keep playing
        if (BGMController.PlayingSongId != 0 && !isPlayingReplacement)
            return;

        // A replacement is available, so change to it
        if (Configuration.SongReplacements.TryGetValue(newSongId, out var replacement))
        {
            PluginLog.Debug($"Song ID {newSongId} has a replacement of {replacement.ReplacementId}");

            // If the replacement is "do not change" and we are not playing something, play the previous track
            // If the replacement is "do not change" and we *are* playing something, it'll just keep playing
            // Else we're only here if we have a replacement, play that 
            if (replacement.ReplacementId == -1 && BGMController.PlayingSongId == 0)
                PlaySong(BGMController.OldSongId, true);
            else if (replacement.ReplacementId != -1)
                PlaySong(replacement.ReplacementId, true);
            return;
        }

        // A replacement is playing
        if (isPlayingReplacement)
        {
            isPlayingReplacement = false;
            StopSong();
        }

        SongUI.AddSongToHistory(newSongId);

        SendSongEcho(newSongId);
        UpdateDtr(newSongId);
    }

    public void PlaySong(int songId, bool isReplacement = false)
    {
        PluginLog.Debug($"Playing {songId}");
        isPlayingReplacement = isReplacement;
        BGMController.SetSong((ushort)songId, Configuration.TargetPriority);
        SongUI.AddSongToHistory(songId);
        SendSongEcho(songId, true);
        UpdateDtr(songId, true);
    }

    public void StopSong()
    {
        PluginLog.Debug($"Stopping playing {BGMController.PlayingSongId}...");

        if (Configuration.SongReplacements.TryGetValue(BGMController.CurrentSongId, out var replacement))
        {
            PluginLog.Debug($"Song ID {BGMController.CurrentSongId} has a replacement of {replacement.ReplacementId}...");

            if (replacement.ReplacementId == BGMController.PlayingSongId)
            {
                // The replacement is the same track as we're currently playing
                // There's no point in continuing to play, so fall through to stop
                PluginLog.Debug($"But that's the song we're playing [{BGMController.PlayingSongId}], so let's stop");
            }
            else if (replacement.ReplacementId == -1)
            {
                // We stopped playing a song and the song under it has a replacement, so play that
                PlaySong(BGMController.OldSongId, true);
                SongUI.AddSongToHistory(BGMController.OldSongId);
                return;
            }
            else
            {
                // Otherwise, go back to the replacement ID (stop playing the song on TOP of the replacement)
                PlaySong(replacement.ReplacementId, true);
                SongUI.AddSongToHistory(replacement.ReplacementId);
                return;
            }
        }

        // If there was no replacement involved, we don't need to do anything else, just stop
        BGMController.SetSong(0, Configuration.TargetPriority);
        SongUI.AddSongToHistory(BGMController.CurrentSongId);
        SendSongEcho(BGMController.CurrentSongId);
        UpdateNui(BGMController.CurrentSongId);
    }

    private void UpdateDtr(int songId, bool playedByOrch = false)
    {
        if (!Configuration.ShowSongInNative) return;

        var songName = SongUI.GetSongTitle(songId);
        var suffix = "";
        if (Configuration.ShowIdInNative)
        {
            if (!string.IsNullOrEmpty(songName))
                suffix = " - ";
            suffix += $"{songId}";
        }

        var text = songName + suffix;

        text = playedByOrch ? $"{NativeNowPlayingPrefix} [{text}]" : $"{NativeNowPlayingPrefix} {text}";
        
        dtrEntry.Text = text;
    }

    private void SendSongEcho(int songId, bool playedByOrch = false)
    {
        var songName = SongUI.GetSongTitle(songId);
        if (!Configuration.ShowSongInChat || string.IsNullOrEmpty(songName)) return;

        var payloads = new List<Payload>
        {
            nowPlayingPayload,
            playedByOrch ? leftBracketPayload : emptyPayload,
            EmphasisItalicPayload.ItalicsOn,
            new TextPayload(songName),
            EmphasisItalicPayload.ItalicsOff,
            playedByOrch ? rightBracketPayload : emptyPayload,
            periodPayload
        };

        ChatGui.PrintChat(new XivChatEntry
        {
            Message = new SeString(payloads),
            Type = XivChatType.Echo
        });
    }
}