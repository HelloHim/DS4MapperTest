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

        // Apex Legends, CS2 and Doom (2016) all turn the same amount per count, so
        // they were three list entries sharing one RWC between them. A preset can
        // only be matched back from that RWC, and a match can only ever return the
        // first entry holding it, so editing In-Game Sensitivity used to snap the
        // dropdown to "Apex Legends" no matter which of the three was chosen.
        public static readonly GameCalibPreset ApexCs2Doom =
            new GameCalibPreset("Apex / CS2 / Doom", 45.4545);

        // Presets identify a game's RWC. Counts are measured by the player and
        // sensitivity is derived from that count total at selection time.
        //
        // Each RWC below is Counts x In-Game Sensitivity / 360 from a measured
        // 360 degree turn, rounded to four decimal places, and the measurement it
        // came from is kept beside it so a later edit can be checked rather than
        // guessed at. Several of these are repeating decimals, so four places is
        // the agreed resolution rather than the exact value.
        public static readonly IReadOnlyList<GameCalibPreset> All =
            new List<GameCalibPreset>
            {
                Custom,
                ApexCs2Doom,                                              // 0.55 x 29752.0661, 0.54 x 30303.0303
                new GameCalibPreset("Battlefield 6",           465.1974), // 5.6 x 29905.5455
                new GameCalibPreset("COD / OW2",               151.5152), // 1.82 x 29970.03
                new GameCalibPreset("Deadlock",                22.7273),  // 0.27 x 30303.0303
                new GameCalibPreset("Destiny 2",               151.5152), // 2 x 27272.7273
                new GameCalibPreset("EMPULSE",                 93.2132),  // 1.12 x 29961.3873
                new GameCalibPreset("Halo Campaign Evolved",   21.2207),  // 0.2521014 x 30303.0339
                new GameCalibPreset("Halo Infinite",           44.4444),  // 0.533 x 30018.7617
                new GameCalibPreset("Marvel Rivals",           57.1429),  // 0.69 x 29813.6646
                new GameCalibPreset("Quake Live",              45.4545),  // 0.545612 x 29991.3425
                new GameCalibPreset("Rainbow Six Siege X",     4165.4636),// 50 x 29991.338
                new GameCalibPreset("THE FINALS",              1000.0),   // 12 x 30000
                new GameCalibPreset("ULTRAKILL",               200.0),    // 2.4 x 30000
                Valorant,                                                 // 0.171 x 30075.188
            }.AsReadOnly();

        // Names dropped when presets sharing an RWC were merged into one entry.
        // Profiles saved before the merge still name them, and an unknown name
        // resolves to Custom, which would quietly strip the game a player had
        // already chosen. Map them onto the entry that replaced them instead.
        private static readonly IReadOnlyDictionary<string, GameCalibPreset> RetiredNames =
            new Dictionary<string, GameCalibPreset>(StringComparer.Ordinal)
            {
                ["Apex Legends"] = ApexCs2Doom,
                ["CS2 / Doom (2016)"] = ApexCs2Doom,
            };

        public static GameCalibPreset FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            GameCalibPreset match = All.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.Ordinal));
            if (match != null)
            {
                return match;
            }

            return RetiredNames.TryGetValue(name, out GameCalibPreset retired)
                ? retired
                : null;
        }

        public static GameCalibPreset MatchByRwc(double rwc, double tolerance = 1e-3)
        {
            return All.FirstOrDefault(p =>
                !p.IsCustom && Math.Abs(p.RWC - rwc) < tolerance);
        }

        // Merging the three duplicates above still leaves Quake Live sharing their
        // RWC, and any future preset may collide the same way. Passing the preset
        // already selected keeps it whenever it still fits the value, so editing an
        // unrelated calibration field can never rewrite the player's choice to
        // whichever equivalent game happens to sit first in the list.
        public static GameCalibPreset MatchByRwc(double rwc, GameCalibPreset preferred,
            double tolerance = 1e-3)
        {
            if (preferred != null && !preferred.IsCustom &&
                Math.Abs(preferred.RWC - rwc) < tolerance)
            {
                return preferred;
            }

            return MatchByRwc(rwc, tolerance);
        }
    }
}
