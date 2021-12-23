using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Game.Gui;
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
    public string Name => "Orchestrion";

    private const string SongListFile = "xiv_bgm.csv";
    private const string CommandName = "/porch";
    private const string NativeNowPlayingPrefix = "♪ ";

    public DalamudPluginInterface PluginInterface { get; }
    public CommandManager CommandManager { get; }
    public ChatGui ChatGui { get; }
    public Framework Framework { get; }
    public NativeUIUtil NativeUI { get; }
    public Configuration Configuration { get; }
    public SongUI SongUI { get; }

    private readonly TextPayload nowPlayingPayload = new("Now playing ");
    private readonly TextPayload periodPayload = new(".");
    private readonly TextPayload emptyPayload = new("");
    private readonly TextPayload leftBracketPayload = new("[");
    private readonly TextPayload rightBracketPayload = new("]");

    private bool isPlayingReplacement = false;

    public int CurrentSong => BGMController.PlayingSongId == 0 ? BGMController.CurrentSongId : BGMController.PlayingSongId;

    public OrchestrionPlugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] GameGui gameGui,
        [RequiredVersion("1.0")] ChatGui chatGui,
        [RequiredVersion("1.0")] CommandManager commandManager,
        [RequiredVersion("1.0")] Framework framework,
        [RequiredVersion("1.0")] SigScanner sigScanner
    )
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        ChatGui = chatGui;
        Framework = framework;

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(pluginInterface, this);

        SongList.Init(pluginInterface.AssemblyLocation.DirectoryName);
        BGMAddressResolver.Init(sigScanner);
        BGMController.OnSongChanged += HandleSongChanged;
        SongUI = new SongUI(this);
        NativeUI = new NativeUIUtil(Configuration, gameGui);

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

        if (Configuration.ShowSongInNative)
            NativeUI.Update();
    }

    public void Dispose()
    {
        Framework.Update -= OrchestrionUpdate;
        NativeUI.Dispose();
        PluginInterface.UiBuilder.Draw -= Display;
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.Dispose();
    }

    public void SetNativeDisplay(bool value)
    {
        // Somehow it was set to the same value. This should not occur
        if (value == Configuration.ShowSongInNative) return;

        if (value)
        {
            NativeUI.Init();
            var songName = SongUI.GetSongTitle(CurrentSong);
            NativeUI.Update(NativeNowPlayingPrefix + songName);
        }
        else
        {
            NativeUI.Dispose();
        }
    }

    private void OnDisplayCommand(string command, string args)
    {
        SongUI.Visible = !SongUI.Visible;
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
        UpdateNui(newSongId);
    }

    public void PlaySong(int songId, bool isReplacement = false)
    {
        PluginLog.Debug($"Playing {songId}");
        isPlayingReplacement = isReplacement;
        BGMController.SetSong((ushort)songId, Configuration.TargetPriority);
        SongUI.AddSongToHistory(songId);
        SendSongEcho(songId, true);
        UpdateNui(songId, true);
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

    private void UpdateNui(int songId, bool playedByOrch = false)
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

        NativeUI.Update(text);
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