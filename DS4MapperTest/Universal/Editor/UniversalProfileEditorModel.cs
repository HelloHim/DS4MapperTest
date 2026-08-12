using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest.Universal.Editor
{
    // Projects a universal profile plus an optional selected controller's
    // capabilities into an editable, labelled, glyph-aware view.
    //
    // The model always holds the COMPLETE profile in memory (every action
    // set, layer, action and binding, including ones the selected controller
    // cannot use). Visibility, labels and glyphs are computed on demand from
    // that complete data; they never filter or mutate it. This is what
    // guarantees edits never lose bindings the current controller happens
    // not to support, and that switching controllers cannot alter stored
    // data - there is no "rebuild from the visible view" step anywhere.
    public sealed class UniversalProfileEditorModel
    {
        private readonly UniversalProfile workingProfile;
        private ControllerCapabilities capabilities;

        public UniversalProfileEditorModel(UniversalProfile profile, ControllerCapabilities capabilities = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            workingProfile = profile.Clone();
            this.capabilities = capabilities;
            SelectFirstAvailableSetAndLayer();
        }

        public Guid ProfileId => workingProfile.ProfileId;

        public string DisplayName
        {
            get => workingProfile.DisplayName;
            set => workingProfile.DisplayName = value ?? string.Empty;
        }

        public string Description
        {
            get => workingProfile.Description;
            set => workingProfile.Description = value ?? string.Empty;
        }

        public ControllerCapabilities Capabilities => capabilities;

        public int SelectedActionSetIndex { get; private set; }
        public int SelectedActionLayerIndex { get; private set; }

        public IReadOnlyList<(int Index, string Name)> ActionSets =>
            workingProfile.ActionSets
                .OrderBy(item => item.Index)
                .Select(item => (item.Index, item.Name))
                .ToArray();

        public IReadOnlyList<(int Index, string Name)> ActionLayers(int actionSetIndex)
        {
            UniversalProfileActionSet set = FindSet(actionSetIndex);
            if (set == null) return Array.Empty<(int, string)>();

            return set.Layers
                .OrderBy(item => item.Index)
                .Select(item => (item.Index, item.Name))
                .ToArray();
        }

        // Rebuilding the presentation for a different controller never
        // touches the stored profile - only the capabilities reference used
        // for display computation changes.
        public void SetController(ControllerCapabilities newCapabilities)
        {
            capabilities = newCapabilities;
        }

        public void SelectActionSetAndLayer(int actionSetIndex, int actionLayerIndex)
        {
            UniversalProfileActionSet set = FindSet(actionSetIndex);
            if (set == null)
            {
                throw new ArgumentException($"Action set {actionSetIndex} does not exist.", nameof(actionSetIndex));
            }

            if (!set.Layers.Any(item => item.Index == actionLayerIndex))
            {
                throw new ArgumentException($"Action layer {actionLayerIndex} does not exist in set {actionSetIndex}.", nameof(actionLayerIndex));
            }

            SelectedActionSetIndex = actionSetIndex;
            SelectedActionLayerIndex = actionLayerIndex;
        }

        // Every catalog input, classified for the currently selected
        // set/layer and controller. Callers typically split this into a
        // primary section (SupportedBound / SupportedUnbound) and a
        // preserved section (UnsupportedPreserved); UnsupportedNoBinding
        // entries carry nothing worth showing.
        public IReadOnlyList<UniversalInputPresentation> GetInputPresentations()
        {
            Dictionary<UniversalInputId, UniversalProfileBinding> bindingsByInput = CurrentLayerBindings()
                .ToDictionary(item => item.Input);

            List<UniversalInputPresentation> results = new List<UniversalInputPresentation>();
            foreach (UniversalInputMetadata metadata in UniversalInputCatalog.All)
            {
                bindingsByInput.TryGetValue(metadata.Id, out UniversalProfileBinding binding);
                EditorInputVisibilityState state = EditorInputVisibilityResolver.Resolve(
                    metadata.Id, capabilities, binding != null);

                results.Add(new UniversalInputPresentation(
                    metadata.Id,
                    metadata.ValueKind,
                    metadata.Category,
                    state,
                    ControllerLabelProvider.GetLabel(metadata.Id, capabilities),
                    ControllerGlyphProvider.GetGlyphKey(metadata.Id, capabilities),
                    capabilities == null || capabilities.Supports(metadata.Id),
                    binding));
            }

            return results;
        }

        public IReadOnlyList<UniversalInputPresentation> GetPrimaryInputPresentations()
        {
            return GetInputPresentations()
                .Where(item => EditorInputVisibilityResolver.IsPrimarilyVisible(item.VisibilityState))
                .ToArray();
        }

        public IReadOnlyList<UniversalInputPresentation> GetPreservedInputPresentations()
        {
            return GetInputPresentations()
                .Where(item => EditorInputVisibilityResolver.BelongsInPreservedSection(item.VisibilityState))
                .ToArray();
        }

        public IReadOnlyList<UniversalActionSummary> GetActionsInCurrentLayer()
        {
            UniversalProfileActionLayer layer = FindLayer(SelectedActionSetIndex, SelectedActionLayerIndex);
            if (layer == null) return Array.Empty<UniversalActionSummary>();

            return layer.Actions
                .Select(item => new UniversalActionSummary(GetActionId(item), GetActionType(item)))
                .OrderBy(item => item.ActionId)
                .ToArray();
        }

        // Assigns (or reassigns) the binding for inputId in the current
        // set/layer to an existing action id. Does not create or guess
        // action content - actionId must already exist in the current layer.
        public void AssignBinding(UniversalInputId inputId, int actionId)
        {
            UniversalProfileActionLayer layer = FindLayer(SelectedActionSetIndex, SelectedActionLayerIndex)
                ?? throw new InvalidOperationException("No action layer is selected.");

            if (!layer.Actions.Any(item => GetActionId(item) == actionId))
            {
                throw new ArgumentException($"Action {actionId} does not exist in the current layer.", nameof(actionId));
            }

            UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(inputId);
            UniversalProfileBinding existing = workingProfile.Bindings.FirstOrDefault(item =>
                item.ActionSet == SelectedActionSetIndex &&
                item.ActionLayer == SelectedActionLayerIndex &&
                item.Input == inputId);

            if (existing != null)
            {
                existing.Action = actionId;
                existing.ValueKind = metadata.ValueKind;
            }
            else
            {
                workingProfile.Bindings.Add(new UniversalProfileBinding
                {
                    ActionSet = SelectedActionSetIndex,
                    ActionLayer = SelectedActionLayerIndex,
                    Input = inputId,
                    ValueKind = metadata.ValueKind,
                    Action = actionId,
                });
            }
        }

        public void ClearBinding(UniversalInputId inputId)
        {
            workingProfile.Bindings.RemoveAll(item =>
                item.ActionSet == SelectedActionSetIndex &&
                item.ActionLayer == SelectedActionLayerIndex &&
                item.Input == inputId);
        }

        public int AddActionSet(string name)
        {
            int nextIndex = workingProfile.ActionSets.Count == 0
                ? 0
                : workingProfile.ActionSets.Max(item => item.Index) + 1;

            UniversalProfileActionSet set = new UniversalProfileActionSet
            {
                Index = nextIndex,
                Name = name ?? string.Empty,
            };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            workingProfile.ActionSets.Add(set);
            return nextIndex;
        }

        public void RenameActionSet(int actionSetIndex, string name)
        {
            UniversalProfileActionSet set = FindSet(actionSetIndex)
                ?? throw new ArgumentException($"Action set {actionSetIndex} does not exist.", nameof(actionSetIndex));
            set.Name = name ?? string.Empty;
        }

        // Removing a set removes the bindings that reference it - those
        // bindings cannot exist independently of their set, so this is a
        // direct consequence of the user's action, not incidental loss.
        public void RemoveActionSet(int actionSetIndex)
        {
            UniversalProfileActionSet set = FindSet(actionSetIndex)
                ?? throw new ArgumentException($"Action set {actionSetIndex} does not exist.", nameof(actionSetIndex));

            workingProfile.ActionSets.Remove(set);
            workingProfile.Bindings.RemoveAll(item => item.ActionSet == actionSetIndex);

            if (SelectedActionSetIndex == actionSetIndex)
            {
                SelectFirstAvailableSetAndLayer();
            }
        }

        public int AddActionLayer(int actionSetIndex, string name)
        {
            UniversalProfileActionSet set = FindSet(actionSetIndex)
                ?? throw new ArgumentException($"Action set {actionSetIndex} does not exist.", nameof(actionSetIndex));

            int nextIndex = set.Layers.Count == 0 ? 0 : set.Layers.Max(item => item.Index) + 1;
            set.Layers.Add(new UniversalProfileActionLayer { Index = nextIndex, Name = name ?? string.Empty });
            return nextIndex;
        }

        public void RenameActionLayer(int actionSetIndex, int actionLayerIndex, string name)
        {
            UniversalProfileActionLayer layer = FindLayer(actionSetIndex, actionLayerIndex)
                ?? throw new ArgumentException("Action layer does not exist.");
            layer.Name = name ?? string.Empty;
        }

        public void RemoveActionLayer(int actionSetIndex, int actionLayerIndex)
        {
            UniversalProfileActionSet set = FindSet(actionSetIndex)
                ?? throw new ArgumentException($"Action set {actionSetIndex} does not exist.", nameof(actionSetIndex));
            UniversalProfileActionLayer layer = set.Layers.FirstOrDefault(item => item.Index == actionLayerIndex)
                ?? throw new ArgumentException("Action layer does not exist.");

            set.Layers.Remove(layer);
            workingProfile.Bindings.RemoveAll(item =>
                item.ActionSet == actionSetIndex && item.ActionLayer == actionLayerIndex);

            if (SelectedActionSetIndex == actionSetIndex && SelectedActionLayerIndex == actionLayerIndex)
            {
                SelectFirstAvailableSetAndLayer();
            }
        }

        // The full, unfiltered projection - everything visible plus
        // everything preserved. This is the only place a caller should get
        // a profile to persist.
        public UniversalProfile BuildUpdatedProfile()
        {
            return workingProfile.Clone();
        }

        // Splices an action-content edit (from UniversalActionContentEditorSession)
        // back into the working profile. Scoped to exactly one layer's Actions,
        // mirroring the precision UniversalActionContentEditorSession.BuildUpdatedProfile
        // already guarantees - bindings, other sets/layers and profile identity are
        // never touched here.
        public void ReplaceLayerActions(int actionSetIndex, int actionLayerIndex, IEnumerable<JObject> actions)
        {
            UniversalProfileActionLayer layer = FindLayer(actionSetIndex, actionLayerIndex)
                ?? throw new ArgumentException("Action layer does not exist.");

            layer.Actions.Clear();
            layer.Actions.AddRange((actions ?? Enumerable.Empty<JObject>()).Select(item => (JObject)item.DeepClone()));
        }

        private IEnumerable<UniversalProfileBinding> CurrentLayerBindings()
        {
            return workingProfile.Bindings.Where(item =>
                item.ActionSet == SelectedActionSetIndex &&
                item.ActionLayer == SelectedActionLayerIndex);
        }

        private UniversalProfileActionSet FindSet(int actionSetIndex)
        {
            return workingProfile.ActionSets.FirstOrDefault(item => item.Index == actionSetIndex);
        }

        private UniversalProfileActionLayer FindLayer(int actionSetIndex, int actionLayerIndex)
        {
            return FindSet(actionSetIndex)?.Layers.FirstOrDefault(item => item.Index == actionLayerIndex);
        }

        private void SelectFirstAvailableSetAndLayer()
        {
            UniversalProfileActionSet firstSet = workingProfile.ActionSets.OrderBy(item => item.Index).FirstOrDefault();
            if (firstSet == null)
            {
                SelectedActionSetIndex = 0;
                SelectedActionLayerIndex = 0;
                return;
            }

            SelectedActionSetIndex = firstSet.Index;
            SelectedActionLayerIndex = firstSet.Layers.OrderBy(item => item.Index).FirstOrDefault()?.Index ?? 0;
        }

        private static int GetActionId(JObject action)
        {
            return action.Value<int?>("id") ?? action.Value<int?>("Id") ?? -1;
        }

        private static string GetActionType(JObject action)
        {
            return action.Value<string>("type") ?? action.Value<string>("ActionMode") ?? string.Empty;
        }
    }
}
