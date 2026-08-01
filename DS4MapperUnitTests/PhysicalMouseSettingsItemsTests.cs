using System.Collections.Generic;
using System.Linq;
using DS4MapperTest.PhysicalMouse;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class PhysicalMouseSettingsItemsTests
    {
        [TestMethod]
        public void ConnectedDevicesAreFriendlyAndVirtualDevicesAreHidden()
        {
            var devices = new List<PhysicalMouseDevice>
            {
                new PhysicalMouseDevice("mouse-a", "USB Optical Mouse", 0x1234, 0x0001, true, false),
                new PhysicalMouseDevice("virtual", "FakerInput", 0, 0, false, true),
            };

            List<PhysicalMouseSettingsItem> items = PhysicalMouseSettingsItems.Create(devices, "mouse-a");

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("USB Optical Mouse", items[0].DisplayName);
            Assert.AreEqual("mouse-a", items[0].StableId);
        }

        [TestMethod]
        public void DuplicateNamesAreDisambiguatedAndSavedUnavailableIsRetained()
        {
            var devices = new List<PhysicalMouseDevice>
            {
                new PhysicalMouseDevice("mouse-a", "USB Optical Mouse", 0x1234, 0x0001, true, false),
                new PhysicalMouseDevice("mouse-b", "USB Optical Mouse", 0x5678, 0x0002, true, false),
            };

            List<PhysicalMouseSettingsItem> items = PhysicalMouseSettingsItems.Create(devices, "saved-mouse");

            Assert.IsTrue(items[0].DisplayName.Contains("VID 1234"));
            Assert.IsTrue(items[1].DisplayName.Contains("VID 5678"));
            PhysicalMouseSettingsItem unavailable = items.Single(i => i.StableId == "saved-mouse");
            Assert.IsFalse(unavailable.IsAvailable);
            Assert.IsTrue(unavailable.DisplayName.Contains("unavailable"));
        }
    }
}
