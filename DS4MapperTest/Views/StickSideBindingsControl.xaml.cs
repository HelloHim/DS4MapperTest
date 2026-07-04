using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class StickSideBindingsControl : UserControl
    {
        public StickSideBindingsControl()
        {
            InitializeComponent();
        }

        private void ClickAddExtraBindingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ClickAddExtraBindingMenuItem_Click(object sender, RoutedEventArgs e)
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
                "Double" => FaceBindingFuncKind.Double,
                "Distance" => FaceBindingFuncKind.Distance,
                "Start" => FaceBindingFuncKind.Start,
                "Release" => FaceBindingFuncKind.Release,
                _ => null,
            };

            if (kind == null) return;

            buttonItem.AddExtraBinding(kind.Value);
        }

        private void ClickEditBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FaceButtonFuncItem item } button)
            {
                OpenOutputEditor(item, button);
            }
        }

        private void ClickRemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FaceButtonFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }

        private void OpenOutputEditor(FaceButtonFuncItem item, DependencyObject source)
        {
            EditFaceBindingContext editContext = item.Owner.PrepareEdit(item);
            if (editContext == null) return;

            ContentControl host = InlineBindingEditorService.FindInlineHost(source);
            InlineBindingEditorService.Open(host, editContext,
                $"{item.Owner.DisplayName} - {item.DisplayName}",
                item.Owner.RefreshAfterEdit);
        }

        private void ExtraAddExtraBindingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ExtraAddExtraBindingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem ||
                menuItem.Tag is not string tag ||
                menuItem.Parent is not ContextMenu menu ||
                menu.PlacementTarget is not FrameworkElement target ||
                target.DataContext is not StickExtraBindingItem bindingItem)
            {
                return;
            }

            FaceBindingFuncKind? kind = tag switch
            {
                "Hold" => FaceBindingFuncKind.Hold,
                "Double" => FaceBindingFuncKind.Double,
                "Distance" => FaceBindingFuncKind.Distance,
                "Start" => FaceBindingFuncKind.Start,
                "Release" => FaceBindingFuncKind.Release,
                _ => null,
            };

            if (kind == null) return;

            bindingItem.AddExtraBinding(kind.Value);
        }

        private void ExtraEditBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: StickExtraFuncItem item } button)
            {
                OpenOutputEditor(item, button);
            }
        }

        private void ExtraRemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: StickExtraFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }

        private void OpenOutputEditor(StickExtraFuncItem item, DependencyObject source)
        {
            EditFaceBindingContext editContext = item.Owner.PrepareEdit(item);
            if (editContext == null) return;

            ContentControl host = InlineBindingEditorService.FindInlineHost(source);
            InlineBindingEditorService.Open(host, editContext,
                $"{item.Owner.DisplayName} - {item.DisplayName}",
                item.Owner.RefreshAfterEdit);
        }
    }
}
