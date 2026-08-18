using System;

namespace DS4MapperTest.DS4Library
{
    // Controller-agnostic RGB lightbar colour used by profile-content
    // LightbarSettings (SolidColor/FlashColor/BatteryFullColor/PulseColor)
    // and LightbarProcessor. Kept under the historical DS4Library namespace
    // (rather than renamed) so existing profile-content code and JSON
    // property names are untouched; the actual DS4-hardware-specific reader
    // and mapper this once shared a file with were removed in the SDL3
    // cutover.
    public struct DS4Color : IEquatable<DS4Color>
    {
        public byte red;
        public byte green;
        public byte blue;

        public DS4Color()
        {
            red = 0;
            green = 0;
            blue = 255;
        }

        public DS4Color(byte red, byte green, byte blue)
        {
            this.red = red;
            this.green = green;
            this.blue = blue;
        }

        public void Reset()
        {
            red = green = blue = 0;
        }

        public bool Equals(DS4Color other)
        {
            return this.red == other.red && this.green == other.green && this.blue == other.blue;
        }

        public override string ToString()
        {
            return $"Red: {red} Green: {green} Blue: {blue}";
        }
    }
}
