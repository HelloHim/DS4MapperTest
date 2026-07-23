using System;
using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class FaceKeybindsControl : UserControl
    {
        public FaceKeybindsControl()
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
                "Double" => FaceBindingFuncKind.Double,
                "Distance" => FaceBindingFuncKind.Distance,
                "Chorded" => FaceBindingFuncKind.Chorded,
                "Start" => FaceBindingFuncKind.Start,
                _ => null,
            };

            if (kind == null) return;

            buttonItem.AddExtraBinding(kind.Value);
        }

        private void RemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FaceButtonFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }
    }
}
