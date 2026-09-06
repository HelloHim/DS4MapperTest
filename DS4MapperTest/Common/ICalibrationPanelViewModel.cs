namespace DS4MapperTest.Common
{
    // Angle Calibration is one profile-wide setting (Profile.CalibMode/CalibRwc/
    // CalibInGameSens/CalibCounts/CalibPresetName) surfaced by every panel that shows
    // CalibrationModeControl: Gyro, Stick/Trackpad Mouse, Stick/Touchpad Flick Stick,
    // Hybrid Aim and the Flick Turn output binding. Each of those panels caches the
    // values in its own ViewModel, so a panel has to listen to the profile while it is
    // on screen or it would show, and then write back, numbers another panel has since
    // replaced.
    //
    // CalibrationModeControl drives this from its own Loaded/Unloaded, so the listening
    // lasts exactly as long as the panel is in the visual tree instead of for the life
    // of the profile. Attach is expected to pull the current profile values back in, and
    // both calls are reference counted so several controls can share one ViewModel.
    public interface ICalibrationPanelViewModel
    {
        void AttachProfileCalibEvents();
        void DetachProfileCalibEvents();

        // Called by the control before its own fields go live. HandyControl's NumericUpDown
        // fires ValueChanged(Minimum) while it initialises, before the binding has handed it
        // the real number, so a ViewModel has to ignore writes until the control settles or
        // that init write lands in the profile as a zeroed calibration. A ViewModel that
        // outlives its control -- a stick mode panel rebuilt when the user switches modes
        // and back, for example -- gets a fresh control and so needs a fresh window here.
        void BeginPanelInit();
    }
}
