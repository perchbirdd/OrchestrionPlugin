using CheapLoc;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Orchestrion.BGMSystem;
using Orchestrion.Persistence;

namespace Orchestrion.UI.Windows;

public class SettingsWindow : Window
{
	public SettingsWindow() : base("Orchestrion Settings###orchsettings", ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    
    public override void PreDraw()
    {
        Size = ImGuiHelpers.ScaledVector2(720, 520);
    }

    private static void Checkbox(string text, Func<bool> get, Action<bool> set)
    {
        var value = get();
        if (ImGui.Checkbox($"##orch_{text}", ref value))
        {
            set(value);
            Configuration.Instance.Save();
        }
        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }
    
    public override void Draw()
	{
        ImGui.PushFont(OrchestrionPlugin.LargeFont);
        ImGui.Text(Loc.Localize("GeneralSettings", "General Settings"));
        ImGui.PopFont();
        
        ImGui.PushItemWidth(500f);
        
        Checkbox(Loc.Localize("ShowSongTitleBar",
            "Show current song in player title bar"),
            () => Configuration.Instance.ShowSongInTitleBar,
            b => Configuration.Instance.ShowSongInTitleBar = b);
        
        Checkbox(Loc.Localize("ShowNowPlayingChat",
            "Show \"Now playing\" messages in game chat when the current song changes"), 
            () => Configuration.Instance.ShowSongInChat,
            b => Configuration.Instance.ShowSongInChat = b);

        Checkbox(Loc.Localize("ShowSongServerInfo",
            "Show current song in the \"server info\" UI element in-game"),
            () => Configuration.Instance.ShowSongInNative, 
            b => Configuration.Instance.ShowSongInNative = b);

        if (!Configuration.Instance.ShowSongInNative)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        
        Checkbox(Loc.Localize("ShowSongIdServerInfo", 
            "Show song ID in the \"server info\" UI element in-game"), 
            () => Configuration.Instance.ShowIdInNative, 
            b => Configuration.Instance.ShowIdInNative = b); 
        
        if (!Configuration.Instance.ShowSongInNative)
            ImGui.PopStyleVar();
        
        Checkbox(Loc.Localize("HandleSpecialModes", 
            "Handle special \"in-combat\" and mount movement BGM modes"), 
            () => Configuration.Instance.HandleSpecialModes, 
            b => Configuration.Instance.HandleSpecialModes = b);
        
        if (!BGMAddressResolver.StreamingEnabled)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextWrapped(Loc.Localize("AudioStreamingDisabledWarning" , 
                "Audio streaming is disabled. This may be due to Sound Filter or a third-party plugin. The above setting may not work as " +
                              "expected and you may encounter other audio issues such as popping or tracks not swapping channels. This is not" +
                              " related to the Orchestrion Plugin."));
            ImGui.PopStyleColor();
        }
        
        ImGui.PushFont(OrchestrionPlugin.LargeFont);
        ImGui.Text(Loc.Localize("LocSettings", "Localization Settings"));
        ImGui.PopFont();

        Checkbox(Loc.Localize("ShowAltLangTitles", 
            "Show alternate language song titles in tooltips"), 
            () => Configuration.Instance.ShowAltLangTitles, 
            b => Configuration.Instance.ShowAltLangTitles = b);

        Checkbox(Loc.Localize("UseClientLangInServerInfo", 
            "Use client language, not Dalamud language, for song titles in the \"server info\" UI element in-game"), 
            () => Configuration.Instance.UseClientLangInServerInfo, 
            b => Configuration.Instance.UseClientLangInServerInfo = b);

        Checkbox(Loc.Localize("UseClientLangInChat", 
            "Use client language, not Dalamud language, for song titles in Orchestrion chat messages in-game"), 
            () => Configuration.Instance.UseClientLangInChat, 
            b => Configuration.Instance.UseClientLangInChat = b);

        ImGui.PushFont(OrchestrionPlugin.LargeFont);
        ImGui.Text(Loc.Localize("MiniPlayerSettings", "Mini Player Settings"));
        ImGui.PopFont();
        
        Checkbox(Loc.Localize("ShowMiniPlayer", "Show mini player"),
            () => Configuration.Instance.ShowMiniPlayer, 
            b => Configuration.Instance.ShowMiniPlayer = b);

        Checkbox(Loc.Localize("LockMiniPlayer", "Lock mini player"), 
            () => Configuration.Instance.MiniPlayerLock, 
            b => Configuration.Instance.MiniPlayerLock = b);
        
        var miniPlayerOpacity = Configuration.Instance.MiniPlayerOpacity;
        ImGui.PushItemWidth(200f);
        if (ImGui.SliderFloat($"##orch_MiniPlayerOpacity", ref miniPlayerOpacity, 0.01f, 1.0f))
        {
            Configuration.Instance.MiniPlayerOpacity = miniPlayerOpacity;
            Configuration.Instance.Save();
        }
        ImGui.SameLine();
        ImGui.TextWrapped(Loc.Localize("MiniPlayerOpacity", "Mini player opacity"));
        ImGui.PopItemWidth();
        ImGui.PopItemWidth();
    }
}