namespace DS4MapperUnitTests
{
    [TestClass]
    public class FakerInputHandlerTests
    {
        [TestMethod]
        public void AccumulatesMultipleMovesBeforeSync()
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();

            handler.MoveRelativeMouse(5, -2);
            handler.MoveRelativeMouse(3, 1);

            Assert.AreEqual(8, handler.PendingMouseX);
            Assert.AreEqual(-1, handler.PendingMouseY);
            Assert.AreEqual(0, handler.RelativeMouseReportCount);
        }

        [TestMethod]
        public void SyncWithNoPendingMovementDoesNotReport()
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();

            handler.Sync();

            Assert.AreEqual(0, handler.RelativeMouseReportCount);
        }

        [TestMethod]
        public void SyncFlushesAccumulatedMovementAndResetsPending()
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();

            handler.MoveRelativeMouse(5, -2);
            handler.MoveRelativeMouse(3, 1);
            handler.Sync();

            Assert.AreEqual(1, handler.RelativeMouseReportCount);
            Assert.AreEqual(0, handler.PendingMouseX);
            Assert.AreEqual(0, handler.PendingMouseY);
            Assert.IsFalse(handler.SyncRelativeMouseFlag);
        }

        [TestMethod]
        public void OverflowCarriesRemainderAcrossSyncCallsInsteadOfDropping()
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();

            // Exceeds a single report's short range (-32767..32767).
            handler.MoveRelativeMouse(40000, 0);

            handler.Sync();

            // First flush clamps to the report max and keeps the remainder
            // pending rather than discarding it.
            Assert.AreEqual(1, handler.RelativeMouseReportCount);
            Assert.AreEqual(40000 - 32767, handler.PendingMouseX);
            Assert.IsTrue(handler.SyncRelativeMouseFlag);

            handler.Sync();

            // Second flush drains the remainder and clears the sync flag.
            Assert.AreEqual(2, handler.RelativeMouseReportCount);
            Assert.AreEqual(0, handler.PendingMouseX);
            Assert.IsFalse(handler.SyncRelativeMouseFlag);
        }
    }
}
