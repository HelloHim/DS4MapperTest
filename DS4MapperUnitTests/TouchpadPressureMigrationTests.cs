using System.Linq;
using DS4MapperTest;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.TriggerActions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    // Covers the legacy Regular Press -> Full Press migration for Steam Controller 2
    // touchpad clicks, and save/reload persistence of the new TouchpadPressureDualStageAction
    // format. Uses the same ProfileSerializer + FillMappingProfileInitialData/SyncActionData
    // pattern as MappingTests.cs, since Mapper.ReadFromProfile() itself is private and reads
    // from a file path.
    [TestClass]
    public class TouchpadPressureMigrationTests : BindingHelperBase
    {
        public TouchpadPressureMigrationTests()
        {
            ProfileSerializer.EventInputMapper = new SendInputMapping();
        }

        private const string LegacyLeftClickProfileJson = @"{
  ""Name"": ""LegacyTouchpadProfile"",
  ""ActionSets"": [
    {
      ""Index"": ""0"",
      ""Name"": ""Set 1"",
      ""ActionLayers"": [
        {
          ""Index"": ""0"",
          ""Name"": ""Default"",
          ""MappedActions"": [
            {
              ""Id"": ""0"",
              ""Name"": ""Left Click"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                {
                  ""Type"": ""NormalPress"",
                  ""OutputActions"": [
                    { ""Type"": ""Keyboard"", ""Code"": ""A"" }
                  ]
                },
                {
                  ""Type"": ""HoldPress"",
                  ""DurationMs"": 500,
                  ""OutputActions"": [
                    { ""Type"": ""Keyboard"", ""Code"": ""B"" }
                  ]
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  ""Mappings"": [
    {
      ""ActionSet"": 0,
      ""ActionLayer"": 0,
      ""InputMappings"": [
        { ""Input"": ""LeftPadClick"", ""Action"": 0 }
      ]
    }
  ]
}";

        private static Profile LoadProfile(string json, out System.Collections.Generic.List<ProfileActionsMapping> tempMappings)
        {
            Profile tempProfile = new Profile();
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(json, profileSerializer);
            profileSerializer.PopulateProfile();
            tempProfile.ResetAliases();
            tempMappings = profileSerializer.ActionMappings;

            return tempProfile;
        }

        [TestMethod]
        public void LegacyRegularPress_MigratesToFullPressWithDefaults()
        {
            mapper = new TestMapper { DeviceTypeOverride = InputDeviceType.SteamControllerTriton };
            Profile tempProfile = LoadProfile(LegacyLeftClickProfileJson, out var tempMappings);

            FillMappingProfileInitialData(tempProfile, tempMappings);
            mapper.RunMigrateLegacyTouchpadClickBindings(tempProfile);
            SyncActionData(tempProfile);

            ButtonMapAction migrated = tempProfile.ActionSets[0].ActionLayers[0].buttonActionDict["LeftPadClick"];
            Assert.IsInstanceOfType(migrated, typeof(TouchpadPressureDualStageAction));

            TouchpadPressureDualStageAction touchAction = (TouchpadPressureDualStageAction)migrated;
            Assert.AreEqual(TriggerDualStageAction.DualStageMode.Threshold, touchAction.ActivationStyle);
            Assert.AreEqual(4096, touchAction.SoftPressThreshold);
            Assert.AreEqual(17096, touchAction.FullPressThreshold);
            Assert.AreEqual(100, touchAction.HipFireDelayMs);

            // Soft Press remains unbound after migration.
            Assert.IsFalse(touchAction.SoftPressActButton.ActionFuncs.Any());

            // Full Press inherits the entire old Regular Press output, including the
            // Hold Press extra activator the user already had configured - optional
            // activators must remain intact through migration.
            Assert.AreEqual(2, touchAction.FullPressActButton.ActionFuncs.Count);
            Assert.IsTrue(touchAction.FullPressActButton.ActionFuncs.Any(f => f is DS4MapperTest.ActionUtil.NormalPressFunc));
            Assert.IsTrue(touchAction.FullPressActButton.ActionFuncs.Any(f => f is DS4MapperTest.ActionUtil.HoldPressFunc));
        }

        [TestMethod]
        public void NonTritonDevice_NeverMigratesLegacyButtonAction()
        {
            mapper = new TestMapper { DeviceTypeOverride = InputDeviceType.SteamController };
            Profile tempProfile = LoadProfile(LegacyLeftClickProfileJson, out var tempMappings);

            FillMappingProfileInitialData(tempProfile, tempMappings);
            mapper.RunMigrateLegacyTouchpadClickBindings(tempProfile);
            SyncActionData(tempProfile);

            ButtonMapAction untouched = tempProfile.ActionSets[0].ActionLayers[0].buttonActionDict["LeftPadClick"];
            Assert.IsInstanceOfType(untouched, typeof(ButtonAction));
        }

        private const string NewFormatBothPadsProfileJson = @"{
  ""Name"": ""NewFormatTouchpadProfile"",
  ""ActionSets"": [
    {
      ""Index"": ""0"",
      ""Name"": ""Set 1"",
      ""ActionLayers"": [
        {
          ""Index"": ""0"",
          ""Name"": ""Default"",
          ""MappedActions"": [
            {
              ""Id"": ""0"",
              ""Name"": ""Left Click"",
              ""ActionMode"": ""TouchpadPressureDualStageAction"",
              ""Settings"": {
                ""ActivationStyle"": ""HipFire"",
                ""SoftPressThreshold"": 3000,
                ""FullPressThreshold"": 20000,
                ""HipFireDelay"": 150,
                ""ForceHipFireDelay"": true
              },
              ""SoftPress"": {
                ""Functions"": [
                  { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""A"" } ] }
                ]
              },
              ""FullPress"": {
                ""Functions"": [
                  { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""B"" } ] }
                ]
              }
            },
            {
              ""Id"": ""1"",
              ""Name"": ""Right Click"",
              ""ActionMode"": ""TouchpadPressureDualStageAction""
            }
          ]
        }
      ]
    }
  ],
  ""Mappings"": [
    {
      ""ActionSet"": 0,
      ""ActionLayer"": 0,
      ""InputMappings"": [
        { ""Input"": ""LeftPadClick"", ""Action"": 0 },
        { ""Input"": ""RightPadClick"", ""Action"": 1 }
      ]
    }
  ]
}";

        [TestMethod]
        public void SaveReload_PreservesSettingsIndependentlyPerPad()
        {
            mapper = new TestMapper { DeviceTypeOverride = InputDeviceType.SteamControllerTriton };
            Profile tempProfile = LoadProfile(NewFormatBothPadsProfileJson, out var tempMappings);

            FillMappingProfileInitialData(tempProfile, tempMappings);
            mapper.RunMigrateLegacyTouchpadClickBindings(tempProfile); // no-op: already new format
            SyncActionData(tempProfile);

            ActionLayer layer = tempProfile.ActionSets[0].ActionLayers[0];
            TouchpadPressureDualStageAction left =
                (TouchpadPressureDualStageAction)layer.buttonActionDict["LeftPadClick"];
            TouchpadPressureDualStageAction right =
                (TouchpadPressureDualStageAction)layer.buttonActionDict["RightPadClick"];

            // Left pad: explicit non-default settings and bindings load correctly.
            Assert.AreEqual(TriggerDualStageAction.DualStageMode.HipFire, left.ActivationStyle);
            Assert.AreEqual(3000, left.SoftPressThreshold);
            Assert.AreEqual(20000, left.FullPressThreshold);
            Assert.AreEqual(150, left.HipFireDelayMs);
            Assert.IsTrue(left.ForceHipFireDelay);
            Assert.IsTrue(left.SoftPressActButton.ActionFuncs.Any());
            Assert.IsTrue(left.FullPressActButton.ActionFuncs.Any());

            // Right pad: no Settings/bindings present at all - falls back to defaults,
            // completely independent of whatever the left pad has (reset-to-default shape).
            Assert.AreEqual(TriggerDualStageAction.DualStageMode.Threshold, right.ActivationStyle);
            Assert.AreEqual(4096, right.SoftPressThreshold);
            Assert.AreEqual(17096, right.FullPressThreshold);
            Assert.AreEqual(100, right.HipFireDelayMs);
            Assert.IsFalse(right.SoftPressActButton.ActionFuncs.Any());
            Assert.IsFalse(right.FullPressActButton.ActionFuncs.Any());

            // Re-serializing the left pad's action must write the new format back out with
            // every setting intact (save/reload round trip).
            MapActionSerializer resaved = MapActionSerializerFactory.CreateSerializer(layer, left);
            string json = JsonConvert.SerializeObject(resaved);
            JObject parsed = JObject.Parse(json);

            Assert.AreEqual("TouchpadPressureDualStageAction", parsed["ActionMode"]?.ToString());
            Assert.AreEqual("HipFire", parsed["Settings"]?["ActivationStyle"]?.ToString());
            Assert.AreEqual(3000, parsed["Settings"]?["SoftPressThreshold"]?.Value<int>());
            Assert.AreEqual(20000, parsed["Settings"]?["FullPressThreshold"]?.Value<int>());
            Assert.AreEqual(150, parsed["Settings"]?["HipFireDelay"]?.Value<int>());
            Assert.AreEqual(true, parsed["Settings"]?["ForceHipFireDelay"]?.Value<bool>());
            Assert.IsTrue(parsed["SoftPress"]?["Functions"]?.HasValues == true);
            Assert.IsTrue(parsed["FullPress"]?["Functions"]?.HasValues == true);
        }
    }
}
