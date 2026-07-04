using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.GyroActions;
using DS4MapperTest.ViewModels;
using DS4MapperTest.ViewModels.GyroActionPropViewModels;

namespace DS4MapperTest.Views
{
    public partial class GyroOutputControl : UserControl
    {
        public GyroOutputControl()
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
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
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
                    "Gyro Mode is currently set to Unbound. Choose a gyro mode in the Action tab to configure gyro output settings.");
                return;
            }

            if (item.IsGyroMouseAction)
            {
                GyroMouseActionPropViewModel vm = new GyroMouseActionPropViewModel(item.Mapper, item.MappedAction);
                host.Content = BuildContent("MouseOutputTemplate", vm);
                return;
            }

            if (item.IsGyroMouseJoystickAction)
            {
                GyroMouseJoystickPropViewModel vm = new GyroMouseJoystickPropViewModel(item.Mapper, item.MappedAction);
                host.Content = BuildContent("JoystickOutputTemplate", vm);
                return;
            }

            if (item.IsGyroDirSwipeAction)
            {
                GyroDirSwipeActionPropViewModel vm = new GyroDirSwipeActionPropViewModel(item.Mapper, item.MappedAction);
                host.Content = BuildContent("DirSwipeOutputTemplate", vm);
                return;
            }

            host.Content = CreateMessage(
                $"Gyro Mode is set to {item.ActionDisplayName}. This mode has no gyro output settings in this tab.");
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

        private void BtnUpEdit_Click(object sender, RoutedEventArgs e)
        {
            EditDirButton(sender,
                (vm) => vm.Action.UsedEventsButtonsY[(int)GyroDirectionalSwipe.SwipeAxisYDir.Up],
                (vm) => !vm.Action.UseParentDataY[(int)GyroDirectionalSwipe.SwipeAxisYDir.Up],
                (vm) => vm.UpdateUpDirButton);
        }

        private void BtnDownEdit_Click(object sender, RoutedEventArgs e)
        {
            EditDirButton(sender,
                (vm) => vm.Action.UsedEventsButtonsY[(int)GyroDirectionalSwipe.SwipeAxisYDir.Down],
                (vm) => !vm.Action.UseParentDataY[(int)GyroDirectionalSwipe.SwipeAxisYDir.Down],
                (vm) => vm.UpdateDownDirButton);
        }

        private void BtnLeftEdit_Click(object sender, RoutedEventArgs e)
        {
            EditDirButton(sender,
                (vm) => vm.Action.UsedEventsButtonsX[(int)GyroDirectionalSwipe.SwipeAxisXDir.Left],
                (vm) => !vm.Action.UseParentDataX[(int)GyroDirectionalSwipe.SwipeAxisXDir.Left],
                (vm) => vm.UpdateLeftDirButton);
        }

        private void BtnRightEdit_Click(object sender, RoutedEventArgs e)
        {
            EditDirButton(sender,
                (vm) => vm.Action.UsedEventsButtonsX[(int)GyroDirectionalSwipe.SwipeAxisXDir.Right],
                (vm) => !vm.Action.UseParentDataX[(int)GyroDirectionalSwipe.SwipeAxisXDir.Right],
                (vm) => vm.UpdateRightDirButton);
        }

        private void EditDirButton(object sender,
            Func<GyroDirSwipeActionPropViewModel, ButtonAction> getCurrent,
            Func<GyroDirSwipeActionPropViewModel, bool> getRealAction,
            Func<GyroDirSwipeActionPropViewModel, Action<ButtonAction, ButtonAction>> getUpdater)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not GyroDirSwipeActionPropViewModel vm)
            {
                return;
            }

            ContentControl host = FindAncestor<ContentControl>(element);
            if (host == null || host.DataContext is not GyroBindingItemsTest item)
            {
                return;
            }

            FrameworkElement baseContent = (FrameworkElement)host.Content;
            ShowFuncEditor(host, baseContent, item, getCurrent(vm), getRealAction(vm), getUpdater(vm));
        }

        private void ShowFuncEditor(ContentControl host, FrameworkElement baseContent, GyroBindingItemsTest item,
            ButtonAction action, bool realAction,
            Action<ButtonAction, ButtonAction> updateHandler)
        {
            FuncBindingControl funcControl = new FuncBindingControl();
            funcControl.PostInit(item.Mapper, action);
            funcControl.FuncBindVM.IsRealAction = realAction;
            funcControl.PreActionSwitch += (oldAction, newAction) => updateHandler?.Invoke(oldAction, newAction);
            funcControl.ActionChanged += (s, newAction) => updateHandler?.Invoke(null, newAction);
            funcControl.RequestBindingEditor += (s, func) => ShowOutputEditor(host, funcControl, item, action, func);
            funcControl.RequestClose += (s, args) =>
            {
                // Force a rebind of the one-way button-display strings without
                // constructing a new prop view model (avoids re-triggering the
                // composite-layer "soft copy" detection on every button edit).
                object vm = baseContent.DataContext;
                baseContent.DataContext = null;
                baseContent.DataContext = vm;
                host.Content = baseContent;
            };

            host.Content = funcControl;
        }

        private void ShowOutputEditor(ContentControl host, FuncBindingControl funcControl, GyroBindingItemsTest item,
            ButtonAction action, ActionFunc func)
        {
            OutputBindingEditorControl outputControl = new OutputBindingEditorControl();
            outputControl.PostInit(item.Mapper, action, func);
            outputControl.Finished += (s, args) =>
            {
                funcControl.RefreshView();
                host.Content = funcControl;
            };

            host.Content = outputControl;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
