using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace DS4MapperTest
{
    public class ProfileList
    {
        public const string DEFAULT_PROFILE_FOLDER = "Default";
        public const string VALORANT_PROFILE_FOLDER = "VALORANT";

        private object _proLockobj = new object();
        private ObservableCollection<ProfileEntity> profileListCol =
            new ObservableCollection<ProfileEntity>();
        private ObservableCollection<string> profileFolderCol =
            new ObservableCollection<string>();

        public ObservableCollection<ProfileEntity> ProfileListCol { get => profileListCol; set => profileListCol = value; }
        public ObservableCollection<string> ProfileFolderCol { get => profileFolderCol; }

        private InputDeviceType inputDeviceType;

        public ProfileList(InputDeviceType inputDeviceType)
        {
            this.inputDeviceType = inputDeviceType;
            BindingOperations.EnableCollectionSynchronization(profileListCol, _proLockobj);
            BindingOperations.EnableCollectionSynchronization(profileFolderCol, _proLockobj);
        }

        public void Refresh()
        {
            profileListCol.Clear();
            profileFolderCol.Clear();
            string tempDirPath = AppGlobalDataSingleton.Instance.GetDeviceProfileFolderLocation(inputDeviceType);
            if (Directory.Exists(tempDirPath))
            {
                EnsureStandardFolders(tempDirPath);
                MigrateRootProfiles(tempDirPath);
                RefreshFolders(tempDirPath);

                foreach (string s in EnumerateProfileFiles(tempDirPath))
                {
                    if (s.EndsWith(".json"))
                    {
                        string json = File.ReadAllText(s);

                        try
                        {
                            ProfilePreview tempPreview =
                                JsonConvert.DeserializeObject<ProfilePreview>(json);

                            if (tempPreview == null)
                            {
                                continue;
                            }

                            // The file name is a profile's real identity: it is
                            // what survives a manual rename in Explorer or a copy
                            // to a new file. Treat it as the source of truth and
                            // repair the stored "Name" field on disk whenever the
                            // two have drifted apart, rather than showing a name
                            // that no longer matches the file it lives in.
                            string expectedName = Path.GetFileNameWithoutExtension(s);
                            string entryName = tempPreview.Name;
                            if (!string.Equals(entryName, expectedName, StringComparison.Ordinal))
                            {
                                entryName = expectedName;
                                TrySyncStoredName(s, expectedName);
                            }

                            string folderName = GetFolderName(tempDirPath, s);
                            ProfileEntity item = new ProfileEntity(path: s, name: entryName, inputDeviceType, folderName);
                            profileListCol.Add(item);
                        }
                        catch (JsonReaderException)
                        {
                        }
                        catch (JsonSerializationException)
                        {
                        }
                    }
                }

                SortProfiles();
            }
        }

        public string GetDeviceProfileRoot()
        {
            return AppGlobalDataSingleton.Instance.GetDeviceProfileFolderLocation(inputDeviceType);
        }

        public string GetFolderPath(string folderName)
        {
            return Path.Combine(GetDeviceProfileRoot(), folderName);
        }

        public bool FolderExists(string folderName)
        {
            return profileFolderCol.Any(item => string.Equals(item, folderName, StringComparison.OrdinalIgnoreCase));
        }

        public bool CreateFolder(string folderName)
        {
            string cleanName = NormalizeFolderName(folderName);
            if (string.IsNullOrWhiteSpace(cleanName) || FolderExists(cleanName))
            {
                return false;
            }

            Directory.CreateDirectory(GetFolderPath(cleanName));
            InsertFolderName(cleanName);
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

            string oldPath = GetFolderPath(oldFolderName);
            string newPath = GetFolderPath(cleanName);
            Directory.Move(oldPath, newPath);

            int folderIndex = profileFolderCol.IndexOf(oldFolderName);
            if (folderIndex >= 0)
            {
                profileFolderCol.RemoveAt(folderIndex);
            }

            foreach (ProfileEntity profile in profileListCol.Where(p => string.Equals(p.FolderName, oldFolderName, StringComparison.OrdinalIgnoreCase)))
            {
                string newProfilePath = Path.Combine(newPath, Path.GetFileName(profile.ProfilePath));
                profile.UpdatePath(newProfilePath);
                profile.FolderName = cleanName;
            }

            InsertFolderName(cleanName);
            SortProfiles();
            return true;
        }

        public bool DeleteFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName) ||
                string.Equals(folderName, DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (profileListCol.Any(p => string.Equals(p.FolderName, folderName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string folderPath = GetFolderPath(folderName);
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath);
            }

            profileFolderCol.Remove(folderName);
            return true;
        }

        public bool MoveProfile(ProfileEntity profile, string folderName)
        {
            if (profile == null || string.IsNullOrWhiteSpace(folderName) ||
                string.Equals(profile.FolderName, folderName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!FolderExists(folderName))
            {
                CreateFolder(folderName);
            }

            string destPath = Path.Combine(GetFolderPath(folderName), Path.GetFileName(profile.ProfilePath));
            if (File.Exists(destPath))
            {
                return false;
            }

            File.Move(profile.ProfilePath, destPath);
            profile.UpdatePath(destPath);
            profile.FolderName = folderName;
            SortProfiles();
            return true;
        }

        public string GetRelativeProfileName(string profilePath)
        {
            string root = GetDeviceProfileRoot();
            string relative = Path.GetRelativePath(root, profilePath);
            return Path.ChangeExtension(relative, null);
        }

        public string ResolveStoredProfilePath(string storedProfile)
        {
            if (string.IsNullOrWhiteSpace(storedProfile)) return string.Empty;

            string root = GetDeviceProfileRoot();
            string relativePath = storedProfile.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? storedProfile
                : storedProfile + ".json";
            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate)) return candidate;

            string fileName = Path.GetFileName(relativePath);
            return Directory.Exists(root)
                ? EnumerateProfileFiles(root, fileName).FirstOrDefault() ?? string.Empty
                : string.Empty;
        }

        private static void TrySyncStoredName(string path, string expectedName)
        {
            try
            {
                string json = File.ReadAllText(path);
                JObject root = JObject.Parse(json);
                root["Name"] = expectedName;
                using (StreamWriter writer = new StreamWriter(path))
                using (JsonTextWriter jwriter = new JsonTextWriter(writer))
                {
                    jwriter.Formatting = Formatting.Indented;
                    jwriter.Indentation = 2;
                    root.WriteTo(jwriter);
                }
            }
            catch (Exception)
            {
                // Best-effort repair. If the write fails the in-memory name is
                // still correct for this session; the file gets another chance
                // to be fixed up on the next scan.
            }
        }

        public void CreateProfileItem(string profilePath, string profileName,
            InputDeviceType deviceType)
        {
            lock (_proLockobj)
            {
                string deviceRoot = AppGlobalDataSingleton.Instance.GetDeviceProfileFolderLocation(deviceType);
                string folderName = GetFolderName(deviceRoot, profilePath);
                if (!FolderExists(folderName))
                {
                    InsertFolderName(folderName);
                }

                ProfileEntity tempEntity =
                    new ProfileEntity(profilePath, profileName, deviceType, folderName);
                profileListCol.Add(tempEntity);
                SortProfiles();
            }
        }

        private void EnsureStandardFolders(string deviceProfilePath)
        {
            Directory.CreateDirectory(Path.Combine(deviceProfilePath, DEFAULT_PROFILE_FOLDER));
            Directory.CreateDirectory(Path.Combine(deviceProfilePath, VALORANT_PROFILE_FOLDER));
        }

        private void MigrateRootProfiles(string deviceProfilePath)
        {
            foreach (string file in Directory.GetFiles(deviceProfilePath, "*.json", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(file);
                string destFolder = fileName.StartsWith("Default - ", StringComparison.OrdinalIgnoreCase)
                    ? DEFAULT_PROFILE_FOLDER
                    : VALORANT_PROFILE_FOLDER;
                string destPath = Path.Combine(deviceProfilePath, destFolder, fileName);

                // This migration only ever moves stray pre-folders-era files that
                // still sit directly in the device root; that location should be
                // empty otherwise. If the subfolder already has a same-named file,
                // the root copy is a leftover of an already-completed migration
                // (e.g. left behind by a move that didn't fully clear the source),
                // not a distinct profile. Numbering it with GetUniquePath used to
                // pile up an ever-growing "_1", "_2", ... duplicate of every
                // bundled default profile on each subsequent startup; discard the
                // stale root copy instead.
                if (File.Exists(destPath))
                {
                    File.Delete(file);
                    continue;
                }

                File.Move(file, destPath);
            }
        }

        private void RefreshFolders(string deviceProfilePath)
        {
            foreach (string folder in Directory.GetDirectories(deviceProfilePath, "*", SearchOption.TopDirectoryOnly))
            {
                InsertFolderName(Path.GetFileName(folder));
            }
        }

        private static IEnumerable<string> EnumerateProfileFiles(string deviceProfilePath)
        {
            foreach (string folder in Directory.EnumerateDirectories(deviceProfilePath, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
                {
                    yield return file;
                }
            }
        }

        private static IEnumerable<string> EnumerateProfileFiles(string deviceProfilePath, string fileName)
        {
            foreach (string folder in Directory.EnumerateDirectories(deviceProfilePath, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (string file in Directory.EnumerateFiles(folder, fileName, SearchOption.TopDirectoryOnly))
                {
                    yield return file;
                }
            }
        }

        private void InsertFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName) || FolderExists(folderName)) return;

            int insertIndex = profileFolderCol
                .TakeWhile(item => CompareFolderNames(item, folderName) <= 0)
                .Count();
            profileFolderCol.Insert(insertIndex, folderName);
        }

        private void SortProfiles()
        {
            List<ProfileEntity> sortedProfiles = profileListCol
                .OrderBy(item => item.FolderName, new ProfileFolderNameComparer())
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            profileListCol.Clear();
            foreach (ProfileEntity profile in sortedProfiles)
            {
                profileListCol.Add(profile);
            }
        }

        private static int CompareFolderNames(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return 0;

            int leftRank = GetFolderSortRank(left);
            int rightRank = GetFolderSortRank(right);
            if (leftRank != rightRank)
            {
                return leftRank.CompareTo(rightRank);
            }

            return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
        }

        private static int GetFolderSortRank(string folderName)
        {
            if (string.Equals(folderName, DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(folderName, VALORANT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private static string NormalizeFolderName(string folderName)
        {
            return (folderName ?? string.Empty).Trim();
        }

        private static string GetFolderName(string deviceRoot, string profilePath)
        {
            string dir = Path.GetDirectoryName(profilePath) ?? deviceRoot;
            string relativeDir = Path.GetRelativePath(deviceRoot, dir);
            return relativeDir == "." ? VALORANT_PROFILE_FOLDER : relativeDir;
        }

        private class ProfileFolderNameComparer : IComparer<string>
        {
            public int Compare(string x, string y) => CompareFolderNames(x, y);
        }
    }

    public class ProfilePreview
    {
        private string name;
        public string Name
        {
            get => name;
            set => name = value;
        }

        private string controllerType;
        public string ControllerType
        {
            get => controllerType;
            set => controllerType = value;
        }
    }
}
