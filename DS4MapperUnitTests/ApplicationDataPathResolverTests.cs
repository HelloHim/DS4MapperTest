using DS4MapperTest;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class ApplicationDataPathResolverTests
    {
        [TestMethod]
        public void DevelopmentApplicationDataPathsUseIsolatedRoot()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(
                    ApplicationDataBuildFlavor.Development,
                    tempRoot);

                string expectedRoot = Path.Combine(tempRoot, AppGlobalData.DEVELOPMENT_APP_FOLDER_NAME);
                Assert.AreEqual(Path.GetFullPath(expectedRoot), paths.RootPath);
                Assert.AreEqual(Path.Combine(paths.RootPath, AppGlobalData.PROFILES_FOLDER_NAME), paths.ProfilesPath);
                Assert.AreEqual(Path.Combine(paths.RootPath, AppGlobalData.LEGACY_PROFILES_FOLDER_NAME), paths.LegacyProfilesPath);
                Assert.AreEqual(Path.Combine(paths.RootPath, AppGlobalData.LOGS_FOLDER_NAME), paths.LogsPath);
                Assert.AreEqual(Path.Combine(paths.RootPath, AppGlobalData.APP_SETTINGS_FILENAME), paths.SettingsPath);
                Assert.AreEqual(Path.Combine(paths.RootPath, AppGlobalData.CONTROLLER_CONFIGS_FILENAME), paths.ControllerConfigsPath);
                AssertAllChildrenAreUnderRoot(paths);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void ProductionApplicationDataPathsKeepExistingRoot()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(
                    ApplicationDataBuildFlavor.Production,
                    tempRoot);

                string expectedRoot = Path.Combine(tempRoot, AppGlobalData.APP_FOLDER_NAME);
                Assert.AreEqual(Path.GetFullPath(expectedRoot), paths.RootPath);
                AssertAllChildrenAreUnderRoot(paths);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void AppGlobalDataUsesConfiguredDefaultRoot()
        {
            // The suite redirects default paths away from the real folder, so
            // drop the redirect for the length of this assertion to check what
            // a shipped build would actually resolve. Constructing
            // AppGlobalData only computes paths, so nothing is created.
            string savedOverride = Environment.GetEnvironmentVariable(
                ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE);
            try
            {
                Environment.SetEnvironmentVariable(
                    ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE, null);

                AppGlobalData appGlobal = new AppGlobalData();

                string expectedFolder = ApplicationDataPathResolver.DefaultBuildFlavor == ApplicationDataBuildFlavor.Development
                    ? AppGlobalData.DEVELOPMENT_APP_FOLDER_NAME
                    : AppGlobalData.APP_FOLDER_NAME;

                Assert.AreEqual(expectedFolder, Path.GetFileName(appGlobal.appdatapath));
                Assert.AreEqual(Path.Combine(appGlobal.appdatapath, AppGlobalData.PROFILES_FOLDER_NAME), appGlobal.baseProfilesPath);
                Assert.AreEqual(Path.Combine(appGlobal.appdatapath, AppGlobalData.LOGS_FOLDER_NAME), appGlobal.LogsPath);
                Assert.AreEqual(Path.Combine(appGlobal.appdatapath, AppGlobalData.CONTROLLER_CONFIGS_FILENAME), appGlobal.ControllerConfigsPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE, savedOverride);
            }
        }

        [TestMethod]
        public void DefaultRootHonoursTheOverrideVariable()
        {
            string savedOverride = Environment.GetEnvironmentVariable(
                ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE);
            string tempRoot = CreateTempDirectory();
            try
            {
                Environment.SetEnvironmentVariable(
                    ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE, tempRoot);

                ApplicationDataPathSet paths = ApplicationDataPathResolver.ResolveDefault();

                Assert.AreEqual(Path.GetFullPath(tempRoot), paths.RootPath);
                AssertAllChildrenAreUnderRoot(paths);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE, savedOverride);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void CurrentFormatProfileLoadsFromIsolatedDevelopmentProfileDirectory()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(
                    ApplicationDataBuildFlavor.Development,
                    tempRoot);

                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(paths.RootPath);
                appGlobal.CreateBaseConfigSkeleton();

                string profilePath = Path.Combine(
                    appGlobal.GetDeviceProfileFolderLocation(InputDeviceType.DS4),
                    ProfileList.DEFAULT_PROFILE_FOLDER,
                    "Default - XInput.json");

                string sourcePath = Path.Combine(
                    FindRepoRoot(),
                    "template_profiles",
                    "DualShock4",
                    "Default - XInput.json");

                File.Copy(sourcePath, profilePath);

                Profile profile = new Profile();
                ProfileSerializer serializer = new ProfileSerializer(profile);
                JsonConvert.PopulateObject(File.ReadAllText(profilePath), serializer);
                serializer.PopulateProfile();

                Assert.AreEqual("Default - XInput", profile.Name);
                Assert.IsTrue(profile.ActionSets.Count > 0);
                Assert.IsTrue(IsUnderRoot(profilePath, paths.RootPath));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void DeviceProfileFoldersLiveOutsideTheUniversalProfileRoot()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(
                    ApplicationDataBuildFlavor.Development,
                    tempRoot);

                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(paths.RootPath);
                appGlobal.CreateBaseConfigSkeleton();

                string deviceRoot = appGlobal.GetDeviceProfileFolderLocation(InputDeviceType.DS4);
                Assert.AreEqual(
                    Path.Combine(paths.LegacyProfilesPath, AppGlobalData.DS4_PROFILE_DIR),
                    deviceRoot);
                Assert.IsTrue(Directory.Exists(deviceRoot));
                CollectionAssert.AreEqual(
                    Array.Empty<string>(),
                    Directory.GetDirectories(paths.UniversalProfilesPath));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void ExistingDeviceProfileFoldersAreMovedOutOfTheUniversalProfileRoot()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(
                    ApplicationDataBuildFlavor.Development,
                    tempRoot);

                string strandedProfile = Path.Combine(
                    paths.UniversalProfilesPath,
                    AppGlobalData.DS4_PROFILE_DIR,
                    ProfileList.DEFAULT_PROFILE_FOLDER,
                    "Default - XInput.json");
                Directory.CreateDirectory(Path.GetDirectoryName(strandedProfile));
                File.WriteAllText(strandedProfile, "{}");

                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(paths.RootPath);
                appGlobal.CreateDeviceProfilesSkeleton();

                Assert.IsFalse(Directory.Exists(
                    Path.Combine(paths.UniversalProfilesPath, AppGlobalData.DS4_PROFILE_DIR)));
                Assert.IsTrue(File.Exists(Path.Combine(
                    paths.LegacyProfilesPath,
                    AppGlobalData.DS4_PROFILE_DIR,
                    ProfileList.DEFAULT_PROFILE_FOLDER,
                    "Default - XInput.json")));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void RelocationMergesIntoAnAlreadyMovedLegacyFolder()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(
                    ApplicationDataBuildFlavor.Development,
                    tempRoot);

                string oldDefaultFolder = Path.Combine(
                    paths.UniversalProfilesPath,
                    AppGlobalData.DS4_PROFILE_DIR,
                    ProfileList.DEFAULT_PROFILE_FOLDER);
                Directory.CreateDirectory(oldDefaultFolder);
                File.WriteAllText(Path.Combine(oldDefaultFolder, "Stranded.json"), "{}");

                string newDefaultFolder = Path.Combine(
                    paths.LegacyProfilesPath,
                    AppGlobalData.DS4_PROFILE_DIR,
                    ProfileList.DEFAULT_PROFILE_FOLDER);
                Directory.CreateDirectory(newDefaultFolder);
                File.WriteAllText(Path.Combine(newDefaultFolder, "Relocated.json"), "{}");

                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(paths.RootPath);
                appGlobal.RelocateLegacyDeviceProfileFolders();

                Assert.IsFalse(Directory.Exists(
                    Path.Combine(paths.UniversalProfilesPath, AppGlobalData.DS4_PROFILE_DIR)));
                Assert.IsTrue(File.Exists(Path.Combine(newDefaultFolder, "Stranded.json")));
                Assert.IsTrue(File.Exists(Path.Combine(newDefaultFolder, "Relocated.json")));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static void AssertAllChildrenAreUnderRoot(ApplicationDataPathSet paths)
        {
            Assert.IsTrue(IsUnderRoot(paths.ProfilesPath, paths.RootPath));
            Assert.IsTrue(IsUnderRoot(paths.LegacyProfilesPath, paths.RootPath));
            Assert.IsTrue(IsUnderRoot(paths.LogsPath, paths.RootPath));
            Assert.IsTrue(IsUnderRoot(paths.SettingsPath, paths.RootPath));
            Assert.IsTrue(IsUnderRoot(paths.ControllerConfigsPath, paths.RootPath));
        }

        private static bool IsUnderRoot(string childPath, string rootPath)
        {
            string relative = Path.GetRelativePath(rootPath, childPath);
            return relative != "." &&
                !relative.StartsWith("..", StringComparison.Ordinal) &&
                !Path.IsPathRooted(relative);
        }

        private static string CreateTempDirectory()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DS4MapperTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private static string FindRepoRoot()
        {
            string current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "template_profiles")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not locate template_profiles from the test output directory.");
        }
    }
}
