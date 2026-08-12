namespace DS4MapperTest.Universal
{
    public static class OriginalSteamControllerIdentity
    {
        public const ushort ValveVendorId = 0x28DE;
        public const ushort WiredProductId = 0x1102;
        public const ushort DongleProductId = 0x1142;
        public const ushort BluetoothProductId = 0x1106;

        public static bool IsOriginalSteamController(ushort? vendorId, ushort? productId)
        {
            return vendorId == ValveVendorId &&
                (productId == WiredProductId ||
                 productId == DongleProductId ||
                 productId == BluetoothProductId);
        }
    }
}
