using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Logging;

namespace Orchestrion
{
    // TODO:
    // try to find what writes to bgm 0, block it if we are playing?
    //   or save/restore if we preempt it?
    // debug info of which priority is active
    //  notifications/logs of changes even to lower priorities?

    public class OrchestrionPlugin : IDalamudPlugin, IPlaybackController, IResourceLoader
    {
        public string Name => "Orchestrion";
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;

        private const string SongListFile = "xiv_bgm.csv";
        private const string CommandName = "/porch";
        private const string NativeNowPlayingPrefix = "♪ ";

        private readonly DalamudPluginInterface pi;
        private readonly CommandManager commandManager;
        private readonly ChatGui chatGui;
        private readonly Framework framework;
        private readonly NativeUIUtil nui;
        private readonly Configuration configuration;
        private readonly SongList songList;
        private readonly BGMControl bgmControl;
        private readonly string localDir;

        private readonly TextPayload nowPlayingPayload = new("Now playing ");
        private readonly TextPayload periodPayload = new(".");

        public OrchestrionPlugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] ChatGui chatGui,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] SigScanner sigScanner
            )
        {
            pi = pluginInterface;
            this.commandManager = commandManager;
            this.chatGui = chatGui;
            this.framework = framework;

            configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pluginInterface, this);
            enableFallbackPlayer = configuration.UseOldPlayback;

            localDir = Path.GetDirectoryName(AssemblyLocation);

            var songlistPath = Path.Combine(localDir, SongListFile);
            songList = new SongList(songlistPath, configuration, this, this);

            // TODO: eventually it might be nice to do this only if the fallback player isn't being used
            // and to add/remove it on-demand if that changes
            var addressResolver = new AddressResolver();
            try
            {
                addressResolver.Setup(sigScanner);
                bgmControl = new BGMControl(addressResolver);
                bgmControl.OnSongChanged += HandleSongChanged;
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "Failed to find BGM playback objects");
                bgmControl = null;
                enableFallbackPlayer = true;
            }

            nui = new NativeUIUtil(configuration, gameGui);

            commandManager.AddHandler(CommandName, new CommandInfo(OnDisplayCommand)
            {
                HelpMessage = "Displays the Orchestrion window, to view, change, or stop in-game BGM."
            });
            pluginInterface.UiBuilder.Draw += Display;
            pluginInterface.UiBuilder.OpenConfigUi += (_, _) => songList.SettingsVisible = true;
            framework.Update += OrchestrionUpdate;
        }

        private void OrchestrionUpdate(Framework unused)
        {
            bgmControl.Update();
            
            if (configuration.ShowSongInNative)
                nui.Update();
        }

        public void Dispose()
        {
            framework.Update -= OrchestrionUpdate;
            songList.Dispose();
            nui.Dispose();
            pi.UiBuilder.Draw -= Display;
            commandManager.RemoveHandler(CommandName);
            pi.Dispose();
        }

        public void SetNativeDisplay(bool value)
        {
            // Somehow it was set to the same value. This should not occur
            if (value == configuration.ShowSongInNative) return;

            if (value)
            {
                nui.Init();
                var songName = songList.GetSongTitle(CurrentSong);
                nui.Update(NativeNowPlayingPrefix + songName);
            }
                
            else
                nui.Dispose();
        }

        private void OnDisplayCommand(string command, string args)
        {
            if (!string.IsNullOrEmpty(args) && args.Split(' ')[0].ToLowerInvariant() == "debug")
            {
                songList.AllowDebug = !songList.AllowDebug;
                chatGui.Print($"Orchestrion debug options have been {(songList.AllowDebug ? "enabled" : "disabled")}.");
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
            PluginLog.Debug($"song id changed {songId}");
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

                    chatGui.PrintChat(new XivChatEntry
                    {
                        Message = new SeString(payloads),
                        Type = XivChatType.Echo
                    });
                }
            }
            if (configuration.ShowSongInNative)
                nui.Update(NativeNowPlayingPrefix + songName);
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

        public ushort CurrentSong => EnableFallbackPlayer ? (ushort) 0 : bgmControl.CurrentSongId;

        public void PlaySong(int songId)
        {
            if (EnableFallbackPlayer)
            {
                commandManager.Commands["/xlbgmset"].Handler("/xlbgmset", songId.ToString());
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
                commandManager.Commands["/xlbgmset"].Handler("/xlbgmset", "9999");
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
