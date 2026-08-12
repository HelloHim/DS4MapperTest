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
            string json = UniversalProfileSerializer.Serialize(profile);
            Directory.CreateDirectory(rootPath);
            string targetPath = GetProfilePath(profile.ProfileId);
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
            return UniversalProfileSerializer.Deserialize(File.ReadAllText(GetProfilePath(profileId)));
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
    }
}
