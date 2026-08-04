using Newtonsoft.Json;
using DS4MapperTest;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class GyroInvertTests
    {
        // --- Defaults ------------------------------------------------------------------

        [TestMethod]
        public void InvertSettingsDefaultIsOffWithXOnlyAndHoldToEnableShape()
        {
            GyroInvertSettings invert = GyroInvertSettings.CreateDefault();

            Assert.IsFalse(invert.enabled);
            Assert.AreEqual(GyroInvertAxisChoice.XOnly, invert.axisChoice);
            Assert.IsTrue(invert.triggerActivates); // Hold to Enable shape
            Assert.IsFalse(invert.andCond);
            Assert.AreEqual(0, invert.activationHoldMs);
            Assert.AreEqual(0, invert.triggerButtons.Length);
        }

        [TestMethod]
        public void NewGyroMouseActionHasCorrectDefaultInvertSettings()
        {
            GyroMouse action = new();

            Assert.IsFalse(action.mouseParams.invert.enabled);
            Assert.AreEqual(GyroInvertAxisChoice.XOnly, action.mouseParams.invert.axisChoice);
            Assert.IsTrue(action.mouseParams.invert.triggerActivates);
        }

        // --- GyroInvertApplier -----------------------------------------------------------

        [TestMethod]
        public void ApplyXOnlyFlipsOnlyX()
        {
            double x = 5.0, y = 7.0;
            GyroInvertApplier.Apply(GyroInvertAxisChoice.XOnly, ref x, ref y);

            Assert.AreEqual(-5.0, x, 0.0000001);
            Assert.AreEqual(7.0, y, 0.0000001);
        }

        [TestMethod]
        public void ApplyYOnlyFlipsOnlyY()
        {
            double x = 5.0, y = 7.0;
            GyroInvertApplier.Apply(GyroInvertAxisChoice.YOnly, ref x, ref y);

            Assert.AreEqual(5.0, x, 0.0000001);
            Assert.AreEqual(-7.0, y, 0.0000001);
        }

        [TestMethod]
        public void ApplyXAndYFlipsBoth()
        {
            double x = 5.0, y = 7.0;
            GyroInvertApplier.Apply(GyroInvertAxisChoice.XAndY, ref x, ref y);

            Assert.AreEqual(-5.0, x, 0.0000001);
            Assert.AreEqual(-7.0, y, 0.0000001);
        }

        [TestMethod]
        public void ApplyPreservesMagnitude()
        {
            double x = -3.25, y = 0.0;
            GyroInvertApplier.Apply(GyroInvertAxisChoice.XAndY, ref x, ref y);

            Assert.AreEqual(3.25, x, 0.0000001);
            Assert.AreEqual(0.0, y, 0.0000001); // negating zero stays zero
        }

        // --- Serialization -----------------------------------------------------------

        private static GyroMouseSerializer DeserializeGyroMouse(string settingsJson)
        {
            string json = @"{
              ""Id"": 0,
              ""ActionMode"": ""GyroMouseAction"",
              ""Settings"": " + settingsJson + @"
            }";

            GyroMouseSerializer serializer = new();
            JsonConvert.PopulateObject(json, serializer);
            serializer.PopulateMap();
            return serializer;
        }

        [TestMethod]
        public void DeserializesInvertSettingsFromJson()
        {
            GyroMouseSerializer serializer = DeserializeGyroMouse(@"{
                ""InvertGyroEnabled"": true,
                ""InvertGyroAxis"": ""YOnly"",
                ""InvertGyroTriggerButtons"": ""BtnEast"",
                ""InvertGyroTriggerActivates"": true,
                ""InvertGyroTriggerEvalCond"": ""And"",
                ""InvertGyroActivationHoldMs"": 150
            }");

            var invert = ((GyroMouse)serializer.MapAction).mouseParams.invert;
            Assert.IsTrue(invert.enabled);
            Assert.AreEqual(GyroInvertAxisChoice.YOnly, invert.axisChoice);
            Assert.IsTrue(invert.triggerActivates);
            Assert.IsTrue(invert.andCond);
            Assert.AreEqual(150, invert.activationHoldMs);
            CollectionAssert.Contains(invert.triggerButtons, JoypadActionCodes.BtnEast);
        }

        [TestMethod]
        public void ProfileWithoutInvertSettingsKeepsFeatureDisabled()
        {
            // A profile saved before this feature existed (or one that never touched it)
            // has none of the InvertGyro* keys at all - the feature must stay off.
            GyroMouseSerializer serializer = DeserializeGyroMouse(@"{
                ""InvertX"": false,
                ""InvertY"": false,
                ""UseForXAxis"": ""Yaw""
            }");

            var invert = ((GyroMouse)serializer.MapAction).mouseParams.invert;
            Assert.IsFalse(invert.enabled);
            Assert.AreEqual(GyroInvertAxisChoice.XOnly, invert.axisChoice);
        }
    }
}
