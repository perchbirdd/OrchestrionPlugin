using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Hooking;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;

namespace Orchestrion
{
    [Flags]
    enum SceneFlags : byte
    {
        None = 0,
        Unknown = 1,
        Resume = 2,
        EnablePassEnd = 4,
        ForceAutoReset = 8,
        EnableDisableRestart = 16,
        IgnoreBattle = 32,
    }
    
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct BGMScene
    {
        public int priorityIndex;
        public SceneFlags flags;
        private int padding1;
        // often writing songId will cause songId2 and 3 to be written automatically
        // songId3 is sometimes not updated at all, and I'm unsure of its use
        // zeroing out songId2 seems to be necessary to actually cancel playback without using
        // an invalid id (which is the only way to do it with just songId1)
        public ushort bgmReference;       // Reference to sheet; BGM, BGMSwitch, BGMSituation
        public ushort bgmId;              // Actual BGM that's playing. Game will manage this if it's a switch or situation
        public ushort previousBgmId;      // BGM that was playing before this one; I think it only changed if the previous BGM 
        public byte timerEnable;            // whether the timer automatically counts up
        private byte padding2;
        public float timer;                 // if enabled, seems to always count from 0 to 6
        // if 0x30 is 0, up through 0x4F are 0
        // in theory function params can be written here if 0x30 is non-zero but I've never seen it
        private fixed byte disableRestartList[24]; // 'vector' of bgm ids that will be restarted - managed by game. it is 3 pointers
        private byte unknown1;
        private uint unknown2;
        private uint unknown3;
        private uint unknown4;
        private uint unknown5;
        private uint unknown6;
        private ulong unknown7;
        private uint unknown8;
        private byte unknown9;
        private byte unknown10;
        private byte unknown11;
        private byte unknown12;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct BasicBGMPlayer
    {
        [FieldOffset(0x08)] public uint bgmId;
        [FieldOffset(0x10)] public uint bgmScene;
        [FieldOffset(0x20)] public uint specialMode;
        [FieldOffset(0x4D)] public byte specialModeType;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DisableRestart
    {
        public ushort disableRestartId;
        public bool isTimedOut; // ?
        public byte padding1;
        public float resetWaitTime;
        public float elapsedTime;
        public bool timerEnabled;
        // 3 byte padding
    }

    public static class BGMController
    {
        /// <summary>
        /// The last song that the game was previously playing.
        /// </summary>
        public static int OldSongId { get; private set; }
        
        /// <summary>
        /// The scene that OldSongId was playing at.
        /// </summary>
        public static int OldScene { get; private set; }
        
        /// <summary>
        /// The song that the game is currently playing. That is, the song
        /// that is currently playing at the highest scene that is not
        /// PlayingPriority.
        /// </summary>
        public static int CurrentSongId { get; private set; }
        
        /// <summary>
        /// The scene that CurrentSongId is playing at.
        /// </summary>
        public static int CurrentScene { get; private set; }
        
        /// <summary>
        /// The song that the game is currently playing behind CurrentSongId.
        /// </summary>
        public static int SecondSongId { get; private set; }
        
        /// <summary>
        /// The scene that SecondSongId is currently playing at.
        /// </summary>
        public static int SecondScene { get; private set; }
        
        /// <summary>
        /// The previous song that the game was playing behind CurrentSongId.
        /// </summary>
        public static int OldSecondSongId { get; private set; }
        
        /// <summary>
        /// The scene that OldSecondSongId was playing at.
        /// </summary>
        public static int OldSecondScene { get; private set; }

        /// <summary>
        /// The song that Orchestrion is currently playing.
        /// </summary>
        public static int PlayingSongId { get; private set; }
        
        /// <summary>
        /// The scene that PlayingSongId is playing at.
        /// </summary>
        public static int PlayingScene { get; private set; }

        /// <summary>
        /// The event that fires when the game changes songs.
        /// Note that there are no song parameters - up-to-date fields of BGMController
        /// are available at all times.
        /// </summary>
        public delegate void SongChangedHandler(bool currentChanged, bool secondChanged);
        public static SongChangedHandler OnSongChanged;

        private const int SceneCount = 12;

        private const SceneFlags SceneZeroFlags = SceneFlags.Resume;
        
        private unsafe delegate DisableRestart* AddDisableRestartIdPrototype(BGMScene* scene, ushort songId);
        private static readonly AddDisableRestartIdPrototype AddDisableRestartId;
        
        private unsafe delegate int GetSpecialModeByScenePrototype(BasicBGMPlayer* bgmPlayer, byte specialModeType);
        private static readonly Hook<GetSpecialModeByScenePrototype> GetSpecialModeForSceneHook;
        
        static unsafe BGMController()
        {
            AddDisableRestartId = Marshal.GetDelegateForFunctionPointer<AddDisableRestartIdPrototype>(BGMAddressResolver.AddRestartId);
            GetSpecialModeForSceneHook = new Hook<GetSpecialModeByScenePrototype>(BGMAddressResolver.GetSpecialMode, GetSpecialModeBySceneDetour);
            GetSpecialModeForSceneHook.Enable();
        }

        public static void Dispose()
        {
            GetSpecialModeForSceneHook?.Disable();
            GetSpecialModeForSceneHook?.Dispose();
        }

        public static void SetSpecialModeHandling(bool value)
        {
            if (value)
                GetSpecialModeForSceneHook.Enable();
            else
                GetSpecialModeForSceneHook.Disable();
        }

        public static void Update()
        {
            ushort currentSong = 0;
            ushort secondSong = 0;
            var currentScene = 0;
            var secondScene = 0;

            if (BGMAddressResolver.BGMSceneList != IntPtr.Zero)
            {
                unsafe
                {
                    var bgms = (BGMScene*)BGMAddressResolver.BGMSceneList.ToPointer();
                    
                    for (int sceneIdx = 0; sceneIdx < SceneCount; sceneIdx++)
                    {
                        // This is so we can receive BGM change updates even while playing a song
                        if (PlayingSongId != 0 && sceneIdx == PlayingScene)
                        {
                            // If the game overwrote our song, play it again
                            if (bgms[PlayingScene].bgmId != PlayingSongId)
                                SetSong((ushort)PlayingSongId, PlayingScene);
                            continue;
                        }
                        
                        if (bgms[sceneIdx].bgmReference == 0) continue;

                        if (bgms[sceneIdx].bgmId != 0 && bgms[sceneIdx].bgmId != 9999)
                        {
                            if (currentSong == 0)
                            {
                                currentSong = bgms[sceneIdx].bgmId;
                                currentScene = sceneIdx;
                            }
                            else
                            {
                                secondSong = bgms[sceneIdx].bgmId;
                                secondScene = sceneIdx;
                                break;
                            }
                        }
                    }
                }
            }
            
            var currentChanged = false;
            var secondChanged = false;
            
            if (CurrentSongId != currentSong)
            {
                OldSongId = CurrentSongId;
                OldScene = CurrentScene;
                CurrentSongId = currentSong;
                CurrentScene = currentScene;
                currentChanged = true;
            }
            
            if (SecondSongId != secondSong)
            {
                OldSecondSongId = SecondSongId;
                OldSecondScene = SecondScene;
                SecondSongId = secondSong;
                SecondScene = secondScene;
                secondChanged = true;
            }

            if (currentChanged || secondChanged) OnSongChanged?.Invoke(currentChanged, secondChanged);
        }

        // private static bool CurrentSongShouldBeIgnored()
        // {
        //     if (OrchestrionPlugin.Configuration.SongReplacements.TryGetValue(CurrentSongId, out var potentialReplacement))
        //     {
        //         if (PlayingSongId != 0 && potentialReplacement.ReplacementId == SongReplacement.NoChangeId)
        //             return true;
        //     }
        //
        //     return false;
        // } 
        
        public static void SetSong(ushort songId, int priority = 0)
        {
            if (priority < 0 || priority >= SceneCount) throw new IndexOutOfRangeException();

            if (BGMAddressResolver.BGMSceneList != IntPtr.Zero)
            {
                unsafe
                {
                    var bgms = (BGMScene*)BGMAddressResolver.BGMSceneList.ToPointer();
                    // sometimes we only have to set the first and it will set the other 2
                    // but particularly on stop/clear, the 2nd seems important as well
                    bgms[priority].bgmReference = songId;
                    bgms[priority].bgmId = songId;
                    bgms[priority].previousBgmId = songId;

                    if (songId == 0 && priority == 0)
                        bgms[priority].flags = SceneZeroFlags;

                    // these are probably not necessary, but clear them to be safe
                    bgms[priority].timer = 0;
                    bgms[priority].timerEnable = 0;

                    PlayingSongId = songId;
                    PlayingScene = priority;

                    // unk5 is set to 0x100 by the game in some cases for priority 0
                    // but I wasn't able to see that it did anything

                    if (!SongList.TryGetSong(songId, out var song)) return;

                    
                    // I hate my life
                    if (song.Bgm.DisableRestart)
                    {
                        bgms[priority].flags = SceneFlags.EnableDisableRestart;
                        var disableRestart = AddDisableRestartId(&bgms[priority], songId);
                        PluginLog.Debug($"AddDisableRestartId: {(ulong) disableRestart:X}");
                        bgms[priority].flags = SceneZeroFlags;
                        
                        // A lot.
                        Task.Delay(500).ContinueWith(_ =>
                        {
                            bgms[priority].flags = GetSceneFlagsNeededForBgm(song.Bgm);
                        });
                    }
                }
            }
        }

        private static unsafe int GetSpecialModeBySceneDetour(BasicBGMPlayer* player, byte specialModeType)
        {
            // Let the game do what it needs to do
            if (player->bgmScene != PlayingScene
                || player->bgmId != PlayingSongId
                || specialModeType == 0) 
                return GetSpecialModeForSceneHook.Original(player, specialModeType);
            
            if (!SongList.TryGetSong((int) player->bgmId, out var song)) return GetSpecialModeForSceneHook.Original(player, specialModeType);

            // Default to scene 10 behavior, but if the mode is mount mode, use the mount scene
            uint newScene = 10;
            if (song.Bgm.SpecialMode == 2)
                newScene = 6;
            
            // Trick the game into giving us the result we want for the scene our song should actually be playing on
            var tempScene = player->bgmScene;
            player->bgmScene = newScene;
            var result = GetSpecialModeForSceneHook.Original(player, specialModeType);
            player->bgmScene = tempScene;
            return result;
        }

        private static SceneFlags GetSceneFlagsNeededForBgm(BGM bgm)
        {
            // var sceneFlags = SceneFlags.None;
            var sceneFlags = SceneZeroFlags;
            if (bgm.DisableRestart) sceneFlags |= SceneFlags.EnableDisableRestart;
            // if (bgm.PassEnd) sceneFlags |= SceneFlags.EnablePassEnd;
            
            // This one is an assumption...
            // if (bgm.DisableRestartTimeOut) sceneFlags |= SceneFlags.Resume;

            return sceneFlags;
        }
    }
}
