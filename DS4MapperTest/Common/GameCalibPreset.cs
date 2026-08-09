using System.Collections.Generic;
using System;
using System.Linq;

namespace DS4MapperTest.Common
{
    public class GameCalibPreset
    {
        public string Name { get; }
        public double RWC { get; }
        public bool IsCustom { get; }

        private GameCalibPreset(string name, double rwc, bool isCustom = false)
        {
            Name = name;
            RWC = rwc;
            IsCustom = isCustom;
        }

        public static readonly GameCalibPreset Custom =
            new GameCalibPreset("Custom", 0, isCustom: true);

        // The default profile calibration (Profile.cs) is seeded to match this
        // preset at an In-Game Sensitivity of 1.0, so a brand-new profile shows
        // VALORANT selected with no further setup needed.
        public static readonly GameCalibPreset Valorant =
            new GameCalibPreset("VALORANT", 14.2857);

        // Presets identify a game's RWC. Counts are measured by the player and
        // sensitivity is derived from that count total at selection time.
        public static readonly IReadOnlyList<GameCalibPreset> All =
            new List<GameCalibPreset>
            {
                Custom,
                new GameCalibPreset("Apex Legends",            45.4545),
                new GameCalibPreset("Battlefield 6",           465.1974),
                new GameCalibPreset("COD / OW2",               151.5152),
                new GameCalibPreset("CS2 / Doom (2016)",       45.4545),
                new GameCalibPreset("Deadlock",                22.7273),
                new GameCalibPreset("Destiny 2",               151.5152),
                new GameCalibPreset("EMPULSE",                 93.2132),
                new GameCalibPreset("Halo Infinite",           44.4444),
                new GameCalibPreset("Marvel Rivals",           57.1429),
                new GameCalibPreset("Quake Live",              45.4545),
                new GameCalibPreset("Rainbow Six Siege X",     4165.4636),
                new GameCalibPreset("THE FINALS",              1000.0),
                new GameCalibPreset("ULTRAKILL",               200.0),
                Valorant,
            }.AsReadOnly();

        public static GameCalibPreset FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return All.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.Ordinal));
        }

        public static GameCalibPreset MatchByRwc(double rwc, double tolerance = 1e-3)
        {
            return All.FirstOrDefault(p =>
                !p.IsCustom && Math.Abs(p.RWC - rwc) < tolerance);
        }
    }
}
