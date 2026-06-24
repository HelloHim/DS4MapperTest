using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class MouseAccelerationTests
    {
        [TestMethod]
        public void LinearCurveUsesCombinedSpeed()
        {
            MouseAcceleration.CalculateMultipliers(
                GyroMouseAccelCurveChoice.Linear,
                5.0,
                0.0,
                10.0,
                1.0,
                3.0,
                2.0,
                4.0,
                1.0,
                1.0,
                20.0,
                out double multiplierX,
                out double multiplierY);

            Assert.AreEqual(2.0, multiplierX, 0.0000001);
            Assert.AreEqual(3.0, multiplierY, 0.0000001);
        }

        [TestMethod]
        public void SpeedBelowThresholdUsesMinimumSensitivity()
        {
            MouseAcceleration.CalculateMultipliers(
                GyroMouseAccelCurveChoice.Cubic,
                4.0,
                5.0,
                10.0,
                1.2,
                3.0,
                1.4,
                4.0,
                1.0,
                1.0,
                20.0,
                out double multiplierX,
                out double multiplierY);

            Assert.AreEqual(1.2, multiplierX, 0.0000001);
            Assert.AreEqual(1.4, multiplierY, 0.0000001);
        }
    }
}
