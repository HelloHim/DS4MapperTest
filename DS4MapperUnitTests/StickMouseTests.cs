using DS4MapperTest.StickActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickMouseTests
    {
        [TestMethod]
        public void UsesRequestedDefaultMouseSpeed()
        {
            StickMouse action = new StickMouse();

            Assert.AreEqual(3000, action.MouseSpeed);
        }

        [TestMethod]
        public void ClampsMouseSpeedToSupportedRange()
        {
            StickMouse action = new StickMouse();

            action.MouseSpeed = 10001;
            Assert.AreEqual(10000, action.MouseSpeed);

            action.MouseSpeed = -1;
            Assert.AreEqual(0, action.MouseSpeed);
        }
    }
}
