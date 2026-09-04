using DS4MapperTest;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DS4MapperUnitTests
{
    // The mapping loop's cadence, and therefore how smooth gyro output feels,
    // rests on this wait being accurate without a timeBeginPeriod request
    // behind it. Windows ignores that request for a power throttled background
    // process, which is when the old Thread.Sleep pacing quietly lost half its
    // poll rate.
    [TestClass]
    public class PrecisionLoopTimerTests
    {
        [TestMethod]
        public void HighResolutionTimerIsAvailableOnWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("High resolution waitable timers are Windows-only.");
            }

            using PrecisionLoopTimer timer = new PrecisionLoopTimer();
            Assert.IsTrue(timer.IsHighResolution,
                "Expected a high resolution waitable timer on Windows 10 1803 or later.");
        }

        [TestMethod]
        public void WaitNeverReturnsEarly()
        {
            using PrecisionLoopTimer timer = new PrecisionLoopTimer();

            Stopwatch clock = Stopwatch.StartNew();
            for (int pass = 0; pass < 10; pass++)
            {
                timer.Wait(8.0);
            }

            // A tenth of a millisecond of slack per wait for the timestamp
            // itself; returning meaningfully early would mean the loop spinning
            // faster than the controller can produce samples.
            Assert.IsTrue(clock.Elapsed.TotalMilliseconds >= 79.0,
                $"Ten 8 ms waits took only {clock.Elapsed.TotalMilliseconds:0.0} ms.");
        }

        [TestMethod]
        public void NonPositiveWaitReturnsImmediately()
        {
            using PrecisionLoopTimer timer = new PrecisionLoopTimer();

            Stopwatch clock = Stopwatch.StartNew();
            timer.Wait(0.0);
            timer.Wait(-5.0);

            Assert.IsTrue(clock.Elapsed.TotalMilliseconds < 50.0,
                "A loop that has already missed its deadline must not wait at all.");
        }

        [TestMethod]
        public void DisposeIsSafeToRepeat()
        {
            PrecisionLoopTimer timer = new PrecisionLoopTimer();
            timer.Dispose();
            timer.Dispose();

            // The fallback path still has to honour the contract after disposal
            // rather than throwing into the mapping loop.
            Assert.IsFalse(timer.IsHighResolution);
        }
    }
}
