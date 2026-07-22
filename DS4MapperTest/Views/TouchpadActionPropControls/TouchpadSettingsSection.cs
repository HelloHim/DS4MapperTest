namespace DS4MapperTest.Views.TouchpadActionPropControls
{
    public static class TouchpadUiFeatureFlags
    {
        // Flip to true to restore the per-binding "Action Name" field across
        // all touchpad settings prop controls.
        public const bool ShowActionNameField = false;
    }

    public enum TouchpadSettingsSection
    {
        ModeSettings,
        MouseMovement,
        SensitivityCalibration,
        FilteringStabilisation,
        Zones,
        OuterRing,
        Gestures,
        TrackballScroll,
        Advanced,
        Extra,
    }

    public interface ISectionAwareTouchpadPropControl
    {
        void ApplySection(TouchpadSettingsSection section);
    }
}
