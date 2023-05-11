using System.Numerics;
using Dalamud;
using Dalamud.Interface;
using ImGuiNET;
using Microsoft.VisualBasic.CompilerServices;
using Orchestrion.Persistence;
using Orchestrion.Struct;

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

	public static bool SearchMatches(string searchText, int songId)
	{
		return SongList.Instance.TryGetSong(songId, out var song) && SearchMatches(searchText, song);
	}

	public static bool SearchMatches(string searchText, Song song)
	{
		if (searchText.Length == 0) return true;

		var lang = Lang();

		var matchesSearch = false;
		
		// En title check
		matchesSearch |= song.Strings["en"].Name.ToLower().Contains(searchText.ToLower());
		matchesSearch |= song.Strings["en"].AlternateName.ToLower().Contains(searchText.ToLower());
		matchesSearch |= song.Strings["en"].SpecialModeName.ToLower().Contains(searchText.ToLower());
		
		// Ja title check
		matchesSearch |= song.Strings["ja"].Name.ToLower().Contains(searchText.ToLower());
		matchesSearch |= song.Strings["ja"].AlternateName.ToLower().Contains(searchText.ToLower());
		matchesSearch |= song.Strings["ja"].SpecialModeName.ToLower().Contains(searchText.ToLower());
		
		// Localized addtl info check
		matchesSearch |= song.Strings[lang].Locations.ToLower().Contains(searchText.ToLower());
		matchesSearch |= song.Strings[lang].AdditionalInfo.ToLower().Contains(searchText.ToLower());

		// Id check
		matchesSearch |= song.Id.ToString().Contains(searchText);
		
		return matchesSearch;
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