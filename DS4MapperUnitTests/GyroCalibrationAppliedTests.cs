using DS4MapperTest;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    // Calibration exists to cancel a resting gyro's drift. It only does that if
    // the offset reaches the fields the gyro actions read. Gyro mouse,
    // directional swipe and the joystick angular path all read AngGyro*, so
    // these assert against those, not against the legacy integer fields that
    // happen to sit beside them in the same frame.
    [TestClass]
    public class GyroCalibrationAppliedTests
    {
        private const double DriftDegreesPerSecond = 2.0;
        private const double DegreesToRadians = Math.PI / 180.0;

        [TestMethod]
        public void RestingDriftIsCancelledInTheAngularValuesTheActionsRead()
        {
            UniversalMapper mapper = CreateGyroMapper();
            try
            {
                // A controller sitting still but reporting a steady 2 deg/s on
                // every axis, which is what calibration is for.
                for (int i = 1; i <= 200; i++)
                {
                    mapper.ProcessSnapshot(GyroSnapshot(i,
                        DriftDegreesPerSecond, DriftDegreesPerSecond, DriftDegreesPerSecond));
                }

                GyroEventFrame frame = mapper.LastGyroFrameForTest;
                Assert.AreEqual(0.0, frame.AngGyroYaw, 0.01,
                    "Yaw drift survived calibration and would still push the pointer.");
                Assert.AreEqual(0.0, frame.AngGyroPitch, 0.01);
                Assert.AreEqual(0.0, frame.AngGyroRoll, 0.01);
            }
            finally
            {
                mapper.Stop(true);
            }
        }

        [TestMethod]
        public void RealMotionSurvivesCalibration()
        {
            UniversalMapper mapper = CreateGyroMapper();
            try
            {
                for (int i = 1; i <= 200; i++)
                {
                    mapper.ProcessSnapshot(GyroSnapshot(i,
                        DriftDegreesPerSecond, DriftDegreesPerSecond, DriftDegreesPerSecond));
                }

                // Now actually turn the controller: 90 deg/s of yaw on top of
                // the same drift. Calibration must remove the drift and leave
                // the movement.
                mapper.ProcessSnapshot(GyroSnapshot(201,
                    DriftDegreesPerSecond + 90.0, DriftDegreesPerSecond, DriftDegreesPerSecond));

                GyroEventFrame frame = mapper.LastGyroFrameForTest;
                Assert.AreEqual(90.0, frame.AngGyroYaw, 0.5,
                    "Calibration must subtract the resting bias, not the motion.");
                Assert.AreEqual(0.0, frame.AngGyroPitch, 0.05);
            }
            finally
            {
                mapper.Stop(true);
            }
        }

        [TestMethod]
        public void AngularAndLegacyFieldsAgreeAfterCalibration()
        {
            UniversalMapper mapper = CreateGyroMapper();
            try
            {
                for (int i = 1; i <= 200; i++)
                {
                    mapper.ProcessSnapshot(GyroSnapshot(i, DriftDegreesPerSecond, 0.0, 0.0));
                }

                mapper.ProcessSnapshot(GyroSnapshot(201, DriftDegreesPerSecond + 45.0, 0.0, 0.0));

                GyroEventFrame frame = mapper.LastGyroFrameForTest;
                double legacyAsDegrees = frame.GyroYaw /
                    UniversalMapper.LegacyGyroUnitsPerDegreePerSecond;

                // The two representations of the same reading must not disagree
                // about whether calibration has been applied.
                Assert.AreEqual(legacyAsDegrees, frame.AngGyroYaw, 0.1);
            }
            finally
            {
                mapper.Stop(true);
            }
        }

        [TestMethod]
        public void PreciseOffsetKeepsResolutionTheRoundedOneLoses()
        {
            GyroCalibration calibration = new GyroCalibration();

            // A mean that falls between whole device units. Rounding it away
            // leaves drift the user can still see.
            int[] samples = { 3, 4, 3, 4, 3, 4, 3, 3 };
            foreach (int sample in samples)
            {
                int yaw = sample, pitch = 0, roll = 0, ax = 0, ay = 0, az = 0;
                calibration.Update(ref yaw, ref pitch, ref roll, ref ax, ref ay, ref az);
            }

            Assert.AreEqual(3.375, calibration.GyroOffsetXPrecise, 0.0001);
            Assert.AreEqual(3, calibration.gyro_offset_x,
                "The rounded accessor is kept for the legacy integer path.");
        }

        private static UniversalMapper CreateGyroMapper()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                ControllerDisplayInfo.Unknown(),
                new[]
                {
                    new ControllerInputDescriptor(UniversalInputId.Gyroscope,
                        UniversalInputValueKind.Gyroscope, true, "Gyro", string.Empty,
                        new ControllerInputSource("sdl3", "1", "sensor:Gyro")),
                    new ControllerInputDescriptor(UniversalInputId.Accelerometer,
                        UniversalInputValueKind.Accelerometer, true, "Accel", string.Empty,
                        new ControllerInputSource("sdl3", "1", "sensor:Accel")),
                });

            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(Guid.NewGuid(), UniversalControllerBackendIds.Sdl3, "1",
                    new UniversalDeviceIdentity(UniversalControllerBackendIds.Sdl3, "1"),
                    DateTimeOffset.UtcNow),
                capabilities,
                UniversalControllerStateSnapshot.Disconnected());

            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            mapping.PopulateMappings();
            ProfileSerializer.EventInputMapper = mapping;

            UniversalMapper mapper = new UniversalMapper(controller, CreateGyroProfile());
            mapper.Start(new SilentVirtualKeyboard(), mapping);
            return mapper;
        }

        private static UniversalProfile CreateGyroProfile()
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = "gyro calibration",
                ProfileSettings = new JObject
                {
                    ["OutputGamepadSettings"] = new JObject { ["Enabled"] = false },
                },
            };

            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
            UniversalProfileActionLayer layer = new UniversalProfileActionLayer { Index = 0, Name = "Default" };
            layer.Actions.Add(new JObject
            {
                ["id"] = 1,
                ["type"] = "GyroMouseAction",
                ["payload"] = new JObject
                {
                    ["Id"] = 1,
                    ["ActionMode"] = "GyroMouseAction",
                },
            });
            set.Layers.Add(layer);
            profile.ActionSets.Add(set);
            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = UniversalInputId.Gyroscope,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.Gyroscope).ValueKind,
                Action = 1,
            });

            return profile;
        }

        // The universal layer carries gyro in radians per second, x = pitch,
        // y = yaw, z = roll.
        private static UniversalControllerStateSnapshot GyroSnapshot(
            long sequence, double yawDegrees, double pitchDegrees, double rollDegrees)
        {
            return new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow, sequence, true,
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [UniversalInputId.Gyroscope] = UniversalInputValue.Gyroscope(
                        pitchDegrees * DegreesToRadians,
                        yawDegrees * DegreesToRadians,
                        rollDegrees * DegreesToRadians),
                    [UniversalInputId.Accelerometer] =
                        UniversalInputValue.Accelerometer(0.0, 9.80665, 0.0),
                });
        }

        private sealed class SilentVirtualKeyboard : VirtualKBMBase
        {
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
            public override string GetDisplayName() => "silent";
            public override string GetIdentifier() => "silent";
            public override string GetFullDisplayName() => "silent";
        }
    }
}
