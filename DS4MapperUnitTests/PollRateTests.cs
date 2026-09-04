using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;

namespace DS4MapperUnitTests
{
    // The mapping loop used to run at a fixed 125 Hz whatever was plugged in,
    // so a controller reporting motion faster than that had samples it produced
    // never read at all.
    [TestClass]
    public class PollRateTests
    {
        private static ControllerCapabilities Caps(double? motionRateHz)
        {
            return new ControllerCapabilities(ControllerDisplayInfo.Unknown(),
                Array.Empty<ControllerInputDescriptor>(), motionRateHz);
        }

        [TestMethod]
        public void MotionRateIsKeptWhenTheDeviceReportsOne()
        {
            Assert.AreEqual(250.0, Caps(250.0).MotionSampleRateHz);
        }

        [TestMethod]
        public void AbsentOrNonsenseMotionRateReadsAsUnknown()
        {
            Assert.IsNull(Caps(null).MotionSampleRateHz);
            Assert.IsNull(Caps(0.0).MotionSampleRateHz,
                "A device reporting 0 Hz has told us nothing, not that it is silent.");
            Assert.IsNull(Caps(-5.0).MotionSampleRateHz);
        }

    }
}
