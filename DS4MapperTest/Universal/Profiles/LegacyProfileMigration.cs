using DS4MapperTest.MapperUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DS4MapperTest.Universal.Profiles
{
    public enum ProfileMigrationStatus
    {
        Success,
        SuccessWithWarnings,
        Preview,
        AlreadyMigrated,
        Conflict,
        Failed,
    }

    public sealed class ProfileMigrationIssue
    {
        public ProfileMigrationIssue(UniversalProfileValidationSeverity severity, string location, string message)
        {
            Severity = severity;
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public UniversalProfileValidationSeverity Severity { get; }
        public string Location { get; }
        public string Message { get; }
    }

    public sealed class ProfileMigrationReport
    {
        private readonly List<ProfileMigrationIssue> issues = new List<ProfileMigrationIssue>();

        public ProfileMigrationStatus Status { get; set; }
        public InputDeviceType SourceFamily { get; set; }
        public string SourceIdentity { get; set; } = string.Empty;
        public string SourceContentHash { get; set; } = string.Empty;
        public Guid UniversalProfileId { get; set; }
        public string OutputFileName { get; set; } = string.Empty;
        public UniversalProfile Profile { get; set; }
        public IReadOnlyList<ProfileMigrationIssue> Issues => issues;
        public bool HasErrors => issues.Any(item => item.Severity == UniversalProfileValidationSeverity.Error);
        public bool HasWarnings => issues.Any(item => item.Severity == UniversalProfileValidationSeverity.Warning);

        public void AddError(string location, string message)
        {
            issues.Add(new ProfileMigrationIssue(UniversalProfileValidationSeverity.Error, location, message));
        }

        public void AddWarning(string location, string message)
        {
            issues.Add(new ProfileMigrationIssue(UniversalProfileValidationSeverity.Warning, location, message));
        }
    }

    public sealed class LegacyProfileMigrationSource
    {
        public LegacyProfileMigrationSource(InputDeviceType family, string relativeSourceIdentity, string json)
        {
            Family = family;
            RelativeSourceIdentity = relativeSourceIdentity ?? string.Empty;
            Json = json ?? string.Empty;
        }

        public InputDeviceType Family { get; }
        public string RelativeSourceIdentity { get; }
        public string Json { get; }
    }

    public sealed class LegacyProfileMigrator
    {
        public const int MigrationSchemaVersion = 1;
        private const string ManifestFileName = "_universal-profile-migration-manifest.json";
        private readonly UniversalProfileStore store;

        public LegacyProfileMigrator(UniversalProfileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ProfileMigrationReport Preview(LegacyProfileMigrationSource source)
        {
            return Migrate(source, writeProfile: false);
        }

        public ProfileMigrationReport Migrate(LegacyProfileMigrationSource source)
        {
            return Migrate(source, writeProfile: true);
        }

        public IReadOnlyList<ProfileMigrationReport> MigrateBatch(IEnumerable<LegacyProfileMigrationSource> sources, bool preview)
        {
            return sources.Select(source => preview ? Preview(source) : Migrate(source)).ToArray();
        }

        private ProfileMigrationReport Migrate(LegacyProfileMigrationSource source, bool writeProfile)
        {
            ProfileMigrationReport report = new ProfileMigrationReport
            {
                SourceFamily = source.Family,
                SourceIdentity = source.RelativeSourceIdentity,
                SourceContentHash = ComputeHash(source.Json),
                Status = writeProfile ? ProfileMigrationStatus.Success : ProfileMigrationStatus.Preview,
            };

            if (!IsSafeRelativeIdentity(source.RelativeSourceIdentity))
            {
                report.AddError("$.source", "Migration source identity must be relative and machine-independent.");
                report.Status = ProfileMigrationStatus.Failed;
                return report;
            }

            MigrationManifest manifest = LoadManifest();
            MigrationManifestEntry existing = manifest.Find(source.Family, source.RelativeSourceIdentity);
            if (writeProfile && existing != null)
            {
                if (string.Equals(existing.SourceContentHash, report.SourceContentHash, StringComparison.Ordinal))
                {
                    report.Status = ProfileMigrationStatus.AlreadyMigrated;
                    report.UniversalProfileId = existing.UniversalProfileId;
                    report.OutputFileName = Path.GetFileName(store.GetProfilePath(existing.UniversalProfileId));
                    return report;
                }

                report.Status = ProfileMigrationStatus.Conflict;
                report.UniversalProfileId = existing.UniversalProfileId;
                report.AddError("$.source", "Legacy source changed after migration; existing universal profile was not overwritten.");
                return report;
            }

            JObject root;
            try
            {
                root = ParseJsonObject(source.Json);
            }
            catch (JsonException ex)
            {
                report.Status = ProfileMigrationStatus.Failed;
                report.AddError("$", $"Legacy profile JSON is malformed: {ex.Message}");
                return report;
            }

            UniversalProfile profile = ConvertProfile(source, root, report);
            if (report.HasErrors)
            {
                report.Status = ProfileMigrationStatus.Failed;
                return report;
            }

            UniversalProfileValidationResult validation = UniversalProfileValidator.Validate(profile);
            foreach (UniversalProfileValidationIssue issue in validation.Issues)
            {
                if (issue.Severity == UniversalProfileValidationSeverity.Error)
                {
                    report.AddError(issue.Location, issue.Message);
                }
                else
                {
                    report.AddWarning(issue.Location, issue.Message);
                }
            }

            if (report.HasErrors)
            {
                report.Status = ProfileMigrationStatus.Failed;
                return report;
            }

            report.Profile = profile;
            report.UniversalProfileId = profile.ProfileId;
            report.OutputFileName = Path.GetFileName(store.GetProfilePath(profile.ProfileId));
            if (report.HasWarnings && !writeProfile)
            {
                report.Status = ProfileMigrationStatus.Preview;
            }
            else if (report.HasWarnings)
            {
                report.Status = ProfileMigrationStatus.SuccessWithWarnings;
            }

            if (!writeProfile)
            {
                return report;
            }

            store.Save(profile);
            manifest.Entries.Add(new MigrationManifestEntry
            {
                SourceFamily = source.Family.ToString(),
                SourceIdentity = source.RelativeSourceIdentity,
                SourceContentHash = report.SourceContentHash,
                UniversalProfileId = profile.ProfileId,
                MigrationSchemaVersion = MigrationSchemaVersion,
                Outcome = report.Status.ToString(),
                Warnings = report.Issues
                    .Where(item => item.Severity == UniversalProfileValidationSeverity.Warning)
                    .Select(item => $"{item.Location}: {item.Message}")
                    .ToList(),
            });
            SaveManifest(manifest);
            return report;
        }

        private UniversalProfile ConvertProfile(
            LegacyProfileMigrationSource source,
            JObject root,
            ProfileMigrationReport report)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = root.Value<string>("Name") ?? Path.GetFileNameWithoutExtension(source.RelativeSourceIdentity),
                Description = root.Value<string>("Description") ?? string.Empty,
                CreatedUtc = ParseDateTimeOffset(root, "CreationDate", DateTimeOffset.UtcNow),
                Migration = new UniversalProfileMigrationProvenance
                {
                    SourceFamily = source.Family.ToString(),
                    SourceIdentity = source.RelativeSourceIdentity,
                    SourceContentHash = report.SourceContentHash,
                    MigrationSchemaVersion = MigrationSchemaVersion,
                },
            };

            profile.ProfileSettings = ExtractControllerIndependentSettings(root);
            ConvertActionSets(root, profile);
            ConvertBindings(source.Family, root, profile, report);
            return profile;
        }

        private static JObject ExtractControllerIndependentSettings(JObject root)
        {
            JObject settings = new JObject();
            CopyIfPresent(root, settings, "OutputGamepadSettings");
            CopyIfPresent(root, settings, "LightbarSettings");
            CopyIfPresent(root, settings, "CycleBindings");
            CopyIfPresent(root, settings, "CalibRwc");
            CopyIfPresent(root, settings, "CalibInGameSens");
            CopyIfPresent(root, settings, "CalibCounts");
            CopyIfPresent(root, settings, "CalibMode");
            CopyIfPresent(root, settings, "CalibPreset");
            return settings;
        }

        private static void CopyIfPresent(JObject source, JObject target, string propertyName)
        {
            if (source.TryGetValue(propertyName, out JToken value))
            {
                target[propertyName] = value.DeepClone();
            }
        }

        private static void ConvertActionSets(JObject root, UniversalProfile profile)
        {
            foreach (JObject setObject in (root["ActionSets"] as JArray ?? new JArray()).OfType<JObject>())
            {
                UniversalProfileActionSet set = new UniversalProfileActionSet
                {
                    Index = setObject.Value<int?>("Index") ?? 0,
                    Name = setObject.Value<string>("Name") ?? string.Empty,
                    Description = setObject.Value<string>("Description") ?? string.Empty,
                };

                foreach (JObject layerObject in (setObject["ActionLayers"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    UniversalProfileActionLayer layer = new UniversalProfileActionLayer
                    {
                        Index = layerObject.Value<int?>("Index") ?? 0,
                        Name = layerObject.Value<string>("Name") ?? string.Empty,
                        Description = layerObject.Value<string>("Description") ?? string.Empty,
                    };

                    foreach (JObject actionObject in (layerObject["MappedActions"] as JArray ?? new JArray()).OfType<JObject>())
                    {
                        int actionId = actionObject.Value<int?>("Id") ?? -1;
                        string actionMode = actionObject.Value<string>("ActionMode") ?? string.Empty;
                        layer.Actions.Add(new JObject
                        {
                            ["id"] = actionId,
                            ["type"] = actionMode,
                            ["payload"] = actionObject.DeepClone(),
                        });
                    }

                    set.Layers.Add(layer);
                }

                profile.ActionSets.Add(set);
            }
        }

        private static void ConvertBindings(
            InputDeviceType family,
            JObject root,
            UniversalProfile profile,
            ProfileMigrationReport report)
        {
            foreach (JObject mappingGroup in (root["Mappings"] as JArray ?? new JArray()).OfType<JObject>())
            {
                int actionSet = mappingGroup.Value<int?>("ActionSet") ?? 0;
                int actionLayer = mappingGroup.Value<int?>("ActionLayer") ?? 0;
                foreach (JObject mapping in (mappingGroup["InputMappings"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    string legacyInput = mapping.Value<string>("Input") ?? string.Empty;
                    int action = mapping.Value<int?>("Action") ?? -1;
                    if (!LegacyInputMappingTable.TryMap(family, legacyInput, out LegacyInputMapping inputMapping))
                    {
                        report.AddError($"$.Mappings[{actionSet}:{actionLayer}].{legacyInput}", $"Legacy input '{legacyInput}' for {family} has no safe universal mapping.");
                        continue;
                    }

                    if (!inputMapping.Exact)
                    {
                        report.AddWarning($"$.Mappings[{actionSet}:{actionLayer}].{legacyInput}", inputMapping.Warning);
                    }

                    foreach (UniversalInputId input in ExpandLegacyInput(legacyInput, inputMapping.Input))
                    {
                        UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(input);
                        profile.Bindings.Add(new UniversalProfileBinding
                        {
                            ActionSet = actionSet,
                            ActionLayer = actionLayer,
                            Input = input,
                            ValueKind = metadata.ValueKind,
                            Action = action,
                            LegacyInput = legacyInput,
                        });
                    }
                }
            }
        }

        private static IEnumerable<UniversalInputId> ExpandLegacyInput(string legacyInput, UniversalInputId input)
        {
            if (string.Equals(legacyInput, "DPad", StringComparison.Ordinal))
            {
                return new[]
                {
                    UniversalInputId.DPadUp,
                    UniversalInputId.DPadDown,
                    UniversalInputId.DPadLeft,
                    UniversalInputId.DPadRight,
                };
            }

            return new[] { input };
        }

        private static bool IsSafeRelativeIdentity(string sourceIdentity)
        {
            if (string.IsNullOrWhiteSpace(sourceIdentity) || Path.IsPathRooted(sourceIdentity))
            {
                return false;
            }

            string normalised = sourceIdentity.Replace('\\', '/');
            return !normalised.Split('/').Any(part => part == ".." || string.IsNullOrWhiteSpace(part));
        }

        private string ManifestPath => Path.Combine(store.RootPath, ManifestFileName);

        private MigrationManifest LoadManifest()
        {
            if (!File.Exists(ManifestPath))
            {
                return new MigrationManifest();
            }

            try
            {
                return JsonConvert.DeserializeObject<MigrationManifest>(File.ReadAllText(ManifestPath)) ?? new MigrationManifest();
            }
            catch (JsonException)
            {
                return new MigrationManifest();
            }
        }

        private void SaveManifest(MigrationManifest manifest)
        {
            Directory.CreateDirectory(store.RootPath);
            string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            string tempPath = Path.Combine(store.RootPath, $".{Guid.NewGuid():N}.migration-manifest.tmp");
            bool moved = false;
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(ManifestPath))
                {
                    File.Replace(tempPath, ManifestPath, null);
                }
                else
                {
                    File.Move(tempPath, ManifestPath);
                }

                moved = true;
            }
            finally
            {
                if (!moved && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static string ComputeHash(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static DateTimeOffset ParseDateTimeOffset(JObject root, string propertyName, DateTimeOffset fallback)
        {
            string value = root.Value<string>(propertyName);
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed)
                    ? parsed
                    : fallback;
        }

        private static JObject ParseJsonObject(string json)
        {
            using (StringReader reader = new StringReader(json))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                jsonReader.DateParseHandling = DateParseHandling.None;
                return JObject.Load(jsonReader);
            }
        }
    }

    public sealed class LegacyInputMapping
    {
        public LegacyInputMapping(UniversalInputId input, bool exact = true, string warning = "")
        {
            Input = input;
            Exact = exact;
            Warning = warning;
        }

        public UniversalInputId Input { get; }
        public bool Exact { get; }
        public string Warning { get; }
    }

    public static class LegacyInputMappingTable
    {
        private static readonly Dictionary<InputDeviceType, IReadOnlyDictionary<string, LegacyInputMapping>> mappings =
            new Dictionary<InputDeviceType, IReadOnlyDictionary<string, LegacyInputMapping>>
            {
                [InputDeviceType.DS4] = Playstation(),
                [InputDeviceType.DualSense] = DualSense(),
                [InputDeviceType.SwitchPro] = Nintendo(false),
                [InputDeviceType.JoyCon] = Nintendo(true),
                [InputDeviceType.SteamController] = SteamController(),
                [InputDeviceType.SteamControllerTriton] = Triton(),
                [InputDeviceType.EightBitDoUltimate2Wireless] = EightBitDo(),
            };

        public static bool TryMap(InputDeviceType family, string legacyInput, out LegacyInputMapping mapping)
        {
            mapping = null;
            return mappings.TryGetValue(family, out IReadOnlyDictionary<string, LegacyInputMapping> familyMappings) &&
                familyMappings.TryGetValue(legacyInput ?? string.Empty, out mapping);
        }

        private static IReadOnlyDictionary<string, LegacyInputMapping> Playstation()
        {
            return BaseModern(new Dictionary<string, UniversalInputId>
            {
                ["Cross"] = UniversalInputId.FaceButtonSouth,
                ["Circle"] = UniversalInputId.FaceButtonEast,
                ["Square"] = UniversalInputId.FaceButtonWest,
                ["Triangle"] = UniversalInputId.FaceButtonNorth,
                ["Share"] = UniversalInputId.View,
                ["Options"] = UniversalInputId.Menu,
                ["PS"] = UniversalInputId.System,
                ["TouchClick"] = UniversalInputId.PrimaryTouchSurfaceClick,
                ["Touchpad"] = UniversalInputId.PrimaryTouchSurface,
                ["TouchpadLeft"] = UniversalInputId.PrimaryTouchSurface,
                ["TouchpadRight"] = UniversalInputId.PrimaryTouchSurface,
            }, "PlayStation touchpad regions migrate to the primary touch surface; Step 6 will decide editor visibility.");
        }

        private static IReadOnlyDictionary<string, LegacyInputMapping> DualSense()
        {
            Dictionary<string, UniversalInputId> values = Playstation().ToDictionary(item => item.Key, item => item.Value.Input);
            values["Create"] = UniversalInputId.View;
            values["Mute"] = UniversalInputId.Mute;
            values["FnL"] = UniversalInputId.LeftSidePrimary;
            values["FnR"] = UniversalInputId.RightSidePrimary;
            values["BLP"] = UniversalInputId.LeftRearPrimary;
            values["BRP"] = UniversalInputId.RightRearPrimary;
            return BaseModern(values, "PlayStation touchpad regions migrate to the primary touch surface; Step 6 will decide editor visibility.");
        }

        private static IReadOnlyDictionary<string, LegacyInputMapping> Nintendo(bool joyCon)
        {
            Dictionary<string, UniversalInputId> values = new Dictionary<string, UniversalInputId>
            {
                ["A"] = UniversalInputId.FaceButtonEast,
                ["B"] = UniversalInputId.FaceButtonSouth,
                ["X"] = UniversalInputId.FaceButtonNorth,
                ["Y"] = UniversalInputId.FaceButtonWest,
                ["Minus"] = UniversalInputId.View,
                ["Plus"] = UniversalInputId.Menu,
                ["Home"] = UniversalInputId.System,
                ["Capture"] = UniversalInputId.Capture,
                ["LShoulder"] = UniversalInputId.LeftShoulder,
                ["RShoulder"] = UniversalInputId.RightShoulder,
                ["ZL"] = UniversalInputId.LeftTrigger,
                ["ZR"] = UniversalInputId.RightTrigger,
                ["LSClick"] = UniversalInputId.LeftStickClick,
                ["RSClick"] = UniversalInputId.RightStickClick,
                ["LS"] = UniversalInputId.LeftStick,
                ["RS"] = UniversalInputId.RightStick,
                ["DPad"] = UniversalInputId.DPadUp,
                ["Gyro"] = UniversalInputId.Gyroscope,
            };

            if (joyCon)
            {
                values["GyroL"] = UniversalInputId.Gyroscope;
                values["GyroR"] = UniversalInputId.Gyroscope;
                values["LSideL"] = UniversalInputId.LeftSidePrimary;
                values["LSideR"] = UniversalInputId.LeftSideSecondary;
                values["RSideL"] = UniversalInputId.RightSidePrimary;
                values["RSideR"] = UniversalInputId.RightSideSecondary;
            }

            return ToMappings(values);
        }

        private static IReadOnlyDictionary<string, LegacyInputMapping> SteamController()
        {
            return ToMappings(new Dictionary<string, UniversalInputId>
            {
                ["A"] = UniversalInputId.FaceButtonSouth,
                ["B"] = UniversalInputId.FaceButtonEast,
                ["X"] = UniversalInputId.FaceButtonWest,
                ["Y"] = UniversalInputId.FaceButtonNorth,
                ["Back"] = UniversalInputId.View,
                ["Start"] = UniversalInputId.Menu,
                ["Steam"] = UniversalInputId.System,
                ["LShoulder"] = UniversalInputId.LeftShoulder,
                ["RShoulder"] = UniversalInputId.RightShoulder,
                ["LT"] = UniversalInputId.LeftTrigger,
                ["RT"] = UniversalInputId.RightTrigger,
                ["LSClick"] = UniversalInputId.LeftStickClick,
                ["Stick"] = UniversalInputId.LeftStick,
                ["LeftGrip"] = UniversalInputId.LeftRearPrimary,
                ["RightGrip"] = UniversalInputId.RightRearPrimary,
                ["LeftTouchpad"] = UniversalInputId.LeftTouchSurface,
                ["RightTouchpad"] = UniversalInputId.RightTouchSurface,
                ["LeftPadClick"] = UniversalInputId.LeftTouchSurfaceClick,
                ["RightPadClick"] = UniversalInputId.RightTouchSurfaceClick,
                ["LeftPadTouch"] = UniversalInputId.LeftTouchContact,
                ["RightPadTouch"] = UniversalInputId.RightTouchContact,
                ["Gyro"] = UniversalInputId.Gyroscope,
            });
        }

        private static IReadOnlyDictionary<string, LegacyInputMapping> Triton()
        {
            Dictionary<string, UniversalInputId> values = SteamController().ToDictionary(item => item.Key, item => item.Value.Input);
            values["Select"] = UniversalInputId.View;
            values["L1"] = UniversalInputId.LeftShoulder;
            values["R1"] = UniversalInputId.RightShoulder;
            values["L2"] = UniversalInputId.LeftTrigger;
            values["R2"] = UniversalInputId.RightTrigger;
            values["L3"] = UniversalInputId.LeftStickClick;
            values["R3"] = UniversalInputId.RightStickClick;
            values["LS"] = UniversalInputId.LeftStick;
            values["RS"] = UniversalInputId.RightStick;
            values["L4"] = UniversalInputId.LeftRearPrimary;
            values["R4"] = UniversalInputId.RightRearPrimary;
            values["L5"] = UniversalInputId.LeftRearSecondary;
            values["R5"] = UniversalInputId.RightRearSecondary;
            values["LSTouch"] = UniversalInputId.LeftStickTouch;
            values["RSTouch"] = UniversalInputId.RightStickTouch;
            values["LeftGripSense"] = UniversalInputId.LeftGripTouch;
            values["RightGripSense"] = UniversalInputId.RightGripTouch;
            values["QAM"] = UniversalInputId.QuickAccessMenu;
            values["DPad"] = UniversalInputId.DPadUp;
            return ToMappings(values);
        }

        private static IReadOnlyDictionary<string, LegacyInputMapping> EightBitDo()
        {
            return ToMappings(new Dictionary<string, UniversalInputId>
            {
                ["A"] = UniversalInputId.FaceButtonSouth,
                ["B"] = UniversalInputId.FaceButtonEast,
                ["X"] = UniversalInputId.FaceButtonWest,
                ["Y"] = UniversalInputId.FaceButtonNorth,
                ["LB"] = UniversalInputId.LeftShoulder,
                ["RB"] = UniversalInputId.RightShoulder,
                ["LT"] = UniversalInputId.LeftTrigger,
                ["RT"] = UniversalInputId.RightTrigger,
                ["LSClick"] = UniversalInputId.LeftStickClick,
                ["RSClick"] = UniversalInputId.RightStickClick,
                ["L4"] = UniversalInputId.LeftRearPrimary,
                ["R4"] = UniversalInputId.RightRearPrimary,
                ["PL"] = UniversalInputId.LeftRearSecondary,
                ["PR"] = UniversalInputId.RightRearSecondary,
                ["Minus"] = UniversalInputId.View,
                ["Plus"] = UniversalInputId.Menu,
                ["Guide"] = UniversalInputId.System,
                ["LS"] = UniversalInputId.LeftStick,
                ["RS"] = UniversalInputId.RightStick,
                ["DPad"] = UniversalInputId.DPadUp,
                ["Gyro"] = UniversalInputId.Gyroscope,
            });
        }

        private static IReadOnlyDictionary<string, LegacyInputMapping> BaseModern(
            Dictionary<string, UniversalInputId> specific,
            string touchRegionWarning)
        {
            Dictionary<string, UniversalInputId> values = new Dictionary<string, UniversalInputId>
            {
                ["L1"] = UniversalInputId.LeftShoulder,
                ["R1"] = UniversalInputId.RightShoulder,
                ["L2"] = UniversalInputId.LeftTrigger,
                ["R2"] = UniversalInputId.RightTrigger,
                ["L3"] = UniversalInputId.LeftStickClick,
                ["R3"] = UniversalInputId.RightStickClick,
                ["LS"] = UniversalInputId.LeftStick,
                ["RS"] = UniversalInputId.RightStick,
                ["DPad"] = UniversalInputId.DPadUp,
                ["Gyro"] = UniversalInputId.Gyroscope,
            };

            foreach (KeyValuePair<string, UniversalInputId> pair in specific)
            {
                values[pair.Key] = pair.Value;
            }

            Dictionary<string, LegacyInputMapping> result = ToMappings(values);
            result["TouchpadLeft"] = new LegacyInputMapping(UniversalInputId.PrimaryTouchSurface, false, touchRegionWarning);
            result["TouchpadRight"] = new LegacyInputMapping(UniversalInputId.PrimaryTouchSurface, false, touchRegionWarning);
            return result;
        }

        private static Dictionary<string, LegacyInputMapping> ToMappings(Dictionary<string, UniversalInputId> values)
        {
            return values.ToDictionary(
                item => item.Key,
                item => new LegacyInputMapping(item.Value),
                StringComparer.Ordinal);
        }
    }

    public sealed class MigrationManifest
    {
        public int MigrationSchemaVersion { get; set; } = LegacyProfileMigrator.MigrationSchemaVersion;
        public List<MigrationManifestEntry> Entries { get; set; } = new List<MigrationManifestEntry>();

        public MigrationManifestEntry Find(InputDeviceType family, string sourceIdentity)
        {
            return Entries.FirstOrDefault(item =>
                string.Equals(item.SourceFamily, family.ToString(), StringComparison.Ordinal) &&
                string.Equals(item.SourceIdentity, sourceIdentity, StringComparison.Ordinal));
        }
    }

    public sealed class MigrationManifestEntry
    {
        public string SourceFamily { get; set; } = string.Empty;
        public string SourceIdentity { get; set; } = string.Empty;
        public string SourceContentHash { get; set; } = string.Empty;
        public Guid UniversalProfileId { get; set; }
        public int MigrationSchemaVersion { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
