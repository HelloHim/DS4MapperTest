using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ViewModels;
using DS4MapperTest.ViewModels.GyroActionPropViewModels;

namespace DS4MapperTest.Views
{
    public partial class GyroNoiseSteadyingControl : UserControl
    {
        public GyroNoiseSteadyingControl()
        {
            InitializeComponent();
        }

        private void InlineSettingsHost_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ContentControl host ||
                host.DataContext is not GyroBindingItemsTest item)
            {
                return;
            }

            if (DataContext is ProfileEditorTestViewModel owner)
            {
                owner.PopulateMapperEditActionRefs(item.Mapper);
            }

            RenderInlineHost(host, item);

            item.PropertyChanged -= Item_PropertyChanged;
            item.PropertyChanged += Item_PropertyChanged;
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(GyroBindingItemsTest.MappedAction))
            {
                return;
            }

            if (sender is not GyroBindingItemsTest item)
            {
                return;
            }

            RefreshInlineHosts(this, item);
        }

        private void RefreshInlineHosts(DependencyObject root, GyroBindingItemsTest item)
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is ContentControl host && host.DataContext == item)
                {
                    RenderInlineHost(host, item);
                }

                RefreshInlineHosts(child, item);
            }
        }

        private void RenderInlineHost(ContentControl host, GyroBindingItemsTest item)
        {
            if (item.IsUnbound)
            {
                host.Content = CreateMessage(
                    "Gyro Mode is currently set to Unbound. Choose a gyro mode in the Action tab to configure noise and steadying settings.");
                return;
            }

            if (item.IsGyroMouseAction)
            {
                GyroMouseActionPropViewModel vm = new GyroMouseActionPropViewModel(item.Mapper, item.MappedAction);
                host.Content = BuildContent("MouseNoiseTemplate", vm);
                return;
            }

            if (item.IsGyroMouseJoystickAction)
            {
                GyroMouseJoystickPropViewModel vm = new GyroMouseJoystickPropViewModel(item.Mapper, item.MappedAction);
                host.Content = BuildContent("JoystickNoiseTemplate", vm);
                return;
            }

            if (item.IsGyroDirSwipeAction)
            {
                GyroDirSwipeActionPropViewModel vm = new GyroDirSwipeActionPropViewModel(item.Mapper, item.MappedAction);
                host.Content = BuildContent("DirSwipeNoiseTemplate", vm);
                return;
            }

            host.Content = CreateMessage(
                $"Gyro Mode is set to {item.ActionDisplayName}. This mode has no noise and steadying settings in this tab.");
        }

        private FrameworkElement BuildContent(string templateKey, object vm)
        {
            FrameworkElement content = (FrameworkElement)((DataTemplate)FindResource(templateKey)).LoadContent();
            content.DataContext = vm;
            return content;
        }

        private TextBlock CreateMessage(string message)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
            };

            if (TryFindResource("JsmccBodyText") is Style style)
            {
                textBlock.Style = style;
            }

            return textBlock;
        }
    }
}
