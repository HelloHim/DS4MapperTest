using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace DS4MapperTest
{
    public class AppSettingsStore
    {
        private string configPath;
        private int configVersion = AppGlobalData.CONFIG_VERSION;
        private string themeMode = ThemeService.DEFAULT_THEME_MODE;

        // Phase-2 physical-mouse forwarding. Disabled by default; phase 3's
        // device-picker UI will read/write these same two properties, so the
        // stored shape shouldn't need to change - see
        // DS4MapperTest.PhysicalMouse.PhysicalMouseService.
        private bool physicalMouseForwardingEnabled = false;
        private string selectedPhysicalMouseId = string.Empty;

        public int ConfigVersion
        {
            get => configVersion;
            set => configVersion = value;
        }

        public string ThemeMode
        {
            get => themeMode;
            set => themeMode = value;
        }

        public bool PhysicalMouseForwardingEnabled
        {
            get => physicalMouseForwardingEnabled;
            set => physicalMouseForwardingEnabled = value;
        }

        /// <summary>
        /// Stable Raw Input device path (PhysicalMouseDevice.StableId), not a
        /// transient hDevice. Empty/null means no device configured.
        /// </summary>
        public string SelectedPhysicalMouseId
        {
            get => selectedPhysicalMouseId;
            set => selectedPhysicalMouseId = value;
        }

        public AppSettingsStore()
        {
        }

        public AppSettingsStore(string configPath)
        {
            this.configPath = configPath;
        }

        public bool LoadConfig()
        {
            bool result = false;

            if (string.IsNullOrEmpty(configPath) ||
                !File.Exists(configPath))
            {
                throw new Exception($"Passed path {configPath} does not exist");
            }

            using (StreamReader sreader = new StreamReader(configPath))
            {
                string json = sreader.ReadToEnd();
                AppSettingsSerializer settingsSerializer =
                    new AppSettingsSerializer(this);

                try
                {
                    JsonConvert.PopulateObject(json, settingsSerializer);
                }
                catch (JsonSerializationException)
                {
                }
            }

            result = true;
            return result;
        }

        public bool SaveConfig()
        {
            bool result = false;

            if (string.IsNullOrEmpty(configPath))
            {
                return false;
            }

            AppSettingsSerializer settingsSerializer =
                    new AppSettingsSerializer(this);
            string json = JsonConvert.SerializeObject(settingsSerializer);
            AtomicFileWriter.WriteText(configPath, json);

            result = true;
            return result;
        }
    }

    public class AppSettingsSerializer
    {
        private AppSettingsStore settings;

        // Only serialize current app version. Don't care about reading value
        public string AppVersion
        {
            get => AppGlobalData.exeversion;
        }

        public int ConfigVersion
        {
            get => settings.ConfigVersion;
            set => settings.ConfigVersion = value;
        }

        public string ThemeMode
        {
            get => settings.ThemeMode;
            set => settings.ThemeMode = value;
        }

        public bool PhysicalMouseForwardingEnabled
        {
            get => settings.PhysicalMouseForwardingEnabled;
            set => settings.PhysicalMouseForwardingEnabled = value;
        }

        public string SelectedPhysicalMouseId
        {
            get => settings.SelectedPhysicalMouseId;
            set => settings.SelectedPhysicalMouseId = value;
        }

        public AppSettingsSerializer(AppSettingsStore appStore)
        {
            this.settings = appStore;
        }
    }

    public class AppSettingsMigration
    {
    }
}
