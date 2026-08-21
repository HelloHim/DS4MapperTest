using System;
using System.IO;

namespace DS4MapperTest
{
    public enum ApplicationDataBuildFlavor
    {
        Production,
        Development,
    }

    public sealed class ApplicationDataPathSet
    {
        public string RootPath { get; }
        public string ProfilesPath { get; }
        public string UniversalProfilesPath { get; }
        public string LegacyProfilesPath { get; }
        public string LogsPath { get; }
        public string SettingsPath { get; }
        public string ControllerConfigsPath { get; }

        public ApplicationDataPathSet(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Application data root path cannot be empty.", nameof(rootPath));
            }

            RootPath = Path.GetFullPath(rootPath);
            ProfilesPath = Path.Combine(RootPath, AppGlobalData.PROFILES_FOLDER_NAME);
            UniversalProfilesPath = ProfilesPath;
            // Per-controller directories are storage for the pre-universal
            // profile format. They are kept only as a migration source, so they
            // live outside the universal profile root: every directory directly
            // under that root is presented to the user as a profile folder.
            LegacyProfilesPath = Path.Combine(RootPath, AppGlobalData.LEGACY_PROFILES_FOLDER_NAME);
            LogsPath = Path.Combine(RootPath, AppGlobalData.LOGS_FOLDER_NAME);
            SettingsPath = Path.Combine(RootPath, AppGlobalData.APP_SETTINGS_FILENAME);
            ControllerConfigsPath = Path.Combine(RootPath, AppGlobalData.CONTROLLER_CONFIGS_FILENAME);
        }
    }

    public static class ApplicationDataPathResolver
    {
        public const string DevelopmentAppFolderName = "DS4TestUniversalDev";

        public static ApplicationDataBuildFlavor DefaultBuildFlavor
        {
            get
            {
#if DS4MAPPER_DEV_APPDATA
                return ApplicationDataBuildFlavor.Development;
#else
                return ApplicationDataBuildFlavor.Production;
#endif
            }
        }

        // Redirects every default path lookup at the named directory.
        //
        // The test project sets this. Without it the suite resolves the real
        // production folder, because the test build configurations do not
        // define DS4MAPPER_DEV_APPDATA, so simply running the tests created and
        // wrote to the live configuration directory of whoever ran them. That
        // is bad on its own, and it also defeated the one-time move of an
        // existing install: the folder already existed by the time the app
        // started, so the move was skipped and the user got an empty install.
        public const string APPDATA_ROOT_OVERRIDE_VARIABLE = "DS4MAPPERTEST_APPDATA_ROOT";

        public static ApplicationDataPathSet ResolveDefault()
        {
            string overrideRoot =
                Environment.GetEnvironmentVariable(APPDATA_ROOT_OVERRIDE_VARIABLE);
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return new ApplicationDataPathSet(overrideRoot);
            }

            return Resolve(DefaultBuildFlavor);
        }

        public static ApplicationDataPathSet Resolve(ApplicationDataBuildFlavor flavor)
        {
            string roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Resolve(flavor, roamingRoot);
        }

        public static ApplicationDataPathSet Resolve(ApplicationDataBuildFlavor flavor, string appDataRoot)
        {
            string appFolderName = flavor == ApplicationDataBuildFlavor.Development
                ? DevelopmentAppFolderName
                : AppGlobalData.APP_FOLDER_NAME;

            return new ApplicationDataPathSet(Path.Combine(appDataRoot, appFolderName));
        }
    }
}
