using System;

namespace DS4MapperTest.Common
{
    public static class AngleSnapping
    {
        public const double MinDegrees = 0.0;
        public const double MaxDegrees = 45.0;

        public static void Apply(ref double x, ref double y, double snapDegrees,
            bool smooth)
        {
            snapDegrees = Math.Clamp(snapDegrees, MinDegrees, MaxDegrees);
            if (snapDegrees <= 0.0 || (x == 0.0 && y == 0.0))
            {
                return;
            }

            double magnitude = Math.Sqrt((x * x) + (y * y));
            if (magnitude <= 0.0)
            {
                return;
            }

            double snapRadians = snapDegrees * Math.PI / 180.0;
            double referenceAngle = x == 0.0
                ? Math.PI / 2.0
                : Math.Atan(Math.Abs(y / x));

            if (smooth)
            {
                if (referenceAngle > (Math.PI / 2.0) - snapRadians)
                {
                    double progress = 1.0 -
                        (((Math.PI / 2.0) - referenceAngle) / snapRadians);
                    double snap = SmoothStep01(progress);
                    x *= 1.0 - snap;
                    y = CopySign(magnitude, y);
                }
                else if (referenceAngle < snapRadians)
                {
                    double progress = 1.0 - (referenceAngle / snapRadians);
                    double snap = SmoothStep01(progress);
                    y *= 1.0 - snap;
                    x = CopySign(magnitude, x);
                }
            }
            else if (referenceAngle > (Math.PI / 2.0) - snapRadians)
            {
                x = 0.0;
                y = CopySign(magnitude, y);
            }
            else if (referenceAngle < snapRadians)
            {
                x = CopySign(magnitude, x);
                y = 0.0;
            }
        }

        private static double SmoothStep01(double value)
        {
            value = Math.Clamp(value, 0.0, 1.0);
            return value * value * (3.0 - (2.0 * value));
        }

        private static double CopySign(double magnitude, double sign)
        {
            return sign < 0.0 ? -Math.Abs(magnitude) : Math.Abs(magnitude);
        }
    }
}
