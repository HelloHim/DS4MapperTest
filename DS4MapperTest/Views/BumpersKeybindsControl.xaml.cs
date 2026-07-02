using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class BumpersKeybindsControl : UserControl
    {
        public BumpersKeybindsControl()
        {
            InitializeComponent();
        }

        private void AddExtraBindingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void AddExtraBindingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem ||
                menuItem.Tag is not string tag ||
                menuItem.Parent is not ContextMenu menu ||
                menu.PlacementTarget is not FrameworkElement target ||
                target.DataContext is not FaceButtonBindingItem buttonItem)
            {
                return;
            }

            FaceBindingFuncKind? kind = tag switch
            {
                "Hold" => FaceBindingFuncKind.Hold,
                "Start" => FaceBindingFuncKind.Start,
                "Release" => FaceBindingFuncKind.Release,
                _ => null,
            };

            if (kind == null) return;

            FaceButtonFuncItem newItem = buttonItem.AddExtraBinding(kind.Value);
            if (newItem != null)
            {
                OpenOutputEditor(newItem);
            }
        }

        private void EditBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FaceButtonFuncItem item })
            {
                OpenOutputEditor(item);
            }
        }

        private void RemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FaceButtonFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }

        private void OpenOutputEditor(FaceButtonFuncItem item)
        {
            EditFaceBindingContext editContext = item.Owner.PrepareEdit(item);
            if (editContext == null) return;

            OutputBindingEditorControl editor = new OutputBindingEditorControl();
            Window host = new Window
            {
                Title = $"{item.Owner.DisplayName} - {item.DisplayName}",
                Owner = Window.GetWindow(this),
                Content = editor,
                Width = 820,
                Height = 540,
                MinWidth = 760,
                MinHeight = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = TryFindResource("JsmccBg0Brush") as System.Windows.Media.Brush,
            };

            editor.PostInit(editContext.Mapper, editContext.Action, editContext.Func);
            editor.Finished += (_, _) => host.Close();
            host.Closed += (_, _) =>
            {
                item.Owner.RefreshAfterEdit();
            };

            host.ShowDialog();
        }
    }
}
