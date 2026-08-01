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
            Assert.AreEqual("USB Optical Mouse - VID 1234, PID 0001", items[0].DisplayName);
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

        [TestMethod]
        public void ParentDeviceNameReplacesGenericHidMouseName()
        {
            var names = new Dictionary<string, string>
            {
                ["hid-mouse"] = "HID-compliant mouse",
                ["usb-receiver"] = "Logitech G Pro X Superlight",
            };
            var parents = new Dictionary<string, string>
            {
                ["hid-mouse"] = "usb-receiver",
                ["usb-receiver"] = @"HTREE\ROOT\0",
            };

            string result = PhysicalMouseEnumerator.FindFriendlyNameFromAncestorChain(
                "hid-mouse",
                instanceId => names.TryGetValue(instanceId, out string name) ? name : null,
                instanceId => parents.TryGetValue(instanceId, out string parent) ? parent : null);

            Assert.AreEqual("Logitech G Pro X Superlight", result);
        }

        [TestMethod]
        public void GenericAncestorNamesDoNotReplaceTheOriginalFallback()
        {
            var names = new Dictionary<string, string>
            {
                ["hid-mouse"] = "HID-compliant mouse",
                ["usb-parent"] = "USB Input Device",
            };
            var parents = new Dictionary<string, string>
            {
                ["hid-mouse"] = "usb-parent",
                ["usb-parent"] = @"HTREE\ROOT\0",
            };

            string result = PhysicalMouseEnumerator.FindFriendlyNameFromAncestorChain(
                "hid-mouse",
                instanceId => names.TryGetValue(instanceId, out string name) ? name : null,
                instanceId => parents.TryGetValue(instanceId, out string parent) ? parent : null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void SpecificBusDescriptionBeatsGenericFriendlyName()
        {
            string result = PhysicalMouseEnumerator.SelectPreferredDeviceName(
                "HID-compliant mouse", "Razer Viper 8K Hz", "HID-compliant device");

            Assert.AreEqual("Razer Viper 8K Hz", result);
        }
    }
}
