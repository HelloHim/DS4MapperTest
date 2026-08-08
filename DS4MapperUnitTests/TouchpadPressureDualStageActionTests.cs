using DS4MapperTest.ButtonActions;
using DS4MapperTest.TriggerActions;

namespace DS4MapperUnitTests
{
    // Exercises TouchpadPressureDualStageAction end to end: thresholds, touch gating,
    // hysteresis, stage transitions, independent left/right pads, and every reused trigger
    // activation style. Uses TestMapper as the Mapper argument with empty (unbound)
    // output funcs on the Soft/Full Press sub-buttons, since only the state-machine
    // transitions are under test here, not real keyboard/mouse dispatch.
    [TestClass]
    public class TouchpadPressureDualStageActionTests
    {
        [TestMethod]
        public void Defaults_MatchSpec()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            Assert.AreEqual(4096, action.SoftPressThreshold);
            Assert.AreEqual(17096, action.FullPressThreshold);
            Assert.AreEqual(100, action.HipFireDelayMs);
            Assert.AreEqual(TriggerDualStageAction.DualStageMode.Threshold, action.ActivationStyle);
        }

        [TestMethod]
        public void SoftPressThreshold_ClampsBelowZeroToZero()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            action.SoftPressThreshold = -500;
            Assert.AreEqual(0, action.SoftPressThreshold);
        }

        [TestMethod]
        public void FullPressThreshold_ClampsAboveMaxToMax()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            action.FullPressThreshold = 99999;
            Assert.AreEqual(32767, action.FullPressThreshold);
        }

        [TestMethod]
        public void SoftPressThreshold_CannotReachOrExceedFullPressThreshold()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction
            {
                FullPressThreshold = 5000,
            };
            action.SoftPressThreshold = 5000;
            Assert.IsTrue(action.SoftPressThreshold < action.FullPressThreshold);
            Assert.AreEqual(5000, action.SoftPressThreshold);
            Assert.AreEqual(5001, action.FullPressThreshold);
        }

        [TestMethod]
        public void FullPressThreshold_CannotDropToOrBelowSoftPressThreshold()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction
            {
                SoftPressThreshold = 4096,
            };
            action.FullPressThreshold = 1000;
            Assert.IsTrue(action.SoftPressThreshold < action.FullPressThreshold);
            Assert.AreEqual(1000, action.FullPressThreshold);
            Assert.AreEqual(999, action.SoftPressThreshold);
        }

        [TestMethod]
        public void NoTouch_NeverActivatesRegardlessOfPressure()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            TestMapper mapper = new TestMapper();

            action.PrepareTouchpadPressure(mapper, 0, touched: false);
            Assert.IsFalse(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);

            action.PrepareTouchpadPressure(mapper, 32767, touched: false);
            Assert.IsFalse(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);
        }

        [TestMethod]
        public void Touch_WithSufficientPressure_ActivatesSoftThenFull()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            TestMapper mapper = new TestMapper();

            action.PrepareTouchpadPressure(mapper, 0, touched: true);
            Assert.IsFalse(action.softPressActActive);

            action.PrepareTouchpadPressure(mapper, 5000, touched: true); // above soft(4096), below full(17096)
            Assert.IsTrue(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);

            action.PrepareTouchpadPressure(mapper, 20000, touched: true); // above full(17096)
            Assert.IsTrue(action.softPressActActive);
            Assert.IsTrue(action.fullPressActActive);
        }

        [TestMethod]
        public void FingerLift_ReleasesAllStagesImmediately()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            TestMapper mapper = new TestMapper();

            action.PrepareTouchpadPressure(mapper, 20000, touched: true);
            Assert.IsTrue(action.fullPressActActive);

            // Finger lifts while pressure reading is still stale/nonzero - must release
            // immediately rather than trusting the stale value.
            action.PrepareTouchpadPressure(mapper, 20000, touched: false);
            Assert.IsFalse(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);
            Assert.AreEqual(TriggerDualStageAction.ActiveZoneButtons.None, action.currentActiveButtons);
        }

        [TestMethod]
        public void StageTransitions_NoStuckOrDuplicateEvents()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            TestMapper mapper = new TestMapper();

            // Released -> Soft
            action.PrepareTouchpadPressure(mapper, 5000, true);
            if (action.active) action.Event(mapper);
            Assert.IsTrue(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);

            // Soft -> Full
            action.PrepareTouchpadPressure(mapper, 20000, true);
            if (action.active) action.Event(mapper);
            Assert.IsTrue(action.softPressActActive);
            Assert.IsTrue(action.fullPressActActive);

            // Full -> Soft
            action.PrepareTouchpadPressure(mapper, 5000, true);
            if (action.active) action.Event(mapper);
            Assert.IsTrue(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);

            // Soft -> Released
            action.PrepareTouchpadPressure(mapper, 0, true);
            if (action.active) action.Event(mapper);
            Assert.IsFalse(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);

            // Released -> Full directly (full click straight from zero)
            action.PrepareTouchpadPressure(mapper, 20000, true);
            if (action.active) action.Event(mapper);
            Assert.IsTrue(action.fullPressActActive);

            // Full -> Released
            action.PrepareTouchpadPressure(mapper, 0, true);
            if (action.active) action.Event(mapper);
            Assert.IsFalse(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);
        }

        [TestMethod]
        public void IndependentPads_LeftDoesNotAffectRight()
        {
            TouchpadPressureDualStageAction left = new TouchpadPressureDualStageAction();
            TouchpadPressureDualStageAction right = new TouchpadPressureDualStageAction();
            TestMapper mapper = new TestMapper();

            left.PrepareTouchpadPressure(mapper, 20000, true);
            right.PrepareTouchpadPressure(mapper, 0, false);

            Assert.IsTrue(left.fullPressActActive);
            Assert.IsFalse(right.softPressActActive);
            Assert.IsFalse(right.fullPressActActive);

            // Both pads can be fully active independently at the same time.
            right.PrepareTouchpadPressure(mapper, 20000, true);
            Assert.IsTrue(left.fullPressActActive);
            Assert.IsTrue(right.fullPressActActive);
        }

        [TestMethod]
        public void Hysteresis_PreventsChatterNearSoftThreshold()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction();
            TestMapper mapper = new TestMapper();

            action.PrepareTouchpadPressure(mapper, 4200, true); // just above 4096 soft threshold
            Assert.IsTrue(action.softPressActActive);

            // Small dip below the raw threshold, but within the hysteresis band - stays active.
            action.PrepareTouchpadPressure(mapper, 3900, true);
            Assert.IsTrue(action.softPressActActive);

            // Drop further, past the hysteresis band, releases.
            action.PrepareTouchpadPressure(mapper, 3000, true);
            Assert.IsFalse(action.softPressActActive);
        }

        [TestMethod]
        public void ExclusiveButtons_FullExcludesSoft()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction
            {
                ActivationStyle = TriggerDualStageAction.DualStageMode.ExclusiveButtons,
            };
            TestMapper mapper = new TestMapper();

            action.PrepareTouchpadPressure(mapper, 5000, true);
            Assert.IsTrue(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);

            action.PrepareTouchpadPressure(mapper, 20000, true);
            Assert.IsFalse(action.softPressActActive);
            Assert.IsTrue(action.fullPressActActive);
        }

        [TestMethod]
        public void HairTrigger_SoftOnlyFollowsPriorFullPull()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction
            {
                ActivationStyle = TriggerDualStageAction.DualStageMode.HairTrigger,
            };
            TestMapper mapper = new TestMapper();

            // Partial pressure alone, never having reached full, does not engage soft.
            action.PrepareTouchpadPressure(mapper, 5000, true);
            Assert.IsFalse(action.softPressActActive);

            // Full pressure engages both.
            action.PrepareTouchpadPressure(mapper, 20000, true);
            Assert.IsTrue(action.softPressActActive);
            Assert.IsTrue(action.fullPressActActive);

            // Dropping back to partial now keeps soft engaged (full pull happened last frame).
            action.PrepareTouchpadPressure(mapper, 5000, true);
            Assert.IsTrue(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);
        }

        [TestMethod]
        public void HipFire_FullPressureLatchesFullPullOnlyAcrossTwoFrames()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction
            {
                ActivationStyle = TriggerDualStageAction.DualStageMode.HipFire,
            };
            TestMapper mapper = new TestMapper();

            // Frame 1 arms the timer; frame 2 (still at full pressure) latches and forces
            // immediate activation via the full-pull condition, independent of elapsed time.
            action.PrepareTouchpadPressure(mapper, 20000, true);
            action.PrepareTouchpadPressure(mapper, 20000, true);
            action.PrepareTouchpadPressure(mapper, 20000, true);
            Assert.IsTrue(action.fullPressActActive);

            action.PrepareTouchpadPressure(mapper, 0, true);
            Assert.IsFalse(action.softPressActActive);
            Assert.IsFalse(action.fullPressActActive);
        }

        [TestMethod]
        public void HipFireExclusiveButtons_FullPressureLatchesFullPullOnly()
        {
            TouchpadPressureDualStageAction action = new TouchpadPressureDualStageAction
            {
                ActivationStyle = TriggerDualStageAction.DualStageMode.HipFireExclusiveButtons,
            };
            TestMapper mapper = new TestMapper();

            action.PrepareTouchpadPressure(mapper, 20000, true);
            action.PrepareTouchpadPressure(mapper, 20000, true);
            Assert.IsTrue(action.fullPressActActive);
            Assert.IsFalse(action.softPressActActive);

            action.PrepareTouchpadPressure(mapper, 0, true);
            Assert.IsFalse(action.fullPressActActive);
        }
    }
}
