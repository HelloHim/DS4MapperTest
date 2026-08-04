using System;
using DS4MapperTest.Common;
using DS4MapperTest.StickActions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickFlickStickMultiplierCompensationTests
    {
        private const double LATENCY = 0.005;
        private const int FRAMES = 60; // 0.3s total, comfortably covers any FlickTime <= 0.1s

        // Drives a single flick (centre -> pushed diagonally to full deflection, then
        // held) through enough frames to complete the flick warp, and returns the
        // total horizontal mouse delta it produced.
        private static double RunDiagonalFlick(StickDefinition stickDefinition,
            TestMapper mapper, StickFlickStick action)
        {
            int max = stickDefinition.xAxis.max;
            double total = 0.0;

            mapper.SetCurrentLatencyForTest(LATENCY);
            for (int i = 0; i < FRAMES; i++)
            {
                action.Prepare(mapper, max, max);
                if (action.active)
                {
                    double before = mapper.MouseX;
                    action.Event(mapper);
                    total += mapper.MouseX - before;
                }
            }

            return total;
        }

        private static StickFlickStick CreateAction(StickDefinition stickDefinition,
            double realWorldCalibration, double inGameSens,
            FlickSnapAngle snapAngle, double snapStrength, double flickTimeExponent,
            bool multiplierCompensation, double accelerationMultiplier)
        {
            StickFlickStick action = new StickFlickStick(stickDefinition)
            {
                RealWorldCalibration = realWorldCalibration,
                InGameSens = inGameSens,
                SnapAngle = snapAngle,
                SnapStrength = snapStrength,
                FlickTimeExponent = flickTimeExponent,
                MultiplierCompensation = multiplierCompensation,
                AccelerationMultiplier = accelerationMultiplier,
            };

            return action;
        }

        [TestMethod]
        [DataRow(FlickSnapAngle.Off, 0.0, 0.0, DisplayName = "No snap, linear timing")]
        [DataRow(FlickSnapAngle.Ninety, 1.0, 0.0, DisplayName = "Full 90 degree snap, linear timing")]
        [DataRow(FlickSnapAngle.Ninety, 0.5, 0.35, DisplayName = "Partial snap, curved timing")]
        [DataRow(FlickSnapAngle.FortyFive, 1.0, 0.75, DisplayName = "Full 45 degree snap, curved timing")]
        public void MultiplierCompensation_DividesOutputByExactAccelerationMultiplier_RegardlessOfSnapOrExponent(
            FlickSnapAngle snapAngle, double snapStrength, double flickTimeExponent)
        {
            const double accelMultiplier = 2.5;
            TestMapper mapper = new TestMapper();
            StickDefinition stickDefinition = mapper.KnownStickDefinitions["Stick"];

            StickFlickStick uncompensated = CreateAction(stickDefinition, 10.0, 1.0,
                snapAngle, snapStrength, flickTimeExponent,
                multiplierCompensation: false, accelerationMultiplier: 1.0);
            double baseline = RunDiagonalFlick(stickDefinition, mapper, uncompensated);

            StickFlickStick compensated = CreateAction(stickDefinition, 10.0, 1.0,
                snapAngle, snapStrength, flickTimeExponent,
                multiplierCompensation: true, accelerationMultiplier: accelMultiplier);
            double compensatedTotal = RunDiagonalFlick(stickDefinition, mapper, compensated);

            Assert.AreNotEqual(0.0, baseline, 1e-9,
                "Test setup should produce a non-zero flick to begin with.");
            Assert.AreEqual(baseline / accelMultiplier, compensatedTotal, 1e-6,
                "Multiplier Compensation should scale the total flick output by exactly " +
                "1 / AccelerationMultiplier, independent of snap angle, snap strength, " +
                "or the flick time exponent curve.");
        }

        [TestMethod]
        public void MultiplierCompensation_DoesNotChangeFlickDuration()
        {
            // Multiplier Compensation must only scale output magnitude - the flick
            // should still land (finish warping) after the same number of frames
            // whether compensation is on or off, so snap timing feel is unaffected.
            const double accelMultiplier = 4.0;
            TestMapper mapper = new TestMapper();
            StickDefinition stickDefinition = mapper.KnownStickDefinitions["Stick"];
            int max = stickDefinition.xAxis.max;

            StickFlickStick uncompensated = CreateAction(stickDefinition, 10.0, 1.0,
                FlickSnapAngle.Off, 0.0, 0.0,
                multiplierCompensation: false, accelerationMultiplier: 1.0);
            StickFlickStick compensated = CreateAction(stickDefinition, 10.0, 1.0,
                FlickSnapAngle.Off, 0.0, 0.0,
                multiplierCompensation: true, accelerationMultiplier: accelMultiplier);

            mapper.SetCurrentLatencyForTest(LATENCY);
            int framesUntilInactiveUncompensated = -1;
            int framesUntilInactiveCompensated = -1;
            for (int i = 0; i < FRAMES; i++)
            {
                uncompensated.Prepare(mapper, max, max);
                if (!uncompensated.active && framesUntilInactiveUncompensated < 0)
                {
                    framesUntilInactiveUncompensated = i;
                }

                compensated.Prepare(mapper, max, max);
                if (!compensated.active && framesUntilInactiveCompensated < 0)
                {
                    framesUntilInactiveCompensated = i;
                }
            }

            Assert.AreEqual(framesUntilInactiveUncompensated, framesUntilInactiveCompensated,
                "Enabling Multiplier Compensation must not change how many frames the flick takes to complete.");
        }
    }
}
