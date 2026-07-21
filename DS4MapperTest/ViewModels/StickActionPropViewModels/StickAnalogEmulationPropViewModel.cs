using System;
using System.Collections.Generic;
using System.Threading;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.StickActions;
using DS4MapperTest.ViewModels.Common;

namespace DS4MapperTest.ViewModels.StickActionPropViewModels
{
    public class StickAnalogEmulationPropViewModel
    {
        private Mapper mapper;
        public Mapper Mapper => mapper;

        private StickAnalogEmulationAction action;
        public StickAnalogEmulationAction Action => action;

        public string Name
        {
            get => action.Name;
            set
            {
                if (action.Name == value) return;
                action.Name = value;
                NameChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler NameChanged;

        private List<EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>> directionResolutionItems =
            new List<EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>>()
            {
                new EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>("16 Directions", AnalogEmulationMath.ResolutionMode.Sixteen),
                new EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>("32 Directions", AnalogEmulationMath.ResolutionMode.ThirtyTwo),
                new EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>("Continuous Direction", AnalogEmulationMath.ResolutionMode.Continuous),
            };
        public List<EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>> DirectionResolutionItems => directionResolutionItems;

        public AnalogEmulationMath.ResolutionMode DirectionResolution
        {
            get => action.DirectionMode;
            set
            {
                if (action.DirectionMode == value) return;
                action.DirectionMode = value;
                DirectionResolutionChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DirectionResolutionChanged;

        public int DirectionPulseTimeMs
        {
            get => action.DirectionPulseTimeMs;
            set
            {
                if (action.DirectionPulseTimeMs == value) return;
                action.DirectionPulseTimeMs = value;
                DirectionPulseTimeMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DirectionPulseTimeMsChanged;

        public bool AnalogSpeedEmulationEnabled
        {
            get => action.SpeedEmulationEnabled;
            set
            {
                if (action.SpeedEmulationEnabled == value) return;
                action.SpeedEmulationEnabled = value;
                AnalogSpeedEmulationEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler AnalogSpeedEmulationEnabledChanged;

        public int AnalogEmulationActivePercent
        {
            get => action.SpeedActivePercent;
            set
            {
                if (action.SpeedActivePercent == value) return;
                action.SpeedActivePercent = value;
                AnalogEmulationActivePercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler AnalogEmulationActivePercentChanged;

        public int AnalogEmulationPulseTimeMs
        {
            get => action.SpeedPulseTimeMs;
            set
            {
                if (action.SpeedPulseTimeMs == value) return;
                action.SpeedPulseTimeMs = value;
                AnalogEmulationPulseTimeMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler AnalogEmulationPulseTimeMsChanged;

        public int FullSpeedThresholdPercent
        {
            get => action.FullSpeedThresholdPercent;
            set
            {
                if (action.FullSpeedThresholdPercent == value) return;
                action.FullSpeedThresholdPercent = value;
                FullSpeedThresholdPercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler FullSpeedThresholdPercentChanged;

        public string ActionUpBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Up]?.DescribeActions(mapper);
        public string ActionDownBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Down]?.DescribeActions(mapper);
        public string ActionLeftBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Left]?.DescribeActions(mapper);
        public string ActionRightBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Right]?.DescribeActions(mapper);

        public bool HighlightName =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.NAME);
        public bool HighlightDirectionMode =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_MODE);
        public bool HighlightDirectionPulseTimeMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_PULSE_TIME_MS);
        public bool HighlightSpeedEnabled =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ENABLED);
        public bool HighlightSpeedActivePercent =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ACTIVE_PERCENT);
        public bool HighlightSpeedPulseTimeMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_PULSE_TIME_MS);
        public bool HighlightFullSpeedThresholdPercent =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.FULL_SPEED_THRESHOLD_PERCENT);

        public event EventHandler ActionPropertyChanged;
        public event EventHandler<StickMapAction> ActionChanged;

        private bool usingRealAction = false;

        public StickAnalogEmulationPropViewModel(Mapper mapper, StickMapAction action)
        {
            this.mapper = mapper;
            this.action = action as StickAnalogEmulationAction;
            usingRealAction = true;

            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                StickAnalogEmulationAction baseLayerAction =
                    mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as StickAnalogEmulationAction;
                StickAnalogEmulationAction tempAction = new StickAnalogEmulationAction();
                tempAction.SoftCopyFromParent(baseLayerAction);
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;

                this.action = tempAction;
                usingRealAction = false;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            NameChanged += StickAnalogEmulationPropViewModel_NameChanged;
            DirectionResolutionChanged += StickAnalogEmulationPropViewModel_DirectionResolutionChanged;
            DirectionPulseTimeMsChanged += StickAnalogEmulationPropViewModel_DirectionPulseTimeMsChanged;
            AnalogSpeedEmulationEnabledChanged += StickAnalogEmulationPropViewModel_AnalogSpeedEmulationEnabledChanged;
            AnalogEmulationActivePercentChanged += StickAnalogEmulationPropViewModel_AnalogEmulationActivePercentChanged;
            AnalogEmulationPulseTimeMsChanged += StickAnalogEmulationPropViewModel_AnalogEmulationPulseTimeMsChanged;
            FullSpeedThresholdPercentChanged += StickAnalogEmulationPropViewModel_FullSpeedThresholdPercentChanged;
        }

        private void StickAnalogEmulationPropViewModel_NameChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.NAME);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.NAME);
        }

