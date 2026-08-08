using System.Threading;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.TriggerActions;
using static DS4MapperTest.TriggerActions.TriggerDualStageAction;

namespace DS4MapperUnitTests
{
    // DualStageEvaluator.ProcessCurrentStage was extracted verbatim from
    // TriggerDualStageAction.ProcessCurrentStage so touchpad pressure could reuse the exact
    // same activation-style state machine. These tests pin down that behaviour directly
    // against the shared evaluator (independent of any Trigger or Touchpad action wrapper)
    // for all five activation styles.
    [TestClass]
    public class DualStageEvaluatorTests
    {
        [TestMethod]
        public void Threshold_NoneBelowZero()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();
            ActiveZoneButtons result = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.Threshold, 0.0, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.None, result);
        }

        [TestMethod]
        public void Threshold_SoftPullWhenNonZero()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();
            ActiveZoneButtons result = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.Threshold, 0.5, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.SoftPull, result);
        }

        [TestMethod]
        public void Threshold_BothOnFullClick()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();
            ActiveZoneButtons result = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.Threshold, 1.0, true, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.SoftPull | ActiveZoneButtons.FullPull, result);
        }

        [TestMethod]
        public void ExclusiveButtons_SoftThenFullThenStaysNoneUntilFullRelease()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();

            ActiveZoneButtons soft = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.ExclusiveButtons, 0.3, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.SoftPull, soft);

            ActiveZoneButtons full = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.ExclusiveButtons, 1.0, true, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.FullPull, full);

            // Dropping back to a partial pull without ever hitting exactly 0 does NOT
            // re-engage Soft - this is the real trigger implementation's behaviour
            // (actionStateMode stays FullPullOnly until axisNorm hits 0.0) and touchpad
            // pressure must match it exactly rather than inventing friendlier semantics.
            ActiveZoneButtons partialAfterFull = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.ExclusiveButtons, 0.3, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.None, partialAfterFull);

            ActiveZoneButtons released = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.ExclusiveButtons, 0.0, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.None, released);

            ActiveZoneButtons softAgain = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.ExclusiveButtons, 0.3, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.SoftPull, softAgain);
        }

        [TestMethod]
        public void HairTrigger_SoftNeverEngagesWithoutPriorFullPull()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();

            ActiveZoneButtons result = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HairTrigger, 0.4, false, false, 0, previousFullPullActive: false, state);
            Assert.AreEqual(ActiveZoneButtons.None, result);
        }

        [TestMethod]
        public void HairTrigger_FullClickThenSoftFollowsOnNextFrame()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();

            ActiveZoneButtons full = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HairTrigger, 1.0, true, false, 0, previousFullPullActive: false, state);
            Assert.AreEqual(ActiveZoneButtons.SoftPull | ActiveZoneButtons.FullPull, full);

            // Caller passes last frame's full-pull-active flag explicitly (the wrapper
            // action tracks this, not the evaluator state object).
            ActiveZoneButtons softFollow = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HairTrigger, 0.4, false, false, 0, previousFullPullActive: true, state);
            Assert.AreEqual(ActiveZoneButtons.SoftPull, softFollow);
        }

        [TestMethod]
        public void HipFire_ArmsThenActivatesBothViaTimer()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();

            // Frame 1: arms the Hip Fire timer, produces no activation yet.
            ActiveZoneButtons arm = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFire, 0.5, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.None, arm);

            Thread.Sleep(5);

            // Frame 2: timer has elapsed past hipFireMs (0), latches outputActive/Both.
            ActiveZoneButtons latch = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFire, 0.5, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.None, latch);

            // Frame 3: now resolves to SoftPull under EngageButtonsMode.Both.
            ActiveZoneButtons resolved = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFire, 0.5, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.SoftPull, resolved);

            // Releasing drops back to None with no stuck state.
            ActiveZoneButtons released = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFire, 0.0, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.None, released);
        }

        [TestMethod]
        public void HipFire_FullClickLatchesFullPullOnlyImmediately()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();

            DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFire, 0.5, false, false, 0, false, state); // arm

            DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFire, 1.0, true, false, 0, false, state); // latch FullPullOnly

            ActiveZoneButtons result = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFire, 1.0, true, false, 0, false, state); // resolve
            Assert.AreEqual(ActiveZoneButtons.FullPull, result);
        }

        [TestMethod]
        public void HipFireExclusiveButtons_FullClickLatchesFullPullOnlyExclusively()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();

            DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFireExclusiveButtons, 1.0, true, false, 0, false, state); // arm+latch (no restart)

            ActiveZoneButtons result = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFireExclusiveButtons, 1.0, true, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.FullPull, result);

            ActiveZoneButtons released = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFireExclusiveButtons, 0.0, false, false, 0, false, state);
            Assert.AreEqual(ActiveZoneButtons.None, released);
        }

        [TestMethod]
        public void HipFireExclusiveButtons_PartialPullLatchesSoftPullOnlyExclusively()
        {
            DualStageEvaluatorState state = new DualStageEvaluatorState();

            DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFireExclusiveButtons, 0.4, false, false, 0, false, state); // arm

            Thread.Sleep(5);

            ActiveZoneButtons result = DualStageEvaluator.ProcessCurrentStage(
                DualStageMode.HipFireExclusiveButtons, 0.4, false, false, 0, false, state); // latch + resolve
            Assert.AreEqual(ActiveZoneButtons.SoftPull, result);
        }
    }
}
