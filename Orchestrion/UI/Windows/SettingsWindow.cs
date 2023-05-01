using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Orchestrion.BGMSystem;
using Orchestrion.Persistence;

namespace Orchestrion.UI.Windows;

public class SettingsWindow : Window
{
	public SettingsWindow() : base("Orchestrion Settings###orchsettings", ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        SizeCondition = ImGuiCond.Once;
    }

    public override void PreDraw()
    {
        var stream = BGMAddressResolver.StreamingEnabled;
        var height = stream ? 170 : 240;
        Size = ImGuiHelpers.ScaledVector2(520, height);
    }
    
    public override void Draw()
	{
        var stream = BGMAddressResolver.StreamingEnabled;
        ImGui.SetNextItemOpen(true, ImGuiCond.Always);
        var showSongInTitlebar = Configuration.Instance.ShowSongInTitleBar;
        if (ImGui.Checkbox(Loc.Localize("ShowSongTitleBar", "Show current song in player title bar"), ref showSongInTitlebar))
        {
            Configuration.Instance.ShowSongInTitleBar = showSongInTitlebar;
            Configuration.Instance.Save();
        }

        var showSongInChat = Configuration.Instance.ShowSongInChat;
        if (ImGui.Checkbox(Loc.Localize("ShowNowPlayingChat", "Show \"Now playing\" messages in game chat when the current song changes"), ref showSongInChat))
        {
            Configuration.Instance.ShowSongInChat = showSongInChat;
            Configuration.Instance.Save();
        }

        var showNative = Configuration.Instance.ShowSongInNative;
        if (ImGui.Checkbox(Loc.Localize("ShowSongServerInfo", "Show current song in the \"server info\" UI element in-game"), ref showNative))
        {
            Configuration.Instance.ShowSongInNative = showNative;
            Configuration.Instance.Save();
        }

        if (!showNative)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);

        var showIdNative = Configuration.Instance.ShowIdInNative;
        if (ImGui.Checkbox(Loc.Localize("ShowSongIdServerInfo", "Show song ID in the \"server info\" UI element in-game"), ref showIdNative) && showNative)
        {
            Configuration.Instance.ShowIdInNative = showIdNative;
            Configuration.Instance.Save();
        }

        if (!showNative)
            ImGui.PopStyleVar();

        var handleSpecial = Configuration.Instance.HandleSpecialModes;
        if (ImGui.Checkbox(Loc.Localize("HandleSpecialModes", "Handle special \"in-combat\" and mount movement BGM modes"), ref handleSpecial))
        {
            Configuration.Instance.HandleSpecialModes = handleSpecial;
            Configuration.Instance.Save();
        }

        if (!stream)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextWrapped(Loc.Localize("AudioStreamingDisabledWarning" , "Audio streaming is disabled. This may be due to Sound Filter or a third-party plugin. The above setting may not work as " +
                              "expected and you may encounter other audio issues such as popping or tracks not swapping channels. This is not" +
                              " related to the Orchestrion Plugin."));
            ImGui.PopStyleColor();
        }

        ImGui.PushFont(OrchestrionPlugin.LargeFont);
        ImGui.Text(Loc.Localize("MiniPlayerSettings", "Mini Player Settings"));
        ImGui.PopFont();
        
        var showMiniPlayer = Configuration.Instance.ShowMiniPlayer;
        if (ImGui.Checkbox(Loc.Localize("ShowMiniPlayer", "Show mini player"), ref showMiniPlayer))
        {
            Configuration.Instance.ShowMiniPlayer = showMiniPlayer;
            Configuration.Instance.Save();
        }

        var miniPlayerLock = Configuration.Instance.MiniPlayerLock;
        if (ImGui.Checkbox(Loc.Localize("LockMiniPlayer", "Lock mini player"), ref miniPlayerLock))
        {
            Configuration.Instance.MiniPlayerLock = miniPlayerLock;
            Configuration.Instance.Save();
        }
        
        var miniPlayerOpacity = Configuration.Instance.MiniPlayerOpacity;
        if (ImGui.SliderFloat(Loc.Localize("MiniPlayerOpacity", "Mini player opacity"), ref miniPlayerOpacity, 0.01f, 1.0f))
        {
            Configuration.Instance.MiniPlayerOpacity = miniPlayerOpacity;
            Configuration.Instance.Save();
        }
    }
}