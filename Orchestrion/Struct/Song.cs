using System.Collections.Generic;

namespace Orchestrion.Struct;

public struct SongStrings
{
	public string Name;
	public string AlternateName;
	public string SpecialModeName;
	public string Locations;
	public string AdditionalInfo;
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
	
	public Song(Dictionary<string, SongStrings> strings)
	{
		Strings = strings;
	}

	public Song()
	{
		Strings = new Dictionary<string, SongStrings>();
	}

	public string Name => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).Name;
	public string AlternateName => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).AlternateName;
	public string SpecialModeName => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).SpecialModeName;
	public string Locations => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).Locations;
	public string AdditionalInfo => Strings.GetValueOrDefault(Util.Lang(), Strings["en"]).AdditionalInfo;
}