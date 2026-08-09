using System;
using System.IO;
using Newtonsoft.Json.Linq;
using DS4MapperTest;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class MouseOutputRoutingTests
    {
        private sealed class TestInputDevice : InputDeviceBase
        {
            public TestInputDevice(InputDeviceType deviceType, string serial)
            {
                this.deviceType = deviceType;
                this.serial = serial;
                deviceOptions = new DummyControllerOptions(deviceType);
            }

            public override void SetOperational()
            {
            }

            public override void Detach()
            {
            }
        }

        private static readonly MouseOutputRoute[] AllRoutes =
        {
            MouseOutputRoute.Gyro,
            MouseOutputRoute.JoystickMouse,
            MouseOutputRoute.FlickStick,
            MouseOutputRoute.Trackpad,
            MouseOutputRoute.TriggerMouse,
            MouseOutputRoute.Other,
            MouseOutputRoute.AbsoluteMouse,
        };

        private static readonly MouseOutputDestination[] AllDestinations =
        {
            MouseOutputDestination.SendInput,
            MouseOutputDestination.FakerInputMouse,
            MouseOutputDestination.ViiperMouse1,
            MouseOutputDestination.ViiperMouse2,
            MouseOutputDestination.ViiperMouse3,
        };

        private static AppSettingsStore CreateStore(string tempDir)
        {
            return new AppSettingsStore(Path.Combine(tempDir, "Settings.json"));
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "DS4MapperUnitTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        [TestMethod]
        public void DefaultsUseFakerInputForAllRoutes()
        {
            AppSettingsStore store = new AppSettingsStore();

            foreach (MouseOutputRoute route in AllRoutes)
            {
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse,
                    store.MouseOutputRouting.GetRouteDestination(route), route.ToString());
            }
        }

        [TestMethod]
        public void EveryRoutePersistsAndRestores()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                AppSettingsStore store = CreateStore(tempDir);
                store.GyroMouseDestination = MouseOutputDestination.SendInput;
                store.JoystickMouseDestination = MouseOutputDestination.ViiperMouse1;
                store.FlickStickMouseDestination = MouseOutputDestination.ViiperMouse2;
                store.TrackpadMouseDestination = MouseOutputDestination.ViiperMouse3;
                store.TriggerMouseDestination = MouseOutputDestination.FakerInputMouse;
                store.OtherMouseDestination = MouseOutputDestination.SendInput;
                store.AbsoluteMouseDestination = MouseOutputDestination.SendInput;

                Assert.IsTrue(store.SaveConfig());

                AppSettingsStore loaded = CreateStore(tempDir);
                Assert.IsTrue(loaded.LoadConfig());

                Assert.AreEqual(MouseOutputDestination.SendInput, loaded.GyroMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse1, loaded.JoystickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse2, loaded.FlickStickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse3, loaded.TrackpadMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.TriggerMouseDestination);
                Assert.AreEqual(MouseOutputDestination.SendInput, loaded.OtherMouseDestination);
                Assert.AreEqual(MouseOutputDestination.SendInput, loaded.AbsoluteMouseDestination);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void OlderSettingsWithoutRoutingFieldsLoadDefaults()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                string configPath = Path.Combine(tempDir, "Settings.json");
                File.WriteAllText(configPath,
                    "{\"AppVersion\":\"0.0.34\",\"ConfigVersion\":0,\"ThemeMode\":\"Dark\",\"PhysicalMouseForwardingEnabled\":true,\"SelectedPhysicalMouseId\":\"mouse-id\"}");

                AppSettingsStore loaded = CreateStore(tempDir);
                Assert.IsTrue(loaded.LoadConfig());

                foreach (MouseOutputRoute route in AllRoutes)
                {
                    Assert.AreEqual(MouseOutputDestination.FakerInputMouse,
                        loaded.MouseOutputRouting.GetRouteDestination(route), route.ToString());
                }
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void MissingRoutingValuesRecoverToDefaults()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                string configPath = Path.Combine(tempDir, "Settings.json");
                JObject json = new JObject
                {
                    ["AppVersion"] = "0.0.34",
                    ["ConfigVersion"] = 0,
                    ["ThemeMode"] = "Dark",
                    ["GyroMouseDestination"] = "SendInput",
                    ["TrackpadMouseDestination"] = "VIIPERMouse2",
                };
                File.WriteAllText(configPath, json.ToString());

                AppSettingsStore loaded = CreateStore(tempDir);
                Assert.IsTrue(loaded.LoadConfig());

                Assert.AreEqual(MouseOutputDestination.SendInput, loaded.GyroMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse2, loaded.TrackpadMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.JoystickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.FlickStickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.TriggerMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.OtherMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.AbsoluteMouseDestination);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void InvalidPersistedValuesRecoverSafely()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                string configPath = Path.Combine(tempDir, "Settings.json");
                JObject json = new JObject
                {
                    ["GyroMouseDestination"] = "BadValue",
                    ["AbsoluteMouseDestination"] = "VIIPERMouse1",
                };
                File.WriteAllText(configPath, json.ToString());

                AppSettingsStore loaded = CreateStore(tempDir);
                Assert.IsTrue(loaded.LoadConfig());

                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.GyroMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, loaded.AbsoluteMouseDestination);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void ConfiguredAndActiveDestinationsRemainDistinctDuringFallback()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.Gyro,
                MouseOutputDestination.FakerInputMouse,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: false,
                    viiperMouse1Available: true));

            Assert.AreEqual(MouseOutputDestination.FakerInputMouse, result.ConfiguredDestination);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse1, result.ActiveDestination);
            Assert.IsTrue(result.IsFallbackActive);
            Assert.AreEqual(MouseOutputFallbackReason.ConfiguredDestinationUnavailable, result.FallbackReason);
        }

        [TestMethod]
        public void TemporaryFallbackDoesNotMutateConfiguredDestination()
        {
            AppSettingsStore store = new AppSettingsStore();
            store.GyroMouseDestination = MouseOutputDestination.ViiperMouse3;

            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.Gyro,
                store.GyroMouseDestination,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: true,
                    viiperMouse3Available: false));

            Assert.AreEqual(MouseOutputDestination.ViiperMouse3, store.GyroMouseDestination);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse3, result.ConfiguredDestination);
            Assert.AreEqual(MouseOutputDestination.FakerInputMouse, result.ActiveDestination);
        }

        [TestMethod]
        public void ConfiguredSendInputRemainsSendInput()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.Other,
                MouseOutputDestination.SendInput,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: true,
                    viiperMouse2Available: true,
                    viiperMouse3Available: true));

            Assert.AreEqual(MouseOutputDestination.SendInput, result.ActiveDestination);
            Assert.IsFalse(result.IsFallbackActive);
        }

        [TestMethod]
        public void AvailableConfiguredFakerInputResolvesToFakerInput()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.Trackpad,
                MouseOutputDestination.FakerInputMouse,
                new MouseOutputRoutingAvailabilitySnapshot(fakerInputMouseAvailable: true));

            Assert.AreEqual(MouseOutputDestination.FakerInputMouse, result.ActiveDestination);
            Assert.IsFalse(result.IsFallbackActive);
        }

        [TestMethod]
        public void UnavailableConfiguredFakerInputResolvesToFirstAvailableViiper()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.Trackpad,
                MouseOutputDestination.FakerInputMouse,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: false,
                    viiperMouse2Available: true,
                    viiperMouse3Available: true));

            Assert.AreEqual(MouseOutputDestination.ViiperMouse2, result.ActiveDestination);
        }

        [TestMethod]
        public void UnavailableFakerAndUnavailableViiperResolveToSendInput()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.TriggerMouse,
                MouseOutputDestination.FakerInputMouse,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: false,
                    viiperMouse1Available: false,
                    viiperMouse2Available: false,
                    viiperMouse3Available: false));

            Assert.AreEqual(MouseOutputDestination.SendInput, result.ActiveDestination);
        }

        [TestMethod]
        public void UnavailableConfiguredViiperPrefersFakerInput()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.JoystickMouse,
                MouseOutputDestination.ViiperMouse2,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: true,
                    viiperMouse2Available: false));

            Assert.AreEqual(MouseOutputDestination.FakerInputMouse, result.ActiveDestination);
        }

        [TestMethod]
        public void UnavailableConfiguredViiperResolvesToAnotherAvailableViiperWhenFakerIsUnavailable()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.FlickStick,
                MouseOutputDestination.ViiperMouse2,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: false,
                    viiperMouse1Available: false,
                    viiperMouse2Available: false,
                    viiperMouse3Available: true));

            Assert.AreEqual(MouseOutputDestination.ViiperMouse3, result.ActiveDestination);
        }

        [TestMethod]
        public void IndependentViiperAvailabilityAndDeterministicOrderAreRespected()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();

            MouseOutputRouteResolution first = resolver.Resolve(
                MouseOutputRoute.Gyro,
                MouseOutputDestination.ViiperMouse3,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: false,
                    viiperMouse1Available: true,
                    viiperMouse2Available: true,
                    viiperMouse3Available: false));

            MouseOutputRouteResolution second = resolver.Resolve(
                MouseOutputRoute.Gyro,
                MouseOutputDestination.ViiperMouse1,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: false,
                    viiperMouse1Available: false,
                    viiperMouse2Available: true,
                    viiperMouse3Available: true));

            Assert.AreEqual(MouseOutputDestination.ViiperMouse1, first.ActiveDestination);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse2, second.ActiveDestination);
        }

        [TestMethod]
        public void RelativeRoutesPermitAllFiveDestinations()
        {
            MouseOutputRoute[] relativeRoutes =
            {
                MouseOutputRoute.Gyro,
                MouseOutputRoute.JoystickMouse,
                MouseOutputRoute.FlickStick,
                MouseOutputRoute.Trackpad,
                MouseOutputRoute.TriggerMouse,
                MouseOutputRoute.Other,
            };

            foreach (MouseOutputRoute route in relativeRoutes)
            {
                foreach (MouseOutputDestination destination in AllDestinations)
                {
                    Assert.IsTrue(MouseOutputRoutingPolicy.IsDestinationEligible(route,
                        destination, viiperAbsoluteMouseSupported: false),
                        $"{route} should allow {destination}");
                }
            }
        }

        [TestMethod]
        public void AbsoluteMouseRejectsViiperDestinationsWhenAbsoluteSupportIsUnproven()
        {
            Assert.AreEqual(MouseOutputDestination.FakerInputMouse,
                MouseOutputRoutingPolicy.SanitizeConfiguredDestination(
                    MouseOutputRoute.AbsoluteMouse,
                    MouseOutputDestination.ViiperMouse1,
                    viiperAbsoluteMouseSupported: false));

            Assert.IsFalse(MouseOutputRoutingPolicy.IsDestinationEligible(
                MouseOutputRoute.AbsoluteMouse,
                MouseOutputDestination.ViiperMouse2,
                viiperAbsoluteMouseSupported: false));
        }

        [TestMethod]
        public void AbsoluteMouseUnavailableFakerInputFallsBackToSendInput()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.AbsoluteMouse,
                MouseOutputDestination.FakerInputMouse,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: false,
                    viiperMouse1Available: true,
                    viiperAbsoluteMouseSupported: false));

            Assert.AreEqual(MouseOutputDestination.SendInput, result.ActiveDestination);
        }

        [TestMethod]
        public void AbsoluteMouseConfiguredSendInputRemainsSendInput()
        {
            MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
            MouseOutputRouteResolution result = resolver.Resolve(
                MouseOutputRoute.AbsoluteMouse,
                MouseOutputDestination.SendInput,
                new MouseOutputRoutingAvailabilitySnapshot(
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: true,
                    viiperAbsoluteMouseSupported: false));

            Assert.AreEqual(MouseOutputDestination.SendInput, result.ActiveDestination);
            Assert.IsFalse(result.IsFallbackActive);
        }

        [TestMethod]
        public void MouseRoutingConfigurationDoesNotAffectKeyboardBackendSelection()
        {
            ArgumentParser parser = new ArgumentParser();
            AppGlobalData appGlobal = new AppGlobalData();
            appGlobal.fakerInputInstalled = true;
            appGlobal.appSettings = new AppSettingsStore();

            string baseline = BackendManager.DetermineConfiguredOutputHandlerIdentifier(parser, appGlobal);

            appGlobal.appSettings.GyroMouseDestination = MouseOutputDestination.ViiperMouse1;
            appGlobal.appSettings.JoystickMouseDestination = MouseOutputDestination.SendInput;
            appGlobal.appSettings.AbsoluteMouseDestination = MouseOutputDestination.SendInput;

            string changed = BackendManager.DetermineConfiguredOutputHandlerIdentifier(parser, appGlobal);

            Assert.AreEqual(FakerInputHandler.IDENTIFIER, baseline);
            Assert.AreEqual(baseline, changed);
        }

        [TestMethod]
        public void ControllerSettingsPersistenceDoesNotReplaceGlobalMouseRoutingSettings()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.appdatapath = tempDir;
                appGlobal.baseProfilesPath = Path.Combine(tempDir, AppGlobalData.PROFILES_FOLDER_NAME);
                appGlobal.controllerConfigsPath = Path.Combine(tempDir, AppGlobalData.CONTROLLER_CONFIGS_FILENAME);
                appGlobal.appSettings = CreateStore(tempDir);
                appGlobal.appSettings.MouseOutputRouting = new MouseOutputRoutingTable
                {
                    Gyro = MouseOutputDestination.ViiperMouse1,
                    JoystickMouse = MouseOutputDestination.ViiperMouse2,
                    FlickStick = MouseOutputDestination.ViiperMouse3,
                    Trackpad = MouseOutputDestination.SendInput,
                    TriggerMouse = MouseOutputDestination.FakerInputMouse,
                    Other = MouseOutputDestination.SendInput,
                    AbsoluteMouse = MouseOutputDestination.SendInput,
                };

                appGlobal.CreateControllerDeviceSettingsFile();

                TestInputDevice device = new TestInputDevice(InputDeviceType.DS4, "00-11-22-33");
                device.Index = 0;
                appGlobal.activeProfiles[device.Index] = @"C:\Profiles\test.json";
                appGlobal.SaveControllerDeviceSettings(device, device.DeviceOptions);
                appGlobal.LoadControllerDeviceSettings(device, device.DeviceOptions);

                Assert.AreEqual(MouseOutputDestination.ViiperMouse1, appGlobal.appSettings.GyroMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse2, appGlobal.appSettings.JoystickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse3, appGlobal.appSettings.FlickStickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.SendInput, appGlobal.appSettings.TrackpadMouseDestination);
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse, appGlobal.appSettings.TriggerMouseDestination);
                Assert.AreEqual(MouseOutputDestination.SendInput, appGlobal.appSettings.OtherMouseDestination);
                Assert.AreEqual(MouseOutputDestination.SendInput, appGlobal.appSettings.AbsoluteMouseDestination);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
