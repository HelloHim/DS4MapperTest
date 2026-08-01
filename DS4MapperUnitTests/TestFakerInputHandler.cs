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

        protected override void SendRelativeMouseReport()
        {
            RelativeMouseReportCount++;
        }
    }
}
