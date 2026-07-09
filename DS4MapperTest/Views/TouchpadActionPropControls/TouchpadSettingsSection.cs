namespace DS4MapperTest.Views.TouchpadActionPropControls
{
    public enum TouchpadSettingsSection
    {
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
