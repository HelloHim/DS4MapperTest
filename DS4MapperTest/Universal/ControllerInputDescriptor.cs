using System;

namespace DS4MapperTest.Universal
{
    public sealed class ControllerInputDescriptor
    {
        public UniversalInputId InputId { get; }
        public UniversalInputValueKind ValueKind { get; }
        public bool IsSupported { get; }
        public string NativeDisplayLabel { get; }
        public string GlyphKey { get; }
        public ControllerInputSource Source { get; }

        public ControllerInputDescriptor(
            UniversalInputId inputId,
            UniversalInputValueKind valueKind,
            bool isSupported = true,
            string nativeDisplayLabel = "",
            string glyphKey = "",
            ControllerInputSource source = null)
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
            IsSupported = isSupported;
            NativeDisplayLabel = nativeDisplayLabel ?? string.Empty;
            GlyphKey = glyphKey ?? string.Empty;
            Source = source ?? ControllerInputSource.None;
        }
    }
}
