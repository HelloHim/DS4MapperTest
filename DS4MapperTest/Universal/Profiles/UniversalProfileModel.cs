using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest.Universal.Profiles
{
    public sealed class UniversalProfile
    {
        public const int CurrentSchemaVersion = 1;

        public Guid ProfileId { get; set; } = Guid.NewGuid();
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public JObject ProfileSettings { get; set; } = new JObject();
        public JObject ExtensionData { get; set; } = new JObject();
        public UniversalProfileMigrationProvenance Migration { get; set; }
        public List<UniversalProfileActionSet> ActionSets { get; } = new List<UniversalProfileActionSet>();
        public List<UniversalProfileBinding> Bindings { get; } = new List<UniversalProfileBinding>();

        public UniversalProfile Clone()
        {
            UniversalProfile clone = new UniversalProfile
            {
                ProfileId = ProfileId,
                SchemaVersion = SchemaVersion,
                DisplayName = DisplayName,
                Description = Description,
                CreatedUtc = CreatedUtc,
                ProfileSettings = ProfileSettings != null ? (JObject)ProfileSettings.DeepClone() : new JObject(),
                ExtensionData = ExtensionData != null ? (JObject)ExtensionData.DeepClone() : new JObject(),
                Migration = Migration?.Clone(),
            };

            clone.ActionSets.AddRange(ActionSets.Select(item => item.Clone()));
            clone.Bindings.AddRange(Bindings.Select(item => item.Clone()));
            return clone;
        }
    }

    public sealed class UniversalProfileMigrationProvenance
    {
        public string SourceFamily { get; set; } = string.Empty;
        public string SourceIdentity { get; set; } = string.Empty;
        public string SourceContentHash { get; set; } = string.Empty;
        public int MigrationSchemaVersion { get; set; } = LegacyProfileMigrator.MigrationSchemaVersion;

        public UniversalProfileMigrationProvenance Clone()
        {
            return new UniversalProfileMigrationProvenance
            {
                SourceFamily = SourceFamily,
                SourceIdentity = SourceIdentity,
                SourceContentHash = SourceContentHash,
                MigrationSchemaVersion = MigrationSchemaVersion,
            };
        }
    }

    public sealed class UniversalProfileActionSet
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<UniversalProfileActionLayer> Layers { get; } = new List<UniversalProfileActionLayer>();

        public UniversalProfileActionSet Clone()
        {
            UniversalProfileActionSet clone = new UniversalProfileActionSet
            {
                Index = Index,
                Name = Name,
                Description = Description,
            };

            clone.Layers.AddRange(Layers.Select(item => item.Clone()));
            return clone;
        }
    }

    public sealed class UniversalProfileActionLayer
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<JObject> Actions { get; } = new List<JObject>();

        public UniversalProfileActionLayer Clone()
        {
            UniversalProfileActionLayer clone = new UniversalProfileActionLayer
            {
                Index = Index,
                Name = Name,
                Description = Description,
            };

            clone.Actions.AddRange(Actions.Select(item => (JObject)item.DeepClone()));
            return clone;
        }
    }

    public sealed class UniversalProfileBinding
    {
        public int ActionSet { get; set; }
        public int ActionLayer { get; set; }
        public UniversalInputId Input { get; set; }
        public UniversalInputValueKind ValueKind { get; set; }
        public int Action { get; set; }
        public string LegacyInput { get; set; } = string.Empty;

        public UniversalProfileBinding Clone()
        {
            return new UniversalProfileBinding
            {
                ActionSet = ActionSet,
                ActionLayer = ActionLayer,
                Input = Input,
                ValueKind = ValueKind,
                Action = Action,
                LegacyInput = LegacyInput,
            };
        }
    }
}
