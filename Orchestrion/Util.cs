using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace Orchestrion;

public static class Util
{
	public static Vector2 GetIconSize(FontAwesomeIcon icon)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		var size = ImGui.CalcTextSize(icon.ToIconString());
		ImGui.PopFont();
		return size;
	}
}