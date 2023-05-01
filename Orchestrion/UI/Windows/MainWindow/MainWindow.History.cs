using System.Collections.Generic;
using System.Numerics;
using Dalamud.Logging;
using ImGuiNET;
using Orchestrion.Persistence;
using Orchestrion.Struct;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
	private readonly List<RenderableSongEntry> _songHistory = new();
	private int _selectedHistoryEntry;

	private void DrawSongHistory()
	{
		// to keep the tab bar always visible and not have it get scrolled out
		ImGui.BeginChild("##_songList_internal", new Vector2(-1f, -60f));
        
		if (ImGui.BeginTable("_songList table", 4, ImGuiTableFlags.SizingFixedFit))
		{
			ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("time", ImGuiTableColumnFlags.WidthFixed);
            
			// going from the end of the list
			for (int i = _songHistory.Count - 1; i >= 0; i--)
			{
				var songHistoryEntry = _songHistory[i];
				var song = SongList.Instance.GetSong(songHistoryEntry.Id);

				if (!SearchMatches(song))
					continue;

				ImGui.TableNextRow();
				ImGui.TableNextColumn();

				DrawSongListItem(song, i, songHistoryEntry.TimePlayed);
			}

			ImGui.EndTable();
		}

		ImGui.EndChild();
		DrawFooter(true);
	}
	
	private void AddSongToHistory(int id)
	{
		// Don't add silence
		if (id == 1 || !SongList.Instance.TryGetSong(id, out _))
			return;

		var newEntry = new RenderableSongEntry
		{
			Id = id,
			TimePlayed = DateTime.Now
		};

		var currentIndex = _songHistory.Count - 1;

		// Check if we have history, if yes, then check if ID is the same as previous, if not, add to history
		if (currentIndex < 0 || _songHistory[currentIndex].Id != id)
		{
			_songHistory.Add(newEntry);
			PluginLog.Verbose($"[AddSongToHistory] Added {id} to history. There are now {currentIndex + 1} songs in history.");
		}
	}
}