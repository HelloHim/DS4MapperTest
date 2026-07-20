using DS4MapperTest.TriggerActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class TriggerPullStateMachineTests
    {
        private sealed class FakeClock
        {
            public long Timestamp;
            public long Now() => Timestamp;
            public void AdvanceMs(int ms)
            {
                Timestamp += ms * System.Diagnostics.Stopwatch.Frequency / 1000;
            }
        }

        [TestMethod]
        public void SimpleThreshold_AllowsSoftThenFull()
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            TriggerPullStateMachine.Result soft =
                machine.Update(TriggerStyle.SimpleThreshold, 0.2, 0.9, 75);
            TriggerPullStateMachine.Result full =
                machine.Update(TriggerStyle.SimpleThreshold, 1.0, 0.9, 75);

            Assert.IsTrue(soft.SoftAllowed);
            Assert.IsFalse(soft.FullAllowed);
            Assert.IsTrue(full.SoftAllowed);
            Assert.IsTrue(full.FullAllowed);
        }

        [TestMethod]
        public void FullPullOnly_NeverAllowsSoft()
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            TriggerPullStateMachine.Result soft =
                machine.Update(TriggerStyle.FullPullOnly, 0.2, 0.9, 75);
            TriggerPullStateMachine.Result full =
                machine.Update(TriggerStyle.FullPullOnly, 1.0, 0.9, 75);

            Assert.IsFalse(soft.SoftAllowed);
            Assert.IsFalse(soft.FullAllowed);
            Assert.IsFalse(full.SoftAllowed);
            Assert.IsTrue(full.FullAllowed);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(50)]
        [DataRow(74)]
        [DataRow(75)]
        public void HipFire_QuickFullPullAllowsFullOnly(int elapsedMs)
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            machine.Update(TriggerStyle.HipFire, 0.2, 0.9, 75);
            clock.AdvanceMs(elapsedMs);
            TriggerPullStateMachine.Result result =
                machine.Update(TriggerStyle.HipFire, 1.0, 0.9, 75);

            Assert.IsFalse(result.SoftAllowed);
            Assert.IsTrue(result.FullAllowed);
        }

        [TestMethod]
        public void HipFire_SlowPullAllowsSoftThenFull()
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            machine.Update(TriggerStyle.HipFire, 0.2, 0.9, 75);
            clock.AdvanceMs(76);
            TriggerPullStateMachine.Result soft =
                machine.Update(TriggerStyle.HipFire, 0.2, 0.9, 75);
            TriggerPullStateMachine.Result full =
                machine.Update(TriggerStyle.HipFire, 1.0, 0.9, 75);

            Assert.IsTrue(soft.SoftAllowed);
            Assert.IsFalse(soft.FullAllowed);
            Assert.IsTrue(full.SoftAllowed);
            Assert.IsTrue(full.FullAllowed);
        }

        [TestMethod]
        public void HipFire_ReleaseCancelsPendingSoft()
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            TriggerPullStateMachine.Result pending =
                machine.Update(TriggerStyle.HipFire, 0.2, 0.9, 75);
            TriggerPullStateMachine.Result released =
                machine.Update(TriggerStyle.HipFire, 0.0, 0.9, 75);

            Assert.IsTrue(pending.SoftPending);
            Assert.IsFalse(released.SoftAllowed);
            Assert.IsFalse(released.FullAllowed);
            Assert.IsFalse(released.SoftPending);
        }

        [TestMethod]
        public void HipFire_AbsentFullThresholdStillAllowsSoftAfterWindow()
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            machine.Update(TriggerStyle.HipFire, 1.0, 2.0, 75);
            clock.AdvanceMs(76);
            TriggerPullStateMachine.Result result =
                machine.Update(TriggerStyle.HipFire, 1.0, 2.0, 75);

            Assert.IsTrue(result.SoftAllowed);
            Assert.IsFalse(result.FullAllowed);
        }

        [TestMethod]
        public void HipFire_FullOutcomeSuppressesSoftUntilReset()
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            machine.Update(TriggerStyle.HipFire, 0.2, 0.9, 75);
            TriggerPullStateMachine.Result full =
                machine.Update(TriggerStyle.HipFire, 1.0, 0.9, 75);
            TriggerPullStateMachine.Result retreat =
                machine.Update(TriggerStyle.HipFire, 0.4, 0.9, 75);
            machine.Update(TriggerStyle.HipFire, 0.0, 0.9, 75);
            clock.AdvanceMs(76);
            TriggerPullStateMachine.Result nextPull =
                machine.Update(TriggerStyle.HipFire, 0.2, 0.9, 75);
            clock.AdvanceMs(76);
            nextPull = machine.Update(TriggerStyle.HipFire, 0.2, 0.9, 75);

            Assert.IsTrue(full.FullAllowed);
            Assert.IsFalse(retreat.SoftAllowed);
            Assert.IsTrue(nextPull.SoftAllowed);
        }

        [TestMethod]
        public void HipFireExclusive_SelectsOneOutcomeUntilReset()
        {
            FakeClock clock = new FakeClock();
            TriggerPullStateMachine machine = new TriggerPullStateMachine(clock.Now);

            machine.Update(TriggerStyle.HipFireExclusive, 0.2, 0.9, 75);
            clock.AdvanceMs(76);
            TriggerPullStateMachine.Result soft =
                machine.Update(TriggerStyle.HipFireExclusive, 0.2, 0.9, 75);
            TriggerPullStateMachine.Result laterFull =
                machine.Update(TriggerStyle.HipFireExclusive, 1.0, 0.9, 75);
            machine.Update(TriggerStyle.HipFireExclusive, 0.0, 0.9, 75);
            TriggerPullStateMachine.Result nextFull =
                machine.Update(TriggerStyle.HipFireExclusive, 1.0, 0.9, 75);

            Assert.IsTrue(soft.SoftAllowed);
            Assert.IsFalse(laterFull.FullAllowed);
            Assert.IsFalse(nextFull.SoftAllowed);
            Assert.IsTrue(nextFull.FullAllowed);
        }
    }
}
