using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;

namespace DS4MapperTest.Universal.Editor
{
    public sealed class UniversalClassicInputDevice : InputDeviceBase
    {
        public UniversalClassicInputDevice(UniversalMapperSession session, int index)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            this.index = index;
            deviceType = session.Mapper.DeviceType;
            deviceOptions = UniversalControllerDeviceOptionsStore.LoadOptions(
                session.Controller,
                deviceType);
            devTypeStr = session.Controller.DisplayInfo.DisplayName;
            serial = UniversalControllerDeviceOptionsStore.BuildControllerKey(session.Controller);
            if (session.Controller.BatteryPercent.HasValue)
            {
                battery = (uint)session.Controller.BatteryPercent.Value;
            }
            if (string.IsNullOrWhiteSpace(serial))
            {
                serial = session.BackendSessionId;
            }
            synced = session.Controller.ConnectionState == UniversalControllerConnectionState.Connected;
        }

        public UniversalMapperSession Session { get; }
        public override void SetOperational() { }
        public override void Detach() { }
    }

    public sealed class UniversalClassicProfileEntry : ProfileEntity
    {
        public UniversalClassicProfileEntry(string path, UniversalProfileSummary summary, string folderName)
            : base(path, summary?.DisplayName ?? string.Empty, InputDeviceType.None, folderName)
        {
            ProfileId = summary?.ProfileId ?? Guid.Empty;
        }

        public Guid ProfileId { get; }
    }

    public sealed class UniversalClassicProfileList
    {
        private readonly UniversalProfileStore store;
        // Both collections are bound straight into the window (the folder list
        // is a ComboBox ItemsSource on the new-profile panel) and Refresh runs
        // from the universal mapping thread whenever sessions change, so WPF
        // needs to be told how to serialise access from a non-UI thread.
        // Without this a controller connecting while that panel is open throws
        // out of the mapping loop instead of updating the list.
        private readonly object collectionLock = new object();
        private readonly ObservableCollection<ProfileEntity> profiles =
            new ObservableCollection<ProfileEntity>();
        private readonly ObservableCollection<string> folders =
            new ObservableCollection<string> { ProfileList.DEFAULT_PROFILE_FOLDER };
        // Explicitly supplied by the UI (see Refresh) rather than sniffed from
        // Application.Current: a test host can have a live Application with no
        // message loop actually pumping its Dispatcher, and Invoke-ing onto that
        // one never returns. Null here (the default, and always the case in
        // tests) means Refresh applies the update on the calling thread.
        private readonly Dispatcher dispatcher;

        public UniversalClassicProfileList(UniversalProfileStore store, Dispatcher dispatcher = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.dispatcher = dispatcher;
            BindingOperations.EnableCollectionSynchronization(profiles, collectionLock);
            BindingOperations.EnableCollectionSynchronization(folders, collectionLock);
            Refresh();
        }

        public ObservableCollection<ProfileEntity> Profiles => profiles;
        public ObservableCollection<string> Folders => folders;

        public void Refresh()
        {
            // Read the store before touching the bound collections so the UI
            // never sees an empty list while the disk scan is in progress.
            List<string> folderNames = store.EnumerateFolders().ToList();
            List<UniversalProfileSummary> entries = store.EnumerateProfileSummaries()
                .Where(item => item.Loaded)
                .OrderBy(item => store.GetFolderName(item.Path), new UniversalFolderNameComparer())
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // EnableCollectionSynchronization above only protects an ItemsControl's own
            // shadow copy of these collections. A CollectionView - created for any bound
            // control that groups, sorts or filters, or via an explicit ICollectionView -
            // raises its CollectionChanged straight off whichever thread mutated the
            // source and requires that be the dispatcher thread regardless. Refresh runs
            // off the universal mapping thread on every controller connect/disconnect, so
            // without this a hotplug while such a view was bound threw out of the mapping
            // loop (silently skipping that refresh) and the same throw during shutdown
            // crashed the app outright instead of exiting cleanly.
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ApplyRefresh(folderNames, entries));
                return;
            }

            ApplyRefresh(folderNames, entries);
        }

        private void ApplyRefresh(List<string> folderNames, List<UniversalProfileSummary> entries)
        {
            lock (collectionLock)
            {
                // ProfileEntity exists to be held on to: the editor keeps the
                // entity for the profile it is working on, and ProfileEntity
                // has UpdatePath precisely so a rename or move can be applied
                // without replacing it. Handing out fresh instances on every
                // refresh left the editor holding an orphan whose path stopped
                // tracking the file, so a later save wrote to the old location.
                Dictionary<Guid, UniversalClassicProfileEntry> reusable = profiles
                    .OfType<UniversalClassicProfileEntry>()
                    .Where(item => item.ProfileId != Guid.Empty)
                    .GroupBy(item => item.ProfileId)
                    .ToDictionary(group => group.Key, group => group.First());

                profiles.Clear();
                folders.Clear();

                foreach (string folder in folderNames)
                {
                    InsertFolderName(folder);
                }

                foreach (UniversalProfileSummary entry in entries)
                {
                    string folderName = store.GetFolderName(entry.Path);
                    if (reusable.TryGetValue(entry.ProfileId, out UniversalClassicProfileEntry existing))
                    {
                        existing.UpdatePath(entry.Path);
                        existing.Name = entry.DisplayName;
                        existing.FolderName = folderName;
                        profiles.Add(existing);
                    }
                    else
                    {
                        profiles.Add(new UniversalClassicProfileEntry(entry.Path, entry, folderName));
                    }
                }
            }
        }

        public bool FolderExists(string folderName)
        {
            lock (collectionLock)
            {
                return folders.Any(item => string.Equals(item, folderName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool CreateFolder(string folderName)
        {
            if (!store.CreateFolder(folderName)) return false;

            lock (collectionLock)
            {
                InsertFolderName(folderName);
            }

            return true;
        }

        public bool RenameFolder(string oldFolderName, string newFolderName)
        {
            if (!store.RenameFolder(oldFolderName, newFolderName)) return false;

            lock (collectionLock)
            {
                int folderIndex = folders.IndexOf(oldFolderName);
                if (folderIndex >= 0)
                {
                    folders.RemoveAt(folderIndex);
                }

                foreach (ProfileEntity profile in profiles.Where(profile =>
                    string.Equals(profile.FolderName, oldFolderName, StringComparison.OrdinalIgnoreCase)))
                {
                    profile.UpdatePath(System.IO.Path.Combine(store.GetFolderPath(newFolderName), System.IO.Path.GetFileName(profile.ProfilePath)));
                    profile.FolderName = newFolderName;
                }

                InsertFolderName(newFolderName);
                SortProfiles();
            }

            return true;
        }

        public bool DeleteFolder(string folderName)
        {
            if (!store.DeleteFolder(folderName)) return false;

            lock (collectionLock)
            {
                folders.Remove(folderName);
            }

            return true;
        }

        public bool MoveProfile(ProfileEntity profile, string folderName)
        {
            if (profile == null) return false;
            if (!store.MoveProfile(profile.ProfilePath, folderName, out string newProfilePath)) return false;

            lock (collectionLock)
            {
                InsertFolderName(folderName);
                profile.UpdatePath(newProfilePath);
                profile.FolderName = folderName;
                SortProfiles();
            }

            return true;
        }

        // Callers hold collectionLock.
        private void InsertFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName) ||
                folders.Any(item => string.Equals(item, folderName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            int insertIndex = folders
                .TakeWhile(item => new UniversalFolderNameComparer().Compare(item, folderName) <= 0)
                .Count();
            folders.Insert(insertIndex, folderName);
        }

        // Callers hold collectionLock.
        private void SortProfiles()
        {
            List<ProfileEntity> sorted = profiles
                .OrderBy(item => item.FolderName, new UniversalFolderNameComparer())
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            profiles.Clear();
            foreach (ProfileEntity profile in sorted)
            {
                profiles.Add(profile);
            }
        }

        private sealed class UniversalFolderNameComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase)) return 0;

                int leftRank = GetFolderSortRank(x);
                int rightRank = GetFolderSortRank(y);
                if (leftRank != rightRank)
                {
                    return leftRank.CompareTo(rightRank);
                }

                return StringComparer.CurrentCultureIgnoreCase.Compare(x, y);
            }

            private static int GetFolderSortRank(string folderName)
            {
                if (string.Equals(folderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase)) return 0;
                if (string.Equals(folderName, ProfileList.VALORANT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase)) return 1;
                return 2;
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

            // Keyed by (ActionSet.Index, ActionLayer.Index): only populated for a layer
            // where a collision actually had to be renumbered below, so it stays empty
            // (and every Mappings lookup below a no-op) on the overwhelming common path.
            Dictionary<(int SetIndex, int LayerIndex), Dictionary<int, int>> actionIdRemaps =
                new Dictionary<(int, int), Dictionary<int, int>>();

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

                    // The classic editor hands out action ids via ActionLayer.FindNextAvailableId,
                    // which scans that layer's own in-memory action list at the moment a binding
                    // is switched. Two switches made in the same editing session can still end up
                    // requesting the same id, and the store's validator rejects the whole save the
                    // instant that happens - "Duplicate action id" - discarding every other edit in
                    // the session along with it. Renumber a collision here instead of failing the
                    // save; nothing outside this layer's own Mappings addresses an action by number,
                    // and the remap below keeps every mapping pointed at the action it actually
                    // switched to.
                    HashSet<int> usedActionIds = new HashSet<int>();
                    Dictionary<int, int> layerRemap = null;
                    foreach (JObject actionObject in (layerObject["MappedActions"] as JArray ?? new JArray()).OfType<JObject>())
                    {
                        int actionId = actionObject.Value<int?>("Id") ?? -1;
                        int assignedId = actionId;
                        while (!usedActionIds.Add(assignedId))
                        {
                            assignedId++;
                        }

                        if (assignedId != actionId)
                        {
                            (layerRemap ??= new Dictionary<int, int>())[actionId] = assignedId;
                        }

                        string actionMode = actionObject.Value<string>("ActionMode") ?? string.Empty;
                        layer.Actions.Add(new JObject
                        {
                            ["id"] = assignedId,
                            ["type"] = actionMode,
                            ["payload"] = actionObject.DeepClone(),
                        });
                    }

                    if (layerRemap != null)
                    {
                        actionIdRemaps[(set.Index, layer.Index)] = layerRemap;
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
                actionIdRemaps.TryGetValue((actionSet, actionLayer), out Dictionary<int, int> layerRemap);
                foreach (JObject mapping in (mappingGroup["InputMappings"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    string legacyInput = mapping.Value<string>("Input") ?? string.Empty;
                    int actionId = mapping.Value<int?>("Action") ?? -1;
                    if (layerRemap != null && layerRemap.TryGetValue(actionId, out int remappedActionId))
                    {
                        actionId = remappedActionId;
                    }

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
