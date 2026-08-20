using DS4MapperTest;
using DS4MapperTest.Common;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class ProfileCalibrationPresetPersistenceTests
    {
        [TestMethod]
        public void SerializerRoundTripsChosenCalibrationPreset()
        {
            Profile profile = new Profile
            {
                Name = "Preset Persistence",
                CalibPresetName = "Apex Legends",
                CalibRwc = GameCalibPreset.Valorant.RWC,
                CalibInGameSens = 1.2345,
                CalibCounts = 5432.1,
            };
            profile.ActionSets.Clear();

            ProfileSerializer serializer = new ProfileSerializer(profile);
            string json = JsonConvert.SerializeObject(serializer, Formatting.Indented);

            StringAssert.Contains(json, @"""CalibPreset"": ""Apex Legends""");

            Profile reloadedProfile = new Profile();
            ProfileSerializer reloadedSerializer = new ProfileSerializer(reloadedProfile);
            JsonConvert.PopulateObject(json, reloadedSerializer);
            reloadedSerializer.PopulateProfile();

            Assert.AreEqual("Apex Legends", reloadedProfile.CalibPresetName);
        }

        [TestMethod]
        public void SerializerRoundTripsRwcCalibrationMode()
        {
            // CountsMode is Profile's compiled-in default, so it round-trips even
            // without an explicit JSON entry. RwcMode is the one a user actually
            // has to choose, so it is the one that must be written out - otherwise
            // reloading a saved profile silently reverts it to CountsMode.
            Profile profile = new Profile
            {
                Name = "Rwc Mode Persistence",
                CalibMode = CalibMode.RwcMode,
            };
            profile.ActionSets.Clear();

            ProfileSerializer serializer = new ProfileSerializer(profile);
            string json = JsonConvert.SerializeObject(serializer, Formatting.Indented);

            StringAssert.Contains(json, @"""CalibMode"": ""RwcMode""");

            Profile reloadedProfile = new Profile();
            ProfileSerializer reloadedSerializer = new ProfileSerializer(reloadedProfile);
            JsonConvert.PopulateObject(json, reloadedSerializer);
            reloadedSerializer.PopulateProfile();

            Assert.AreEqual(CalibMode.RwcMode, reloadedProfile.CalibMode);
        }
    }
}
