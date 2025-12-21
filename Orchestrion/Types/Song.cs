using System.Collections.Generic;

namespace Orchestrion.Types;

public struct SongStrings
{
	public string Name = string.Empty;
	public string AlternateName = string.Empty;
	public string SpecialModeName = string.Empty;
	public string Locations = string.Empty;
	public string AdditionalInfo = string.Empty;

	public SongStrings() { }
}

public struct Song
{
	public int Id;
	public Dictionary<string, SongStrings> Strings;
	public bool DisableRestart;
	public byte SpecialMode;
	public string FilePath;
	public bool FileExists;
	public TimeSpan Duration;
	
	public Song(Dictionary<string, SongStrings> strings, string filePath)
	{
		Strings = strings;
		FilePath = string.Empty;
	}

	public Song(string filePath)
	{
		FilePath = filePath;
		Strings = new Dictionary<string, SongStrings>();
	}

	public string Name => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).Name ?? string.Empty;
	public string AlternateName => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).AlternateName ?? string.Empty;
	public string SpecialModeName => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).SpecialModeName ?? string.Empty;
	public string Locations => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).Locations ?? string.Empty;
	public string AdditionalInfo => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).AdditionalInfo ?? string.Empty;
}