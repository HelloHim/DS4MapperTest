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
    }
}
