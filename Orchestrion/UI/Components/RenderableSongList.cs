using System.Collections.Generic;
using System.Linq;
using System.Text;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Struct;
using Orchestrion.UI.Windows;

namespace Orchestrion.UI.Components;

public class RenderableSongList
{
	private List<RenderableSongEntry> _listSource;
	private readonly SongListRenderStrategy _renderStrategy;
	
	private readonly HashSet<int> _forRemoval = new();
	private readonly HashSet<int> _selected = new();
	private string _searchText = string.Empty;
	
	private readonly NewPlaylistModal _newPlaylistModal = new();
	
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
	}
	
	private bool SearchMatches(int songId)
	{
		if (!SongList.Instance.TryGetSong(songId, out var song)) return false;
		var matchesSearch = _searchText.Length != 0
		                    && (song.Name.ToLower().Contains(_searchText.ToLower())
		                        || song.Locations.ToLower().Contains(_searchText.ToLower())
		                        || song.AdditionalInfo.ToLower().Contains(_searchText.ToLower())
		                        || song.Id.ToString().Contains(_searchText));
		var searchEmpty = _searchText.Length == 0;
		return matchesSearch || searchEmpty;
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

				var operation = new Action<RenderableSongEntry, int>((entry, index) =>
				{
					if (!SearchMatches(entry.Id)) return;

					ImGui.TableNextRow();
					ImGui.TableNextColumn();

					DrawSongListItem(entry, index);
				});

				if (_renderStrategy.RenderBackwards())
				{
					for (int i = _listSource.Count() - 1; i >= 0; i--)
						operation(_listSource.ElementAt(i), i);
				} else {
					var i = 0;
					foreach (var entry in _listSource)
						operation(entry, i++);
				}

				ImGui.EndTable();
			}
		}

		_newPlaylistModal.Draw();
	}
	
	private void DrawSongListItem(RenderableSongEntry entry, int index)
	{
		var isHistory = entry.TimePlayed != default;
		if (!SongList.Instance.TryGetSong(entry.Id, out var song)) return;
		
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
		if (ImGui.Selectable($"{song.Name}##{song.Id}{entry.TimePlayed}", selected, ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.SpanAllColumns))
		{
			HandleSelect(index, selected);

			if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
				_renderStrategy.PlaySong(entry, index);
		}

		if (ImGui.IsItemHovered())
			BgmTooltip.DrawBgmTooltip(song);

		if (ImGui.BeginPopupContextItem())
		{
			HandleSelect(index, selected);

			DrawCopyContentSubmenu(song);
			ImGui.Separator();
			DrawPlaylistAddSubmenu(song);

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
					_selected.Add(i);
			}
		}
		else
		{
			_selected.Clear();
			_selected.Add(index);
		}
	}

	private void DrawPlaylistAddSubmenu(Song song)
	{
		if (ImGui.BeginMenu(Loc.Localize("AddTo", "Add to...")))
		{
			foreach (var p in Configuration.Instance.Playlists.Values)
				if (ImGui.MenuItem(p.Name))
					p.AddSong(song.Id);

			ImGui.Separator();

			if (ImGui.MenuItem(Loc.Localize("NewPlaylistEllipsis", "New playlist...")))
			{
				PluginLog.Debug("Opening new playlist popup...");
				var newPlaylistSongs = _selected.Select(index => _listSource.ElementAt(index)).Select(entry => entry.Id).ToList();
				_newPlaylistModal.Show(newPlaylistSongs);
			}

			ImGui.EndMenu();
		}
	}

	private void DrawCopyContentSubmenu(Song song)
	{
		var songId = Loc.Localize("SongId", "Song ID");
		var songName = Loc.Localize("SongName", "Song Name");
		var songLocation = Loc.Localize("SongLocation", "Song Location");
		var songAdditionalInfo = Loc.Localize("SongAdditionalInfo", "Song Additional Info");
		var duration = Loc.Localize("Duration", "Duration");
		var songFilePath = Loc.Localize("SongFilePath", "Song File Path");
		var all = Loc.Localize("All", "All");

		if (ImGui.BeginMenu(Loc.Localize("Copy", "Copy")))
		{
			if (ImGui.MenuItem(songId))
				ImGui.SetClipboardText(song.Id.ToString());
			if (ImGui.MenuItem(songName))
				ImGui.SetClipboardText(song.Name);
			if (ImGui.MenuItem(songLocation))
				ImGui.SetClipboardText(song.Locations);
			if (ImGui.MenuItem(songAdditionalInfo))
				ImGui.SetClipboardText(song.AdditionalInfo);
			if (ImGui.MenuItem(duration))
				ImGui.SetClipboardText($"{song.Duration:mm\\:ss}");
			if (ImGui.MenuItem(songFilePath))
				ImGui.SetClipboardText(song.FilePath);
			ImGui.Separator();
			if (ImGui.MenuItem(all))
			{
				var text = $"{songId}: {song.Id}\n" +
				           $"{songName}: {song.Name}\n" +
				           $"{songLocation}: {song.Locations}\n" +
				           $"{songAdditionalInfo}: {song.AdditionalInfo}\n" +
				           $"{duration}: {song.Duration:mm\\:ss}\n" +
				           $"{songFilePath}: {song.FilePath}";
				ImGui.SetClipboardText(text);
			}
			ImGui.EndMenu();
		}
	}

	private static string BuildCopyString(HashSet<int> songs, Func<Song, string> field)
	{
		var sb = new StringBuilder();
		foreach (var song in songs)
			sb.Append($"{field.Invoke(SongList.Instance.GetSong(song))}, ");
		var text = sb.ToString();
		return text[^2..];
	}
}