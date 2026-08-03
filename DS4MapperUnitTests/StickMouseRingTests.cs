using DS4MapperTest.StickActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickMouseRingTests
    {
        [TestMethod]
        public void UsesExpectedDefaults()
        {
            StickMouseRing action = new StickMouseRing();

            Assert.AreEqual(0.15, action.RingRadius, 1e-9);
            Assert.AreEqual(0.10, action.DeadMod.DeadZone, 1e-9);
            Assert.AreEqual(1.0, action.DeadMod.MaxZone, 1e-9);
        }

        [TestMethod]
        public void ClampsRingRadiusToSupportedRange()
        {
            StickMouseRing action = new StickMouseRing();

            action.RingRadius = 2.0;
            Assert.AreEqual(1.0, action.RingRadius);

            action.RingRadius = -0.5;
            Assert.AreEqual(0.0, action.RingRadius);
        }

        [TestMethod]
        public void CenteredStickProducesNoOutput()
        {
            TestMapper mapper = new TestMapper();
            StickMouseRing action = new StickMouseRing(mapper.KnownStickDefinitions["Stick"]);

            action.Prepare(mapper, 0, 0);
            action.Event(mapper);

            Assert.IsFalse(mapper.AbsMouseSync);
        }

        [TestMethod]
        public void FullDeflectionRightPlacesCursorOnRingToTheRight()
        {
            TestMapper mapper = new TestMapper();
            StickMouseRing action = new StickMouseRing(mapper.KnownStickDefinitions["Stick"]);
            action.RingRadius = 0.15;

            action.Prepare(mapper, 30000, 0);
            action.Event(mapper);

            Assert.IsTrue(mapper.AbsMouseSync);
            Assert.AreEqual(0.65, mapper.AbsMouseX, 0.01);
            Assert.AreEqual(0.5, mapper.AbsMouseY, 0.01);
        }

        [TestMethod]
        public void ReleasingStickLeavesCursorInPlace()
        {
            TestMapper mapper = new TestMapper();
            StickMouseRing action = new StickMouseRing(mapper.KnownStickDefinitions["Stick"]);
            action.RingRadius = 0.15;

            action.Prepare(mapper, 30000, 0);
            action.Event(mapper);
            double placedX = mapper.AbsMouseX;
            double placedY = mapper.AbsMouseY;

            // Stick returns to center (inside deadzone): JSM leaves the cursor
            // where it was rather than resetting it.
            action.Prepare(mapper, 0, 0);
            action.Event(mapper);

            Assert.AreEqual(placedX, mapper.AbsMouseX, 1e-9);
            Assert.AreEqual(placedY, mapper.AbsMouseY, 1e-9);
        }
    }
}
