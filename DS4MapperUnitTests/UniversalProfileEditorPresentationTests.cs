using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalProfileEditorPresentationTests
    {
        [TestMethod]
        public void ModelOpensWithNoController()
        {
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(CreateProfile("No Controller"));

            Assert.IsNull(model.Capabilities);
            IReadOnlyList<UniversalInputPresentation> presentations = model.GetInputPresentations();
            Assert.IsTrue(presentations.Count > 0);
            Assert.IsTrue(presentations.All(item =>
                item.VisibilityState == EditorInputVisibilityState.SupportedBound ||
                item.VisibilityState == EditorInputVisibilityState.SupportedUnbound));
        }

        [TestMethod]
        public void ModelProjectsBindingsSupportLabelsAndGlyphsPerInput()
        {
            ControllerCapabilities xbox = XboxCapabilities();
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(CreateProfile("Projected"), xbox);

            UniversalInputPresentation south = model.GetInputPresentations()
                .Single(item => item.InputId == UniversalInputId.FaceButtonSouth);

            Assert.AreEqual(EditorInputVisibilityState.SupportedBound, south.VisibilityState);
            Assert.AreEqual("A", south.Label);
            Assert.IsTrue(south.IsSupportedByController);
            Assert.IsNotNull(south.Binding);
            Assert.AreEqual(1, south.Binding.Action);
            Assert.IsFalse(string.IsNullOrWhiteSpace(south.GlyphKey));
        }

        [TestMethod]
        public void EditingVisibleBindingUpdatesProjectionOnlyNotSourceProfile()
        {
            UniversalProfile original = CreateProfile("Mutate");
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(original);

            model.AssignBinding(UniversalInputId.RightShoulder, 1);

            Assert.IsTrue(model.BuildUpdatedProfile().Bindings.Any(item => item.Input == UniversalInputId.RightShoulder));
            Assert.IsFalse(original.Bindings.Any(item => item.Input == UniversalInputId.RightShoulder));
        }

        [TestMethod]
        public void UnsupportedButBoundBindingsAppearInPreservedSection()
        {
            UniversalProfile profile = CreateProfile("Preserved");
            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = UniversalInputId.Mute,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.Mute).ValueKind,
                Action = 1,
            });

            UniversalProfileEditorModel model = new UniversalProfileEditorModel(profile, XboxCapabilities());

            IReadOnlyList<UniversalInputPresentation> preserved = model.GetPreservedInputPresentations();
            Assert.IsTrue(preserved.Any(item => item.InputId == UniversalInputId.Mute));
            Assert.IsFalse(model.GetPrimaryInputPresentations().Any(item => item.InputId == UniversalInputId.Mute));
        }

        [TestMethod]
        public void RebuildingForAnotherControllerDoesNotMutateStoredProfile()
        {
            UniversalProfile profile = CreateProfile("Rebuild");
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(profile, XboxCapabilities());
            string beforeJson = UniversalProfileSerializer.Serialize(model.BuildUpdatedProfile());

            model.SetController(PlayStationCapabilities());
            model.SetController(NintendoCapabilities());
            model.SetController(null);

            string afterJson = UniversalProfileSerializer.Serialize(model.BuildUpdatedProfile());
            Assert.AreEqual(beforeJson, afterJson);
            Assert.IsFalse(profile.Bindings.Count != model.BuildUpdatedProfile().Bindings.Count);
        }

        [TestMethod]
        public void NoLabelOrGlyphKeyIsWrittenIntoAnyBinding()
        {
            string[] prohibited = { "Label", "Glyph" };
            foreach (PropertyInfo property in typeof(UniversalProfileBinding).GetProperties())
            {
                Assert.IsFalse(prohibited.Any(item => property.Name.Contains(item)),
                    $"UniversalProfileBinding.{property.Name} should not exist on a binding.");
            }
        }

        [TestMethod]
        public void UnsupportedSupportedUnboundSupportedBoundAndPreservedAreDistinguished()
        {
            UniversalProfile profile = CreateProfile("States");
            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = UniversalInputId.Mute,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.Mute).ValueKind,
                Action = 1,
            });

            UniversalProfileEditorModel model = new UniversalProfileEditorModel(profile, XboxCapabilities());
            Dictionary<UniversalInputId, EditorInputVisibilityState> byInput = model.GetInputPresentations()
                .ToDictionary(item => item.InputId, item => item.VisibilityState);

            Assert.AreEqual(EditorInputVisibilityState.SupportedBound, byInput[UniversalInputId.FaceButtonSouth]);
            Assert.AreEqual(EditorInputVisibilityState.SupportedUnbound, byInput[UniversalInputId.LeftShoulder]);
            Assert.AreEqual(EditorInputVisibilityState.UnsupportedPreserved, byInput[UniversalInputId.Mute]);
            Assert.AreEqual(EditorInputVisibilityState.UnsupportedNoBinding, byInput[UniversalInputId.Gyroscope]);
        }

        [TestMethod]
        public void SwitchingControllersRecomputesVisibilityWithoutChangingBindings()
        {
            UniversalProfile profile = CreateProfile("Switch");
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(profile, XboxCapabilities());

            EditorInputVisibilityState beforeTrigger = model.GetInputPresentations()
                .Single(item => item.InputId == UniversalInputId.LeftTrigger).VisibilityState;

            model.SetController(null);
            EditorInputVisibilityState afterGenericTrigger = model.GetInputPresentations()
                .Single(item => item.InputId == UniversalInputId.LeftTrigger).VisibilityState;

            Assert.AreEqual(EditorInputVisibilityState.SupportedBound, beforeTrigger);
            Assert.AreEqual(EditorInputVisibilityState.SupportedBound, afterGenericTrigger);
            Assert.AreEqual(2, model.BuildUpdatedProfile().Bindings.Count);
        }

        [TestMethod]
        public void NoControllerVisibilityUsesFullGenericPolicy()
        {
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(CreateProfile("Generic Policy"), null);

            Assert.IsFalse(model.GetInputPresentations().Any(item =>
                item.VisibilityState == EditorInputVisibilityState.UnsupportedNoBinding ||
                item.VisibilityState == EditorInputVisibilityState.UnsupportedPreserved));
        }

        [TestMethod]
        public void VisibilityComputationNeverMutatesBindings()
        {
            UniversalProfile profile = CreateProfile("Read Only Visibility");
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(profile, XboxCapabilities());

            int before = model.BuildUpdatedProfile().Bindings.Count;
            model.GetInputPresentations();
            model.GetPrimaryInputPresentations();
            model.GetPreservedInputPresentations();
            model.SetController(PlayStationCapabilities());
            model.GetInputPresentations();
            int after = model.BuildUpdatedProfile().Bindings.Count;

            Assert.AreEqual(before, after);
        }

        [TestMethod]
        public void RepresentativeFaceButtonsResolveToFamilyNativeLabels()
        {
            AssertFaceLabels(XboxCapabilities(), "A", "B", "X", "Y");
            AssertFaceLabels(PlayStationCapabilities(), "Cross", "Circle", "Square", "Triangle");
            AssertFaceLabels(NintendoCapabilities(), "B", "A", "Y", "X");
        }

        [TestMethod]
        public void NativeLabelsAreLimitedToFaceButtons()
        {
            ControllerCapabilities steamController = SteamControllerCapabilities();

            Assert.AreEqual("A", ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonSouth, steamController));
            Assert.AreEqual("B", ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonEast, steamController));
            Assert.AreEqual("Left Paddle 1", ControllerLabelProvider.GetLabel(UniversalInputId.LeftRearPrimary, steamController));
        }

        [TestMethod]
        public void ShouldersTriggersAndPaddlesUseGenericLabels()
        {
            ControllerCapabilities xbox = XboxCapabilities();

            Assert.AreEqual("Left Bumper", ControllerLabelProvider.GetLabel(UniversalInputId.LeftShoulder, xbox));
            Assert.AreEqual("Right Bumper", ControllerLabelProvider.GetLabel(UniversalInputId.RightShoulder, xbox));
            Assert.AreEqual("Left Trigger", ControllerLabelProvider.GetLabel(UniversalInputId.LeftTrigger, xbox));
            Assert.AreEqual("Right Trigger", ControllerLabelProvider.GetLabel(UniversalInputId.RightTrigger, xbox));
            Assert.AreEqual("Left Paddle 1", ControllerLabelProvider.GetLabel(UniversalInputId.LeftRearPrimary, xbox));
            Assert.AreEqual("Right Paddle 2", ControllerLabelProvider.GetLabel(UniversalInputId.RightRearSecondary, xbox));
        }

        [TestMethod]
        public void MiscLabelsUseOnlyTheHardcodedEvidenceTable()
        {
            Assert.AreEqual("Misc 1 (Share)", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton1, BuildCapabilities("Xbox Series X Controller", "xbox", UniversalInputId.MiscButton1)));
            Assert.AreEqual("Misc 1 (Microphone)", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton1, BuildCapabilities("DualSense Wireless Controller", "playstation", UniversalInputId.MiscButton1)));
            Assert.AreEqual("Misc 1 (Capture)", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton1, BuildCapabilities("Nintendo Switch Pro Controller", "nintendo", UniversalInputId.MiscButton1)));
            Assert.AreEqual("Misc 1", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton1, BuildCapabilities("Nintendo Switch Pro 2 Controller", "nintendo", UniversalInputId.MiscButton1)));
            Assert.AreEqual("Misc 3 (Left Trigger Click)", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton3, BuildCapabilities("GameCube Controller", "generic-sdl", UniversalInputId.MiscButton3)));
            Assert.AreEqual("Misc 2 (Right Trackpad Click)", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton2, BuildCapabilities("Steam Controller 2026 Triton", "steam", UniversalInputId.MiscButton2)));
            Assert.AreEqual("Misc 1 (QAM)", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton1, BuildCapabilities("Steam Controller", "steam", UniversalInputId.MiscButton1)));
            Assert.AreEqual("Misc 1", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton1, BuildCapabilities("Steam Controller", "steam-controller-2015", UniversalInputId.MiscButton1)));
            Assert.AreEqual("Misc 7", ControllerMiscLabelProvider.GetLabel(
                UniversalInputId.MiscButton7, BuildCapabilities("Unknown Controller", "generic-sdl", UniversalInputId.MiscButton7)));
        }

        [TestMethod]
        public void UnknownDevicesFallBackToAbxyFaceLabels()
        {
            ControllerCapabilities unknown = new ControllerCapabilities(
                ControllerDisplayInfo.Unknown(),
                new[]
                {
                    new ControllerInputDescriptor(UniversalInputId.FaceButtonSouth, UniversalInputValueKind.DigitalButton),
                });

            Assert.AreEqual("A", ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonSouth, unknown));
            Assert.AreEqual("A", ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonSouth, null));
        }

        [TestMethod]
        public void LabelsAreDeterministic()
        {
            ControllerCapabilities xbox = XboxCapabilities();
            string first = ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonSouth, xbox);
            string second = ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonSouth, xbox);
            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void GlyphKeysResolvePerFamilyAndAreDeterministic()
        {
            ControllerCapabilities xbox = XboxCapabilities();
            string key1 = ControllerGlyphProvider.GetGlyphKey(UniversalInputId.FaceButtonSouth, xbox);
            string key2 = ControllerGlyphProvider.GetGlyphKey(UniversalInputId.FaceButtonSouth, xbox);

            Assert.AreEqual(key1, key2);
            StringAssert.Contains(key1, "xbox");
        }

        [TestMethod]
        public void UnknownDevicesResolveToSafeGenericGlyphKeys()
        {
            string key = ControllerGlyphProvider.GetGlyphKey(UniversalInputId.FaceButtonSouth, null);
            StringAssert.Contains(key, ControllerGlyphProvider.GenericFallbackGlyphKey);
        }

        [TestMethod]
        public void MissingGlyphAssetsFallBackToTextWithoutError()
        {
            bool resolved = ControllerGlyphProvider.TryResolveImageResourcePath(
                ControllerGlyphProvider.GetGlyphKey(UniversalInputId.FaceButtonSouth, XboxCapabilities()),
                out string path);

            Assert.IsFalse(resolved);
            Assert.IsNull(path);
        }

        [TestMethod]
        public void ActionSetAndLayerEditingRoundTripsWithoutFabricatingBindings()
        {
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(CreateProfile("Structure"));

            int newSet = model.AddActionSet("Second Set");
            int newLayer = model.AddActionLayer(newSet, "Second Layer");
            model.RenameActionSet(newSet, "Renamed Set");
            model.RenameActionLayer(newSet, newLayer, "Renamed Layer");

            UniversalProfile updated = model.BuildUpdatedProfile();
            Assert.AreEqual(2, updated.ActionSets.Count);
            Assert.AreEqual("Renamed Set", updated.ActionSets.Single(item => item.Index == newSet).Name);
            Assert.AreEqual(2, updated.Bindings.Count);

            model.RemoveActionLayer(newSet, newLayer);
            model.RemoveActionSet(newSet);
            UniversalProfile afterRemoval = model.BuildUpdatedProfile();
            Assert.AreEqual(1, afterRemoval.ActionSets.Count);
            Assert.AreEqual(2, afterRemoval.Bindings.Count);
        }

        [TestMethod]
        public void UnsupportedActionTypesAreSurfacedNotDropped()
        {
            UniversalProfileEditorModel model = new UniversalProfileEditorModel(CreateProfile("Action Summary"));

            IReadOnlyList<UniversalActionSummary> actions = model.GetActionsInCurrentLayer();
            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual("ButtonAction", actions[0].ActionType);
            Assert.AreEqual(1, actions[0].ActionId);
        }

        [TestMethod]
        public void HardwareCalibrationIsNotPartOfTheEditorBindingSurface()
        {
            string[] calibrationRelated = { "Calib", "Deadzone", "Alias", "VendorId", "ProductId" };
            foreach (PropertyInfo property in typeof(UniversalProfileEditorModel).GetProperties())
            {
                Assert.IsFalse(calibrationRelated.Any(item => property.Name.Contains(item)),
                    $"UniversalProfileEditorModel.{property.Name} should not expose hardware calibration.");
            }
        }

        private static void AssertFaceLabels(ControllerCapabilities capabilities, string south, string east, string west, string north)
        {
            Assert.AreEqual(south, ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonSouth, capabilities));
            Assert.AreEqual(east, ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonEast, capabilities));
            Assert.AreEqual(west, ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonWest, capabilities));
            Assert.AreEqual(north, ControllerLabelProvider.GetLabel(UniversalInputId.FaceButtonNorth, capabilities));
        }

        internal static ControllerCapabilities XboxCapabilities()
        {
            UniversalInputId[] supported =
            {
                UniversalInputId.FaceButtonSouth, UniversalInputId.FaceButtonEast,
                UniversalInputId.FaceButtonWest, UniversalInputId.FaceButtonNorth,
                UniversalInputId.DPadUp, UniversalInputId.DPadDown, UniversalInputId.DPadLeft, UniversalInputId.DPadRight,
                UniversalInputId.LeftShoulder, UniversalInputId.RightShoulder,
                UniversalInputId.LeftTrigger, UniversalInputId.RightTrigger,
                UniversalInputId.LeftStick, UniversalInputId.RightStick,
                UniversalInputId.LeftStickClick, UniversalInputId.RightStickClick,
                UniversalInputId.Menu, UniversalInputId.View, UniversalInputId.System,
            };
            return BuildCapabilities("Xbox Wireless Controller", "xbox", supported);
        }

        internal static ControllerCapabilities PlayStationCapabilities()
        {
            UniversalInputId[] supported =
            {
                UniversalInputId.FaceButtonSouth, UniversalInputId.FaceButtonEast,
                UniversalInputId.FaceButtonWest, UniversalInputId.FaceButtonNorth,
                UniversalInputId.DPadUp, UniversalInputId.DPadDown, UniversalInputId.DPadLeft, UniversalInputId.DPadRight,
                UniversalInputId.LeftShoulder, UniversalInputId.RightShoulder,
                UniversalInputId.LeftTrigger, UniversalInputId.RightTrigger,
                UniversalInputId.LeftStick, UniversalInputId.RightStick,
                UniversalInputId.LeftStickClick, UniversalInputId.RightStickClick,
                UniversalInputId.Menu, UniversalInputId.View, UniversalInputId.System,
                UniversalInputId.PrimaryTouchSurface, UniversalInputId.PrimaryTouchSurfaceClick,
                UniversalInputId.Gyroscope, UniversalInputId.Accelerometer,
            };
            return BuildCapabilities("DualSense Wireless Controller", "playstation", supported);
        }

        internal static ControllerCapabilities NintendoCapabilities()
        {
            UniversalInputId[] supported =
            {
                UniversalInputId.FaceButtonSouth, UniversalInputId.FaceButtonEast,
                UniversalInputId.FaceButtonWest, UniversalInputId.FaceButtonNorth,
                UniversalInputId.DPadUp, UniversalInputId.DPadDown, UniversalInputId.DPadLeft, UniversalInputId.DPadRight,
                UniversalInputId.LeftShoulder, UniversalInputId.RightShoulder,
                UniversalInputId.LeftTrigger, UniversalInputId.RightTrigger,
                UniversalInputId.LeftStick, UniversalInputId.RightStick,
                UniversalInputId.LeftStickClick, UniversalInputId.RightStickClick,
                UniversalInputId.Menu, UniversalInputId.View, UniversalInputId.System, UniversalInputId.Capture,
                UniversalInputId.Gyroscope,
            };
            return BuildCapabilities("Nintendo Switch Pro Controller", "nintendo", supported);
        }

        internal static ControllerCapabilities SteamControllerCapabilities()
        {
            UniversalInputId[] supported =
            {
                UniversalInputId.FaceButtonSouth, UniversalInputId.FaceButtonEast,
                UniversalInputId.FaceButtonWest, UniversalInputId.FaceButtonNorth,
                UniversalInputId.DPadUp, UniversalInputId.DPadDown, UniversalInputId.DPadLeft, UniversalInputId.DPadRight,
                UniversalInputId.LeftShoulder, UniversalInputId.RightShoulder,
                UniversalInputId.LeftTrigger, UniversalInputId.RightTrigger,
                UniversalInputId.LeftStick, UniversalInputId.LeftStickClick,
                UniversalInputId.Menu, UniversalInputId.View, UniversalInputId.System,
                UniversalInputId.LeftRearPrimary, UniversalInputId.RightRearPrimary,
                UniversalInputId.LeftTouchSurface, UniversalInputId.RightTouchSurface,
                UniversalInputId.Gyroscope, UniversalInputId.Accelerometer,
            };

            ControllerDisplayInfo displayInfo = new ControllerDisplayInfo("Steam Controller", "steam-controller-2015", "steam-controller");
            List<ControllerInputDescriptor> descriptors = supported.Select(id => new ControllerInputDescriptor(
                id,
                UniversalInputCatalog.GetMetadata(id).ValueKind,
                true,
                NativeSteamControllerLabel(id),
                string.Empty,
                ControllerInputSource.None)).ToList();
            return new ControllerCapabilities(displayInfo, descriptors);
        }

        private static string NativeSteamControllerLabel(UniversalInputId inputId)
        {
            return inputId switch
            {
                UniversalInputId.LeftRearPrimary => "Left Grip",
                UniversalInputId.RightRearPrimary => "Right Grip",
                UniversalInputId.LeftTouchSurface => "Left Touchpad",
                UniversalInputId.RightTouchSurface => "Right Touchpad",
                UniversalInputId.System => "Steam",
                _ => UniversalInputCatalog.GetMetadata(inputId).DisplayName,
            };
        }

        private static ControllerCapabilities BuildCapabilities(string displayName, string family, params UniversalInputId[] supported)
        {
            ControllerDisplayInfo displayInfo = new ControllerDisplayInfo(displayName, family, family);
            List<ControllerInputDescriptor> descriptors = supported.Select(id => new ControllerInputDescriptor(
                id,
                UniversalInputCatalog.GetMetadata(id).ValueKind,
                true,
                string.Empty,
                string.Empty,
                ControllerInputSource.None)).ToList();
            return new ControllerCapabilities(displayInfo, descriptors);
        }

        internal static UniversalProfile CreateProfile(string name)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = name,
                CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
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
    }
}
