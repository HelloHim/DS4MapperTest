using DS4MapperTest;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class MouseSideButtonTests
    {
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
    }
}
