namespace DS4MapperTest.Views.TouchpadActionPropControls
{
    public enum TouchpadSettingsSection
    {
        ModeSettings,
        MouseMovement,
        SensitivityCalibration,
        FilteringStabilisation,
        ZonesGestures,
        TrackballScroll,
        Advanced,
        Extra,
    }

    public interface ISectionAwareTouchpadPropControl
    {
        void ApplySection(TouchpadSettingsSection section);
    }
}
