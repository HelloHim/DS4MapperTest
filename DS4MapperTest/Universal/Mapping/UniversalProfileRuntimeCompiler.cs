using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DS4MapperTest.Universal.Mapping
{
    public sealed class UniversalProfileCompilationException : Exception
    {
        public UniversalProfileCompilationException(string message)
            : base(message)
        {
        }
    }

    public sealed class UniversalCompiledProfile
    {
        public UniversalCompiledProfile(
            UniversalProfile sourceProfile,
            string legacyJson,
            IReadOnlyDictionary<UniversalInputId, string> activeBindingIds,
            IReadOnlyList<UniversalProfileBinding> unsupportedBindings)
        {
            SourceProfile = sourceProfile ?? throw new ArgumentNullException(nameof(sourceProfile));
            LegacyJson = legacyJson ?? throw new ArgumentNullException(nameof(legacyJson));
            ActiveBindingIds = activeBindingIds ?? throw new ArgumentNullException(nameof(activeBindingIds));
            UnsupportedBindings = unsupportedBindings ?? throw new ArgumentNullException(nameof(unsupportedBindings));
        }

        public UniversalProfile SourceProfile { get; }
        public string LegacyJson { get; }
        public IReadOnlyDictionary<UniversalInputId, string> ActiveBindingIds { get; }
        public IReadOnlyList<UniversalProfileBinding> UnsupportedBindings { get; }
    }

    public static class UniversalProfileRuntimeCompiler
    {
        public static UniversalCompiledProfile Compile(
            UniversalProfile profile,
            ControllerCapabilities capabilities)
        {
            UniversalProfileValidationResult validation = UniversalProfileValidator.Validate(profile);
            if (!validation.IsValid)
            {
                throw new UniversalProfileCompilationException(
                    string.Join(Environment.NewLine, validation.Issues.Select(item => $"{item.Location}: {item.Message}")));
            }

            ControllerCapabilities controllerCapabilities =
                capabilities ?? new ControllerCapabilities(ControllerDisplayInfo.Unknown(), Array.Empty<ControllerInputDescriptor>());

            Dictionary<UniversalInputId, string> activeBindingIds = new Dictionary<UniversalInputId, string>();
            List<UniversalProfileBinding> unsupportedBindings = new List<UniversalProfileBinding>();
            JObject legacyRoot = CreateLegacyRoot(profile);
            legacyRoot["ActionSets"] = new JArray(profile.ActionSets
                .OrderBy(item => item.Index)
                .Select(CreateLegacyActionSet));

            legacyRoot["Mappings"] = new JArray(profile.Bindings
                .GroupBy(item => (item.ActionSet, item.ActionLayer))
                .OrderBy(item => item.Key.ActionSet)
                .ThenBy(item => item.Key.ActionLayer)
                .Select(group => CreateLegacyMappingGroup(
                    group.Key.ActionSet,
                    group.Key.ActionLayer,
                    group,
                    controllerCapabilities,
                    activeBindingIds,
                    unsupportedBindings)));

            string json = legacyRoot.ToString(Formatting.None);
            return new UniversalCompiledProfile(
                profile.Clone(),
                json,
                activeBindingIds,
                unsupportedBindings);
        }

        private static JObject CreateLegacyRoot(UniversalProfile profile)
        {
            JObject root = new JObject
            {
                ["Name"] = profile.DisplayName,
                ["Description"] = profile.Description,
                ["CreationDate"] = profile.CreatedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                ["ControllerType"] = "Universal",
                ["ProfileSpecVersion"] = 2,
            };

            if (profile.ProfileSettings != null)
            {
                foreach (JProperty property in profile.ProfileSettings.Properties())
                {
                    root[property.Name] = property.Value.DeepClone();
                }
            }

            return root;
        }

        private static JObject CreateLegacyActionSet(UniversalProfileActionSet set)
        {
            return new JObject
            {
                ["Index"] = set.Index,
                ["Name"] = set.Name,
                ["Description"] = set.Description,
                ["ActionLayers"] = new JArray(set.Layers
                    .OrderBy(item => item.Index)
                    .Select(CreateLegacyActionLayer)),
            };
        }

        private static JObject CreateLegacyActionLayer(UniversalProfileActionLayer layer)
        {
            return new JObject
            {
                ["Index"] = layer.Index,
                ["Name"] = layer.Name,
                ["Description"] = layer.Description,
                ["MappedActions"] = new JArray(layer.Actions
                    .OrderBy(GetActionId)
                    .Select(CreateLegacyAction)),
            };
        }

        private static JObject CreateLegacyAction(JObject action)
        {
            JObject payload = action["payload"] as JObject;
            JObject result = payload != null
                ? (JObject)payload.DeepClone()
                : (JObject)action.DeepClone();

            int id = GetActionId(action);
            string actionMode = (action.Value<string>("type") ??
                action.Value<string>("ActionMode") ??
                result.Value<string>("ActionMode") ??
                string.Empty).Trim();

            result["Id"] = id;
            result["ActionMode"] = actionMode;
            result.Remove("id");
            result.Remove("type");
            result.Remove("payload");
            return result;
        }

        private static JObject CreateLegacyMappingGroup(
            int actionSet,
            int actionLayer,
            IEnumerable<UniversalProfileBinding> bindings,
            ControllerCapabilities capabilities,
            Dictionary<UniversalInputId, string> activeBindingIds,
            List<UniversalProfileBinding> unsupportedBindings)
        {
            HashSet<(string Input, int Action)> emitted = new HashSet<(string, int)>();
            JArray inputMappings = new JArray();
            foreach (UniversalProfileBinding binding in bindings
                .OrderBy(item => UniversalInputToken.Format(item.Input), StringComparer.Ordinal))
            {
                if (!capabilities.Supports(binding.Input))
                {
                    unsupportedBindings.Add(binding.Clone());
                    continue;
                }

                if (!UniversalLegacyBindingMap.TryGetBinding(binding.Input, out UniversalRuntimeBinding runtimeBinding))
                {
                    unsupportedBindings.Add(binding.Clone());
                    continue;
                }

                if (!emitted.Add((runtimeBinding.LegacyBindingId, binding.Action)))
                {
                    continue;
                }

                activeBindingIds[binding.Input] = runtimeBinding.LegacyBindingId;
                inputMappings.Add(new JObject
                {
                    ["Input"] = runtimeBinding.LegacyBindingId,
                    ["Action"] = binding.Action,
                });
            }

            return new JObject
            {
                ["ActionSet"] = actionSet,
                ["ActionLayer"] = actionLayer,
                ["InputMappings"] = inputMappings,
            };
        }

        private static int GetActionId(JObject action)
        {
            return action.Value<int?>("id") ??
                action.Value<int?>("Id") ??
                -1;
        }
    }
}
