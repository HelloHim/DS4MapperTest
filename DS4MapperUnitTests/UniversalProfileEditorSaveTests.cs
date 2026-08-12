using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalProfileEditorSaveTests
    {
        [TestMethod]
        public void SavingPreservesHiddenUnsupportedBindings()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = UniversalProfileEditorPresentationTests.CreateProfile("Preserve");
                profile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = UniversalInputId.Mute,
                    ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.Mute).ValueKind,
                    Action = 1,
                });

                UniversalProfileEditorModel model = new UniversalProfileEditorModel(
                    profile, UniversalProfileEditorPresentationTests.XboxCapabilities());
                UniversalProfileEditorSaveCoordinator coordinator = new UniversalProfileEditorSaveCoordinator(store);

                UniversalProfileEditorSaveResult result = coordinator.Save(model);

                Assert.IsTrue(result.Success);
                UniversalProfile reloaded = store.Load(profile.ProfileId);
                Assert.IsTrue(reloaded.Bindings.Any(item => item.Input == UniversalInputId.Mute));
                Assert.IsTrue(reloaded.Bindings.Any(item => item.Input == UniversalInputId.FaceButtonSouth));
            }
        }

        [TestMethod]
        public void SavingFromOneFamilyDoesNotDeleteAnotherFamilysBindings()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = UniversalProfileEditorPresentationTests.CreateProfile("Cross Family");
                profile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = UniversalInputId.LeftTouchSurface,
                    ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.LeftTouchSurface).ValueKind,
                    Action = 1,
                });
                store.Save(profile);

                UniversalProfileEditorModel xboxEditModel = new UniversalProfileEditorModel(
                    store.Load(profile.ProfileId), UniversalProfileEditorPresentationTests.XboxCapabilities());
                xboxEditModel.AssignBinding(UniversalInputId.RightShoulder, 1);
                new UniversalProfileEditorSaveCoordinator(store).Save(xboxEditModel);

                UniversalProfile afterXboxEdit = store.Load(profile.ProfileId);
                Assert.IsTrue(afterXboxEdit.Bindings.Any(item => item.Input == UniversalInputId.LeftTouchSurface),
                    "Steam Controller touch-surface binding should survive an edit made under Xbox presentation.");
                Assert.IsTrue(afterXboxEdit.Bindings.Any(item => item.Input == UniversalInputId.RightShoulder));
            }
        }

        [TestMethod]
        public void ProfileIdentityIsStableAcrossLoadEditSave()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = UniversalProfileEditorPresentationTests.CreateProfile("Identity");
                Guid originalId = profile.ProfileId;
                store.Save(profile);

                UniversalProfileEditorModel model = new UniversalProfileEditorModel(store.Load(originalId));
                model.AssignBinding(UniversalInputId.RightShoulder, 1);
                new UniversalProfileEditorSaveCoordinator(store).Save(model);

                Assert.AreEqual(originalId, store.Load(originalId).ProfileId);
            }
        }

        [TestMethod]
        public void DisplayNameEditDoesNotChangeIdentityOrOverwriteAnotherProfile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profileA = UniversalProfileEditorPresentationTests.CreateProfile("Profile A");
                UniversalProfile profileB = UniversalProfileEditorPresentationTests.CreateProfile("Profile B");
                store.Save(profileA);
                store.Save(profileB);

                UniversalProfileEditorModel model = new UniversalProfileEditorModel(store.Load(profileA.ProfileId));
                model.DisplayName = "Profile A Renamed";
                new UniversalProfileEditorSaveCoordinator(store).Save(model);

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
                UniversalProfile profile = UniversalProfileEditorPresentationTests.CreateProfile("Guarded");
                store.Save(profile);
                string validJson = File.ReadAllText(store.GetProfilePath(profile.ProfileId));

                UniversalProfileEditorModel model = new UniversalProfileEditorModel(store.Load(profile.ProfileId));
                model.DisplayName = string.Empty; // required field -> validation must fail

                UniversalProfileEditorSaveResult result = new UniversalProfileEditorSaveCoordinator(store).Save(model);

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
                UniversalProfile profile = UniversalProfileEditorPresentationTests.CreateProfile("Reload");
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
                UniversalProfileEditorModel validModel = new UniversalProfileEditorModel(store.Load(profile.ProfileId));
                UniversalProfileEditorSaveResult successResult = coordinator.Save(validModel, logicalControllerId);

                Assert.IsTrue(successResult.Success);
                Assert.AreEqual(1, reloadCount);
                Assert.AreEqual(logicalControllerId, reloadedControllerId);
                Assert.AreEqual(profile.ProfileId, reloadedProfile.ProfileId);

                UniversalProfileEditorModel invalidModel = new UniversalProfileEditorModel(store.Load(profile.ProfileId));
                invalidModel.DisplayName = string.Empty;
                UniversalProfileEditorSaveResult failureResult = coordinator.Save(invalidModel, logicalControllerId);

                Assert.IsFalse(failureResult.Success);
                Assert.AreEqual(1, reloadCount, "Reload must not fire when the save failed validation.");
            }
        }

        [TestMethod]
        public void ProfileIsNotRebuiltSolelyFromVisibleControllerView()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = UniversalProfileEditorPresentationTests.CreateProfile("Full Fidelity");
                profile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = UniversalInputId.Gyroscope,
                    ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.Gyroscope).ValueKind,
                    Action = 1,
                });
                store.Save(profile);

                // A controller supporting only face buttons should still save
                // every other stored binding untouched.
                ControllerCapabilities minimal = MinimalFaceButtonCapabilities();
                UniversalProfileEditorModel model = new UniversalProfileEditorModel(store.Load(profile.ProfileId), minimal);
                new UniversalProfileEditorSaveCoordinator(store).Save(model);

                UniversalProfile reloaded = store.Load(profile.ProfileId);
                Assert.AreEqual(3, reloaded.Bindings.Count);
            }
        }

        [TestMethod]
        public void EditingWithNoControllerConnectedWorks()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = UniversalProfileEditorPresentationTests.CreateProfile("Offline Edit");
                store.Save(profile);

                UniversalProfileEditorModel model = new UniversalProfileEditorModel(store.Load(profile.ProfileId), null);
                model.AssignBinding(UniversalInputId.RightShoulder, 1);
                UniversalProfileEditorSaveResult result = new UniversalProfileEditorSaveCoordinator(store).Save(model);

                Assert.IsTrue(result.Success);
                Assert.IsTrue(store.Load(profile.ProfileId).Bindings.Any(item => item.Input == UniversalInputId.RightShoulder));
            }
        }

        private static ControllerCapabilities MinimalFaceButtonCapabilities()
        {
            return new ControllerCapabilities(
                new ControllerDisplayInfo("Minimal Pad", "generic-sdl", "generic-sdl"),
                new[]
                {
                    new ControllerInputDescriptor(UniversalInputId.FaceButtonSouth, UniversalInputValueKind.DigitalButton),
                });
        }

        private sealed class TempProfileDirectory : IDisposable
        {
            public TempProfileDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DS4MT-universal-editor-tests", Guid.NewGuid().ToString("N"));
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
