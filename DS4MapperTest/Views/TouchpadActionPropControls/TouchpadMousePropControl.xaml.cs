using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DS4MapperTest.GyroActions;
using DS4MapperTest.ViewModels.TouchpadActionPropViewModels;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.TouchpadActions;

namespace DS4MapperTest.Views.TouchpadActionPropControls
{
    /// <summary>
    /// Interaction logic for TouchpadMousePropControl.xaml
    /// </summary>
    public partial class TouchpadMousePropControl : UserControl, ISectionAwareTouchpadPropControl
    {
        private TouchpadMousePropViewModel touchMousePropVM;
        public TouchpadMousePropViewModel TouchMousePropVM => touchMousePropVM;

        public TouchpadMousePropControl()
        {
            InitializeComponent();
        }

        public void PostInit(Mapper mapper, TouchpadMapAction action)
        {
            touchMousePropVM = new TouchpadMousePropViewModel(mapper, action);

            DataContext = touchMousePropVM;
        }

        public void ApplySection(TouchpadSettingsSection section)
        {
            ExtraFieldsPanel.Visibility = TouchpadUiFeatureFlags.ShowActionNameField && section == TouchpadSettingsSection.Extra
                ? Visibility.Visible : Visibility.Collapsed;
            MovementFieldsPanel.Visibility = section == TouchpadSettingsSection.MouseMovement
                ? Visibility.Visible : Visibility.Collapsed;
            SensitivityFieldsPanel.Visibility = section == TouchpadSettingsSection.SensitivityCalibration
                ? Visibility.Visible : Visibility.Collapsed;
            FilteringFieldsPanel.Visibility = section == TouchpadSettingsSection.FilteringStabilisation
                ? Visibility.Visible : Visibility.Collapsed;
            TrackballFieldsPanel.Visibility = section == TouchpadSettingsSection.TrackballScroll
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // Mirrors GyroSensitivityControl's Static/Acceleration tab behaviour: picking
        // Static Sensitivity always clears the acceleration curve, and picking
        // Acceleration Curve while it is still unset gives it a starting curve.
        private void SensitivityModeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tabControl)
            {
                SyncSensitivityMode(tabControl);
            }
        }

        private void SensitivityModeTabs_DataContextChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if (sender is TabControl tabControl)
            {
                SyncSensitivityMode(tabControl);
            }
        }

        private static void SyncSensitivityMode(TabControl tabControl)
        {
            if (tabControl.SelectedItem is not TabItem selectedTab ||
                tabControl.DataContext is not TouchpadMousePropViewModel vm)
            {
                return;
            }

            switch (selectedTab.Header as string)
            {
                case "Static Sensitivity":
                    // Static sensitivity always behaves as an unbound acceleration curve.
                    vm.AccelCurveChoice = GyroMouseAccelCurveChoice.None;
                    break;
                case "Acceleration Curve" when vm.AccelCurveChoice == GyroMouseAccelCurveChoice.None:
                    vm.AccelCurveChoice = GyroMouseAccelCurveChoice.Linear;
                    break;
            }
        }
    }
}
