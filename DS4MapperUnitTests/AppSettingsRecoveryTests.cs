using DS4MapperTest;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class AppSettingsRecoveryTests
    {
        [TestMethod]
        public void LoadConfigReportsPartialLoadForMistypedValue()
        {
            string root = CreateTempDirectory();
            try
            {
                string configPath = Path.Combine(root, AppGlobalData.APP_SETTINGS_FILENAME);
                File.WriteAllText(configPath, "{ \"ConfigVersion\": \"not-a-number\" }");

                AppSettingsStore store = new AppSettingsStore(configPath);

                Assert.IsFalse(store.LoadConfig());
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void LoadConfigKeepsValuesReadBeforeAMistypedValue()
        {
            string root = CreateTempDirectory();
            try
            {
                string configPath = Path.Combine(root, AppGlobalData.APP_SETTINGS_FILENAME);
                File.WriteAllText(configPath,
                    "{ \"ThemeMode\": \"Light\", \"ConfigVersion\": \"not-a-number\" }");

                AppSettingsStore store = new AppSettingsStore(configPath);
                store.LoadConfig();

                Assert.AreEqual("Light", store.ThemeMode);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void StartupLoadKeepsACopyOfABadSettingsFile()
        {
            string root = CreateTempDirectory();
            try
            {
                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(root);
                Directory.CreateDirectory(root);
                File.WriteAllText(appGlobal.ConfigPath, "{ \"ConfigVersion\": \"not-a-number\" }");

                appGlobal.StartupLoadAppSettings();

                Assert.IsNotNull(appGlobal.appSettings);
                Assert.IsNotNull(appGlobal.QuarantinedSettingsPath);
                Assert.IsTrue(File.Exists(appGlobal.QuarantinedSettingsPath));
                Assert.IsTrue(File.Exists(appGlobal.ConfigPath));
                Assert.AreEqual(AppGlobalData.CONFIG_VERSION, appGlobal.appSettings.ConfigVersion);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void StartupLoadRewritesAPartiallyReadableSettingsFile()
        {
            string root = CreateTempDirectory();
            try
            {
                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(root);
                Directory.CreateDirectory(root);
                File.WriteAllText(appGlobal.ConfigPath,
                    "{ \"ThemeMode\": \"Light\", \"ConfigVersion\": \"not-a-number\" }");

                appGlobal.StartupLoadAppSettings();

                // The value read before the fault survives, and the rewritten
                // file loads cleanly on the next launch.
                Assert.AreEqual("Light", appGlobal.appSettings.ThemeMode);
                Assert.IsNotNull(appGlobal.QuarantinedSettingsPath);

                AppGlobalData reloaded = new AppGlobalData();
                reloaded.SetApplicationDataRoot(root);
                reloaded.StartupLoadAppSettings();

                Assert.IsNull(reloaded.QuarantinedSettingsPath);
                Assert.AreEqual("Light", reloaded.appSettings.ThemeMode);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void StartupLoadKeepsAReadableSettingsFile()
        {
            string root = CreateTempDirectory();
            try
            {
                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(root);
                Directory.CreateDirectory(root);
                File.WriteAllText(appGlobal.ConfigPath, "{ \"ThemeMode\": \"Light\" }");

                appGlobal.StartupLoadAppSettings();

                Assert.IsNull(appGlobal.QuarantinedSettingsPath);
                Assert.AreEqual("Light", appGlobal.appSettings.ThemeMode);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"ds4mapper_settings_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
