using DS4MapperTest;
using DS4MapperTest.TriggerActions;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class TriggerButtonActionSerializationTests
    {
        [TestMethod]
        public void MissingTriggerStyleSettingsUseCompatibilityDefaults()
        {
            string json = """
            {
              "Id": 1,
              "ActionMode": "TriggerButtonAction",
              "Functions": [],
              "Settings": {
                "DeadZone": 0.15
              }
            }
            """;

            TriggerButtonActionSerializer serializer =
                JsonConvert.DeserializeObject<TriggerButtonActionSerializer>(json);
            serializer.PopulateMap();
            TriggerButtonAction action =
                serializer.MapAction as TriggerButtonAction;

            Assert.AreEqual(TriggerStyle.SimpleThreshold, action.TriggerStyle);
            Assert.AreEqual(HipFirePreset.Balanced, action.HipFirePreset);
            Assert.AreEqual(150, action.HipFireWindowMs);
        }

        [TestMethod]
        public void TriggerStyleSettingsRoundTrip()
        {
            TriggerButtonAction action = new TriggerButtonAction
            {
                Id = 2,
                TriggerStyle = TriggerStyle.HipFireExclusive,
                HipFirePreset = HipFirePreset.Custom,
                HipFireWindowMs = 200,
            };
            action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.TRIGGER_STYLE);
            action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_PRESET);
            action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_WINDOW_MS);

            TriggerButtonActionSerializer serializer =
                new TriggerButtonActionSerializer(null, action);
            string json = JsonConvert.SerializeObject(serializer);
            TriggerButtonActionSerializer roundTrip =
                JsonConvert.DeserializeObject<TriggerButtonActionSerializer>(json);
            roundTrip.PopulateMap();
            TriggerButtonAction result = roundTrip.MapAction as TriggerButtonAction;

            Assert.AreEqual(TriggerStyle.HipFireExclusive, result.TriggerStyle);
            Assert.AreEqual(HipFirePreset.Custom, result.HipFirePreset);
            Assert.AreEqual(200, result.HipFireWindowMs);
        }
    }
}
