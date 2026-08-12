using System;
using System.Collections.Generic;

namespace DS4MapperTest.SdlDiagnostics
{
    internal readonly struct SdlGamepadHandle : IEquatable<SdlGamepadHandle>
    {
        public IntPtr NativeHandle { get; }
        public bool IsNull => NativeHandle == IntPtr.Zero;

        public SdlGamepadHandle(IntPtr nativeHandle)
        {
            NativeHandle = nativeHandle;
        }

        public bool Equals(SdlGamepadHandle other) => NativeHandle == other.NativeHandle;
        public override bool Equals(object obj) => obj is SdlGamepadHandle other && Equals(other);
        public override int GetHashCode() => NativeHandle.GetHashCode();
    }

    internal interface ISdlDiagnosticApi
    {
        SdlDiagnosticVersionInfo VersionInfo { get; }
        bool Initialise(out string error);
        void Shutdown();
        IReadOnlyList<uint> EnumerateGamepads(out string error);
        SdlRawGamepadInfo QueryGamepadInfo(uint instanceId, SdlGamepadHandle handle);
        SdlGamepadHandle OpenGamepad(uint instanceId, out string error);
        void CloseGamepad(SdlGamepadHandle handle);
        bool PollEvent(out SdlDiagnosticEvent diagnosticEvent);
        void RefreshGamepads();
        void RefreshSensors();
        void RefreshLiveState(SdlGamepadHandle handle, SdlRawGamepadInfo info);
    }
}
