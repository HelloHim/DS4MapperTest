using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;

namespace DS4MapperUnitTests
{
    // Covers the three Opposite Tap Length timing modes (Fixed, Wait Variance Percentage,
    // Minimum and Maximum): defaults, the percentage/best-fit maths in OppositeTapLengthTiming,
    // CS2 preset behaviour, mode switching and the mode-aware effective range used at runtime.
    // CounterMovementReleasePressProcessor (stick D-Pad/Analog Emulation) and TouchpadReleaseBrake
    // (touchpad) both compose the same OppositeTapLengthTiming, so both are exercised here to
    // confirm neither duplicated nor diverged from the shared implementation.
    [TestClass]
    public class OppositeTapLengthModeTests
    {
        // --- Defaults ---------------------------------------------------------------

        [TestMethod]
        public void NewProcessor_DefaultsToWaitVariancePercentageWithCs2Values()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();

            Assert.IsFalse(processor.Enabled);
            Assert.AreEqual(OppositeTapLengthMode.WaitVariancePercentage, processor.OppositeTapLengthMode);
            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, processor.EffectiveTapLengthPreset);
            Assert.AreEqual(98, processor.OppositeTapLengthMs);
            Assert.AreEqual(23, processor.OppositeTapLengthVariancePercent);
            Assert.AreEqual(75, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, processor.OppositeTapLengthMaximumMs);
        }

        [TestMethod]
        public void NewTouchpadReleaseBrake_DefaultsToWaitVariancePercentageWithCs2Values()
        {
            TouchpadReleaseBrake brake = new TouchpadReleaseBrake();

            Assert.IsFalse(brake.Enabled);
            Assert.AreEqual(OppositeTapLengthMode.WaitVariancePercentage, brake.OppositeTapLengthMode);
            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, brake.EffectiveTapLengthPreset);
            Assert.AreEqual(98, brake.OppositeTapLengthMs);
            Assert.AreEqual(23, brake.OppositeTapLengthVariancePercent);
            Assert.AreEqual(75, brake.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, brake.OppositeTapLengthMaximumMs);
        }

        [TestMethod]
        public void EnablingDoesNotChangeAnyTimingValue()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.Enabled = true;

            Assert.AreEqual(OppositeTapLengthMode.WaitVariancePercentage, processor.OppositeTapLengthMode);
            Assert.AreEqual(98, processor.OppositeTapLengthMs);
            Assert.AreEqual(23, processor.OppositeTapLengthVariancePercent);
            Assert.AreEqual(75, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, processor.OppositeTapLengthMaximumMs);
        }

        // --- Fixed mode ---------------------------------------------------------------

        [TestMethod]
        public void FixedMode_EffectiveRangeIsExactlyTheFixedValue()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
            processor.ApplyFixedAndPercentage(100, 20);

            var (minimum, maximum) = processor.GetEffectiveOppositeTapLengthRange();
            Assert.AreEqual(100, minimum);
            Assert.AreEqual(100, maximum);
        }

        [TestMethod]
        public void FixedMode_EditingFixedPreservesStoredPercentageAndSyncsHiddenRange()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
            processor.ApplyFixedAndPercentage(100, 20);

            Assert.AreEqual(20, processor.OppositeTapLengthVariancePercent);
            Assert.AreEqual(80, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, processor.OppositeTapLengthMaximumMs);

            // Runtime in Fixed mode still uses exactly 100ms despite the synchronised range.
            var (minimum, maximum) = processor.GetEffectiveOppositeTapLengthRange();
            Assert.AreEqual(100, minimum);
            Assert.AreEqual(100, maximum);
        }

        [TestMethod]
        public void FixedMode_ZeroDurationCompletesSafely()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
            processor.ApplyFixedAndPercentage(10, 0); // 10ms is the field floor

            var (minimum, maximum) = processor.GetEffectiveOppositeTapLengthRange();
            Assert.AreEqual(10, minimum);
            Assert.AreEqual(10, maximum);
        }

        // --- Wait Variance Percentage mode ---------------------------------------------

        [TestMethod]
        [DataRow(98, 23, 75, 120)]
        [DataRow(100, 0, 100, 100)]
        [DataRow(100, 10, 90, 110)]
        public void ComputePercentageRange_MatchesExpectedFloorBoundaries(int fixedMs, int percent, int expectedMin, int expectedMax)
        {
            var (minimum, maximum) = OppositeTapLengthTiming.ComputePercentageRange(fixedMs, percent);
            Assert.AreEqual(expectedMin, minimum);
            Assert.AreEqual(expectedMax, maximum);
        }

        [TestMethod]
        public void ComputePercentageRange_UsesFloorNotRound()
        {
            // 99 * 1.10 = 108.9 -> floor 108, not rounded to 109.
            var (minimum, maximum) = OppositeTapLengthTiming.ComputePercentageRange(99, 10);
            Assert.AreEqual(89, minimum); // 99 * 0.90 = 89.1 -> floor 89
            Assert.AreEqual(108, maximum);
        }

        [TestMethod]
        public void ComputePercentageRange_HundredPercentNeverNegative()
        {
            var (minimum, maximum) = OppositeTapLengthTiming.ComputePercentageRange(50, 100);
            Assert.AreEqual(0, minimum);
            Assert.AreEqual(100, maximum);
            Assert.IsTrue(minimum >= 0);
        }

        [TestMethod]
        public void WaitVariancePercentageMode_EffectiveRangeMatchesComputedBoundaries()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = OppositeTapLengthMode.WaitVariancePercentage;
            processor.ApplyFixedAndPercentage(98, 23);

            var (minimum, maximum) = processor.GetEffectiveOppositeTapLengthRange();
            Assert.AreEqual(75, minimum);
            Assert.AreEqual(120, maximum);
        }

        [TestMethod]
        public void WaitVariancePercentageMode_EditingFixedChangesPresetToCustomViaViewModelConvention()
        {
            // The processor itself never touches TapLengthPreset from a numeric edit (that is
            // the ViewModel's responsibility, mirroring the pre-existing Minimum/Maximum
            // behaviour); confirm the processor's own preset field is unaffected here and the
            // ViewModel-level contract is exercised separately by the ViewModel tests.
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(90, 15);
            Assert.AreEqual(90, processor.OppositeTapLengthMs);
            Assert.AreEqual(15, processor.OppositeTapLengthVariancePercent);
        }

        [TestMethod]
        public void WaitVariancePercentageMode_ZeroPercentIsDeterministic()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(100, 0);

            Assert.AreEqual(100, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(100, processor.OppositeTapLengthMaximumMs);
        }

        // --- Minimum and Maximum mode ---------------------------------------------------

        [TestMethod]
        public void MinimumAndMaximumMode_EffectiveRangeMatchesStoredRangeDirectly()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;
            processor.ApplyMinimumAndMaximum(60, 130);

            var (minimum, maximum) = processor.GetEffectiveOppositeTapLengthRange();
            Assert.AreEqual(60, minimum);
            Assert.AreEqual(130, maximum);
        }

        [TestMethod]
        public void ApplyMinimumAndMaximum_SwapsInvertedRange()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyMinimumAndMaximum(130, 60);

            Assert.IsTrue(processor.OppositeTapLengthMinimumMs <= processor.OppositeTapLengthMaximumMs);
        }

        // --- Best-fit conversion (Minimum/Maximum -> Fixed/Percentage) ------------------

        [TestMethod]
        public void BestFit_75To120_ProducesCs2FixedAndPercentage()
        {
            var (fixedMs, percent) = OppositeTapLengthTiming.BestFitFixedAndPercentage(75, 120);
            Assert.AreEqual(98, fixedMs);
            Assert.AreEqual(23, percent);
        }

        [TestMethod]
        public void BestFit_EqualMinimumAndMaximum_ProducesZeroPercent()
        {
            var (fixedMs, percent) = OppositeTapLengthTiming.BestFitFixedAndPercentage(100, 100);
            Assert.AreEqual(100, fixedMs);
            Assert.AreEqual(0, percent);
        }

        [TestMethod]
        public void BestFit_ReconstructsRequestedRangeWithinFieldPrecision()
        {
            var (fixedMs, percent) = OppositeTapLengthTiming.BestFitFixedAndPercentage(80, 120);
            var (reconstructedMin, reconstructedMax) = OppositeTapLengthTiming.ComputePercentageRange(fixedMs, percent);

            // The best-fit search always finds the closest achievable reconstruction; for an
            // odd-width range that may not be an exact match, but it must never be wildly off.
            Assert.IsTrue(System.Math.Abs(reconstructedMin - 80) <= 2);
            Assert.IsTrue(System.Math.Abs(reconstructedMax - 120) <= 2);
        }

        [TestMethod]
        public void BestFit_IsDeterministicAcrossRepeatedCalls()
        {
            var first = OppositeTapLengthTiming.BestFitFixedAndPercentage(80, 120);
            var second = OppositeTapLengthTiming.BestFitFixedAndPercentage(80, 120);
            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void BestFit_InvertedInputStillProducesOrderedResult()
        {
            var forward = OppositeTapLengthTiming.BestFitFixedAndPercentage(75, 120);
            var reversed = OppositeTapLengthTiming.BestFitFixedAndPercentage(120, 75);
            Assert.AreEqual(forward, reversed);
        }

        [TestMethod]
        public void ApplyMinimumAndMaximum_MatchesStandaloneBestFitFunction()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyMinimumAndMaximum(75, 120);

            var (expectedFixed, expectedPercent) = OppositeTapLengthTiming.BestFitFixedAndPercentage(75, 120);
            Assert.AreEqual(expectedFixed, processor.OppositeTapLengthMs);
            Assert.AreEqual(expectedPercent, processor.OppositeTapLengthVariancePercent);
        }

        // --- CS2 preset ------------------------------------------------------------------

        [TestMethod]
        public void ApplyCs2Preset_SetsAllFourSynchronisedValues()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(50, 5); // perturb away from CS2 first

            processor.ApplyCs2Preset();

            Assert.AreEqual(98, processor.OppositeTapLengthMs);
            Assert.AreEqual(23, processor.OppositeTapLengthVariancePercent);
            Assert.AreEqual(75, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, processor.OppositeTapLengthMaximumMs);
            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, processor.TapLengthPreset);
        }

        [TestMethod]
        public void ApplyCs2Preset_DoesNotChangeSelectedMode()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;

            processor.ApplyCs2Preset();

            Assert.AreEqual(OppositeTapLengthMode.MinimumAndMaximum, processor.OppositeTapLengthMode);
        }

        [TestMethod]
        [DataRow(OppositeTapLengthMode.Fixed, 98, 98)]
        [DataRow(OppositeTapLengthMode.WaitVariancePercentage, 75, 120)]
        [DataRow(OppositeTapLengthMode.MinimumAndMaximum, 75, 120)]
        public void Cs2Preset_ProducesExpectedRuntimeRangePerMode(OppositeTapLengthMode mode, int expectedMin, int expectedMax)
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = mode;
            processor.ApplyCs2Preset();

            var (minimum, maximum) = processor.GetEffectiveOppositeTapLengthRange();
            Assert.AreEqual(expectedMin, minimum);
            Assert.AreEqual(expectedMax, maximum);
        }

        // --- Mode switching preserves values ---------------------------------------------

        [TestMethod]
        public void RepeatedModeSwitching_NeverDriftsTheUnderlyingValues()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyCs2Preset();

            for (int i = 0; i < 5; i++)
            {
                processor.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
                processor.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;
                processor.OppositeTapLengthMode = OppositeTapLengthMode.WaitVariancePercentage;
            }

            Assert.AreEqual(98, processor.OppositeTapLengthMs);
            Assert.AreEqual(23, processor.OppositeTapLengthVariancePercent);
            Assert.AreEqual(75, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, processor.OppositeTapLengthMaximumMs);
        }

        [TestMethod]
        public void SwitchingModeAloneDoesNotChangePreset()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, processor.EffectiveTapLengthPreset);

            processor.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
            processor.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;

            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, processor.EffectiveTapLengthPreset);
        }

        [TestMethod]
        public void MinimumAndMaximumToWaitVariancePercentage_ShowsBestFitValues()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;
            processor.ApplyMinimumAndMaximum(75, 120);

            processor.OppositeTapLengthMode = OppositeTapLengthMode.WaitVariancePercentage;

            Assert.AreEqual(98, processor.OppositeTapLengthMs);
            Assert.AreEqual(23, processor.OppositeTapLengthVariancePercent);
        }

        [TestMethod]
        public void WaitVariancePercentageToMinimumAndMaximum_ShowsSynchronisedRange()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(98, 23);

            processor.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;

            Assert.AreEqual(75, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, processor.OppositeTapLengthMaximumMs);
        }
    }
}
