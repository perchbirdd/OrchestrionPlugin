using System.Collections.Generic;
using System.Numerics;
using CheapLoc;
using ImGuiNET;
using Orchestrion.Persistence;
using Orchestrion.Struct;
using Orchestrion.UI.Components;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
    private SongReplacementEntry _tmpReplacement;
    private readonly List<int> _removalList = new();
    
	private void DrawReplacementsTab()
    {
        ImGui.BeginChild("##replacementlist");
        DrawCurrentReplacement();
        DrawReplacementList();
        ImGui.EndChild();
    }

    private void DrawReplacementList()
    {
        foreach (var replacement in Configuration.Instance.SongReplacements.Values)
        {
            ImGui.Spacing();
            SongList.Instance.TryGetSong(replacement.TargetSongId, out var target);

            var targetText = $"{replacement.TargetSongId} - {target.Name}";
            var replText = replacement.ReplacementId == SongReplacementEntry.NoChangeId ? MainWindow._noChange : $"{replacement.ReplacementId} - {SongList.Instance.GetSong(replacement.ReplacementId).Name}";
            
            ImGui.TextWrapped($"{targetText}");
            if (ImGui.IsItemHovered())
                BgmTooltip.DrawBgmTooltip(target);

            ImGui.Text(Loc.Localize("ReplaceWith", "will be replaced with"));
            ImGui.TextWrapped($"{replText}");
            if (ImGui.IsItemHovered() && replacement.ReplacementId != SongReplacementEntry.NoChangeId)
                BgmTooltip.DrawBgmTooltip(SongList.Instance.GetSong(replacement.ReplacementId));

            // Buttons in bottom right of area
            var editText = Loc.Localize("Edit", "Edit");
            var deleteText = Loc.Localize("Delete", "Delete");
            RightAlignButtons(ImGui.GetCursorPosY(), new[] {editText, deleteText});
            if (ImGui.Button($"{editText}##{replacement.TargetSongId}"))
            {
                _removalList.Add(replacement.TargetSongId);
                _tmpReplacement.TargetSongId = replacement.TargetSongId;
                _tmpReplacement.ReplacementId = replacement.ReplacementId;
            }
            ImGui.SameLine();
            if (ImGui.Button($"{deleteText}##{replacement.TargetSongId}"))
                _removalList.Add(replacement.TargetSongId);

            ImGui.Separator();
        }

        if (_removalList.Count > 0)
        {
            foreach (var toRemove in _removalList)
                Configuration.Instance.SongReplacements.Remove(toRemove);
            _removalList.Clear();
            Configuration.Instance.Save();
        }
    }
    
    private void DrawCurrentReplacement()
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

        if (ImGui.BeginCombo(Loc.Localize("TargetSong", "Target Song"), targetText))
        {
            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!Util.SearchMatches(_searchText, song)) continue;
                if (Configuration.Instance.SongReplacements.ContainsKey(song.Id)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.TargetSongId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.TargetSongId = song.Id;
                if (ImGui.IsItemHovered())
                    BgmTooltip.DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();

        if (ImGui.BeginCombo(Loc.Localize("ReplacementSong", "Replacement Song"), replacementText))
        {
            if (ImGui.Selectable(MainWindow._noChange))
                _tmpReplacement.ReplacementId = SongReplacementEntry.NoChangeId;

            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!Util.SearchMatches(_searchText, song)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.ReplacementId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.ReplacementId = song.Id;
                if (ImGui.IsItemHovered())
                    BgmTooltip.DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        var text = Loc.Localize("AddReplacement", "Add as song replacement");
        MainWindow.RightAlignButton(ImGui.GetCursorPosY(), text);
        if (ImGui.Button(text))
        {
            Configuration.Instance.SongReplacements.Add(_tmpReplacement.TargetSongId, _tmpReplacement);
            Configuration.Instance.Save();
            ResetReplacement();
        }

        ImGui.Separator();
    }
}