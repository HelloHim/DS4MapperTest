using DS4MapperTest.StickActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class AnalogEmulationMathTests
    {
        private const double TOL = 0.0001;

        // --- 8-direction (D-Pad Mode) mapping ---------------------------------------

        [TestMethod]
        public void EightWay_Cardinals_HoldSingleDirectionNoPulse()
        {
            AssertBlend(0.0, AnalogEmulationMath.ResolutionMode.EightWay, 0.0);
            AssertBlend(90.0, AnalogEmulationMath.ResolutionMode.EightWay, 0.0);
            AssertBlend(180.0, AnalogEmulationMath.ResolutionMode.EightWay, 0.0);
            AssertBlend(270.0, AnalogEmulationMath.ResolutionMode.EightWay, 0.0);
        }

        [TestMethod]
        public void EightWay_Diagonals_HoldBothDirectionsContinuously()
        {
            AssertBlend(45.0, AnalogEmulationMath.ResolutionMode.EightWay, 1.0);
            AssertBlend(135.0, AnalogEmulationMath.ResolutionMode.EightWay, 1.0);
            AssertBlend(225.0, AnalogEmulationMath.ResolutionMode.EightWay, 1.0);
            AssertBlend(315.0, AnalogEmulationMath.ResolutionMode.EightWay, 1.0);
        }

        [TestMethod]
        public void EightWay_IntermediateAngles_SnapToNearestCardinalOrDiagonal_NeverPulses()
        {
            // Between North and North-east (0-45): closer to North snaps to 0%, closer to
            // North-east snaps to 100%. Never an intermediate blend, unlike 16/32/Continuous.
            AssertBlend(20.0, AnalogEmulationMath.ResolutionMode.EightWay, 0.0);
            AssertBlend(30.0, AnalogEmulationMath.ResolutionMode.EightWay, 1.0);
        }

        [TestMethod]
        public void EightWay_DiagonalZoneWidth_ChangesOnlyEightWaySnapBoundary()
        {
            // At 30°, the default 45° zone is diagonal, 0° permits no diagonals,
            // and 90° makes every non-cardinal angle diagonal.
            AssertBlendWithWidth(30.0, AnalogEmulationMath.ResolutionMode.EightWay, 45, 1.0);
            AssertBlendWithWidth(30.0, AnalogEmulationMath.ResolutionMode.EightWay, 0, 0.0);
            AssertBlendWithWidth(1.0, AnalogEmulationMath.ResolutionMode.EightWay, 90, 1.0);
            AssertBlendWithWidth(30.0, AnalogEmulationMath.ResolutionMode.Sixteen, 0, 0.5);
        }

        // --- 16-direction mapping ------------------------------------------------

        [TestMethod]
        public void Sixteen_North_HoldsUpOnly()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(0.0, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.Up, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.None, secondary);
            Assert.AreEqual(0.0, blend, TOL);
        }

        [TestMethod]
        public void Sixteen_NNE_HoldsUpPulsesRightAtFiftyPercent()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(22.5, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.Up, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.Right, secondary);
            Assert.AreEqual(0.5, blend, TOL);
        }

        [TestMethod]
        public void Sixteen_NorthEast_HoldsUpAndRightContinuously()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(45.0, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(1.0, blend, TOL);
            CollectionAssert.AreEquivalent(
                new[] { AnalogEmulationMath.Direction.Up, AnalogEmulationMath.Direction.Right },
                new[] { primary, secondary });
        }

        [TestMethod]
        public void Sixteen_ENE_HoldsRightPulsesUpAtFiftyPercent()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(67.5, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.Right, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.Up, secondary);
            Assert.AreEqual(0.5, blend, TOL);
        }

        [TestMethod]
        public void Sixteen_East_HoldsRightOnly()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(90.0, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.Right, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.None, secondary);
            Assert.AreEqual(0.0, blend, TOL);
        }

        [TestMethod]
        public void Sixteen_SSE_HoldsDownPulsesRightAtFiftyPercent()
        {
            // Mirrors ENE one quadrant over: South-East quadrant, closer to South.
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(157.5, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.Down, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.Right, secondary);
            Assert.AreEqual(0.5, blend, TOL);
        }

        [TestMethod]
        public void Sixteen_SSW_HoldsDownPulsesLeftAtFiftyPercent()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(202.5, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.Down, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.Left, secondary);
            Assert.AreEqual(0.5, blend, TOL);
        }

        [TestMethod]
        public void Sixteen_WNW_HoldsLeftPulsesUpAtFiftyPercent()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(292.5, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.Left, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.Up, secondary);
            Assert.AreEqual(0.5, blend, TOL);
        }

        [TestMethod]
        public void Sixteen_SouthWest_HoldsDownAndLeftContinuously()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(225.0, AnalogEmulationMath.ResolutionMode.Sixteen,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(1.0, blend, TOL);
            CollectionAssert.AreEquivalent(
                new[] { AnalogEmulationMath.Direction.Down, AnalogEmulationMath.Direction.Left },
                new[] { primary, secondary });
        }

        // --- 32-direction mapping --------------------------------------------------

        [TestMethod]
        public void ThirtyTwo_EastTowardsNorthEast_ProgressesZeroToOneHundredPercent()
        {
            AssertBlend(90.0, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.0);
            AssertBlend(78.75, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.25);
            AssertBlend(67.5, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.5);
            AssertBlend(56.25, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.75);
            AssertBlend(45.0, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 1.0);
        }

        [TestMethod]
        public void ThirtyTwo_NorthEastTowardsNorth_ProgressesOneHundredToZeroPercent()
        {
            AssertBlend(45.0, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 1.0);
            AssertBlend(33.75, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.75);
            AssertBlend(22.5, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.5);
            AssertBlend(11.25, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.25);
            AssertBlend(0.0, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.0);
        }

        [TestMethod]
        public void ThirtyTwo_EastTowardsSouthEast_MatchesSameProgression()
        {
            // East->South-east quadrant interval (Right held, Down pulsed 0% -> 100%).
            AssertBlend(90.0, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.0);
            AssertBlend(101.25, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.25);
            AssertBlend(112.5, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.5);
            AssertBlend(123.75, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 0.75);
            AssertBlend(135.0, AnalogEmulationMath.ResolutionMode.ThirtyTwo, 1.0);
        }

        // --- Continuous-direction mapping ------------------------------------------

        [TestMethod]
        public void Continuous_EastTowardsNorthEast_InterpolatesLinearly()
        {
            AssertBlend(90.0, AnalogEmulationMath.ResolutionMode.Continuous, 0.0);
            AssertBlend(85.5, AnalogEmulationMath.ResolutionMode.Continuous, 0.10);
            AssertBlend(78.75, AnalogEmulationMath.ResolutionMode.Continuous, 0.25);
            AssertBlend(67.5, AnalogEmulationMath.ResolutionMode.Continuous, 0.50);
            AssertBlend(56.25, AnalogEmulationMath.ResolutionMode.Continuous, 0.75);
            AssertBlend(49.5, AnalogEmulationMath.ResolutionMode.Continuous, 0.90);
        }

        [TestMethod]
        public void Continuous_AtNorthEast_BothDirectionsFullyActive()
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(45.0, AnalogEmulationMath.ResolutionMode.Continuous,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(1.0, blend, TOL);
            CollectionAssert.AreEquivalent(
                new[] { AnalogEmulationMath.Direction.Up, AnalogEmulationMath.Direction.Right },
                new[] { primary, secondary });
        }

        [TestMethod]
        public void Continuous_DeadCenter_NoDirection()
        {
            AnalogEmulationMath.ComputeDirectionBlend(0.0, 0.0, AnalogEmulationMath.ResolutionMode.Continuous,
                out var primary, out var secondary, out double blend);

            Assert.AreEqual(AnalogEmulationMath.Direction.None, primary);
            Assert.AreEqual(AnalogEmulationMath.Direction.None, secondary);
            Assert.AreEqual(0.0, blend, TOL);
        }

        private static void AssertBlend(double angle, AnalogEmulationMath.ResolutionMode mode, double expectedBlend)
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(angle, mode,
                out _, out _, out double blend);
            Assert.AreEqual(expectedBlend, blend, TOL, $"angle={angle}");
        }

        private static void AssertBlendWithWidth(double angle, AnalogEmulationMath.ResolutionMode mode,
            int width, double expectedBlend)
        {
            AnalogEmulationMath.ComputeDirectionBlendFromAngle(angle, mode, width,
                out _, out _, out double blend);
            Assert.AreEqual(expectedBlend, blend, TOL, $"angle={angle}, width={width}");
        }

        // --- Direction pulse timing (ComputeDutyGate) ------------------------------

        [TestMethod]
        public void DutyGate_ZeroDuty_NeverActive()
        {
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(0.0, 30.0, 0.0));
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(29.0, 30.0, 0.0));
        }

        [TestMethod]
        public void DutyGate_FullDuty_AlwaysActive()
        {
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(0.0, 30.0, 1.0));
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(29.0, 30.0, 1.0));
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(1000.0, 30.0, 1.0));
        }

        [TestMethod]
        public void DutyGate_FiftyPercentDuty_ActiveFirstHalfOfCycle()
        {
            // 30ms cycle, 50% duty => 15ms ON, 15ms OFF.
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(0.0, 30.0, 0.5));
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(14.9, 30.0, 0.5));
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(15.0, 30.0, 0.5));
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(29.9, 30.0, 0.5));
        }

        [TestMethod]
        public void DutyGate_TwentyFivePercentDuty_ActiveForQuarterOfCycle()
        {
            // 30ms cycle, 25% duty => 7.5ms ON, 22.5ms OFF.
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(0.0, 30.0, 0.25));
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(7.4, 30.0, 0.25));
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(7.5, 30.0, 0.25));
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(20.0, 30.0, 0.25));
        }

        [TestMethod]
        public void DutyGate_SeventyFivePercentDuty_ActiveForThreeQuartersOfCycle()
        {
            // 30ms cycle, 75% duty => 22.5ms ON, 7.5ms OFF.
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(0.0, 30.0, 0.75));
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(22.4, 30.0, 0.75));
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(22.5, 30.0, 0.75));
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(29.9, 30.0, 0.75));
        }

        [TestMethod]
        public void DutyGate_WrapsAcrossMultipleCycles()
        {
            // 30ms cycle, 50% duty; phase 45ms is 15ms into the second cycle => still OFF half.
            Assert.IsFalse(AnalogEmulationMath.ComputeDutyGate(45.0, 30.0, 0.5));
            // Phase 60ms wraps back to the start of the third cycle => ON half.
            Assert.IsTrue(AnalogEmulationMath.ComputeDutyGate(60.0, 30.0, 0.5));
        }

        // --- Speed active-percentage calculation -----------------------------------

        [TestMethod]
        public void SpeedActive_ZeroRadius_ProducesNoOutput()
        {
            Assert.AreEqual(0.0, AnalogEmulationMath.ComputeSpeedActive(0.0, 0.15, 0.80), TOL);
        }

        [TestMethod]
        public void SpeedActive_JustOutsideDeadzone_StartsNearConfiguredActivePercent()
        {
            double result = AnalogEmulationMath.ComputeSpeedActive(0.001, 0.15, 0.80);
            Assert.IsTrue(result > 0.15 && result < 0.17, $"Expected near 15%, got {result}");
        }

        [TestMethod]
        public void SpeedActive_ScalesLinearlyBetweenDeadzoneAndThreshold()
        {
            // r = f/2 => progress 0.5 => speedActive = a + (1-a)*0.5
            double result = AnalogEmulationMath.ComputeSpeedActive(0.40, 0.15, 0.80);
            Assert.AreEqual(0.15 + (0.85 * 0.5), result, TOL);
        }

        [TestMethod]
        public void SpeedActive_AtFullSpeedThreshold_ReachesOneHundredPercent()
        {
            Assert.AreEqual(1.0, AnalogEmulationMath.ComputeSpeedActive(0.80, 0.15, 0.80), TOL);
        }

        [TestMethod]
        public void SpeedActive_AboveFullSpeedThreshold_RemainsAtOneHundredPercent()
        {
            Assert.AreEqual(1.0, AnalogEmulationMath.ComputeSpeedActive(0.95, 0.15, 0.80), TOL);
            Assert.AreEqual(1.0, AnalogEmulationMath.ComputeSpeedActive(1.5, 0.15, 0.80), TOL);
        }

        [TestMethod]
        public void SpeedActive_InvalidInputs_ClampSafely()
        {
            // Negative radius clamps to 0 => no output, regardless of out-of-range percentages.
            Assert.AreEqual(0.0, AnalogEmulationMath.ComputeSpeedActive(-5.0, 2.0, -1.0), TOL);
            // A radius above an invalid (zero/negative) threshold still resolves to full speed, never NaN.
            double result = AnalogEmulationMath.ComputeSpeedActive(0.5, 0.15, 0.0);
            Assert.IsFalse(double.IsNaN(result));
            Assert.AreEqual(1.0, result, TOL);
        }
    }
}
