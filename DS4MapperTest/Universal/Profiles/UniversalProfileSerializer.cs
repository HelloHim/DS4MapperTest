using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DS4MapperTest.Universal.Profiles
{
    public enum UniversalProfileLoadStatus
    {
        Loaded,
        MissingVersion,
        UnsupportedFutureVersion,
        UnsupportedOldVersion,
        Malformed,
        ValidationFailed,
    }

    public sealed class UniversalProfileLoadException : Exception
    {
        public UniversalProfileLoadException(UniversalProfileLoadStatus status, string message)
            : base(message)
        {
            Status = status;
        }

        public UniversalProfileLoadStatus Status { get; }
    }

    public static class UniversalProfileSerializer
    {
        private static readonly HashSet<string> KnownRootProperties =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "schemaVersion",
                "profileId",
                "displayName",
                "description",
                "createdUtc",
                "profileSettings",
                "migration",
                "actionSets",
                "bindings",
            };

        public static string Serialize(UniversalProfile profile)
        {
            UniversalProfileValidationResult validation = UniversalProfileValidator.Validate(profile);
            if (!validation.IsValid)
            {
                throw new UniversalProfileValidationException(validation);
            }

            JObject root = ToJObject(profile);
            StringBuilder builder = new StringBuilder();
            using (StringWriter writer = new StringWriter(builder, CultureInfo.InvariantCulture))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                jsonWriter.Formatting = Formatting.Indented;
                jsonWriter.Indentation = 2;
                root.WriteTo(jsonWriter);
            }

            return builder.ToString();
        }

        public static UniversalProfile Deserialize(string json)
        {
            JObject root;
            try
            {
                root = ParseJsonObject(json);
            }
            catch (JsonException ex)
            {
                throw new UniversalProfileLoadException(UniversalProfileLoadStatus.Malformed, ex.Message);
            }

            if (root["schemaVersion"] == null)
            {
                throw new UniversalProfileLoadException(UniversalProfileLoadStatus.MissingVersion, "Universal profile schemaVersion is required.");
            }

            int schemaVersion = root.Value<int?>("schemaVersion") ??
                throw new UniversalProfileLoadException(UniversalProfileLoadStatus.Malformed, "schemaVersion must be an integer.");
            if (schemaVersion > UniversalProfile.CurrentSchemaVersion)
            {
                throw new UniversalProfileLoadException(UniversalProfileLoadStatus.UnsupportedFutureVersion, $"Universal profile schema version {schemaVersion} is newer than this application supports.");
            }

            if (schemaVersion < UniversalProfile.CurrentSchemaVersion)
            {
                throw new UniversalProfileLoadException(UniversalProfileLoadStatus.UnsupportedOldVersion, $"Universal profile schema version {schemaVersion} is not supported by this loader.");
            }

            UniversalProfile profile = new UniversalProfile
            {
                SchemaVersion = schemaVersion,
                ProfileId = ParseGuid(root, "profileId"),
                DisplayName = root.Value<string>("displayName") ?? string.Empty,
                Description = root.Value<string>("description") ?? string.Empty,
                CreatedUtc = ParseDateTimeOffset(root, "createdUtc", DateTimeOffset.MinValue),
                ProfileSettings = root["profileSettings"] as JObject ?? new JObject(),
                ExtensionData = ExtractExtensionData(root),
            };

            JObject migration = root["migration"] as JObject;
            if (migration != null)
            {
                profile.Migration = new UniversalProfileMigrationProvenance
                {
                    SourceFamily = migration.Value<string>("sourceFamily") ?? string.Empty,
                    SourceIdentity = migration.Value<string>("sourceIdentity") ?? string.Empty,
                    SourceContentHash = migration.Value<string>("sourceContentHash") ?? string.Empty,
                    MigrationSchemaVersion = migration.Value<int?>("migrationSchemaVersion") ?? LegacyProfileMigrator.MigrationSchemaVersion,
                };
            }

            foreach (JObject setObject in (root["actionSets"] as JArray ?? new JArray()).OfType<JObject>())
            {
                UniversalProfileActionSet set = new UniversalProfileActionSet
                {
                    Index = setObject.Value<int?>("index") ?? 0,
                    Name = setObject.Value<string>("name") ?? string.Empty,
                    Description = setObject.Value<string>("description") ?? string.Empty,
                };

                foreach (JObject layerObject in (setObject["layers"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    UniversalProfileActionLayer layer = new UniversalProfileActionLayer
                    {
                        Index = layerObject.Value<int?>("index") ?? 0,
                        Name = layerObject.Value<string>("name") ?? string.Empty,
                        Description = layerObject.Value<string>("description") ?? string.Empty,
                    };

                    foreach (JObject actionObject in (layerObject["actions"] as JArray ?? new JArray()).OfType<JObject>())
                    {
                        layer.Actions.Add((JObject)actionObject.DeepClone());
                    }

                    set.Layers.Add(layer);
                }

                profile.ActionSets.Add(set);
            }

            foreach (JObject bindingObject in (root["bindings"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string token = bindingObject.Value<string>("input") ?? string.Empty;
                if (!UniversalInputToken.TryParse(token, out UniversalInputId input))
                {
                    throw new UniversalProfileLoadException(UniversalProfileLoadStatus.ValidationFailed, $"Unknown universal input token '{token}'.");
                }

                if (!Enum.TryParse(bindingObject.Value<string>("valueKind") ?? string.Empty, out UniversalInputValueKind valueKind))
                {
                    throw new UniversalProfileLoadException(UniversalProfileLoadStatus.ValidationFailed, $"Invalid value kind for binding '{token}'.");
                }

                profile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = bindingObject.Value<int?>("actionSet") ?? 0,
                    ActionLayer = bindingObject.Value<int?>("actionLayer") ?? 0,
                    Input = input,
                    ValueKind = valueKind,
                    Action = bindingObject.Value<int?>("action") ?? -1,
                    LegacyInput = bindingObject.Value<string>("legacyInput") ?? string.Empty,
                });
            }

            UniversalProfileValidationResult validation = UniversalProfileValidator.Validate(profile);
            if (!validation.IsValid)
            {
                throw new UniversalProfileLoadException(UniversalProfileLoadStatus.ValidationFailed, string.Join(Environment.NewLine, validation.Issues.Select(item => $"{item.Location}: {item.Message}")));
            }

            return profile;
        }

        internal static JObject ToJObject(UniversalProfile profile)
        {
            JObject root = profile.ExtensionData != null ? (JObject)profile.ExtensionData.DeepClone() : new JObject();
            root["schemaVersion"] = profile.SchemaVersion;
            root["profileId"] = profile.ProfileId.ToString("D");
            root["displayName"] = profile.DisplayName;
            if (!string.IsNullOrWhiteSpace(profile.Description))
            {
                root["description"] = profile.Description;
            }
            else
            {
                root.Remove("description");
            }

            root["createdUtc"] = profile.CreatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            root["profileSettings"] = profile.ProfileSettings != null ? profile.ProfileSettings.DeepClone() : new JObject();
            if (profile.Migration != null)
            {
                root["migration"] = new JObject
                {
                    ["sourceFamily"] = profile.Migration.SourceFamily,
                    ["sourceIdentity"] = profile.Migration.SourceIdentity,
                    ["sourceContentHash"] = profile.Migration.SourceContentHash,
                    ["migrationSchemaVersion"] = profile.Migration.MigrationSchemaVersion,
                };
            }
            else
            {
                root.Remove("migration");
            }

            root["actionSets"] = new JArray(profile.ActionSets
                .OrderBy(item => item.Index)
                .Select(ToJObject));
            root["bindings"] = new JArray(profile.Bindings
                .OrderBy(item => item.ActionSet)
                .ThenBy(item => item.ActionLayer)
                .ThenBy(item => UniversalInputToken.Format(item.Input), StringComparer.Ordinal)
                .Select(ToJObject));
            return root;
        }

        private static JObject ToJObject(UniversalProfileActionSet set)
        {
            JObject result = new JObject
            {
                ["index"] = set.Index,
                ["name"] = set.Name,
            };
            if (!string.IsNullOrWhiteSpace(set.Description))
            {
                result["description"] = set.Description;
            }

            result["layers"] = new JArray(set.Layers.OrderBy(item => item.Index).Select(ToJObject));
            return result;
        }

        private static JObject ToJObject(UniversalProfileActionLayer layer)
        {
            JObject result = new JObject
            {
                ["index"] = layer.Index,
                ["name"] = layer.Name,
            };
            if (!string.IsNullOrWhiteSpace(layer.Description))
            {
                result["description"] = layer.Description;
            }

            result["actions"] = new JArray(layer.Actions
                .OrderBy(GetActionId)
                .Select(item => item.DeepClone()));
            return result;
        }

        private static JObject ToJObject(UniversalProfileBinding binding)
        {
            JObject result = new JObject
            {
                ["actionSet"] = binding.ActionSet,
                ["actionLayer"] = binding.ActionLayer,
                ["input"] = UniversalInputToken.Format(binding.Input),
                ["valueKind"] = binding.ValueKind.ToString(),
                ["action"] = binding.Action,
            };

            if (!string.IsNullOrWhiteSpace(binding.LegacyInput))
            {
                result["legacyInput"] = binding.LegacyInput;
            }

            return result;
        }

        private static int GetActionId(JObject action)
        {
            return (action["id"] ?? action["Id"])?.Value<int?>() ?? int.MaxValue;
        }

        private static Guid ParseGuid(JObject root, string propertyName)
        {
            string value = root.Value<string>(propertyName);
            return Guid.TryParse(value, out Guid guid) ? guid : Guid.Empty;
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

        private static JObject ExtractExtensionData(JObject root)
        {
            JObject extensionData = new JObject();
            foreach (JProperty property in root.Properties())
            {
                if (!KnownRootProperties.Contains(property.Name))
                {
                    extensionData[property.Name] = property.Value.DeepClone();
                }
            }

            return extensionData;
        }
    }
}
