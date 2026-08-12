namespace DS4MapperTest.Universal.Editor
{
    // Resolves editor visibility purely from controller capabilities plus
    // whether a binding is stored for an input in the current set/layer.
    // Never inspects binding content beyond presence, and never writes
    // anything back to the profile.
    public static class EditorInputVisibilityResolver
    {
        // With no controller selected, the documented policy is "full
        // visibility": every catalog input is treated as supported so the
        // whole profile remains editable without hardware.
        public static EditorInputVisibilityState Resolve(
            UniversalInputId inputId,
            ControllerCapabilities capabilities,
            bool hasBinding)
        {
            bool supported = capabilities == null || capabilities.Supports(inputId);

            if (supported)
            {
                return hasBinding
                    ? EditorInputVisibilityState.SupportedBound
                    : EditorInputVisibilityState.SupportedUnbound;
            }

            return hasBinding
                ? EditorInputVisibilityState.UnsupportedPreserved
                : EditorInputVisibilityState.UnsupportedNoBinding;
        }

        public static bool BelongsInPreservedSection(EditorInputVisibilityState state)
        {
            return state == EditorInputVisibilityState.UnsupportedPreserved;
        }

        public static bool IsPrimarilyVisible(EditorInputVisibilityState state)
        {
            return state == EditorInputVisibilityState.SupportedBound ||
                state == EditorInputVisibilityState.SupportedUnbound;
        }
    }
}
