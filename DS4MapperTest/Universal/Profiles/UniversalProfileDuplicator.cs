using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest.Universal.Profiles
{
    // Rules a profile has to satisfy before it can join the store as a second
    // copy of something already in there, whether that copy came from the Copy
    // Selected button or from a file the user picked off disk.
    public static class UniversalProfileDuplicator
    {
        public const string DefaultImportName = "Imported Profile";
        public const string CopyNameSuffix = " copy";

        public static string BuildUniqueDisplayName(
            string desiredName,
            IEnumerable<UniversalProfileSummary> existingProfiles)
        {
            string baseName = string.IsNullOrWhiteSpace(desiredName)
                ? DefaultImportName
                : desiredName.Trim();

            HashSet<string> taken = new HashSet<string>(
                (existingProfiles ?? Enumerable.Empty<UniversalProfileSummary>())
                    .Where(item => item != null && item.Loaded)
                    .Select(item => item.DisplayName),
                StringComparer.OrdinalIgnoreCase);

            if (!taken.Contains(baseName)) return baseName;

            for (int suffix = 2; suffix < 1000; suffix++)
            {
                string candidate = $"{baseName} ({suffix})";
                if (!taken.Contains(candidate)) return candidate;
            }

            return $"{baseName} ({Guid.NewGuid():N})";
        }

        // Copies always take a new profile id: two files claiming one id both
        // resolve to whichever the store happens to find first, so the copy
        // would shadow its original everywhere the id is used.
        public static UniversalProfile PrepareCopy(
            UniversalProfile source,
            IEnumerable<UniversalProfileSummary> existingProfiles)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            UniversalProfile copy = source.Clone();
            copy.ProfileId = Guid.NewGuid();
            copy.DisplayName = BuildUniqueDisplayName(source.DisplayName + CopyNameSuffix, existingProfiles);
            copy.Description = copy.DisplayName;
            copy.CreatedUtc = DateTimeOffset.UtcNow;
            ClearMigrationProvenance(copy);
            return copy;
        }

        // Imports keep their id when it is still free, so re-importing a
        // profile that was exported from this store stays recognisable.
        public static UniversalProfile PrepareImport(
            UniversalProfile imported,
            IEnumerable<UniversalProfileSummary> existingProfiles)
        {
            if (imported == null) throw new ArgumentNullException(nameof(imported));

            UniversalProfileSummary[] existing = (existingProfiles ?? Enumerable.Empty<UniversalProfileSummary>())
                .Where(item => item != null && item.Loaded)
                .ToArray();

            if (imported.ProfileId == Guid.Empty ||
                existing.Any(item => item.ProfileId == imported.ProfileId))
            {
                imported.ProfileId = Guid.NewGuid();
            }

            imported.DisplayName = BuildUniqueDisplayName(imported.DisplayName, existing);
            ClearMigrationProvenance(imported);
            return imported;
        }

        // Migration provenance records that a profile was produced from a
        // legacy file, and the migration manifest is keyed on it. A profile the
        // user copied or imported was not produced that way, so leaving the
        // claim in place would let migration bookkeeping act on it.
        private static void ClearMigrationProvenance(UniversalProfile profile)
        {
            profile.Migration = null;
        }
    }
}
