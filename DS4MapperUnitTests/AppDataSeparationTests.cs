using DS4MapperTest;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class AppDataSeparationTests
    {
        [TestMethod]
        public void ForkFolderIsNotTheUpstreamOne()
        {
            // The point of the rename: two applications with incompatible
            // profile formats must not share one directory.
            Assert.AreNotEqual(AppGlobalData.LEGACY_APP_FOLDER_NAME,
                AppGlobalData.APP_FOLDER_NAME);
        }

        [TestMethod]
        public void CreatingForkConfigDoesNotTouchTheUpstreamFolder()
        {
            string container = CreateTempDirectory();
            try
            {
                string upstreamRoot = Path.Combine(container, AppGlobalData.LEGACY_APP_FOLDER_NAME);
                string upstreamProfiles = Path.Combine(upstreamRoot, AppGlobalData.PROFILES_FOLDER_NAME);
                Directory.CreateDirectory(upstreamProfiles);
                string upstreamSettings = Path.Combine(upstreamRoot, AppGlobalData.APP_SETTINGS_FILENAME);
                string upstreamProfile = Path.Combine(upstreamProfiles, "upstream-profile.json");
                File.WriteAllText(upstreamSettings, "{ \"ThemeMode\": \"Dark\" }");
                File.WriteAllText(upstreamProfile, "{ \"Owner\": \"Upstream\" }");
                DateTime settingsWriteTime = File.GetLastWriteTimeUtc(upstreamSettings);
                DateTime profileWriteTime = File.GetLastWriteTimeUtc(upstreamProfile);

                AppGlobalData appGlobal = new AppGlobalData();
                string forkRoot = Path.Combine(container, AppGlobalData.APP_FOLDER_NAME);
                appGlobal.SetApplicationDataRoot(forkRoot);

                Assert.IsTrue(appGlobal.CreateBaseConfigSkeleton());

                Assert.IsTrue(Directory.Exists(forkRoot));
                Assert.IsTrue(Directory.Exists(upstreamRoot));
                Assert.AreEqual("{ \"ThemeMode\": \"Dark\" }", File.ReadAllText(upstreamSettings));
                Assert.AreEqual("{ \"Owner\": \"Upstream\" }", File.ReadAllText(upstreamProfile));
                Assert.AreEqual(settingsWriteTime, File.GetLastWriteTimeUtc(upstreamSettings));
                Assert.AreEqual(profileWriteTime, File.GetLastWriteTimeUtc(upstreamProfile));
            }
            finally
            {
                Directory.Delete(container, recursive: true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"ds4mapper_separate_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
