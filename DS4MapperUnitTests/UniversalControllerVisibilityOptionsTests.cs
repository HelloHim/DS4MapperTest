using DS4MapperTest;
using DS4MapperTest.Universal;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalControllerVisibilityOptionsTests
    {
        [TestMethod]
        public void UniversalHidePhysicalControllerSettingPersistsOutsideProfiles()
        {
            AppGlobalData original = AppGlobalDataSingleton.Instance;
            string root = Path.Combine(Path.GetTempPath(), "DS4MapperUnitTests",
                Guid.NewGuid().ToString("N"));

            try
            {
                AppGlobalData appGlobal = new AppGlobalData();
                appGlobal.SetApplicationDataRoot(root);
                appGlobal.CreateBaseConfigSkeleton();
                appGlobal.CreateControllerDeviceSettingsFile();
                AppGlobalDataSingleton.SetInstanceForTests(appGlobal);

                IUniversalController controller = CreateController(
                    new UniversalDeviceIdentity(
                        UniversalControllerBackendIds.Sdl3,
                        "0",
                        vendorId: 0x045E,
                        productId: 0x0B13,
                        devicePath: "xinput#0"));

                ControllerOptionsStore options =
                    UniversalControllerDeviceOptionsStore.LoadOptions(
                        controller,
                        InputDeviceType.None);
                options.HidePhysicalController = true;
                UniversalControllerDeviceOptionsStore.SaveOptions(
                    controller,
                    InputDeviceType.None,
                    options);

                ControllerOptionsStore reloaded =
                    UniversalControllerDeviceOptionsStore.LoadOptions(
                        controller,
                        InputDeviceType.None);

                Assert.IsTrue(reloaded.HidePhysicalController);
            }
            finally
            {
                AppGlobalDataSingleton.SetInstanceForTests(original);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [TestMethod]
        public void UniversalHidHideTargetAllowsSdlVidPidFallback()
        {
            Assert.IsTrue(UniversalControllerDeviceOptionsStore.HasPossibleHidHideTarget(
                new UniversalDeviceIdentity(
                    UniversalControllerBackendIds.Sdl3,
                    "0",
                    vendorId: 0x045E,
                    productId: 0x0B13,
                    devicePath: "xinput#0")));

            Assert.IsFalse(UniversalControllerDeviceOptionsStore.HasPossibleHidHideTarget(
                new UniversalDeviceIdentity(
                    UniversalControllerBackendIds.Sdl3,
                    "1",
                    devicePath: "xinput#1")));
        }

        private static IUniversalController CreateController(UniversalDeviceIdentity identity)
        {
            return new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    identity.BackendName,
                    identity.BackendSessionId,
                    identity,
                    DateTimeOffset.UtcNow),
                new ControllerCapabilities(
                    new ControllerDisplayInfo("Test Controller"),
                    Array.Empty<ControllerInputDescriptor>()),
                new UniversalControllerStateSnapshot(
                    DateTimeOffset.UtcNow,
                    1,
                    true,
                    new Dictionary<UniversalInputId, UniversalInputValue>()));
        }
    }
}
