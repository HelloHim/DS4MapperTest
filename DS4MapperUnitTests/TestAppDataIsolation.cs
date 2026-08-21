using DS4MapperTest;

namespace DS4MapperUnitTests
{
    // Keeps the suite away from the real configuration directory.
    //
    // The test build configurations do not define DS4MAPPER_DEV_APPDATA, so
    // anything resolving default paths (AppGlobalData, UniversalProfileStore
    // and everything reaching them) used to land in the live production folder.
    // Running the tests therefore created and modified the configuration of
    // whoever ran them, and on a machine yet to be migrated it created the new
    // folder early, which made the app skip the one-time move of the existing
    // install and start empty.
    [TestClass]
    public static class TestAppDataIsolation
    {
        private static string rootPath;

        [AssemblyInitialize]
        public static void RedirectApplicationData(TestContext context)
        {
            rootPath = Path.Combine(Path.GetTempPath(),
                $"ds4mapper_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(rootPath);
            Environment.SetEnvironmentVariable(
                ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE, rootPath);
        }

        [AssemblyCleanup]
        public static void RestoreApplicationData()
        {
            Environment.SetEnvironmentVariable(
                ApplicationDataPathResolver.APPDATA_ROOT_OVERRIDE_VARIABLE, null);

            try
            {
                if (rootPath != null && Directory.Exists(rootPath))
                {
                    Directory.Delete(rootPath, recursive: true);
                }
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
