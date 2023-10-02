namespace Orchestrion.Types;

public struct RenderableSongEntry
{
	public int Id;
	public DateTime TimePlayed;

	public RenderableSongEntry(int id)
	{
		Id = id;
		TimePlayed = default;
	}
	
	public RenderableSongEntry(int id, DateTime timePlayed)
	{
		Id = id;
		TimePlayed = timePlayed;
	}
	
	public override string ToString()
	{
		return $"[{Id}] {TimePlayed}";
	}
}