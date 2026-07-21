using System;

namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Pure, deterministic calculations backing the Analog Emulation stick mode. Kept free of
    /// Mapper/AxisDirButton dependencies so timing and direction-blend behaviour can be unit
    /// tested without a real controller, report loop, or wall-clock delay.
    /// </summary>
    public static class AnalogEmulationMath
    {
        public enum ResolutionMode
        {
            Sixteen,
            ThirtyTwo,
            Continuous,
        }

        public enum Direction
        {
            None,
            Up,
            Down,
            Left,
            Right,
        }

        /// <summary>
        /// Resolves a post-deadzone stick vector into a primary (always continuously held)
        /// direction, an optional secondary (pulsed) direction, and the secondary's duty cycle
        /// (0.0-1.0) within the current Direction Resolution mode.
        /// </summary>
        public static void ComputeDirectionBlend(double xNorm, double yNorm, ResolutionMode mode,
            out Direction primary, out Direction secondary, out double secondaryBlend)
        {
            if (xNorm == 0.0 && yNorm == 0.0)
            {
                primary = Direction.None;
                secondary = Direction.None;
                secondaryBlend = 0.0;
                return;
            }

            // Matches StickPadAction.DetermineDirection's angle convention: 0 deg = Up, increasing clockwise.
            double angleRad = Math.Atan2(xNorm, yNorm);
            double angle = (angleRad >= 0 ? angleRad : (2 * Math.PI + angleRad)) * 180.0 / Math.PI;

            ComputeDirectionBlendFromAngle(angle, mode, out primary, out secondary, out secondaryBlend);
        }

        /// <summary>
        /// Angle-driven core of ComputeDirectionBlend, exposed directly so tests can target exact
        /// anchor and intermediate angles without reconstructing xNorm/yNorm vectors.
        /// </summary>
        public static void ComputeDirectionBlendFromAngle(double angleDeg, ResolutionMode mode,
            out Direction primary, out Direction secondary, out double secondaryBlend)
        {
            double angle = angleDeg % 360.0;
            if (angle < 0) angle += 360.0;

            Direction[] cardinals = { Direction.Up, Direction.Right, Direction.Down, Direction.Left };

            int quadrant = (int)(angle / 90.0);
            if (quadrant > 3) quadrant = 3;
            double angleInQuadrant = angle - (quadrant * 90.0);

            Direction a = cardinals[quadrant];
            Direction b = cardinals[(quadrant + 1) % 4];

            double rawBlend;
            if (angleInQuadrant <= 45.0)
            {
                primary = a;
                secondary = b;
                rawBlend = angleInQuadrant / 45.0;
            }
            else
            {
                primary = b;
                secondary = a;
                rawBlend = (90.0 - angleInQuadrant) / 45.0;
            }

            rawBlend = Math.Clamp(rawBlend, 0.0, 1.0);

            switch (mode)
            {
                case ResolutionMode.Sixteen:
                    secondaryBlend = Math.Round(rawBlend * 2.0, MidpointRounding.AwayFromZero) / 2.0;
                    break;
                case ResolutionMode.ThirtyTwo:
                    secondaryBlend = Math.Round(rawBlend * 4.0, MidpointRounding.AwayFromZero) / 4.0;
                    break;
                case ResolutionMode.Continuous:
                default:
                    secondaryBlend = rawBlend;
                    break;
            }

            if (secondaryBlend <= 0.0)
            {
                secondary = Direction.None;
                secondaryBlend = 0.0;
            }
        }

        /// <summary>
        /// Evaluates a repeating ON/OFF duty cycle at a given running phase (ms), where duty is
        /// the fraction (0.0-1.0) of each cycleTimeMs period that should read as ON. phaseMs is a
        /// monotonically-increasing accumulator supplied by the caller (never wall-clock/sleep
        /// based) so behaviour is fully deterministic under test.
        /// </summary>
        public static bool ComputeDutyGate(double phaseMs, double cycleTimeMs, double duty)
        {
            if (duty <= 0.0) return false;
            if (duty >= 1.0) return true;
            if (cycleTimeMs <= 0.0) return true;

            double onTime = cycleTimeMs * duty;
            double wrapped = phaseMs % cycleTimeMs;
            if (wrapped < 0) wrapped += cycleTimeMs;
            return wrapped < onTime;
        }

        /// <summary>
        /// Linear speed-emulation active percentage per spec section 18: 0 at/below the deadzone,
        /// ramping from activePercent up to 1.0 as radius approaches fullSpeedThreshold, then
        /// pinned at 1.0 beyond it.
        /// </summary>
        public static double ComputeSpeedActive(double radius, double activePercent, double fullSpeedThreshold)
        {
            double r = Math.Clamp(radius, 0.0, 1.0);
            double a = Math.Clamp(activePercent, 0.0, 1.0);
            double f = Math.Clamp(fullSpeedThreshold, 0.0001, 1.0);

            double speedActive;
            if (r <= 0.0)
            {
                speedActive = 0.0;
            }
            else if (r >= f)
            {
                speedActive = 1.0;
            }
            else
            {
                double progress = r / f;
                speedActive = a + ((1.0 - a) * progress);
            }

            return Math.Clamp(speedActive, 0.0, 1.0);
        }
    }
}
