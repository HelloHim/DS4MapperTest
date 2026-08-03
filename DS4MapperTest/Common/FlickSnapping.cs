using System;

namespace DS4MapperTest.Common
{
    /// <summary>
    /// Snap Angle choices for flick stick, expressed as the angle between adjacent
    /// snap directions. Mirrors JoyShockMapper's FLICK_SNAP_MODE values NONE, FOUR
    /// and EIGHT.
    /// </summary>
    public enum FlickSnapAngle
    {
        Off,
        Ninety,
        FortyFive,
    }

    /// <summary>
    /// Biases the target angle of a new flick toward the nearest of a fixed set of
    /// directions. Kept free of Mapper dependencies so the angle maths can be unit
    /// tested without a controller or report loop.
    /// </summary>
    public static class FlickSnapping
    {
        public const double MinStrength = 0.0;
        public const double MaxStrength = 1.0;
        public const double DEFAULT_STRENGTH = 1.0;
        public const FlickSnapAngle DEFAULT_SNAP_ANGLE = FlickSnapAngle.Off;

        /// <summary>
        /// Angle in radians between adjacent snap directions. Ninety gives the four
        /// cardinal directions, FortyFive adds the four diagonals.
        /// </summary>
        public static double SnapIntervalRadians(FlickSnapAngle snapAngle)
        {
            return snapAngle switch
            {
                FlickSnapAngle.Ninety => Math.PI / 2.0,
                FlickSnapAngle.FortyFive => Math.PI / 4.0,
                _ => Math.PI,
            };
        }

        /// <summary>
        /// Returns the flick angle biased toward the nearest snap direction. Strength
        /// 0 leaves the angle untouched and 1 snaps fully; values in between lerp
        /// between the two. The angle is relative to the current facing, so snapping
        /// lands the flick on whole quarter or eighth turns.
        /// </summary>
        public static double Apply(double stickAngle, FlickSnapAngle snapAngle,
            double strength)
        {
            if (snapAngle == FlickSnapAngle.Off)
            {
                return stickAngle;
            }

            strength = Math.Clamp(strength, MinStrength, MaxStrength);
            if (strength <= 0.0)
            {
                return stickAngle;
            }

            double snapInterval = SnapIntervalRadians(snapAngle);
            // MidpointRounding.AwayFromZero matches C++ round(), which JoyShockMapper
            // relies on here. The .NET default is banker's rounding and would send
            // exact midpoint flicks to the wrong neighbour.
            double snappedAngle = Math.Round(stickAngle / snapInterval,
                MidpointRounding.AwayFromZero) * snapInterval;

            return (stickAngle * (1.0 - strength)) + (snappedAngle * strength);
        }
    }
}
