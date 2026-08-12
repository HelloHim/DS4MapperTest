using DS4MapperTest;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Profiles;
using DS4MapperTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace DS4MapperUnitTests
{
    // Covers the action-content editing machinery retained after the Step 8
    // preview editor was retired: UniversalActionContentEditorSession plus
    // the save coordinator, and the defects hands-on testing surfaced while
    // building the reused legacy editing surface against an offline
    // UniversalMapper.
    [TestClass]
    public class UniversalActionContentEditorUiTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ProfileSerializer.EventInputMapper = new SendInputMapping();
        }

        // UniversalProfileEditorPresentationTests.CreateProfile's action payload has no
        // Functions array, which is fine for the presentation-model tests it was written
        // for but fails MapActionTypeConverter's deserializer once a profile actually
        // round-trips through the legacy compiler/loader, as UniversalActionContentEditorSession.Open
        // does. Mirrors UniversalActionContentEditorSessionTests.CreateTwoLayerProfile's fixture shape.
        private static UniversalProfile CreateProfileWithFunctions(string name)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = name,
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
                    ["Functions"] = new JArray(new JObject
                    {
                        ["Type"] = "NormalPress",
                        ["OutputActions"] = new JArray(new JObject
                        {
                            ["Type"] = "Keyboard",
                            ["Code"] = "Space",
                        }),
                    }),
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

        private static void AddNoAction(
            UniversalProfile profile,
            UniversalInputId input,
            int actionId)
        {
            UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(input);
            string type = metadata.ValueKind switch
            {
                UniversalInputValueKind.AnalogAxis1D => "TriggerNoAction",
                UniversalInputValueKind.Stick2D => "StickNoAction",
                UniversalInputValueKind.TouchSurface => "TouchPassthruAction",
                UniversalInputValueKind.Gyroscope => "GyroPassthruAction",
                UniversalInputValueKind.Accelerometer => "GyroPassthruAction",
                _ => "ButtonNoAction",
            };

            profile.ActionSets[0].Layers[0].Actions.Add(new JObject
            {
                ["id"] = actionId,
                ["type"] = type,
                ["payload"] = new JObject
                {
                    ["Id"] = actionId,
                    ["ActionMode"] = type,
                },
            });
            profile.Bindings.Add(new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = input,
                ValueKind = metadata.ValueKind,
                Action = actionId,
            });
        }

        [TestMethod]
        public void LegacyFaceButtonPanelRecognisesUniversalBindingIds()
        {
            UniversalProfile profile = CreateProfileWithFunctions("Face Alias");

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            ProfileEntity stubEntity = new ProfileEntity(string.Empty, "test", InputDeviceType.None);
            ProfileEditorTestViewModel editorVM =
                new ProfileEditorTestViewModel(session.Mapper, stubEntity, session.Mapper.ActionProfile);
            editorVM.Test();

            // Before the alias fix, this was always empty for a UniversalMapper-backed
            // session because the panel only recognised legacy per-family ids ("A"/"Cross")
            // while UniversalLegacyBindingMap emits universal ids ("FaceButtonSouth").
            Assert.IsTrue(editorVM.FaceButtonBindings.Count > 0,
                "Face button panel should surface the profile's FaceButtonSouth binding.");
        }

        [TestMethod]
        public void LegacyDPadPanelRecognisesUniversalDPadBindings()
        {
            UniversalProfile profile = CreateProfileWithFunctions("DPad Alias");
            UniversalProfileActionLayer layer = profile.ActionSets[0].Layers[0];
            layer.Actions.Add(new JObject
            {
                ["id"] = 2,
                ["type"] = "DPadTranslateAction",
                ["payload"] = new JObject
                {
                    ["Id"] = 2,
                    ["Name"] = "DPad Action",
                    ["ActionMode"] = "DPadTranslateAction",
                    ["Settings"] = new JObject
                    {
                        ["OutputDPad"] = "X360_DPAD",
                    },
                },
            });

            foreach (UniversalInputId input in new[]
            {
                UniversalInputId.DPadUp,
                UniversalInputId.DPadDown,
                UniversalInputId.DPadLeft,
                UniversalInputId.DPadRight,
            })
            {
                profile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = input,
                    ValueKind = UniversalInputCatalog.GetMetadata(input).ValueKind,
                    Action = 2,
                });
            }

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            ProfileEditorTestViewModel editorVM = new ProfileEditorTestViewModel(
                session.Mapper,
                new ProfileEntity(string.Empty, "test", InputDeviceType.None),
                session.Mapper.ActionProfile);
            editorVM.Test();

            Assert.IsTrue(editorVM.HasDPadBindings,
                "D-pad panel should surface the universal D-pad binding as the classic DPad control.");
            Assert.AreEqual("DPad", editorVM.DPadBindings[0].BindingName);
        }

        [TestMethod]
        public void BlankUniversalProfileStillShowsEditableDPad()
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = "Blank DPad",
            };
            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Main" };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            profile.ActionSets.Add(set);

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            ProfileEditorTestViewModel editorVM = new ProfileEditorTestViewModel(
                session.Mapper,
                new ProfileEntity(string.Empty, "test", InputDeviceType.None),
                session.Mapper.ActionProfile);
            editorVM.Test();

            Assert.IsTrue(editorVM.HasDPadBindings);
            Assert.AreEqual("DPad", editorVM.DPadBindings[0].BindingName);
        }

        [TestMethod]
        public void ProcessMappingChangeActionDoesNotThrowWithNoBaseReader()
        {
            UniversalProfile profile = CreateProfileWithFunctions("No Reader");

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            Assert.IsNull(session.Mapper.BaseReader);

            bool ranInline = false;
            session.Mapper.ProcessMappingChangeAction(() => { ranInline = true; });

            Assert.IsTrue(ranInline, "The action must still run when there is no device reader to halt.");
        }

        [TestMethod]
        public void AddSetAndLayerMutateUniversalEditorModel()
        {
            UniversalProfile profile = CreateProfileWithFunctions("Sets And Layers");

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            ProfileEditorTestViewModel editorVM = new ProfileEditorTestViewModel(
                session.Mapper,
                new ProfileEntity(string.Empty, "test", InputDeviceType.None),
                session.Mapper.ActionProfile);
            editorVM.Test();

            int setIndex = editorVM.AddSet();
            int layerIndex = editorVM.AddLayer();

            Assert.AreEqual(1, setIndex);
            Assert.AreEqual(1, layerIndex);
            Assert.AreEqual(2, session.Mapper.ActionProfile.ActionSets.Count);
            Assert.AreEqual(2, session.Mapper.ActionProfile.CurrentActionSet.ActionLayers.Count);
            Assert.IsTrue(editorVM.IsProfileDirty);
        }

        [TestMethod]
        public void TouchpadSurfaceRowsShowLeftRightAndCentre()
        {
            UniversalProfile profile = CreateProfileWithFunctions("Touch Surfaces");
            AddNoAction(profile, UniversalInputId.LeftTouchSurface, 2);
            AddNoAction(profile, UniversalInputId.RightTouchSurface, 3);
            AddNoAction(profile, UniversalInputId.PrimaryTouchSurface, 4);
            AddNoAction(profile, UniversalInputId.LeftTouchSurfaceClick, 5);
            AddNoAction(profile, UniversalInputId.RightTouchSurfaceClick, 6);

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            ProfileEditorTestViewModel editorVM = new ProfileEditorTestViewModel(
                session.Mapper,
                new ProfileEntity(string.Empty, "test", InputDeviceType.None),
                session.Mapper.ActionProfile);
            editorVM.Test();

            CollectionAssert.AreEqual(
                new[] { "LeftTouchSurface", "RightTouchSurface", "PrimaryTouchSurface" },
                editorVM.TouchpadTouchSurfaceBindings
                    .Select(item => item.BindingName)
                    .ToArray());
            Assert.AreEqual("Center Touchpad", editorVM.TouchpadTouchSurfaceBindings[2].DisplayName);
            Assert.IsNotNull(editorVM.TouchpadTouchSurfaceBindings[0].TouchpadClickBinding);
            Assert.IsNotNull(editorVM.TouchpadTouchSurfaceBindings[1].TouchpadClickBinding);
        }

        [TestMethod]
        public void OpenActionContentEditorTargetsSelectedSetAndLayer()
        {
            using TempProfileDirectory temp = new TempProfileDirectory();
            UniversalProfileStore store = new UniversalProfileStore(temp.Path);
            UniversalProfile profile = CreateProfileWithFunctions("Open Session");
            store.Save(profile);

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(store.Load(profile.ProfileId), 0, 0);

            Assert.IsNotNull(session);
            Assert.AreEqual(0, session.ActionSetIndex);
            Assert.AreEqual(0, session.ActionLayerIndex);
        }

        [TestMethod]
        public void SaveActionContentPersistsEditOnlyToTargetLayer()
        {
            using TempProfileDirectory temp = new TempProfileDirectory();
            UniversalProfileStore store = new UniversalProfileStore(temp.Path);
            UniversalProfile profile = CreateProfileWithFunctions("Edit And Save");
            store.Save(profile);

            UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(store.Load(profile.ProfileId), 0, 0);
            try
            {
                ButtonAction editedAction = session.Mapper.ActionProfile.ActionSets[0]
                    .ActionLayers[0].buttonActionDict.Values
                    .OfType<ButtonAction>()
                    .Single();
                editedAction.Name = "Step8-Edited-Name";

                UniversalProfile updated = session.BuildUpdatedProfile(store.Load(profile.ProfileId));
                UniversalProfileEditorSaveResult result =
                    new UniversalProfileEditorSaveCoordinator(store).SaveProfile(updated);

                Assert.IsTrue(result.Success);
            }
            finally
            {
                session.Dispose();
            }

            UniversalProfile reloaded = store.Load(profile.ProfileId);
            JObject savedAction = reloaded.ActionSets[0].Layers[0].Actions
                .Single(item => item.Value<int?>("Id") == 1);
            Assert.AreEqual("Step8-Edited-Name", savedAction.Value<string>("Name"));

            // The binding assigning FaceButtonSouth/LeftTrigger to action 1 must survive untouched -
            // action-content edits must never touch the binding table.
            Assert.AreEqual(2, reloaded.Bindings.Count);
        }

        [TestMethod]
        public void CancellingEditLeavesStoredProfileUnchanged()
        {
            using TempProfileDirectory temp = new TempProfileDirectory();
            UniversalProfileStore store = new UniversalProfileStore(temp.Path);
            UniversalProfile profile = CreateProfileWithFunctions("Cancel Edit");
            store.Save(profile);
            string beforeJson = File.ReadAllText(store.GetProfilePath(profile.ProfileId));

            UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(store.Load(profile.ProfileId), 0, 0);
            try
            {
                ButtonAction editedAction = session.Mapper.ActionProfile.ActionSets[0]
                    .ActionLayers[0].buttonActionDict.Values
                    .OfType<ButtonAction>()
                    .Single();
                editedAction.Name = "Should-Never-Be-Saved";

                // Cancel: never call viewModel.SaveActionContent - mirrors the dialog's
                // Cancel button, which just closes without invoking Save.
            }
            finally
            {
                session.Dispose();
            }

            string afterJson = File.ReadAllText(store.GetProfilePath(profile.ProfileId));
            Assert.AreEqual(beforeJson, afterJson, "A cancelled edit must not touch the persisted profile.");
        }

        [TestMethod]
        public void SaveActionContentRejectsSessionOpenedAgainstAnotherProfile()
        {
            using TempProfileDirectory temp = new TempProfileDirectory();
            UniversalProfileStore store = new UniversalProfileStore(temp.Path);
            UniversalProfile profileA = CreateProfileWithFunctions("Profile A");
            UniversalProfile profileB = CreateProfileWithFunctions("Profile B");
            store.Save(profileA);
            store.Save(profileB);

            using UniversalActionContentEditorSession sessionForA =
                UniversalActionContentEditorSession.Open(store.Load(profileA.ProfileId), 0, 0);

            bool threw = false;
            try
            {
                sessionForA.BuildUpdatedProfile(store.Load(profileB.ProfileId));
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            Assert.IsTrue(threw,
                "Saving a session opened against a profile that is no longer the selected one must throw, not silently write into the wrong profile.");
        }

        private sealed class TempProfileDirectory : IDisposable
        {
            public TempProfileDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DS4MT-action-content-ui-tests", Guid.NewGuid().ToString("N"));
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
