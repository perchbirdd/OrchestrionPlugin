using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dalamud.Game.Internal;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Orchestrion
{
    // TODO:
    // try to find what writes to bgm 0, block it if we are playing?
    //   or save/restore if we preempt it?
    // debug info of which priority is active
    //  notifications/logs of changes even to lower priorities?

    public unsafe class Plugin : IDalamudPlugin, IPlaybackController, IResourceLoader
    {
        public string Name => "Orchestrion plugin";
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;

        private const string songListFile = "xiv_bgm.csv";
        private const string commandName = "/porch";

        private DalamudPluginInterface pi;
        private NativeUIUtil nui;
        private Configuration configuration;
        private SongList songList;
        private BGMControl bgmControl;
        private string localDir;

        private TextPayload nowPlayingPayload = new("Now playing ");
        private TextPayload periodPayload = new(".");

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;

            configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pluginInterface);
            enableFallbackPlayer = configuration.UseOldPlayback;

            localDir = Path.GetDirectoryName(AssemblyLocation);

            var songlistPath = Path.Combine(localDir, songListFile);
            songList = new SongList(songlistPath, configuration, this, this);

            // TODO: eventually it might be nice to do this only if the fallback player isn't being used
            // and to add/remove it on-demand if that changes
            var addressResolver = new AddressResolver();
            try
            {
                addressResolver.Setup(pluginInterface.TargetModuleScanner);
                bgmControl = new BGMControl(addressResolver);
                bgmControl.OnSongChanged += HandleSongChanged;
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to find BGM playback objects");
                bgmControl = null;
                enableFallbackPlayer = true;
            }

            nui = new NativeUIUtil(pi);

            pluginInterface.CommandManager.AddHandler(commandName, new CommandInfo(OnDisplayCommand)
            {
                HelpMessage = "Displays the orchestrion player, to view, change, or stop in-game BGM."
            });
            pluginInterface.UiBuilder.OnBuildUi += Display;
            pluginInterface.UiBuilder.OnOpenConfigUi += (_, _) => songList.SettingsVisible = true;
            pluginInterface.Framework.OnUpdateEvent += OrchestrionUpdate;
        }

        private void OrchestrionUpdate(Framework unused)
        {
            bgmControl.Update();
            nui.Update();
        }

        public void Dispose()
        {
            songList.Dispose();
            nui.Dispose();

            pi.UiBuilder.OnBuildUi -= Display;
            pi.CommandManager.RemoveHandler(commandName);

            pi.Dispose();
        }

        private void OnDisplayCommand(string command, string args)
        {
            if (!string.IsNullOrEmpty(args) && args.Split(' ')[0].ToLowerInvariant() == "debug")
            {
                songList.AllowDebug = !songList.AllowDebug;
                pi.Framework.Gui.Chat.Print($"Orchestrion debug options have been {(songList.AllowDebug ? "enabled" : "disabled")}.");
            }
            else
            {
                // might be better to fully add/remove the OnBuildUi handler
                songList.Visible = true;
            }
        }

        private void Display()
        {
            songList.Draw();
        }

        private void HandleSongChanged(ushort songId)
        {
            var songName = songList.GetSongTitle(songId);
            if (configuration.ShowSongInChat && !EnableFallbackPlayer) // hack to not show 'new' updates when using the old player... temporary hopefully
            {
                if (!string.IsNullOrEmpty(songName))
                {
                    var payloads = new List<Payload>();

                    payloads.Add(nowPlayingPayload);
                    payloads.Add(EmphasisItalicPayload.ItalicsOn);
                    payloads.Add(new TextPayload(songName));
                    payloads.Add(EmphasisItalicPayload.ItalicsOff);
                    payloads.Add(periodPayload);

                    pi.Framework.Gui.Chat.PrintChat(new XivChatEntry
                    {
                        Message = new SeString(payloads),
                        Type = XivChatType.Echo
                    });
                }
            }
            
            // nui.Update($"♪ {songName}");
        }

        #region IPlaybackController

        private bool enableFallbackPlayer;
        public bool EnableFallbackPlayer
        {
            get { return enableFallbackPlayer; }
            set
            {
                // we should probably kill bgmControl's update loop when we disable it
                // but this is hopefully completely temporary anyway

                // if we force disabled due to a failed load, don't allow changing
                if (bgmControl != null)
                {
                    enableFallbackPlayer = value;
                    configuration.UseOldPlayback = value;
                    configuration.Save();
                }
            }
        }

        public int CurrentSong => EnableFallbackPlayer ? 0 : bgmControl.CurrentSongId;

        public void PlaySong(int songId)
        {
            if (EnableFallbackPlayer)
            {
                pi.CommandManager.Commands["/xlbgmset"].Handler("/xlbgmset", songId.ToString());
            }
            else
            {
                bgmControl.SetSong((ushort)songId, configuration.TargetPriority);
            }
        }

        public void StopSong()
        {
            if (EnableFallbackPlayer)
            {
                // still no real way to do this
                pi.CommandManager.Commands["/xlbgmset"].Handler("/xlbgmset", "9999");
            }
            else
            {
                bgmControl.SetSong(0, configuration.TargetPriority);
            }
        }

        public void DumpDebugInformation()
        {
            bgmControl?.DumpPriorityInfo();
        }

        #endregion

        #region IResourceLoader

        public ImGuiScene.TextureWrap LoadUIImage(string imageFile)
        {
            var path = Path.Combine(localDir, imageFile);
            return pi.UiBuilder.LoadImage(path);
        }

        #endregion
    }
}