        private void StickAnalogEmulationPropViewModel_DirectionResolutionChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_MODE);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_MODE);
        }

        private void StickAnalogEmulationPropViewModel_DirectionPulseTimeMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_PULSE_TIME_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_PULSE_TIME_MS);
        }

        private void StickAnalogEmulationPropViewModel_AnalogSpeedEmulationEnabledChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ENABLED);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ENABLED);
        }

        private void StickAnalogEmulationPropViewModel_AnalogEmulationActivePercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ACTIVE_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ACTIVE_PERCENT);
        }

        private void StickAnalogEmulationPropViewModel_AnalogEmulationPulseTimeMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_PULSE_TIME_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.SPEED_PULSE_TIME_MS);
        }

        private void StickAnalogEmulationPropViewModel_FullSpeedThresholdPercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.FULL_SPEED_THRESHOLD_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.FULL_SPEED_THRESHOLD_PERCENT);
        }

        private void ReplaceExistingLayerAction(object sender, EventArgs e)
        {
            if (!usingRealAction)
            {
                mapper.ProcessMappingChangeAction(() =>
                {
                    this.action.ParentAction.Release(mapper, ignoreReleaseActions: true);

                    mapper.EditLayer.AddStickAction(this.action);
                    if (mapper.EditActionSet.UsingCompositeLayer)
                    {
                        mapper.EditActionSet.RecompileCompositeLayer(mapper);
                    }
                    else
                    {
                        mapper.EditLayer.SyncActions();
                        mapper.EditActionSet.ClearCompositeLayerActions();
                        mapper.EditActionSet.PrepareCompositeLayer();
                    }
                });

                usingRealAction = true;

                ActionChanged?.Invoke(this, action);
            }
        }

        public void UpdateUpDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            UpdateDirAction(StickAnalogEmulationAction.DirSlot.Up, oldAction, newAction,
                StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP);
        }

        public void UpdateDownDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            UpdateDirAction(StickAnalogEmulationAction.DirSlot.Down, oldAction, newAction,
                StickAnalogEmulationAction.PropertyKeyStrings.DIR_DOWN);
        }

        public void UpdateLeftDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            UpdateDirAction(StickAnalogEmulationAction.DirSlot.Left, oldAction, newAction,
                StickAnalogEmulationAction.PropertyKeyStrings.DIR_LEFT);
        }

        public void UpdateRightDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            UpdateDirAction(StickAnalogEmulationAction.DirSlot.Right, oldAction, newAction,
                StickAnalogEmulationAction.PropertyKeyStrings.DIR_RIGHT);
        }

        private void UpdateDirAction(StickAnalogEmulationAction.DirSlot slot, ButtonAction oldAction,
            ButtonAction newAction, string propertyKey)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            ExecuteInMapperThread(() =>
            {
                if (oldAction != null)
                {
                    oldAction.Release(mapper, ignoreReleaseActions: true);
                    action.DirButtons[(int)slot] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(propertyKey);
                action.UsingParentActionButton[(int)slot] = false;
                action.RaiseNotifyPropertyChange(mapper, propertyKey);
            });
        }

        protected void ExecuteInMapperThread(Action tempAction)
        {
            ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

            mapper.ProcessMappingChangeAction(() =>
            {
                tempAction?.Invoke();

                resetEvent.Set();
            });

            resetEvent.Wait();
        }
    }
}
