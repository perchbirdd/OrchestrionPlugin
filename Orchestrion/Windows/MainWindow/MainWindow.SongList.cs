using Dalamud.Interface;
using ImGuiNET;
using Orchestrion.Persistence;

namespace Orchestrion.Windows.MainWindow;

public partial class MainWindow
{
	private void DrawSongList()
	{
		// to keep the tab bar always visible and not have it get scrolled out
		ImGui.BeginChild("##_songList_internal", ImGuiHelpers.ScaledVector2(-1f, -25f));

		if (ImGui.BeginTable("_songList table", 4, ImGuiTableFlags.SizingFixedFit))
		{
			ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
            
			foreach (var s in SongList.Instance.GetSongs())
			{
				var song = s.Value;
				if (!SearchMatches(song)) continue;

				ImGui.TableNextRow();
				ImGui.TableNextColumn();

				DrawSongListItem(song);
			}

			ImGui.EndTable();
		}

		ImGui.EndChild();
		DrawFooter();
	}
}