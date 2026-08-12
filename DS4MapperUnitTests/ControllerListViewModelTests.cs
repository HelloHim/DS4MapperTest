using DS4MapperTest;
using DS4MapperTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class ControllerListViewModelTests
    {
        [TestMethod]
        public void DeviceListItemStringIncludesBatteryForClosedComboBox()
        {
            FakeInputDevice device = new FakeInputDevice("Test Controller");
            device.Battery = 67;

            DeviceListItem item = new DeviceListItem(device, 0, null);

            Assert.AreEqual("67%", item.Battery);
            Assert.AreEqual("Test Controller  67%", item.ToString());
        }

        private sealed class FakeInputDevice : InputDeviceBase
        {
            public FakeInputDevice(string name)
            {
                devTypeStr = name;
            }

            public override void SetOperational()
            {
            }

            public override void Detach()
            {
            }
        }
    }
}
