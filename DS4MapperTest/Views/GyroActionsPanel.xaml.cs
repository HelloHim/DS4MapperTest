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
        public static readonly DependencyProperty ShowActionSelectProperty =
            DependencyProperty.Register(
                nameof(ShowActionSelect),
                typeof(bool),
                typeof(GyroActionsPanel),
                new PropertyMetadata(true));

        public bool ShowActionSelect
        {
            get => (bool)GetValue(ShowActionSelectProperty);
            set => SetValue(ShowActionSelectProperty, value);
        }

        public static readonly DependencyProperty ShowActionSettingsProperty =
            DependencyProperty.Register(
                nameof(ShowActionSettings),
                typeof(bool),
                typeof(GyroActionsPanel),
                new PropertyMetadata(true));

        public bool ShowActionSettings
        {
            get => (bool)GetValue(ShowActionSettingsProperty);
            set => SetValue(ShowActionSettingsProperty, value);
        }

        public static readonly DependencyProperty ShowNameSettingsProperty =
            DependencyProperty.Register(
                nameof(ShowNameSettings),
                typeof(bool),
                typeof(GyroActionsPanel),
                new PropertyMetadata(true));

        public bool ShowNameSettings
        {
            get => (bool)GetValue(ShowNameSettingsProperty);
            set => SetValue(ShowNameSettingsProperty, value);
        }

        public static readonly DependencyProperty ShowActivationSettingsProperty =
            DependencyProperty.Register(
                nameof(ShowActivationSettings),
                typeof(bool),
                typeof(GyroActionsPanel),
                new PropertyMetadata(true));

        public bool ShowActivationSettings
        {
            get => (bool)GetValue(ShowActivationSettingsProperty);
            set => SetValue(ShowActivationSettingsProperty, value);
        }

        public static readonly DependencyProperty ShowPanelHeaderProperty =
            DependencyProperty.Register(
                nameof(ShowPanelHeader),
                typeof(bool),
                typeof(GyroActionsPanel),
                new PropertyMetadata(true));

        public bool ShowPanelHeader
        {
            get => (bool)GetValue(ShowPanelHeaderProperty);
            set => SetValue(ShowPanelHeaderProperty, value);
        }

        public static readonly DependencyProperty ShowBindingHeaderProperty =
            DependencyProperty.Register(
                nameof(ShowBindingHeader),
                typeof(bool),
                typeof(GyroActionsPanel),
                new PropertyMetadata(true));

        public bool ShowBindingHeader
        {
            get => (bool)GetValue(ShowBindingHeaderProperty);
            set => SetValue(ShowBindingHeaderProperty, value);
        }

        public static readonly DependencyProperty PanelTitleProperty =
            DependencyProperty.Register(
                nameof(PanelTitle),
                typeof(string),
                typeof(GyroActionsPanel),
                new PropertyMetadata("Gyro Action"));

        public string PanelTitle
        {
            get => (string)GetValue(PanelTitleProperty);
            set => SetValue(PanelTitleProperty, value);
        }

        public static readonly DependencyProperty PanelDescriptionProperty =
            DependencyProperty.Register(
                nameof(PanelDescription),
                typeof(string),
                typeof(GyroActionsPanel),
                new PropertyMetadata(
                    "Choose the gyro mode, name the binding, and decide which buttons arm it. Output direction, sensitivity, and noise steadying are tuned in Gyro Behaviour, Sensitivity, and Noise & Steadying."));

        public string PanelDescription
        {
            get => (string)GetValue(PanelDescriptionProperty);
            set => SetValue(PanelDescriptionProperty, value);
        }

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
                        propControl.ShowActionSelect = ShowActionSelect;
                        propControl.ShowActionSettings = ShowActionSettings;
                        propControl.ShowNameSettings = ShowNameSettings;
                        propControl.ShowActivationSettings = ShowActivationSettings;
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        return propControl;
                    }
                case GyroMouseJoystick:
                    {
                        GyroMouseJoystickPropControl propControl = new GyroMouseJoystickPropControl();
                        propControl.ShowActionSelect = ShowActionSelect;
                        propControl.ShowActionSettings = ShowActionSettings;
                        propControl.ShowNameSettings = ShowNameSettings;
                        propControl.ShowActivationSettings = ShowActivationSettings;
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        return propControl;
                    }
                case GyroDirectionalSwipe:
                    {
                        GyroDirSwipePropControl propControl = new GyroDirSwipePropControl();
                        propControl.ShowActionSelect = ShowActionSelect;
                        propControl.ShowActionSettings = ShowActionSettings;
                        propControl.ShowNameSettings = ShowNameSettings;
                        propControl.ShowActivationSettings = ShowActivationSettings;
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        return propControl;
                    }
                case GyroPassthruAction:
                    {
                        GyroPassthruPropControl propControl = new GyroPassthruPropControl();
                        propControl.ShowActionSelect = ShowActionSelect;
                        propControl.ShowActionSettings = ShowActionSettings;
                        propControl.ShowNameSettings = ShowNameSettings;
                        propControl.ShowActivationSettings = ShowActivationSettings;
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.ActionTypeIndexChanged += (s, ind) => HandleActionTypeChanged(host, item, ind);
                        return propControl;
                    }
                case GyroNoMapAction:
                default:
                    {
                        GyroNoActionControl propControl = new GyroNoActionControl();
                        propControl.ShowActionSelect = ShowActionSelect;
                        propControl.ShowActionSettings = ShowActionSettings;
                        propControl.ShowNameSettings = ShowNameSettings;
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
