using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DS4MapperTest.Views
{
    public partial class ThemeToggleControl : UserControl
    {
        public ThemeToggleControl()
        {
            InitializeComponent();

            Loaded += ThemeToggleControl_Loaded;
            Unloaded += ThemeToggleControl_Unloaded;
        }

        private void ThemeToggleControl_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeService.ThemeChanged += ThemeService_ThemeChanged;
            RefreshVisualState(ThemeService.CurrentTheme);
        }

        private void ThemeToggleControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeService.ThemeChanged -= ThemeService_ThemeChanged;
        }

        private void ThemeService_ThemeChanged(object sender, ThemeMode mode)
        {
            Dispatcher.Invoke(() => RefreshVisualState(mode));
        }

        private void Chrome_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ThemeService.ToggleTheme(AppGlobalDataSingleton.Instance);
        }

        private void Chrome_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                ThemeService.ToggleTheme(AppGlobalDataSingleton.Instance);
            }
        }

        private void RefreshVisualState(ThemeMode mode)
        {
            bool isLight = mode == ThemeMode.Light;

            IconText.Text = isLight ? "☀" : "☾";

            TrackBorder.Background = isLight
                ? (Brush)FindResource("JsmccToggleOnTrackBrush")
                : (Brush)FindResource("JsmccBgButtonHoverBrush");
            TrackBorder.BorderBrush = isLight
                ? (Brush)FindResource("JsmccAccentBrush")
                : (Brush)FindResource("JsmccBorderBrush");

            SwitchThumb.HorizontalAlignment = isLight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            SwitchThumb.Margin = isLight ? new Thickness(0, 0, 2, 0) : new Thickness(2, 0, 0, 0);
            SwitchThumb.Stroke = isLight
                ? (Brush)FindResource("JsmccAccentBrush")
                : (Brush)FindResource("JsmccBorderBrush");

            Chrome.ToolTip = isLight ? "Switch to dark mode" : "Switch to light mode";
        }
    }
}
