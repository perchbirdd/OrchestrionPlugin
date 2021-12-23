using Dalamud.Game;
using System;
using System.Runtime.InteropServices;

namespace Orchestrion
{
    static class BGMAddressResolver
    {
        private static IntPtr _baseAddress;

        static BGMAddressResolver() { }

        public static void Init(SigScanner sig)
        {
            _baseAddress = sig.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 85 C0 74 37 83 78 08 04", 2);
        }

        public static IntPtr BGMController
        {
            get
            {
                var baseObject = Marshal.ReadIntPtr(_baseAddress);

                // I've never seen this happen, but the game checks for it in a number of places
                return baseObject == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(baseObject + 0xC0);
            }
        }
    }
}