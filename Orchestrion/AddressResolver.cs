using Dalamud.Game;
using Dalamud.Game.Internal;
using System;
using System.Runtime.InteropServices;

namespace Orchestrion
{
    class AddressResolver : BaseAddressResolver
    {
        public IntPtr BaseAddress { get; private set; }
        public IntPtr BGMControl { get; private set; }

        protected override void Setup64Bit(SigScanner sig)
        {
            // TODO: this is probably on framework or gui somewhere, which might be cleaner if that is exposed
            BaseAddress = sig.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 85 C0 74 37 83 78 08 04", 2);

            UpdateBGMControl();
        }

        public void UpdateBGMControl()
        {
            var baseObject = Marshal.ReadIntPtr(BaseAddress);
            // I've never seen this happen, but the game checks for it in a number of places
            if (baseObject != IntPtr.Zero)
            {
                BGMControl = Marshal.ReadIntPtr(baseObject + 0xC0);
            }
            else
            {
                BGMControl = IntPtr.Zero;
            }
        }
    }
}
