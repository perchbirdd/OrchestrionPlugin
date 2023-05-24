namespace Orchestrion.BGMSystem;

[Flags]
public enum SceneFlags : byte
{
	None = 0,
	Unknown = 1,
	Resume = 2,
	EnablePassEnd = 4,
	ForceAutoReset = 8,
	EnableDisableRestart = 16,
	IgnoreBattle = 32,
}