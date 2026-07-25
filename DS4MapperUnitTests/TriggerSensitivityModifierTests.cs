using DS4MapperTest.GyroActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class TriggerSensitivityModifierTests
    {
        [TestMethod]
        public void DisabledModifierLeavesBaseSensitivityUnchanged()
        {
            var settings = new TriggerSensitivityModifierSettings(5.0);
            Assert.AreEqual(5.0, TriggerSensitivityModifier.Evaluate(settings, 5.0, 1.0));
        }

        [TestMethod]
        public void DecreaseWithPullInterpolatesLinearEndpoints()
        {
            var settings = new TriggerSensitivityModifierSettings(5.0)
            { enabled = true, targetSensitivity = 2.0 };
            Assert.AreEqual(5.0, TriggerSensitivityModifier.Evaluate(settings, 5.0, 0.0), 0.000001);
            Assert.AreEqual(3.5, TriggerSensitivityModifier.Evaluate(settings, 5.0, 0.5), 0.000001);
            Assert.AreEqual(2.0, TriggerSensitivityModifier.Evaluate(settings, 5.0, 1.0), 0.000001);
        }

        [TestMethod]
        public void IncreaseWithPullInterpolatesAndClampsInvalidEndpoint()
        {
            var settings = new TriggerSensitivityModifierSettings(5.0)
            { enabled = true, behaviour = TriggerSensitivityModifierBehaviour.IncreaseWithPull, targetSensitivity = 8.0 };
            Assert.AreEqual(6.5, TriggerSensitivityModifier.Evaluate(settings, 5.0, 0.5), 0.000001);
            Assert.AreEqual(8.0, TriggerSensitivityModifier.Evaluate(settings, 5.0, 1.0), 0.000001);
            settings.targetSensitivity = 2.0;
            Assert.IsFalse(TriggerSensitivityModifier.IsValid(settings, 5.0));
            Assert.AreEqual(5.0, TriggerSensitivityModifier.Evaluate(settings, 5.0, 1.0), 0.000001);
        }

        [TestMethod]
        public void MultiplierModeTracksBaseWhileAbsoluteModeDoesNot()
        {
            var settings = new TriggerSensitivityModifierSettings(5.0)
            { enabled = true, configureUsing = TriggerSensitivityModifierConfigureUsing.AbsoluteSensitivity, targetSensitivity = 2.0 };
            Assert.AreEqual(2.0, TriggerSensitivityModifier.ResolveTarget(settings, 8.0), 0.000001);
            settings.configureUsing = TriggerSensitivityModifierConfigureUsing.Multiplier;
            settings.multiplier = 0.4;
            Assert.AreEqual(3.2, TriggerSensitivityModifier.ResolveTarget(settings, 8.0), 0.000001);
        }

        [TestMethod]
        public void DecreaseAndIncreaseMultiplierValidationIsDirectionAware()
        {
            var settings = new TriggerSensitivityModifierSettings(5.0)
            { configureUsing = TriggerSensitivityModifierConfigureUsing.Multiplier, multiplier = 1.2 };
            Assert.IsFalse(TriggerSensitivityModifier.IsValid(settings, 5.0));
            settings.behaviour = TriggerSensitivityModifierBehaviour.IncreaseWithPull;
            Assert.IsTrue(TriggerSensitivityModifier.IsValid(settings, 5.0));
        }

        [TestMethod]
        public void SwappingEndpointsReversesAbsoluteAndMultiplierConfigurations()
        {
            var absoluteSettings = new TriggerSensitivityModifierSettings(5.0)
            { targetSensitivity = 2.0 };
            double absoluteBaseSensitivity = 5.0;
            TriggerSensitivityModifier.SwapEndpoints(ref absoluteSettings,
                ref absoluteBaseSensitivity);
            Assert.AreEqual(2.0, absoluteBaseSensitivity, 0.000001);
            Assert.AreEqual(5.0, absoluteSettings.targetSensitivity, 0.000001);

            var multiplierSettings = new TriggerSensitivityModifierSettings(5.0)
            {
                configureUsing = TriggerSensitivityModifierConfigureUsing.Multiplier,
                multiplier = 1.6,
            };
            double multiplierBaseSensitivity = 5.0;
            TriggerSensitivityModifier.SwapEndpoints(ref multiplierSettings,
                ref multiplierBaseSensitivity);
            Assert.AreEqual(8.0, multiplierBaseSensitivity, 0.000001);
            Assert.AreEqual(0.625, multiplierSettings.multiplier, 0.000001);
            Assert.AreEqual(TriggerSensitivityModifierConfigureUsing.Multiplier,
                multiplierSettings.configureUsing);
            Assert.AreEqual(5.0, TriggerSensitivityModifier.ResolveTarget(
                multiplierSettings, multiplierBaseSensitivity), 0.000001);
        }

        [TestMethod]
        public void VerticalModificationDefaultsToOff()
        {
            var settings = new TriggerSensitivityModifierSettings(5.0);
            Assert.IsFalse(settings.modifyVerticalSensitivity);
        }
    }
}
