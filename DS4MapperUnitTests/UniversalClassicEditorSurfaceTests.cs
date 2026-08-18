using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using DS4MapperTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace DS4MapperUnitTests
{
    // The classic editor panels are driven by a Mapper compiled from a
    // universal profile rather than by a controller-specific mapper, so the
    // binding ids they see are universal ones ("FaceButtonSouth") instead of
    // the old per-family ones ("A"/"Cross"). These tests pin the alias
    // handling and the panel population that hands-on testing broke.
    [TestClass]
    public class UniversalClassicEditorSurfaceTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ProfileSerializer.EventInputMapper = new SendInputMapping();
        }

        [TestMethod]
        public void LegacyFaceButtonPanelRecognisesUniversalBindingIds()
        {
            UniversalProfile profile = CreateProfileWithFunctions("Face Alias");

            using OfflineUniversalProfileFixture fixture = OfflineUniversalProfileFixture.Open(profile);

            ProfileEditorTestViewModel editorVM = CreateEditorViewModel(fixture);

            // Before the alias fix, this was always empty for a UniversalMapper-backed
            // profile because the panel only recognised legacy per-family ids ("A"/"Cross")
            // while UniversalLegacyBindingMap emits universal ids ("FaceButtonSouth").
            Assert.IsTrue(editorVM.FaceButtonBindings.Count > 0,
                "Face button panel should surface the profile's FaceButtonSouth binding.");
        }

        [TestMethod]
        public void LegacyDPadPanelRecognisesUniversalDPadBindings()
        {
            UniversalProfile profile = CreateProfileWithFunctions("DPad Alias");
            UniversalProfileActionLayer layer = profile.ActionSets[0].Layers[0];
            layer.Actions.Add(new JObject
            {
                ["id"] = 2,
                ["type"] = "DPadTranslateAction",
                ["payload"] = new JObject
                {
                    ["Id"] = 2,
                    ["Name"] = "DPad Action",
                    ["ActionMode"] = "DPadTranslateAction",
                    ["Settings"] = new JObject
                    {
                        ["OutputDPad"] = "X360_DPAD",
                    },
                },
            });

            foreach (UniversalInputId input in new[]
            {
                UniversalInputId.DPadUp,
                UniversalInputId.DPadDown,
                UniversalInputId.DPadLeft,
                UniversalInputId.DPadRight,
            })
            {
                profile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = input,
                    ValueKind = UniversalInputCatalog.GetMetadata(input).ValueKind,
                    Action = 2,
                });
            }

            using OfflineUniversalProfileFixture fixture = OfflineUniversalProfileFixture.Open(profile);

            ProfileEditorTestViewModel editorVM = CreateEditorViewModel(fixture);

            Assert.IsTrue(editorVM.HasDPadBindings,
                "D-pad panel should surface the universal D-pad binding as the classic DPad control.");
            Assert.AreEqual("DPad", editorVM.DPadBindings[0].BindingName);
        }

        [TestMethod]
        public void BlankUniversalProfileStillShowsEditableDPad()
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = "Blank DPad",
            };
            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Main" };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            profile.ActionSets.Add(set);

            using OfflineUniversalProfileFixture fixture = OfflineUniversalProfileFixture.Open(profile);

            ProfileEditorTestViewModel editorVM = CreateEditorViewModel(fixture);

            Assert.IsTrue(editorVM.HasDPadBindings);
            Assert.AreEqual("DPad", editorVM.DPadBindings[0].BindingName);
        }

        [TestMethod]
        public void ProcessMappingChangeActionDoesNotThrowWithNoBaseReader()
        {
            UniversalProfile profile = CreateProfileWithFunctions("No Reader");

            using OfflineUniversalProfileFixture fixture = OfflineUniversalProfileFixture.Open(profile);

            Assert.IsNull(fixture.Mapper.BaseReader);

            bool ranInline = false;
            fixture.Mapper.ProcessMappingChangeAction(() => { ranInline = true; });

            Assert.IsTrue(ranInline, "The action must still run when there is no device reader to halt.");
        }

        [TestMethod]
        public void AddSetAndLayerMutateOfflineActionProfile()
        {
            UniversalProfile profile = CreateProfileWithFunctions("Sets And Layers");

            using OfflineUniversalProfileFixture fixture = OfflineUniversalProfileFixture.Open(profile);

            ProfileEditorTestViewModel editorVM = CreateEditorViewModel(fixture);

            int setIndex = editorVM.AddSet();
            int layerIndex = editorVM.AddLayer();

            Assert.AreEqual(1, setIndex);
            Assert.AreEqual(1, layerIndex);
            Assert.AreEqual(2, fixture.Mapper.ActionProfile.ActionSets.Count);
            Assert.AreEqual(2, fixture.Mapper.ActionProfile.CurrentActionSet.ActionLayers.Count);
            Assert.IsTrue(editorVM.IsProfileDirty);
        }

        [TestMethod]
        public void TouchpadSurfaceRowsShowLeftRightAndCentre()
        {
            UniversalProfile profile = CreateProfileWithFunctions("Touch Surfaces");
            AddNoAction(profile, UniversalInputId.LeftTouchSurface, 2);
            AddNoAction(profile, UniversalInputId.RightTouchSurface, 3);
            AddNoAction(profile, UniversalInputId.PrimaryTouchSurface, 4);
            AddNoAction(profile, UniversalInputId.LeftTouchSurfaceClick, 5);
            AddNoAction(profile, UniversalInputId.RightTouchSurfaceClick, 6);

            using OfflineUniversalProfileFixture fixture = OfflineUniversalProfileFixture.Open(profile);

            ProfileEditorTestViewModel editorVM = CreateEditorViewModel(fixture);

            CollectionAssert.AreEqual(
                new[] { "LeftTouchSurface", "RightTouchSurface", "PrimaryTouchSurface" },
                editorVM.TouchpadTouchSurfaceBindings
                    .Select(item => item.BindingName)
                    .ToArray());
            Assert.AreEqual("Center Touchpad", editorVM.TouchpadTouchSurfaceBindings[2].DisplayName);
            Assert.IsNotNull(editorVM.TouchpadTouchSurfaceBindings[0].TouchpadClickBinding);
            Assert.IsNotNull(editorVM.TouchpadTouchSurfaceBindings[1].TouchpadClickBinding);
        }

        private static ProfileEditorTestViewModel CreateEditorViewModel(OfflineUniversalProfileFixture fixture)
        {
            ProfileEditorTestViewModel editorVM = new ProfileEditorTestViewModel(
                fixture.Mapper,
                new ProfileEntity(string.Empty, "test", InputDeviceType.None),
                fixture.Mapper.ActionProfile);
            editorVM.Test();
            return editorVM;
        }

        private static UniversalProfile CreateProfileWithFunctions(string name)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = name,
            };

            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
            UniversalProfileActionLayer layer = new UniversalProfileActionLayer { Index = 0, Name = "Default" };
            layer.Actions.Add(new JObject
            {
                ["id"] = 1,
                ["type"] = "ButtonAction",
                ["payload"] = new JObject
                {
                    ["Id"] = 1,
                    ["ActionMode"] = "ButtonAction",
                    ["Functions"] = new JArray(new JObject
                    {
                        ["Type"] = "NormalPress",
                        ["OutputActions"] = new JArray(new JObject
                        {
                            ["Type"] = "Keyboard",
                            ["Code"] = "Space",
                        }),
                    }),
                },
            });
            set.Layers.Add(layer);
            profile.ActionSets.Add(set);

            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = UniversalInputId.FaceButtonSouth,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.FaceButtonSouth).ValueKind,
                Action = 1,
            });
            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = UniversalInputId.LeftTrigger,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.LeftTrigger).ValueKind,
                Action = 1,
            });

            return profile;
        }

        private static void AddNoAction(
            UniversalProfile profile,
            UniversalInputId input,
            int actionId)
        {
            UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(input);
            string type = metadata.ValueKind switch
            {
                UniversalInputValueKind.AnalogAxis1D => "TriggerNoAction",
                UniversalInputValueKind.Stick2D => "StickNoAction",
                UniversalInputValueKind.TouchSurface => "TouchPassthruAction",
                UniversalInputValueKind.Gyroscope => "GyroPassthruAction",
                UniversalInputValueKind.Accelerometer => "GyroPassthruAction",
                _ => "ButtonNoAction",
            };

            profile.ActionSets[0].Layers[0].Actions.Add(new JObject
            {
                ["id"] = actionId,
                ["type"] = type,
                ["payload"] = new JObject
                {
                    ["Id"] = actionId,
                    ["ActionMode"] = type,
                },
            });
            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = input,
                ValueKind = metadata.ValueKind,
                Action = actionId,
            });
        }

        // Compiles a universal profile into a UniversalMapper that is never
        // registered with BackendManager and never fed live snapshots, so the
        // classic editor view models can be exercised without a controller.
        private sealed class OfflineUniversalProfileFixture : IDisposable
        {
            private const string OfflineBackendId = "offline-editor-test";

            private readonly UniversalController offlineController;

            private OfflineUniversalProfileFixture(UniversalMapper mapper, UniversalController offlineController)
            {
                Mapper = mapper;
                this.offlineController = offlineController;
            }

            public UniversalMapper Mapper { get; }

            public static OfflineUniversalProfileFixture Open(UniversalProfile profile)
            {
                ControllerCapabilities allInputsSupported = new ControllerCapabilities(
                    ControllerDisplayInfo.Unknown(),
                    UniversalInputCatalog.All.Select(metadata =>
                        new ControllerInputDescriptor(metadata.Id, metadata.ValueKind, isSupported: true)));

                UniversalDeviceIdentity deviceIdentity = new UniversalDeviceIdentity(
                    OfflineBackendId,
                    Guid.NewGuid().ToString("N"));
                UniversalControllerIdentity identity = new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    OfflineBackendId,
                    deviceIdentity.BackendSessionId,
                    deviceIdentity,
                    DateTimeOffset.UtcNow);

                UniversalController offlineController = new UniversalController(
                    identity, allInputsSupported, UniversalControllerStateSnapshot.Disconnected());

                return new OfflineUniversalProfileFixture(
                    new UniversalMapper(offlineController, profile.Clone()),
                    offlineController);
            }

            public void Dispose()
            {
                Mapper?.Stop(finalSync: false);
                offlineController?.Dispose();
            }
        }
    }
}
