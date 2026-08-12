using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DS4MapperTest.Universal.Profiles
{
    public sealed class UniversalProfileStoreEntry
    {
        public UniversalProfileStoreEntry(string path, UniversalProfile profile, Exception error)
        {
            Path = path;
            Profile = profile;
            Error = error;
        }

        public string Path { get; }
        public UniversalProfile Profile { get; }
        public Exception Error { get; }
        public bool Loaded => Profile != null && Error == null;
    }

    public sealed class UniversalProfileStore
    {
        public const string ProfileFileExtension = ".universal-profile.json";
        private const string TempExtension = ".tmp";
        private const int MaxSafeFileBaseLength = 120;
        private readonly string rootPath;

        public UniversalProfileStore(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Profile store root cannot be empty.", nameof(rootPath));
            }

            this.rootPath = Path.GetFullPath(rootPath);
        }

        public string RootPath => rootPath;

        public static UniversalProfileStore CreateDefault()
        {
            return new UniversalProfileStore(ApplicationDataPathResolver.ResolveDefault().UniversalProfilesPath);
        }

        public string GetProfilePath(Guid profileId)
        {
            if (profileId == Guid.Empty)
            {
                throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));
            }

            return Path.Combine(rootPath, $"{profileId:D}{ProfileFileExtension}");
        }

        public string GetNamedProfilePath(string displayName)
        {
            return Path.Combine(rootPath, BuildSafeFileName(displayName));
        }

        public string ResolveRelativeProfilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Profile filename cannot be empty.", nameof(fileName));
            }

            if (Path.IsPathRooted(fileName))
            {
                throw new ArgumentException("Profile filename must be relative.", nameof(fileName));
            }

            if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            {
                throw new ArgumentException("Profile filename cannot contain directory segments.", nameof(fileName));
            }

            if (IsReservedWindowsName(Path.GetFileNameWithoutExtension(fileName)))
            {
                throw new ArgumentException("Profile filename uses a reserved Windows device name.", nameof(fileName));
            }

            string fullPath = Path.GetFullPath(Path.Combine(rootPath, fileName));
            string fullRoot = EnsureTrailingSeparator(rootPath);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Profile filename resolves outside the universal profile root.", nameof(fileName));
            }

            return fullPath;
        }

        public void Save(UniversalProfile profile)
        {
            SaveToPath(profile, GetProfilePath(profile.ProfileId));
        }

        public void SaveNamed(UniversalProfile profile, string previousPath = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            Directory.CreateDirectory(rootPath);
            string targetPath = GetNamedProfilePath(profile.DisplayName);
            UniversalProfileStoreEntry collision = EnumerateProfiles().FirstOrDefault(item =>
                item.Loaded &&
                item.Profile.ProfileId != profile.ProfileId &&
                (string.Equals(item.Profile.DisplayName, profile.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Path.GetFileName(item.Path), Path.GetFileName(targetPath), StringComparison.OrdinalIgnoreCase)));
            if (collision != null)
            {
                throw new InvalidOperationException($"A universal profile named \"{profile.DisplayName}\" already exists.");
            }

            SaveToPath(profile, targetPath);

            if (!string.IsNullOrWhiteSpace(previousPath) &&
                !string.Equals(Path.GetFullPath(previousPath), targetPath, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(previousPath))
            {
                File.Delete(previousPath);
            }
        }

        public void Delete(string profilePath)
        {
            string fullPath = EnsurePathInsideRoot(profilePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public UniversalProfile LoadFromPath(string profilePath)
        {
            return UniversalProfileSerializer.Deserialize(File.ReadAllText(EnsurePathInsideRoot(profilePath)));
        }

        public string FindProfilePath(Guid profileId)
        {
            string legacyPath = GetProfilePath(profileId);
            if (File.Exists(legacyPath)) return legacyPath;

            return EnumerateProfiles()
                .FirstOrDefault(item => item.Loaded && item.Profile.ProfileId == profileId)
                ?.Path;
        }

        private void SaveToPath(UniversalProfile profile, string targetPath)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            UniversalProfileValidationResult validation = UniversalProfileValidator.Validate(profile);
            if (!validation.IsValid)
            {
                throw new UniversalProfileValidationException(validation);
            }

            string json = UniversalProfileSerializer.Serialize(profile);
            Directory.CreateDirectory(rootPath);
            targetPath = EnsurePathInsideRoot(targetPath);
            string tempPath = Path.Combine(rootPath, $".{profile.ProfileId:D}.{Guid.NewGuid():N}{TempExtension}");
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(targetPath))
                {
                    File.Replace(tempPath, targetPath, null);
                }
                else
                {
                    File.Move(tempPath, targetPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        public UniversalProfile Load(Guid profileId)
        {
            string path = FindProfilePath(profileId) ?? GetProfilePath(profileId);
            return UniversalProfileSerializer.Deserialize(File.ReadAllText(path));
        }

        public IReadOnlyList<UniversalProfileStoreEntry> EnumerateProfiles()
        {
            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<UniversalProfileStoreEntry>();
            }

            List<UniversalProfileStoreEntry> entries = new List<UniversalProfileStoreEntry>();
            foreach (string file in Directory.EnumerateFiles(rootPath, $"*{ProfileFileExtension}", SearchOption.TopDirectoryOnly)
                .Where(file => !file.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    entries.Add(new UniversalProfileStoreEntry(file, UniversalProfileSerializer.Deserialize(File.ReadAllText(file)), null));
                }
                catch (Exception ex)
                {
                    entries.Add(new UniversalProfileStoreEntry(file, null, ex));
                }
            }

            return entries;
        }

        private static string EnsureTrailingSeparator(string path)
        {
            string full = Path.GetFullPath(path);
            return full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? full
                : full + Path.DirectorySeparatorChar;
        }

        private static bool IsReservedWindowsName(string fileNameWithoutExtension)
        {
            string name = fileNameWithoutExtension.Split('.')[0].TrimEnd(' ');
            string[] reserved =
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            };

            return reserved.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
        }

        public static string BuildSafeFileName(string displayName)
        {
            string baseName = (displayName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "Profile";
            }

            foreach (char c in Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }).Distinct())
            {
                baseName = baseName.Replace(c, '_');
            }

            baseName = baseName.Trim().TrimEnd('.');
            while (baseName.Contains("__"))
            {
                baseName = baseName.Replace("__", "_");
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "Profile";
            }

            if (IsReservedWindowsName(baseName))
            {
                baseName = "_" + baseName;
            }

            if (baseName.Length > MaxSafeFileBaseLength)
            {
                baseName = baseName.Substring(0, MaxSafeFileBaseLength).TrimEnd(' ', '.');
            }

            return baseName + ProfileFileExtension;
        }

        private string EnsurePathInsideRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Profile path cannot be empty.", nameof(path));
            }

            string fullPath = Path.GetFullPath(path);
            string fullRoot = EnsureTrailingSeparator(rootPath);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Profile path resolves outside the universal profile root.", nameof(path));
            }

            return fullPath;
        }
    }
}
