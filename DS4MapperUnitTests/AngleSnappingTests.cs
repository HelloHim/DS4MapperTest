using DS4MapperTest.Common;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class AngleSnappingTests
    {
        [TestMethod]
        public void DisabledSnappingLeavesVectorUnchanged()
        {
            double x = 10.0;
            double y = 2.0;

            AngleSnapping.Apply(ref x, ref y, 0.0, false);

            Assert.AreEqual(10.0, x);
            Assert.AreEqual(2.0, y);
        }

        [TestMethod]
        public void HardHorizontalSnapPreservesMagnitudeAndSign()
        {
            double x = -10.0;
            double y = 1.0;
            double magnitude = Math.Sqrt((x * x) + (y * y));

            AngleSnapping.Apply(ref x, ref y, 10.0, false);

            Assert.AreEqual(-magnitude, x, 0.0000001);
            Assert.AreEqual(0.0, y);
        }

        [TestMethod]
        public void HardVerticalSnapPreservesMagnitudeAndSign()
        {
            double x = 1.0;
            double y = -10.0;
            double magnitude = Math.Sqrt((x * x) + (y * y));

            AngleSnapping.Apply(ref x, ref y, 10.0, false);

            Assert.AreEqual(0.0, x);
            Assert.AreEqual(-magnitude, y, 0.0000001);
        }

        [TestMethod]
        public void SmoothSnapSuppressesOppositeAxis()
        {
            double x = 10.0;
            double y = 1.0;
            double originalY = y;
            double magnitude = Math.Sqrt((x * x) + (y * y));

            AngleSnapping.Apply(ref x, ref y, 10.0, true);

            Assert.AreEqual(magnitude, x, 0.0000001);
            Assert.IsTrue(y > 0.0);
            Assert.IsTrue(y < originalY);
        }
    }
}
