using DS4MapperTest;
using DS4MapperTest.PhysicalMouse;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class PhysicalMouseForwarderTests
    {
        private static FakerInputMapping CreateMapping()
        {
            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            return mapping;
        }

        // capture.Start() is never called in these tests, so no Raw Input/
        // native APIs are touched - only the forwarder's own event-handling
        // logic (called directly, as the real events would call it) is
        // under test.
        private static PhysicalMouseForwarder CreateAttachedForwarder(
            out TestFakerInputHandler handler, out FakerInputMapping mapping)
        {
            handler = new TestFakerInputHandler();
            mapping = CreateMapping();
            RawMouseCaptureDevice capture = new RawMouseCaptureDevice();
            PhysicalMouseForwarder forwarder = new PhysicalMouseForwarder(capture);
            forwarder.AttachOutput(handler, mapping);
            return forwarder;
        }

        [TestInitialize]
        public void ClearSharedState()
        {
            TestMapper.MouseButtonReferenceCountDict.Clear();
        }

        [TestCleanup]
        public void CleanupSharedState()
        {
            TestMapper.MouseButtonReferenceCountDict.Clear();
        }

        [TestMethod]
        public void MovementForwardsExactCountsAndFlushesImmediately()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out _);

            forwarder.HandleMouseMove(12, -7);

            Assert.AreEqual(1, handler.RelativeMouseReportCount);
            Assert.AreEqual((short)12, handler.LastSentMouseX);
            Assert.AreEqual((short)(-7), handler.LastSentMouseY);
        }

        [TestMethod]
        public void ZeroMovementDoesNotTriggerAReport()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out _);

            forwarder.HandleMouseMove(0, 0);

            Assert.AreEqual(0, handler.RelativeMouseReportCount);
        }

        [TestMethod]
        public void WheelConvertsWholeNotchImmediately()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out _);

            forwarder.HandleMouseWheel(120, horizontal: false); // exactly one notch, WHEEL_DELTA units

            Assert.AreEqual(1, handler.RelativeMouseReportCount);
            Assert.AreEqual(unchecked((byte)1), handler.LastSentWheelPosition);
            Assert.AreEqual(unchecked((byte)0), handler.LastSentHWheelPosition);
        }

        [TestMethod]
        public void WheelSubNotchDeltaIsCarriedNotDropped()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out _);

            // A high-resolution wheel sending less than a full WHEEL_DELTA
            // notch per report; three thirds should add up to one notch.
            forwarder.HandleMouseWheel(40, horizontal: false);
            Assert.AreEqual(0, handler.RelativeMouseReportCount);

            forwarder.HandleMouseWheel(40, horizontal: false);
            Assert.AreEqual(0, handler.RelativeMouseReportCount);

            forwarder.HandleMouseWheel(40, horizontal: false);
            Assert.AreEqual(1, handler.RelativeMouseReportCount);
            Assert.AreEqual(unchecked((byte)1), handler.LastSentWheelPosition);
        }

        [TestMethod]
        public void HorizontalWheelIsForwardedOnTheHorizontalAxis()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out _);

            forwarder.HandleMouseWheel(-120, horizontal: true);

            Assert.AreEqual(unchecked((byte)0), handler.LastSentWheelPosition);
            Assert.AreEqual(unchecked((byte)(-1)), handler.LastSentHWheelPosition);
        }

        [TestMethod]
        public void DuplicateDownFromSameSourceDoesNotInflateRefCount()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out FakerInputMapping mapping);

            forwarder.HandleMouseButton(RawMouseButton.Left, true);
            forwarder.HandleMouseButton(RawMouseButton.Left, true); // duplicate down; must be ignored

            forwarder.HandleMouseButton(RawMouseButton.Left, false); // single release should fully release

            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_LEFTDOWN));
        }

        [TestMethod]
        public void Button4And5ForwardToTheirDistinctVirtualButtons()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out FakerInputMapping mapping);

            forwarder.HandleMouseButton(RawMouseButton.Button4, true);
            Assert.IsTrue(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON1DOWN));
            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON2DOWN));

            forwarder.HandleMouseButton(RawMouseButton.Button5, true);
            Assert.IsTrue(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON1DOWN));
            Assert.IsTrue(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON2DOWN));

            forwarder.HandleMouseButton(RawMouseButton.Button4, false);
            forwarder.HandleMouseButton(RawMouseButton.Button5, false);

            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON1UP));
            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON2UP));
        }

        [TestMethod]
        public void UnknownMouseButtonIsIgnored()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out _);

            forwarder.HandleMouseButton((RawMouseButton)999, true);

            Assert.AreEqual(0, handler.RelativeMouseReportCount);
        }

        [TestMethod]
        public void DeviceRemovedReleasesOnlyPhysicalMouseHeldButtons()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out FakerInputMapping mapping);

            // Controller holds LEFT independently of the physical mouse.
            Mapper.AcquireSharedMouseButton(handler, mapping, DS4MapperTest.MapperUtil.MouseButtonCodes.MOUSE_LEFT_BUTTON);

            // Physical mouse holds LEFT (shared with controller) and RIGHT (its own).
            forwarder.HandleMouseButton(RawMouseButton.Left, true);
            forwarder.HandleMouseButton(RawMouseButton.Right, true);

            forwarder.HandleDeviceRemoved();

            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_RIGHTDOWN),
                "right was only held by the disconnected physical mouse");
            Assert.IsTrue(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_LEFTDOWN),
                "left is still held by the controller");
        }

        [TestMethod]
        public void DeviceRemovedReleasesOnlyPhysicalSideButtonOwnership()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out FakerInputMapping mapping);

            Mapper.AcquireSharedMouseButton(handler, mapping, DS4MapperTest.MapperUtil.MouseButtonCodes.MOUSE_XBUTTON1);
            forwarder.HandleMouseButton(RawMouseButton.Button4, true);
            forwarder.HandleMouseButton(RawMouseButton.Button5, true);

            forwarder.HandleDeviceRemoved();

            Assert.IsTrue(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON1DOWN),
                "XButton1 is still held by the controller");
            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_XBUTTON2DOWN),
                "XButton2 was held only by the disconnected physical mouse");
        }

        [TestMethod]
        public void DetachOutputReleasesHeldButtonsBeforeDetaching()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out TestFakerInputHandler handler, out FakerInputMapping mapping);

            forwarder.HandleMouseButton(RawMouseButton.Left, true);
            forwarder.DetachOutput();

            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_LEFTDOWN));
        }
    }
}
