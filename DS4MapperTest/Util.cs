using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using HidLibrary;

namespace DS4MapperTest
{
    // ==========================================================
    // 1. libVIIPER NATIVE INTEROP DEFINITIONS
    // ==========================================================
    enum VIIPERLogLevel { Debug = -4, Info = 0, Warn = 4, Error = 8 }

    [StructLayout(LayoutKind.Sequential)]
    struct USBServerConfig
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? addr;
        public ulong connection_timeout_ms;
        public ulong device_handler_connect_timeout_ms;
        public uint write_batch_flush_interval_ms;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Xbox360DeviceState
    {
        public uint Buttons;
        public byte LT;
        public byte RT;
        public short LX;
        public short LY;
        public short RX;
        public short RY;
        public byte Reserved0, Reserved1, Reserved2, Reserved3, Reserved4, Reserved5;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DS4DeviceState
    {
        public sbyte Sticklx;
        public sbyte Stickly;
        public sbyte Stickrx;
        public sbyte Stickry;
        public ushort Buttons;
        public byte Dpad;
        public byte Triggerl2;
        public byte Triggerr2;
        public ushort Touch1x;
        public ushort Touch1y;
        public byte Touch1active;
        public ushort Touch2x;
        public ushort Touch2y;
        public byte Touch2active;
        public short Gyrox;
        public short Gyroy;
        public short Gyroz;
        public short Accelx;
        public short Accely;
        public short Accelz;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DS4MetaState
    {
        public IntPtr SerialNumber;
        public IntPtr Board;
        public byte BatteryStatus;
        public double TemperatureCelsius;
        public double BatteryVoltage;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DSDeviceState
    {
        public sbyte LX;
        public sbyte LY;
        public sbyte RX;
        public sbyte RY;
        public uint Buttons;
        public byte DPad;
        public byte L2;
        public byte R2;
        public ushort Touch1X;
        public ushort Touch1Y;
        public byte Touch1Active;
        public ushort Touch2X;
        public ushort Touch2Y;
        public byte Touch2Active;
        public short GyroX;
        public short GyroY;
        public short GyroZ;
        public short AccelX;
        public short AccelY;
        public short AccelZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NS2ProDeviceState
    {
        public uint Buttons;
        public ushort LX;
        public ushort LY;
        public ushort RX;
        public ushort RY;
        public short AccelX;
        public short AccelY;
        public short AccelZ;
        public short GyroX;
        public short GyroY;
        public short GyroZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NS2ProMetaState
    {
        public IntPtr SerialNumber;
        public byte BatteryLevel;
        public byte Charging;
        public byte ExternalPower;
        public ushort BatteryVolts;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NS2ProOutputState
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LeftRumble;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RightRumble;
        public byte Flags;
        public byte PlayerLedMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DSMetaState
    {
        public IntPtr SerialNumber;
        public IntPtr MACAddress;
        public IntPtr Board;
        public byte BatteryStatus;
        public double TemperatureCelsius;
        public double BatteryVoltage;
        public IntPtr ShellColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MouseDeviceState
    {
        public byte Buttons;
        public short DX;
        public short DY;
        public short Wheel;
        public short Pan;
    }

    [Flags]
    enum VIIPERDPadDir : ushort
    {
        PadUp = 0x01,
        PadDown = 0x02,
        PadLeft = 0x04,
        PadRight = 0x08,
    }

    public static class DS4Button
    {
        public static ushort Ps = 1;
        public static ushort Touchpad = 2;

        public static ushort Square = 16;
        public static ushort Cross = 32;
        public static ushort Circle = 64;
        public static ushort Triangle = 128;
        public static ushort ShoulderLeft = 256;
        public static ushort ShoulderRight = 512;
        public static ushort TriggerLeft = 1024;
        public static ushort TriggerRight = 2048;
        public static ushort Share = 4096;
        public static ushort Options = 8192;
        public static ushort ThumbLeft = 16384;
        public static ushort ThumbRight = 32768;
    }

    public static class Xbox360Button
    {
        public static ushort Up = 1;
        public static ushort Down = 2;
        public static ushort Left = 4;
        public static ushort Right = 8;
        public static ushort Start = 16;
        public static ushort Back = 32;
        public static ushort LeftThumb = 64;
        public static ushort RightThumb = 128;
        public static ushort LeftShoulder = 256;
        public static ushort RightShoulder = 512;
        public static ushort Guide = 1024;
        public static ushort A = 4096;
        public static ushort B = 8192;
        public static ushort X = 16384;
        public static ushort Y = 32768;
    }

    public static class DualSenseButton
    {
        public static uint Square = 0x00000010;
        public static uint Cross = 0x00000020;
        public static uint Circle = 0x00000040;
        public static uint Triangle = 0x00000080;
        public static uint ShoulderLeft = 0x00000100;
        public static uint ShoulderRight = 0x00000200;
        public static uint TriggerLeft = 0x00000400;
        public static uint TriggerRight = 0x00000800;
        public static uint Create = 0x00001000;
        public static uint Options = 0x00002000;
        public static uint ThumbLeft = 0x00004000;
        public static uint ThumbRight = 0x00008000;
        public static uint Ps = 0x00010000;
        public static uint Touchpad = 0x00020000;
        public static uint Mute = 0x00040000;
        public static uint LFn = 0x00100000;
        public static uint RFn = 0x00200000;
        public static uint L4 = 0x00400000;
        public static uint R4 = 0x00800000;
    }

    public static class NS2ProButton
    {
        public const uint B = 0x00000001;
        public const uint A = 0x00000002;
        public const uint Y = 0x00000004;
        public const uint X = 0x00000008;
        public const uint R = 0x00000010;
        public const uint ZR = 0x00000020;
        public const uint Plus = 0x00000040;
        public const uint RightStick = 0x00000080;
        public const uint Down = 0x00000100;
        public const uint Right = 0x00000200;
        public const uint Left = 0x00000400;
        public const uint Up = 0x00000800;
        public const uint L = 0x00001000;
        public const uint ZL = 0x00002000;
        public const uint Minus = 0x00004000;
        public const uint LeftStick = 0x00008000;
        public const uint Home = 0x00010000;
        public const uint Capture = 0x00020000;
        public const uint GR = 0x00040000;
        public const uint GL = 0x00080000;
        public const uint C = 0x00100000;
        public const uint Headset = 0x00200000;
    }

    public static class VIIPERMouseButton
    {
        public const byte Left = 0x01;
        public const byte Right = 0x02;
        public const byte Middle = 0x04;
        public const byte Button4 = 0x08;
        public const byte Button5 = 0x10;
    }

    [SuppressUnmanagedCodeSecurity]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Xbox360RumbleCallbackDelegate(nuint handle, byte leftMotor, byte rightMotor);

    [SuppressUnmanagedCodeSecurity]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DSOutputCallbackDelegate(nuint handle, byte rumbleSmall, byte rumbleLarge,
        byte ledRed, byte ledGreen, byte ledBlue, byte playerLeds);

    [SuppressUnmanagedCodeSecurity]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NS2ProOutputCallbackDelegate(nuint handle, NS2ProOutputState output);

    [SuppressUnmanagedCodeSecurity]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void VIIPERLogCallbackDelegate(VIIPERLogLevel level, [MarshalAs(UnmanagedType.LPStr)] string message);

    [SuppressUnmanagedCodeSecurity]
    static class LibVIIPER
    {
        const string Lib = "libVIIPER";

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool NewUSBServer([In] ref USBServerConfig config, out nuint outHandle, VIIPERLogCallbackDelegate? logCallback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CloseUSBServer(nuint handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateUSBBus(nuint handle, ref uint busID);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateXbox360Device(nuint serverHandle, out nuint outDeviceHandle, uint busID, [MarshalAs(UnmanagedType.I1)] bool autoAttachLocalhost, ushort idVendor, ushort idProduct, byte xinputSubType);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RemoveXbox360Device(nuint outDeviceHandle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetXbox360DeviceState(nuint deviceHandle, Xbox360DeviceState state);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetXbox360RumbleCallback(nuint deviceHandle, Xbox360RumbleCallbackDelegate? callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateDS4Device(nuint serverHandle, out nuint outDeviceHandle, uint busID, [MarshalAs(UnmanagedType.I1)] bool autoAttachLocalhost, ushort idVendor, ushort idProduct, IntPtr meta);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RemoveDS4Device(nuint outDeviceHandle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetDS4DeviceState(nuint deviceHandle, DS4DeviceState state);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateDualSenseDevice(nuint serverHandle, out nuint outDeviceHandle, uint busID, [MarshalAs(UnmanagedType.I1)] bool autoAttachLocalhost, ushort idVendor, ushort idProduct, IntPtr meta);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateDualSenseEdgeDevice(nuint serverHandle, out nuint outDeviceHandle, uint busID, [MarshalAs(UnmanagedType.I1)] bool autoAttachLocalhost, ushort idVendor, ushort idProduct, IntPtr meta);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RemoveDualSenseDevice(nuint outDeviceHandle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetDualSenseDeviceState(nuint deviceHandle, DSDeviceState state);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetDualSenseOutputCallback(nuint deviceHandle, DSOutputCallbackDelegate? callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateNS2ProDevice(nuint serverHandle, out nuint outDeviceHandle, uint busID, [MarshalAs(UnmanagedType.I1)] bool autoAttachLocalhost, ushort idVendor, ushort idProduct, IntPtr meta);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RemoveNS2ProDevice(nuint outDeviceHandle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetNS2ProDeviceState(nuint deviceHandle, NS2ProDeviceState state);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetNS2ProOutputCallback(nuint deviceHandle, NS2ProOutputCallbackDelegate? callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateMouseDevice(nuint serverHandle, out nuint outDeviceHandle, uint busID, [MarshalAs(UnmanagedType.I1)] bool autoAttachLocalhost, ushort idVendor, ushort idProduct);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SetMouseDeviceState(nuint deviceHandle, MouseDeviceState state);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RemoveMouseDevice(nuint outDeviceHandle);
    }

    [SuppressUnmanagedCodeSecurity]
    public class Util
    {
        public static Guid sysGuid = Guid.Parse("{4d36e97d-e325-11ce-bfc1-08002be10318}");
        public static Guid fakerInputGuid = Guid.Parse("{ab67b0fa-d0f5-4f60-81f4-346e18fd0805}");

        public enum PROCESS_INFORMATION_CLASS : int
        {
            ProcessBasicInformation = 0,
            ProcessQuotaLimits,
            ProcessIoCounters,
            ProcessVmCounters,
            ProcessTimes,
            ProcessBasePriority,
            ProcessRaisePriority,
            ProcessDebugPort,
            ProcessExceptionPort,
            ProcessAccessToken,
            ProcessLdtInformation,
            ProcessLdtSize,
            ProcessDefaultHardErrorMode,
            ProcessIoPortHandlers,
            ProcessPooledUsageAndLimits,
            ProcessWorkingSetWatch,
            ProcessUserModeIOPL,
            ProcessEnableAlignmentFaultFixup,
            ProcessPriorityClass,
            ProcessWx86Information,
            ProcessHandleCount,
            ProcessAffinityMask,
            ProcessPriorityBoost,
            ProcessDeviceMap,
            ProcessSessionInformation,
            ProcessForegroundInformation,
            ProcessWow64Information,
            ProcessImageFileName,
            ProcessLUIDDeviceMapsEnabled,
            ProcessBreakOnTermination,
            ProcessDebugObjectHandle,
            ProcessDebugFlags,
            ProcessHandleTracing,
            ProcessIoPriority,
            ProcessExecuteFlags,
            ProcessResourceManagement,
            ProcessCookie,
            ProcessImageInformation,
            ProcessCycleTime,
            ProcessPagePriority,
            ProcessInstrumentationCallback,
            ProcessThreadStackAllocation,
            ProcessWorkingSetWatchEx,
            ProcessImageFileNameWin32,
            ProcessImageFileMapping,
            ProcessAffinityUpdateMode,
            ProcessMemoryAllocationMode,
            MaxProcessInfoClass
        }

        [StructLayout(LayoutKind.Sequential)]
        public class DEV_BROADCAST_DEVICEINTERFACE
        {
            internal Int32 dbcc_size;
            internal Int32 dbcc_devicetype;
            internal Int32 dbcc_reserved;
            internal Guid dbcc_classguid;
            internal Int16 dbcc_name;
        }

        public const Int32 DBT_DEVTYP_DEVICEINTERFACE = 0x0005;

        public const Int32 DEVICE_NOTIFY_WINDOW_HANDLE = 0x0000;
        public const Int32 DEVICE_NOTIFY_SERVICE_HANDLE = 0x0001;
        public const Int32 DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 0x0004;

        public const Int32 WM_CREATE = 0x0001;
        public const Int32 WM_DEVICECHANGE = 0x0219;

        public const Int32 DIGCF_PRESENT = 0x0002;
        public const Int32 DIGCF_DEVICEINTERFACE = 0x0010;

        public const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;


        [Flags]
        public enum DisplayDeviceStateFlags : int
        {
            /// <summary>The device is part of the desktop.</summary>
            AttachedToDesktop = 0x1,
            MultiDriver = 0x2,
            /// <summary>The device is part of the desktop.</summary>
            PrimaryDevice = 0x4,
            /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
            MirroringDriver = 0x8,
            /// <summary>The device is VGA compatible.</summary>
            VGACompatible = 0x10,
            /// <summary>The device is removable; it cannot be the primary display.</summary>
            Removable = 0x20,
            /// <summary>The device has more display modes than its output devices support.</summary>
            ModesPruned = 0x8000000,
            Remote = 0x4000000,
            Disconnect = 0x2000000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAY_DEVICE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetInformationProcess(IntPtr processHandle,
           PROCESS_INFORMATION_CLASS processInformationClass, ref IntPtr processInformation, uint processInformationLength);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        protected static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, Int32 Flags);

        [DllImport("user32.dll", SetLastError = true)]
        protected static extern Boolean UnregisterDeviceNotification(IntPtr Handle);

        public static Boolean RegisterNotify(IntPtr Form, Guid Class, ref IntPtr Handle, Boolean Window = true)
        {
            IntPtr devBroadcastDeviceInterfaceBuffer = IntPtr.Zero;

            try
            {
                DEV_BROADCAST_DEVICEINTERFACE devBroadcastDeviceInterface = new DEV_BROADCAST_DEVICEINTERFACE();
                Int32 Size = Marshal.SizeOf(devBroadcastDeviceInterface);

                devBroadcastDeviceInterface.dbcc_size = Size;
                devBroadcastDeviceInterface.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                devBroadcastDeviceInterface.dbcc_reserved = 0;
                devBroadcastDeviceInterface.dbcc_classguid = Class;

                devBroadcastDeviceInterfaceBuffer = Marshal.AllocHGlobal(Size);
                Marshal.StructureToPtr(devBroadcastDeviceInterface, devBroadcastDeviceInterfaceBuffer, true);

                Handle = RegisterDeviceNotification(Form, devBroadcastDeviceInterfaceBuffer, Window ? DEVICE_NOTIFY_WINDOW_HANDLE : DEVICE_NOTIFY_SERVICE_HANDLE);

                Marshal.PtrToStructure(devBroadcastDeviceInterfaceBuffer, devBroadcastDeviceInterface);

                return Handle != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                throw;
            }
            finally
            {
                if (devBroadcastDeviceInterfaceBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(devBroadcastDeviceInterfaceBuffer);
                }
            }
        }

        public static Boolean UnregisterNotify(IntPtr Handle)
        {
            try
            {
                return UnregisterDeviceNotification(Handle);
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                throw;
            }
        }

        public static string GetOSProductName()
        {
            string productName =
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString();
            return productName;
        }

        public static string GetOSReleaseId()
        {
            string releaseId =
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString();
            return releaseId;
        }

        private static string GetStringDeviceProperty(string deviceInstanceId,
            NativeMethods.DEVPROPKEY prop)
        {
            string result = string.Empty;
            NativeMethods.SP_DEVINFO_DATA deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
            deviceInfoData.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(deviceInfoData);
            ulong propertyType = 0;
            var requiredSize = 0;

            Guid hidGuid = new Guid();
            NativeMethods.HidD_GetHidGuid(ref hidGuid);
            //IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(IntPtr.Zero, deviceInstanceId, 0, NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE | NativeMethods.DIGCF_ALLCLASSES);
            IntPtr deviceInfoSet = NativeMethods.SetupDiCreateDeviceInfoList(IntPtr.Zero, 0);
            //NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData);
            NativeMethods.SetupDiOpenDeviceInfo(deviceInfoSet, deviceInstanceId, IntPtr.Zero, 0, ref deviceInfoData);
            NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref prop, ref propertyType,
                    null, 0, ref requiredSize, 0);

            if (requiredSize > 0)
            {
                byte[] dataBuffer = new byte[requiredSize];
                NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref prop, ref propertyType,
                    dataBuffer, dataBuffer.Length, ref requiredSize, 0);

                result = dataBuffer.ToUTF16String();
            }

            if (deviceInfoSet.ToInt64() != NativeMethods.INVALID_HANDLE_VALUE)
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return result;
        }

        public static string GetInstanceIdFromDevicePath(string devicePath)
        {
            string result = string.Empty;
            uint requiredSize = 0;
            NativeMethods.CM_Get_Device_Interface_Property(devicePath, ref NativeMethods.DEVPKEY_Device_InstanceId, out _, null, ref requiredSize, 0);
            if (requiredSize > 0)
            {
                byte[] buffer = new byte[requiredSize];
                NativeMethods.CM_Get_Device_Interface_Property(devicePath, ref NativeMethods.DEVPKEY_Device_InstanceId, out _, buffer, ref requiredSize, 0);
                result = buffer.ToUTF16String();
            }

            return result;
        }

        private static string[] GetStringArrayDeviceProperty(string deviceInstanceId,
            NativeMethods.DEVPROPKEY prop)
        {
            string[] result = null;
            NativeMethods.SP_DEVINFO_DATA deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
            deviceInfoData.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(deviceInfoData);
            ulong propertyType = 0;
            var requiredSize = 0;

            IntPtr zero = IntPtr.Zero;
            //IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(zero, deviceInstanceId, 0, NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE | NativeMethods.DIGCF_ALLCLASSES);
            IntPtr deviceInfoSet = NativeMethods.SetupDiCreateDeviceInfoList(IntPtr.Zero, 0);
            //NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData);
            NativeMethods.SetupDiOpenDeviceInfo(deviceInfoSet, deviceInstanceId, IntPtr.Zero, 0, ref deviceInfoData);
            NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref prop, ref propertyType,
                    null, 0, ref requiredSize, 0);

            if (requiredSize > 0)
            {
                byte[] dataBuffer = new byte[requiredSize];
                NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref prop, ref propertyType,
                    dataBuffer, dataBuffer.Length, ref requiredSize, 0);

                string tempStr = Encoding.Unicode.GetString(dataBuffer);
                string[] hardwareIds = tempStr.TrimEnd(new char[] { '\0', '\0' }).Split('\0');
                result = hardwareIds;
            }

            if (deviceInfoSet.ToInt64() != NativeMethods.INVALID_HANDLE_VALUE)
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return result;
        }

        public static bool CheckIfVirtualDevice(string devicePath)
        {
            bool result = false;
            bool excludeMatchFound = false;

            var instanceId = GetInstanceIdFromDevicePath(devicePath);
            var testInstanceId = instanceId;
            while (!string.IsNullOrEmpty(testInstanceId))
            {
                var hardwareIds = GetStringArrayDeviceProperty(testInstanceId, NativeMethods.DEVPKEY_Device_HardwareIds);
                if (hardwareIds != null)
                {
                    // hardware IDs of root hubs/controllers that emit supported virtual devices as sources
                    var excludedIds = new[]
                    {
                        @"ROOT\HIDGAMEMAP", // reWASD
                        @"ROOT\VHUSB3HC", // VirtualHere
                    };

                    excludeMatchFound = hardwareIds.Any(id => excludedIds.Contains(id.ToUpper()));
                    if (excludeMatchFound)
                    {
                        break;
                    }
                }

                // Check for potential non-present device as well
                string parentInstanceId = GetStringDeviceProperty(testInstanceId, NativeMethods.DEVPKEY_Device_Parent);

                // Found root enumerator. Use instanceId of device one layer lower in final check
                if (parentInstanceId.Equals(@"HTREE\ROOT\0", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                testInstanceId = parentInstanceId;
            }

            if (!excludeMatchFound &&
                !string.IsNullOrEmpty(testInstanceId) &&
                (testInstanceId.StartsWith(@"ROOT\SYSTEM", StringComparison.OrdinalIgnoreCase)
                || testInstanceId.StartsWith(@"ROOT\USB", StringComparison.OrdinalIgnoreCase)))
            {
                result = true;
            }

            return result;
        }

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint period);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplayDevicesW(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint GW_OWNER = 4;
        private const uint WM_CLOSE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Closes any top-level window (e.g. an open SaveFileDialog/OpenFolderDialog)
        // owned by ownerHandle. Used to get rid of a native file/folder picker left
        // open when the controller it belongs to is unplugged mid-workflow, so a
        // disconnect can't leave a dangling modal dialog pointed at a stale device.
        public static void CloseOwnedDialogs(IntPtr ownerHandle)
        {
            if (ownerHandle == IntPtr.Zero) return;

            EnumWindows((hWnd, lParam) =>
            {
                if (GetWindow(hWnd, GW_OWNER) == ownerHandle)
                {
                    PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }

                return true;
            }, IntPtr.Zero);
        }
    }
}
