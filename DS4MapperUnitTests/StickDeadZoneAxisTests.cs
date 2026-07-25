using DS4MapperTest.StickModifiers;
using DS4MapperTest.StickActions;
using DS4MapperTest;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickDeadZoneAxisTests
    {
        [TestMethod]
        public void DisabledAxisSplit_PreservesCombinedRadialBehaviour()
        {
            StickDeadZone deadZone = new StickDeadZone(0.30, 1.0, 0.0);
            deadZone.DeadZoneType = StickDeadZone.DeadZoneTypes.Radial;
            deadZone.DeadZoneX = 0.10;
            deadZone.DeadZoneY = 0.60;

            deadZone.CalcOutValues(25, 0, 100, 100, out double xNorm, out double yNorm);

            Assert.IsFalse(deadZone.inSafeZone);
            Assert.AreEqual(0.0, xNorm);
            Assert.AreEqual(0.0, yNorm);
        }

        [TestMethod]
        public void AxisSplit_UsesIndependentHorizontalAndVerticalThresholds()
        {
            StickDeadZone deadZone = new StickDeadZone(0.30, 1.0, 0.0)
            {
                DeadZoneType = StickDeadZone.DeadZoneTypes.Radial,
                SeparateAxisDeadZones = true,
                DeadZoneX = 0.10,
                DeadZoneY = 0.60,
            };

            deadZone.CalcOutValues(25, 0, 100, 100, out double xNorm, out _);
            Assert.IsTrue(deadZone.inSafeZone);
            Assert.IsTrue(xNorm > 0.0);

            deadZone.CalcOutValues(0, 25, 100, 100, out _, out double yNorm);
            Assert.IsFalse(deadZone.inSafeZone);
            Assert.AreEqual(0.0, yNorm);
        }

        [TestMethod]
        public void MatchingAxisValues_PreserveExistingRadialOutput()
        {
            StickDeadZone combined = new StickDeadZone(0.30, 1.0, 0.0);
            StickDeadZone split = new StickDeadZone(0.30, 1.0, 0.0)
            {
                SeparateAxisDeadZones = true,
                DeadZoneX = 0.30,
                DeadZoneY = 0.30,
            };

            combined.CalcOutValues(65, 40, 100, 100, out double combinedX, out double combinedY);
            split.CalcOutValues(65, 40, 100, 100, out double splitX, out double splitY);

            Assert.AreEqual(combined.inSafeZone, split.inSafeZone);
            Assert.AreEqual(combinedX, splitX, 0.000001);
            Assert.AreEqual(combinedY, splitY, 0.000001);
        }

        [TestMethod]
        public void DpadAxisSplit_PersistsAndClonesIndependently()
        {
            StickPadAction action = new StickPadAction();
            action.DeadMod.SeparateAxisDeadZones = true;
            action.DeadMod.DeadZoneX = 0.15;
            action.DeadMod.DeadZoneY = 0.45;
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_X);
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_Y);

            string json = JsonConvert.SerializeObject(new StickPadActionSerializer(null, action));
            StringAssert.Contains(json, "SeparateAxisDeadZones");
            StringAssert.Contains(json, "DeadZoneX");
            StringAssert.Contains(json, "DeadZoneY");

            StickPadActionSerializer loaded = JsonConvert.DeserializeObject<StickPadActionSerializer>(json);
            StickPadAction loadedAction = (StickPadAction)loaded.MapAction;
            Assert.IsTrue(loadedAction.DeadMod.SeparateAxisDeadZones);
            Assert.AreEqual(0.15, loadedAction.DeadMod.DeadZoneX, 0.000001);
            Assert.AreEqual(0.45, loadedAction.DeadMod.DeadZoneY, 0.000001);

            StickPadAction copy = (StickPadAction)loadedAction.DuplicateAction();
            copy.DeadMod.DeadZoneX = 0.25;
            Assert.AreEqual(0.15, loadedAction.DeadMod.DeadZoneX, 0.000001);
        }
    }
}
