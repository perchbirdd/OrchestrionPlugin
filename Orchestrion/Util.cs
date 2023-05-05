using System.Numerics;
using Dalamud;
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

	public static string Lang()
	{
		return DalamudApi.PluginInterface.UiLanguage;
	}

	public static string AltLang()
	{
		return Lang() switch
		{
			"en" => "ja",
			"ja" => "en",
			_ => "en",
		};
	}

	public static string ClientLangCode()
	{
		return DalamudApi.ClientState.ClientLanguage switch
		{
			ClientLanguage.Japanese => "ja",
			ClientLanguage.English => "en",
			ClientLanguage.German => "de",
			ClientLanguage.French => "fr",
			_ => throw new ArgumentOutOfRangeException()
		};
	}
}