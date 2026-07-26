using DS4MapperTest.InputDevices.SteamControllerTritonLibrary;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class SteamControllerTritonReaderTests
    {
        [TestMethod]
        public void FirstControllerStateReport_RecognisesWiredAndBleReports()
        {
            Assert.IsTrue(SteamControllerTritonReader.IsFirstControllerStateReport(
                true, SteamControllerTritonDevice.ID_TRITON_CONTROLLER_STATE));
            Assert.IsTrue(SteamControllerTritonReader.IsFirstControllerStateReport(
                true, SteamControllerTritonDevice.ID_TRITON_CONTROLLER_STATE_BLE));
        }

        [TestMethod]
        public void FirstControllerStateReport_DoesNotResetForLaterOrInvalidReports()
        {
            Assert.IsFalse(SteamControllerTritonReader.IsFirstControllerStateReport(
                false, SteamControllerTritonDevice.ID_TRITON_CONTROLLER_STATE));
            Assert.IsFalse(SteamControllerTritonReader.IsFirstControllerStateReport(
                false, SteamControllerTritonDevice.ID_TRITON_CONTROLLER_STATE_BLE));
            Assert.IsFalse(SteamControllerTritonReader.IsFirstControllerStateReport(
                true, SteamControllerTritonDevice.ID_TRITON_BATTERY_STATUS));
        }
    }
}
