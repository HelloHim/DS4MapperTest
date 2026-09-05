using System;

namespace DS4MapperTest.StickModifiers
{
    public class StickDeadZone
    {
        public enum DeadZoneTypes : ushort
        {
            Radial,
            Bowtie,
            Axial,
        }

        public class AxialDeadSettings
        {
            public double deadZone;
            public double maxZone = 1.0;
            public double antiDeadZone;

            public void CopyTo(AxialDeadSettings other)
            {
                other.deadZone = deadZone;
                other.maxZone = maxZone;
                other.antiDeadZone = antiDeadZone;
            }
        }

        private double deadZone;
        private double maxZone = 1.0;
        private double antiDeadZone;
        private bool circleDead = true;
        private DeadZoneTypes deadZoneType;
        private bool separateAxisDeadZones;
        private double deadZoneX;
        private double deadZoneY;

        private AxialDeadSettings deadSettingsXAxis = new AxialDeadSettings();
        private AxialDeadSettings deadSettingsYAxis = new AxialDeadSettings();

        public bool inSafeZone;

        public bool CircleDead
        {
            get => circleDead;
            set
            {
                circleDead = value;
                if (circleDead)
                {
                    deadZoneType = DeadZoneTypes.Radial;
                }
                else
                {
                    deadZoneType = DeadZoneTypes.Axial;
                }
            }
        }

        public double DeadZone { get => deadZone; set => deadZone = value; }
        public double MaxZone { get => maxZone; set => maxZone = value; }
        public double AntiDeadZone { get => antiDeadZone; set => antiDeadZone = value; }
        public DeadZoneTypes DeadZoneType
        {
            get => deadZoneType; set => deadZoneType = value;
        }
        public bool SeparateAxisDeadZones { get => separateAxisDeadZones; set => separateAxisDeadZones = value; }
        public double DeadZoneX { get => deadZoneX; set => deadZoneX = value; }
        public double DeadZoneY { get => deadZoneY; set => deadZoneY = value; }
        public AxialDeadSettings DeadSettingsXAxis
        {
            get => deadSettingsXAxis;
        }

        public AxialDeadSettings DeadSettingsYAxis
        {
            get => deadSettingsYAxis;
        }

        public StickDeadZone(double deadZone, double maxZone, double antiDeadZone)
        {
            this.deadZone = deadZone;
            this.maxZone = maxZone;
            this.antiDeadZone = antiDeadZone;
            deadZoneX = deadZone;
            deadZoneY = deadZone;
            circleDead = true;
            deadZoneType = DeadZoneTypes.Radial;
        }

        public StickDeadZone(double deadZoneX, double deadZoneY,
            double maxZoneX, double maxZoneY,
            double antiDeadZoneX, double antiDeadZoneY)
        {
            deadSettingsXAxis.deadZone = deadZoneX;
            deadSettingsXAxis.maxZone = maxZoneX;
            deadSettingsXAxis.antiDeadZone = antiDeadZoneX;

            deadSettingsYAxis.maxZone = maxZoneY;
            deadSettingsYAxis.deadZone = deadZoneY;
            deadSettingsYAxis.antiDeadZone = antiDeadZoneY;
            this.deadZoneX = deadZoneX;
            this.deadZoneY = deadZoneY;
            separateAxisDeadZones = true;
            circleDead = false;
            deadZoneType = DeadZoneTypes.Axial;
        }

        public StickDeadZone(StickDeadZone other)
        {
            deadZone = other.deadZone;
            maxZone = other.maxZone;
            antiDeadZone = other.antiDeadZone;
            separateAxisDeadZones = other.separateAxisDeadZones;
            deadZoneX = other.deadZoneX;
            deadZoneY = other.deadZoneY;
            other.deadSettingsXAxis.CopyTo(this.deadSettingsXAxis);
            other.deadSettingsYAxis.CopyTo(this.deadSettingsYAxis);
            circleDead = other.circleDead;
            deadZoneType = other.deadZoneType;
        }

