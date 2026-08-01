namespace DS4MapperUnitTests
{
    /// <summary>
    /// FakerInputHandler subclass that suppresses the native output call so
    /// accumulation/carry-over behaviour can be tested without a FakerInput
    /// driver installed.
    /// </summary>
    public class TestFakerInputHandler : DS4MapperTest.FakerInputHandler
    {
        public int RelativeMouseReportCount { get; private set; }

        public long PendingMouseX => pendingMouseX;
        public long PendingMouseY => pendingMouseY;
        public bool SyncRelativeMouseFlag => syncRelativeMouse;

        public short LastSentMouseX { get; private set; }
        public short LastSentMouseY { get; private set; }

        // Wheel position/hwheel are part of the same relative-mouse report as
        // X/Y, so Sync() resets them (via ResetMousePos()) right after
        // sending too - snapshot at send time, same as LastSentMouseX/Y.
        public byte LastSentWheelPosition { get; private set; }
        public byte LastSentHWheelPosition { get; private set; }

        protected override void SendRelativeMouseReport()
        {
            RelativeMouseReportCount++;
            LastSentMouseX = LastReportMouseX;
            LastSentMouseY = LastReportMouseY;
            LastSentWheelPosition = MouseReportWheelPosition;
            LastSentHWheelPosition = MouseReportHWheelPosition;
        }

        public bool MouseButtonHeldForTest(uint mouseButtonFlag) => IsMouseButtonHeld(mouseButtonFlag);
    }
}
