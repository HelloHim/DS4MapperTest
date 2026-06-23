using System.Collections.Generic;

namespace DS4MapperTest.Common
{
    public class GameCalibPreset
    {
        public string Name { get; }
        public double InGameSens { get; }
        public double RWC { get; }
        public double Counts { get; }
        public bool IsCustom { get; }

        private GameCalibPreset(string name, double inGameSens, double rwc,
            double counts, bool isCustom = false)
        {
            Name = name;
            InGameSens = inGameSens;
            RWC = rwc;
            Counts = counts;
            IsCustom = isCustom;
        }

        public static readonly GameCalibPreset Custom =
            new GameCalibPreset("Custom", 0, 0, 0, isCustom: true);

        // RWC = InGameSens * Counts / 360, rounded to 4 dp
        public static readonly IReadOnlyList<GameCalibPreset> All =
            new List<GameCalibPreset>
            {
                Custom,
                new GameCalibPreset("VALORANT",                0.171,     14.2857,   30075.188 ),
                new GameCalibPreset("Apex Legends",            0.55,      45.4545,   29752.0661),
                new GameCalibPreset("Battlefield 6",           5.6,       465.1974,  29905.5455),
                new GameCalibPreset("COD / OW2",               1.82,      151.5152,  29970.03  ),
                new GameCalibPreset("CS2 / Doom (2016)",       0.54,      45.4545,   30303.0303),
                new GameCalibPreset("Deadlock",                0.27,      22.7273,   30303.0303),
                new GameCalibPreset("Destiny 2",               2.0,       151.5152,  27272.7273),
                new GameCalibPreset("EMPULSE",                 1.12,      93.2132,   29961.3873),
                new GameCalibPreset("Halo Infinite",           0.533,     44.4444,   30018.7617),
                new GameCalibPreset("Marvel Rivals",           0.69,      57.1429,   29813.6646),
                new GameCalibPreset("Quake Live",              0.545612,  45.4545,   29991.3425),
                new GameCalibPreset("Rainbow Six Siege X",     50.0,      4165.4636, 29991.338 ),
                new GameCalibPreset("THE FINALS",              12.0,      1000.0,    30000.0   ),
                new GameCalibPreset("ULTRAKILL",               2.4,       200.0,     30000.0   ),
            }.AsReadOnly();
    }
}
