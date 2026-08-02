
namespace DS4MapperTest
{
    public abstract class VirtualKBMMapping
    {
        public uint MOUSEEVENTF_LEFTDOWN = 2, MOUSEEVENTF_LEFTUP = 4,
            MOUSEEVENTF_RIGHTDOWN = 8, MOUSEEVENTF_RIGHTUP = 16,
            MOUSEEVENTF_MIDDLEDOWN = 32, MOUSEEVENTF_MIDDLEUP = 64,
            MOUSEEVENTF_XBUTTONDOWN = 128, MOUSEEVENTF_XBUTTONUP = 256,
            MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x1000;

        // X buttons require their own values for virtual backends. Unlike
        // Win32's MOUSEEVENTF_XBUTTONDOWN/XBUTTONUP flags, FakerInput uses
        // one persistent report bit per button and press/release is selected
        // by the handler method that receives it.
        public uint MOUSEEVENTF_XBUTTON1DOWN, MOUSEEVENTF_XBUTTON1UP,
            MOUSEEVENTF_XBUTTON2DOWN, MOUSEEVENTF_XBUTTON2UP;

        // SendInput identifies the particular side button separately from
        // its shared X-button down/up event flags. Backends such as
        // FakerInput leave these at zero and use their normal press/release
        // methods with their own persistent held-button bits instead.
        public int MOUSEEVENTF_XBUTTON1DATA, MOUSEEVENTF_XBUTTON2DATA;

        public uint KEY_TAB = 0x09, KEY_LALT = 0x12;
        public int WHEEL_TICK_DOWN = -120, WHEEL_TICK_UP = 120;
        public int WHEEL_TICK_BASE = 120;
        public bool macroKeyTranslate = false;

        public abstract void PopulateConstants();
        public abstract void PopulateMappings();
        public abstract uint GetRealEventKey(uint winVkKey);
    }
}
