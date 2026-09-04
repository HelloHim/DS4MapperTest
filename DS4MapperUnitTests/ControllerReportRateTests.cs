using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class ControllerReportRateTests
    {
        private static readonly DateTimeOffset Origin =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void RateIsUnknownUntilAFullWindowHasElapsed()
        {
            ControllerReportRateMeter meter = new ControllerReportRateMeter();
            for (int i = 0; i < 50; i++)
            {
                meter.RecordReport(Origin.AddMilliseconds(i * 4));
            }

            Assert.IsNull(meter.MeasuredRateHz,
                "Reporting a rate from a partial window would let the loop chase noise.");
        }

        [TestMethod]
        public void MeasuresTheRateReportsActuallyArriveAt()
        {
            ControllerReportRateMeter meter = new ControllerReportRateMeter();

            // 250 reports spread evenly across one second.
            for (int i = 0; i <= 250; i++)
            {
                meter.RecordReport(Origin.AddSeconds(i / 250.0));
            }

            Assert.IsNotNull(meter.MeasuredRateHz);
            Assert.AreEqual(250.0, meter.MeasuredRateHz.Value, 5.0);
        }

        [TestMethod]
        public void AControllerThatGoesQuietStopsAdvertisingItsOldRate()
        {
            ControllerReportRateMeter meter = new ControllerReportRateMeter();
            for (int i = 0; i <= 250; i++)
            {
                meter.RecordReport(Origin.AddSeconds(i / 250.0));
            }

            Assert.IsNotNull(meter.MeasuredRateHz);

            // No reports at all across the next window.
            meter.Tick(Origin.AddSeconds(2.5));
            Assert.IsNull(meter.MeasuredRateHz,
                "A silent controller must not keep claiming the rate it used to manage.");
        }

        [TestMethod]
        public void CapIsClampedToTheRatesTheLoopCanActuallyRun()
        {
            Assert.AreEqual(125, AppSettingsStore.ClampPollRateCap(10));
            Assert.AreEqual(1000, AppSettingsStore.ClampPollRateCap(50000));
            Assert.AreEqual(500, AppSettingsStore.ClampPollRateCap(500));
        }

        [TestMethod]
        public void DefaultCapDoesNotLimitAnySupportedHardware()
        {
            // Out of the box the ceiling must not be the thing deciding the
            // rate; that job belongs to the measured hardware.
            Assert.IsTrue(
                AppSettingsStore.DEFAULT_POLL_RATE_CAP_HZ >=
                    (int)UniversalMappingRuntime.MaximumPollRateHz,
                $"Default cap {AppSettingsStore.DEFAULT_POLL_RATE_CAP_HZ} Hz is below the " +
                $"fastest rate the loop supports.");
        }
    }
}
