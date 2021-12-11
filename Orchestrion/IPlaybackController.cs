
namespace Orchestrion
{
    interface IPlaybackController
    {
        int CurrentSong { get; }
        void PlaySong(int songId, bool isReplacement = false);
        void StopSong();
        void DumpDebugInformation();
    }
}
