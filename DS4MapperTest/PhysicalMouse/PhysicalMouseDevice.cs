namespace DS4MapperTest.PhysicalMouse
{
    /// <summary>
    /// A mouse discovered via Raw Input enumeration. <see cref="StableId"/> is
    /// what should be persisted in a profile/settings file; <see cref="DevicePath"/>
    /// is the same value today but kept as a distinct property so a more
    /// resilient identity (e.g. a SetupAPI instance id chain) can replace it
    /// later without changing every call site.
    /// </summary>
    public class PhysicalMouseDevice
    {
        /// <summary>
        /// Raw Input device path (RIDI_DEVICENAME), e.g.
        /// \\?\HID#VID_1234&amp;PID_5678&amp;...#{guid}. Stable across
        /// reconnects of the same physical device on the same port; not
        /// guaranteed stable across a different USB port/hub on every
        /// system, which is an inherent Raw Input/SetupAPI limitation.
        /// </summary>
        public string DevicePath { get; }

        /// <summary>
        /// Persistable identity to store in settings and resolve again later.
        /// Currently equal to <see cref="DevicePath"/>.
        /// </summary>
        public string StableId => DevicePath;

        public string FriendlyName { get; }

        public ushort VendorId { get; }
        public ushort ProductId { get; }
        public bool HasVendorProductId { get; }

        /// <summary>
        /// Best-effort heuristic (see <see cref="Util.CheckIfVirtualDevice"/>)
        /// for whether this device is driver-emulated (e.g. a FakerInput,
        /// reWASD or VirtualHere endpoint) rather than a physical USB/PS2
        /// mouse. Selection UI should default to hiding/flagging these.
        /// </summary>
        public bool IsLikelyVirtual { get; }

        public PhysicalMouseDevice(string devicePath, string friendlyName,
            ushort vendorId, ushort productId, bool hasVendorProductId, bool isLikelyVirtual)
        {
            DevicePath = devicePath;
            FriendlyName = friendlyName;
            VendorId = vendorId;
            ProductId = productId;
            HasVendorProductId = hasVendorProductId;
            IsLikelyVirtual = isLikelyVirtual;
        }

        public override string ToString()
        {
            return $"{FriendlyName} ({DevicePath})";
        }
    }
}
