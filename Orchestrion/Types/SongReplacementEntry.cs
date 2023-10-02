namespace Orchestrion.Types;

public struct SongReplacementEntry
{
	public const int NoChangeId = -1;
    
	/// <summary>
	/// The ID of the song to replace.
	/// </summary>
	public int TargetSongId;

	/// <summary>
	/// The ID of the replacement track to play. -1 (NoChangeId) means "ignore this song"
	/// </summary>
	public int ReplacementId;
}