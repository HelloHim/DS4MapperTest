using System;
using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class DPadKeybindsControl : UserControl
    {
        public DPadKeybindsControl()
        {
            InitializeComponent();
        }

        private void ToggleAdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProfileEditorTestViewModel profileVm)
            {
                profileVm.DPadKeybinds.IsAdvancedExpanded = !profileVm.DPadKeybinds.IsAdvancedExpanded;
            }
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
                target.DataContext is not DPadDirectionBindingItem directionItem)
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

            directionItem.AddExtraBinding(kind.Value);
        }

        private void EditBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: DPadDirectionFuncItem item } button)
            {
                OpenOutputEditor(item, button);
            }
        }

        private void RemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: DPadDirectionFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }

        private void OpenOutputEditor(DPadDirectionFuncItem item, DependencyObject source)
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
