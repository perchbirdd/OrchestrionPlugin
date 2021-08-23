
namespace Orchestrion
{
    interface IPlaybackController
    {
        ushort CurrentSong { get; }
        bool EnableFallbackPlayer { get; set; }
        void PlaySong(int songId);
        void StopSong();

        void DumpDebugInformation();
    }
}
