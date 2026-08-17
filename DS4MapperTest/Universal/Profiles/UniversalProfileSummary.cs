using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace DS4MapperTest.Universal.Profiles
{
    // Identity-only view of a stored profile.
    //
    // Listing the profile browser, resolving a profile id to a path and
    // checking a save for name collisions all need nothing but a profile's
    // name and id, yet each used to deserialize and fully validate every
    // profile in the store. With a few dozen profiles of a few tens of KB
    // each, an operation that ran three of those passes stalled the UI thread
    // for a visible beat. A summary reads the header properties and skips the
    // action sets and bindings, which are almost the entire file.
    public sealed class UniversalProfileSummary
    {
        public UniversalProfileSummary(
            string path,
            Guid profileId,
            string displayName,
            string migrationSourceFamily,
            bool loaded)
        {
            Path = path;
            ProfileId = profileId;
            DisplayName = displayName ?? string.Empty;
            MigrationSourceFamily = migrationSourceFamily ?? string.Empty;
            Loaded = loaded;
        }

        public string Path { get; }
        public Guid ProfileId { get; }
        public string DisplayName { get; }
        public string MigrationSourceFamily { get; }

        // False when the file could not be read far enough to identify it.
        // Such a file is still a profile as far as the store is concerned; it
        // simply cannot be listed or matched by id.
        public bool Loaded { get; }
    }

    internal static class UniversalProfileSummaryReader
    {
        private sealed class CacheEntry
        {
            public long LastWriteTicks { get; set; }
            public long Length { get; set; }
            public UniversalProfileSummary Summary { get; set; }
        }

        // Keyed by full path and shared across store instances: several parts
        // of the app hold their own UniversalProfileStore over the same root.
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache =
            new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        public static UniversalProfileSummary Read(string path)
        {
            FileInfo info = new FileInfo(path);
            long lastWriteTicks;
            long length;
            try
            {
                lastWriteTicks = info.LastWriteTimeUtc.Ticks;
                length = info.Length;
            }
            catch (IOException)
            {
                return ReadUncached(path);
            }

            if (Cache.TryGetValue(path, out CacheEntry cached) &&
                cached.LastWriteTicks == lastWriteTicks &&
                cached.Length == length)
            {
                return cached.Summary;
            }

            UniversalProfileSummary summary = ReadUncached(path);
            Cache[path] = new CacheEntry
            {
                LastWriteTicks = lastWriteTicks,
                Length = length,
                Summary = summary,
            };

            return summary;
        }

        public static void Invalidate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            Cache.TryRemove(path, out _);
        }

        private static UniversalProfileSummary ReadUncached(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader streamReader = new StreamReader(stream))
                using (JsonTextReader reader = new JsonTextReader(streamReader))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    return ReadSummary(path, reader);
                }
            }
            catch (IOException)
            {
                return Unreadable(path);
            }
            catch (UnauthorizedAccessException)
            {
                return Unreadable(path);
            }
            catch (JsonException)
            {
                return Unreadable(path);
            }
        }

        private static UniversalProfileSummary ReadSummary(string path, JsonTextReader reader)
        {
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
            {
                return Unreadable(path);
            }

            int? schemaVersion = null;
            Guid profileId = Guid.Empty;
            string displayName = string.Empty;
            string migrationSourceFamily = string.Empty;

            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                string propertyName = reader.Value as string;
                if (!reader.Read()) break;

                switch (propertyName)
                {
                    case "schemaVersion":
                        schemaVersion = reader.Value is long parsedVersion ? (int)parsedVersion : null;
                        break;
                    case "profileId":
                        Guid.TryParse(reader.Value as string, out profileId);
                        break;
                    case "displayName":
                        displayName = reader.Value as string ?? string.Empty;
                        break;
                    case "migration":
                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            migrationSourceFamily =
                                JObject.Load(reader).Value<string>("sourceFamily") ?? string.Empty;
                        }
                        break;
                    default:
                        // Skips the whole value, so actionSets and bindings are
                        // walked but never materialised.
                        reader.Skip();
                        break;
                }
            }

            bool loaded = schemaVersion == UniversalProfile.CurrentSchemaVersion &&
                profileId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(displayName);

            return new UniversalProfileSummary(path, profileId, displayName, migrationSourceFamily, loaded);
        }

        private static UniversalProfileSummary Unreadable(string path)
        {
            return new UniversalProfileSummary(path, Guid.Empty, string.Empty, string.Empty, false);
        }
    }
}