        public void CalcOutValues(int axisXDir, int axisYDir,
            int maxDirX, int maxDirY,
            out double xNorm, out double yNorm)
        {
            bool xNegative = axisXDir < 0;
            bool yNegative = axisYDir < 0;

            if (deadZoneType == DeadZoneTypes.Radial)
            {
                if (separateAxisDeadZones && Math.Abs(deadZoneX - deadZoneY) > double.Epsilon)
                {
                    CalcEllipticalRadialValues(axisXDir, axisYDir, maxDirX, maxDirY,
                        out xNorm, out yNorm);
                    return;
                }

                double angle = Math.Atan2(-(axisYDir), axisXDir);
                double angCos = Math.Abs(Math.Cos(angle)),
                    angSin = Math.Abs(Math.Sin(angle));

                int currentDeadX = (int)(deadZone * maxDirX * angCos);
                int currentDeadY = (int)(deadZone * maxDirY * angSin);

                double stickDeadzoneSquared = (currentDeadX * currentDeadX) + (currentDeadY * currentDeadY);
                double stickSquared = Math.Pow(axisXDir, 2) + Math.Pow(axisYDir, 2);
                inSafeZone = stickSquared > stickDeadzoneSquared;

                if (inSafeZone)
                {
                    double antiDeadX = antiDeadZone * angCos;
                    double antiDeadY = antiDeadZone * angSin;

                    int maxZoneDirX = (int)(maxZone * maxDirX);
                    int maxZoneDirY = (int)(maxZone * maxDirY);

                    int valueX = (axisXDir < 0 && axisXDir < maxZoneDirX) ? maxZoneDirX : (axisXDir > 0 && axisXDir > maxZoneDirX) ? maxZoneDirX : axisXDir;
                    xNorm = (1.0 - antiDeadX) * ((valueX - currentDeadX) / (double)(maxZoneDirX - currentDeadX)) + antiDeadX;
                    if (xNegative) xNorm *= -1.0;

                    int valueY = (axisYDir < 0 && axisYDir < maxZoneDirY) ? maxZoneDirY : (axisYDir > 0 && axisYDir > maxZoneDirY) ? maxZoneDirY : axisYDir;
                    yNorm = (1.0 - antiDeadY) * ((valueY - currentDeadY) / (double)(maxZoneDirY - currentDeadY)) + antiDeadY;
                    if (yNegative) yNorm *= -1.0;
                }
                else
                {
                    xNorm = 0.0;
                    yNorm = 0.0;
                }
            }
            else if (deadZoneType == DeadZoneTypes.Axial)
            {
                int currentDeadX = (int)(deadSettingsXAxis.deadZone * maxDirX);
                int currentDeadY = (int)(deadSettingsXAxis.deadZone * maxDirY);

                bool tempSafeZone;
                tempSafeZone = axisXDir >= 0 ? (axisXDir > currentDeadX) :
                    (axisXDir < currentDeadX);
                if (tempSafeZone)
                {
                    inSafeZone = true;
                    double antiDeadX = deadSettingsXAxis.antiDeadZone;
                    int maxZoneDirX = (int)(deadSettingsXAxis.maxZone * maxDirX);
                    int valueX = (axisXDir < 0 && axisXDir < maxZoneDirX) ? maxZoneDirX : (axisXDir > 0 && axisXDir > maxZoneDirX) ? maxZoneDirX : axisXDir;
                    xNorm = (1.0 - antiDeadX) * ((valueX - currentDeadX) / (double)(maxZoneDirX - currentDeadX)) + antiDeadX;
                    if (xNegative) xNorm *= -1.0;
                }
                else
                {
                    xNorm = 0.0;
                }

                tempSafeZone = axisYDir >= 0 ? (axisYDir > currentDeadY) : (axisYDir < currentDeadY);
                if (tempSafeZone)
                {
                    inSafeZone = true;
                    double antiDeadY = deadSettingsYAxis.antiDeadZone;
                    int maxZoneDirY = (int)(deadSettingsYAxis.maxZone * maxDirY);
                    int valueY = (axisYDir < 0 && axisYDir < maxZoneDirY) ? maxZoneDirY : (axisYDir > 0 && axisYDir > maxZoneDirY) ? maxZoneDirY : axisYDir;
                    yNorm = (1.0 - antiDeadY) * ((valueY - currentDeadY) / (double)(maxZoneDirY - currentDeadY)) + antiDeadY;
                    if (yNegative) yNorm *= -1.0;
                }
                else
                {
                    yNorm = 0.0;
                }
            }
            else if (deadZoneType == DeadZoneTypes.Bowtie)
            {

                double angle = Math.Atan2(-(axisYDir), axisXDir);
                double angCos = Math.Abs(Math.Cos(angle)),
                    angSin = Math.Abs(Math.Sin(angle));

                int currentDeadX = (int)(deadZone * maxDirX * angCos);
                int currentDeadY = (int)(deadZone * maxDirY * angSin);
                if (separateAxisDeadZones && Math.Abs(deadZoneX - deadZoneY) > double.Epsilon)
                {
                    double xUnit = Math.Abs(axisXDir) / Math.Max(1.0, Math.Abs((double)maxDirX));
                    double yUnit = Math.Abs(axisYDir) / Math.Max(1.0, Math.Abs((double)maxDirY));
                    double ellipticalAngle = Math.Atan2(yUnit, xUnit);
                    currentDeadX = (int)(deadZoneX * Math.Abs(maxDirX) * Math.Cos(ellipticalAngle));
                    currentDeadY = (int)(deadZoneY * Math.Abs(maxDirY) * Math.Sin(ellipticalAngle));
                }

                double stickDeadzoneSquared = (currentDeadX * currentDeadX) + (currentDeadY * currentDeadY);
                double stickSquared = Math.Pow(axisXDir, 2) + Math.Pow(axisYDir, 2);
                inSafeZone = separateAxisDeadZones && Math.Abs(deadZoneX - deadZoneY) > double.Epsilon ?
                    IsOutsideEllipticalDeadZone(axisXDir, axisYDir, maxDirX, maxDirY) :
                    stickSquared > stickDeadzoneSquared;

                if (inSafeZone)
                {
                    double antiDeadX = antiDeadZone * angCos;
                    double antiDeadY = antiDeadZone * angSin;

                    int maxDeadZoneAxialX = (int)(maxDirX * 0.15);
                    int minDeadZoneAxialX = (int)(maxDirX * 0.08);

                    int maxDeadZoneAxialY = (int)(maxDirY * 0.15);
                    int minDeadZoneAxialY = (int)(maxDirY * 0.08);

                    int absDX = Math.Abs(axisXDir);
                    int absDY = Math.Abs(axisYDir);

                    int signX = Math.Sign(axisXDir);
                    int signY = Math.Sign(axisYDir);

                    int maxZoneDirX = (int)(maxZone * maxDirX);
                    int maxZoneDirY = (int)(maxZone * maxDirY);

                    double tempRangeRatioX = absDX / Math.Abs((double)maxZoneDirX);
                    double tempRangeRatioY = absDY / Math.Abs((double)maxZoneDirY);

                    int axialDeadX = (int)((maxDeadZoneAxialX - minDeadZoneAxialX) *
                        Math.Min(1.0, tempRangeRatioY) + minDeadZoneAxialX);
                    int axialDeadY = (int)((maxDeadZoneAxialY - minDeadZoneAxialY) *
                        Math.Min(1.0, tempRangeRatioX) + minDeadZoneAxialY);

                    if (absDX > axialDeadX)
                    {
                        int absRadialDeadX = Math.Abs(currentDeadX);
                        int tempUseDeadX = (absRadialDeadX > axialDeadX ? absRadialDeadX : axialDeadX) * signX;
                        int valueX = (axisXDir < 0 && axisXDir < maxZoneDirX) ? maxZoneDirX : (axisXDir > 0 && axisXDir > maxZoneDirX) ? maxZoneDirX : axisXDir;
                        xNorm = (1.0 - antiDeadX) * (valueX - tempUseDeadX) / (double)(maxZoneDirX - tempUseDeadX) + antiDeadX;
                        if (xNegative) xNorm *= -1.0;
                    }
                    else
                    {
                        xNorm = 0.0;
                    }

                    if (absDY > axialDeadY)
                    {
                        int absRadialDeadY = Math.Abs(currentDeadY);
                        int tempUseDeadY = (absRadialDeadY > axialDeadY ? absRadialDeadY : axialDeadY) * signY;
                        int valueY = (axisYDir < 0 && axisYDir < maxZoneDirY) ? maxZoneDirY : (axisYDir > 0 && axisYDir > maxZoneDirY) ? maxZoneDirY : axisYDir;
                        yNorm = (1.0 - antiDeadY) * ((valueY - tempUseDeadY) / (double)(maxZoneDirY - tempUseDeadY)) + antiDeadY;
                        if (yNegative) yNorm *= -1.0;
                    }
                    else
                    {
                        yNorm = 0.0;
                    }

                }
                else
                {
                    xNorm = yNorm = 0.0;
                }
            }
            else
            {
                xNorm = yNorm = 0.0;
            }
        }

