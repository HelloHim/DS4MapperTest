using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalLastProfileTests
    {
        [TestMethod]
        public void LastProfileRoundTripsPerController()
        {
            RunWithTempAppData((root) =>
            {
                UniversalLastProfileStore lastProfiles = new UniversalLastProfileStore();
                IUniversalController first = CreateController("first");
                IUniversalController second = CreateController("second");
                Guid firstProfile = Guid.NewGuid();
                Guid secondProfile = Guid.NewGuid();

                lastProfiles.SetLastProfileId(first, firstProfile);
                lastProfiles.SetLastProfileId(second, secondProfile);

                Assert.AreEqual(firstProfile, lastProfiles.GetLastProfileId(first));
                Assert.AreEqual(secondProfile, lastProfiles.GetLastProfileId(second));
            });
        }

        [TestMethod]
        public void ControllerWithoutARecordedProfileHasNoLastProfile()
        {
            RunWithTempAppData((root) =>
            {
                Assert.IsNull(new UniversalLastProfileStore().GetLastProfileId(CreateController("unknown")));
            });
        }

        [TestMethod]
        public void RecordingALastProfileKeepsControllerDeviceOptions()
        {
            RunWithTempAppData((root) =>
            {
                IUniversalController controller = CreateController("shared-entry");
                ControllerOptionsStore options = UniversalControllerDeviceOptionsStore.LoadOptions(
                    controller, InputDeviceType.None);
                options.HidePhysicalController = true;
                UniversalControllerDeviceOptionsStore.SaveOptions(controller, InputDeviceType.None, options);

                Guid profileId = Guid.NewGuid();
                new UniversalLastProfileStore().SetLastProfileId(controller, profileId);

                Assert.AreEqual(profileId, new UniversalLastProfileStore().GetLastProfileId(controller));
                Assert.IsTrue(UniversalControllerDeviceOptionsStore
                    .LoadOptions(controller, InputDeviceType.None).HidePhysicalController);
            });
        }

        [TestMethod]
        public void SelectorRestoresTheLastProfileInsteadOfTheFirstSortedProfile()
        {
            RunWithTempAppData((root) =>
            {
                UniversalProfileStore store = new UniversalProfileStore(Path.Combine(root, "Profiles"));
                UniversalProfile sortsFirst = SaveProfile(store, "Alpha");
                UniversalProfile lastUsed = SaveProfile(store, "Zulu");
                IUniversalController controller = CreateController("restore");

                UniversalLastProfileStore lastProfiles = new UniversalLastProfileStore();
                lastProfiles.SetLastProfileId(controller, lastUsed.ProfileId);

                UniversalProfile selected =
                    new UniversalProfileStoreSelector(store, lastProfileStore: lastProfiles)
                        .SelectProfile(controller);

                Assert.AreEqual(lastUsed.ProfileId, selected.ProfileId);
                Assert.AreNotEqual(sortsFirst.ProfileId, selected.ProfileId);
            });
        }

        [TestMethod]
        public void SelectorFallsBackWhenTheLastProfileIsGone()
        {
            RunWithTempAppData((root) =>
            {
                UniversalProfileStore store = new UniversalProfileStore(Path.Combine(root, "Profiles"));
                UniversalProfile sortsFirst = SaveProfile(store, "Alpha");
                IUniversalController controller = CreateController("deleted");

                UniversalLastProfileStore lastProfiles = new UniversalLastProfileStore();
                lastProfiles.SetLastProfileId(controller, Guid.NewGuid());

                UniversalProfile selected =
                    new UniversalProfileStoreSelector(store, lastProfileStore: lastProfiles)
                        .SelectProfile(controller);

                Assert.AreEqual(sortsFirst.ProfileId, selected.ProfileId);
            });
        }

        [TestMethod]
        public void SelectorWithoutALastProfileStoreStillPicksTheFirstSortedProfile()
        {
            RunWithTempAppData((root) =>
            {
                UniversalProfileStore store = new UniversalProfileStore(Path.Combine(root, "Profiles"));
                UniversalProfile sortsFirst = SaveProfile(store, "Alpha");
                SaveProfile(store, "Zulu");

                UniversalProfile selected = new UniversalProfileStoreSelector(store)
                    .SelectProfile(CreateController("no-store"));

                Assert.AreEqual(sortsFirst.ProfileId, selected.ProfileId);
            });
        }

        private static UniversalProfile SaveProfile(UniversalProfileStore store, string displayName)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = displayName,
                CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                ProfileSettings = new JObject
                {
                    ["OutputGamepadSettings"] = new JObject { ["Enabled"] = false },
                },
            };

            profile.ActionSets.Add(new UniversalProfileActionSet { Index = 0, Name = "Set 1" });
            profile.ActionSets[0].Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            store.Save(profile);
            return profile;
        }

        private static void RunWithTempAppData(Action<string> body)
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

                body(root);
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

        private static IUniversalController CreateController(string sessionId)
        {
            UniversalDeviceIdentity identity = new UniversalDeviceIdentity(
                UniversalControllerBackendIds.Sdl3,
                sessionId,
                devicePath: $"xinput#{sessionId}");

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
