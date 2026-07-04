using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class AlwaysOnKeybindsControl : UserControl
    {
        private ContentControl activeHost;
        private AlwaysOnInlineEditorControl activeEditor;

        public AlwaysOnKeybindsControl()
        {
            InitializeComponent();
        }

        private ProfileEditorTestViewModel ProfileVm =>
            DataContext as ProfileEditorTestViewModel;

        private void AddAlwaysOnButton_Click(object sender, RoutedEventArgs e)
        {
            AlwaysOnBindingItem item = ProfileVm?.AddAlwaysOnBinding();
            if (item == null) return;

            Dispatcher.BeginInvoke(() =>
            {
                AlwaysOnItems.UpdateLayout();
                ContentControl host = FindInlineHostForItem(item);
                OpenEditor(item, host);
            });
        }

        private void EditAlwaysOnButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: AlwaysOnBindingItem item } button)
            {
                OpenEditor(item, InlineBindingEditorService.FindInlineHost(button));
            }
        }

        private void RemoveAlwaysOnButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: AlwaysOnBindingItem item })
            {
                CloseEditor(cancel: true);
                ProfileVm?.RemoveAlwaysOnBinding(item);
            }
        }

        private void OpenEditor(AlwaysOnBindingItem item, ContentControl host)
        {
            if (item == null || host == null || ProfileVm == null) return;

            if (activeHost == host)
            {
                CloseEditor(cancel: true);
                return;
            }

            CloseEditor(cancel: true);

            AlwaysOnInlineEditorControl editor = new AlwaysOnInlineEditorControl();
            editor.PostInit(ProfileVm, item);
            editor.Applied += (_, _) =>
            {
                host.Content = null;
                host.Visibility = Visibility.Collapsed;
                activeHost = null;
                activeEditor = null;
                ProfileVm.RefreshLayerBindings();
            };
            editor.Cancelled += (_, _) =>
            {
                host.Content = null;
                host.Visibility = Visibility.Collapsed;
                activeHost = null;
                activeEditor = null;
                ProfileVm.RefreshLayerBindings();
            };

            host.Content = editor;
            host.Visibility = Visibility.Visible;
            activeHost = host;
            activeEditor = editor;
        }

        private void CloseEditor(bool cancel)
        {
            AlwaysOnInlineEditorControl editor = activeEditor;
            ContentControl host = activeHost;
            activeEditor = null;
            activeHost = null;

            if (editor != null && cancel)
            {
                editor.CancelEdit();
            }

            if (host != null)
            {
                host.Content = null;
                host.Visibility = Visibility.Collapsed;
            }
        }

        private ContentControl FindInlineHostForItem(AlwaysOnBindingItem item)
        {
            DependencyObject container =
                AlwaysOnItems.ItemContainerGenerator.ContainerFromItem(item);
            return FindVisualChild<ContentControl>(container, "InlineEditorHost");
        }

        private static T FindVisualChild<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }

                T result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }

            return null;
        }
    }
}
