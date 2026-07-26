using DS4MapperTest.InputDevices.SteamControllerTritonLibrary;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class SteamControllerTritonMapperTests
    {
        [TestMethod]
        public void NormaliseStickAxis_PreservesFullSymmetricRange()
        {
            Assert.AreEqual(-32767,
                SteamControllerTritonMapper.NormaliseStickAxis(short.MinValue));
            Assert.AreEqual(0,
                SteamControllerTritonMapper.NormaliseStickAxis(0));
            Assert.AreEqual(32767,
                SteamControllerTritonMapper.NormaliseStickAxis(short.MaxValue));
        }

        [TestMethod]
        public void NormaliseStickAxis_DoesNotRetainTheFormerThirtyThousandClamp()
        {
            const short beyondFormerClamp = 30001;

            Assert.AreEqual(beyondFormerClamp,
                SteamControllerTritonMapper.NormaliseStickAxis(beyondFormerClamp));
        }

        [TestMethod]
        public void NormaliseStickAxis_UsesTheSameBoundaryForBothTritonSticks()
        {
            short leftStick = SteamControllerTritonMapper.NormaliseStickAxis(
                short.MinValue);
            short rightStick = SteamControllerTritonMapper.NormaliseStickAxis(
                short.MinValue);

            Assert.AreEqual(-32767, leftStick);
            Assert.AreEqual(leftStick, rightStick);
            Assert.AreNotEqual(short.MinValue, leftStick);
        }
    }
}
