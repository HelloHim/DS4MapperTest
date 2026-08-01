using DS4MapperTest.PhysicalMouse;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class PhysicalMouseDeviceIdentifierTests
    {
        [TestMethod]
        public void NormalizeDevicePath_ConvertsNtNamespaceFormToWin32Form()
        {
            const string ntPath = @"\??\HID#VID_1234&PID_5678#7&2a3b4c5d&0&0000#{378de44c-56ef-11d1-bc8c-00a0c91405dd}";

            string result = PhysicalMouseEnumerator.NormalizeDevicePath(ntPath);

            Assert.AreEqual(
                @"\\?\HID#VID_1234&PID_5678#7&2a3b4c5d&0&0000#{378de44c-56ef-11d1-bc8c-00a0c91405dd}",
                result);
        }

        [TestMethod]
        public void NormalizeDevicePath_LeavesWin32FormUnchanged()
        {
            const string win32Path = @"\\?\HID#VID_1234&PID_5678#7&2a3b4c5d&0&0000#{378de44c-56ef-11d1-bc8c-00a0c91405dd}";

            string result = PhysicalMouseEnumerator.NormalizeDevicePath(win32Path);

            Assert.AreEqual(win32Path, result);
        }

        [TestMethod]
        public void StableIdIsTheNormalisedDevicePathNotATransientHandle()
        {
            var device = new PhysicalMouseDevice(
                devicePath: @"\\?\HID#VID_1234&PID_5678#7&2a3b4c5d&0&0000#{guid}",
                friendlyName: "Test Mouse",
                vendorId: 0x1234,
                productId: 0x5678,
                hasVendorProductId: true,
                isLikelyVirtual: false);

            // StableId must be resolvable again later purely from the string
            // - it must never be, or derive from, a Raw Input hDevice.
            Assert.AreEqual(device.DevicePath, device.StableId);
        }
    }
}
