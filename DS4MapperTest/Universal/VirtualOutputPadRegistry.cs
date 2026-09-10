using System.Threading;

namespace DS4MapperTest.Universal
{
    // Counts the virtual Xbox 360 pads this process currently has plugged in, so
    // a controller backend can tell our own output apart from real hardware.
    //
    // Only the Xbox 360 pad needs this. Windows hands it to applications through
    // XInput, and an XInput device has no device path to walk, so SDL reports it
    // as "XInput#0" and the usual virtual device check has nothing to resolve. It
    // reports vendor 045E and product 028E as well, exactly like a real Xbox 360
    // pad, because being indistinguishable is the whole point of emulating one.
    // Nothing about how the device looks can identify it, so the only reliable
    // answer is whether we plugged one in ourselves.
    //
    // The DS4, DualSense Edge and Switch 2 Pro pads need none of this. They
    // arrive as ordinary HID devices whose path walks up to the usbip bus, and
    // Util.CheckIfVirtualDevice already recognises them.
    //
    // Deliberately lock free. This is read from the SDL polling thread while the
    // mapping threads write it, and those threads already hold backend locks when
    // they do, so taking a lock here would risk another ordering cycle.
    internal static class VirtualOutputPadRegistry
    {
        private static int xbox360PadCount;

        public static int Xbox360PadCount => Volatile.Read(ref xbox360PadCount);

        public static void AddXbox360Pad()
        {
            Interlocked.Increment(ref xbox360PadCount);
        }

        public static void RemoveXbox360Pad()
        {
            if (Interlocked.Decrement(ref xbox360PadCount) < 0)
            {
                Interlocked.Exchange(ref xbox360PadCount, 0);
            }
        }
    }
}
