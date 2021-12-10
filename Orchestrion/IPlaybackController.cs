
namespace Orchestrion
{
    interface IPlaybackController
    {
        int CurrentSong { get; }
        void PlaySong(int songId);
        void StopSong();
        void DumpDebugInformation();
    }
}
