namespace DS4MapperTest.Universal
{
    // SDL reports the 2026 Steam Controller (Triton) under the plain name
    // "Steam Controller", which is indistinguishable from the 2015 model by
    // name alone. Device-type resolution therefore has to go by USB ids, or
    // every Triton-specific behaviour (pad rotation, gyro space, touchpad
    // pressure, profile folder) silently stays switched off.
    public static class SteamController2026Identity
    {
        public const ushort ValveVendorId = 0x28DE;

        // Observed on the wired connection. The 0x1302 sibling shows up on the
        // same HID collection layout when the controller is on its dongle.
        public const ushort WiredProductId = 0x1304;
        public const ushort DongleProductId = 0x1302;

        public static bool IsSteamController2026(ushort? vendorId, ushort? productId)
        {
            return vendorId == ValveVendorId &&
                (productId == WiredProductId ||
                 productId == DongleProductId);
        }
    }
}
