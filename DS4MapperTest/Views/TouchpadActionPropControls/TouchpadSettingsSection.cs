namespace DS4MapperTest.Views.TouchpadActionPropControls
{
    public enum TouchpadSettingsSection
    {
        MouseMovement,
        ZonesGestures,
        TrackballScroll,
        Advanced,
    }

    public interface ISectionAwareTouchpadPropControl
    {
        void ApplySection(TouchpadSettingsSection section);
    }
}
