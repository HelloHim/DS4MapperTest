using System;
using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.GyroActions;
using DS4MapperTest.ViewModels;
using DS4MapperTest.Views.GyroActionPropControls;

namespace DS4MapperTest.Views
{
    /// <summary>
    /// Interaction logic for GyroActionsPanel.xaml
    /// </summary>
    public partial class GyroActionsPanel : UserControl
    {
        public GyroActionsPanel()
        {
            InitializeComponent();
        }

        private void GyroBindingHost_Loaded(object sender, RoutedEventArgs e)
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
        }

        private void RenderInlineHost(ContentControl host, GyroBindingItemsTest item)
        {
            host.Content = CreateActionControl(host, item);
        }

        private FrameworkElement CreateActionControl(ContentControl host, GyroBindingItemsTest item)
        {
            switch (item.MappedAction)
            {
                case GyroMouse:
                    {
                        GyroMousePropControl propControl = new GyroMousePropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        return propControl;
                    }
                case GyroMouseJoystick:
                    {
                        GyroMouseJoystickPropControl propControl = new GyroMouseJoystickPropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        return propControl;
                    }
                case GyroDirectionalSwipe:
                    {
                        GyroDirSwipePropControl propControl = new GyroDirSwipePropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        propControl.RequestFuncEditor += (s, args) =>
                            ShowFuncEditor(host, propControl, args.DirBtn, args.RealAction,
                                (oldAction, newAction) => args.UpdateActHandler?.Invoke(oldAction, newAction),
                                propControl.RefreshView);
                        return propControl;
                    }
                case GyroNoMapAction:
                default:
                    {
                        GyroNoActionControl propControl = new GyroNoActionControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        return propControl;
                    }
            }
        }

        private void HandleActionTypeChanged(ContentControl host, GyroBindingItemsTest item, int ind)
        {
            GyroBindEditViewModel editVM = new GyroBindEditViewModel(item.Mapper, item.MappedAction);
            GyroMapAction newAction = editVM.PrepareNewAction(ind);
            if (newAction == null)
            {
                return;
            }

            newAction.CopyBaseMapProps(item.MappedAction);
            editVM.MigrateActionId(newAction);
            editVM.SwitchAction(newAction);
            item.UpdateAction(newAction);

            RenderInlineHost(host, item);
        }

        private void ShowFuncEditor(ContentControl host, UserControl baseControl, ButtonAction action,
            bool realAction, Action<ButtonAction, ButtonAction> updateHandler, Action refreshBaseControl)
        {
            FuncBindingControl funcControl = new FuncBindingControl();
            funcControl.PostInit((host.DataContext as GyroBindingItemsTest).Mapper, action);
            funcControl.FuncBindVM.IsRealAction = realAction;
            funcControl.PreActionSwitch += (oldAction, newAction) => updateHandler?.Invoke(oldAction, newAction);
            funcControl.ActionChanged += (sender, newAction) => updateHandler?.Invoke(null, newAction);
            funcControl.RequestBindingEditor += (sender, func) => ShowOutputEditor(host, funcControl, action, func);
            funcControl.RequestClose += (sender, args) =>
            {
                refreshBaseControl?.Invoke();
                host.Content = baseControl;
            };

            host.Content = funcControl;
        }

        private void ShowOutputEditor(ContentControl host, FuncBindingControl funcControl,
            ButtonAction action, ActionFunc func)
        {
            OutputBindingEditorControl outputControl = new OutputBindingEditorControl();
            outputControl.PostInit((host.DataContext as GyroBindingItemsTest).Mapper, action, func);
            outputControl.Finished += (sender, args) =>
            {
                funcControl.RefreshView();
                host.Content = funcControl;
            };

            host.Content = outputControl;
        }
    }
}
