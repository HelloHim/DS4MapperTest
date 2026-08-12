using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DS4MapperTest.Universal.Profiles
{
    public enum UniversalProfileValidationSeverity
    {
        Warning,
        Error,
    }

    public sealed class UniversalProfileValidationIssue
    {
        public UniversalProfileValidationIssue(
            UniversalProfileValidationSeverity severity,
            string location,
            string message)
        {
            Severity = severity;
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public UniversalProfileValidationSeverity Severity { get; }
        public string Location { get; }
        public string Message { get; }
    }

    public sealed class UniversalProfileValidationResult
    {
        private readonly List<UniversalProfileValidationIssue> issues =
            new List<UniversalProfileValidationIssue>();

        public IReadOnlyList<UniversalProfileValidationIssue> Issues => issues;
        public bool IsValid => !issues.Any(item => item.Severity == UniversalProfileValidationSeverity.Error);

        public void AddError(string location, string message)
        {
            issues.Add(new UniversalProfileValidationIssue(
                UniversalProfileValidationSeverity.Error,
                location,
                message));
        }

        public void AddWarning(string location, string message)
        {
            issues.Add(new UniversalProfileValidationIssue(
                UniversalProfileValidationSeverity.Warning,
                location,
                message));
        }
    }

    public sealed class UniversalProfileValidationException : Exception
    {
        public UniversalProfileValidationException(UniversalProfileValidationResult result)
            : base(string.Join(Environment.NewLine, result.Issues.Select(item => $"{item.Location}: {item.Message}")))
        {
            Result = result;
        }

        public UniversalProfileValidationResult Result { get; }
    }

    public static class UniversalProfileValidator
    {
        public static UniversalProfileValidationResult Validate(UniversalProfile profile)
        {
            UniversalProfileValidationResult result = new UniversalProfileValidationResult();
            if (profile == null)
            {
                result.AddError("$", "Profile is missing.");
                return result;
            }

            if (profile.ProfileId == Guid.Empty)
            {
                result.AddError("$.profileId", "Profile id is required.");
            }

            if (profile.SchemaVersion != UniversalProfile.CurrentSchemaVersion)
            {
                result.AddError("$.schemaVersion", $"Unsupported universal profile schema version {profile.SchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                result.AddError("$.displayName", "Display name is required.");
            }

            Dictionary<(int Set, int Layer), HashSet<int>> actionIdsByLayer = new Dictionary<(int, int), HashSet<int>>();
            HashSet<int> setIndexes = new HashSet<int>();
            for (int setIndex = 0; setIndex < profile.ActionSets.Count; setIndex++)
            {
                UniversalProfileActionSet set = profile.ActionSets[setIndex];
                if (!setIndexes.Add(set.Index))
                {
                    result.AddError($"$.actionSets[{setIndex}].index", $"Duplicate action set index {set.Index}.");
                }

                HashSet<int> layerIndexes = new HashSet<int>();
                for (int layerIndex = 0; layerIndex < set.Layers.Count; layerIndex++)
                {
                    UniversalProfileActionLayer layer = set.Layers[layerIndex];
                    if (!layerIndexes.Add(layer.Index))
                    {
                        result.AddError($"$.actionSets[{setIndex}].layers[{layerIndex}].index", $"Duplicate action layer index {layer.Index}.");
                    }

                    HashSet<int> actionIds = new HashSet<int>();
                    actionIdsByLayer[(set.Index, layer.Index)] = actionIds;
                    for (int actionIndex = 0; actionIndex < layer.Actions.Count; actionIndex++)
                    {
                        JObject action = layer.Actions[actionIndex];
                        JToken idToken = action["id"] ?? action["Id"];
                        JToken modeToken = action["type"] ?? action["ActionMode"];
                        if (idToken == null || idToken.Type != JTokenType.Integer)
                        {
                            result.AddError($"$.actionSets[{setIndex}].layers[{layerIndex}].actions[{actionIndex}].id", "Action id is required.");
                            continue;
                        }

                        int actionId = idToken.Value<int>();
                        if (!actionIds.Add(actionId))
                        {
                            result.AddError($"$.actionSets[{setIndex}].layers[{layerIndex}].actions[{actionIndex}].id", $"Duplicate action id {actionId}.");
                        }

                        if (modeToken == null || modeToken.Type != JTokenType.String || string.IsNullOrWhiteSpace(modeToken.Value<string>()))
                        {
                            result.AddError($"$.actionSets[{setIndex}].layers[{layerIndex}].actions[{actionIndex}].type", "Stable action type identifier is required.");
                        }
                    }
                }
            }

            HashSet<(int Set, int Layer, UniversalInputId Input)> bindings =
                new HashSet<(int, int, UniversalInputId)>();
            for (int index = 0; index < profile.Bindings.Count; index++)
            {
                UniversalProfileBinding binding = profile.Bindings[index];
                string location = $"$.bindings[{index}]";
                if (!UniversalInputCatalog.TryGetMetadata(binding.Input, out UniversalInputMetadata metadata))
                {
                    result.AddError($"{location}.input", $"Unknown universal input id {binding.Input}.");
                    continue;
                }

                if (binding.ValueKind != metadata.ValueKind)
                {
                    result.AddError($"{location}.valueKind", $"Binding value kind {binding.ValueKind} does not match {binding.Input} metadata {metadata.ValueKind}.");
                }

                if (!bindings.Add((binding.ActionSet, binding.ActionLayer, binding.Input)))
                {
                    result.AddError(location, $"Duplicate binding for {binding.Input} on set {binding.ActionSet}, layer {binding.ActionLayer}.");
                }

                if (!actionIdsByLayer.TryGetValue((binding.ActionSet, binding.ActionLayer), out HashSet<int> actionIds))
                {
                    result.AddError(location, $"Binding references missing set {binding.ActionSet}, layer {binding.ActionLayer}.");
                }
                else if (!actionIds.Contains(binding.Action))
                {
                    result.AddError($"{location}.action", $"Binding references missing action id {binding.Action}.");
                }
            }

            if (ContainsRootedPath(profile.ProfileSettings))
            {
                result.AddError("$.profileSettings", "Profile settings contain a rooted local path.");
            }

            return result;
        }

        private static bool ContainsRootedPath(JToken token)
        {
            if (token == null)
            {
                return false;
            }

            if (token.Type == JTokenType.String)
            {
                string value = token.Value<string>();
                return !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value);
            }

            return token.Children().Any(ContainsRootedPath);
        }
    }
}
