using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Orchestrion.Persistence;
using Orchestrion.Struct;

namespace Orchestrion.Windows.MainWindow;

public partial class MainWindow
{
    private SongReplacementEntry _tmpReplacement;
    private readonly List<int> _removalList = new();
    
	private void DrawReplacements()
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
            var replText = replacement.ReplacementId == SongReplacementEntry.NoChangeId ? NoChange : $"{replacement.ReplacementId} - {SongList.Instance.GetSong(replacement.ReplacementId).Name}";
            
            ImGui.TextWrapped($"{targetText}");
            if (ImGui.IsItemHovered())
                DrawBgmTooltip(target);

            ImGui.Text($"will be replaced with");
            ImGui.TextWrapped($"{replText}");
            if (ImGui.IsItemHovered() && replacement.ReplacementId != SongReplacementEntry.NoChangeId)
                DrawBgmTooltip(SongList.Instance.GetSong(replacement.ReplacementId));

            // Buttons in bottom right of area
            RightAlignButtons(ImGui.GetCursorPosY(), new[] {"Edit", "Delete"});
            if (ImGui.Button($"Edit##{replacement.TargetSongId}"))
            {
                _removalList.Add(replacement.TargetSongId);
                _tmpReplacement.TargetSongId = replacement.TargetSongId;
                _tmpReplacement.ReplacementId = replacement.ReplacementId;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Delete##{replacement.TargetSongId}"))
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
            replacementText = NoChange;
        else
            replacementText = $"{SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Id} - {SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Name}";

        // This fixes the ultra-wide combo boxes, I guess
        var width = ImGui.GetWindowWidth() * 0.60f;

        if (ImGui.BeginCombo("Target Song", targetText))
        {
            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!SearchMatches(song)) continue;
                if (Configuration.Instance.SongReplacements.ContainsKey(song.Id)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.TargetSongId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.TargetSongId = song.Id;
                if (ImGui.IsItemHovered())
                    DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();

        if (ImGui.BeginCombo("Replacement Song", replacementText))
        {
            if (ImGui.Selectable(NoChange))
                _tmpReplacement.ReplacementId = SongReplacementEntry.NoChangeId;

            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!SearchMatches(song)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.ReplacementId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.ReplacementId = song.Id;
                if (ImGui.IsItemHovered())
                    DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        RightAlignButton(ImGui.GetCursorPosY(), "Add as song replacement");
        if (ImGui.Button("Add as song replacement"))
        {
            Configuration.Instance.SongReplacements.Add(_tmpReplacement.TargetSongId, _tmpReplacement);
            Configuration.Instance.Save();
            ResetReplacement();
        }

        ImGui.Separator();
    }
}