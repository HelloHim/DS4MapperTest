using System;

namespace DS4MapperTest.GyroActions
{
    public enum GyroSpaceChoice
    {
        LocalSpace,
        PlayerTurn,
        PlayerLean,
        WorldTurn,
        WorldLean,
    }

    public enum GyroLocalAxisSource
    {
        Yaw,
        Roll,
        Pitch,
        YawPlusRoll,
    }

    public struct GyroLocalAxisMapping
    {
        public const double CONTRIBUTION_DEFAULT = 100.0;
        public const double CONTRIBUTION_MIN = -100.0;
        public const double CONTRIBUTION_MAX = 100.0;

        public GyroLocalAxisSource source;
        public bool invertSingle;
        public double yawContribution;
        public double rollContribution;

        public static GyroLocalAxisMapping CreateDefault(GyroLocalAxisSource source)
        {
            return new GyroLocalAxisMapping()
            {
                source = source,
                invertSingle = false,
                yawContribution = CONTRIBUTION_DEFAULT,
                rollContribution = CONTRIBUTION_DEFAULT,
            };
        }
    }

    public struct GyroOrientationSettings
    {
        public GyroSpaceChoice gyroSpace;
        public GyroLocalAxisMapping horizontal;
        public GyroLocalAxisMapping vertical;

        public static GyroOrientationSettings CreateDefault()
        {
            return new GyroOrientationSettings()
            {
                gyroSpace = GyroSpaceChoice.LocalSpace,
                horizontal = GyroLocalAxisMapping.CreateDefault(GyroLocalAxisSource.Yaw),
                vertical = GyroLocalAxisMapping.CreateDefault(GyroLocalAxisSource.Pitch),
            };
        }
    }

    // Keeps a Yaw+Roll contribution and its derived "inverted" toggle in sync: the signed
    // contribution is authoritative, the toggle is always just its sign.
    public static class GyroContributionSync
    {
        public static double ApplySignFromInvert(double contribution, bool invert)
        {
            double magnitude = Math.Abs(contribution);
            return invert ? -magnitude : magnitude;
        }

        public static bool InvertFromContribution(double contribution) => contribution < 0.0;

        // At exactly 0% the contribution has no direction, so the invert toggle for it
        // is disabled (and shown off) rather than left clickable with no visible effect.
        public static bool CanToggleInvert(double contribution) => contribution != 0.0;
    }

    public static class GyroOrientationResolver
    {
        public static double Resolve(in GyroLocalAxisMapping mapping, double yaw, double roll, double pitch)
        {
            if (mapping.source == GyroLocalAxisSource.YawPlusRoll)
            {
                return (yaw * (mapping.yawContribution / 100.0))
                    + (roll * (mapping.rollContribution / 100.0));
            }

            double raw = mapping.source switch
            {
                GyroLocalAxisSource.Roll => roll,
                GyroLocalAxisSource.Pitch => pitch,
                _ => yaw,
            };

            return mapping.invertSingle ? -raw : raw;
        }
    }
}
