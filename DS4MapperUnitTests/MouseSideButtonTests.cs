using DS4MapperTest;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class MouseSideButtonTests
    {
        private sealed class RecordingVirtualKBM : VirtualKBMBase
        {
            public sealed class MouseEvent
            {
                public bool Pressed { get; set; }
                public uint Flags { get; set; }
                public int MouseData { get; set; }
            }

            public List<MouseEvent> MouseEvents { get; } = new List<MouseEvent>();

            public override bool Connect() => true;
            public override bool Disconnect() => true;
            public override void MoveRelativeMouse(int x, int y) { }
            public override void MoveAbsoluteMouse(double x, double y) { }
            public override void PerformMouseWheelEvent(int vertical, int horizontal) { }
            public override void PerformMouseButtonEvent(uint mouseButton) { }
            public override void PerformMouseButtonPress(uint mouseButton) { }
            public override void PerformMouseButtonRelease(uint mouseButton) { }
            public override void PerformKeyPress(uint key) { }
            public override void PerformKeyPressAlt(uint key) { }
            public override void PerformKeyRelease(uint key) { }
            public override void PerformKeyReleaseAlt(uint key) { }
            public override string GetDisplayName() => "recording";
            public override string GetIdentifier() => "recording";
            public override string GetFullDisplayName() => "recording";

            public override void PerformMouseButtonPressAlt(uint mouseButton, int type)
            {
                MouseEvents.Add(new MouseEvent { Pressed = true, Flags = mouseButton, MouseData = type });
            }

            public override void PerformMouseButtonReleaseAlt(uint mouseButton, int type)
            {
                MouseEvents.Add(new MouseEvent { Pressed = false, Flags = mouseButton, MouseData = type });
            }
        }

        private static FakerInputMapping CreateMapping()
        {
            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            return mapping;
        }

        [TestInitialize]
        public void ClearSharedState() => TestMapper.MouseButtonReferenceCountDict.Clear();

        [TestCleanup]
        public void CleanupSharedState() => TestMapper.MouseButtonReferenceCountDict.Clear();

        [TestMethod]
        public void FakerInputMappingUsesTheWrapperSideButtonBits()
        {
            FakerInputMapping mapping = CreateMapping();

            Assert.AreEqual(8u, mapping.MOUSEEVENTF_XBUTTON1DOWN);
            Assert.AreEqual(8u, mapping.MOUSEEVENTF_XBUTTON1UP);
            Assert.AreEqual(16u, mapping.MOUSEEVENTF_XBUTTON2DOWN);
            Assert.AreEqual(16u, mapping.MOUSEEVENTF_XBUTTON2UP);
            Assert.AreNotEqual(mapping.MOUSEEVENTF_XBUTTON1DOWN, mapping.MOUSEEVENTF_XBUTTON2DOWN);
            Assert.AreEqual(1u, mapping.MOUSEEVENTF_LEFTDOWN);
            Assert.AreEqual(2u, mapping.MOUSEEVENTF_RIGHTDOWN);
            Assert.AreEqual(4u, mapping.MOUSEEVENTF_MIDDLEDOWN);
        }

        [TestMethod]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON1)]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON2)]
        public void ControllerMouseBindingPressesAndReleasesSideButton(int mouseCode)
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();
            FakerInputMapping mapping = CreateMapping();
            TestMapper mapper = new TestMapper();
            mapper.AttachVirtualOutputForTest(handler, mapping);
            uint button = mouseCode == MouseButtonCodes.MOUSE_XBUTTON1
                ? mapping.MOUSEEVENTF_XBUTTON1DOWN
                : mapping.MOUSEEVENTF_XBUTTON2DOWN;
            OutputActionData action = new OutputActionData(OutputActionData.ActionType.MouseButton, mouseCode);

            mapper.RunEventFromButton(action, true);
            mapper.SyncMouseButtons();
            Assert.IsTrue(handler.MouseButtonHeldForTest(button));

            mapper.RunEventFromButton(action, false);
            mapper.SyncMouseButtons();
            Assert.IsFalse(handler.MouseButtonHeldForTest(button));
        }

        [TestMethod]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON1)]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON2)]
        public void AnalogControllerMouseBindingPressesAndReleasesSideButton(int mouseCode)
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();
            FakerInputMapping mapping = CreateMapping();
            TestMapper mapper = new TestMapper();
            mapper.AttachVirtualOutputForTest(handler, mapping);
            uint button = mouseCode == MouseButtonCodes.MOUSE_XBUTTON1
                ? mapping.MOUSEEVENTF_XBUTTON1DOWN
                : mapping.MOUSEEVENTF_XBUTTON2DOWN;
            OutputActionData action = new OutputActionData(OutputActionData.ActionType.MouseButton, mouseCode);

            mapper.RunEventFromAnalog(action, true, 0.0, 0.0);
            mapper.SyncMouseButtons();
            Assert.IsTrue(handler.MouseButtonHeldForTest(button));

            mapper.RunEventFromAnalog(action, false, 0.0, 0.0);
            mapper.SyncMouseButtons();
            Assert.IsFalse(handler.MouseButtonHeldForTest(button));
        }

        [TestMethod]
        public void SendInputUsesSharedFlagsAndDistinctSideButtonData()
        {
            SendInputMapping mapping = new SendInputMapping();
            mapping.PopulateConstants();

            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONDOWN, mapping.MOUSEEVENTF_XBUTTON1DOWN);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONDOWN, mapping.MOUSEEVENTF_XBUTTON2DOWN);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONUP, mapping.MOUSEEVENTF_XBUTTON1UP);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONUP, mapping.MOUSEEVENTF_XBUTTON2UP);
            Assert.AreEqual(1, mapping.MOUSEEVENTF_XBUTTON1DATA);
            Assert.AreEqual(2, mapping.MOUSEEVENTF_XBUTTON2DATA);
        }

        [TestMethod]
        public void ControllerSideButtonsUseDistinctSendInputPressAndReleaseEvents()
        {
            RecordingVirtualKBM handler = new RecordingVirtualKBM();
            SendInputMapping mapping = new SendInputMapping();
            mapping.PopulateConstants();
            TestMapper mapper = new TestMapper();
            mapper.AttachVirtualOutputForTest(handler, mapping);
            OutputActionData mouse4 = new OutputActionData(OutputActionData.ActionType.MouseButton,
                MouseButtonCodes.MOUSE_XBUTTON1);
            OutputActionData mouse5 = new OutputActionData(OutputActionData.ActionType.MouseButton,
                MouseButtonCodes.MOUSE_XBUTTON2);

            mapper.RunEventFromButton(mouse4, true);
            mapper.RunEventFromButton(mouse4, true);
            mapper.RunEventFromButton(mouse5, true);
            mapper.SyncMouseButtons();

            Assert.AreEqual(2, handler.MouseEvents.Count);
            Assert.IsTrue(handler.MouseEvents[0].Pressed);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONDOWN, handler.MouseEvents[0].Flags);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTON1DATA, handler.MouseEvents[0].MouseData);
            Assert.IsTrue(handler.MouseEvents[1].Pressed);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONDOWN, handler.MouseEvents[1].Flags);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTON2DATA, handler.MouseEvents[1].MouseData);

            mapper.RunEventFromButton(mouse4, false);
            mapper.RunEventFromButton(mouse5, false);
            mapper.SyncMouseButtons();

            Assert.AreEqual(4, handler.MouseEvents.Count);
            Assert.IsFalse(handler.MouseEvents[2].Pressed);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONUP, handler.MouseEvents[2].Flags);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTON1DATA, handler.MouseEvents[2].MouseData);
            Assert.IsFalse(handler.MouseEvents[3].Pressed);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTONUP, handler.MouseEvents[3].Flags);
            Assert.AreEqual(mapping.MOUSEEVENTF_XBUTTON2DATA, handler.MouseEvents[3].MouseData);
        }

        [TestMethod]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON1)]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON2)]
        public void SendInputSideButtonReleasesOnlyAfterItsFinalOwner(int mouseCode)
        {
            RecordingVirtualKBM handler = new RecordingVirtualKBM();
            SendInputMapping mapping = new SendInputMapping();
            mapping.PopulateConstants();

            Mapper.AcquireSharedMouseButton(handler, mapping, mouseCode);
            Mapper.AcquireSharedMouseButton(handler, mapping, mouseCode);
            Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode);

            Assert.AreEqual(1, handler.MouseEvents.Count);
            Assert.IsTrue(handler.MouseEvents[0].Pressed);

            Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode);

            Assert.AreEqual(2, handler.MouseEvents.Count);
            Assert.IsFalse(handler.MouseEvents[1].Pressed);
            Assert.AreEqual(handler.MouseEvents[0].MouseData, handler.MouseEvents[1].MouseData);
        }
    }
}
