using DS4MapperTest;
using DS4MapperTest.StickActions;
using Newtonsoft.Json;

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

        private static void PrepareAndEvent(StickMouse action, TestMapper mapper,
            int axisX, int axisY, double latency)
        {
            mapper.SetCurrentLatencyForTest(latency);
            action.Prepare(mapper, axisX, axisY);
            action.Event(mapper);
        }

        private static double SimulateForDuration(StickMouse action, TestMapper mapper,
            int axisX, int axisY, double durationSeconds, params double[] frameLatencies)
        {
            double totalX = 0.0;
            double elapsed = 0.0;
            int index = 0;
            while (elapsed < durationSeconds - 1e-12)
            {
                double latency = frameLatencies[index % frameLatencies.Length];
                if (elapsed + latency > durationSeconds)
                {
                    latency = durationSeconds - elapsed;
                }

                PrepareAndEvent(action, mapper, axisX, axisY, latency);
                totalX += GetJoystickRouteState(mapper).X;
                elapsed += latency;
                index++;
            }

            return totalX;
        }

        private sealed class RecordingVirtualKBM : VirtualKBMBase
        {
            public int TotalX { get; private set; }
            public int TotalY { get; private set; }

            public override bool Connect() => true;
            public override bool Disconnect() => true;

            public override void MoveRelativeMouse(int x, int y)
            {
                TotalX += x;
                TotalY += y;
            }

            public override void MoveAbsoluteMouse(double x, double y) { }
            public override void PerformMouseWheelEvent(int x, int y) { }
            public override void PerformMouseButtonEvent(uint mouseButton) { }
            public override void PerformMouseButtonPress(uint mouseButton) { }
            public override void PerformMouseButtonRelease(uint mouseButton) { }
            public override void PerformKeyPress(uint code) { }
            public override void PerformKeyPressAlt(uint key) { }
            public override void PerformKeyRelease(uint code) { }
            public override void PerformKeyReleaseAlt(uint key) { }
            public override string GetDisplayName() => "RecordingVirtualKBM";
            public override string GetIdentifier() => "recording";
            public override string GetFullDisplayName() => "RecordingVirtualKBM";
        }

        [TestMethod]
        public void UsesRequestedDefaultDegreesPerSecond()
        {
            StickMouse action = new StickMouse();

            Assert.AreEqual(360.0, action.DegreesPerSecond);
        }

        [TestMethod]
        public void ClampsDegreesPerSecondToSupportedRange()
        {
            StickMouse action = new StickMouse();

            action.DegreesPerSecond = StickMouse.MaxDegreesPerSecond + 1.0;
            Assert.AreEqual(StickMouse.MaxDegreesPerSecond, action.DegreesPerSecond);

            action.DegreesPerSecond = -1.0;
            Assert.AreEqual(0.0, action.DegreesPerSecond);

            action.DegreesPerSecond = double.PositiveInfinity;
            Assert.AreEqual(StickMouse.DefaultDegreesPerSecond, action.DegreesPerSecond);
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

        [TestMethod]
        public void ZeroDegreesPerSecondProducesNoMovement()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DegreesPerSecond = 0.0;

            PrepareAndEvent(action, mapper, 30000, 0, 0.125);

            Mapper.RouteMouseStateSnapshot snapshot = GetJoystickRouteState(mapper);
            Assert.AreEqual(0.0, snapshot.X, 1e-10);
            Assert.AreEqual(0.0, snapshot.Y, 1e-10);
        }

        [TestMethod]
        public void FullDeflectionProducesConfiguredCountsPerSecond()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DegreesPerSecond = 360.0;

            PrepareAndEvent(action, mapper, 30000, 0, 0.125);

            Mapper.RouteMouseStateSnapshot snapshot = GetJoystickRouteState(mapper);
            Assert.AreEqual(2000.0, snapshot.X, 1e-6);
            Assert.AreEqual(0.0, snapshot.Y, 1e-10);
        }

        [TestMethod]
        public void DegreesPerSecondMapsToCalibratedCountsPerSecond()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;

            action.DegreesPerSecond = 180.0;
            PrepareAndEvent(action, mapper, 30000, 0, 0.125);
            Assert.AreEqual(1000.0, GetJoystickRouteState(mapper).X, 1e-6);

            action.DegreesPerSecond = 360.0;
            PrepareAndEvent(action, mapper, 30000, 0, 0.125);
            Assert.AreEqual(2000.0, GetJoystickRouteState(mapper).X, 1e-6);

            action.DegreesPerSecond = 720.0;
            PrepareAndEvent(action, mapper, 30000, 0, 0.125);
            Assert.AreEqual(4000.0, GetJoystickRouteState(mapper).X, 1e-6);
        }

        [TestMethod]
        public void LinearCurveHalfDeflectionProducesHalfConfiguredSpeed()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.OutputCurve = DS4MapperTest.StickModifiers.StickOutCurve.Curve.Linear;
            action.DegreesPerSecond = 360.0;

            PrepareAndEvent(action, mapper, 15000, 0, 0.125);

            Mapper.RouteMouseStateSnapshot snapshot = GetJoystickRouteState(mapper);
            Assert.AreEqual(1000.0, snapshot.X, 1e-6);
        }

        [TestMethod]
        public void OppositeDeflectionsProduceOppositeMagnitudes()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DegreesPerSecond = 360.0;

            PrepareAndEvent(action, mapper, 30000, 0, 0.125);
            double positiveX = GetJoystickRouteState(mapper).X;

            PrepareAndEvent(action, mapper, -30000, 0, 0.125);
            double negativeX = GetJoystickRouteState(mapper).X;

            Assert.AreEqual(-positiveX, negativeX, 1e-6);
        }

        [TestMethod]
        public void RemovesLegacyMovementFloorFromSmallDeflection()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DegreesPerSecond = 360.0;

            PrepareAndEvent(action, mapper, 3000, 0, 0.125);

            Mapper.RouteMouseStateSnapshot snapshot = GetJoystickRouteState(mapper);
            Assert.AreEqual(200.0, snapshot.X, 1e-6);
        }

        [TestMethod]
        public void TimingIsIndependentAcrossPollingRates()
        {
            const double durationSeconds = 1.0;

            double RunAtRate(double latency)
            {
                TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
                StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
                action.DeadMod.DeadZone = 0.0;
                action.DegreesPerSecond = 360.0;
                return SimulateForDuration(action, mapper, 30000, 0, durationSeconds, latency);
            }

            double rate125 = RunAtRate(1.0 / 125.0);
            double rate250 = RunAtRate(1.0 / 250.0);
            double rate500 = RunAtRate(1.0 / 500.0);
            double rate1000 = RunAtRate(1.0 / 1000.0);

            TestMapper varyingMapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse varyingAction = new StickMouse(varyingMapper.KnownStickDefinitions["Stick"]);
            varyingAction.DeadMod.DeadZone = 0.0;
            varyingAction.DegreesPerSecond = 360.0;
            double varying = SimulateForDuration(varyingAction, varyingMapper, 30000, 0,
                durationSeconds, 0.003, 0.005, 0.002, 0.004);

            Assert.AreEqual(rate125, rate250, 2.0);
            Assert.AreEqual(rate125, rate500, 2.0);
            Assert.AreEqual(rate125, rate1000, 2.0);
            Assert.AreEqual(rate125, varying, 2.0);
        }

        [TestMethod]
        public void FractionalOutputAccumulatesAcrossFlushes()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            RecordingVirtualKBM output = new RecordingVirtualKBM();
            mapper.AttachVirtualOutputForTest(output, new SendInputMapping());

            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DegreesPerSecond = 1.0;

            for (int i = 0; i < 1000; i++)
            {
                PrepareAndEvent(action, mapper, 30000, 0, 0.001);
                mapper.FlushQueuedMouseOutputForTest();
            }

            Assert.AreEqual(40, output.TotalX);
            Assert.AreEqual(0, output.TotalY);
        }

        [TestMethod]
        public void CalibrationChangesUpdateDerivedJoystickMouseOutput()
        {
            Profile profile = new Profile { CalibCounts = 16000.0 };
            TestMapper mapper = new TestMapper(profile);
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DegreesPerSecond = 360.0;

            PrepareAndEvent(action, mapper, 30000, 0, 0.125);
            double before = GetJoystickRouteState(mapper).X;

            profile.CalibCounts = 32000.0;

            PrepareAndEvent(action, mapper, 30000, 0, 0.125);
            double after = GetJoystickRouteState(mapper).X;

            Assert.AreEqual(before * 2.0, after, 1e-6);
            Assert.AreEqual(360.0, action.DegreesPerSecond, 1e-10);
        }

        [TestMethod]
        public void VerticalScaleStillAppliesToVerticalDegreesPerSecond()
        {
            TestMapper mapper = new TestMapper(new Profile { CalibCounts = 16000.0 });
            StickMouse action = new StickMouse(mapper.KnownStickDefinitions["Stick"]);
            action.DeadMod.DeadZone = 0.0;
            action.DegreesPerSecond = 360.0;
            action.VerticalScale = 0.5;

            PrepareAndEvent(action, mapper, 0, 30000, 0.125);

            Mapper.RouteMouseStateSnapshot snapshot = GetJoystickRouteState(mapper);
            Assert.AreEqual(-1000.0, snapshot.Y, 1e-6);
        }

        [TestMethod]
        public void SerializerPersistsDegreesPerSecond()
        {
            StickMouse action = new StickMouse();
            action.DegreesPerSecond = 540.5;
            action.VerticalScale = 1.25;

            StickMouseSerializer serializer = new StickMouseSerializer(null, action);
            string json = JsonConvert.SerializeObject(serializer, Formatting.Indented);

            Assert.IsTrue(json.Contains(@"""DegreesPerSecond"": 540.5"));
            Assert.IsFalse(json.Contains(@"""MouseSpeed"""));
        }

        [TestMethod]
        public void LegacyMouseSpeedLoadsAsDefaultDegreesPerSecondAndResavesOnce()
        {
            const string json = @"{
  ""Id"": 9,
  ""ActionMode"": ""StickMouseAction"",
  ""Settings"": {
    ""MouseSpeed"": 3000,
    ""DeadZone"": 0.1
  }
}";

            StickMouseSerializer serializer = new StickMouseSerializer();
            JsonConvert.PopulateObject(json, serializer);

            StickMouse action = (StickMouse)serializer.MapAction;
            Assert.IsTrue(action.LegacyMouseSpeedLoaded);
            Assert.AreEqual(StickMouse.DefaultDegreesPerSecond, action.DegreesPerSecond, 1e-10);

            string migratedJson = JsonConvert.SerializeObject(serializer, Formatting.Indented);
            Assert.IsTrue(migratedJson.Contains(@"""DegreesPerSecond"": 360.0"));
            Assert.IsFalse(migratedJson.Contains(@"""MouseSpeed"""));
        }
    }
}
