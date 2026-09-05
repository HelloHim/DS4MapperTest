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

            Assert.AreEqual(360.0, action.DegreesPerSecond);
            Assert.AreEqual(180.0, action.MouselikeFactor);
            Assert.AreEqual(1.0, action.VerticalScale, 1e-9);
            Assert.AreEqual(0.10, action.DeadMod.DeadZone, 1e-9);
            // Must stay below 1.0 or the "pegged at the edge" test that gates edge
            // push is unreachable for a real stick.
            Assert.AreEqual(0.9, action.DeadMod.MaxZone, 1e-9);
            Assert.IsTrue(action.EdgePushEnabled);
            Assert.IsTrue(action.ReturnDeadzoneEnabled);
            Assert.AreEqual(45.0, action.ReturnDeadzoneAngle);
            Assert.AreEqual(90.0, action.ReturnDeadzoneCutoffAngle);
        }

        [TestMethod]
        public void ClampsDegreesPerSecondToSupportedRange()
        {
            StickHybridAim action = new StickHybridAim();

            action.DegreesPerSecond = StickHybridAim.MaxDegreesPerSecond + 1.0;
            Assert.AreEqual(StickHybridAim.MaxDegreesPerSecond, action.DegreesPerSecond);

            action.DegreesPerSecond = -1.0;
            Assert.AreEqual(0.0, action.DegreesPerSecond);

            action.DegreesPerSecond = double.PositiveInfinity;
            Assert.AreEqual(StickHybridAim.DefaultDegreesPerSecond, action.DegreesPerSecond);
        }

        [TestMethod]
        public void ClampsMouselikeFactorToSupportedRange()
        {
            StickHybridAim action = new StickHybridAim();

            action.MouselikeFactor = 20000.0;
            Assert.AreEqual(StickHybridAim.MaxMouselikeFactor, action.MouselikeFactor);

            action.MouselikeFactor = -5.0;
            Assert.AreEqual(0.0, action.MouselikeFactor);

            action.MouselikeFactor = double.NaN;
            Assert.AreEqual(StickHybridAim.DefaultMouselikeFactor, action.MouselikeFactor);
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

        // Turn rate is authored in degrees per second and converted through the
        // profile's angle calibration, so a held stick turns the camera by the
        // configured amount rather than by an arbitrary uncalibrated multiplier.
        [TestMethod]
        public void HeldStickTurnRateMatchesCalibratedDegreesPerSecond()
        {
            TestMapper mapper = new TestMapper();
            StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);
            action.DegreesPerSecond = 360.0;
            action.MouselikeFactor = 0.0;
            action.EdgePushEnabled = false;
            action.ReturnDeadzoneEnabled = false;
            mapper.SetCurrentLatencyForTest(0.004);

            // First frame carries the stick's own movement into position; the second
            // is a steady hold, which is what the turn-rate term describes.
            action.Prepare(mapper, 30000, 0);
            action.Event(mapper);
            action.Prepare(mapper, 30000, 0);
            action.Event(mapper);

            double expected = mapper.ActionProfile.CalibCounts * 0.004;
            Assert.IsTrue(TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot snapshot));
            Assert.AreEqual(expected, snapshot.X, 1e-9);
            Assert.AreEqual(0.0, snapshot.Y, 1e-9);
        }

        // The mouselike half is a displacement, not a rate: moving the stick a given
        // distance turns the camera by MouselikeFactor degrees per full sweep no
        // matter how many frames the sweep took.
        [TestMethod]
        public void MouselikeTermMatchesCalibratedDegreesPerFullSweep()
        {
            TestMapper mapper = new TestMapper();
            StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);
            action.DegreesPerSecond = 0.0;
            action.MouselikeFactor = 180.0;
            action.EdgePushEnabled = false;
            action.ReturnDeadzoneEnabled = false;
            mapper.SetCurrentLatencyForTest(0.004);

            // Half a sweep out from centre in one frame.
            action.Prepare(mapper, 15000, 0);
            action.Event(mapper);

            double expected = 180.0 / 360.0 * mapper.ActionProfile.CalibCounts * 0.5;
            Assert.IsTrue(TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot snapshot));
            Assert.AreEqual(expected, snapshot.X, 1e-9);
        }

        // Edge push must engage on a straight horizontal push at the default max
        // zone: that combination exercises both the max zone default and the radial
        // projection, either of which previously killed edge push outright.
        [TestMethod]
        public void EdgePushSustainsMotionWhilePeggedOnAPureAxisPush()
        {
            TestMapper mapper = new TestMapper();
            mapper.SetCurrentLatencyForTest(0.004);

            double WithEdgePush(bool enabled)
            {
                StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);
                action.EdgePushEnabled = enabled;
                action.ReturnDeadzoneEnabled = false;

                // Flick out to the edge, then hold there with no further movement so
                // only the turn rate and any edge push remain.
                foreach (int axisValue in new[] { 8000, 16000, 24000, 30000, 30000, 30000 })
                {
                    action.Prepare(mapper, axisValue, 0);
                    action.Event(mapper);
                }

                TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot state);
                return state.X;
            }

            double withoutPush = WithEdgePush(false);
            double withPush = WithEdgePush(true);

            Assert.IsTrue(withoutPush > 0.0);
            Assert.IsTrue(withPush > withoutPush,
                $"edge push added no sustain while pegged ({withPush} vs {withoutPush})");
        }

        // Every term aims along the stick's own angle, so a 45 degree push must come
        // out as an exact 45 degree mouse movement instead of drifting off-diagonal.
        [TestMethod]
        public void DiagonalPushProducesDiagonalOutput()
        {
            TestMapper mapper = new TestMapper();
            StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);
            action.MouselikeFactor = 0.0;
            action.EdgePushEnabled = false;
            action.ReturnDeadzoneEnabled = false;
            mapper.SetCurrentLatencyForTest(0.004);

            action.Prepare(mapper, 30000, 30000);
            action.Event(mapper);
            action.Prepare(mapper, 30000, 30000);
            action.Event(mapper);

            Assert.IsTrue(TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot snapshot));
            Assert.IsTrue(snapshot.X > 0.0);
            Assert.AreEqual(-snapshot.Y, snapshot.X, 1e-9);
        }

        [TestMethod]
        public void VerticalScaleScalesOnlyVerticalOutput()
        {
            TestMapper mapper = new TestMapper();
            StickHybridAim action = new StickHybridAim(mapper.KnownStickDefinitions["Stick"]);
            action.MouselikeFactor = 0.0;
            action.EdgePushEnabled = false;
            action.ReturnDeadzoneEnabled = false;
            action.VerticalScale = 0.5;
            mapper.SetCurrentLatencyForTest(0.004);

            action.Prepare(mapper, 30000, 30000);
            action.Event(mapper);
            action.Prepare(mapper, 30000, 30000);
            action.Event(mapper);

            Assert.IsTrue(TryGetJoystickRouteState(mapper, out Mapper.RouteMouseStateSnapshot snapshot));
            Assert.AreEqual(-snapshot.Y * 2.0, snapshot.X, 1e-9);
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

            // Ramp back down to centre.
            int[] rampIn = { 24000, 18000, 12000, 6000, 0 };
            foreach (int axisValue in rampIn)
            {
                action.Prepare(mapper, axisValue, 0);
                action.Event(mapper);
            }

            // One more frame resting exactly at centre: velocity is zero and the
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
