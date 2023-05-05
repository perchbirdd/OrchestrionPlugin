using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Struct;
using Orchestrion.UI.Components;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
	private Playlist _selectedPlaylist;
	private int _selectedPlaylistIndex = -1;

	private string _newPlaylistName = "";
	private bool _newPlaylistModal;
	private int _newPlaylistSong;

	private int _toDelete = -1;
	private string _playlistToDelete;
	private int _playlistDeletionPhase;

	private float _basePlaylistPaneSize = 150f;
	private float _deltaPlaylistPaneSize = 0f;
	private float _playlistPaneConfigSize = 150f;
	private float _startDragY = 0f;
	
	private float PlaylistPaneSize {
		get
		{
			var b = _deltaPlaylistPaneSize != 0f ? _deltaPlaylistPaneSize + _basePlaylistPaneSize : _playlistPaneConfigSize;
			return Configuration.Instance.PlaylistPaneOpen ? b : 25f;
		}
	}

	private void DrawPlaylistsTab()
	{
		if (!Configuration.Instance.ShowMiniPlayer)
			Player.Draw();
		DrawPlaylistSongs();
		DrawPlaylistPane();
	}
	
	private void DrawPlaylistSongs()
	{
		ImGui.PushFont(OrchestrionPlugin.LargeFont);
		var text = _selectedPlaylist?.Name ?? Loc.Localize("NoPlaylistSelected", "No Playlist Selected");
		var pSize = ImGui.CalcTextSize(text);
		ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - pSize.X) / 2);
		ImGui.Text(text);
		ImGui.PopFont();
		ImGui.Separator();
		
		ImGui.BeginChild("##playlist_internal", ImGuiHelpers.ScaledVector2(-1f, -1 * PlaylistPaneSize));
		if (_selectedPlaylist != null)
		{
			_playlistSongList.Draw();
		}
		else
		{
			var selectPlaylistText = Loc.Localize("SelectAPlaylist", "Select a playlist.");
			var size = ImGui.CalcTextSize(selectPlaylistText);
			ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - size.X) / 2);
			ImGui.Text(selectPlaylistText);
		}
		ImGui.EndChild();
	}

	private void DrawPlaylistPane()
	{
		DrawPlaylistPaneButton();

		ImGui.BeginChild("##playlist_list", ImGuiHelpers.ScaledVector2(-1f, PlaylistPaneSize));

		if (!Configuration.Instance.PlaylistPaneOpen)
		{
			ImGui.EndChild();
			return;
		}

		if (ImGui.BeginTable($"playlistpane", 3, ImGuiTableFlags.SizingFixedFit))
		{
			ImGui.TableSetupColumn("playing", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("del", ImGuiTableColumnFlags.WidthFixed);

			foreach (var playlist in Configuration.Instance.Playlists.Values)
			{
				var pName = playlist.Name;

				var trash = FontAwesomeIcon.Trash;
				var iconSize = Util.GetIconSize(trash);
				var drawHeight = iconSize.Y + ImGui.GetStyle().FramePadding.Y * 2;
				var drawWidth = iconSize.X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ScrollbarSize;

				ImGui.TableNextColumn();
				ImGui.PushFont(UiBuilder.IconFont);
				var fakePaddingA = new Vector2(6, 0);
				var fakePaddingB = new Vector2(fakePaddingA.X - ImGui.GetStyle().ItemSpacing.X, 0);
				
				if (PlaylistManager.CurrentPlaylist?.Name == pName)
				{
					ImGui.Dummy(fakePaddingB);
					ImGui.SameLine();
					ImGui.Text(FontAwesomeIcon.Play.ToIconString());	
				}
				else
				{
					var size = ImGui.CalcTextSize(FontAwesomeIcon.Play.ToIconString());
					ImGui.Dummy(size + fakePaddingA);
				}
				ImGui.PopFont();
				
				ImGui.TableNextColumn();
				if (ImGui.Selectable(pName, 
					    _selectedPlaylist == playlist,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowItemOverlap,
					    ImGuiHelpers.ScaledVector2(0f, drawHeight)
				    ))
				{
					SetSelectedPlaylist(playlist);
					
					if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && playlist.Songs.Count > 0)
					{
						PlaylistManager.Play(pName);
					}
				}

				if (ImGui.BeginPopupContextItem($"{pName}context"))
				{
					SetSelectedPlaylist(playlist);

					if (ImGui.MenuItem(Loc.Localize("Play", "Play")))
					{
						PlaylistManager.Play(pName);
					}
					if (ImGui.MenuItem(Loc.Localize("Repeat", "Repeat")))
					{
						playlist.RepeatMode = RepeatMode.All;
						Configuration.Instance.Save();
						PlaylistManager.Play(pName);
					}
					if (ImGui.MenuItem(Loc.Localize("Shuffle", "Shuffle")))
					{
						playlist.ShuffleMode = ShuffleMode.On;
						Configuration.Instance.Save();
						PlaylistManager.Play(pName);
					}
					ImGui.EndPopup();
				}

				ImGui.TableNextColumn();

				var colorsPushed = 0;
				if (_playlistDeletionPhase == 1 && _playlistToDelete == pName)
				{
					ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
					ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGuiColors.DalamudRed);
					colorsPushed = 2;
				}

				ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
				if (ImGuiComponents.IconButton($"##{pName}_del", trash))
				{
					Task.Delay(2000).ContinueWith(ResetDeletion);
					if (_playlistDeletionPhase == 0 || _playlistToDelete != pName)
					{
						_playlistToDelete = pName;
						_playlistDeletionPhase = 1;
					}
					else if (_playlistDeletionPhase == 1)
					{
						_playlistDeletionPhase = 2;
					}
				}
				ImGui.PopStyleVar();

				if (ImGui.IsItemHovered())
				{
					if (_playlistDeletionPhase == 1 && _playlistToDelete == pName)
					{
						ImGui.SetTooltip(Loc.Localize("ClickAgainDelete", "Click again to confirm deletion"));
					}
					else
					{
						ImGui.SetTooltip(Loc.Localize("Delete", "Delete"));
					}
				}

				ImGui.PopStyleColor(colorsPushed);
				ImGui.TableNextRow();
			}

			ImGui.EndTable();
		}
		
		if (ImGui.Button(Loc.Localize("NewPlaylistEllipsis", "New playlist..."), ImGuiHelpers.ScaledVector2(-1f, 0f)))
			_newPlaylistModal = true;

		// I don't know why the bottom is cut off, so I'm just going to do this and pretend it's not.
		ImGui.Dummy(ImGuiHelpers.ScaledVector2(-1f, 26f));
		ImGui.EndChild();

		if (_playlistToDelete != null && _playlistDeletionPhase == 2)
		{
			if (PlaylistManager.CurrentPlaylist?.Name == _playlistToDelete)
				PlaylistManager.Stop();
			if (_selectedPlaylist?.Name == _playlistToDelete)
				SetSelectedPlaylist(null);

			Configuration.Instance.DeletePlaylist(_playlistToDelete);
			_playlistToDelete = null;
			_playlistDeletionPhase = 0;
		}
	}

	private void SetSelectedPlaylist(Playlist playlist)
	{
		_selectedPlaylist = playlist;
		if (_selectedPlaylist != null)
			_playlistSongList.SetListSource(playlist.Songs.Select(s => new RenderableSongEntry(s)).ToList());
		else
			_playlistSongList.SetListSource(new List<RenderableSongEntry>());
	}

	private void DrawPlaylistPaneButton()
	{
		ImGui.Separator();

		// var mouseDownOver = false;
		// if (Configuration.Instance.PlaylistPaneOpen && ImGui.IsItemHovered())
		// {
		// 	ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
		//
		// 	if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
		// 		mouseDownOver = true;
		// }
		//
		// if (_startDragY != 0f && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
		// {
		// 	_startDragY = 0f;
		// 	_playlistPaneConfigSize = _deltaPlaylistPaneSize + _basePlaylistPaneSize;
		// 	_deltaPlaylistPaneSize = 0f;
		// 	_basePlaylistPaneSize = _playlistPaneConfigSize;
		// }
		// else if (mouseDownOver || _startDragY != 0f)
		// {
		// 	if (_startDragY == 0f)
		// 		_startDragY = ImGui.GetMousePos().Y;
		//
		// 	_deltaPlaylistPaneSize = -1 * (ImGui.GetMousePos().Y - _startDragY);
		// 	PluginLog.Debug($"drag {_startDragY} {_basePlaylistPaneSize} {_deltaPlaylistPaneSize} {_playlistPaneConfigSize}");
		// }
		
		var icon = Configuration.Instance.PlaylistPaneOpen ? FontAwesomeIcon.ArrowDown : FontAwesomeIcon.ArrowUp;
		var text = Loc.Localize("Playlists", "Playlists");

		var iconSize = Util.GetIconSize(icon);
		var textSize = ImGui.CalcTextSize(text);
		var padding = ImGui.GetStyle().FramePadding;
		var spacing = ImGui.GetStyle().ItemSpacing;
		var width = ImGui.GetContentRegionAvail().X;
		var firstIconStart = width / 2 - textSize.X - iconSize.X - spacing.X;
		var secondIconStart = width / 2 + textSize.X + spacing.X;

		var buttonSizeY = (iconSize.Y > textSize.Y ? iconSize.Y : textSize.Y) + padding.Y * 2;
		var buttonSize = new Vector2(-1f, buttonSizeY);

		if (ImGui.Button($"{text}###playlist_open", buttonSize))
		{
			Configuration.Instance.PlaylistPaneOpen = !Configuration.Instance.PlaylistPaneOpen;
			Configuration.Instance.Save();
		}

		ImGui.SameLine();
		ImGui.SetCursorPosX(firstIconStart);
		ImGui.PushFont(UiBuilder.IconFont);
		ImGui.Text(icon.ToIconString());
		ImGui.SameLine();
		ImGui.SetCursorPosX(secondIconStart);
		ImGui.Text(icon.ToIconString());
		ImGui.PopFont();
	}

	// private void DrawSongListItem(Song song, Playlist playlist, int songIndex)
	// {
	// 	if (PlaylistManager.IsPlaying && PlaylistManager.CurrentPlaylist == playlist && PlaylistManager.CurrentSongIndex == songIndex)
	// 	{
	// 		ImGui.PushFont(UiBuilder.IconFont);
	// 		ImGui.Text(FontAwesomeIcon.Play.ToIconString());
	// 		ImGui.PopFont();
	// 	}
	// 	else
	// 	{
	// 		var size = Util.GetIconSize(FontAwesomeIcon.Play);
	// 		ImGui.Dummy(size);
	// 	}
	// 	ImGui.TableNextColumn();
	// 	ImGui.Text(song.Id.ToString());
	// 	ImGui.TableNextColumn();
	//
	// 	var selected = _selectedPlaylist == playlist && _selectedPlaylistIndex == songIndex;
	//
	// 	if (ImGui.Selectable($"{song.Name}##{song.Id}", selected, ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.SpanAllColumns))
	// 	{
	// 		_selectedPlaylist = playlist;
	// 		_selectedPlaylistIndex = songIndex;
	//
	// 		if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
	// 			PlaylistManager.Play(playlist.Name, songIndex);
	// 	}
	//
	// 	if (ImGui.IsItemHovered())
	// 		BgmTooltip.DrawBgmTooltip(song);
	//
	// 	if (ImGui.BeginPopupContextItem())
	// 	{
	// 		_selectedPlaylist = playlist;
	// 		_selectedPlaylistIndex = songIndex;
	//
	// 		// DrawCopyContentSubmenu(song);
	// 		ImGui.Separator();
	// 		// DrawPlaylistAddSubmenu(song);
	//
	// 		if (ImGui.MenuItem("Delete"))
	// 			_toDelete = songIndex;
	//
	// 		ImGui.EndPopup();
	// 	}
	//
	// 	ImGui.TableNextColumn();
	// }
	//
	// private void DrawNewPlaylistModal()
	// {
	// 	if (_newPlaylistModal)
	// 		ImGui.OpenPopup("Create New Playlist");
	//
	// 	var a = true;
	// 	if (ImGui.BeginPopupModal($"Create New Playlist", ref a, ImGuiWindowFlags.AlwaysAutoResize))
	// 	{
	// 		ImGui.Text("Enter a name for your playlist:");
	// 		if (ImGui.IsWindowAppearing())
	// 			ImGui.SetKeyboardFocusHere();
	// 		var yes = ImGui.InputText("##newplaylistname", ref _newPlaylistName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
	// 		var invalid = string.IsNullOrWhiteSpace(_newPlaylistName)
	// 		              || string.IsNullOrEmpty(_newPlaylistName)
	// 		              || Configuration.Instance.Playlists.ContainsKey(_newPlaylistName);
	// 		ImGui.BeginDisabled(invalid);
	// 		yes |= ImGui.Button("Create");
	//
	// 		if (yes)
	// 		{
	// 			var songs = new List<int>();
	// 			if (_newPlaylistSong != 0)
	// 				songs.Add(_newPlaylistSong);
	// 			Configuration.Instance.Playlists.Add(_newPlaylistName!, new Playlist(_newPlaylistName, songs));
	// 			Configuration.Instance.Save();
	// 			_newPlaylistName = "";
	// 			_newPlaylistSong = 0;
	// 			_newPlaylistModal = false;
	// 			ImGui.CloseCurrentPopup();
	// 		}
	// 		ImGui.EndDisabled();
	// 		ImGui.SameLine();
	// 		if (ImGui.Button("Cancel"))
	// 		{
	// 			_newPlaylistName = "";
	// 			_newPlaylistSong = 0;
	// 			_newPlaylistModal = false;
	// 			ImGui.CloseCurrentPopup();
	// 		}
	// 		ImGui.EndPopup();
	// 	}
	// }

	private void ResetDeletion(Task task)
	{
		_playlistToDelete = null;
		_playlistDeletionPhase = 0;
	}
}