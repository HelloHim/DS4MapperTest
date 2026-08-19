using DS4MapperTest;
using DS4MapperTest.SdlDiagnostics;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using DS4MapperTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperUnitTests
{
    // Touchpad capacitive touch ("a finger rests on this pad") used to exist as a
    // universal input id and nothing else: no legacy binding, no capability, no
    // value. The Touchpad page's Touch Bindings tab therefore had nothing to show
    // and fell back to repeating each pad's mode settings. These tests pin the
    // wiring that makes those bindings real.
    [TestClass]
    public class TouchContactBindingTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ProfileSerializer.EventInputMapper = new SendInputMapping();
        }

        [TestMethod]
        public void TouchContactsAreBindableButtons()
        {
            foreach (UniversalInputId input in new[]
            {
                UniversalInputId.PrimaryTouchContact,
                UniversalInputId.LeftTouchContact,
                UniversalInputId.RightTouchContact,
            })
            {
                Assert.AreEqual(UniversalInputValueKind.DigitalButton,
                    UniversalInputCatalog.GetMetadata(input).ValueKind,
                    $"{input} carries an ordinary button binding.");
                Assert.IsTrue(UniversalLegacyBindingMap.TryGetBinding(input, out UniversalRuntimeBinding binding),
                    $"{input} needs a legacy binding id to be bindable at all.");
                Assert.AreEqual(InputBindingMeta.InputControlType.Button, binding.ControlType);
            }
        }

        [TestMethod]
        public void MigratedTouchContactBindingsKeepTheirLegacyIds()
        {
            // LegacyProfileMigration already writes these names, so changing them
            // would orphan every profile migrated before this point.
            Assert.IsTrue(UniversalLegacyBindingMap.TryGetUniversalInput("LeftPadTouch", out UniversalInputId left));
            Assert.AreEqual(UniversalInputId.LeftTouchContact, left);
            Assert.IsTrue(UniversalLegacyBindingMap.TryGetUniversalInput("RightPadTouch", out UniversalInputId right));
            Assert.AreEqual(UniversalInputId.RightTouchContact, right);
        }

        [TestMethod]
        public void ProfilesWrittenWithTheOldTouchContactValueKindStillLoad()
        {
            // valueKind is derived from the catalog rather than authored, so a file
            // written while touch contacts were still typed as touch surfaces has to
            // keep loading instead of locking the user out of the profile.
            string json = @"{
  ""schemaVersion"": 1,
  ""profileId"": ""11111111-2222-3333-4444-555555555555"",
  ""displayName"": ""Legacy Kind"",
  ""createdUtc"": ""2026-01-01T00:00:00.0000000+00:00"",
  ""profileSettings"": {},
  ""actionSets"": [
    { ""index"": 0, ""name"": ""Set 1"", ""layers"": [
      { ""index"": 0, ""name"": ""Default"", ""actions"": [
        { ""id"": 1, ""type"": ""ButtonAction"", ""payload"": { ""Id"": 1, ""ActionMode"": ""ButtonAction"" } }
      ] }
    ] }
  ],
  ""bindings"": [
    { ""actionSet"": 0, ""actionLayer"": 0, ""input"": ""left-touch-contact"", ""valueKind"": ""TouchSurface"", ""action"": 1, ""legacyInput"": ""LeftPadTouch"" }
  ]
}";

            UniversalProfile profile = UniversalProfileSerializer.Deserialize(json);

            Assert.AreEqual(1, profile.Bindings.Count);
            Assert.AreEqual(UniversalInputId.LeftTouchContact, profile.Bindings[0].Input);
            Assert.AreEqual(UniversalInputValueKind.DigitalButton, profile.Bindings[0].ValueKind);
        }

        [TestMethod]
        public void DualPadControllerReportsTouchContactsPerPad()
        {
            SdlRawGamepadInfo info = CreateDualPadSteamController();
            info.Touchpads[0].Fingers[0].Active = true;

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftTouchContact));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightTouchContact));
            Assert.IsTrue(state.Values[UniversalInputId.LeftTouchContact].Pressed);
            Assert.IsFalse(state.Values[UniversalInputId.RightTouchContact].Pressed);
        }

        [TestMethod]
        public void SinglePadControllerDerivesTouchContactPerHalf()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Buttons.Add(new SdlRawButtonState { Name = "Touchpad", Supported = true });
            info.Touchpads.Add(new SdlRawTouchpadState
            {
                TouchpadIndex = 0,
                FingerCapacity = 2,
                Fingers = new List<SdlRawTouchFingerState>
                {
                    new SdlRawTouchFingerState { FingerIndex = 0, Active = true, X = 0.75f, Y = 0.5f },
                    new SdlRawTouchFingerState { FingerIndex = 1, Active = false },
                },
            });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(state.Values[UniversalInputId.PrimaryTouchContact].Pressed);
            Assert.IsFalse(state.Values[UniversalInputId.LeftTouchContact].Pressed);
            Assert.IsTrue(state.Values[UniversalInputId.RightTouchContact].Pressed);
        }

        [TestMethod]
        public void SdlTouchpadYIsFlippedIntoPadOrientation()
        {
            // SDL reports fingers screen-style (Y grows downwards); the touchpad
            // actions read Y as growing towards the top of the pad. Without the
            // flip, a swipe up fired the Down binding.
            SdlRawGamepadInfo info = CreateDualPadSteamController();
            info.Touchpads[0].Fingers[0].Active = true;
            info.Touchpads[0].Fingers[0].X = 0.5f;
            info.Touchpads[0].Fingers[0].Y = 0.0f;

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            UniversalTouchContact contact = state.Values[UniversalInputId.LeftTouchSurface].Contacts[0];
            Assert.AreEqual(0.5, contact.X, 0.0001);
            Assert.AreEqual(1.0, contact.Y, 0.0001, "The top of the pad is Y = 1.");
            Assert.AreEqual(32767, UniversalLegacyBindingMap.ScaleTouchAxis(contact.Y),
                "The top of the pad has to reach the legacy axis maximum, which the pad actions read as up.");
        }

        [TestMethod]
        public void SteamController2026ExposesStickTouchAndGripSense()
        {
            SdlRawGamepadInfo info = CreateDualPadSteamController();
            info.Buttons.Single(item => item.Name == "Misc3").Pressed = true;
            info.Buttons.Single(item => item.Name == "Misc6").Pressed = true;

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftStickTouch));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightStickTouch));
            Assert.IsTrue(state.Values[UniversalInputId.LeftStickTouch].Pressed);
            Assert.IsFalse(state.Values[UniversalInputId.RightStickTouch].Pressed);
            Assert.IsFalse(state.Values[UniversalInputId.LeftGripTouch].Pressed);
            Assert.IsTrue(state.Values[UniversalInputId.RightGripTouch].Pressed);
        }

        [TestMethod]
        public void SteamController2026IsIdentifiedByUsbIdsWhenSdlOnlySaysSteamController()
        {
            Assert.IsTrue(SteamController2026Identity.IsSteamController2026(0x28DE, 0x1304));
            Assert.IsFalse(SteamController2026Identity.IsSteamController2026(0x28DE, 0x1102));
            Assert.IsFalse(SteamController2026Identity.IsSteamController2026(0x054C, 0x1304));
        }

        [TestMethod]
        public void TouchpadPanelListsLeftRightAndCentreTouchBindings()
        {
            UniversalProfile profile = new UniversalProfile { DisplayName = "Touch Bindings" };
            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            profile.ActionSets.Add(set);

            SdlRawGamepadInfo info = CreateDualPadSteamController();
            ControllerCapabilities capabilities = new SdlUniversalStateTranslator().CreateCapabilities(info);

            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(), "test", "1",
                    new UniversalDeviceIdentity("test", "1", vendorId: 0x28DE, productId: 0x1304),
                    DateTimeOffset.UtcNow),
                capabilities,
                UniversalControllerStateSnapshot.Disconnected());
            UniversalMapper mapper = new UniversalMapper(controller, profile.Clone());
            try
            {
                Assert.AreEqual(InputDeviceType.SteamControllerTriton, mapper.DeviceType,
                    "USB ids identify the 2026 Steam Controller even though SDL names it \"Steam Controller\".");

                ProfileEditorTestViewModel editorVM = new ProfileEditorTestViewModel(
                    mapper,
                    new ProfileEntity(string.Empty, "test", InputDeviceType.None),
                    mapper.ActionProfile);
                editorVM.Test();

                CollectionAssert.AreEquivalent(
                    new[] { "Left Touch", "Right Touch", "Center Touch" },
                    editorVM.TouchpadTouchBindings.Select(item => item.DisplayName).ToArray());
                Assert.IsTrue(editorVM.TouchpadTouchBindings
                    .Single(item => item.DisplayName == "Left Touch").IsAvailable);
                Assert.IsFalse(editorVM.TouchpadTouchBindings
                    .Single(item => item.DisplayName == "Center Touch").IsAvailable,
                    "A dual-pad controller has no centre pad, so that row stays read-only.");
            }
            finally
            {
                mapper.Stop(finalSync: false);
                controller.Dispose();
            }
        }

        private static SdlRawGamepadInfo CreateDualPadSteamController()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Name = "Steam Controller";
            info.VendorId = 0x28DE;
            info.ProductId = 0x1304;
            info.Buttons.Add(new SdlRawButtonState { Name = "Touchpad", Supported = true });
            foreach (string misc in new[] { "Misc1", "Misc2", "Misc3", "Misc4", "Misc5", "Misc6" })
            {
                info.Buttons.Add(new SdlRawButtonState { Name = misc, Supported = true });
            }

            for (int index = 0; index < 2; index++)
            {
                info.Touchpads.Add(new SdlRawTouchpadState
                {
                    TouchpadIndex = index,
                    FingerCapacity = 1,
                    Fingers = new List<SdlRawTouchFingerState>
                    {
                        new SdlRawTouchFingerState { FingerIndex = 0, Active = false, X = 0.5f, Y = 0.5f },
                    },
                });
            }

            return info;
        }

        private static SdlRawGamepadInfo CreateSdlDevice()
        {
            return new SdlRawGamepadInfo
            {
                InstanceId = 1,
                Name = "Synthetic SDL Pad",
                Guid = "guid-1",
                VendorId = 0x1234,
                ProductId = 0x5678,
                SerialNumber = string.Empty,
                IsMappedGamepad = true,
            };
        }
    }
}
