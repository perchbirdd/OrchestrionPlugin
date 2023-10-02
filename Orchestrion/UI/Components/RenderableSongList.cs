using System.Collections.Generic;
using System.Linq;
using System.Text;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using Orchestrion.Persistence;
using Orchestrion.Types;
using Orchestrion.UI.Windows;

namespace Orchestrion.UI.Components;

public class RenderableSongList
{
	private List<RenderableSongEntry> _listSource;
	private readonly SongListRenderStrategy _renderStrategy;
	
	private readonly HashSet<int> _forRemoval = new();
	private readonly HashSet<int> _selected = new();
	private string _searchText = string.Empty;

	public RenderableSongList(List<RenderableSongEntry> listSource, SongListRenderStrategy strategy)
	{
		_listSource = listSource;
		_renderStrategy = strategy;
	}
	
	public void SetListSource(List<RenderableSongEntry> listSource)
	{
		lock (_listSource)
		{
			_listSource = listSource;	
		}
	}

	public void SetSearch(string searchText)
	{
		_searchText = searchText;
		_selected.Clear();
	}
	
	public HashSet<int> GetSelectedIndices()
	{
		return _selected;
	}

	public List<Song> GetSelectedSongs()
	{
		return _selected.Select(index => _listSource[index]).Select(entry => SongList.Instance.GetSong(entry.Id)).ToList();
	}

	public Song GetFirstSelectedSong()
	{
		if (_listSource.Count == 0 || _selected.Count == 0) return default;
		var first = _selected.ElementAt(0);
		if (first < 0 || first >= _listSource.Count) return default;
		return SongList.Instance.GetSong(_listSource[first].Id);
	}
	
	public void Draw()
	{
		lock (_listSource)
		{
			if (ImGui.BeginTable("RenderableSongList", 4, ImGuiTableFlags.SizingStretchProp))
			{
				ImGui.TableSetupColumn("playing", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("time", ImGuiTableColumnFlags.WidthFixed);

				var render = new Action<RenderableSongEntry, int>((entry, index) =>
				{
					if (!Util.SearchMatches(_searchText, entry.Id)) return;

					ImGui.TableNextRow();
					ImGui.TableNextColumn();

					DrawSongListItem(entry, index);
				});

				if (_renderStrategy.RenderBackwards())
				{
					for (int i = _listSource.Count - 1; i >= 0; i--)
						render(_listSource[i], i);
				} else {
					var i = 0;
					foreach (var entry in _listSource)
						render(entry, i++);
				}
				
				ImGui.EndTable();
			}
		}
		
		if (_forRemoval.Count > 0)
		{
			lock (_listSource)
			{
				var toRemove = _forRemoval.ToList();
				toRemove.Sort();
				toRemove.Reverse();
				foreach (var index in toRemove)
				{
					_renderStrategy.RemoveSong(index);
					_selected.Remove(index);
				}
				_forRemoval.Clear();	
			}
		}
	}
	
	private void DrawSongListItem(RenderableSongEntry entry, int index)
	{
		var isHistory = entry.TimePlayed != default;
		if (entry.Id == 0 || !SongList.Instance.TryGetSong(entry.Id, out var song)) return;
		
		if (_renderStrategy.IsPlaying(_listSource, index))
		{
			ImGui.PushFont(UiBuilder.IconFont);
			ImGui.Text(FontAwesomeIcon.Play.ToIconString());
			ImGui.PopFont();
		}
		else
		{
			var size = Util.GetIconSize(FontAwesomeIcon.Play);
			ImGui.Dummy(size);
		}
		ImGui.TableNextColumn();
		ImGui.Text(song.Id.ToString());
		ImGui.TableNextColumn();

		var selected = _selected.Contains(index);

		// ImGui.SetNextItemWidth(-1);
		if (ImGui.Selectable($"{song.Name}##{index}", selected, ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.SpanAllColumns))
		{
			HandleSelect(index, selected);

			if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && song.FileExists)
				_renderStrategy.PlaySong(entry, index);
		}

		if (ImGui.IsItemHovered())
			BgmTooltip.DrawBgmTooltip(song);

		if (ImGui.BeginPopupContextItem())
		{
			if (_selected.Count is 0 or 1)
				HandleSelect(index, selected);

			DrawCopyContentSubmenu();
			ImGui.Separator();
			if (DrawRemoveSubmenu())
				ImGui.Separator();
			DrawPlaylistAddSubmenu();

			ImGui.EndPopup();
		}

		ImGui.TableNextColumn();
		if (!isHistory) return;
		var deltaTime = DateTime.Now - entry.TimePlayed;
		var unit = deltaTime.TotalMinutes >= 1 ? (int)deltaTime.TotalMinutes : (int)deltaTime.TotalSeconds;
		var label = 
			deltaTime.TotalMinutes >= 1 
				? Loc.Localize("MinutesAgo", "{0}m ago") 
				: Loc.Localize("SecondsAgo", "{0}s ago");
		ImGui.Text(string.Format(label, unit));
	}

