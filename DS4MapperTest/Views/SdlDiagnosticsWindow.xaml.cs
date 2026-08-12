using System;
using System.Windows;
using System.Windows.Threading;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class SdlDiagnosticsWindow : Window
    {
        private readonly SdlDiagnosticsViewModel viewModel;
        private readonly DispatcherTimer refreshTimer;

        public SdlDiagnosticsWindow(AppGlobalData appGlobal)
        {
            InitializeComponent();

            viewModel = new SdlDiagnosticsViewModel(appGlobal);
            DataContext = viewModel;

            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50),
            };
            refreshTimer.Tick += RefreshTimer_Tick;

            Loaded += SdlDiagnosticsWindow_Loaded;
            Closed += SdlDiagnosticsWindow_Closed;
        }

        private void SdlDiagnosticsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (viewModel.Start(out string error))
            {
                refreshTimer.Start();
            }
            else
            {
                MessageBox.Show(error, "SDL3 Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e) => viewModel.Refresh();

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => viewModel.Refresh();

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = viewModel.CaptureSelectedDevice();
                MessageBox.Show($"SDL diagnostic report saved:\n{path}", "SDL3 Diagnostics",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save SDL diagnostic report:\n{ex.Message}",
                    "SDL3 Diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SdlDiagnosticsWindow_Closed(object sender, EventArgs e)
        {
            refreshTimer.Stop();
            refreshTimer.Tick -= RefreshTimer_Tick;
            viewModel.Dispose();
            DataContext = null;
        }
    }
}
