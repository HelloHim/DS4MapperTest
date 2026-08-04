using DS4MapperTest;
using DS4MapperTest.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    // A brand-new profile should present as: VALORANT preset selected, In-Game
    // Sensitivity 1.0. Previously the compiled-in default (RWC 45.4545, sens
    // 0.54, counts 30303.0303) matched no game preset the user actually wants
    // as a starting point, and specifically did not match VALORANT.
    [TestClass]
    public class ProfileDefaultCalibrationTests
    {
        [TestMethod]
        public void DefaultCalibInGameSens_IsOne()
        {
            Profile profile = new Profile();
            Assert.AreEqual(1.0, profile.CalibInGameSens);
        }

        [TestMethod]
        public void DefaultCalibRwc_MatchesValorantPreset()
        {
            Profile profile = new Profile();
            Assert.AreEqual(GameCalibPreset.Valorant.RWC, profile.CalibRwc);
        }

        [TestMethod]
        public void DefaultCalibration_IsInternallyConsistent()
        {
            // RWC = Counts x InGameSens / 360 (per the app's own calibration formula).
            Profile profile = new Profile();
            double expectedRwc = profile.CalibCounts * profile.CalibInGameSens / 360.0;
            Assert.AreEqual(expectedRwc, profile.CalibRwc, 1e-9);
        }

        [TestMethod]
        public void ValorantPreset_IsInPresetList_AndNotCustom()
        {
            Assert.IsFalse(GameCalibPreset.Valorant.IsCustom);
            CollectionAssert.Contains((System.Collections.ICollection)GameCalibPreset.All, GameCalibPreset.Valorant);
        }
    }
}
