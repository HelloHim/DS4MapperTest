using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalMapperElapsedTimeTests
    {
        [TestMethod]
        public void FirstFrameUsesTheNominalPollPeriod()
        {
            UniversalControllerStateSnapshot previous =
                CreateSnapshot(DateTimeOffset.UnixEpoch, sequence: 0);
            UniversalControllerStateSnapshot current =
                CreateSnapshot(DateTimeOffset.UnixEpoch.AddSeconds(3), sequence: 1);

            Assert.AreEqual(UniversalMapper.DefaultElapsedSeconds,
                UniversalMapper.CalculateElapsedSeconds(current, previous));
        }

        [TestMethod]
        public void NormalFrameKeepsItsMeasuredInterval()
        {
            UniversalControllerStateSnapshot previous =
                CreateSnapshot(DateTimeOffset.UnixEpoch, sequence: 1);
            UniversalControllerStateSnapshot current =
                CreateSnapshot(DateTimeOffset.UnixEpoch.AddMilliseconds(8), sequence: 2);

            Assert.AreEqual(0.008,
                UniversalMapper.CalculateElapsedSeconds(current, previous), 1e-9);
        }

        [TestMethod]
        public void ResumeFromSleepIsClampedToTheStallLimit()
        {
            // A machine suspended for five minutes with a controller still
            // attached delivers one frame carrying the whole gap. Integrating
            // it unclamped threw the pointer across the screen on wake.
            UniversalControllerStateSnapshot previous =
                CreateSnapshot(DateTimeOffset.UnixEpoch, sequence: 1);
            UniversalControllerStateSnapshot current =
                CreateSnapshot(DateTimeOffset.UnixEpoch.AddMinutes(5), sequence: 2);

            Assert.AreEqual(UniversalMapper.MaxElapsedSeconds,
                UniversalMapper.CalculateElapsedSeconds(current, previous));
        }

        [TestMethod]
        public void BackwardsClockStepFallsBackToTheNominalPollPeriod()
        {
            UniversalControllerStateSnapshot previous =
                CreateSnapshot(DateTimeOffset.UnixEpoch.AddSeconds(10), sequence: 1);
            UniversalControllerStateSnapshot current =
                CreateSnapshot(DateTimeOffset.UnixEpoch.AddSeconds(9), sequence: 2);

            Assert.AreEqual(UniversalMapper.DefaultElapsedSeconds,
                UniversalMapper.CalculateElapsedSeconds(current, previous));
        }

        [TestMethod]
        public void MonotonicClockNeverGoesBackwards()
        {
            DateTimeOffset previous = UniversalMonotonicClock.UtcNow;
            for (int i = 0; i < 1000; i++)
            {
                DateTimeOffset current = UniversalMonotonicClock.UtcNow;
                Assert.IsTrue(current >= previous);
                previous = current;
            }
        }

        private static UniversalControllerStateSnapshot CreateSnapshot(
            DateTimeOffset timestampUtc, long sequence)
        {
            return new UniversalControllerStateSnapshot(
                timestampUtc,
                sequence,
                true,
                new Dictionary<UniversalInputId, UniversalInputValue>());
        }
    }
}
