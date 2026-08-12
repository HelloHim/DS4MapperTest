using DS4MapperTest;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalActionContentEditorSessionTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            // ProfileSerializer.EventInputMapper is normally wired up by the
            // app's backend manager at startup; keyboard output-action
            // deserialization reads it to resolve key codes.
            ProfileSerializer.EventInputMapper = new SendInputMapping();
        }

        [TestMethod]
        public void OpenLoadsOfflineMapperWithoutTouchingBackendManager()
        {
            UniversalProfile profile = CreateTwoLayerProfile();

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            Assert.IsNotNull(session.Mapper);
            Assert.AreEqual(0, session.ActionSetIndex);
            Assert.AreEqual(0, session.ActionLayerIndex);
            Assert.IsTrue(session.Mapper.ActionProfile.ActionSets.Any());
        }

        [TestMethod]
        public void SaveWithoutEditsPreservesBindingsAndOtherLayers()
        {
            UniversalProfile profile = CreateTwoLayerProfile();

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);
            UniversalProfile updated = session.BuildUpdatedProfile(profile);

            Assert.AreEqual(profile.ProfileId, updated.ProfileId);
            Assert.AreEqual(profile.Bindings.Count, updated.Bindings.Count);
            for (int i = 0; i < profile.Bindings.Count; i++)
            {
                Assert.AreEqual(profile.Bindings[i].Input, updated.Bindings[i].Input);
                Assert.AreEqual(profile.Bindings[i].Action, updated.Bindings[i].Action);
            }

            // The second action set was never opened for editing; its
            // content must be byte-for-byte untouched.
            Assert.AreEqual(profile.ActionSets[1].Layers[0].Actions[0].ToString(),
                updated.ActionSets[1].Layers[0].Actions[0].ToString());
        }

        [TestMethod]
        public void SaveWritesEditedActionContentOnlyToTargetLayer()
        {
            UniversalProfile profile = CreateTwoLayerProfile();

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            ButtonMapAction editedAction = session.Mapper.ActionProfile.ActionSets[0]
                .ActionLayers[0].buttonActionDict.Values
                .OfType<ButtonAction>()
                .Single();
            editedAction.Name = "Edited-Name-Test";

            UniversalProfile updated = session.BuildUpdatedProfile(profile);

            JObject savedAction = updated.ActionSets[0].Layers[0].Actions
                .Single(item => item.Value<int?>("Id") == 1);
            Assert.AreEqual("Edited-Name-Test", savedAction.Value<string>("Name"));

            // Untouched layer/bindings remain exactly as they were.
            Assert.AreEqual(profile.ActionSets[1].Layers[0].Actions[0].ToString(),
                updated.ActionSets[1].Layers[0].Actions[0].ToString());
            Assert.AreEqual(profile.Bindings.Count, updated.Bindings.Count);
        }

        [TestMethod]
        public void BuildUpdatedProfileRejectsMismatchedProfile()
        {
            UniversalProfile profile = CreateTwoLayerProfile();
            UniversalProfile otherProfile = CreateTwoLayerProfile();

            using UniversalActionContentEditorSession session =
                UniversalActionContentEditorSession.Open(profile, 0, 0);

            bool threw = false;
            try
            {
                session.BuildUpdatedProfile(otherProfile);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected ArgumentException for a profile the session was not opened against.");
        }

        private static UniversalProfile CreateTwoLayerProfile()
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = "Editor Session Fixture",
            };

            UniversalProfileActionSet set0 = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
            UniversalProfileActionLayer layer0 = new UniversalProfileActionLayer { Index = 0, Name = "Default" };
            layer0.Actions.Add(new JObject
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
            set0.Layers.Add(layer0);

            UniversalProfileActionSet set1 = new UniversalProfileActionSet { Index = 1, Name = "Set 2" };
            UniversalProfileActionLayer layer1 = new UniversalProfileActionLayer { Index = 0, Name = "Default" };
            layer1.Actions.Add(new JObject
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
                            ["Code"] = "Enter",
                        }),
                    }),
                },
            });
            set1.Layers.Add(layer1);

            profile.ActionSets.Add(set0);
            profile.ActionSets.Add(set1);

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
                ActionSet = 1,
                ActionLayer = 0,
                Input = UniversalInputId.FaceButtonEast,
                ValueKind = UniversalInputCatalog.GetMetadata(UniversalInputId.FaceButtonEast).ValueKind,
                Action = 1,
            });

            return profile;
        }
    }
}
