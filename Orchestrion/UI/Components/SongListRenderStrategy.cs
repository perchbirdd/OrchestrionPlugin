using System.Collections.Generic;
using System.Linq;
using Orchestrion.Audio;
using Orchestrion.Struct;

namespace Orchestrion.UI.Components;

public class SongListRenderStrategy
{
	public Func<bool> RenderBackwards { get; init; }
	public Func<List<RenderableSongEntry>, int, bool> IsPlaying { get; init; }
	public Action<RenderableSongEntry, int> PlaySong { get; init; }
	public Func<bool> SourceMutable { get; init; }
	public Action<int> RemoveSong { get; init; }

	public SongListRenderStrategy()
	{
		RenderBackwards = () => false;
		IsPlaying = (entry, index) => BGMManager.CurrentAudibleSong == entry.ElementAtOrDefault(index).Id;
		PlaySong = (entry, index) => BGMManager.Play(entry.Id);
		SourceMutable = () => false;
		RemoveSong = index => { };
	}
}