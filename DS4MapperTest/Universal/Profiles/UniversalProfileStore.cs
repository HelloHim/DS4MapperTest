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

        public string GetNamedProfilePath(string displayName, string folderName)
        {
            return Path.Combine(GetFolderPath(folderName), BuildSafeFileName(displayName));
        }

        public string GetFolderPath(string folderName)
        {
            string cleanName = NormalizeFolderName(folderName);
            if (string.IsNullOrWhiteSpace(cleanName))
            {
                cleanName = ProfileList.DEFAULT_PROFILE_FOLDER;
            }

            if (cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                cleanName.Contains(Path.DirectorySeparatorChar) ||
                cleanName.Contains(Path.AltDirectorySeparatorChar) ||
                cleanName == "." ||
                cleanName == "..")
            {
                throw new ArgumentException("Folder name contains invalid characters.", nameof(folderName));
            }

            return EnsurePathInsideRoot(Path.Combine(rootPath, cleanName));
        }

        public string GetFolderName(string profilePath)
        {
            string fullPath = EnsurePathInsideRoot(profilePath);
            string folderPath = Path.GetDirectoryName(fullPath);
            if (string.Equals(folderPath, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                return ProfileList.DEFAULT_PROFILE_FOLDER;
            }

            return Path.GetFileName(folderPath);
        }

        public void EnsureStandardFolders()
        {
            Directory.CreateDirectory(rootPath);
            Directory.CreateDirectory(GetFolderPath(ProfileList.DEFAULT_PROFILE_FOLDER));
            Directory.CreateDirectory(GetFolderPath(ProfileList.VALORANT_PROFILE_FOLDER));
        }

        public IReadOnlyList<string> EnumerateFolders()
        {
            EnsureStandardFolders();
            return Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, new UniversalProfileFolderNameComparer())
                .ToList();
        }

        public bool FolderExists(string folderName)
        {
            return Directory.Exists(GetFolderPath(folderName));
        }

        public bool CreateFolder(string folderName)
        {
            string cleanName = NormalizeFolderName(folderName);
            if (string.IsNullOrWhiteSpace(cleanName) || FolderExists(cleanName))
            {
                return false;
            }

            Directory.CreateDirectory(GetFolderPath(cleanName));
            return true;
        }

        public bool RenameFolder(string oldFolderName, string newFolderName)
        {
            string cleanName = NormalizeFolderName(newFolderName);
            if (string.IsNullOrWhiteSpace(oldFolderName) ||
                string.IsNullOrWhiteSpace(cleanName) ||
                string.Equals(oldFolderName, cleanName, StringComparison.OrdinalIgnoreCase) ||
                FolderExists(cleanName))
            {
                return false;
            }

            Directory.Move(GetFolderPath(oldFolderName), GetFolderPath(cleanName));
            return true;
        }

        public bool DeleteFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName) ||
                string.Equals(folderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string folderPath = GetFolderPath(folderName);
            if (!Directory.Exists(folderPath)) return false;

            // Scan the whole subtree, not just the top level: a recursive delete
            // below would otherwise silently destroy a profile sitting in a
            // nested directory.
            if (Directory.EnumerateFiles(folderPath, $"*{ProfileFileExtension}", SearchOption.AllDirectories).Any())
            {
                return false;
            }

            // A plain Directory.Delete fails with "The directory is not empty"
            // whenever the folder still holds anything the browser does not
            // list. Empty leftover subdirectories are safe to remove with the
            // folder; unrecognised files are the user's, so name them instead
            // of deleting them behind their back.
            string strayFile = Directory
                .EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (strayFile != null)
            {
                throw new IOException(
                    $"\"{folderName}\" still contains files that are not universal profiles, " +
                    $"starting with \"{Path.GetFileName(strayFile)}\". Move or remove them first.");
            }

            Directory.Delete(folderPath, recursive: true);
            return true;
        }

        public bool MoveProfile(string profilePath, string folderName, out string newProfilePath)
        {
            newProfilePath = null;
            string sourcePath = EnsurePathInsideRoot(profilePath);
            string cleanFolderName = NormalizeFolderName(folderName);
            if (string.IsNullOrWhiteSpace(cleanFolderName))
            {
                return false;
            }

            string destinationFolder = GetFolderPath(cleanFolderName);
            Directory.CreateDirectory(destinationFolder);
            string destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (File.Exists(destinationPath))
            {
                return false;
            }

            File.Move(sourcePath, destinationPath);
            UniversalProfileSummaryReader.Invalidate(sourcePath);
            newProfilePath = destinationPath;
            return true;
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
            string targetPath = string.IsNullOrWhiteSpace(previousPath)
                ? GetNamedProfilePath(profile.DisplayName)
                : Path.Combine(Path.GetDirectoryName(EnsurePathInsideRoot(previousPath)), BuildSafeFileName(profile.DisplayName));
            UniversalProfileSummary collision = EnumerateProfileSummaries().FirstOrDefault(item =>
                item.Loaded &&
                item.ProfileId != profile.ProfileId &&
                (string.Equals(item.DisplayName, profile.DisplayName, StringComparison.OrdinalIgnoreCase) ||
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
                UniversalProfileSummaryReader.Invalidate(Path.GetFullPath(previousPath));
            }
        }

        public void Delete(string profilePath)
        {
            string fullPath = EnsurePathInsideRoot(profilePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                UniversalProfileSummaryReader.Invalidate(fullPath);
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

            return EnumerateProfileSummaries()
                .FirstOrDefault(item => item.Loaded && item.ProfileId == profileId)
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
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
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

                // The profile store commonly lives under a sync-tracked folder (OneDrive)
                // that briefly locks a file while indexing it, and antivirus scanners do
                // the same. Either one landing on targetPath during the swap turned an
                // otherwise-successful save into a one-off "Failed to save" error.
                RetryOnTransientIOError(() =>
                {
                    if (File.Exists(targetPath))
                    {
                        File.Replace(tempPath, targetPath, null);
                    }
                    else
                    {
                        File.Move(tempPath, targetPath);
                    }
                });

                UniversalProfileSummaryReader.Invalidate(targetPath);
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

        private static void RetryOnTransientIOError(Action action)
        {
            const int maxAttempts = 4;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts &&
                    (ex is IOException || ex is UnauthorizedAccessException))
                {
                    System.Threading.Thread.Sleep(50 * attempt);
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
            List<UniversalProfileStoreEntry> entries = new List<UniversalProfileStoreEntry>();
            foreach (string file in EnumerateProfileFiles())
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

        // Identity-only listing. Prefer this over EnumerateProfiles wherever the
        // caller only needs names, ids or paths: it costs a header read per file
        // instead of a full parse and validation pass, and repeats are served
        // from a modification-time keyed cache.
        public IReadOnlyList<UniversalProfileSummary> EnumerateProfileSummaries()
        {
            List<UniversalProfileSummary> summaries = new List<UniversalProfileSummary>();
            foreach (string file in EnumerateProfileFiles())
            {
                summaries.Add(UniversalProfileSummaryReader.Read(file));
            }

            return summaries;
        }

        private IEnumerable<string> EnumerateProfileFiles()
        {
            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(rootPath, $"*{ProfileFileExtension}", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
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

        private static string NormalizeFolderName(string folderName)
        {
            return (folderName ?? string.Empty).Trim();
        }

        private sealed class UniversalProfileFolderNameComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase)) return 0;

                int leftRank = GetFolderSortRank(x);
                int rightRank = GetFolderSortRank(y);
                if (leftRank != rightRank)
                {
                    return leftRank.CompareTo(rightRank);
                }

                return StringComparer.CurrentCultureIgnoreCase.Compare(x, y);
            }

            private static int GetFolderSortRank(string folderName)
            {
                if (string.Equals(folderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase)) return 0;
                if (string.Equals(folderName, ProfileList.VALORANT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase)) return 1;
                return 2;
            }
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
