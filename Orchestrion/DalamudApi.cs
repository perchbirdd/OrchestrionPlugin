using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Orchestrion;

public class DalamudApi
{
    public static void Initialize(DalamudPluginInterface pluginInterface) => pluginInterface.Create<DalamudApi>();

    [PluginService][RequiredVersion("1.0")] public static IAetheryteList AetheryteList { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IBuddyList BuddyList { get; private set; } = null;    
    [PluginService][RequiredVersion("1.0")] public static IChatGui ChatGui { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IClientState ClientState { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static ICommandManager CommandManager { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static ICondition Condition { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static DalamudPluginInterface PluginInterface { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IDataManager DataManager { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IDtrBar DtrBar { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IFateTable FateTable { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IFlyTextGui FlyTextGui { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IFramework Framework { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IGameGui GameGui { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IGameNetwork GameNetwork { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IGamepadState GamePadState { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IJobGauges JobGauges { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IKeyState KeyState { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static ILibcFunction LibcFunction { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IObjectTable ObjectTable { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IPartyFinderGui PartyFinderGui { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IPartyList PartyList { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static ISigScanner SigScanner { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static ITargetManager TargetManager { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IToastGui ToastGui { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IGameInteropProvider Hooker { get; private set; } = null;
    [PluginService][RequiredVersion("1.0")] public static IPluginLog PluginLog { get; private set; } = null;
}