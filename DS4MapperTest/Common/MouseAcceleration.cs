using System;
using DS4MapperTest.GyroActions;

namespace DS4MapperTest.Common
{
    public static class MouseAcceleration
    {
        public static void CalculateMultipliers(
            GyroMouseAccelCurveChoice curve,
            double speed,
            double minThreshold,
            double maxThreshold,
            double minXSens,
            double maxXSens,
            double minYSens,
            double maxYSens,
            double powerVRef,
            double powerExponent,
            double naturalVHalf,
            out double multiplierX,
            out double multiplierY)
        {
            multiplierX = minXSens;
            multiplierY = minYSens;

            double activeMinThreshold = Math.Min(minThreshold, maxThreshold);
            double activeMaxThreshold = Math.Max(minThreshold, maxThreshold);
            if (speed < activeMinThreshold)
            {
                return;
            }

            double speedPastMinimum = speed - activeMinThreshold;
            if (curve == GyroMouseAccelCurveChoice.Natural)
            {
                if (naturalVHalf <= 0.0)
                {
                    multiplierX = maxXSens;
                    multiplierY = maxYSens;
                    return;
                }

                double decay = Math.Exp(-Math.Log(2.0) *
                    speedPastMinimum / naturalVHalf);
                multiplierX = maxXSens - (maxXSens - minXSens) * decay;
                multiplierY = maxYSens - (maxYSens - minYSens) * decay;
                return;
            }

            double alpha;
            if (curve == GyroMouseAccelCurveChoice.Power)
            {
                double reference = Math.Max(powerVRef, double.Epsilon);
                double ratio = speedPastMinimum / reference;
                alpha = 1.0 - Math.Exp(-Math.Pow(ratio, powerExponent));
            }
            else
            {
                double thresholdRange = activeMaxThreshold -
                    activeMinThreshold;
                alpha = thresholdRange > 0.0
                    ? Math.Clamp(speedPastMinimum / thresholdRange, 0.0, 1.0)
                    : 1.0;

                if (curve == GyroMouseAccelCurveChoice.Quadratic)
                {
                    alpha *= alpha;
                }
                else if (curve == GyroMouseAccelCurveChoice.Cubic)
                {
                    alpha = alpha * alpha * alpha;
                }
            }

            alpha = Math.Clamp(alpha, 0.0, 1.0);
            multiplierX = minXSens + (maxXSens - minXSens) * alpha;
            multiplierY = minYSens + (maxYSens - minYSens) * alpha;
        }
    }
}
