using System.Threading;
using DS4MapperTest.ActionUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class DoublePressFuncTests
    {
        // The tap window is measured off a real Stopwatch, so these tests have
        // to sleep. Sleeps therefore only ever push the clock well past a
        // window, never up to just inside one: a Thread.Sleep can overrun its
        // request by tens of milliseconds under load, and a test that needed a
        // short sleep to stay inside a short window failed intermittently for
        // no reason to do with the code under test. The two presses that must
        // land inside a window are adjacent statements with no sleep between
        // them, against a window long enough to absorb a scheduling hiccup.
        private const int TapWindowMs = 200;
        private const int PastWindowMs = 500;

        [TestMethod]
        public void DefaultTapWindow_IsUsable()
        {
            Assert.AreEqual(DoublePressFunc.DEFAULT_TAP_WINDOW_MS,
                new DoublePressFunc().DurationMs);
        }

        [TestMethod]
        public void SecondPressWithinWindowAfterFirstRelease_Activates()
        {
            DoublePressFunc func = new DoublePressFunc { DurationMs = TapWindowMs };
            TestMapper mapper = new TestMapper();

            func.Prepare(mapper, true, null);
            Thread.Sleep(PastWindowMs); // A slow first tap must not consume the second-tap window.
            func.Prepare(mapper, false, null);
            func.Prepare(mapper, true, null);

            Assert.IsTrue(func.active);
            Assert.IsTrue(func.outputActive);
        }

        [TestMethod]
        public void ExpiredWindow_TreatsNextPressAsNewFirstTap()
        {
            DoublePressFunc func = new DoublePressFunc { DurationMs = TapWindowMs };
            TestMapper mapper = new TestMapper();

            func.Prepare(mapper, true, null);
            func.Prepare(mapper, false, null);
            Thread.Sleep(PastWindowMs);

            func.Prepare(mapper, true, null); // Starts a new first tap.
            Assert.IsFalse(func.active, "A press after the window expired is a new first tap, not a second one.");

            func.Prepare(mapper, false, null);
            func.Prepare(mapper, true, null);

            Assert.IsTrue(func.active);
        }
    }
}
