using DS4MapperTest.GyroActions;
using DS4MapperTest.ViewModels.GyroActionPropViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace DS4MapperTest.Views.GyroActionPropControls
{
    public partial class GyroPassthruPropControl : UserControl
    {
        public bool ShowActionSelect { get; set; } = true;
        public bool ShowActionSettings { get; set; } = true;
        public bool ShowNameSettings { get; set; } = true;
        public bool ShowActivationSettings { get; set; } = true;

        public event EventHandler<int> ActionTypeIndexChanged;

        public GyroPassthruPropControl()
        {
            InitializeComponent();
        }

        public void PostInit(Mapper mapper, GyroMapAction action)
        {
            DataContext = new GyroPassthruActionPropViewModel(mapper, action);

            gyroSelectControl.PostInit(mapper, action);
            gyroSelectControl.Visibility = ShowActionSelect ? Visibility.Visible : Visibility.Collapsed;
            activationSettings.Visibility = ShowActionSettings && ShowActivationSettings
                ? Visibility.Visible
                : Visibility.Collapsed;
            gyroSelectControl.GyroActSelVM.SelectedIndexChanged += GyroActSelVM_SelectedIndexChanged;
        }

        private void GyroActSelVM_SelectedIndexChanged(object sender, EventArgs e)
        {
            ActionTypeIndexChanged?.Invoke(this, gyroSelectControl.GyroActSelVM.SelectedIndex);
        }
    }
}
