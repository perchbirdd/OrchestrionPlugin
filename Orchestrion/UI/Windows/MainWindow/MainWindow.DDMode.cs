using CheapLoc;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Struct;

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

        var targetText = $"{SongList.Instance.GetSong(_tmpReplacement.TargetSongId).Id} - {SongList.Instance.GetSong(_tmpReplacement.TargetSongId).Name}";
        string replacementText;
        if (_tmpReplacement.ReplacementId == SongReplacementEntry.NoChangeId)
            replacementText = _noChange;
        else
            replacementText = $"{SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Id} - {SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Name}";

        // This fixes the ultra-wide combo boxes, I guess
        var width = ImGui.GetWindowWidth() * 0.60f;

        string allSongsLoc = Loc.Localize("DDPlaylistAll", "All Songs");
        if (ImGui.BeginCombo(Loc.Localize("DDPlaylist", "Playlist"), _selectedDDPlaylist == "" ? allSongsLoc : _selectedDDPlaylist))
        {
            if (ImGui.Selectable(allSongsLoc))
            {
                _selectedDDPlaylist = "";
            }
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
        var text = Loc.Localize("DDModeStart", "Start DD Mode");
        if (ImGui.Button(text))
        {
            BGMManager.StartDeepDungeonMode(_selectedDDPlaylist);
        }
    }
}