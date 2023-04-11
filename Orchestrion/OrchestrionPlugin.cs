using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Orchestrion.Game;
using Orchestrion.Windows;

namespace Orchestrion;

// ReSharper disable once ClassNeverInstantiated.Global
public class OrchestrionPlugin : IDalamudPlugin
{
    private const string ConstName = "Orchestrion";
    public string Name => ConstName;

    private const string CommandName = "/porch";
    private const string NativeNowPlayingPrefix = "♪ ";

    private readonly WindowSystem _windowSystem;
    private readonly MainWindow _mainWindow;
    private readonly SettingsWindow _settingsWindow;
    
    private readonly TextPayload _nowPlayingPayload = new("Orchestrion: Now playing ");
    private readonly TextPayload _periodPayload = new(".");
    private readonly TextPayload _emptyPayload = new("");
    private readonly TextPayload _leftBracketPayload = new("[");
    private readonly TextPayload _rightBracketPayload = new("]");

    private readonly DtrBarEntry _dtrEntry;

    private List<Payload> _songEchoPayload;

    public OrchestrionPlugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        DalamudApi.Initialize(pluginInterface);

        if (Configuration.Instance.ShowSongInNative)
        {
            _dtrEntry = DalamudApi.DtrBar.Get(ConstName);
        }

        BGMAddressResolver.Init();
        BGMManager.OnSongChanged += OnSongChanged;
        
        _windowSystem = new WindowSystem();
        _mainWindow = new MainWindow(this);
        _settingsWindow = new SettingsWindow();
        
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_settingsWindow);

        DalamudApi.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Displays the Orchestrion window, to view, change, or stop in-game BGM.",
        });
        
        DalamudApi.PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += OpenSettingsWindow;
        
        DalamudApi.Framework.Update += OrchestrionUpdate;
        DalamudApi.ClientState.Logout += ClientStateOnLogout;
    }
    
    private void ClientStateOnLogout(object sender, EventArgs e)
    {
        BGMManager.Stop();
    }

    private void OrchestrionUpdate(Framework ignored)
    {
        if (_songEchoPayload == null || IsLoadingScreen()) return;
        
        DalamudApi.ChatGui.PrintChat(new XivChatEntry
        {
            Message = new SeString(_songEchoPayload),
            Type = DalamudApi.PluginInterface.GeneralChatType,
        });

        _songEchoPayload = null;
    }

    private void OnSongChanged(int oldSong, int newSong, bool playedByOrch)
    {
        UpdateDtr(newSong, playedByOrch: playedByOrch);
        UpdateChat(newSong, playedByOrch: playedByOrch);
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OrchestrionUpdate;
        DalamudApi.PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        DalamudApi.CommandManager.RemoveHandler(CommandName);
        _dtrEntry?.Dispose();
        BGMManager.Dispose();
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

        switch (argLen)
        {
            case 1 when string.IsNullOrEmpty(argSplit[0]):
                _mainWindow.IsOpen = !_mainWindow.IsOpen;
                break;
            case 1 when argSplit[0].ToLowerInvariant() == "stop":
                BGMManager.Stop();
                break;
            case 1 when argSplit[0].ToLowerInvariant() == "play":
                DalamudApi.ChatGui.PrintError("You must specify a song to play.");
                break;
            case 1 when argSplit[0].ToLowerInvariant() == "random":
                BGMManager.PlayRandomSong();
                break;
            case 2 when argSplit[0].ToLowerInvariant() == "random" && argSplit[1].ToLowerInvariant() == "favorites":
                BGMManager.PlayRandomSong(restrictToFavorites: true);
                break;
            case 2 when argSplit[0].ToLowerInvariant() == "play" && int.TryParse(argSplit[1], out var songId):
                if (SongList.Instance.SongExists(songId))
                    BGMManager.Play(songId);
                else
                    DalamudApi.ChatGui.PrintError($"Song {argSplit[1]} not found.");
                break;
            case >= 2 when argSplit[0] == "play".ToLowerInvariant() && !int.TryParse(argSplit[1], out _):
                var songName = argSplit.Skip(1).Aggregate((x, y) => $"{x} {y}");
                if (SongList.Instance.TryGetSongByName(songName, out var songIdFromName))
                {
                    BGMManager.Play(songIdFromName);
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
                    DalamudApi.ChatGui.PrintError(new SeString(payloads));
                }

                break;
            default:
                if (argSplit[0].ToLowerInvariant() != "help") break;
                DalamudApi.ChatGui.Print(CommandName + " help: ");
                DalamudApi.ChatGui.Print("/porch help - Display this message");
                DalamudApi.ChatGui.Print("/porch - Display the Orchestrion UI");
                DalamudApi.ChatGui.Print("/porch play [songId] - Play the specified song");
                DalamudApi.ChatGui.Print("/porch play [song name] - Play the specified song");
                DalamudApi.ChatGui.Print("/porch random - Play a random song");
                DalamudApi.ChatGui.Print("/porch random favorites - Play a random song from favorites");
                DalamudApi.ChatGui.Print("/porch stop - Stop the current playing song or replacement song");
                break;
        }
    }
    
    private void UpdateDtr(int songId, bool playedByOrch = false)
    {
        if (!Configuration.Instance.ShowSongInNative) return;

        var songName = SongList.Instance.GetSongTitle(songId);
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
        var songName = SongList.Instance.GetSongTitle(songId);
        if (!Configuration.Instance.ShowSongInChat || string.IsNullOrEmpty(songName)) return;

        // the actual echoing is done during framework update
        _songEchoPayload = new List<Payload>
        {
            _nowPlayingPayload,
            playedByOrch ? _leftBracketPayload : _emptyPayload,
            EmphasisItalicPayload.ItalicsOn,
            new TextPayload(songName),
            EmphasisItalicPayload.ItalicsOff,
            playedByOrch ? _rightBracketPayload : _emptyPayload,
            _periodPayload
        };
    }

    private unsafe bool IsLoadingScreen()
    {
        var titleCard = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_LocationTitle");
        var blackScreen = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("FadeMiddle");
        return titleCard != null && titleCard->IsVisible || blackScreen != null && blackScreen->IsVisible;
    }
}