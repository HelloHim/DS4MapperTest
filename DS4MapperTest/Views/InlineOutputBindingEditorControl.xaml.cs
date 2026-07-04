using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.Views
{
    public partial class InlineOutputBindingEditorControl : UserControl
    {
        private Mapper mapper;
        private ButtonAction action;
        private ActionFunc func;
        private List<OutputActionData> snapshot;
        private bool snapshotHadFunctionsChange;
        private bool profileWasDirty;

        public event EventHandler Applied;
        public event EventHandler Cancelled;

        public InlineOutputBindingEditorControl()
        {
            InitializeComponent();
        }

        public void PostInit(Mapper mapper, ButtonAction action, ActionFunc func, string title)
        {
            this.mapper = mapper;
            this.action = action;
            this.func = func;
            snapshot = CloneOutputs(func.OutputActions);
            snapshotHadFunctionsChange =
                action.ChangedProperties.Contains(ButtonAction.PropertyKeyStrings.FUNCTIONS);
            profileWasDirty = mapper?.ActionProfile?.Dirty == true;

            TitleText.Text = title;
            Editor.PostInit(mapper, action, func);
            Focus();
        }

        public void CancelEdit()
        {
            RestoreSnapshot();
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Applied?.Invoke(this, EventArgs.Empty);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelEdit();
        }

        private void UnbindButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.AssignUnboundSelectedSlot();
        }

        private void RestoreSnapshot()
        {
            if (mapper == null || action == null || func == null || snapshot == null) return;

            using (mapper.SuppressProfileDirtyTracking())
            {
                mapper.ProcessMappingChangeAction(() =>
                {
                    action.Release(mapper, ignoreReleaseActions: true);
                    func.OutputActions.Clear();
                    foreach (OutputActionData data in CloneOutputs(snapshot))
                    {
                        func.OutputActions.Add(data);
                    }

                    if (snapshotHadFunctionsChange)
                    {
                        if (!action.ChangedProperties.Contains(ButtonAction.PropertyKeyStrings.FUNCTIONS))
                        {
                            action.ChangedProperties.Add(ButtonAction.PropertyKeyStrings.FUNCTIONS);
                        }
                    }
                    else
                    {
                        action.ChangedProperties.Remove(ButtonAction.PropertyKeyStrings.FUNCTIONS);
                    }
                });

                if (mapper.ActionProfile != null)
                {
                    mapper.ActionProfile.Dirty = profileWasDirty;
                }
            }
        }

        private static List<OutputActionData> CloneOutputs(IEnumerable<OutputActionData> source)
        {
            return source.Select(CloneOutput).ToList();
        }

        private static OutputActionData CloneOutput(OutputActionData source)
        {
            OutputActionData clone = new OutputActionData(source)
            {
                AxisCode = source.AxisCode,
                checkTick = source.checkTick,
                mouseDir = source.mouseDir,
                useNotches = source.useNotches,
                currentNotches = source.currentNotches,
                breakSequence = source.breakSequence,
                skipRelease = source.skipRelease,
                waitForRelease = source.waitForRelease,
                processOutput = source.processOutput,
                extraSettings = new OutputActionDataBindSettings
                {
                    wheelXTicks = source.extraSettings.wheelXTicks,
                    wheelYTicks = source.extraSettings.wheelYTicks,
                    mouseXSpeed = source.extraSettings.mouseXSpeed,
                    mouseYSpeed = source.extraSettings.mouseYSpeed,
                },
            };

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
