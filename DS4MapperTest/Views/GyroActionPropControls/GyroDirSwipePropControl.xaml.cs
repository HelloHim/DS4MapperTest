using System;
using System.Windows.Controls;
using DS4MapperTest.GyroActions;
using DS4MapperTest.ViewModels.GyroActionPropViewModels;

namespace DS4MapperTest.Views.GyroActionPropControls
{
    /// <summary>
    /// Interaction logic for GyroDirSwipePropControl.xaml
    /// </summary>
    public partial class GyroDirSwipePropControl : UserControl
    {
        private GyroDirSwipeActionPropViewModel gyroDirSwipeVM;
        public GyroDirSwipeActionPropViewModel GyroDirSwipeVM => gyroDirSwipeVM;

        public bool ShowActionSelect { get; set; } = true;
        public bool ShowActionSettings { get; set; } = true;
        public bool ShowNameSettings { get; set; } = true;

        public event EventHandler<int> ActionTypeIndexChanged;

        public GyroDirSwipePropControl()
        {
            InitializeComponent();
        }

        public void PostInit(Mapper mapper, GyroMapAction action)
        {
            gyroDirSwipeVM = new GyroDirSwipeActionPropViewModel(mapper, action);
            DataContext = gyroDirSwipeVM;

            gyroSelectControl.PostInit(mapper, action);
            gyroSelectControl.Visibility = ShowActionSelect ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            nameSettings.Visibility = ShowActionSettings && ShowNameSettings ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            activationSettings.Visibility = ShowActionSettings ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            dirSwipeSettingsHint.Visibility = ShowActionSettings ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            gyroSelectControl.GyroActSelVM.SelectedIndexChanged += GyroActSelVM_SelectedIndexChanged;
        }

        public void RefreshView()
        {
            // Force re-eval of bindings
            DataContext = null;
            DataContext = gyroDirSwipeVM;
        }

        private void GyroActSelVM_SelectedIndexChanged(object sender, EventArgs e)
        {
            ActionTypeIndexChanged?.Invoke(this,
                gyroSelectControl.GyroActSelVM.SelectedIndex);
        }
    }
}
