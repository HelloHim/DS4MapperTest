using System;

namespace DS4MapperTest.Universal
{
    public sealed class UniversalInputBindingIdentity
    {
        public UniversalInputId InputId { get; }

        public UniversalInputBindingIdentity(UniversalInputId inputId)
        {
            if (!UniversalInputCatalog.IsDefined(inputId))
            {
                throw new ArgumentOutOfRangeException(nameof(inputId), inputId, "Unknown universal input id.");
            }

            InputId = inputId;
        }
    }

    public sealed class UniversalInputStateSnapshot
    {
        public UniversalInputId InputId { get; }
        public UniversalInputValueKind ValueKind { get; }
        public bool IsActive { get; }

        public UniversalInputStateSnapshot(
            UniversalInputId inputId,
            UniversalInputValueKind valueKind,
            bool isActive)
        {
            UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(inputId);
            if (metadata.ValueKind != valueKind)
            {
                throw new ArgumentException(
                    $"Input {inputId} is {metadata.ValueKind}, not {valueKind}.",
                    nameof(valueKind));
            }

            InputId = inputId;
            ValueKind = valueKind;
            IsActive = isActive;
        }
    }

    public enum UniversalInputEditorVisibility
    {
        Visible,
        Disabled,
        Hidden,
    }

    public sealed class UniversalInputEditorPresentation
    {
        public UniversalInputId InputId { get; }
        public UniversalInputEditorVisibility Visibility { get; }

        public UniversalInputEditorPresentation(
            UniversalInputId inputId,
            UniversalInputEditorVisibility visibility)
        {
            if (!UniversalInputCatalog.IsDefined(inputId))
            {
                throw new ArgumentOutOfRangeException(nameof(inputId), inputId, "Unknown universal input id.");
            }

            InputId = inputId;
            Visibility = visibility;
        }
    }
}
