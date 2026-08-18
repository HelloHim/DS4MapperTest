using DS4MapperTest.DS4Library;
using System;

namespace DS4MapperTest
{
    public enum LightbarMode : ushort
    {
        SolidColor,
        Rainbow,
        Flashing,
        Pulse,
        Battery,
        Passthru,
    }

    public class LightbarSettings
    {
        public const int RAINBOW_SECONDS_CYCLE_DEFAULT = 5;

        public LightbarMode Mode = LightbarMode.SolidColor;
        public DS4Library.DS4Color SolidColor = new DS4Library.DS4Color();
        public DS4Library.DS4Color FlashColor = new DS4Library.DS4Color();
        public DS4Library.DS4Color BatteryFullColor = new DS4Library.DS4Color();
        public int rainbowSecondsCycle = RAINBOW_SECONDS_CYCLE_DEFAULT;
        public DS4Library.DS4Color PulseColor = new DS4Library.DS4Color();

        public event EventHandler<LightbarMode> LightbarModeChanged;

        public void RaiseModeChanged()
        {
            LightbarModeChanged?.Invoke(this, Mode);
        }
    }

    public class LightbarProcessor
    {
        private bool useOverrideColor;
        public bool UserOverrideColor
        {
            get => useOverrideColor;
            set => useOverrideColor = value;
        }
        private DS4Color overrideColor = new DS4Color(0, 0, 0);
        public DS4Color OverrideColor => overrideColor;
        public ref DS4Color OverrideColorRef
        {
            get => ref overrideColor;
        }

        public void Reset()
        {
            useOverrideColor = false;
            overrideColor = new DS4Color(0, 0, 0);
        }

        public void UpdateLightbar(InputDeviceBase device, Profile profile)
        {
            switch(profile.LightbarSettings.Mode)
            {
                case LightbarMode.SolidColor:
                    {

                    }

                    break;
                default: break;
            }
        }
    }
}
