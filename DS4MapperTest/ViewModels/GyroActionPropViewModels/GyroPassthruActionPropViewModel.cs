using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DS4MapperTest.ViewModels.GyroActionPropViewModels
{
    public class GyroPassthruActionPropViewModel : GyroActionPropVMBase, INotifyPropertyChanged
    {
        private GyroPassthruAction action;
        private readonly List<GyroTriggerButtonItem> triggerButtonItems;

        public event PropertyChangedEventHandler PropertyChanged;
        public override event EventHandler ActionPropertyChanged;
        public List<GyroTriggerButtonItem> TriggerButtonItems => triggerButtonItems;
        public List<GyroTriggerButtonItem> ActivationButtonItems =>
            triggerButtonItems.Where(item => item.Code != JoypadActionCodes.AlwaysOn).ToList();

        public GyroActivationModeChoice GyroActivationModeChoice
        {
            get => GetGyroActivationMode(
                action.passthruParams.gyroTriggerButtons ?? Array.Empty<JoypadActionCodes>(),
                action.passthruParams.triggerActivates);
            set
            {
                if (GyroActivationModeChoice == value) return;

                if (value == GyroActivationModeChoice.AlwaysOn)
                {
                    foreach (GyroTriggerButtonItem item in ActivationButtonItems)
                    {
                        item.Enabled = false;
                    }

                    SetTriggerItemEnabled(triggerButtonItems, JoypadActionCodes.AlwaysOn, true);
                    GyroTriggerActivates = true;
                }
                else
                {
                    SetTriggerItemEnabled(triggerButtonItems, JoypadActionCodes.AlwaysOn, false);
                    GyroTriggerActivates = value == GyroActivationModeChoice.HoldToEnable;
                }

                OnActivationChanged();
            }
        }

        public bool GyroActivationButtonsUsed =>
            GyroActivationModeChoice != GyroActivationModeChoice.AlwaysOn;

        public bool GyroTriggerAnySelected
        {
            get => !action.passthruParams.andCond;
            set
            {
                if (value)
                {
                    GyroTriggerCondChoice = false;
                }
            }
        }

        public bool GyroTriggerAllSelected
        {
            get => action.passthruParams.andCond;
            set
            {
                if (value)
                {
                    GyroTriggerCondChoice = true;
                }
            }
        }

        public bool GyroTriggerCondChoice
        {
            get => action.passthruParams.andCond;
            set
            {
                if (action.passthruParams.andCond == value) return;
                action.passthruParams.andCond = value;
                MarkChanged(GyroPassthruAction.PropertyKeyStrings.TRIGGER_EVAL_COND);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GyroTriggerAnySelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GyroTriggerAllSelected)));
            }
        }

        public bool GyroTriggerActivates
        {
            get => action.passthruParams.triggerActivates;
            set
            {
                if (action.passthruParams.triggerActivates == value) return;
                action.passthruParams.triggerActivates = value;
                MarkChanged(GyroPassthruAction.PropertyKeyStrings.TRIGGER_ACTIVATE);
            }
        }

        public int GyroActivationHoldMs
        {
            get => action.passthruParams.activationHoldMs;
            set
            {
                int clampedValue = Math.Clamp(value, 0, 60000);
                if (action.passthruParams.activationHoldMs == clampedValue) return;
                action.passthruParams.activationHoldMs = clampedValue;
                MarkChanged(GyroPassthruAction.PropertyKeyStrings.ACTIVATION_HOLD_MS);
            }
        }

        public GyroPassthruActionPropViewModel(Mapper mapper, GyroMapAction action)
        {
            this.mapper = mapper;
            this.action = action as GyroPassthruAction;
            baseAction = action;
            triggerButtonItems = new List<GyroTriggerButtonItem>();

            PopulateModel();
            NameChanged += GyroPassthruActionPropViewModel_NameChanged;
        }

        private void PopulateModel()
        {
            foreach (ActionTriggerItem item in mapper.ActionTriggerItems)
            {
                triggerButtonItems.Add(new GyroTriggerButtonItem(item.DisplayName, item.Code));
            }

            foreach (JoypadActionCodes code in action.passthruParams.gyroTriggerButtons ??
                Array.Empty<JoypadActionCodes>())
            {
                GyroTriggerButtonItem item = triggerButtonItems.FirstOrDefault(candidate => candidate.Code == code);
                if (item != null)
                {
                    item.Enabled = true;
                }
            }

            triggerButtonItems.ForEach(item => item.EnabledChanged += GyroTriggerItem_EnabledChanged);
        }

        private void GyroTriggerItem_EnabledChanged(object sender, EventArgs e)
        {
            GyroTriggerButtonItem item = sender as GyroTriggerButtonItem;
            List<JoypadActionCodes> updated = (action.passthruParams.gyroTriggerButtons ??
                Array.Empty<JoypadActionCodes>()).ToList();

            if (item.Enabled && !updated.Contains(item.Code))
            {
                updated.Add(item.Code);
            }
            else if (!item.Enabled)
            {
                updated.Remove(item.Code);
            }

            action.passthruParams.gyroTriggerButtons = updated.ToArray();
            MarkChanged(GyroPassthruAction.PropertyKeyStrings.TRIGGER_BUTTONS);
            OnActivationChanged();
        }

        private void GyroPassthruActionPropViewModel_NameChanged(object sender, EventArgs e)
        {
            MarkChanged(GyroPassthruAction.PropertyKeyStrings.NAME);
        }

        private void MarkChanged(string propertyName)
        {
            if (!action.ChangedProperties.Contains(propertyName))
            {
                action.ChangedProperties.Add(propertyName);
            }

            action.RaiseNotifyPropertyChange(mapper, propertyName);
            ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnActivationChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GyroActivationModeChoice)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GyroActivationButtonsUsed)));
        }
    }
}
