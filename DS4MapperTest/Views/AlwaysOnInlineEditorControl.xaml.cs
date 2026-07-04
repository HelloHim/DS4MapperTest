using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.ViewModels;
using DS4MapperTest.Views.ButtonActionPropControls;

namespace DS4MapperTest.Views
{
    public partial class AlwaysOnInlineEditorControl : UserControl
    {
        private ProfileEditorTestViewModel profileVm;
        private AlwaysOnBindingItem item;
        private ButtonMapAction snapshotAction;
        private ButtonMapAction currentAction;
        private AlwaysOnButtonFuncEditViewModel editVm;
        private ButtonActionViewModel buttonActionVm;
        private ButtonNoActionViewModel noActionVm;
        private FuncBindingControl funcBindingControl;
        private readonly ButtonNoActionPropControl noActionControl =
            new ButtonNoActionPropControl();
        private bool suppressTransformChange;
        private bool finished;

        public event EventHandler Applied;
        public event EventHandler Cancelled;

        public AlwaysOnInlineEditorControl()
        {
            InitializeComponent();
        }

        public void PostInit(ProfileEditorTestViewModel profileVm,
            AlwaysOnBindingItem item)
        {
            this.profileVm = profileVm;
            this.item = item;
            currentAction = item.MappedAction;
            snapshotAction = item.RestoreActionOnCancel != null
                ? CloneAction(item.RestoreActionOnCancel)
                : CloneAction(currentAction);

            TitleText.Text = item.DisplayName;
            InitialiseEditVm(currentAction);
            SetupDisplayControl();
            Focus();
        }

        public void CancelEdit()
        {
            if (finished) return;
            finished = true;

            RestoreSnapshot();
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (finished) return;
            finished = true;
            item.RestoreActionOnCancel = null;

            Applied?.Invoke(this, EventArgs.Empty);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelEdit();
        }

        private void UnbindButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchToAction(new ButtonNoAction(), copyProps: false);
        }

        private void TransformCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (suppressTransformChange || editVm == null) return;

            int index = TransformCombo.SelectedIndex;
            ButtonMapAction newAction = editVm.PrepareNewAction(index);
            if (newAction == null) return;

            SwitchToAction(newAction);
        }

        private void InitialiseEditVm(ButtonMapAction action)
        {
            editVm = new AlwaysOnButtonFuncEditViewModel(
                profileVm.DeviceMapper, action);
            currentAction = action;

            suppressTransformChange = true;
            TransformCombo.SelectedIndex = action is ButtonAction ? 1 : 0;
            suppressTransformChange = false;
        }

        private void SetupDisplayControl()
        {
            switch (currentAction)
            {
                case ButtonAction buttonAction:
                    buttonActionVm = new ButtonActionViewModel(
                        profileVm.DeviceMapper, buttonAction);

                    funcBindingControl = new FuncBindingControl();
                    funcBindingControl.PostInit(profileVm.DeviceMapper, buttonAction);
                    funcBindingControl.RequestBindingEditor +=
                        FuncBindingControl_RequestBindingEditor;
                    funcBindingControl.PreActionSwitch +=
                        FuncBindingControl_PreActionSwitch;
                    funcBindingControl.ActionChanged +=
                        FuncBindingControl_ActionChanged;
                    funcBindingControl.RequestClose +=
                        FuncBindingControl_RequestClose;
                    funcBindingControl.FuncBindVM.IsRealAction =
                        buttonActionVm.Action.ParentAction == null;

                    EditorHost.Content = funcBindingControl;
                    break;
                case ButtonNoAction:
                    noActionVm = new ButtonNoActionViewModel(
                        profileVm.DeviceMapper, currentAction);
                    noActionVm.DisplayControl = noActionControl;
                    EditorHost.Content = noActionControl;
                    break;
                default:
                    EditorHost.Content = null;
                    break;
            }
        }

        private void SwitchToAction(ButtonMapAction newAction,
            bool copyProps = true)
        {
            if (newAction == null || currentAction == null) return;

            if (newAction.Id == MapAction.DEFAULT_UNBOUND_ID)
            {
                editVm.MigrationActionId(newAction);
            }

            newAction.MappingId = currentAction.MappingId;
            profileVm.ReplaceAlwaysOnAction(currentAction, newAction, copyProps);
            InitialiseEditVm(newAction);
            SetupDisplayControl();
        }

        private void FuncBindingControl_PreActionSwitch(ButtonAction oldAction,
            ButtonAction newAction)
        {
            profileVm.ReplaceAlwaysOnAction(oldAction, newAction, copyProps: false);
            editVm.MigrationActionId(newAction);
            editVm.UpdateAction(newAction);
            currentAction = newAction;
        }

        private void FuncBindingControl_ActionChanged(object sender,
            ButtonAction action)
        {
            editVm.MigrationActionId(action);
            editVm.UpdateAction(action);
            currentAction = action;
        }

        private void FuncBindingControl_RequestClose(object sender, EventArgs e)
        {
            ApplyButton_Click(this, new RoutedEventArgs());
        }

        private void FuncBindingControl_RequestBindingEditor(object sender,
            ActionFunc func)
        {
            OutputBindingEditorControl outputEditor = new OutputBindingEditorControl();
            outputEditor.PostInit(profileVm.DeviceMapper,
                currentAction as ButtonAction, func);
            outputEditor.Finished += (_, _) =>
            {
                funcBindingControl.RefreshView();
                EditorHost.Content = funcBindingControl;
            };

            EditorHost.Content = outputEditor;
        }

        private void RestoreSnapshot()
        {
            if (snapshotAction == null || currentAction == null) return;

            profileVm.ReplaceAlwaysOnAction(currentAction, CloneAction(snapshotAction),
                copyProps: false);
        }

        private static ButtonMapAction CloneAction(ButtonMapAction source)
        {
            ButtonMapAction clone = source switch
            {
                ButtonAction buttonAction => CloneButtonAction(buttonAction),
                ButtonNoAction => new ButtonNoAction(),
                _ => source?.DuplicateAction(),
            };

            if (clone != null)
            {
                clone.CopyBaseProps(source);
                clone.Id = source.Id;
                clone.MappingId = source.MappingId;
            }

            return clone;
        }

        private static ButtonAction CloneButtonAction(ButtonAction source)
        {
            ButtonAction clone = new ButtonAction();
            clone.CopyBaseProps(source);
            clone.CopyAction(source);
            clone.Id = source.Id;
            clone.MappingId = source.MappingId;
            return clone;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }
}
