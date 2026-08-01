using DS4MapperTest;
using DS4MapperTest.MapperUtil;

namespace DS4MapperUnitTests
{
    // mouseButtonReferenceCountDict is a static field shared by every Mapper
    // instance (and, as of phase 2, by physical-mouse forwarding), so tests
    // must clear it before/after to avoid bleeding state into other tests.
    [TestClass]
    public class SharedMouseButtonOwnershipTests
    {
        private static FakerInputMapping CreateMapping()
        {
            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            return mapping;
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
        public void ControllerHoldSurvivesPhysicalPressAndRelease()
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();
            FakerInputMapping mapping = CreateMapping();

            Mapper.AcquireSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_LEFT_BUTTON); // controller holds
            Mapper.AcquireSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_LEFT_BUTTON); // physical mouse presses
            Mapper.ReleaseSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_LEFT_BUTTON); // physical mouse releases

            Assert.IsTrue(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_LEFTDOWN),
                "controller's hold must survive an unrelated source's press+release");
        }

        [TestMethod]
        public void ReleasingLastHolderReleasesTheButton()
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();
            FakerInputMapping mapping = CreateMapping();

            Mapper.AcquireSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_RIGHT_BUTTON);
            Mapper.ReleaseSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_RIGHT_BUTTON);

            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_RIGHTDOWN));
        }

        [TestMethod]
        public void BothSourcesMustReleaseBeforeButtonGoesUp()
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();
            FakerInputMapping mapping = CreateMapping();

            Mapper.AcquireSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_MIDDLE_BUTTON); // controller
            Mapper.AcquireSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_MIDDLE_BUTTON); // physical mouse

            Mapper.ReleaseSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_MIDDLE_BUTTON); // controller releases
            Assert.IsTrue(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_MIDDLEDOWN),
                "still held by the physical mouse source");

            Mapper.ReleaseSharedMouseButton(handler, mapping, MouseButtonCodes.MOUSE_MIDDLE_BUTTON); // physical mouse releases
            Assert.IsFalse(handler.MouseButtonHeldForTest(mapping.MOUSEEVENTF_MIDDLEDOWN));
        }

        [TestMethod]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON1, true)]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON1, false)]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON2, true)]
        [DataRow(MouseButtonCodes.MOUSE_XBUTTON2, false)]
        public void EitherSourceReleasingSideButtonLeavesOtherHoldIntact(int mouseCode,
            bool physicalReleasesFirst)
        {
            TestFakerInputHandler handler = new TestFakerInputHandler();
            FakerInputMapping mapping = CreateMapping();
            uint button = mouseCode == MouseButtonCodes.MOUSE_XBUTTON1
                ? mapping.MOUSEEVENTF_XBUTTON1DOWN
                : mapping.MOUSEEVENTF_XBUTTON2DOWN;

            Mapper.AcquireSharedMouseButton(handler, mapping, mouseCode); // controller
            Mapper.AcquireSharedMouseButton(handler, mapping, mouseCode); // physical mouse
            if (physicalReleasesFirst)
            {
                Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode); // physical mouse
            }
            else
            {
                Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode); // controller
            }

            Assert.IsTrue(handler.MouseButtonHeldForTest(button));

            Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode);
            Assert.IsFalse(handler.MouseButtonHeldForTest(button));
        }
    }
}
