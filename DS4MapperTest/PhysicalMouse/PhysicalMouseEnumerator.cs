using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using HidLibrary;

namespace DS4MapperTest.PhysicalMouse
{
    /// <summary>
    /// Enumerates mouse devices visible to Raw Input. Deliberately separate
    /// from <see cref="HidLibrary.HidDevices"/>: Raw Input exposes every
    /// mouse (including ones with no readable HID report descriptor), and a
    /// hDevice handle from this listing is only valid until the next
    /// reconnect/reboot, so callers must key off <see cref="PhysicalMouseDevice.StableId"/>,
    /// never the handle.
    /// </summary>
    public static class PhysicalMouseEnumerator
    {
        // DEVPKEY_Device_FriendlyName {a45c254e-df1c-4efd-8020-67d146a850e0}, 14.
        // Not present in HidLibrary.NativeMethods (which only defines the
        // sibling DeviceDesc/HardwareIds/Manufacturer/UINumber keys from the
        // same property group), so declared locally for this subsystem.
        private static NativeMethods.DEVPROPKEY DEVPKEY_Device_FriendlyName =
            new NativeMethods.DEVPROPKEY
            {
                fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
                pid = 14
            };

        private static readonly Regex VidPidRegex = new Regex(
            @"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);

        public static List<PhysicalMouseDevice> EnumerateMice()
        {
            List<PhysicalMouseDevice> results = new List<PhysicalMouseDevice>();

            foreach (RawInputNativeMethods.RAWINPUTDEVICELIST entry in EnumerateRawInputDeviceList())
            {
                if (entry.dwType != RawInputNativeMethods.RIM_TYPEMOUSE)
                {
                    continue;
                }

                string devicePath = GetDeviceName(entry.hDevice);
                if (string.IsNullOrEmpty(devicePath))
                {
                    continue;
                }

                devicePath = NormalizeDevicePath(devicePath);

                string friendlyName = GetFriendlyName(devicePath) ?? devicePath;
                bool hasVidPid = TryParseVidPid(devicePath, out ushort vendorId, out ushort productId);
                bool isVirtual = SafeCheckIfVirtualDevice(devicePath);

                results.Add(new PhysicalMouseDevice(devicePath, friendlyName, vendorId, productId, hasVidPid, isVirtual));
            }

            return results;
        }

        internal static IEnumerable<RawInputNativeMethods.RAWINPUTDEVICELIST> EnumerateRawInputDeviceList()
        {
            uint deviceCount = 0;
            int listSize = Marshal.SizeOf<RawInputNativeMethods.RAWINPUTDEVICELIST>();

            uint status = RawInputNativeMethods.GetRawInputDeviceList(null, ref deviceCount, (uint)listSize);
            if (status != 0 || deviceCount == 0)
            {
                yield break;
            }

            RawInputNativeMethods.RAWINPUTDEVICELIST[] deviceList =
                new RawInputNativeMethods.RAWINPUTDEVICELIST[deviceCount];

            uint fetched = RawInputNativeMethods.GetRawInputDeviceList(deviceList, ref deviceCount, (uint)listSize);
            if (fetched == unchecked((uint)-1))
            {
                yield break;
            }

            for (int i = 0; i < fetched; i++)
            {
                yield return deviceList[i];
            }
        }

        internal static string GetDeviceName(IntPtr hDevice)
        {
            uint size = 0;
            uint result = RawInputNativeMethods.GetRawInputDeviceInfo(hDevice,
                RawInputNativeMethods.RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (result != 0 || size == 0)
            {
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size * sizeof(char));
            try
            {
                uint written = RawInputNativeMethods.GetRawInputDeviceInfo(hDevice,
                    RawInputNativeMethods.RIDI_DEVICENAME, buffer, ref size);
                if (written == unchecked((uint)-1))
                {
                    return null;
                }

                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // GetRawInputDeviceInfo has been observed to return the NT-namespace
        // form (\??\...) rather than the Win32 device-interface form
        // (\\?\...) that SetupAPI/CM_* functions expect.
        // Public: pure string normalisation, no native calls - also used
        // directly by RawMouseCaptureDevice's device-resolution logic and
        // covered by DS4MapperUnitTests.
        public static string NormalizeDevicePath(string devicePath)
        {
            if (devicePath.StartsWith(@"\??\", StringComparison.Ordinal))
            {
                return @"\\?\" + devicePath.Substring(4);
            }

            return devicePath;
        }

        private static bool TryParseVidPid(string devicePath, out ushort vendorId, out ushort productId)
        {
            vendorId = 0;
            productId = 0;

            Match match = VidPidRegex.Match(devicePath);
            if (!match.Success)
            {
                return false;
            }

            return ushort.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out vendorId)
                && ushort.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, null, out productId);
        }

        private static bool SafeCheckIfVirtualDevice(string devicePath)
        {
            try
            {
                return Util.CheckIfVirtualDevice(devicePath);
            }
            catch
            {
                // Best-effort heuristic; treat lookup failure as "unknown",
                // not "virtual", so a real mouse is never hidden by a
                // SetupAPI error.
                return false;
            }
        }

        private static string GetFriendlyName(string devicePath)
        {
            string instanceId = Util.GetInstanceIdFromDevicePath(devicePath);
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            return GetStringDeviceProperty(instanceId, DEVPKEY_Device_FriendlyName)
                ?? GetStringDeviceProperty(instanceId, NativeMethods.DEVPKEY_Device_BusReportedDeviceDesc)
                ?? GetStringDeviceProperty(instanceId, NativeMethods.DEVPKEY_Device_DeviceDesc);
        }

        private static string GetStringDeviceProperty(string deviceInstanceId, NativeMethods.DEVPROPKEY prop)
        {
            string result = null;
            NativeMethods.SP_DEVINFO_DATA deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
            deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);
            ulong propertyType = 0;
            int requiredSize = 0;

            IntPtr deviceInfoSet = NativeMethods.SetupDiCreateDeviceInfoList(IntPtr.Zero, 0);
            try
            {
                if (!NativeMethods.SetupDiOpenDeviceInfo(deviceInfoSet, deviceInstanceId, IntPtr.Zero, 0, ref deviceInfoData))
                {
                    return null;
                }

                NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref prop, ref propertyType,
                    null, 0, ref requiredSize, 0);

                if (requiredSize > 0)
                {
                    byte[] dataBuffer = new byte[requiredSize];
                    if (NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfoData, ref prop, ref propertyType,
                        dataBuffer, dataBuffer.Length, ref requiredSize, 0))
                    {
                        result = dataBuffer.ToUTF16String();
                    }
                }
            }
            finally
            {
                if (deviceInfoSet.ToInt64() != NativeMethods.INVALID_HANDLE_VALUE)
                {
                    NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }

            return string.IsNullOrEmpty(result) ? null : result;
        }
    }
}
