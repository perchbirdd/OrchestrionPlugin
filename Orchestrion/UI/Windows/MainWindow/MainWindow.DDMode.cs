using CheapLoc;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.Persistence;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
    private string _selectedDDPlaylist = "";
    private void DrawDeepDungeonModeTab()
    {
        ImGui.BeginChild("##ddmode");
        DrawDeepDungeonModeSelector();
        ImGui.EndChild();
    }

    private void DrawDeepDungeonModeSelector()
    {
        ImGui.Spacing();
        var description = Loc.Localize("DDModeDescription", "Deep Dungeon Mode is a feature that will instead play a random track from either \"All Songs\" or a specified playlist whenever the track changes.");
        ImGui.TextWrapped(description);
        
        var allSongsLoc = Loc.Localize("DDPlaylistAll", "All Songs");
        if (ImGui.BeginCombo(Loc.Localize("DDPlaylist", "Playlist"), _selectedDDPlaylist == "" ? allSongsLoc : _selectedDDPlaylist))
        {
            if (ImGui.Selectable(allSongsLoc))
                _selectedDDPlaylist = "";
            
            foreach (var pInfo in Configuration.Instance.Playlists)
            {
                var pName = pInfo.Key;
                if (ImGui.Selectable(pName))
                {
                    _selectedDDPlaylist = pName;
                }
            }
            ImGui.EndCombo();
        }
        var ddStart = Loc.Localize("DDModeStart", "Start Deep Dungeon Mode");
        var ddEnd = Loc.Localize("DDModeEnd", "Stop Deep Dungeon Mode");

        var isDd = BGMManager.DeepDungeonModeActive();
        
        if (!isDd && ImGui.Button(ddStart))
        {
            BGMManager.StartDeepDungeonMode(_selectedDDPlaylist);
        }
        else if (isDd && ImGui.Button(ddEnd))
        {
            BGMManager.StopDeepDungeonMode();
        }
    }
}