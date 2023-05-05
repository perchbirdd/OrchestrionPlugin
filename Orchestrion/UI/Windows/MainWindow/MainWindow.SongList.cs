using Dalamud.Interface;
using ImGuiNET;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
	private void DrawSongList()
	{
		// to keep the tab bar always visible and not have it get scrolled out
		ImGui.BeginChild("##_songList_internal", ImGuiHelpers.ScaledVector2(-1f, -25f));

		_mainSongList.Draw();

		ImGui.EndChild();
		DrawFooter();
	}
}