using System.Collections.Generic;
using System.Numerics;
using Dalamud.Logging;
using ImGuiNET;
using Orchestrion.Persistence;
using Orchestrion.Struct;
using Orchestrion.UI.Components;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
	private readonly RenderableSongList _historySongList;
	
	// In order to modify the song history, we keep a reference to the history list
	private readonly List<RenderableSongEntry> _songHistory = new();

	private void DrawSongHistoryTab()
	{
		// to keep the tab bar always visible and not have it get scrolled out
		ImGui.BeginChild("##_songList_internal", new Vector2(-1f, -60f));
		_historySongList.Draw();
		ImGui.EndChild();
		DrawFooter();
	}
	
	private void AddSongToHistory(int id)
	{
		// Don't add silence
		if (id == 1 || !SongList.Instance.TryGetSong(id, out _)) return;
		var newEntry = new RenderableSongEntry(id, DateTime.Now);
		var currentIndex = _songHistory.Count - 1;

		// Check if we have history, if yes, then check if ID is the same as previous, if not, add to history
		if (currentIndex < 0 || _songHistory[currentIndex].Id != id)
		{
			_songHistory.Add(newEntry);
			PluginLog.Verbose($"[AddSongToHistory] Added {id} to history. There are now {currentIndex + 1} songs in history.");
		}
	}
}