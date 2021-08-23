using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Orchestrion
{
    [Serializable]
    internal class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public bool ShowSongInTitleBar { get; set; } = true;
        public bool ShowSongInChat { get; set; } = true;

        [JsonProperty]
        private bool showSongInNative = true;

        [JsonIgnore]
        public bool ShowSongInNative
        {
            get => showSongInNative;
            set
            {
                plugin.SetNativeDisplay(value);
                showSongInNative = value;
            }
        }

        public bool UseOldPlayback { get; set; } = false;
        public int TargetPriority { get; set; } = 0;

        public HashSet<int> FavoriteSongs { get; internal set; } = new HashSet<int>();

        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        [NonSerialized] private OrchestrionPlugin plugin;

        public void Initialize(DalamudPluginInterface pluginInterface, OrchestrionPlugin plugin)
        {
            this.pluginInterface = pluginInterface;
            this.plugin = plugin;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
