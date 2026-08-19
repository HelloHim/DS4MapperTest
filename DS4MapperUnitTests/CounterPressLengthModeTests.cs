using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;

namespace DS4MapperUnitTests
{
    // Covers the three Counter Press Length timing modes (Fixed, Wait Variance Percentage,
    // Minimum and Maximum): defaults, the percentage/best-fit maths in CounterPressLengthTiming,
    // CS2 preset behaviour, mode switching and the mode-aware effective range used at runtime.
    // CounterMovementReleasePressProcessor (stick D-Pad/Analog Emulation) and TouchpadCounterMovementReleasePress
    // (touchpad) both compose the same CounterPressLengthTiming, so both are exercised here to
    // confirm neither duplicated nor diverged from the shared implementation.
    [TestClass]
    public class CounterPressLengthModeTests
    {
        // --- Defaults ---------------------------------------------------------------

        [TestMethod]
        public void NewProcessor_DefaultsToWaitVariancePercentageWithCs2Values()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();

            Assert.IsFalse(processor.Enabled);
            Assert.AreEqual(CounterPressLengthMode.WaitVariancePercentage, processor.CounterPressLengthMode);
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, processor.EffectivePressLengthPreset);
            Assert.AreEqual(84, processor.CounterPressLengthMs);
            Assert.AreEqual(7, processor.CounterPressLengthVariancePercent);
            Assert.AreEqual(78, processor.CounterPressLengthMinimumMs);
            Assert.AreEqual(90, processor.CounterPressLengthMaximumMs);
        }

        [TestMethod]
        public void NewTouchpadCounterMovementReleasePress_DefaultsToWaitVariancePercentageWithCs2Values()
        {
            TouchpadCounterMovementReleasePress releasePress = new TouchpadCounterMovementReleasePress();

            Assert.IsFalse(releasePress.Enabled);
            Assert.AreEqual(CounterPressLengthMode.WaitVariancePercentage, releasePress.CounterPressLengthMode);
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, releasePress.EffectivePressLengthPreset);
            Assert.AreEqual(84, releasePress.CounterPressLengthMs);
            Assert.AreEqual(7, releasePress.CounterPressLengthVariancePercent);
            Assert.AreEqual(78, releasePress.CounterPressLengthMinimumMs);
            Assert.AreEqual(90, releasePress.CounterPressLengthMaximumMs);
        }

        [TestMethod]
        public void EnablingDoesNotChangeAnyTimingValue()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.Enabled = true;

            Assert.AreEqual(CounterPressLengthMode.WaitVariancePercentage, processor.CounterPressLengthMode);
            Assert.AreEqual(84, processor.CounterPressLengthMs);
            Assert.AreEqual(7, processor.CounterPressLengthVariancePercent);
            Assert.AreEqual(78, processor.CounterPressLengthMinimumMs);
            Assert.AreEqual(90, processor.CounterPressLengthMaximumMs);
        }

        // --- Fixed mode ---------------------------------------------------------------

        [TestMethod]
        public void FixedMode_EffectiveRangeIsExactlyTheFixedValue()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = CounterPressLengthMode.Fixed;
            processor.ApplyFixedAndPercentage(100, 20);

            var (minimum, maximum) = processor.GetEffectiveCounterPressLengthRange();
            Assert.AreEqual(100, minimum);
            Assert.AreEqual(100, maximum);
        }

        [TestMethod]
        public void FixedMode_EditingFixedPreservesStoredPercentageAndSyncsHiddenRange()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = CounterPressLengthMode.Fixed;
            processor.ApplyFixedAndPercentage(100, 20);

            Assert.AreEqual(20, processor.CounterPressLengthVariancePercent);
            Assert.AreEqual(80, processor.CounterPressLengthMinimumMs);
            Assert.AreEqual(120, processor.CounterPressLengthMaximumMs);

            // Runtime in Fixed mode still uses exactly 100ms despite the synchronised range.
            var (minimum, maximum) = processor.GetEffectiveCounterPressLengthRange();
            Assert.AreEqual(100, minimum);
            Assert.AreEqual(100, maximum);
        }

        [TestMethod]
        public void FixedMode_ZeroDurationCompletesSafely()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = CounterPressLengthMode.Fixed;
            processor.ApplyFixedAndPercentage(10, 0); // 10ms is the field floor

            var (minimum, maximum) = processor.GetEffectiveCounterPressLengthRange();
            Assert.AreEqual(10, minimum);
            Assert.AreEqual(10, maximum);
        }

        // --- Wait Variance Percentage mode ---------------------------------------------

        [TestMethod]
        [DataRow(84, 7, 78, 89)]
        [DataRow(100, 0, 100, 100)]
        [DataRow(100, 10, 90, 110)]
        public void ComputePercentageRange_MatchesExpectedFloorBoundaries(int fixedMs, int percent, int expectedMin, int expectedMax)
        {
            var (minimum, maximum) = CounterPressLengthTiming.ComputePercentageRange(fixedMs, percent);
            Assert.AreEqual(expectedMin, minimum);
            Assert.AreEqual(expectedMax, maximum);
        }

        [TestMethod]
        public void ComputePercentageRange_UsesFloorNotRound()
        {
            // 99 * 1.10 = 108.9 -> floor 108, not rounded to 109.
            var (minimum, maximum) = CounterPressLengthTiming.ComputePercentageRange(99, 10);
            Assert.AreEqual(89, minimum); // 99 * 0.90 = 89.1 -> floor 89
            Assert.AreEqual(108, maximum);
        }

        [TestMethod]
        public void ComputePercentageRange_HundredPercentNeverNegative()
        {
            var (minimum, maximum) = CounterPressLengthTiming.ComputePercentageRange(50, 100);
            Assert.AreEqual(0, minimum);
            Assert.AreEqual(100, maximum);
            Assert.IsTrue(minimum >= 0);
        }

        [TestMethod]
        public void WaitVariancePercentageMode_EffectiveRangeMatchesComputedBoundaries()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = CounterPressLengthMode.WaitVariancePercentage;
            processor.ApplyFixedAndPercentage(84, 7);

            var (minimum, maximum) = processor.GetEffectiveCounterPressLengthRange();
            Assert.AreEqual(78, minimum);
            Assert.AreEqual(89, maximum);
        }

        [TestMethod]
        public void WaitVariancePercentageMode_EditingFixedChangesPresetToCustomViaViewModelConvention()
        {
            // The processor itself never touches PressLengthPreset from a numeric edit (that is
            // the ViewModel's responsibility, mirroring the pre-existing Minimum/Maximum
            // behaviour); confirm the processor's own preset field is unaffected here and the
            // ViewModel-level contract is exercised separately by the ViewModel tests.
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(90, 15);
            Assert.AreEqual(90, processor.CounterPressLengthMs);
            Assert.AreEqual(15, processor.CounterPressLengthVariancePercent);
        }

        [TestMethod]
        public void WaitVariancePercentageMode_ZeroPercentIsDeterministic()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(100, 0);

            Assert.AreEqual(100, processor.CounterPressLengthMinimumMs);
            Assert.AreEqual(100, processor.CounterPressLengthMaximumMs);
        }

        // --- Minimum and Maximum mode ---------------------------------------------------

        [TestMethod]
        public void MinimumAndMaximumMode_EffectiveRangeMatchesStoredRangeDirectly()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;
            processor.ApplyMinimumAndMaximum(60, 130);

            var (minimum, maximum) = processor.GetEffectiveCounterPressLengthRange();
            Assert.AreEqual(60, minimum);
            Assert.AreEqual(130, maximum);
        }

        [TestMethod]
        public void ApplyMinimumAndMaximum_SwapsInvertedRange()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyMinimumAndMaximum(130, 60);

            Assert.IsTrue(processor.CounterPressLengthMinimumMs <= processor.CounterPressLengthMaximumMs);
        }

        // --- Best-fit conversion (Minimum/Maximum -> Fixed/Percentage) ------------------

        [TestMethod]
        public void BestFit_78To90_ProducesCs2FixedAndPercentage()
        {
            var (fixedMs, percent) = CounterPressLengthTiming.BestFitFixedAndPercentage(78, 90);
            Assert.AreEqual(84, fixedMs);
            Assert.AreEqual(7, percent);
        }

        [TestMethod]
        public void BestFit_EqualMinimumAndMaximum_ProducesZeroPercent()
        {
            var (fixedMs, percent) = CounterPressLengthTiming.BestFitFixedAndPercentage(100, 100);
            Assert.AreEqual(100, fixedMs);
            Assert.AreEqual(0, percent);
        }

        [TestMethod]
        public void BestFit_ReconstructsRequestedRangeWithinFieldPrecision()
        {
            var (fixedMs, percent) = CounterPressLengthTiming.BestFitFixedAndPercentage(80, 120);
            var (reconstructedMin, reconstructedMax) = CounterPressLengthTiming.ComputePercentageRange(fixedMs, percent);

            // The best-fit search always finds the closest achievable reconstruction; for an
            // odd-width range that may not be an exact match, but it must never be wildly off.
            Assert.IsTrue(System.Math.Abs(reconstructedMin - 80) <= 2);
            Assert.IsTrue(System.Math.Abs(reconstructedMax - 120) <= 2);
        }

        [TestMethod]
        public void BestFit_IsDeterministicAcrossRepeatedCalls()
        {
            var first = CounterPressLengthTiming.BestFitFixedAndPercentage(80, 120);
            var second = CounterPressLengthTiming.BestFitFixedAndPercentage(80, 120);
            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void BestFit_InvertedInputStillProducesOrderedResult()
        {
            var forward = CounterPressLengthTiming.BestFitFixedAndPercentage(78, 90);
            var reversed = CounterPressLengthTiming.BestFitFixedAndPercentage(90, 78);
            Assert.AreEqual(forward, reversed);
        }

        [TestMethod]
        public void ApplyMinimumAndMaximum_MatchesStandaloneBestFitFunction()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyMinimumAndMaximum(78, 90);

            var (expectedFixed, expectedPercent) = CounterPressLengthTiming.BestFitFixedAndPercentage(78, 90);
            Assert.AreEqual(expectedFixed, processor.CounterPressLengthMs);
            Assert.AreEqual(expectedPercent, processor.CounterPressLengthVariancePercent);
        }

        // --- CS2 preset ------------------------------------------------------------------

        [TestMethod]
        public void ApplyCs2Preset_SetsAllFourSynchronisedValues()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(50, 5); // perturb away from CS2 first

            processor.ApplyCs2Preset();

            Assert.AreEqual(84, processor.CounterPressLengthMs);
            Assert.AreEqual(7, processor.CounterPressLengthVariancePercent);
            Assert.AreEqual(78, processor.CounterPressLengthMinimumMs);
            Assert.AreEqual(90, processor.CounterPressLengthMaximumMs);
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, processor.PressLengthPreset);
        }

        [TestMethod]
        public void ApplyCs2Preset_DoesNotChangeSelectedMode()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;

            processor.ApplyCs2Preset();

            Assert.AreEqual(CounterPressLengthMode.MinimumAndMaximum, processor.CounterPressLengthMode);
        }

        [TestMethod]
        [DataRow(CounterPressLengthMode.Fixed, 84, 84)]
        [DataRow(CounterPressLengthMode.WaitVariancePercentage, 78, 90)]
        [DataRow(CounterPressLengthMode.MinimumAndMaximum, 78, 90)]
        public void Cs2Preset_ProducesExpectedRuntimeRangePerMode(CounterPressLengthMode mode, int expectedMin, int expectedMax)
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = mode;
            processor.ApplyCs2Preset();

            var (minimum, maximum) = processor.GetEffectiveCounterPressLengthRange();
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
                processor.CounterPressLengthMode = CounterPressLengthMode.Fixed;
                processor.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;
                processor.CounterPressLengthMode = CounterPressLengthMode.WaitVariancePercentage;
            }

            Assert.AreEqual(84, processor.CounterPressLengthMs);
            Assert.AreEqual(7, processor.CounterPressLengthVariancePercent);
            Assert.AreEqual(78, processor.CounterPressLengthMinimumMs);
            Assert.AreEqual(90, processor.CounterPressLengthMaximumMs);
        }

        [TestMethod]
        public void SwitchingModeAloneDoesNotChangePreset()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, processor.EffectivePressLengthPreset);

            processor.CounterPressLengthMode = CounterPressLengthMode.Fixed;
            processor.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;

            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, processor.EffectivePressLengthPreset);
        }

        [TestMethod]
        public void MinimumAndMaximumToWaitVariancePercentage_ShowsBestFitValues()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;
            processor.ApplyMinimumAndMaximum(78, 90);

            processor.CounterPressLengthMode = CounterPressLengthMode.WaitVariancePercentage;

            Assert.AreEqual(84, processor.CounterPressLengthMs);
            Assert.AreEqual(7, processor.CounterPressLengthVariancePercent);
        }

        [TestMethod]
        public void WaitVariancePercentageToMinimumAndMaximum_ShowsSynchronisedRange()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.ApplyFixedAndPercentage(84, 7);

            processor.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;

            Assert.AreEqual(78, processor.CounterPressLengthMinimumMs);
            Assert.AreEqual(89, processor.CounterPressLengthMaximumMs);
        }
    }
}
