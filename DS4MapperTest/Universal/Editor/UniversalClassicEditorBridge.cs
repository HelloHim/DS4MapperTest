using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DS4MapperTest.Universal.Editor
{
    public sealed class UniversalClassicInputDevice : InputDeviceBase
    {
        public UniversalClassicInputDevice(UniversalMapperSession session, int index)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            this.index = index;
            deviceType = session.Mapper.DeviceType;
            devTypeStr = session.Controller.DisplayInfo.DisplayName;
            serial = session.Controller.Identity.DeviceIdentity.BestEffortPersistentKey;
            if (session.Controller.BatteryPercent.HasValue)
            {
                battery = (uint)session.Controller.BatteryPercent.Value;
            }
            if (string.IsNullOrWhiteSpace(serial))
            {
                serial = session.BackendSessionId;
            }
        }

        public UniversalMapperSession Session { get; }
        public override void SetOperational() { }
        public override void Detach() { }
    }

    public sealed class UniversalClassicProfileEntry : ProfileEntity
    {
        public UniversalClassicProfileEntry(string path, UniversalProfile profile)
            : base(path, profile?.DisplayName ?? string.Empty, InputDeviceType.None, ProfileList.DEFAULT_PROFILE_FOLDER)
        {
            ProfileId = profile?.ProfileId ?? Guid.Empty;
        }

        public Guid ProfileId { get; }
    }

    public sealed class UniversalClassicProfileList
    {
        private readonly UniversalProfileStore store;
        private readonly ObservableCollection<ProfileEntity> profiles =
            new ObservableCollection<ProfileEntity>();
        private readonly ObservableCollection<string> folders =
            new ObservableCollection<string> { ProfileList.DEFAULT_PROFILE_FOLDER };

        public UniversalClassicProfileList(UniversalProfileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Refresh();
        }

        public ObservableCollection<ProfileEntity> Profiles => profiles;
        public ObservableCollection<string> Folders => folders;

        public void Refresh()
        {
            profiles.Clear();
            foreach (UniversalProfileStoreEntry entry in store.EnumerateProfiles()
                .Where(item => item.Loaded)
                .OrderBy(item => item.Profile.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                profiles.Add(new UniversalClassicProfileEntry(entry.Path, entry.Profile));
            }
        }
    }

    public static class UniversalClassicProfileProjector
    {
        private static readonly HashSet<string> RootUniversalProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Name", "Description", "CreationDate", "ControllerType",
                "ProfileSpecVersion", "ActionSets", "Mappings",
            };

        public static UniversalProfile BuildUpdatedProfile(
            UniversalMapper mapper,
            Profile editedProfile,
            UniversalProfile latestProfile)
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            if (editedProfile == null) throw new ArgumentNullException(nameof(editedProfile));
            if (latestProfile == null) throw new ArgumentNullException(nameof(latestProfile));

            ProfileSerializer serializer = new ProfileSerializer(editedProfile);
            JObject root = JObject.Parse(JsonConvert.SerializeObject(serializer, Formatting.None));
            UniversalProfile updated = latestProfile.Clone();

            updated.DisplayName = root.Value<string>("Name") ?? updated.DisplayName;
            updated.Description = root.Value<string>("Description") ?? updated.Description;
            updated.ProfileSettings = new JObject(root.Properties()
                .Where(property => !RootUniversalProperties.Contains(property.Name))
                .Select(property => new JProperty(property.Name, property.Value.DeepClone())));

            updated.ActionSets.Clear();
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

                updated.ActionSets.Add(set);
            }

            HashSet<UniversalInputId> visibleInputs = new HashSet<UniversalInputId>(
                mapper.CompiledProfile.ActiveBindingIds.Keys);
            updated.Bindings.RemoveAll(item => visibleInputs.Contains(item.Input));

            foreach (JObject mappingGroup in (root["Mappings"] as JArray ?? new JArray()).OfType<JObject>())
            {
                int actionSet = mappingGroup.Value<int?>("ActionSet") ?? 0;
                int actionLayer = mappingGroup.Value<int?>("ActionLayer") ?? 0;
                foreach (JObject mapping in (mappingGroup["InputMappings"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    string legacyInput = mapping.Value<string>("Input") ?? string.Empty;
                    int actionId = mapping.Value<int?>("Action") ?? -1;
                    if (!UniversalLegacyBindingMap.TryGetUniversalInput(legacyInput, out UniversalInputId input))
                    {
                        continue;
                    }

                    UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(input);
                    updated.Bindings.Add(new UniversalProfileBinding
                    {
                        ActionSet = actionSet,
                        ActionLayer = actionLayer,
                        Input = input,
                        ValueKind = metadata.ValueKind,
                        Action = actionId,
                        LegacyInput = legacyInput,
                    });
                }
            }

            return updated;
        }
    }

    public static class ControllerMiscLabelProvider
    {
        public static string GetLabel(UniversalInputId inputId, ControllerCapabilities capabilities)
        {
            if (!TryGetMiscSlot(inputId, out int slot))
            {
                return ControllerLabelProvider.GetLabel(inputId, capabilities);
            }

            string family = capabilities?.DisplayInfo?.ControllerFamily ?? capabilities?.DisplayInfo?.GlyphFamily ?? string.Empty;
            string name = capabilities?.DisplayInfo?.DisplayName ?? string.Empty;
            if (TryLookup(family, name, slot, out string label))
            {
                return $"Misc {slot} ({label})";
            }

            return $"Misc {slot}";
        }

        private static bool TryGetMiscSlot(UniversalInputId inputId, out int slot)
        {
            if (inputId >= UniversalInputId.MiscButton1 && inputId <= UniversalInputId.MiscButton16)
            {
                slot = (inputId - UniversalInputId.MiscButton1) + 1;
                return true;
            }

            slot = 0;
            return false;
        }

        private static bool TryLookup(string family, string name, int slot, out string label)
        {
            string text = $"{family} {name}".ToLowerInvariant();
            label = null;
            if (slot == 1 && text.Contains("xbox series")) label = "Share";
            else if (slot == 1 && text.Contains("dualsense")) label = "Microphone";
            else if (slot == 1 && text.Contains("switch pro") && !text.Contains("pro 2")) label = "Capture";
            else if (slot == 1 && text.Contains("luna")) label = "Microphone";
            else if (slot == 1 && text.Contains("stadia")) label = "Capture";
            else if (slot == 3 && text.Contains("gamecube")) label = "Left Trigger Click";
            else if (slot == 4 && text.Contains("gamecube")) label = "Right Trigger Click";
            else if (text.Contains("triton") || text.Contains("steam controller 2026") ||
                string.Equals(family, "steam", StringComparison.OrdinalIgnoreCase))
            {
                label = slot switch
                {
                    1 => "QAM",
                    2 => "Right Trackpad Click",
                    3 => "Left Stick Touch",
                    4 => "Right Stick Touch",
                    5 => "Left Grip Sense",
                    6 => "Right Grip Sense",
                    _ => null,
                };
            }

            return !string.IsNullOrWhiteSpace(label);
        }
    }
}
