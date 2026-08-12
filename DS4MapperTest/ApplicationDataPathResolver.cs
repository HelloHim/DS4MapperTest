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

        public static ApplicationDataPathSet ResolveDefault()
        {
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
