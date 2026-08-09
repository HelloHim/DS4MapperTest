using DS4MapperTest;
using DS4MapperTest.StickActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickHybridAimTests
    {
        private static bool TryGetJoystickRouteState(TestMapper mapper,
            out Mapper.RouteMouseStateSnapshot snapshot) =>
            mapper.TryGetRouteMouseStateForTest(MouseOutputRoute.JoystickMouse, out snapshot);

        [TestMethod]
        public void UsesExpectedDefaults()
        {
            StickHybridAim action = new StickHybridAim();

            Assert.AreEqual(3000, action.StickSens);
            Assert.AreEqual(1500.0, action.MouselikeFactor);
            Assert.AreEqual(0.10, action.DeadMod.DeadZone, 1e-9);
            Assert.AreEqual(1.0, action.DeadMod.MaxZone, 1e-9);
            Assert.IsTrue(action.EdgePushEnabled);
            Assert.IsTrue(action.ReturnDeadzoneEnabled);
            Assert.AreEqual(45.0, action.ReturnDeadzoneAngle);
            Assert.AreEqual(90.0, action.ReturnDeadzoneCutoffAngle);
        }

        [TestMethod]
        public void ClampsMouselikeFactorToSupportedRange()
        {
            StickHybridAim action = new StickHybridAim();

            action.MouselikeFactor = 20000.0;
            Assert.AreEqual(10000.0, action.MouselikeFactor);

            action.MouselikeFactor = -5.0;
            Assert.AreEqual(0.0, action.MouselikeFactor);
        }

        [TestMethod]
        public void ClampsReturnDeadzoneAnglesToSupportedRange()
        {
            StickHybridAim action = new StickHybridAim();

            action.ReturnDeadzoneAngle = 200.0;
            Assert.AreEqual(180.0, action.ReturnDeadzoneAngle);

            action.ReturnDeadzoneAngle = -10.0;
            Assert.AreEqual(0.0, action.ReturnDeadzoneAngle);

            action.ReturnDeadzoneCutoffAngle = 200.0;
            Assert.AreEqual(180.0, action.ReturnDeadzoneCutoffAngle);

            action.ReturnDeadzoneCutoffAngle = -10.0;
            Assert.AreEqual(0.0, action.ReturnDeadzoneCutoffAngle);
        }

        [TestMethod]
        public void CenteredStickProducesNoOutput()
        {
            TestMapper mapper = new TestMapper();
            StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);

            action.Prepare(mapper, 0, 0);
            action.Event(mapper);

            Assert.IsTrue(TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot snapshot));
            Assert.AreEqual(0.0, snapshot.X);
            Assert.AreEqual(0.0, snapshot.Y);
        }

        [TestMethod]
        public void FullDeflectionAlongXProducesPositiveMouseXOutput()
        {
            TestMapper mapper = new TestMapper();
            StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);

            action.Prepare(mapper, 30000, 0);
            action.Event(mapper);

            Assert.IsTrue(TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot snapshot));
            Assert.IsFalse(double.IsNaN(snapshot.X));
            Assert.IsFalse(double.IsNaN(snapshot.Y));
            Assert.IsTrue(snapshot.X > 0.0);
        }

        [TestMethod]
        public void ReleasingFromPeggedFlickSettlesToZeroOutput()
        {
            TestMapper mapper = new TestMapper();
            StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);
            // Lower the outer deadzone so a full-deflection flick actually
            // crosses into "pegged" territory and exercises edge push.
            action.DeadMod.MaxZone = 0.9;

            // Ramp the stick out to full deflection over several frames so the
            // edge-push velocity history is populated by an actual flick, not a
            // single-frame teleport.
            int[] rampOut = { 5000, 12000, 20000, 26000, 30000, 30000 };
            foreach (int axisValue in rampOut)
            {
                action.Prepare(mapper, axisValue, 0);
                action.Event(mapper);
            }

            // Ramp back down to center.
            int[] rampIn = { 24000, 18000, 12000, 6000, 0 };
            foreach (int axisValue in rampIn)
            {
                action.Prepare(mapper, axisValue, 0);
                action.Event(mapper);
            }

            // One more frame resting exactly at center: velocity is zero and the
            // inner-deadzone branch has cleared edgePushAmount, so output should
            // have fully settled rather than leaving a residual flick.
            action.Prepare(mapper, 0, 0);
            action.Event(mapper);

            Assert.IsTrue(TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot snapshot));
            Assert.AreEqual(0.0, snapshot.X);
            Assert.AreEqual(0.0, snapshot.Y);
        }
    }
}
