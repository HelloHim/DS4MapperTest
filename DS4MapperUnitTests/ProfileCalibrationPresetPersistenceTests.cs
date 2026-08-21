using DS4MapperTest;
using DS4MapperTest.Common;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class ProfileCalibrationPresetPersistenceTests
    {
        // The 360 degree turn each preset was measured from, as In-Game Sensitivity
        // and the mouse counts that turn took.
        private static readonly (string Name, double Sens, double Counts)[] Measurements =
            new[]
            {
                ("Apex / CS2 / Doom",     0.54,      30303.0303),
                ("Battlefield 6",         5.6,       29905.5455),
                ("COD / OW2",             1.82,      29970.03),
                ("Deadlock",              0.27,      30303.0303),
                ("Destiny 2",             2.0,       27272.7273),
                ("EMPULSE",               1.12,      29961.3873),
                ("Halo Campaign Evolved", 0.2521014, 30303.0339),
                ("Halo Infinite",         0.533,     30018.7617),
                ("Marvel Rivals",         0.69,      29813.6646),
                ("Quake Live",            0.545612,  29991.3425),
                ("Rainbow Six Siege X",   50.0,      29991.338),
                ("THE FINALS",            12.0,      30000.0),
                ("ULTRAKILL",             2.4,       30000.0),
                ("VALORANT",              0.171,     30075.188),
            };

        // A preset's RWC is Counts x In-Game Sensitivity / 360, agreed to four decimal
        // places. Deriving it by hand is easy to get subtly wrong, and a preset that is
        // out by even a thousandth stops being recognised by the tolerance the dropdown
        // matches on, so pin every constant to the turn it came from.
        [TestMethod]
        public void EveryPresetRwcMatchesTheTurnItWasMeasuredFrom()
        {
            foreach ((string name, double sens, double counts) in Measurements)
            {
                GameCalibPreset preset = GameCalibPreset.FindByName(name);
                Assert.IsNotNull(preset, $"'{name}' is missing from the preset list.");
                Assert.AreEqual(Math.Round(counts * sens / 360.0, 4), preset.RWC, 1e-9,
                    $"'{name}' does not match {sens} x {counts} / 360.");
            }

            CollectionAssert.AreEquivalent(
                Measurements.Select(m => m.Name).ToArray(),
                GameCalibPreset.All.Where(p => !p.IsCustom).Select(p => p.Name).ToArray(),
                "Every preset needs the measurement it was derived from recorded here.");
        }

        // Counts and In-Game Sensitivity are entered to the precision the game itself
        // reports, but RWC is a derived figure agreed at four places, so a constant
        // carrying more than that is a transcription slip rather than extra accuracy.
        [TestMethod]
        public void PresetRwcValuesStopAtFourDecimalPlaces()
        {
            foreach (GameCalibPreset preset in GameCalibPreset.All.Where(p => !p.IsCustom))
            {
                Assert.AreEqual(Math.Round(preset.RWC, 4), preset.RWC, 1e-12,
                    $"'{preset.Name}' carries more than four decimal places.");
            }
        }

        [TestMethod]
        public void SerializerRoundTripsChosenCalibrationPreset()
        {
            // Deliberately pairs a preset with another preset's RWC: the chosen name has
            // to survive because it was stored, not because it could be derived back.
            Profile profile = new Profile
            {
                Name = "Preset Persistence",
                CalibPresetName = "Deadlock",
                CalibRwc = GameCalibPreset.Valorant.RWC,
                CalibInGameSens = 1.2345,
                CalibCounts = 5432.1,
            };
            profile.ActionSets.Clear();

            ProfileSerializer serializer = new ProfileSerializer(profile);
            string json = JsonConvert.SerializeObject(serializer, Formatting.Indented);

            StringAssert.Contains(json, @"""CalibPreset"": ""Deadlock""");

            Profile reloadedProfile = new Profile();
            ProfileSerializer reloadedSerializer = new ProfileSerializer(reloadedProfile);
            JsonConvert.PopulateObject(json, reloadedSerializer);
            reloadedSerializer.PopulateProfile();

            Assert.AreEqual("Deadlock", reloadedProfile.CalibPresetName);
        }

        // Apex Legends, CS2 and Doom (2016) share a yaw scale and are now one entry.
        // Profiles written before the merge name the old entries, and an unknown preset
        // name falls back to Custom, which would silently drop the game the player had
        // chosen off every profile they already had.
        [TestMethod]
        public void ProfilesNamingAMergedPresetKeepTheirGame()
        {
            foreach (string retiredName in new[] { "Apex Legends", "CS2 / Doom (2016)" })
            {
                Profile profile = new Profile { CalibPresetName = retiredName };
                Assert.AreEqual(GameCalibPreset.ApexCs2Doom.Name, profile.CalibPresetName,
                    $"'{retiredName}' should resolve to the entry that replaced it.");

                Assert.AreEqual(GameCalibPreset.ApexCs2Doom, GameCalibPreset.FindByName(retiredName));
            }
        }

        // Quake Live still shares the merged entry's RWC, so matching on RWC alone
        // cannot tell them apart and always returns whichever is listed first. Editing
        // In-Game Sensitivity re-runs that match, which is what used to rewrite the
        // dropdown to another game with the same scale.
        [TestMethod]
        public void RematchingKeepsAPresetThatStillFitsTheValue()
        {
            GameCalibPreset quake = GameCalibPreset.FindByName("Quake Live");
            Assert.IsNotNull(quake);
            Assert.AreEqual(GameCalibPreset.ApexCs2Doom.RWC, quake.RWC, 1e-6,
                "This test is only meaningful while the two presets share an RWC.");

            Assert.AreEqual(quake, GameCalibPreset.MatchByRwc(quake.RWC, quake));
            Assert.AreEqual(GameCalibPreset.ApexCs2Doom,
                GameCalibPreset.MatchByRwc(GameCalibPreset.ApexCs2Doom.RWC, GameCalibPreset.ApexCs2Doom));

            // A selection that no longer fits the value gives way to the RWC match.
            Assert.AreEqual(GameCalibPreset.Valorant,
                GameCalibPreset.MatchByRwc(GameCalibPreset.Valorant.RWC, quake));
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
