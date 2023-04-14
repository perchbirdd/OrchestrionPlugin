using System.Collections.Generic;

namespace Orchestrion.Persistence;

public class Playlist
{
	public string Name { get; set; }
	public List<int> Songs { get; set; }
	public RepeatMode RepeatMode { get; set; }
	public ShuffleMode ShuffleMode { get; set; }
	
	public Playlist() { }
	
	public Playlist(string name)
	{
		RepeatMode = RepeatMode.All;
		ShuffleMode = ShuffleMode.Off;
		
		Name = name;
		Songs = new List<int>();
	}
	
	public Playlist(string name, List<int> songs) : this(name)
	{
		Songs = songs;
	}
	
	public void NextRepeatMode()
	{
		RepeatMode = (RepeatMode) ((int)(RepeatMode + 1) % 3);
		Configuration.Instance.Save();
	}
	
	public void NextShuffleMode()
	{
		ShuffleMode = (ShuffleMode) ((int)(ShuffleMode + 1) % 2);
		Configuration.Instance.Save();
	}

	public void AddSong(int songId)
	{
		Songs.Add(songId);
		Configuration.Instance.Save();
	}
	
	public void AddSongs(IEnumerable<int> songIds)
	{
		Songs.AddRange(songIds);
		Configuration.Instance.Save();
	}
	
	public void RemoveSong(int index)
	{
		Songs.RemoveAt(index);
		Configuration.Instance.Save();
	}
}