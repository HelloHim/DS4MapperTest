using DS4MapperTest;
using DS4MapperTest.StickActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickMouseTests
    {
        private static Mapper.RouteMouseStateSnapshot GetJoystickRouteState(TestMapper mapper)
        {
            Assert.IsTrue(mapper.TryGetRouteMouseStateForTest(MouseOutputRoute.JoystickMouse,
                out Mapper.RouteMouseStateSnapshot snapshot));
            return snapshot;
        }

        [TestMethod]
        public void UsesRequestedDefaultMouseSpeed()
        {
            StickMouse action = new StickMouse();

            Assert.AreEqual(3000, action.MouseSpeed);
        }

        [TestMethod]
        public void ClampsMouseSpeedToSupportedRange()
        {
            StickMouse action = new StickMouse();

            action.MouseSpeed = 10001;
            Assert.AreEqual(10000, action.MouseSpeed);

            action.MouseSpeed = -1;
            Assert.AreEqual(0, action.MouseSpeed);
        }

        [TestMethod]
        public void UsesExpectedDefaultDiagonalRange()
        {
            StickMouse action = new StickMouse();

            Assert.AreEqual(90, action.DiagonalRange);
        }

        [TestMethod]
        public void ClampsDiagonalRangeToSupportedRange()
        {
            StickMouse action = new StickMouse();

            action.DiagonalRange = 120;
            Assert.AreEqual(90, action.DiagonalRange);

            action.DiagonalRange = -1;
            Assert.AreEqual(0, action.DiagonalRange);
        }

        [TestMethod]
        public void ZeroDiagonalRangeSuppressesMinorAxisOutput()
        {
            TestMapper mapper = new TestMapper();
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DiagonalRange = 0;

            action.Prepare(mapper, 30000, 15000);
            action.Event(mapper);

            Mapper.RouteMouseStateSnapshot snapshot = GetJoystickRouteState(mapper);
            Assert.IsTrue(snapshot.X > 0.0);
            Assert.AreEqual(0.0, snapshot.Y, 1e-9);
        }

        [TestMethod]
        public void ReducedDiagonalRangePreservesDiagonalOutputNearDiagonalAngle()
        {
            TestMapper mapper = new TestMapper();
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DiagonalRange = 30;

            action.Prepare(mapper, 30000, 25000);
            action.Event(mapper);

            Mapper.RouteMouseStateSnapshot snapshot = GetJoystickRouteState(mapper);
            Assert.IsTrue(snapshot.X > 0.0);
            Assert.IsTrue(snapshot.Y < 0.0);
        }
    }
}
