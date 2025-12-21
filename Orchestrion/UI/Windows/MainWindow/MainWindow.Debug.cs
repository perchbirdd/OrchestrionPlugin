using System;
using CheapLoc;
using Dalamud.Bindings.ImGui;
using Orchestrion.Audio;
using Orchestrion.BGMSystem;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
    private void DrawDebugTab()
    {
        try
        {
            var addr = BGMAddressResolver.BGMSceneManager;
            if (addr == IntPtr.Zero)
            {
                ImGui.Text("BGMSceneManager Address is Zero (Scan failed?)");
                return;
            }

            var addrStr = $"{addr.ToInt64():X}";
            ImGui.Text($"Address: {addrStr}");
            
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                ImGui.SetClipboardText(addrStr);

            ImGui.Text($"Streaming Enabled: {BGMAddressResolver.StreamingEnabled}");
            
            try 
            {
                ImGui.Text($"PlayingScene: {BGMManager.PlayingScene}");
                ImGui.Text($"PlayingSongId: {BGMManager.PlayingSongId}");
                ImGui.Text($"Audible: {BGMManager.CurrentAudibleSong}");
            }
            catch (NullReferenceException)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Error: BGMManager not initialized.");
            }

            if (ImGui.Button("Export Loc"))
            {
                Loc.ExportLocalizable(true);
            }
            
            try
            {
                ImGui.Text($"DD Mode: {BGMManager.DeepDungeonModeActive()}");
            }
            catch
            {
                ImGui.Text("DD Mode: <Error>");
            }
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), $"Critical Debug Tab Error: {ex.Message}");
        }
    }
}