using System;
using System.Runtime.InteropServices;
using System.Security;

namespace DS4MapperTest.PhysicalMouse
{
    /// <summary>
    /// Raw Input (WM_INPUT) P/Invoke surface. Scoped to the mouse-only subset
    /// this subsystem needs; keyboard/HID raw input variants are intentionally
    /// not modelled.
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal static class RawInputNativeMethods
    {
        internal const int WM_INPUT = 0x00FF;
        internal const int WM_INPUT_DEVICE_CHANGE = 0x00FE;

        internal const int GIDC_ARRIVAL = 1;
        internal const int GIDC_REMOVAL = 2;

        internal const uint RIM_TYPEMOUSE = 0;

        internal const uint RIDEV_REMOVE = 0x00000001;
        internal const uint RIDEV_INPUTSINK = 0x00000100;
        internal const uint RIDEV_DEVNOTIFY = 0x00002000;

        internal const uint RIDI_PREPARSEDDATA = 0x20000005;
        internal const uint RIDI_DEVICENAME = 0x20000007;
        internal const uint RIDI_DEVICEINFO = 0x2000000B;

        internal const uint RID_HEADER = 0x10000005;
        internal const uint RID_INPUT = 0x10000003;

        // usFlags on RAWMOUSE
        internal const ushort MOUSE_MOVE_RELATIVE = 0x00;
        internal const ushort MOUSE_MOVE_ABSOLUTE = 0x01;

        // usButtonFlags on RAWMOUSE
        internal const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        internal const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        internal const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        internal const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        internal const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        internal const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
        internal const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
        internal const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
        internal const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
        internal const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;
        internal const ushort RI_MOUSE_WHEEL = 0x0400;
        internal const ushort RI_MOUSE_HWHEEL = 0x0800;

        internal static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTDEVICE
        {
            internal ushort usUsagePage;
            internal ushort usUsage;
            internal uint dwFlags;
            internal IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTDEVICELIST
        {
            internal IntPtr hDevice;
            internal uint dwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTHEADER
        {
            internal uint dwType;
            internal uint dwSize;
            internal IntPtr hDevice;
            internal IntPtr wParam;
        }

        // Mirrors the native union{ ULONG ulButtons; struct{ usButtonFlags; usButtonData; } }
        // as flattened sequential fields; .NET's default alignment for this
        // field order reproduces the native struct's padding/offsets.
        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWMOUSE
        {
            internal ushort usFlags;
            internal ushort usButtonFlags;
            internal ushort usButtonData;
            internal uint ulRawButtons;
            internal int lLastX;
            internal int lLastY;
            internal uint ulExtraInformation;
        }

        // Only the mouse variant of the RAWINPUT union is modelled; this must
        // only be interpreted after confirming header.dwType == RIM_TYPEMOUSE.
        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUT
        {
            internal RAWINPUTHEADER header;
            internal RAWMOUSE mouse;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputDeviceList(
            [In, Out] RAWINPUTDEVICELIST[] pRawInputDeviceList,
            ref uint puiNumDevices,
            uint cbSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterRawInputDevices(
            [In] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);
    }
}