	private void HandleSelect(int index, bool selected)
	{
		if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl))
		{
			if (selected)
				_selected.Remove(index);
			else
				_selected.Add(index);
		}
		else if (ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift))
		{
			if (_selected.Count == 0)
			{
				_selected.Add(index);
			}
			else
			{
				var min = Math.Min(_selected.Min(), index);
				var max = Math.Max(_selected.Max(), index);
				_selected.Clear();
				for (var i = min; i <= max; i++)
					if (Util.SearchMatches(_searchText, _listSource[i].Id)) // don't add invisible songs
						_selected.Add(i);
			}
		}
		else
		{
			_selected.Clear();
			_selected.Add(index);
		}
	}

	private void DrawPlaylistAddSubmenu()
	{
		if (ImGui.BeginMenu(Loc.Localize("AddTo", "Add to...")))
		{
			var newPlaylistSongs = _selected.Select(index => _listSource.ElementAt(index)).Select(entry => entry.Id).ToList();
			foreach (var p in Configuration.Instance.Playlists.Values)
				if (ImGui.MenuItem(p.Name))
					p.AddSongs(newPlaylistSongs);

			ImGui.Separator();

			if (ImGui.MenuItem(Loc.Localize("NewPlaylistEllipsis", "New playlist...")))
			{
				DalamudApi.PluginLog.Debug("Opening new playlist popup...");
				NewPlaylistModal.Instance.Show(newPlaylistSongs);
			}

			ImGui.EndMenu();
		}
	}
	
	private void DrawCopyContentSubmenu()
	{
		var songId = Loc.Localize("SongId", "Song ID");
		var songName = Loc.Localize("SongTitle", "Song Title");
		var songAltName = Loc.Localize("SongAltTitle", "Song Alternate Title");
		var songSpcModeTitle = Loc.Localize("SongSpcModeTitle", "Song Special Mode Title");
		var songLocation = Loc.Localize("SongLocation", "Song Location");
		var songAdditionalInfo = Loc.Localize("SongAdditionalInfo", "Song Additional Info");
		var duration = Loc.Localize("Duration", "Duration");
		var songFilePath = Loc.Localize("SongFilePath", "Song File Path");
		var all = Loc.Localize("All", "All");

		var copy = string.Empty;
		
		if (ImGui.BeginMenu(Loc.Localize("Copy", "Copy")))
		{
			if (ImGui.MenuItem(songId))
				copy = BuildCopyString(_selected, s => s.Id.ToString());
			if (ImGui.MenuItem(songName))
				copy = BuildCopyString(_selected, s => s.Name);
			if (_selected.Select(songIndex => SongList.Instance.GetSong(_listSource[songIndex].Id)).Any(s => !string.IsNullOrEmpty(s.AlternateName)) 
			    && ImGui.MenuItem(songAltName))
				copy = BuildCopyString(_selected, s => s.AlternateName);
			if (_selected.Select(songIndex => SongList.Instance.GetSong(_listSource[songIndex].Id)).Any(s => !string.IsNullOrEmpty(s.SpecialModeName))
			    && ImGui.MenuItem(songSpcModeTitle))
				copy = BuildCopyString(_selected, s => s.SpecialModeName);
			if (ImGui.MenuItem(songLocation))
				copy = BuildCopyString(_selected, s => s.Locations);
			if (ImGui.MenuItem(songAdditionalInfo))
				copy = BuildCopyString(_selected, s => s.AdditionalInfo);
			if (ImGui.MenuItem(duration))
				copy = BuildCopyString(_selected, s => $"{s.Duration:mm\\:ss}");
			if (ImGui.MenuItem(songFilePath))
				copy = BuildCopyString(_selected, s => s.FilePath);
			ImGui.Separator();
			if (ImGui.MenuItem(all))
			{
				copy = BuildCopyString(_selected, s => $"{songId}: {s.Id}\n" +
				                                       $"{songName}: {s.Name}\n" +
				                                       (string.IsNullOrEmpty(s.AlternateName) ? "" : $"{songAltName}: {s.AlternateName}\n") +
				                                       (string.IsNullOrEmpty(s.SpecialModeName) ? "" : $"{songSpcModeTitle}: {s.SpecialModeName}\n") +
				                                       $"{songLocation}: {s.Locations}\n" +
				                                       $"{songAdditionalInfo}: {s.AdditionalInfo}\n" +
				                                       $"{duration}: {s.Duration:mm\\:ss}\n" +
				                                       $"{songFilePath}: {s.FilePath}");
			}
			if (copy != string.Empty)
				ImGui.SetClipboardText(copy);
			ImGui.EndMenu();
		}
	}

	private bool DrawRemoveSubmenu()
	{
		if (!_renderStrategy.SourceMutable()) return false;
		if (_selected.Count <= 0) return false;

		var label = Loc.Localize("RemoveSelected", "Remove {0} song(s)");
		label = string.Format(label, _selected.Count);

		if (!ImGui.MenuItem(label)) return false;
		
		_forRemoval.Clear();
		foreach (var i in _selected)
		{
			_forRemoval.Add(i);
		}
		return true;
	}

	public bool Compare(List<int> songs)
	{
		if (_listSource.Count != songs.Count) return false;
		for (var i = 0; i < _listSource.Count; i++)
			if (_listSource[i].Id != songs[i]) return false;
		return true;
	}

	private string BuildCopyString(HashSet<int> indices, Func<Song, string> contentExtractor)
	{
		var sb = new StringBuilder();
		foreach (var songId in indices.Select(songIndex => _listSource[songIndex].Id))
		{
			DalamudApi.PluginLog.Debug($"[BuildCopyString] Content extractor song {songId}");
			var extracted = contentExtractor.Invoke(SongList.Instance.GetSong(songId));
			if (!string.IsNullOrEmpty(extracted))
				sb.Append($"{extracted}, ");
			DalamudApi.PluginLog.Debug($"[BuildCopyString] Content extractor song {songId} done {sb}");
		}
		var text = sb.ToString();
		return text[..^2];
	}
}