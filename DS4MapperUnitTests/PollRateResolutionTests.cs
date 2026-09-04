using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;

namespace DS4MapperUnitTests
{
    // End to end over the decision the mapping loop actually asks for: given
    // what the connected controllers report, and the user's ceiling, what rate
    // does the loop run at.
    [TestClass]
    public class PollRateResolutionTests
    {
        [TestMethod]
        public void MeasuredDeviceRateDrivesTheLoopRate()
        {
            using UniversalMappingRuntime runtime = CreateRuntime(reportRateHz: 248.0);

            double rate = runtime.ResolvePollRateHz(out bool limitedByCap);

            Assert.AreEqual(496.0, rate, 0.01,
                "A 248 Hz controller should be polled at twice its rate.");
            Assert.IsFalse(limitedByCap);
        }

        [TestMethod]
        public void SlowOrUnmeasuredDevicesStillGetTheOldFixedRate()
        {
            using UniversalMappingRuntime runtime = CreateRuntime(reportRateHz: null);

            Assert.AreEqual(UniversalMappingRuntime.MinimumPollRateHz,
                runtime.ResolvePollRateHz(out _), 0.01,
                "A device that cannot be measured must not poll slower than it used to.");
        }

        [TestMethod]
        public void VeryFastHardwareIsHeldAtTheAbsoluteCeiling()
        {
            using UniversalMappingRuntime runtime = CreateRuntime(reportRateHz: 4000.0);

            Assert.AreEqual(UniversalMappingRuntime.MaximumPollRateHz,
                runtime.ResolvePollRateHz(out _), 0.01);
        }

        [TestMethod]
        public void UserCapLowersTheRateAndSaysSo()
        {
            using UniversalMappingRuntime runtime = CreateRuntime(reportRateHz: 248.0);
            runtime.PollRateCapHz = 250.0;

            double rate = runtime.ResolvePollRateHz(out bool limitedByCap);

            Assert.AreEqual(250.0, rate, 0.01);
            Assert.IsTrue(limitedByCap,
                "The UI has to be able to tell the user the ceiling is what decided this.");
        }

        [TestMethod]
        public void CapAboveWhatTheHardwareNeedsChangesNothing()
        {
            using UniversalMappingRuntime runtime = CreateRuntime(reportRateHz: 125.0);
            runtime.PollRateCapHz = UniversalMappingRuntime.MaximumPollRateHz;

            double rate = runtime.ResolvePollRateHz(out bool limitedByCap);

            Assert.AreEqual(250.0, rate, 0.01);
            Assert.IsFalse(limitedByCap);
        }

        private static UniversalMappingRuntime CreateRuntime(double? reportRateHz)
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                ControllerDisplayInfo.Unknown(),
                new[]
                {
                    new ControllerInputDescriptor(UniversalInputId.FaceButtonSouth,
                        UniversalInputValueKind.DigitalButton, true, "A", string.Empty,
                        new ControllerInputSource("fake", "1", "button:South")),
                });

            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(Guid.NewGuid(), "fake", "1",
                    new UniversalDeviceIdentity("fake", "1"), DateTimeOffset.UtcNow),
                capabilities,
                UniversalControllerStateSnapshot.Disconnected());
            controller.PublishState(new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow, 1, true,
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(false),
                }));
            controller.PublishReportRate(reportRateHz);

            UniversalProfile profile = new UniversalProfile { DisplayName = "poll rate" };
            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            profile.ActionSets.Add(set);

            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            mapping.PopulateMappings();
            ProfileSerializer.EventInputMapper = mapping;

            UniversalMappingRuntime runtime = new UniversalMappingRuntime(
                new UniversalControllerManager(new[] { new SingleControllerBackend(controller) }),
                new FixedProfileSelector(profile),
                new SilentVirtualKeyboard(),
                mapping);
            runtime.Start();
            return runtime;
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

        private sealed class FixedProfileSelector : IUniversalProfileSelector
        {
            private readonly UniversalProfile profile;

            public FixedProfileSelector(UniversalProfile profile) => this.profile = profile;

            public UniversalProfile SelectProfile(IUniversalController controller) =>
                profile?.Clone();
        }

        private sealed class SingleControllerBackend : IUniversalControllerBackend
        {
            private readonly IUniversalController[] controllers;

            public SingleControllerBackend(IUniversalController controller)
            {
                controllers = new[] { controller };
            }

            public string BackendName => "fake";
            public IReadOnlyList<IUniversalController> Controllers => controllers;
            public event EventHandler ControllersChanged;

            public bool Start(out string error)
            {
                error = string.Empty;
                ControllersChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }

            public void Refresh() { }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
