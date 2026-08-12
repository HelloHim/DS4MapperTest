using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest.Universal.Editor
{
    // Hosts action-content editing (what a bound action actually does, as
    // opposed to which input it is bound to) against a universal profile
    // without touching a physical controller or the live mapping runtime.
    //
    // The legacy action object graph (ButtonMapAction, StickMapAction, etc.)
    // is only ever constructed by round-tripping through the legacy JSON
    // serializer (ActionSetSerializer and friends), which relies on ambient
    // static state during deserialization - see UniversalProfileRuntimeCompiler
    // and Mapper.LoadProfileFromJson, which UniversalMapper itself already
    // uses for exactly this reason. This session reuses that same machinery
    // offline: it compiles the whole profile (cross-layer references such as
    // "switch to layer N" need the full action-set context to resolve) into
    // an UniversalMapper that is never registered with BackendManager or fed
    // live snapshots, hosts the existing action-content editing views against
    // that offline Mapper.ActionProfile, and on Save serializes only the
    // edited layer's actions back into the profile via the normal Step 4
    // store. Bindings, action-set/layer structure and profile identity are
    // never touched here - those remain exclusively owned by the universal
    // profile editor.
    public sealed class UniversalActionContentEditorSession : IDisposable
    {
        private readonly UniversalMapper mapper;
        private readonly UniversalController offlineController;
        private readonly Guid sourceProfileId;

        private UniversalActionContentEditorSession(
            UniversalMapper mapper,
            UniversalController offlineController,
            Guid sourceProfileId,
            int actionSetIndex,
            int actionLayerIndex)
        {
            this.mapper = mapper;
            this.offlineController = offlineController;
            this.sourceProfileId = sourceProfileId;
            ActionSetIndex = actionSetIndex;
            ActionLayerIndex = actionLayerIndex;
        }

        // The offline Mapper hosts the legacy action-content editing views
        // (the *ActionPropControls/*ActionPropViewModels families), exactly
        // as they already bind against Mapper.ActionProfile.
        public Mapper Mapper => mapper;
        public int ActionSetIndex { get; }
        public int ActionLayerIndex { get; }

        public static UniversalActionContentEditorSession Open(
            UniversalProfile profile,
            int actionSetIndex,
            int actionLayerIndex)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            if (!profile.ActionSets.Any(set => set.Index == actionSetIndex &&
                set.Layers.Any(layer => layer.Index == actionLayerIndex)))
            {
                throw new ArgumentException(
                    $"Action set {actionSetIndex} / layer {actionLayerIndex} does not exist in profile {profile.ProfileId}.");
            }

            ControllerCapabilities allInputsSupported = new ControllerCapabilities(
                ControllerDisplayInfo.Unknown(),
                UniversalInputCatalog.All.Select(metadata =>
                    new ControllerInputDescriptor(metadata.Id, metadata.ValueKind, isSupported: true)));

            UniversalDeviceIdentity deviceIdentity = new UniversalDeviceIdentity(
                UniversalControllerBackendIds.OfflineActionContentEditor,
                Guid.NewGuid().ToString("N"));
            UniversalControllerIdentity identity = new UniversalControllerIdentity(
                Guid.NewGuid(),
                UniversalControllerBackendIds.OfflineActionContentEditor,
                deviceIdentity.BackendSessionId,
                deviceIdentity,
                DateTimeOffset.UtcNow);

            UniversalController offlineController = new UniversalController(
                identity, allInputsSupported, UniversalControllerStateSnapshot.Disconnected());

            UniversalMapper mapper = new UniversalMapper(offlineController, profile.Clone());

            return new UniversalActionContentEditorSession(
                mapper, offlineController, profile.ProfileId, actionSetIndex, actionLayerIndex);
        }

        // Serializes the offline action object graph back to the same
        // legacy JSON shape ProfileEditorTestViewModel.TestSave already
        // produces, then replaces only the target layer's Actions on a
        // clone of latestProfile - every other action set, layer and every
        // binding is left untouched. The caller is responsible for
        // persisting the returned profile through the normal Step 4 store
        // (UniversalProfileStore / UniversalProfileEditorSaveCoordinator).
        public UniversalProfile BuildUpdatedProfile(UniversalProfile latestProfile)
        {
            if (latestProfile == null) throw new ArgumentNullException(nameof(latestProfile));
            if (latestProfile.ProfileId != sourceProfileId)
            {
                throw new ArgumentException(
                    "The supplied profile is not the one this editing session was opened against.",
                    nameof(latestProfile));
            }

            ProfileSerializer profileSerializer = new ProfileSerializer(mapper.ActionProfile);
            string json = JsonConvert.SerializeObject(profileSerializer, Formatting.None);
            JObject root = JObject.Parse(json);

            JObject editedSet = (root["ActionSets"] as JArray)?
                .OfType<JObject>()
                .FirstOrDefault(set => set.Value<int?>("Index") == ActionSetIndex);
            JObject editedLayer = (editedSet?["ActionLayers"] as JArray)?
                .OfType<JObject>()
                .FirstOrDefault(layer => layer.Value<int?>("Index") == ActionLayerIndex);
            JArray editedActions = editedLayer?["MappedActions"] as JArray;

            if (editedActions == null)
            {
                throw new InvalidOperationException(
                    $"Could not locate action set {ActionSetIndex} / layer {ActionLayerIndex} in the re-serialized profile.");
            }

            UniversalProfile updated = latestProfile.Clone();
            UniversalProfileActionLayer targetLayer = updated.ActionSets
                .First(set => set.Index == ActionSetIndex).Layers
                .First(layer => layer.Index == ActionLayerIndex);

            targetLayer.Actions.Clear();
            targetLayer.Actions.AddRange(editedActions.OfType<JObject>().Select(item => (JObject)item.DeepClone()));

            return updated;
        }

        public void Dispose()
        {
            mapper?.Stop(finalSync: false);
            offlineController?.Dispose();
        }
    }
}
