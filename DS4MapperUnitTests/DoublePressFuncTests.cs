using System.Threading;
using DS4MapperTest.ActionUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class DoublePressFuncTests
    {
        [TestMethod]
        public void DefaultTapWindow_IsUsable()
        {
            Assert.AreEqual(DoublePressFunc.DEFAULT_TAP_WINDOW_MS,
                new DoublePressFunc().DurationMs);
        }

        [TestMethod]
        public void SecondPressWithinWindowAfterFirstRelease_Activates()
        {
            DoublePressFunc func = new DoublePressFunc { DurationMs = 30 };
            TestMapper mapper = new TestMapper();

            func.Prepare(mapper, true, null);
            Thread.Sleep(45); // A slow first tap must not consume the second-tap window.
            func.Prepare(mapper, false, null);
            Thread.Sleep(5);
            func.Prepare(mapper, true, null);

            Assert.IsTrue(func.active);
            Assert.IsTrue(func.outputActive);
        }

        [TestMethod]
        public void ExpiredWindow_TreatsNextPressAsNewFirstTap()
        {
            DoublePressFunc func = new DoublePressFunc { DurationMs = 20 };
            TestMapper mapper = new TestMapper();

            func.Prepare(mapper, true, null);
            func.Prepare(mapper, false, null);
            Thread.Sleep(35);
            func.Prepare(mapper, true, null); // Starts a new first tap.
            func.Prepare(mapper, false, null);
            Thread.Sleep(5);
            func.Prepare(mapper, true, null);

            Assert.IsTrue(func.active);
        }
    }
}
