using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalProfileTests
    {
        [TestMethod]
        public void ProfileCanBeCreatedWithoutController()
        {
            UniversalProfile profile = CreateProfile("No Controller");

            Assert.AreNotEqual(Guid.Empty, profile.ProfileId);
            Assert.AreEqual(UniversalProfile.CurrentSchemaVersion, profile.SchemaVersion);
            Assert.IsTrue(UniversalProfileValidator.Validate(profile).IsValid);
        }

        [TestMethod]
        public void DisplayNameChangeDoesNotChangeProfileIdentity()
        {
            UniversalProfile profile = CreateProfile("Original");
            Guid id = profile.ProfileId;

            profile.DisplayName = "Renamed";

            Assert.AreEqual(id, profile.ProfileId);
        }

        [TestMethod]
        public void PublicProfileModelDoesNotExposeBackendHandles()
        {
            string[] prohibited =
            {
                "Sdl",
                "Hid",
                "DS4Button",
                "DualSenseButton",
                "SteamControllerState",
            };

            Type[] types =
            {
                typeof(UniversalProfile),
                typeof(UniversalProfileBinding),
                typeof(UniversalProfileActionSet),
                typeof(UniversalProfileActionLayer),
            };

            foreach (PropertyInfo property in types.SelectMany(type => type.GetProperties()))
            {
                Assert.IsFalse(prohibited.Any(item => property.PropertyType.Name.Contains(item)),
                    $"{property.DeclaringType.Name}.{property.Name} exposes {property.PropertyType.Name}");
            }
        }

        [TestMethod]
        public void SerializerUsesStableTextualInputTokens()
        {
            UniversalProfile profile = CreateProfile("Tokens");
            string json = UniversalProfileSerializer.Serialize(profile);

            StringAssert.Contains(json, "\"input\": \"face-button-south\"");
            Assert.IsFalse(json.Contains($"\"input\": {(ushort)UniversalInputId.FaceButtonSouth}"));
            Assert.IsFalse(json.Contains("Cross"));
            Assert.IsFalse(json.Contains("Circle"));
        }

        [TestMethod]
        public void RepresentativeProfileRoundTripsDeterministically()
        {
            UniversalProfile profile = CreateProfile("Round Trip");

            string first = UniversalProfileSerializer.Serialize(profile);
            UniversalProfile loaded = UniversalProfileSerializer.Deserialize(first);
            string second = UniversalProfileSerializer.Serialize(loaded);

            Assert.AreEqual(first, second);
            Assert.AreEqual(profile.ProfileId, loaded.ProfileId);
            Assert.AreEqual(2, loaded.Bindings.Count);
            Assert.AreEqual("ButtonAction", loaded.ActionSets[0].Layers[0].Actions[0].Value<string>("type"));
        }

        [TestMethod]
        public void UnknownRootFieldsArePreserved()
        {
            UniversalProfile profile = CreateProfile("Extension");
            JObject json = JObject.Parse(UniversalProfileSerializer.Serialize(profile));
            json["futureField"] = new JObject { ["value"] = 5 };

            UniversalProfile loaded = UniversalProfileSerializer.Deserialize(json.ToString());
            string roundTrip = UniversalProfileSerializer.Serialize(loaded);

            StringAssert.Contains(roundTrip, "\"futureField\"");
            StringAssert.Contains(roundTrip, "\"value\": 5");
        }

        [TestMethod]
        public void MissingSchemaVersionFailsClearly()
        {
            JObject json = JObject.Parse(UniversalProfileSerializer.Serialize(CreateProfile("Missing Version")));
            json.Remove("schemaVersion");

            UniversalProfileLoadException ex = ExpectException<UniversalProfileLoadException>(
                () => UniversalProfileSerializer.Deserialize(json.ToString()));

            Assert.AreEqual(UniversalProfileLoadStatus.MissingVersion, ex.Status);
        }

        [TestMethod]
        public void FutureSchemaVersionFailsWithoutRewrite()
        {
            JObject json = JObject.Parse(UniversalProfileSerializer.Serialize(CreateProfile("Future")));
            json["schemaVersion"] = UniversalProfile.CurrentSchemaVersion + 1;

            UniversalProfileLoadException ex = ExpectException<UniversalProfileLoadException>(
                () => UniversalProfileSerializer.Deserialize(json.ToString()));

            Assert.AreEqual(UniversalProfileLoadStatus.UnsupportedFutureVersion, ex.Status);
        }

        [TestMethod]
        public void MalformedJsonFailsClearly()
        {
            UniversalProfileLoadException ex = ExpectException<UniversalProfileLoadException>(
                () => UniversalProfileSerializer.Deserialize("{ nope"));

            Assert.AreEqual(UniversalProfileLoadStatus.Malformed, ex.Status);
        }

        [TestMethod]
        public void UnknownInputTokenFailsClearly()
        {
            JObject json = JObject.Parse(UniversalProfileSerializer.Serialize(CreateProfile("Unknown Input")));
            json["bindings"][0]["input"] = "future-input";

            UniversalProfileLoadException ex = ExpectException<UniversalProfileLoadException>(
                () => UniversalProfileSerializer.Deserialize(json.ToString()));

            Assert.AreEqual(UniversalProfileLoadStatus.ValidationFailed, ex.Status);
            StringAssert.Contains(ex.Message, "future-input");
        }

        [TestMethod]
        public void ValueKindMismatchIsRejected()
        {
            UniversalProfile profile = CreateProfile("Mismatch");
            profile.Bindings[0].ValueKind = UniversalInputValueKind.Stick2D;

            ExpectException<UniversalProfileValidationException>(
                () => UniversalProfileSerializer.Serialize(profile));
        }

        [TestMethod]
        public void DuplicateBindingIsRejectedDeterministically()
        {
            UniversalProfile profile = CreateProfile("Duplicate");
            profile.Bindings.Add(profile.Bindings[0].Clone());

            UniversalProfileValidationResult result = UniversalProfileValidator.Validate(profile);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(item => item.Message.Contains("Duplicate binding")));
        }

        [TestMethod]
        public void UnsupportedBindingsRemainStoredWithoutCapabilities()
        {
            UniversalProfile profile = CreateProfile("Unsupported");
            profile.Bindings.Add(CreateBinding(UniversalInputId.LeftRearSecondary, 1));
            profile.Bindings.Add(CreateBinding(UniversalInputId.RightTouchSurface, 1));
            profile.Bindings.Add(CreateBinding(UniversalInputId.Mute, 1));

            UniversalProfile loaded = UniversalProfileSerializer.Deserialize(
                UniversalProfileSerializer.Serialize(profile));

            CollectionAssert.AreEquivalent(
                profile.Bindings.Select(item => item.Input).ToArray(),
                loaded.Bindings.Select(item => item.Input).ToArray());
        }

        [TestMethod]
        public void StoreUsesFlatControllerIndependentDirectory()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Flat");

                store.Save(profile);
                string saved = store.GetProfilePath(profile.ProfileId);

                Assert.AreEqual(temp.Path, Directory.GetParent(saved).FullName);
                Assert.IsTrue(File.Exists(saved));
                Assert.IsFalse(Directory.EnumerateDirectories(temp.Path).Any());
            }
        }

        [TestMethod]
        public void SafeFilenameResolutionRejectsTraversalAndRootedPaths()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);

                ExpectException<ArgumentException>(() => store.ResolveRelativeProfilePath("..\\bad.universal-profile.json"));
                ExpectException<ArgumentException>(() => store.ResolveRelativeProfilePath(Path.GetFullPath("bad.universal-profile.json")));
                ExpectException<ArgumentException>(() => store.ResolveRelativeProfilePath("CON.universal-profile.json"));
            }
        }

        [TestMethod]
        public void SameDisplayNamesDoNotOverwriteProfiles()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile first = CreateProfile("Same");
                UniversalProfile second = CreateProfile("Same");

                store.Save(first);
                store.Save(second);

                Assert.AreNotEqual(store.GetProfilePath(first.ProfileId), store.GetProfilePath(second.ProfileId));
                Assert.AreEqual(2, store.EnumerateProfiles().Count(item => item.Loaded));
            }
        }

        [TestMethod]
        public void NamedSaveSanitisesFilenameAndRenamesFile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("CON");

                store.SaveNamed(profile, store.GetNamedProfilePath(profile.DisplayName));
                string firstPath = store.FindProfilePath(profile.ProfileId);

                Assert.AreEqual("_CON.universal-profile.json", Path.GetFileName(firstPath));
                Assert.IsTrue(File.Exists(firstPath));

                profile.DisplayName = "bad:name/with*chars?";
                store.SaveNamed(profile, firstPath);
                string secondPath = store.FindProfilePath(profile.ProfileId);

                Assert.AreEqual("bad_name_with_chars_.universal-profile.json", Path.GetFileName(secondPath));
                Assert.IsFalse(File.Exists(firstPath));
                Assert.IsTrue(File.Exists(secondPath));
            }
        }

        [TestMethod]
        public void NamedSaveRejectsDisplayNameAndSanitisedFilenameCollisions()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile first = CreateProfile("Same");
                UniversalProfile second = CreateProfile("same");
                UniversalProfile third = CreateProfile("bad/name");
                UniversalProfile fourth = CreateProfile("bad:name");

                store.SaveNamed(first, store.GetNamedProfilePath(first.DisplayName));
                ExpectException<InvalidOperationException>(() =>
                    store.SaveNamed(second, store.GetNamedProfilePath(second.DisplayName)));

                store.SaveNamed(third, store.GetNamedProfilePath(third.DisplayName));
                ExpectException<InvalidOperationException>(() =>
                    store.SaveNamed(fourth, store.GetNamedProfilePath(fourth.DisplayName)));
            }
        }

        [TestMethod]
        public void ClassicProfileListBridgeListsEveryUniversalProfile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                store.SaveNamed(CreateProfile("Xbox Shared"), store.GetNamedProfilePath("Xbox Shared"));
                store.SaveNamed(CreateProfile("Steam Shared"), store.GetNamedProfilePath("Steam Shared"));

                UniversalClassicProfileList list = new UniversalClassicProfileList(store);
                list.Refresh();

                CollectionAssert.AreEquivalent(
                    new[] { "Xbox Shared", "Steam Shared" },
                    list.Profiles.Select(item => item.Name).ToArray());
                Assert.IsTrue(list.Profiles.All(item => item.InputDeviceType == InputDeviceType.None));
            }
        }

        [TestMethod]
        public void ClassicProfileListBridgeListsUniversalProfileFolders()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                store.CreateFolder("Aim");
                store.SaveNamed(CreateProfile("Desktop"), store.GetNamedProfilePath("Desktop", ProfileList.DEFAULT_PROFILE_FOLDER));
                store.SaveNamed(CreateProfile("Valorant"), store.GetNamedProfilePath("Valorant", "Aim"));

                UniversalClassicProfileList list = new UniversalClassicProfileList(store);
                list.Refresh();

                CollectionAssert.Contains(list.Folders.ToArray(), ProfileList.DEFAULT_PROFILE_FOLDER);
                CollectionAssert.Contains(list.Folders.ToArray(), "Aim");
                Assert.AreEqual("Aim", list.Profiles.Single(item => item.Name == "Valorant").FolderName);
            }
        }

        [TestMethod]
        public void ClassicProfileListBridgeMovesUniversalProfileBetweenFolders()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Move Me");
                store.SaveNamed(profile, store.GetNamedProfilePath(profile.DisplayName, ProfileList.DEFAULT_PROFILE_FOLDER));
                UniversalClassicProfileList list = new UniversalClassicProfileList(store);

                ProfileEntity entry = list.Profiles.Single(item => item.Name == "Move Me");
                bool moved = list.MoveProfile(entry, "Arcade");

                Assert.IsTrue(moved);
                Assert.AreEqual("Arcade", entry.FolderName);
                StringAssert.Contains(entry.ProfilePath, $"{Path.DirectorySeparatorChar}Arcade{Path.DirectorySeparatorChar}");
                Assert.IsTrue(File.Exists(entry.ProfilePath));
                Assert.AreEqual(entry.ProfilePath, store.FindProfilePath(profile.ProfileId));
            }
        }

        [TestMethod]
        public void CopiedProfileTakesANewIdAndAFreeName()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile source = CreateProfile("Desktop");
                source.Migration = new UniversalProfileMigrationProvenance
                {
                    SourceFamily = InputDeviceType.DS4.ToString(),
                    SourceIdentity = "DualShock4/Default/Desktop.json",
                };
                store.SaveNamed(source, store.GetNamedProfilePath("Desktop"));

                UniversalProfile copy = UniversalProfileDuplicator.PrepareCopy(
                    store.LoadFromPath(store.FindProfilePath(source.ProfileId)),
                    store.EnumerateProfileSummaries());
                store.SaveNamed(copy, store.GetNamedProfilePath(copy.DisplayName));

                Assert.AreNotEqual(source.ProfileId, copy.ProfileId);
                Assert.AreEqual("Desktop copy", copy.DisplayName);
                Assert.IsNull(copy.Migration);
                Assert.AreEqual(2, store.EnumerateProfileSummaries().Count(item => item.Loaded));
                Assert.AreNotEqual(
                    store.FindProfilePath(source.ProfileId),
                    store.FindProfilePath(copy.ProfileId));
            }
        }

        [TestMethod]
        public void RepeatedCopiesNumberThemselvesInsteadOfColliding()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile source = CreateProfile("Desktop");
                store.SaveNamed(source, store.GetNamedProfilePath("Desktop"));

                for (int round = 0; round < 3; round++)
                {
                    UniversalProfile copy = UniversalProfileDuplicator.PrepareCopy(
                        store.LoadFromPath(store.FindProfilePath(source.ProfileId)),
                        store.EnumerateProfileSummaries());
                    store.SaveNamed(copy, store.GetNamedProfilePath(copy.DisplayName));
                }

                CollectionAssert.AreEquivalent(
                    new[] { "Desktop", "Desktop copy", "Desktop copy (2)", "Desktop copy (3)" },
                    store.EnumerateProfileSummaries().Select(item => item.DisplayName).ToArray());
            }
        }

        [TestMethod]
        public void ImportKeepsAFreeProfileIdAndReplacesAClashingOne()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile resident = CreateProfile("Desktop");
                store.SaveNamed(resident, store.GetNamedProfilePath("Desktop"));

                UniversalProfile freshImport = CreateProfile("Aim");
                Guid originalId = freshImport.ProfileId;
                UniversalProfileDuplicator.PrepareImport(freshImport, store.EnumerateProfileSummaries());

                Assert.AreEqual(originalId, freshImport.ProfileId);
                Assert.AreEqual("Aim", freshImport.DisplayName);

                UniversalProfile clashingImport = CreateProfile("Desktop");
                clashingImport.ProfileId = resident.ProfileId;
                UniversalProfileDuplicator.PrepareImport(clashingImport, store.EnumerateProfileSummaries());

                Assert.AreNotEqual(resident.ProfileId, clashingImport.ProfileId);
                Assert.AreEqual("Desktop (2)", clashingImport.DisplayName);
            }
        }

        [TestMethod]
        public void ImportWithoutANameFallsBackToAPlaceholder()
        {
            UniversalProfile imported = CreateProfile("Anything");
            imported.DisplayName = "   ";

            UniversalProfileDuplicator.PrepareImport(imported, Array.Empty<UniversalProfileSummary>());

            Assert.AreEqual(UniversalProfileDuplicator.DefaultImportName, imported.DisplayName);
        }

        [TestMethod]
        public void SummariesIdentifyEveryStoredProfileWithoutFullParses()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile first = CreateProfile("Desktop");
                UniversalProfile second = CreateProfile("Aim");
                store.SaveNamed(first, store.GetNamedProfilePath("Desktop", ProfileList.DEFAULT_PROFILE_FOLDER));
                store.SaveNamed(second, store.GetNamedProfilePath("Aim", ProfileList.DEFAULT_PROFILE_FOLDER));

                var summaries = store.EnumerateProfileSummaries();

                CollectionAssert.AreEquivalent(
                    new[] { "Desktop", "Aim" },
                    summaries.Where(item => item.Loaded).Select(item => item.DisplayName).ToArray());
                CollectionAssert.AreEquivalent(
                    new[] { first.ProfileId, second.ProfileId },
                    summaries.Where(item => item.Loaded).Select(item => item.ProfileId).ToArray());
            }
        }

        [TestMethod]
        public void SummaryOfAnUnreadableProfileIsReportedAsNotLoaded()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                store.SaveNamed(CreateProfile("Good"), store.GetNamedProfilePath("Good"));
                File.WriteAllText(Path.Combine(temp.Path, $"{Guid.NewGuid():D}.universal-profile.json"), "{ nope");

                var summaries = store.EnumerateProfileSummaries();

                Assert.AreEqual(2, summaries.Count);
                Assert.AreEqual(1, summaries.Count(item => item.Loaded));
                Assert.AreEqual("Good", summaries.Single(item => item.Loaded).DisplayName);
            }
        }

        [TestMethod]
        public void SummariesFollowRenamesAndDeletesOfTheSameFile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Before");
                store.SaveNamed(profile, store.GetNamedProfilePath("Before"));

                Assert.AreEqual("Before", store.EnumerateProfileSummaries().Single().DisplayName);

                // Rewrite the same path so a stale cache entry would be reused
                // if modification time were not part of the cache key.
                profile.DisplayName = "After";
                store.SaveNamed(profile, store.GetNamedProfilePath("Before"));

                Assert.AreEqual("After", store.EnumerateProfileSummaries().Single().DisplayName);
                Assert.AreEqual(
                    store.FindProfilePath(profile.ProfileId),
                    store.EnumerateProfileSummaries().Single().Path);

                store.Delete(store.FindProfilePath(profile.ProfileId));
                Assert.AreEqual(0, store.EnumerateProfileSummaries().Count);
            }
        }

        [TestMethod]
        public void SummaryExposesMigrationSourceFamily()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Migrated");
                profile.Migration = new UniversalProfileMigrationProvenance
                {
                    SourceFamily = InputDeviceType.SteamController.ToString(),
                    SourceIdentity = "SteamController/Default/sample.json",
                };
                store.Save(profile);

                Assert.AreEqual(
                    InputDeviceType.SteamController.ToString(),
                    store.EnumerateProfileSummaries().Single().MigrationSourceFamily);
            }
        }

        [TestMethod]
        public void DeleteFolderRemovesEmptyLeftoverSubdirectories()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                store.CreateFolder("Retired");
                Directory.CreateDirectory(Path.Combine(store.GetFolderPath("Retired"), "Default"));
                Directory.CreateDirectory(Path.Combine(store.GetFolderPath("Retired"), "VALORANT"));

                Assert.IsTrue(store.DeleteFolder("Retired"));
                Assert.IsFalse(Directory.Exists(store.GetFolderPath("Retired")));
            }
        }

        [TestMethod]
        public void DeleteFolderReportsUnrecognisedLeftoverFiles()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                store.CreateFolder("Retired");
                string strayFolder = Path.Combine(store.GetFolderPath("Retired"), "Default");
                Directory.CreateDirectory(strayFolder);
                File.WriteAllText(Path.Combine(strayFolder, "Default - XInput.json"), "{}");

                IOException error = ExpectException<IOException>(() => store.DeleteFolder("Retired"));

                StringAssert.Contains(error.Message, "Default - XInput.json");
                Assert.IsTrue(Directory.Exists(store.GetFolderPath("Retired")));
            }
        }

        [TestMethod]
        public void DeleteFolderKeepsProfilesStoredInSubdirectories()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                store.CreateFolder("Retired");
                string nested = Path.Combine(store.GetFolderPath("Retired"), "Nested");
                Directory.CreateDirectory(nested);
                File.WriteAllText(
                    Path.Combine(nested, $"{Guid.NewGuid():D}.universal-profile.json"),
                    "{}");

                Assert.IsFalse(store.DeleteFolder("Retired"));
                Assert.IsTrue(Directory.Exists(nested));
            }
        }

        [TestMethod]
        public void FailedSavePreservesPreviousFile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                UniversalProfile profile = CreateProfile("Valid");
                store.Save(profile);
                string path = store.GetProfilePath(profile.ProfileId);
                string previous = File.ReadAllText(path);

                profile.DisplayName = string.Empty;
                ExpectException<UniversalProfileValidationException>(() => store.Save(profile));

                Assert.AreEqual(previous, File.ReadAllText(path));
            }
        }

        [TestMethod]
        public void TemporaryFilesAreNotEnumeratedAsProfiles()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                File.WriteAllText(System.IO.Path.Combine(temp.Path, ".partial.tmp"), "{}");
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);

                Assert.AreEqual(0, store.EnumerateProfiles().Count);
            }
        }

        [TestMethod]
        public void OneBadFileDoesNotBlockValidEnumeration()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                store.Save(CreateProfile("Valid"));
                File.WriteAllText(System.IO.Path.Combine(temp.Path, $"{Guid.NewGuid():D}.universal-profile.json"), "{ nope");

                var entries = store.EnumerateProfiles();

                Assert.AreEqual(2, entries.Count);
                Assert.AreEqual(1, entries.Count(item => item.Loaded));
                Assert.AreEqual(1, entries.Count(item => item.Error != null));
            }
        }

        [TestMethod]
        public void DevelopmentRootProvidesUniversalProfileDirectory()
        {
            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(ApplicationDataBuildFlavor.Development, tempRoot);

            StringAssert.Contains(paths.UniversalProfilesPath, ApplicationDataPathResolver.DevelopmentAppFolderName);
            Assert.AreEqual(paths.ProfilesPath, paths.UniversalProfilesPath);
            Assert.IsTrue(paths.UniversalProfilesPath.StartsWith(System.IO.Path.GetFullPath(tempRoot), StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void MigrationPreviewPerformsNoWrites()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(new UniversalProfileStore(temp.Path));

                ProfileMigrationReport report = migrator.Preview(Source(InputDeviceType.DS4, "DualShock4/Default/sample.json"));

                Assert.AreEqual(ProfileMigrationStatus.Preview, report.Status);
                Assert.IsFalse(Directory.EnumerateFiles(temp.Path).Any());
                Assert.IsNotNull(report.Profile);
                Assert.IsTrue(report.Profile.Bindings.Any(item => item.Input == UniversalInputId.FaceButtonSouth));
            }
        }

        [TestMethod]
        public void MigrationWritesUniversalProfileAndManifest()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(store);

                ProfileMigrationReport report = migrator.Migrate(Source(InputDeviceType.SwitchPro, "SwitchPro/sample.json"));

                Assert.AreEqual(ProfileMigrationStatus.Success, report.Status);
                Assert.IsTrue(File.Exists(store.GetProfilePath(report.UniversalProfileId)));
                string manifest = File.ReadAllText(System.IO.Path.Combine(temp.Path, "_universal-profile-migration-manifest.json"));
                Assert.IsFalse(manifest.Contains(temp.Path));
                Assert.IsFalse(manifest.Contains(Environment.UserName));
            }
        }

        [TestMethod]
        public void RepeatedMigrationIsIdempotent()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(store);
                LegacyProfileMigrationSource source = Source(InputDeviceType.DualSense, "DualSense/sample.json");

                ProfileMigrationReport first = migrator.Migrate(source);
                ProfileMigrationReport second = migrator.Migrate(source);

                Assert.AreEqual(ProfileMigrationStatus.Success, first.Status);
                Assert.AreEqual(ProfileMigrationStatus.AlreadyMigrated, second.Status);
                Assert.AreEqual(first.UniversalProfileId, second.UniversalProfileId);
                Assert.AreEqual(1, store.EnumerateProfiles().Count(item => item.Loaded));
            }
        }

        [TestMethod]
        public void ChangedLegacySourceReportsConflict()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(new UniversalProfileStore(temp.Path));
                migrator.Migrate(Source(InputDeviceType.DS4, "DualShock4/sample.json"));

                ProfileMigrationReport changed = migrator.Migrate(Source(InputDeviceType.DS4, "DualShock4/sample.json", "Changed"));

                Assert.AreEqual(ProfileMigrationStatus.Conflict, changed.Status);
                Assert.IsTrue(changed.HasErrors);
            }
        }

        [TestMethod]
        public void SameNamedLegacyProfilesMigrateSeparately()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(store);

                migrator.Migrate(Source(InputDeviceType.DS4, "DualShock4/a.json"));
                migrator.Migrate(Source(InputDeviceType.DualSense, "DualSense/a.json"));

                Assert.AreEqual(2, store.EnumerateProfiles().Count(item => item.Loaded));
            }
        }

        [TestMethod]
        public void UnknownLegacyInputBlocksWritingProfile()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(new UniversalProfileStore(temp.Path));
                string json = LegacyJson("Mystery");

                ProfileMigrationReport report = migrator.Migrate(new LegacyProfileMigrationSource(InputDeviceType.DS4, "DualShock4/bad.json", json));

                Assert.AreEqual(ProfileMigrationStatus.Failed, report.Status);
                Assert.IsFalse(Directory.EnumerateFiles(temp.Path, "*.universal-profile.json").Any());
            }
        }

        [TestMethod]
        public void MigrationDoesNotModifyLegacySourceText()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                string legacyPath = System.IO.Path.Combine(temp.Path, "legacy.json");
                string legacy = LegacyJson("Cross");
                File.WriteAllText(legacyPath, legacy);
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(new UniversalProfileStore(System.IO.Path.Combine(temp.Path, "out")));

                migrator.Migrate(new LegacyProfileMigrationSource(InputDeviceType.DS4, "DualShock4/legacy.json", File.ReadAllText(legacyPath)));

                Assert.AreEqual(legacy, File.ReadAllText(legacyPath));
            }
        }

        [TestMethod]
        public void MigrationMapsFaceButtonsByPhysicalPosition()
        {
            AssertMaps(InputDeviceType.DS4, "Cross", UniversalInputId.FaceButtonSouth);
            AssertMaps(InputDeviceType.DualSense, "Circle", UniversalInputId.FaceButtonEast);
            AssertMaps(InputDeviceType.SwitchPro, "B", UniversalInputId.FaceButtonSouth);
            AssertMaps(InputDeviceType.JoyCon, "A", UniversalInputId.FaceButtonEast);
            AssertMaps(InputDeviceType.EightBitDoUltimate2Wireless, "X", UniversalInputId.FaceButtonWest);
            AssertMaps(InputDeviceType.SteamController, "Y", UniversalInputId.FaceButtonNorth);
        }

        [TestMethod]
        public void MigrationPreservesTouchpadPolicy()
        {
            ProfileMigrationReport ds4 = MigrateFixture(InputDeviceType.DS4, "TouchpadLeft");
            Assert.IsTrue(ds4.Profile.Bindings.Any(item => item.Input == UniversalInputId.PrimaryTouchSurface));
            Assert.IsFalse(ds4.Profile.Bindings.Any(item => item.Input == UniversalInputId.LeftTouchSurface));
            Assert.IsTrue(ds4.HasWarnings);

            ProfileMigrationReport steam = MigrateFixture(InputDeviceType.SteamController, "LeftTouchpad");
            Assert.IsTrue(steam.Profile.Bindings.Any(item => item.Input == UniversalInputId.LeftTouchSurface));
        }

        [TestMethod]
        public void MigrationPreservesSteamControllerIndependentControls()
        {
            AssertMaps(InputDeviceType.SteamController, "LT", UniversalInputId.LeftTrigger);
            AssertMaps(InputDeviceType.SteamController, "LeftPadClick", UniversalInputId.LeftTouchSurfaceClick);
            AssertMaps(InputDeviceType.SteamController, "RightPadTouch", UniversalInputId.RightTouchContact);
            AssertMaps(InputDeviceType.SteamController, "LeftGrip", UniversalInputId.LeftRearPrimary);
            AssertMaps(InputDeviceType.SteamController, "Gyro", UniversalInputId.Gyroscope);
        }

        [TestMethod]
        public void MigrationMapsTritonSpecialControlsOnlyWhereKnown()
        {
            AssertMaps(InputDeviceType.SteamControllerTriton, "QAM", UniversalInputId.QuickAccessMenu);
            AssertMaps(InputDeviceType.SteamControllerTriton, "LSTouch", UniversalInputId.LeftStickTouch);
            AssertMaps(InputDeviceType.SteamControllerTriton, "LeftGripSense", UniversalInputId.LeftGripTouch);
            AssertMaps(InputDeviceType.SteamControllerTriton, "L5", UniversalInputId.LeftRearSecondary);
        }

        [TestMethod]
        public void MigrationExpandsLegacyWholeDPad()
        {
            ProfileMigrationReport report = MigrateFixture(InputDeviceType.SwitchPro, "DPad");

            Assert.IsTrue(report.Profile.Bindings.Any(item => item.Input == UniversalInputId.DPadUp));
            Assert.IsTrue(report.Profile.Bindings.Any(item => item.Input == UniversalInputId.DPadDown));
            Assert.IsTrue(report.Profile.Bindings.Any(item => item.Input == UniversalInputId.DPadLeft));
            Assert.IsTrue(report.Profile.Bindings.Any(item => item.Input == UniversalInputId.DPadRight));
        }

        [TestMethod]
        public void MigrationPreservesActionPayloadAndSettings()
        {
            ProfileMigrationReport report = MigrateFixture(InputDeviceType.DS4, "Cross");
            JObject action = report.Profile.ActionSets[0].Layers[0].Actions[0];

            Assert.AreEqual(7, action.Value<int>("id"));
            Assert.AreEqual("ButtonAction", action.Value<string>("type"));
            Assert.AreEqual(true, action["payload"]["Functions"][0]["Settings"]["Toggle"].Value<bool>());
            Assert.AreEqual(14.2857, report.Profile.ProfileSettings.Value<double>("CalibRwc"));
            Assert.IsNotNull(report.Profile.ProfileSettings["OutputGamepadSettings"]);
        }

        [TestMethod]
        public void MigrationExcludesControllerSpecificProfileIdentity()
        {
            ProfileMigrationReport report = MigrateFixture(InputDeviceType.DS4, "Cross");
            string json = UniversalProfileSerializer.Serialize(report.Profile);

            Assert.IsFalse(json.Contains("\"ControllerType\""));
            Assert.IsFalse(json.Contains("DualShock4"));
            Assert.IsFalse(json.Contains("HID#"));
        }

        [TestMethod]
        public void PartialBatchFailureIsReportedWithoutBlockingOtherSources()
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                UniversalProfileStore store = new UniversalProfileStore(temp.Path);
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(store);

                var reports = migrator.MigrateBatch(new[]
                {
                    Source(InputDeviceType.DS4, "DualShock4/good.json"),
                    new LegacyProfileMigrationSource(InputDeviceType.DS4, "DualShock4/bad.json", LegacyJson("Unknown")),
                }, preview: false);

                Assert.AreEqual(2, reports.Count);
                Assert.AreEqual(ProfileMigrationStatus.Success, reports[0].Status);
                Assert.AreEqual(ProfileMigrationStatus.Failed, reports[1].Status);
                Assert.AreEqual(1, store.EnumerateProfiles().Count(item => item.Loaded));
            }
        }

        private static UniversalProfile CreateProfile(string name)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = name,
                CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                ProfileSettings = new JObject
                {
                    ["OutputGamepadSettings"] = new JObject { ["Enabled"] = false },
                },
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
            profile.Bindings.Add(CreateBinding(UniversalInputId.FaceButtonSouth, 1));
            profile.Bindings.Add(CreateBinding(UniversalInputId.LeftTrigger, 1));
            return profile;
        }

        private static UniversalProfileBinding CreateBinding(UniversalInputId input, int action)
        {
            return new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = input,
                ValueKind = UniversalInputCatalog.GetMetadata(input).ValueKind,
                Action = action,
            };
        }

        private static LegacyProfileMigrationSource Source(InputDeviceType family, string sourceIdentity, string nameSuffix = "")
        {
            return new LegacyProfileMigrationSource(family, sourceIdentity, LegacyJson(DefaultInputFor(family), nameSuffix));
        }

        private static ProfileMigrationReport MigrateFixture(InputDeviceType family, string input)
        {
            using (TempProfileDirectory temp = new TempProfileDirectory())
            {
                LegacyProfileMigrator migrator = new LegacyProfileMigrator(new UniversalProfileStore(temp.Path));
                return migrator.Preview(new LegacyProfileMigrationSource(family, $"{family}/fixture.json", LegacyJson(input)));
            }
        }

        private static void AssertMaps(InputDeviceType family, string input, UniversalInputId expected)
        {
            ProfileMigrationReport report = MigrateFixture(family, input);

            Assert.IsFalse(report.HasErrors, string.Join("; ", report.Issues.Select(item => item.Message)));
            Assert.IsTrue(report.Profile.Bindings.Any(item => item.Input == expected), $"{family}:{input} did not map to {expected}");
        }

        private static string DefaultInputFor(InputDeviceType family)
        {
            switch (family)
            {
                case InputDeviceType.SwitchPro:
                case InputDeviceType.JoyCon:
                    return "B";
                case InputDeviceType.EightBitDoUltimate2Wireless:
                case InputDeviceType.SteamController:
                case InputDeviceType.SteamControllerTriton:
                    return "A";
                default:
                    return "Cross";
            }
        }

        private static string LegacyJson(string input, string nameSuffix = "")
        {
            return @"{
  ""Name"": ""Fixture" + nameSuffix + @""",
  ""Description"": ""Synthetic fixture"",
  ""CreationDate"": ""2026-01-02T03:04:05Z"",
  ""ProfileSpecVersion"": 2,
  ""ControllerType"": ""DualShock4"",
  ""OutputGamepadSettings"": {
    ""Enabled"": false
  },
  ""CalibRwc"": 14.2857,
  ""CalibInGameSens"": 1.0,
  ""CalibCounts"": 5142.852,
  ""CalibMode"": ""CountsMode"",
  ""CalibPreset"": ""VALORANT"",
  ""ActionSets"": [
    {
      ""Index"": 0,
      ""Name"": ""Main"",
      ""ActionLayers"": [
        {
          ""Index"": 0,
          ""Name"": ""Default"",
          ""MappedActions"": [
            {
              ""Id"": 7,
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                {
                  ""Type"": ""NormalPress"",
                  ""OutputActions"": [
                    {
                      ""Type"": ""Keyboard"",
                      ""Code"": ""Space""
                    }
                  ],
                  ""Settings"": {
                    ""Toggle"": true,
                    ""FireDelayMs"": 100
                  }
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  ""Mappings"": [
    {
      ""ActionSet"": 0,
      ""ActionLayer"": 0,
      ""InputMappings"": [
        {
          ""Input"": """ + input + @""",
          ""Action"": 7
        }
      ]
    }
  ]
}";
        }

        private static TException ExpectException<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
            }

            Assert.Fail($"Expected {typeof(TException).Name}.");
            return null;
        }

        private sealed class TempProfileDirectory : IDisposable
        {
            public TempProfileDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DS4MT-universal-profile-tests", Guid.NewGuid().ToString("N"));
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
