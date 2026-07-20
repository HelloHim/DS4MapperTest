namespace DS4MapperTest.Views
{
    /// <summary>
    /// Enforces that only one QuickBindControl is listening for input at a
    /// time. The actual keyboard/mouse event handling stays on the active
    /// QuickBindControl itself (it already owns keyboard focus and mouse
    /// capture while listening); this service only arbitrates hand-offs and
    /// gives every row a single place to ask "am I still the active one".
    /// </summary>
    internal static class QuickBindCaptureService
    {
        private static QuickBindControl activeControl;

        public static bool IsCapturing => activeControl != null;

        public static bool IsActive(QuickBindControl control) => activeControl == control;

        // Returns true if the caller should proceed to enter listening state.
        // Cancels any previously active row first so only one control is ever
        // shown as "listening" at once.
        public static bool RequestCapture(QuickBindControl control)
        {
            if (activeControl == control)
            {
                return false;
            }

            CancelActive();
            activeControl = control;
            return true;
        }

        public static void CancelActive()
        {
            QuickBindControl previous = activeControl;
            activeControl = null;
            previous?.CancelCapture();
        }

        public static void NotifyEnded(QuickBindControl control)
        {
            if (activeControl == control)
            {
                activeControl = null;
            }
        }
    }
}
