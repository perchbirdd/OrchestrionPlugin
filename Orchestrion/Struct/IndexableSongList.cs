using System.Collections.Generic;
using System.Linq;

namespace Orchestrion.Struct;

public class IndexableSongList
{
	public List<Song> Source { get; set; }
	public List<int> Selected { get; set; }
	
	public IndexableSongList()
	{
		Source = new List<Song>();
		Selected = new List<int>();
	}
	
	public IndexableSongList(List<Song> source, List<int> selected)
	{
		Source = source;
		Selected = selected;
	}
	
	public List<Song> GetSelectedSongs()
	{
		var selectedSongs = Selected.Select(index => Source[index]).ToList();
		return selectedSongs;
	}
	
	public Song GetFirstSelectedSong()
	{
		return Selected.Count > 0 ? Source[0] : default;
	}
}