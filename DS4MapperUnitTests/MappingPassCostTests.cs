using DS4MapperTest;
using DS4MapperTest.SdlDiagnostics;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using System.Diagnostics;

namespace DS4MapperUnitTests
{
    // What one pass of the mapping loop costs decides what poll rate the app
    // can honestly offer. Reading the controller is only part of it; this is
    // the other part, with a real shipped profile rather than an empty one.
    [TestClass]
    public class MappingPassCostTests
    {
        private static string RepoRoot
        {
            get
            {
                string current = AppContext.BaseDirectory;
                while (!string.IsNullOrEmpty(current))
                {
                    if (Directory.Exists(Path.Combine(current, "template_profiles"))) return current;
                    current = Directory.GetParent(current)?.FullName;
                }

                throw new DirectoryNotFoundException("Could not locate template_profiles.");
            }
        }

        [TestMethod]
        public void FullMappingPassIsCheapEnoughForA1000HzLoop()
        {
            // The shipped templates are still in the legacy format, so migrate
            // one rather than hand-rolling a profile: this measures the cost of
            // the bindings a user actually gets by default.
            string legacyJson = File.ReadAllText(Path.Combine(RepoRoot, "template_profiles",
                "SteamControllerTriton", "Default - Desktop.json"));
            string scratchRoot = Path.Combine(Path.GetTempPath(),
                "ds4mapper-pass-cost-" + Guid.NewGuid().ToString("N"));
            ProfileMigrationReport report = new LegacyProfileMigrator(
                new UniversalProfileStore(scratchRoot)).Migrate(
                    new LegacyProfileMigrationSource(InputDeviceType.SteamControllerTriton,
                        "SteamControllerTriton/Default - Desktop.json", legacyJson));

            Assert.IsNotNull(report.Profile, $"Migration returned {report.Status}.");
            UniversalProfile profile = report.Profile;

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            SdlRawGamepadInfo info = CreateSteamController2026();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);

            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(Guid.NewGuid(), UniversalControllerBackendIds.Sdl3, "1",
                    new UniversalDeviceIdentity(UniversalControllerBackendIds.Sdl3, "1",
                        vendorId: 0x28DE, productId: 0x1304),
                    DateTimeOffset.UtcNow),
                capabilities,
                UniversalControllerStateSnapshot.Disconnected());

            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            mapping.PopulateMappings();
            // Compiling a profile with real key bindings goes through the
            // shared serializer, which needs a mapper before the constructor
            // below deserializes anything.
            ProfileSerializer.EventInputMapper = mapping;

            UniversalMapper mapper = new UniversalMapper(controller, profile.Clone());
            mapper.Start(new SilentVirtualKeyboard(), mapping);

            try
            {
                // Warm up so JIT and first-touch allocation are not measured.
                for (int i = 0; i < 2000; i++)
                {
                    mapper.ProcessSnapshot(translator.CreateState(info, capabilities, true,
                        i + 1, UniversalMonotonicClock.UtcNow));
                }

                const int passes = 20000;
                Stopwatch clock = Stopwatch.StartNew();
                for (int i = 0; i < passes; i++)
                {
                    mapper.ProcessSnapshot(translator.CreateState(info, capabilities, true,
                        i + 1, UniversalMonotonicClock.UtcNow));
                }

                double microseconds = clock.Elapsed.TotalMilliseconds * 1000.0 / passes;
                Console.WriteLine($"Full mapping pass: {microseconds:0.0} us");
                foreach (int hz in new[] { 125, 250, 500, 1000 })
                {
                    Console.WriteLine($"  at {hz,4} Hz -> {microseconds * hz / 10000.0:0.00}% of one core");
                }

                // A pass has to fit inside the 1 ms period of the fastest rate
                // the app offers, with room to spare. The bound is generous
                // because this runs on shared CI style hardware; the printed
                // figure is the number to look at.
                Assert.IsTrue(microseconds < 250.0,
                    $"A mapping pass took {microseconds:0.0} us, too slow to sustain 1000 Hz.");
            }
            finally
            {
                mapper.Stop(true);
                try { Directory.Delete(scratchRoot, recursive: true); } catch (IOException) { }
            }
        }

        // A fully populated 2026 Steam Controller: every button and axis, both
        // pads and both motion sensors. Measuring against a stripped down
        // device would understate the cost of a real pass.
        private static SdlRawGamepadInfo CreateSteamController2026()
        {
            SdlRawGamepadInfo info = new SdlRawGamepadInfo
            {
                InstanceId = 1,
                Name = "Steam Controller",
                Guid = "guid-sc2026",
                VendorId = 0x28DE,
                ProductId = 0x1304,
                SerialNumber = string.Empty,
                IsMappedGamepad = true,
            };

            foreach (string button in new[]
            {
                "South", "East", "West", "North", "DpadUp", "DpadDown", "DpadLeft", "DpadRight",
                "LeftShoulder", "RightShoulder", "LeftStick", "RightStick", "Start", "Back",
                "Guide", "Touchpad", "LeftPaddle1", "LeftPaddle2", "RightPaddle1", "RightPaddle2",
                "Misc1", "Misc2", "Misc3", "Misc4", "Misc5", "Misc6",
            })
            {
                info.Buttons.Add(new SdlRawButtonState { Name = button, Supported = true });
            }

            foreach (string axis in new[]
            {
                "LeftX", "LeftY", "RightX", "RightY", "LeftTrigger", "RightTrigger",
            })
            {
                info.Axes.Add(new SdlRawAxisState { Name = axis, Supported = true });
            }

            for (int index = 0; index < 2; index++)
            {
                info.Touchpads.Add(new SdlRawTouchpadState
                {
                    TouchpadIndex = index,
                    FingerCapacity = 1,
                    Fingers = new List<SdlRawTouchFingerState>
                    {
                        new SdlRawTouchFingerState { FingerIndex = 0, Active = true, X = 0.5f, Y = 0.5f },
                    },
                });
            }

            foreach (string sensor in new[] { "Gyro", "Accel" })
            {
                info.Sensors.Add(new SdlRawSensorState
                {
                    Name = sensor,
                    Supported = true,
                    Enabled = true,
                    EnableSucceeded = true,
                    // The rate measured off real hardware.
                    DataRateHz = 248f,
                    Values = new[] { 0.01f, 0.02f, 0.03f },
                });
            }

            return info;
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
