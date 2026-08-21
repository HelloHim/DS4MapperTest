using DS4MapperTest;
using DS4MapperTest.StickActions;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using DS4MapperTest.ViewModels;
using DS4MapperTest.ViewModels.StickActionPropViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reflection;

namespace DS4MapperUnitTests
{
    /// <summary>
    /// The Left/Right Stick Settings panels edit the action the running mapper owns, in
    /// place, with no intermediate model. Hands-on testing found ways that link could be
    /// cut without the panel showing anything was wrong, so they are pinned here.
    /// </summary>
    [TestClass]
    public class StickModeSettingsTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ProfileSerializer.EventInputMapper = new SendInputMapping();
        }

        // Switching a stick between the D-Pad and Analog Emulation layouts builds a
        // replacement action and copies the settings the two modes share onto it. Copying
        // only part of the Counter Movement Release Press configuration silently retuned it:
        // the press length mode reverted to the default while its ranges came across, so the
        // stick came back reading a duration the user never chose.
        [TestMethod]
        public void SwitchingToAnalogEmulationKeepsTheCounterMovementConfiguration()
        {
            using OfflineEditorFixture fixture = OfflineEditorFixture.OpenBlank();
            StickSideViewModel ls = fixture.EditorVM.LeftStickKeybinds;

            ls.SelectedModeIndex = DirectionalPadModeIndex;
            StickPadActionPropViewModel padVM = (StickPadActionPropViewModel)ls.SettingsViewModel;
            padVM.CounterMovementReleasePressEnabled = true;
            padVM.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;
            padVM.CounterPressLengthMinimumMs = 70;
            padVM.CounterPressLengthMaximumMs = 120;
            padVM.UseArrowKeysForCounterMovementPresses = true;
            padVM.Rotation = 12;
            padVM.DeadZone = "0.15";

            SelectAnalogEmulationLayout(padVM);

            StickAnalogEmulationAction analog = (StickAnalogEmulationAction)ls.CurrentAction;
            CounterMovementReleasePressProcessor carried = analog.CounterMovementReleasePress;
            Assert.IsTrue(carried.Enabled);
            Assert.AreEqual(CounterPressLengthMode.MinimumAndMaximum, carried.CounterPressLengthMode);
            Assert.AreEqual(70, carried.CounterPressLengthMinimumMs);
            Assert.AreEqual(120, carried.CounterPressLengthMaximumMs);
            Assert.IsTrue(carried.UseArrowKeysForCounterMovementPresses);
            Assert.AreEqual(12, analog.Rotation);
            Assert.AreEqual(0.15, analog.DeadMod.DeadZone, 0.0001);
        }

        // A serializer only writes a setting the action lists in ChangedProperties. The
        // replacement action built by the layout switch listed none of them, so everything
        // carried across was live in memory but missing from the next save: the profile
        // reloaded with Counter Movement Release Press off and the stick shaping reset,
        // while the panel had shown the settings on the whole time.
        [TestMethod]
        public void SwitchingToAnalogEmulationStoresTheCarriedSettings()
        {
            using OfflineEditorFixture fixture = OfflineEditorFixture.OpenBlank();
            StickSideViewModel ls = fixture.EditorVM.LeftStickKeybinds;

            ls.SelectedModeIndex = DirectionalPadModeIndex;
            StickPadActionPropViewModel padVM = (StickPadActionPropViewModel)ls.SettingsViewModel;
            padVM.CounterMovementReleasePressEnabled = true;
            padVM.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;
            padVM.CounterPressLengthMinimumMs = 70;
            padVM.CounterPressLengthMaximumMs = 120;
            padVM.Rotation = 12;

            SelectAnalogEmulationLayout(padVM);

            UniversalProfile saved = UniversalClassicProfileProjector.BuildUpdatedProfile(
                fixture.Mapper, fixture.Mapper.ActionProfile, fixture.SourceProfile);

            JObject settings = saved.ActionSets[0].Layers[0].Actions
                .First(item => item.Value<string>("type") == "StickAnalogEmulationAction")
                ["payload"]["Settings"] as JObject;

            Assert.IsNotNull(settings, "The switched action stored no settings at all.");
            Assert.AreEqual(true, settings.Value<bool>("CounterMovementReleasePressEnabled"));
            Assert.AreEqual("MinimumAndMaximum", settings.Value<string>("CounterPressLengthMode"));
            Assert.AreEqual(70, settings.Value<int>("CounterPressLengthMinimumMs"));
            Assert.AreEqual(120, settings.Value<int>("CounterPressLengthMaximumMs"));
            Assert.AreEqual(12, settings.Value<int>("Rotation"));
        }

        private const int DirectionalPadModeIndex = 2;

        private static void SelectAnalogEmulationLayout(StickPadActionPropViewModel padVM)
        {
            padVM.SelectedPadModeIndex = padVM.PadModeItems.FindIndex(
                item => item.DPadMode == StickPadAction.DPadMode.AnalogEmulation);
        }

        // Compiles a blank universal profile into a UniversalMapper that is never registered
        // with BackendManager and never fed snapshots, so the classic editor's stick panels
        // can be driven without a controller.
        private sealed class OfflineEditorFixture : IDisposable
        {
            private const string OfflineBackendId = "offline-stick-settings-test";

            private readonly UniversalController offlineController;

            private OfflineEditorFixture(
                UniversalMapper mapper,
                UniversalController offlineController,
                UniversalProfile sourceProfile,
                ProfileEditorTestViewModel editorVM)
            {
                Mapper = mapper;
                this.offlineController = offlineController;
                SourceProfile = sourceProfile;
                EditorVM = editorVM;
            }

            public UniversalMapper Mapper { get; }
            public UniversalProfile SourceProfile { get; }
            public ProfileEditorTestViewModel EditorVM { get; }

            public static OfflineEditorFixture OpenBlank()
            {
                UniversalProfile profile = new UniversalProfile { DisplayName = "Stick Settings" };
                UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
                set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
                profile.ActionSets.Add(set);

                ControllerCapabilities allInputsSupported = new ControllerCapabilities(
                    ControllerDisplayInfo.Unknown(),
                    UniversalInputCatalog.All.Select(metadata =>
                        new ControllerInputDescriptor(metadata.Id, metadata.ValueKind, isSupported: true)));

                UniversalDeviceIdentity deviceIdentity = new UniversalDeviceIdentity(
                    OfflineBackendId, Guid.NewGuid().ToString("N"));
                UniversalControllerIdentity identity = new UniversalControllerIdentity(
                    Guid.NewGuid(), OfflineBackendId, deviceIdentity.BackendSessionId,
                    deviceIdentity, DateTimeOffset.UtcNow);

                UniversalController offlineController = new UniversalController(
                    identity, allInputsSupported, UniversalControllerStateSnapshot.Disconnected());
                UniversalMapper mapper = new UniversalMapper(offlineController, profile.Clone());

                // Switching a stick to a mode that binds keyboard directions reads the
                // mapper's keyboard mapping, which Mapper.Start would normally supply.
                typeof(Mapper)
                    .GetField("eventInputMapping", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(mapper, new SendInputMapping());

                ProfileEditorTestViewModel editorVM = new ProfileEditorTestViewModel(
                    mapper,
                    new ProfileEntity(string.Empty, "test", InputDeviceType.None),
                    mapper.ActionProfile);
                editorVM.Test();

                return new OfflineEditorFixture(mapper, offlineController, profile, editorVM);
            }

            public void Dispose()
            {
                Mapper?.Stop(finalSync: false);
                offlineController?.Dispose();
            }
        }
    }
}
