namespace DS4MapperTest.Universal
{
    public sealed class UniversalInputMetadata
    {
        public UniversalInputId Id { get; }
        public UniversalInputValueKind ValueKind { get; }
        public UniversalInputCategory Category { get; }
        public string DisplayName { get; }

        public UniversalInputMetadata(
            UniversalInputId id,
            UniversalInputValueKind valueKind,
            UniversalInputCategory category,
            string displayName)
        {
            Id = id;
            ValueKind = valueKind;
            Category = category;
            DisplayName = displayName;
        }
    }
}