        private bool IsOutsideEllipticalDeadZone(int axisXDir, int axisYDir, int maxDirX, int maxDirY)
        {
            if (deadZoneX <= 0.0 && deadZoneY <= 0.0)
            {
                return axisXDir != 0 || axisYDir != 0;
            }

            double xRadius = Math.Max(double.Epsilon, Math.Abs(maxDirX) * deadZoneX);
            double yRadius = Math.Max(double.Epsilon, Math.Abs(maxDirY) * deadZoneY);
            double x = axisXDir / xRadius;
            double y = axisYDir / yRadius;
            return (x * x) + (y * y) > 1.0;
        }

        private void CalcEllipticalRadialValues(int axisXDir, int axisYDir, int maxDirX, int maxDirY,
            out double xNorm, out double yNorm)
        {
            inSafeZone = IsOutsideEllipticalDeadZone(axisXDir, axisYDir, maxDirX, maxDirY);
            if (!inSafeZone)
            {
                xNorm = yNorm = 0.0;
                return;
            }

            bool xNegative = axisXDir < 0;
            bool yNegative = axisYDir < 0;
            double xUnit = Math.Abs(axisXDir) / Math.Max(1.0, Math.Abs((double)maxDirX));
            double yUnit = Math.Abs(axisYDir) / Math.Max(1.0, Math.Abs((double)maxDirY));
            double angle = Math.Atan2(yUnit, xUnit);
            int currentDeadX = (int)(deadZoneX * Math.Abs(maxDirX) * Math.Cos(angle));
            int currentDeadY = (int)(deadZoneY * Math.Abs(maxDirY) * Math.Sin(angle));
            int maxZoneDirX = (int)(maxZone * maxDirX);
            int maxZoneDirY = (int)(maxZone * maxDirY);
            int valueX = Math.Clamp(axisXDir, -Math.Abs(maxZoneDirX), Math.Abs(maxZoneDirX));
            int valueY = Math.Clamp(axisYDir, -Math.Abs(maxZoneDirY), Math.Abs(maxZoneDirY));

            xNorm = (1.0 - antiDeadZone * Math.Cos(angle)) *
                ((Math.Abs(valueX) - currentDeadX) / (double)Math.Max(1, Math.Abs(maxZoneDirX) - currentDeadX)) +
                antiDeadZone * Math.Cos(angle);
            yNorm = (1.0 - antiDeadZone * Math.Sin(angle)) *
                ((Math.Abs(valueY) - currentDeadY) / (double)Math.Max(1, Math.Abs(maxZoneDirY) - currentDeadY)) +
                antiDeadZone * Math.Sin(angle);
            if (xNegative) xNorm *= -1.0;
            if (yNegative) yNorm *= -1.0;
        }

        public void SetAxialDeadZone(double deadzoneX, double deadzoneY)
        {
            deadSettingsXAxis.deadZone = deadzoneX;
            deadSettingsYAxis.deadZone = deadzoneY;
            deadZoneType = DeadZoneTypes.Axial;
        }

        public bool ShouldInterpolate()
        {
            bool result;
            if (deadZoneType == DeadZoneTypes.Radial)
            {
                result = deadZone != 0.0 || maxZone != 1.0 || antiDeadZone != 0.0;
            }
            else if (deadZoneType == DeadZoneTypes.Axial)
            {
                result = deadSettingsXAxis.deadZone != 0.0 || deadSettingsYAxis.deadZone != 0.0 ||
                    deadSettingsXAxis.maxZone != 1.0 || deadSettingsYAxis.maxZone != 1.0 ||
                    deadSettingsXAxis.antiDeadZone != 0.0 || deadSettingsYAxis.antiDeadZone != 0.0;
            }
            else
            {
                result = false;
            }

            return result;
        }

        public void Release()
        {
            inSafeZone = false;
        }
    }
}
