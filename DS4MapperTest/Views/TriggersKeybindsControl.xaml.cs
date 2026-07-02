using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class TriggersKeybindsControl : UserControl
    {
        public TriggersKeybindsControl()
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
                target.DataContext is not TriggerKeybindItem triggerItem)
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

            TriggerButtonFuncItem newItem = triggerItem.AddExtraBinding(kind.Value);
            if (newItem != null)
            {
                OpenOutputEditor(newItem);
            }
        }

        private void EditBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TriggerButtonFuncItem item })
            {
                OpenOutputEditor(item);
            }
        }

        private void RemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TriggerButtonFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }

        private void EditFullPull_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TriggerKeybindItem item })
            {
                OpenButtonActionEditor(item, item.PrepareFullPullEdit(), $"{item.DisplayName} - Full Pull");
            }
        }

        private void EditSoftPull_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TriggerKeybindItem item })
            {
                OpenButtonActionEditor(item, item.PrepareSoftPullEdit(), $"{item.DisplayName} - Soft Pull");
            }
        }

        private void OpenOutputEditor(TriggerButtonFuncItem item)
        {
            EditTriggerButtonBindingContext editContext = item.Owner.PrepareEdit(item);
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
            host.Closed += (_, _) => item.Owner.RefreshAfterEdit();

            host.ShowDialog();
        }

        private void OpenButtonActionEditor(TriggerKeybindItem ownerItem,
            TriggerButtonEditContext editContext, string title)
        {
            if (editContext == null) return;

            FuncBindingControl funcControl = new FuncBindingControl();
            Window host = new Window
            {
                Title = title,
                Owner = Window.GetWindow(this),
                Content = funcControl,
                Width = 820,
                Height = 540,
                MinWidth = 760,
                MinHeight = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = TryFindResource("JsmccBg0Brush") as System.Windows.Media.Brush,
            };

            funcControl.PostInit(ownerItem.Owner.DeviceMapper, editContext.Action);
            funcControl.FuncBindVM.IsRealAction = editContext.IsRealAction;
            funcControl.PreActionSwitch += (oldAction, newAction) =>
            {
                editContext.UpdateAction?.Invoke(oldAction, newAction);
            };
            funcControl.ActionChanged += (_, action) =>
            {
                editContext.UpdateAction?.Invoke(null, action);
            };
            funcControl.RequestBindingEditor += (_, func) =>
            {
                OpenNestedOutputEditor(host, funcControl, ownerItem, editContext.Action, func);
            };
            funcControl.RequestClose += (_, _) => host.Close();
            host.Closed += (_, _) => ownerItem.RefreshAfterEdit();

            host.ShowDialog();
        }

        private void OpenNestedOutputEditor(Window host, FuncBindingControl funcControl,
            TriggerKeybindItem ownerItem, ButtonAction action, ActionFunc func)
        {
            OutputBindingEditorControl editor = new OutputBindingEditorControl();
            editor.PostInit(ownerItem.Owner.DeviceMapper, action, func);
            editor.Finished += (_, _) =>
            {
                funcControl.RefreshView();
                host.Content = funcControl;
            };

            host.Content = editor;
        }
    }
}
