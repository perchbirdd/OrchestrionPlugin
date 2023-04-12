namespace Orchestrion.Game.BGMSystem;

[StructLayout(LayoutKind.Sequential)]
public struct DisableRestart
{
	public ushort DisableRestartId;
	public bool IsTimedOut; // ?
	public byte Padding1;
	public float ResetWaitTime;
	public float ElapsedTime;
	public bool TimerEnabled;
	// 3 byte padding
}