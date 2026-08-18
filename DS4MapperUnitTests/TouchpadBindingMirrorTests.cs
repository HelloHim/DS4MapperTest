using System;
using System.Collections.Generic;
using System.Linq;
using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using DS4MapperTest.ViewModels;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class TouchpadBindingMirrorTests : BindingHelperBase
    {
        [TestMethod]
        public void TouchpadButtons_AreSharedBetweenKeybindAndTouchpadViews()
        {
            Profile profile = new Profile();
            profile.Name = "TouchpadMirror";
            profile.ActionSets[0].ActionLayers[0].Name = "Default";
            mapper = new TestMapper(profile);
            AddButtonBinding("LeftPadTouch", "Left Pad Touch");
            AddButtonBinding("RightPadTouch", "Right Pad Touch");
            AddButtonBinding("TouchClick", "Touch Click");

            PrepareDefaultLayerForBindingHelper(profile);
            FillMappingProfileInitialData(profile, null);
            SyncActionData(profile);

            mapper.EditActionSet = profile.ActionSets[0];
            mapper.EditLayer = profile.ActionSets[0].ActionLayers[0];

            ProfileEditorTestViewModel vm = new ProfileEditorTestViewModel(
                mapper,
                new ProfileEntity("", "TouchpadMirror", InputDeviceType.SteamController),
                profile);
            vm.Test();

            CollectionAssert.AreEquivalent(
                new string[] { "Left Touch", "Right Touch", "Center Touch", "Left Press", "Right Press", "Center Press" },
                vm.TouchpadButtonBindings.Select(item => item.DisplayName).ToArray());

            AssertTouchpadButton(vm, "Left Touch", "LeftPadTouch");
            AssertTouchpadButton(vm, "Right Touch", "RightPadTouch");
            FaceButtonBindingItem centerTouch = AssertTouchpadButton(vm, "Center Touch", "CenterTouch");
            FaceButtonBindingItem centerPress = AssertTouchpadButton(vm, "Center Press", "TouchClick");
            Assert.IsFalse(centerTouch.IsAvailable);
            Assert.IsFalse(centerPress.IsAvailable);

            FaceButtonBindingItem leftClick = AssertTouchpadButton(vm, "Left Press", "LeftPadClick");
            FaceButtonBindingItem rightClick = AssertTouchpadButton(vm, "Right Press", "RightPadClick");
            Assert.AreSame(leftClick,
                vm.TouchpadBindings.First(item => item.BindingName == "LeftTouchpad").TouchpadClickBinding);
            Assert.AreSame(rightClick,
                vm.TouchpadBindings.First(item => item.BindingName == "RightTouchpad").TouchpadClickBinding);

            CollectionAssert.DoesNotContain(
                vm.ExtraButtonBindings.Select(item => item.DisplayName).ToArray(),
                "Left Touch");
            CollectionAssert.DoesNotContain(
                vm.ExtraButtonBindings.Select(item => item.DisplayName).ToArray(),
                "Right Touch");
        }

        [TestMethod]
        public void TouchpadSidebarEligibilityIsLimitedToPlayStationAndSteamFamilies()
        {
            AssertTouchpadEligibility(InputDeviceType.DualSense, true, true);
            AssertTouchpadEligibility(InputDeviceType.SteamController, true, false);
            AssertTouchpadEligibility(InputDeviceType.SteamControllerTriton, true, false);
            AssertTouchpadEligibility(InputDeviceType.SwitchPro, false, false);
            AssertTouchpadEligibility(InputDeviceType.None, false, false);
        }

        [TestMethod]
        public void GyroSidebarEligibilityFollowsUniversalCapabilities()
        {
            ProfileEditorTestViewModel noGyro =
                CreateUniversalEditorVm("No Gyro", UniversalInputId.FaceButtonSouth);
            ProfileEditorTestViewModel withGyro =
                CreateUniversalEditorVm("With Gyro", UniversalInputId.FaceButtonSouth,
                    UniversalInputId.Gyroscope);

            Assert.IsFalse(noGyro.HasSupportedGyroHardware);
            Assert.IsTrue(withGyro.HasSupportedGyroHardware);
        }

        private void AddButtonBinding(string id, string displayName)
        {
            if (mapper.BindingDict.ContainsKey(id))
            {
                return;
            }

            InputBindingMeta meta =
                new InputBindingMeta(id, displayName, InputBindingMeta.InputControlType.Button);
            mapper.BindingList.Add(meta);
            mapper.BindingDict.Add(id, meta);
        }

        private static void PrepareDefaultLayerForBindingHelper(Profile profile)
        {
            foreach (ActionSet set in profile.ActionSets)
            {
                foreach (ActionLayer layer in set.ActionLayers)
                {
                    layer.actionSetActionDict.Clear();
                }
            }
        }

        private static void AssertTouchpadEligibility(
            InputDeviceType deviceType,
            bool expectedSupported,
            bool expectedCenter)
        {
            Profile profile = new Profile { Name = $"Touchpad-{deviceType}" };
            profile.ActionSets[0].ActionLayers[0].Name = "Default";
            TestMapper mapper = new TestMapper(profile)
            {
                DeviceTypeOverride = deviceType,
            };
            ProfileEditorTestViewModel vm = new ProfileEditorTestViewModel(
                mapper,
                new ProfileEntity("", profile.Name, deviceType),
                profile);

            Assert.AreEqual(expectedSupported, vm.HasSupportedTouchpadHardware, $"{deviceType} touchpad tab eligibility.");
            Assert.AreEqual(expectedCenter, vm.HasCenterTouchpad, $"{deviceType} centre touchpad eligibility.");
        }

        private static ProfileEditorTestViewModel CreateUniversalEditorVm(
            string profileName,
            params UniversalInputId[] inputs)
        {
            UniversalProfile profile = new UniversalProfile { DisplayName = profileName };
            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Default" };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            profile.ActionSets.Add(set);

            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Synthetic Controller"),
                inputs.Select(input => new ControllerInputDescriptor(
                    input,
                    UniversalInputCatalog.GetMetadata(input).ValueKind,
                    true,
                    input.ToString(),
                    string.Empty,
                    new ControllerInputSource("test", input.ToString(), input.ToString()))));

            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    UniversalControllerBackendIds.Sdl3,
                    profileName,
                    new UniversalDeviceIdentity("test", profileName),
                    DateTimeOffset.UtcNow),
                capabilities,
                new UniversalControllerStateSnapshot(
                    DateTimeOffset.UtcNow,
                    1,
                    true,
                    new Dictionary<UniversalInputId, UniversalInputValue>()));

            UniversalMapper mapper = new UniversalMapper(controller, profile);
            return new ProfileEditorTestViewModel(
                mapper,
                new ProfileEntity(string.Empty, profileName, InputDeviceType.None),
                mapper.ActionProfile);
        }

        private static FaceButtonBindingItem AssertTouchpadButton(
            ProfileEditorTestViewModel vm, string displayName, string bindingName)
        {
            FaceButtonBindingItem item =
                vm.TouchpadButtonBindings.FirstOrDefault(binding =>
                    binding.DisplayName == displayName);
            Assert.IsNotNull(item, $"{displayName} was not present.");
            Assert.AreEqual(bindingName, item.BindingName);
            return item;
        }
    }
}
