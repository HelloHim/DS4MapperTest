using System;
using DS4MapperTest.StickModifiers;

namespace DS4MapperTest.MouseModifiers
{
    public class MouseMotionSettings
    {
        public const int DefaultMouseSpeed = 3000;
        public const int MaxMouseSpeed = 10000;
        public const double DefaultVerticalScale = 1.0;
        public const double MaxVerticalScale = 10.0;

        public class DeltaAccelSettings
        {
            public bool enabled = false;
            public double multiplier = 4.0;
            public double maxTravel = 0.2;
            public double minTravel = 0.01;
            public double easingDuration = 0.2;
            public double minfactor = 1.0;

            public bool Enabled
            {
                get => enabled;
                set => enabled = value;
            }

            public double Multiplier
            {
                get => multiplier;
                set => multiplier = value;
            }

            public double MaxTravel
            {
                get => maxTravel;
                set => maxTravel = value;
            }

            public double MinTravel
            {
                get => minTravel;
                set => minTravel = value;
            }

            public double EasingDuration
            {
                get => easingDuration;
                set => easingDuration = value;
            }

            public double MinFactor
            {
                get => minfactor;
                set => minfactor = value;
            }

            public DeltaAccelSettings()
            {
            }

            public DeltaAccelSettings(DeltaAccelSettings source)
            {
                enabled = source.enabled;
                multiplier = source.multiplier;
                maxTravel = source.maxTravel;
                minTravel = source.minTravel;
                easingDuration = source.easingDuration;
                minfactor = source.minfactor;
            }
        }

        private int mouseSpeed = DefaultMouseSpeed;
        public int MouseSpeed
        {
            get => mouseSpeed;
            set => mouseSpeed = Math.Clamp(value, 0, MaxMouseSpeed);
        }

        private double verticalScale = DefaultVerticalScale;
        public double VerticalScale
        {
            get => verticalScale;
            set => verticalScale = Math.Clamp(value, 0.0, MaxVerticalScale);
        }

        private StickOutCurve.Curve outputCurve = StickOutCurve.Curve.Linear;
        public StickOutCurve.Curve OutputCurve
        {
            get => outputCurve;
            set => outputCurve = value;
        }

        private DeltaAccelSettings deltaSettings = new DeltaAccelSettings();
        public DeltaAccelSettings DeltaSettings
        {
            get => deltaSettings;
            set => deltaSettings = value;
        }

        public MouseMotionSettings()
        {
        }

        public MouseMotionSettings(MouseMotionSettings source)
        {
            mouseSpeed = source.mouseSpeed;
            verticalScale = source.verticalScale;
            outputCurve = source.outputCurve;
            deltaSettings = new DeltaAccelSettings(source.deltaSettings);
        }
    }
}
