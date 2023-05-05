using System.Collections.Generic;
using ImGuiNET;
using Orchestrion.Persistence;

namespace Orchestrion.UI.Windows;

public class NewPlaylistModal
{
	private string _newPlaylistName = string.Empty;
	private List<int> _newPlaylistSongs = new();
	private bool _isOpen;

	public void Show(List<int> songs)
	{
		_newPlaylistSongs = songs;
		_isOpen = true;
	}
	
	public void Close()
	{
		_newPlaylistName = "";
		_newPlaylistSongs = null;
		_isOpen = false;
		ImGui.CloseCurrentPopup();
	}

	public void Draw()
	{
		if (_isOpen)
			ImGui.OpenPopup("Create New Playlist");

		var a = true;
		if (ImGui.BeginPopupModal($"Create New Playlist", ref a, ImGuiWindowFlags.AlwaysAutoResize))
		{
			ImGui.Text("Enter a name for your playlist:");
			if (ImGui.IsWindowAppearing())
				ImGui.SetKeyboardFocusHere();
			var yes = ImGui.InputText("##newplaylistname", ref _newPlaylistName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
			var invalid = string.IsNullOrWhiteSpace(_newPlaylistName)
			              || string.IsNullOrEmpty(_newPlaylistName)
			              || Configuration.Instance.Playlists.ContainsKey(_newPlaylistName);
			ImGui.BeginDisabled(invalid);
			yes |= ImGui.Button("Create");

			if (yes)
			{
				var songs = new List<int>();
				if (_newPlaylistSongs.Count != 0)
					songs.AddRange(_newPlaylistSongs);
				Configuration.Instance.Playlists.Add(_newPlaylistName!, new Playlist(_newPlaylistName, songs));
				Configuration.Instance.Save();
				Close();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if (ImGui.Button("Cancel"))
				Close();
			ImGui.EndPopup();
		}
	}
}