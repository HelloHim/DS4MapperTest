using DS4MapperTest;
using DS4MapperTest.GyroActions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class DS4NativeGyroOutputTests
    {
        [TestMethod]
        public void DualSenseFrameConvertsToDualShock4NativeOutputAxes()
        {
            GyroMotionAxisAdapter.ToDualShock4OutputSpace(InputDeviceType.DualSense,
                frameGyroYaw: 11, frameGyroPitch: 22, frameGyroRoll: 33,
                frameAccelX: 44, frameAccelY: 55, frameAccelZ: 66,
                out short gyroX, out short gyroY, out short gyroZ,
                out short accelX, out short accelY, out short accelZ);

            Assert.AreEqual((short)-22, gyroX);
            Assert.AreEqual((short)-11, gyroY);
            Assert.AreEqual((short)-33, gyroZ);
            Assert.AreEqual((short)-44, accelX);
            Assert.AreEqual((short)-55, accelY);
            Assert.AreEqual((short)66, accelZ);
        }

        [TestMethod]
        public void SteamControllerFrameConvertsToDualShock4NativeOutputAxes()
        {
            GyroMotionAxisAdapter.ToDualShock4OutputSpace(InputDeviceType.SteamController,
                frameGyroYaw: 11, frameGyroPitch: 22, frameGyroRoll: 33,
                frameAccelX: 44, frameAccelY: 55, frameAccelZ: 66,
                out short gyroX, out short gyroY, out short gyroZ,
                out short accelX, out short accelY, out short accelZ);

            Assert.AreEqual((short)-22, gyroX);
            Assert.AreEqual((short)-11, gyroY);
            Assert.AreEqual((short)33, gyroZ);
            Assert.AreEqual((short)-44, accelX);
            Assert.AreEqual((short)66, accelY);
            Assert.AreEqual((short)-55, accelZ);
        }

        [TestMethod]
        public void PassthruAndUnboundDifferentiateNativeGyroOutput()
        {
            Assert.IsTrue(new GyroPassthruAction().OutputsNativeGyro);
            Assert.IsFalse(new GyroNoMapAction().OutputsNativeGyro);
            Assert.IsTrue(new GyroMouse().OutputsNativeGyro);
        }
    }
}
