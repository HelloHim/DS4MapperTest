using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperUnitTests
{
    // A profile that omits OutputGamepadSettings falls back to the virtual
    // Xbox 360 pad being enabled, so it is the ordinary "brand new profile"
    // shape that first drives the mapper into libVIIPER. If the VIIPER server
    // never started, entering the native library takes the whole process down
    // with a Go panic that no managed handler can catch, so these tests run the
    // switch with no server handle at all.
    [TestClass]
    public class VirtualOutputWithoutViiperServerTests
    {
        [TestInitialize]
        public void Setup()
        {
            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            mapping.PopulateMappings();
            ProfileSerializer.EventInputMapper = mapping;
        }

        [TestMethod]
        public void ProfileWithDefaultOutputGamepadStartsWithoutViiperServer()
        {
            UniversalMapper mapper = new UniversalMapper(
                CreateController(), CreateProfile(withOutputGamepadSettings: false));
            mapper.Start(new RecordingKbm(), CreateMapping());

            mapper.ProcessSnapshot(State(1, false));

            Assert.AreEqual((nuint)0, mapper.VIIPERDeviceHanle);
            mapper.Stop(true);
        }

        [TestMethod]
        public void SwitchToProfileWithDefaultOutputGamepadKeepsMapperAlive()
        {
            UniversalMapper mapper = new UniversalMapper(
                CreateController(), CreateProfile(withOutputGamepadSettings: true));
            mapper.Start(new RecordingKbm(), CreateMapping());
            mapper.ProcessSnapshot(State(1, false));

            mapper.ActivateProfile(CreateProfile(withOutputGamepadSettings: false));

            mapper.ProcessSnapshot(State(2, true));
            mapper.ProcessSnapshot(State(3, false));

            Assert.AreEqual((nuint)0, mapper.VIIPERDeviceHanle);
            Assert.AreEqual(0u, mapper.VIIPerBusId);
            mapper.Stop(true);
        }

        private static UniversalProfile CreateProfile(bool withOutputGamepadSettings)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = "no-viiper",
                CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                ProfileSettings = withOutputGamepadSettings
                    ? new JObject
                    {
                        ["OutputGamepadSettings"] = new JObject { ["Enabled"] = false },
                    }
                    : new JObject(),
            };

            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Main" };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            profile.ActionSets.Add(set);
            return profile;
        }

        private static FakerInputMapping CreateMapping()
        {
            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            mapping.PopulateMappings();
            return mapping;
        }

        private static UniversalController CreateController()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Synthetic Controller"),
                new[] { UniversalInputId.FaceButtonSouth, UniversalInputId.LeftStick }
                    .Select(input => new ControllerInputDescriptor(
                        input,
                        UniversalInputCatalog.GetMetadata(input).ValueKind,
                        true,
                        input.ToString(),
                        string.Empty,
                        new ControllerInputSource("test", input.ToString(), input.ToString()))));

            UniversalDeviceIdentity deviceIdentity = new UniversalDeviceIdentity(
                UniversalControllerBackendIds.Sdl3,
                "sdl-no-viiper",
                string.Empty,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                false);

            return new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    UniversalControllerBackendIds.Sdl3,
                    "sdl-no-viiper",
                    deviceIdentity,
                    DateTimeOffset.UtcNow),
                capabilities,
                State(0, false));
        }

        private static UniversalControllerStateSnapshot State(long sequence, bool pressed)
        {
            return new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow.AddMilliseconds(sequence * 8),
                sequence,
                true,
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(pressed),
                });
        }

        private sealed class RecordingKbm : VirtualKBMBase
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
            public override string GetDisplayName() => "recording";
            public override string GetIdentifier() => "recording";
            public override string GetFullDisplayName() => "recording";
        }
    }
}
