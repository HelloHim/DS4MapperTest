using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.Common;

namespace DS4MapperTest.Views.Shared
{
    public partial class CalibrationModeControl : UserControl
    {
        public CalibrationModeControl()
        {
            InitializeComponent();

            // Angle Calibration is one setting for the whole profile, so every panel
            // showing this control has to follow the profile for as long as it is on
            // screen: attaching here keeps the panels in step with each other, and
            // detaching keeps a closed panel's ViewModel from staying wired to a profile
            // that outlives it.
            Loaded += CalibrationModeControl_Loaded;
            Unloaded += CalibrationModeControl_Unloaded;
            DataContextChanged += CalibrationModeControl_DataContextChanged;
        }

        private bool attached;

        private void CalibrationModeControl_Loaded(object sender, RoutedEventArgs e)
        {
            Attach(DataContext as ICalibrationPanelViewModel);
        }

        private void CalibrationModeControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Detach(DataContext as ICalibrationPanelViewModel);
        }

        private void CalibrationModeControl_DataContextChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            Detach(e.OldValue as ICalibrationPanelViewModel);
            if (IsLoaded)
            {
                Attach(e.NewValue as ICalibrationPanelViewModel);
            }
        }

        private void Attach(ICalibrationPanelViewModel calibVM)
        {
            if (attached || calibVM == null) return;
            attached = true;
            // AttachProfileCalibEvents ends in BeginPanelInit, which is what holds this
            // control's fields off the profile until they hold real values rather than the
            // NumericUpDown minimums they start life with.
            calibVM.AttachProfileCalibEvents();
        }

        private void Detach(ICalibrationPanelViewModel calibVM)
        {
            if (!attached || calibVM == null) return;
            attached = false;
            calibVM.DetachProfileCalibEvents();
        }
    }
}
