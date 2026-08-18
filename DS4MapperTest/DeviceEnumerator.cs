using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HidLibrary;
using System.Runtime.InteropServices;
using static DS4MapperTest.VidPidMeta;
using DS4MapperTest.SteamControllerLibrary;

namespace DS4MapperTest
{
    internal class VidPidMeta
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct DelegateUnion
        {
            [FieldOffset(0)]
            public DeviceEnumerator.HidDeviceCheckHandler hidHandler;
            [FieldOffset(0)]
            public DeviceEnumerator.HidDeviceCheckHandler hidHandler2;
        }

        public enum UsedConnectionBus : ushort
        {
            HID,
            USB,
        }

        public int vid;
        public int pid;
        public string displayName;
        public InputDeviceType inputDevType;
        public UsedConnectionBus connectBus;
        public DelegateUnion testDelUnion;

        internal VidPidMeta(int vid, int pid, string displayName, InputDeviceType inputDevType,
            UsedConnectionBus connectBus)
        {
            this.vid = vid;
            this.pid = pid;
            this.displayName = displayName;
            this.inputDevType = inputDevType;
            this.connectBus = connectBus;
            this.testDelUnion = new DelegateUnion();
        }
    }

    // Modern controllers (DS4, DualSense, Switch Pro, Joy-Con, Steam
    // Controller 2026/Triton, 8BitDo Ultimate 2 Wireless) are owned entirely
    // by the SDL3 universal backend, which does its own device discovery
    // independent of this HID-based enumerator. The original 2015 Steam
    // Controller is the one family that still needs its own native HID
    // identification here, since it backs SteamControllerUniversalAdapter's
    // native reader rather than going through SDL3.
    public class DeviceEnumerator
    {
        private const int STEAM_CONTROLLER_VENDOR_ID = 0x28DE;
        private const int STEAM_CONTROLLER_PRODUCT_ID = 0x1102;
        private const int STEAM_DONGLE_CONTROLLER_PRODUCT_ID = 0x1142;
        private const int STEAM_BT_CONTROLLER_PRODUCT_ID = 0x1106;

        internal delegate bool HidDeviceCheckHandler(HidDevice device, VidPidMeta meta);

        private HashSet<string> foundDevicePaths;
        private ReaderWriterLockSlim _foundDevlocker = new ReaderWriterLockSlim();

        private Dictionary<string, InputDeviceBase> foundKnownDevices;
        private Dictionary<InputDeviceBase, string> revFoundKnownDevices;
        private Dictionary<string, InputDeviceBase> newKnownDevices;
        private Dictionary<string, InputDeviceBase> removedKnownDevices;
        private Dictionary<string, VidPidMeta> vidPidMetaDict;

        private VidPidMeta[] knownDevicesMeta = new VidPidMeta[]
        {
            new VidPidMeta(STEAM_CONTROLLER_VENDOR_ID, STEAM_CONTROLLER_PRODUCT_ID, "Steam Controller", InputDeviceType.SteamController,
                VidPidMeta.UsedConnectionBus.HID),
            new VidPidMeta(STEAM_CONTROLLER_VENDOR_ID, STEAM_DONGLE_CONTROLLER_PRODUCT_ID, "Steam Controller", InputDeviceType.SteamController,
                VidPidMeta.UsedConnectionBus.HID),
            new VidPidMeta(STEAM_CONTROLLER_VENDOR_ID, STEAM_BT_CONTROLLER_PRODUCT_ID, "Steam Controller", InputDeviceType.SteamController,
                VidPidMeta.UsedConnectionBus.HID),
        };

        public DeviceEnumerator()
        {
            foundDevicePaths = new HashSet<string>();
            foundKnownDevices = new Dictionary<string, InputDeviceBase>();
            revFoundKnownDevices = new Dictionary<InputDeviceBase, string>();
            newKnownDevices = new Dictionary<string, InputDeviceBase>();
            removedKnownDevices = new Dictionary<string, InputDeviceBase>();
            vidPidMetaDict = new Dictionary<string, VidPidMeta>();
            foreach (VidPidMeta meta in knownDevicesMeta)
            {
                if (meta.inputDevType == InputDeviceType.SteamController)
                {
                    meta.testDelUnion.hidHandler = SteamControllerDeviceCheckHandler;
                    vidPidMetaDict.Add($"VID_{meta.vid}&PID_{meta.pid}", meta);
                }
            }
        }

        private bool IsRealDev(HidDevice hDevice)
        {
            bool result = !Util.CheckIfVirtualDevice(hDevice.DevicePath);
            return result;
        }

        public void FindControllers()
        {
            using WriteLocker locker = new WriteLocker(_foundDevlocker);

            HashSet<string> previousDevicePaths = new HashSet<string>(foundDevicePaths);
            HashSet<string> currentDevicePaths = new HashSet<string>();
            newKnownDevices.Clear();
            removedKnownDevices.Clear();

            // Materialize once. HidDevice construction opens/closes a handle and reads
            // attributes per device, so re-enumerating the lazy sequence below would
            // redo that native work for every device a second time.
            List<HidDevice> hidDevs = HidDevices.Enumerate().ToList();
            foreach(HidDevice hidDev in hidDevs)
            {
                currentDevicePaths.Add(hidDev.DevicePath);
            }

            IEnumerable<string> removedHidDevices = previousDevicePaths.Except(currentDevicePaths);

            foreach (string devicePath in removedHidDevices)
            {
                if (foundKnownDevices.Remove(devicePath, out InputDeviceBase tempDevice))
                {
                    revFoundKnownDevices.Remove(tempDevice);
                    foundDevicePaths.Remove(devicePath);
                }
            }

            // Filter out devices already scanned in previous sessions. These devices were
            // just enumerated above, so presence in currentDevicePaths already proves they're
            // connected; checking hidDevice.IsConnected here would re-run a full system-wide
            // HID scan per device (O(n^2) over every HID device on the machine).
            IEnumerable<HidDevice> newHidDevs = hidDevs.Where((hidDevice) =>
            {
                return !foundDevicePaths.Contains(hidDevice.DevicePath);
            });

            foreach (HidDevice hidDev in newHidDevs)
            {
                // Check the cheap VID/PID dictionary lookup before the virtual-device check:
                // IsRealDev walks the device tree via several SetupDi calls per device, and on
                // a fresh scan newHidDevs is every HID device on the machine (keyboards, mice,
                // sensors, etc.), not just controllers. Only pay that cost for devices that
                // already look like a known controller.
                if (vidPidMetaDict.TryGetValue($"VID_{hidDev.Attributes.VendorId}&PID_{hidDev.Attributes.ProductId}",
                    out VidPidMeta value) && IsRealDev(hidDev))
                {
                    if (!hidDev.IsOpen)
                    {
                        hidDev.OpenDevice(false);
                    }

                    if (hidDev.IsOpen)
                    {
                        if (value.inputDevType == InputDeviceType.SteamController)
                        {
                            value.testDelUnion.hidHandler?.Invoke(hidDev, value);
                        }
                    }
                }

                foundDevicePaths.Add(hidDev.DevicePath);
            }
        }

        public IEnumerable<InputDeviceBase> GetKnownDevices()
        {
            using WriteLocker locker = new WriteLocker(_foundDevlocker);
            return foundKnownDevices.Values.ToList();
        }

        public IEnumerable<InputDeviceBase> GetNewKnownDevices()
        {
            using WriteLocker locker = new WriteLocker(_foundDevlocker);
            return newKnownDevices.Values.ToList();
        }

        public IEnumerable<InputDeviceBase> GetRemoveKnownDevices()
        {
            using WriteLocker locker = new WriteLocker(_foundDevlocker);
            return removedKnownDevices.Values.ToList();
        }

        public void ClearRemovedDevicesReferences()
        {
            using WriteLocker locker = new WriteLocker(_foundDevlocker);
            removedKnownDevices.Clear();
        }

        public void RemoveDevice(InputDeviceBase inputDevice)
        {
            using (WriteLocker locker = new WriteLocker(_foundDevlocker))
            {
                if (revFoundKnownDevices.TryGetValue(inputDevice, out string temp))
                {
                    revFoundKnownDevices.Remove(inputDevice);
                    foundKnownDevices.Remove(temp);
                    foundDevicePaths.Remove(temp);
                }
            }
        }

        public void StopControllers()
        {
            using (WriteLocker locker = new WriteLocker(_foundDevlocker))
            {
                foreach (InputDeviceBase inputDevice in foundKnownDevices.Values)
                {
                    inputDevice.Detach();
                }

                revFoundKnownDevices.Clear();
                foundKnownDevices.Clear();
                newKnownDevices.Clear();
                removedKnownDevices.Clear();
                foundDevicePaths.Clear();
            }
        }

        private bool SteamControllerDeviceCheckHandler(HidDevice hidDev, VidPidMeta meta)
        {
            bool result = false;

            if (meta != null)
            {
                if (meta.pid == STEAM_CONTROLLER_PRODUCT_ID ||
                    meta.pid == STEAM_DONGLE_CONTROLLER_PRODUCT_ID)
                {
                    SteamControllerLibrary.SteamControllerDevice tempDev =
                        new SteamControllerLibrary.SteamControllerDevice(hidDev, meta.displayName);

                    foundKnownDevices.Add(hidDev.DevicePath, tempDev);
                    revFoundKnownDevices.Add(tempDev, hidDev.DevicePath);
                    newKnownDevices.Add(hidDev.DevicePath, tempDev);
                    result = true;
                }
                else if (meta.pid == STEAM_BT_CONTROLLER_PRODUCT_ID)
                {
                    SteamControllerBTDevice tempDev = new SteamControllerBTDevice(hidDev, meta.displayName);
                    foundKnownDevices.Add(hidDev.DevicePath, tempDev);
                    revFoundKnownDevices.Add(tempDev, hidDev.DevicePath);
                    newKnownDevices.Add(hidDev.DevicePath, tempDev);
                    result = true;
                }
            }

            return result;
        }
    }
}
