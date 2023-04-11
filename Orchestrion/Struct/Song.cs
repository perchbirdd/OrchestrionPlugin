namespace Orchestrion.Struct;

public struct Song
{
	public int Id;
	public string Name;
	public string Locations;
	public string AdditionalInfo;
	public bool DisableRestart;
	public byte SpecialMode;
	public bool FileExists;
	public TimeSpan Duration;
}