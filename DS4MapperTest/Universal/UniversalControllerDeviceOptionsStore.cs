using System;
using System.IO;

namespace DS4MapperTest.Universal
{
    public static class UniversalControllerDeviceOptionsStore
    {
        private const string UNIVERSAL_KEY_PREFIX = "Universal:";

        public static ControllerOptionsStore LoadOptions(
            IUniversalController controller,
            InputDeviceType deviceType)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            AppGlobalData appGlobal = AppGlobalDataSingleton.Instance;
            EnsureControllerConfigFile(appGlobal);

            ControllerOptionsStore options = new DummyControllerOptions(deviceType);
            appGlobal.LoadControllerDeviceSettings(
                CreateDevice(controller, deviceType, options),
                options);
            return options;
        }

        public static void SaveOptions(
            IUniversalController controller,
            InputDeviceType deviceType,
            ControllerOptionsStore options)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (options == null) throw new ArgumentNullException(nameof(options));

            AppGlobalData appGlobal = AppGlobalDataSingleton.Instance;
            EnsureControllerConfigFile(appGlobal);
            appGlobal.SaveControllerDeviceSettings(
                CreateDevice(controller, deviceType, options),
                options);
        }

        public static UniversalControllerVisibilityTarget CreateVisibilityTarget(
            IUniversalController controller,
            InputDeviceType deviceType)
        {
            if (controller == null) return null;

            return new UniversalControllerVisibilityTarget(
                controller.Identity?.DeviceIdentity,
                LoadOptions(controller, deviceType),
                controller.ConnectionState == UniversalControllerConnectionState.Connected);
        }

        public static string BuildControllerKey(IUniversalController controller)
        {
            UniversalDeviceIdentity identity = controller?.Identity?.DeviceIdentity;
            if (identity == null)
            {
                return string.Empty;
            }

            string key = !string.IsNullOrWhiteSpace(identity.StrongPhysicalKey)
                ? identity.StrongPhysicalKey
                : !string.IsNullOrWhiteSpace(identity.BestEffortPersistentKey)
                    ? $"best:{identity.BestEffortPersistentKey}"
                    : !string.IsNullOrWhiteSpace(identity.Guid)
                        ? $"guid:{identity.Guid}"
                        : $"session:{identity.BackendName}:{identity.BackendSessionId}";

            return UNIVERSAL_KEY_PREFIX + key;
        }

        public static bool HasPossibleHidHideTarget(UniversalDeviceIdentity identity)
        {
            if (identity == null) return false;
            if (!string.IsNullOrWhiteSpace(
                HidHideVisibilityManager.ResolveCurrentDeviceInstancePath(identity)))
            {
                return true;
            }

            return identity.VendorId.HasValue && identity.ProductId.HasValue;
        }

        private static InputDeviceBase CreateDevice(
            IUniversalController controller,
            InputDeviceType deviceType,
            ControllerOptionsStore options)
        {
            return new UniversalControllerOptionsDevice(
                BuildControllerKey(controller),
                controller.DisplayInfo?.DisplayName ?? "Universal Controller",
                deviceType,
                options);
        }

        private static void EnsureControllerConfigFile(AppGlobalData appGlobal)
        {
            if (!Directory.Exists(appGlobal.appdatapath))
            {
                Directory.CreateDirectory(appGlobal.appdatapath);
            }

            if (!File.Exists(appGlobal.ControllerConfigsPath))
            {
                appGlobal.CreateControllerDeviceSettingsFile();
            }
        }

        private sealed class UniversalControllerOptionsDevice : InputDeviceBase
        {
            public UniversalControllerOptionsDevice(
                string key,
                string displayName,
                InputDeviceType deviceType,
                ControllerOptionsStore options)
            {
                serial = key ?? string.Empty;
                devTypeStr = displayName ?? "Universal Controller";
                this.deviceType = deviceType;
                deviceOptions = options;
                synced = true;
                primaryDevice = false;
            }

            public override void SetOperational() { }

            public override void Detach() { }
        }
    }

    public sealed class UniversalControllerVisibilityTarget
    {
        public UniversalControllerVisibilityTarget(
            UniversalDeviceIdentity identity,
            ControllerOptionsStore options,
            bool synced)
        {
            Identity = identity;
            Options = options;
            Synced = synced;
        }

        public UniversalDeviceIdentity Identity { get; }
        public ControllerOptionsStore Options { get; }
        public bool Synced { get; }
    }
}
