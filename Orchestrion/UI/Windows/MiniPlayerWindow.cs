using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Orchestrion.Persistence;
using Orchestrion.UI.Components;

namespace Orchestrion.UI.Windows;

public class MiniPlayerWindow : Window
{
	private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar;
	
	public MiniPlayerWindow() : base("###orchestrion_miniplayer", BaseFlags)
	{
		SizeCondition = ImGuiCond.Once;
		Size = ImGuiHelpers.ScaledVector2(300, 100);
		RespectCloseHotkey = false;
		IsOpen = true;
	}

	public override bool DrawConditions() => Configuration.Instance.ShowMiniPlayer;

	public override void PreDraw()
	{
		ImGui.PushStyleColor(ImGuiCol.ResizeGrip, 0);
		ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, 0);
		ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, 0);

		if (Configuration.Instance.MiniPlayerLock)
			Flags |= (ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
		else
			Flags &= (~ImGuiWindowFlags.NoMove & ~ImGuiWindowFlags.NoResize);
		
		BgAlpha = Configuration.Instance.MiniPlayerOpacity;
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
	}

	public override void PostDraw()
	{
		// PluginLog.Debug("PostDraw");
		ImGui.PopStyleColor(3);
		ImGui.PopStyleVar(2);
	}

	public override void Draw()
	{
		Player.Draw();
	}
}