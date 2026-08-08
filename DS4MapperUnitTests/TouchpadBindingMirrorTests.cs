using System;
using System.Collections.Generic;
using System.Linq;
using DS4MapperTest;
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
                new string[] { "Left Touch", "Right Touch", "Main Press", "Left Press", "Right Press" },
                vm.TouchpadButtonBindings.Select(item => item.DisplayName).ToArray());

            AssertTouchpadButton(vm, "Left Touch", "LeftPadTouch");
            AssertTouchpadButton(vm, "Right Touch", "RightPadTouch");
            AssertTouchpadButton(vm, "Main Press", "TouchClick");

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
