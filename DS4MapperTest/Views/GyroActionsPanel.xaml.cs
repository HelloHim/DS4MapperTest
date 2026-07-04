using System;
using System.Windows;
using System.Windows.Controls;
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
    }
}
