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
        private MouseOutputDestination gyroMouseDestination = MouseOutputDestination.FakerInputMouse;
        private MouseOutputDestination joystickMouseDestination = MouseOutputDestination.FakerInputMouse;
        private MouseOutputDestination flickStickMouseDestination = MouseOutputDestination.FakerInputMouse;
        private MouseOutputDestination trackpadMouseDestination = MouseOutputDestination.FakerInputMouse;
        private MouseOutputDestination triggerMouseDestination = MouseOutputDestination.FakerInputMouse;
        private MouseOutputDestination otherMouseDestination = MouseOutputDestination.FakerInputMouse;
        private MouseOutputDestination absoluteMouseDestination = MouseOutputDestination.FakerInputMouse;

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

        public MouseOutputDestination GyroMouseDestination
        {
            get => gyroMouseDestination;
            set => gyroMouseDestination = MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                MouseOutputRoute.Gyro, value, viiperAbsoluteMouseSupported: false);
        }

        public MouseOutputDestination JoystickMouseDestination
        {
            get => joystickMouseDestination;
            set => joystickMouseDestination = MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                MouseOutputRoute.JoystickMouse, value, viiperAbsoluteMouseSupported: false);
        }

        public MouseOutputDestination FlickStickMouseDestination
        {
            get => flickStickMouseDestination;
            set => flickStickMouseDestination = MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                MouseOutputRoute.FlickStick, value, viiperAbsoluteMouseSupported: false);
        }

        public MouseOutputDestination TrackpadMouseDestination
        {
            get => trackpadMouseDestination;
            set => trackpadMouseDestination = MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                MouseOutputRoute.Trackpad, value, viiperAbsoluteMouseSupported: false);
        }

        public MouseOutputDestination TriggerMouseDestination
        {
            get => triggerMouseDestination;
            set => triggerMouseDestination = MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                MouseOutputRoute.TriggerMouse, value, viiperAbsoluteMouseSupported: false);
        }

        public MouseOutputDestination OtherMouseDestination
        {
            get => otherMouseDestination;
            set => otherMouseDestination = MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                MouseOutputRoute.Other, value, viiperAbsoluteMouseSupported: false);
        }

        public MouseOutputDestination AbsoluteMouseDestination
        {
            get => absoluteMouseDestination;
            set => absoluteMouseDestination = MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                MouseOutputRoute.AbsoluteMouse, value, viiperAbsoluteMouseSupported: false);
        }

        public MouseOutputRoutingTable MouseOutputRouting
        {
            get => new MouseOutputRoutingTable()
            {
                Gyro = GyroMouseDestination,
                JoystickMouse = JoystickMouseDestination,
                FlickStick = FlickStickMouseDestination,
                Trackpad = TrackpadMouseDestination,
                TriggerMouse = TriggerMouseDestination,
                Other = OtherMouseDestination,
                AbsoluteMouse = AbsoluteMouseDestination,
            };
            set
            {
                MouseOutputRoutingTable table = value ?? new MouseOutputRoutingTable();
                GyroMouseDestination = table.Gyro;
                JoystickMouseDestination = table.JoystickMouse;
                FlickStickMouseDestination = table.FlickStick;
                TrackpadMouseDestination = table.Trackpad;
                TriggerMouseDestination = table.TriggerMouse;
                OtherMouseDestination = table.Other;
                AbsoluteMouseDestination = table.AbsoluteMouse;
            }
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

        public string GyroMouseDestination
        {
            get => MouseOutputRoutingPolicy.SerializeDestination(settings.GyroMouseDestination);
            set => TryApplyDestination(value, destination => settings.GyroMouseDestination = destination);
        }

        public string JoystickMouseDestination
        {
            get => MouseOutputRoutingPolicy.SerializeDestination(settings.JoystickMouseDestination);
            set => TryApplyDestination(value, destination => settings.JoystickMouseDestination = destination);
        }

        public string FlickStickMouseDestination
        {
            get => MouseOutputRoutingPolicy.SerializeDestination(settings.FlickStickMouseDestination);
            set => TryApplyDestination(value, destination => settings.FlickStickMouseDestination = destination);
        }

        public string TrackpadMouseDestination
        {
            get => MouseOutputRoutingPolicy.SerializeDestination(settings.TrackpadMouseDestination);
            set => TryApplyDestination(value, destination => settings.TrackpadMouseDestination = destination);
        }

        public string TriggerMouseDestination
        {
            get => MouseOutputRoutingPolicy.SerializeDestination(settings.TriggerMouseDestination);
            set => TryApplyDestination(value, destination => settings.TriggerMouseDestination = destination);
        }

        public string OtherMouseDestination
        {
            get => MouseOutputRoutingPolicy.SerializeDestination(settings.OtherMouseDestination);
            set => TryApplyDestination(value, destination => settings.OtherMouseDestination = destination);
        }

        public string AbsoluteMouseDestination
        {
            get => MouseOutputRoutingPolicy.SerializeDestination(settings.AbsoluteMouseDestination);
            set => TryApplyDestination(value, destination => settings.AbsoluteMouseDestination = destination);
        }

        public AppSettingsSerializer(AppSettingsStore appStore)
        {
            this.settings = appStore;
        }

        private static void TryApplyDestination(string value,
            Action<MouseOutputDestination> applyDestination)
        {
            if (MouseOutputRoutingPolicy.TryParseSerializedDestination(value,
                out MouseOutputDestination destination))
            {
                applyDestination(destination);
            }
        }
    }

    public class AppSettingsMigration
    {
    }
}
