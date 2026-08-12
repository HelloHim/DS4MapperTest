namespace DS4MapperTest.Universal.Editor
{
    // Distinguishes the four ways a universal input can present in the editor
    // for a given selected controller. This is a display-only classification;
    // it never drives deletion or fabrication of stored bindings.
    public enum EditorInputVisibilityState
    {
        // The selected controller does not support this input and no binding
        // is stored for it in the current set/layer. Nothing to preserve.
        UnsupportedNoBinding,

        // The selected controller supports this input but the current
        // set/layer has no stored binding for it yet.
        SupportedUnbound,

        // The selected controller supports this input and the current
        // set/layer has a stored binding for it.
        SupportedBound,

        // The selected controller does not support this input, but a
        // binding is stored for it in the current set/layer. Must be shown
        // in a dedicated preserved/other-controllers section, never dropped.
        UnsupportedPreserved,
    }
}
