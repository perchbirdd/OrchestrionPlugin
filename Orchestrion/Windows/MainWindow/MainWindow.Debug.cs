using CheapLoc;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.BGMSystem;

namespace Orchestrion.Windows.MainWindow;

public partial class MainWindow
{
	private void DrawDebug()
	{
		var addr = BGMAddressResolver.BGMSceneManager;
		if (addr == IntPtr.Zero) return;
		var addrStr = $"{addr.ToInt64():X}";
		ImGui.Text(addrStr);
		if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
			ImGui.SetClipboardText(addrStr);
		ImGui.Text($"streaming enabled: {BGMAddressResolver.StreamingEnabled}");
		ImGui.Text($"PlayingScene: {BGMManager.PlayingScene}");
		ImGui.Text($"PlayingSongId: {BGMManager.PlayingSongId}");
		// ImGui.Text($"OldScene: {BGMManager.OldScene}");
		// ImGui.Text($"OldSongId: {BGMManager.OldSongId}");
		// ImGui.Text($"OldSecondScene: {BGMManager.OldSecondScene}");
		// ImGui.Text($"OldSecondSongId: {BGMManager.OldSecondSongId}");
		ImGui.Text($"Audible: {BGMManager.CurrentAudibleSong}");
		if (ImGui.Button("export loc"))
		{
			Loc.ExportLocalizable(true);
		}
	}
}