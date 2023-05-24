namespace Orchestrion.Game.BGMSystem;

[StructLayout(LayoutKind.Explicit)]
struct BGMPlayer
{
	[FieldOffset(0x00)] public float MaxStandbyTime;
	[FieldOffset(0x04)] public uint State;
	[FieldOffset(0x08)] public ushort BgmId;
	[FieldOffset(0x10)] public uint BgmScene;
	[FieldOffset(0x20)] public uint SpecialMode;
	[FieldOffset(0x25)] public bool IsStandby;
	[FieldOffset(0x28)] public uint FadeOutTime;
	[FieldOffset(0x2C)] public uint ResumeFadeInTime;
	[FieldOffset(0x30)] public uint FadeInStartTime;
	[FieldOffset(0x34)] public uint FadeInTime;
	[FieldOffset(0x38)] public uint ElapsedTime;
	[FieldOffset(0x40)] public float StandbyTime;
	[FieldOffset(0x4D)] public byte SpecialModeType;
}