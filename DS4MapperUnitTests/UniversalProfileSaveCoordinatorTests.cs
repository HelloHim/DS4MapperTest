using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace DS4MapperUnitTests
{
    // Covers the guarantees the save coordinator makes on top of the store:
    // validation runs before anything is written, profile identity survives a
    // load/edit/save round trip, and the runtime reload hook fires only when
    // the write actually happened.
    [TestClass]
    public class UniversalProfileSaveCoordinatorTests
    {
        [TestMethod]
        public void ProfileIdentityIsStableAcrossLoadEditSave()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Identity");
                Guid originalId = profile.ProfileId;
                store.Save(profile);

                UniversalProfile edited = store.Load(originalId);
                edited.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = UniversalInputId.RightShoulder,
                    ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.RightShoulder).ValueKind,
                    Action = 1,
                });
                new UniversalProfileEditorSaveCoordinator(store).SaveProfile(edited);

                Assert.AreEqual(originalId, store.Load(originalId).ProfileId);
                Assert.IsTrue(store.Load(originalId).Bindings
                    .Any(item => item.Input == UniversalInputId.RightShoulder));
            }
        }

        [TestMethod]
        public void DisplayNameEditDoesNotChangeIdentityOrOverwriteAnotherProfile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profileA = CreateProfile("Profile A");
                UniversalProfile profileB = CreateProfile("Profile B");
                store.Save(profileA);
                store.Save(profileB);

                UniversalProfile renamed = store.Load(profileA.ProfileId);
                renamed.DisplayName = "Profile A Renamed";
                new UniversalProfileEditorSaveCoordinator(store).SaveProfile(renamed);

                Assert.AreEqual(profileA.ProfileId, store.Load(profileA.ProfileId).ProfileId);
                Assert.AreEqual("Profile A Renamed", store.Load(profileA.ProfileId).DisplayName);
                Assert.AreEqual("Profile B", store.Load(profileB.ProfileId).DisplayName);
            }
        }

        [TestMethod]
        public void MalformedEditDoesNotReplaceLastValidProfile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Guarded");
                store.Save(profile);
                string validJson = File.ReadAllText(store.GetProfilePath(profile.ProfileId));

                UniversalProfile malformed = store.Load(profile.ProfileId);
                malformed.DisplayName = string.Empty; // required field -> validation must fail

                UniversalProfileEditorSaveResult result =
                    new UniversalProfileEditorSaveCoordinator(store).SaveProfile(malformed);

                Assert.IsFalse(result.Success);
                Assert.IsTrue(result.Issues.Count > 0);
                string onDiskJson = File.ReadAllText(store.GetProfilePath(profile.ProfileId));
                Assert.AreEqual(validJson, onDiskJson);
            }
        }

        [TestMethod]
        public void SuccessfulSaveWritesAtomicallyAndTriggersReloadOnlyOnSuccess()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Reload");
                store.Save(profile);

                int reloadCount = 0;
                Guid reloadedControllerId = Guid.Empty;
                UniversalProfile reloadedProfile = null;
                UniversalProfileEditorSaveCoordinator coordinator = new UniversalProfileEditorSaveCoordinator(
                    store,
                    (controllerId, updatedProfile) =>
                    {
                        reloadCount++;
                        reloadedControllerId = controllerId;
                        reloadedProfile = updatedProfile;
                    });

                Guid logicalControllerId = Guid.NewGuid();
                UniversalProfileEditorSaveResult successResult =
                    coordinator.SaveProfile(store.Load(profile.ProfileId), logicalControllerId);

                Assert.IsTrue(successResult.Success);
                Assert.AreEqual(1, reloadCount);
                Assert.AreEqual(logicalControllerId, reloadedControllerId);
                Assert.AreEqual(profile.ProfileId, reloadedProfile.ProfileId);

                UniversalProfile invalid = store.Load(profile.ProfileId);
                invalid.DisplayName = string.Empty;
                UniversalProfileEditorSaveResult failureResult =
                    coordinator.SaveProfile(invalid, logicalControllerId);

                Assert.IsFalse(failureResult.Success);
                Assert.AreEqual(1, reloadCount, "Reload must not fire when the save failed validation.");
            }
        }

        [TestMethod]
        public void SavingLeavesUnsupportedBindingsUntouched()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Preserve");
                profile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = UniversalInputId.Mute,
                    ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.Mute).ValueKind,
                    Action = 1,
                });

                UniversalProfileEditorSaveResult result =
                    new UniversalProfileEditorSaveCoordinator(store).SaveProfile(profile);

                Assert.IsTrue(result.Success);
                UniversalProfile reloaded = store.Load(profile.ProfileId);
                Assert.IsTrue(reloaded.Bindings.Any(item => item.Input == UniversalInputId.Mute));
                Assert.IsTrue(reloaded.Bindings.Any(item => item.Input == UniversalInputId.FaceButtonSouth));
            }
        }

        internal static UniversalProfile CreateProfile(string name)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = name,
                CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            };

            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
            UniversalProfileActionLayer layer = new UniversalProfileActionLayer { Index = 0, Name = "Default" };
            layer.Actions.Add(new JObject
            {
                ["id"] = 1,
                ["type"] = "ButtonAction",
                ["payload"] = new JObject
                {
                    ["Id"] = 1,
                    ["ActionMode"] = "ButtonAction",
                },
            });
            set.Layers.Add(layer);
            profile.ActionSets.Add(set);

            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = UniversalInputId.FaceButtonSouth,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.FaceButtonSouth).ValueKind,
                Action = 1,
            });
            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = UniversalInputId.LeftTrigger,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.LeftTrigger).ValueKind,
                Action = 1,
            });

            return profile;
        }

        private sealed class TempProfileDirectory : IDisposable
        {
            public TempProfileDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "DS4MT-universal-save-tests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                string tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
                string fullPath = System.IO.Path.GetFullPath(Path);
                if (fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, recursive: true);
                }
            }
        }
    }
}
