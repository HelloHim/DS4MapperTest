using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
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

            triggerItem.AddExtraBinding(kind.Value);
        }

        private void EditBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TriggerButtonFuncItem item } button)
            {
                OpenOutputEditor(item, button);
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
            if (sender is Button { Tag: TriggerKeybindItem item } button)
            {
                OpenTriggerPullOutputEditor(item, item.PrepareFullPullEdit(),
                    $"{item.DisplayName} - Full Pull", button);
            }
        }

        private void EditSoftPull_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TriggerKeybindItem item } button)
            {
                OpenTriggerPullOutputEditor(item, item.PrepareSoftPullEdit(),
                    $"{item.DisplayName} - Soft Pull", button);
            }
        }

        private void OpenOutputEditor(TriggerButtonFuncItem item, DependencyObject source)
        {
            EditTriggerButtonBindingContext editContext = item.Owner.PrepareEdit(item);
            if (editContext == null) return;

            ContentControl host = InlineBindingEditorService.FindInlineHost(source);
            InlineBindingEditorService.Open(host,
                new EditFaceBindingContext(editContext.Mapper, editContext.Action, editContext.Func),
                $"{item.Owner.DisplayName} - {item.DisplayName}",
                item.Owner.RefreshAfterEdit);
        }

        private void OpenTriggerPullOutputEditor(TriggerKeybindItem ownerItem,
            TriggerButtonEditContext editContext, string title, DependencyObject source)
        {
            if (editContext?.Action == null) return;

            ActionFunc func = EnsureNormalPressFunc(ownerItem, editContext.Action);
            ContentControl host = InlineBindingEditorService.FindInlineHost(source);
            InlineBindingEditorService.Open(host,
                new EditFaceBindingContext(ownerItem.Owner.DeviceMapper, editContext.Action, func),
                title,
                ownerItem.RefreshAfterEdit);
        }

        private static ActionFunc EnsureNormalPressFunc(TriggerKeybindItem ownerItem, ButtonAction action)
        {
            ActionFunc func = action.ActionFuncs.Find(temp => temp is NormalPressFunc);
            if (func != null) return func;

            func = new NormalPressFunc(new OutputActionData(OutputActionData.ActionType.Empty, 0));
            ownerItem.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                action.Release(ownerItem.Owner.DeviceMapper, ignoreReleaseActions: true);
                action.ActionFuncs.Insert(0, func);
                FaceButtonBindingItem.MarkFunctionsChanged(action);
            });

            return func;
        }
    }
}
