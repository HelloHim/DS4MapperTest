using DS4MapperTest.Universal.Profiles;

namespace DS4MapperTest.Universal.Editor
{
    // A single universal input projected for editor display: what it is,
    // whether the selected controller supports it, whether the current
    // set/layer has a binding for it, and what label/glyph to show. This is
    // a read-only snapshot; editing happens through UniversalProfileEditorModel,
    // never by mutating this object.
    public sealed class UniversalInputPresentation
    {
        public UniversalInputId InputId { get; }
        public UniversalInputValueKind ValueKind { get; }
        public UniversalInputCategory Category { get; }
        public EditorInputVisibilityState VisibilityState { get; }
        public string Label { get; }
        public string GlyphKey { get; }
        public bool IsSupportedByController { get; }
        public UniversalProfileBinding Binding { get; }

        public bool HasBinding => Binding != null;

        public UniversalInputPresentation(
            UniversalInputId inputId,
            UniversalInputValueKind valueKind,
            UniversalInputCategory category,
            EditorInputVisibilityState visibilityState,
            string label,
            string glyphKey,
            bool isSupportedByController,
            UniversalProfileBinding binding)
        {
            InputId = inputId;
            ValueKind = valueKind;
            Category = category;
            VisibilityState = visibilityState;
            Label = label ?? string.Empty;
            GlyphKey = glyphKey ?? string.Empty;
            IsSupportedByController = isSupportedByController;
            Binding = binding;
        }
    }
}
