using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels;
using DS4MapperTest.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DS4MapperTest.ViewModels.TouchpadActionPropViewModels
{
    public class TouchpadActionPadPropViewModel : TouchpadActionPropVMBase
    {
        public enum ActionPresetChoices
        {
            None,
            WASD,
            Arrows,
        }

        private TouchpadActionPad action;
        public TouchpadActionPad Action
        {
            get => action;
        }

        public string DeadZone
        {
            get => action.DeadMod.DeadZone.ToString();
            set
            {
                if (double.TryParse(value, out double temp))
                {
                    action.DeadMod.DeadZone = Math.Clamp(temp, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                    ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler DeadZoneChanged;

        private List<EnumChoiceSelection<StickDeadZone.DeadZoneTypes>> deadZoneModesChoices =
            new List<EnumChoiceSelection<StickDeadZone.DeadZoneTypes>>()
            {
                new EnumChoiceSelection<StickDeadZone.DeadZoneTypes>("Radial", StickDeadZone.DeadZoneTypes.Radial),
                new EnumChoiceSelection<StickDeadZone.DeadZoneTypes>("Bowtie", StickDeadZone.DeadZoneTypes.Bowtie),
            };

        public List<EnumChoiceSelection<StickDeadZone.DeadZoneTypes>> DeadZoneModesChoices => deadZoneModesChoices;

        public StickDeadZone.DeadZoneTypes DeadZoneType
        {
            get => action.DeadMod.DeadZoneType;
            set
            {
                action.DeadMod.DeadZoneType = value;
                DeadZoneTypeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeadZoneTypeChanged;

        public int DiagonalRange
        {
            get => action.DiagonalRange;
            set
            {
                if (action.DiagonalRange == value) return;
                action.DiagonalRange = value;
                DiagonalRangeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DiagonalRangeChanged;

        public bool RequiresClick
        {
            get => action.RequiresClick;
            set
            {
                if (action.RequiresClick == value) return;
                action.RequiresClick = value;
                RequiresClickChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RequiresClickChanged;

        private List<PadModeItem> padModeItems;
        public List<PadModeItem> PadModeItems => padModeItems;

        private int selectedPadModeIndex = -1;
        public int SelectedPadModeIndex
        {
            get => selectedPadModeIndex;
            set
            {
                if (selectedPadModeIndex == value) return;
                selectedPadModeIndex = value;
                SelectedPadModeIndexChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedPadModeIndexChanged;

        public bool ShowDiagonalPad
        {
            get => action.CurrentMode == TouchpadActionPad.DPadMode.EightWay ||
                action.CurrentMode == TouchpadActionPad.DPadMode.FourWayDiagonal;
        }
        public event EventHandler ShowDiagonalPadChanged;

        public bool ShowCardinalPad
        {
            get => action.CurrentMode == TouchpadActionPad.DPadMode.Standard ||
                action.CurrentMode == TouchpadActionPad.DPadMode.EightWay ||
                action.CurrentMode == TouchpadActionPad.DPadMode.FourWayCardinal;
        }
        public event EventHandler ShowCardinalPadChanged;

        public bool ShowReleaseBrakeSection => true;
        public event EventHandler ShowReleaseBrakeSectionChanged;

        public bool CounterMovementReleasePressEnabled
        {
            get => action.ReleaseBrake.Enabled;
            set
            {
                if (action.ReleaseBrake.Enabled == value) return;
                action.ReleaseBrake.Enabled = value;
                CounterMovementReleasePressEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterMovementReleasePressEnabledChanged;

        private List<EnumChoiceSelection<CounterMovementTapLengthPreset>> tapLengthPresetItems =
            new List<EnumChoiceSelection<CounterMovementTapLengthPreset>>()
            {
                new EnumChoiceSelection<CounterMovementTapLengthPreset>("Custom", CounterMovementTapLengthPreset.Custom),
                new EnumChoiceSelection<CounterMovementTapLengthPreset>("CS2", CounterMovementTapLengthPreset.CS2),
            };
        public List<EnumChoiceSelection<CounterMovementTapLengthPreset>> TapLengthPresetItems => tapLengthPresetItems;

        public CounterMovementTapLengthPreset TapLengthPreset
        {
            get => action.ReleaseBrake.EffectiveTapLengthPreset;
            set
            {
                if (action.ReleaseBrake.EffectiveTapLengthPreset == value) return;

                if (value == CounterMovementTapLengthPreset.CS2)
                {
                    action.ReleaseBrake.ApplyCs2Preset();
                }
                else
                {
                    action.ReleaseBrake.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                }

                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler TapLengthPresetChanged;

        public int OppositeTapLengthMinimumMs
        {
            get => action.ReleaseBrake.OppositeTapLengthMinimumMs;
            set
            {
                if (action.ReleaseBrake.OppositeTapLengthMinimumMs == value) return;
                action.ReleaseBrake.OppositeTapLengthMinimumMs = value;
                action.ReleaseBrake.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                action.ReleaseBrake.NormalizeRanges();
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapLengthMinimumMsChanged;

        public int OppositeTapLengthMaximumMs
        {
            get => action.ReleaseBrake.OppositeTapLengthMaximumMs;
            set
            {
                if (action.ReleaseBrake.OppositeTapLengthMaximumMs == value) return;
                action.ReleaseBrake.OppositeTapLengthMaximumMs = value;
                action.ReleaseBrake.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                action.ReleaseBrake.NormalizeRanges();
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapLengthMaximumMsChanged;

        public int OppositeTapStartDelayMinimumMs
        {
            get => action.ReleaseBrake.OppositeTapStartDelayMinimumMs;
            set
            {
                if (action.ReleaseBrake.OppositeTapStartDelayMinimumMs == value) return;
                action.ReleaseBrake.OppositeTapStartDelayMinimumMs = value;
                action.ReleaseBrake.NormalizeRanges();
                OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapStartDelayMinimumMsChanged;

        public int OppositeTapStartDelayMaximumMs
        {
            get => action.ReleaseBrake.OppositeTapStartDelayMaximumMs;
            set
            {
                if (action.ReleaseBrake.OppositeTapStartDelayMaximumMs == value) return;
                action.ReleaseBrake.OppositeTapStartDelayMaximumMs = value;
                action.ReleaseBrake.NormalizeRanges();
                OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapStartDelayMaximumMsChanged;

        public int BrakeMinimumHoldMs
        {
            get => action.ReleaseBrake.MinimumHoldMs;
            set
            {
                if (action.ReleaseBrake.MinimumHoldMs == value) return;
                action.ReleaseBrake.MinimumHoldMs = value;
                BrakeMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler BrakeMinimumHoldMsChanged;

        public string ActionUpBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Up].DescribeActions(mapper);
        }
        public event EventHandler ActionUpBtnDisplayBindChanged;

        public string ActionDownBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Down].DescribeActions(mapper);
        }
        public event EventHandler ActionDownBtnDisplayBindChanged;

        public string ActionLeftBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Left].DescribeActions(mapper);
        }
        public event EventHandler ActionLeftBtnDisplayBindChanged;

        public string ActionRightBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Right].DescribeActions(mapper);
        }
        public event EventHandler ActionRightBtnDisplayBindChanged;

        public string ActionUpLeftBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.UpLeft].DescribeActions(mapper);
        }

        public string ActionUpRightBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.UpRight].DescribeActions(mapper);
        }

        public string ActionDownLeftBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.DownLeft].DescribeActions(mapper);
        }

        public string ActionDownRightBtnDisplayBind
        {
            get => action.EventCodes4[(int)TouchpadActionPad.DpadDirections.DownRight].DescribeActions(mapper);
        }

        public bool UseOuterRing
        {
            get => action.UseRingButton;
            set
            {
                if (action.UseRingButton == value) return;
                action.UseRingButton = value;
                UseOuterRingChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler UseOuterRingChanged;

        public bool OuterRingInvert
        {
            get => !action.UseAsOuterRing;
            set
            {
                if (action.UseAsOuterRing == !value) return;
                action.UseAsOuterRing = !value;
                OuterRingInvertChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OuterRingInvertChanged;

        public string OuterRingDeadZone
        {
            get => action.OuterRingDeadZone.ToString("N2");
            set
            {
                if (double.TryParse(value, out double temp))
                {
                    action.OuterRingDeadZone = Math.Clamp(temp, 0.0, 10000.0);
                    OuterRingDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                    ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler OuterRingDeadZoneChanged;

        private List<EnumChoiceSelection<OuterRingUseRange>> outerRingRangeChoiceItems =
            new List<EnumChoiceSelection<OuterRingUseRange>>()
            {
                new EnumChoiceSelection<OuterRingUseRange>("Only Active", OuterRingUseRange.OnlyActive),
                new EnumChoiceSelection<OuterRingUseRange>("Full Range", OuterRingUseRange.FullRange),
            };
        public List<EnumChoiceSelection<OuterRingUseRange>> OuterRingRangeChoiceItems => outerRingRangeChoiceItems;

        public OuterRingUseRange OuterRingRangeChoice
        {
            get => action.UsedOuterRingRange;
            set
            {
                action.UsedOuterRingRange = value;
                OuterRingRangeChoiceChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OuterRingRangeChoiceChanged;

        public string ActionRingDisplayBind
        {
            get
            {
                string result = "";
                if (action.RingButton != null)
                {
                    result = action.RingButton.DescribeActions(mapper);
                }

                return result;
            }
        }

        private List<EnumChoiceSelection<ActionPresetChoices>> actionPresetChoicesItems = new List<EnumChoiceSelection<ActionPresetChoices>>()
        {
            new EnumChoiceSelection<ActionPresetChoices>("", ActionPresetChoices.None),
            new EnumChoiceSelection<ActionPresetChoices>("WASD", ActionPresetChoices.WASD),
            new EnumChoiceSelection<ActionPresetChoices>("Arrows", ActionPresetChoices.Arrows),
        };
        public List<EnumChoiceSelection<ActionPresetChoices>> ActionPresetChoicesItems => actionPresetChoicesItems;

        private ActionPresetChoices actionPresetChoice;
        public ActionPresetChoices ActionPresetChoice
        {
            get => actionPresetChoice;
            set
            {
                if (actionPresetChoice == value) return;
                actionPresetChoice = value;
                ActionPresetChoiceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ActionPresetChoiceChanged;

        private List<TouchpadDirectionBindItem> cardinalDirectionItems;
        public List<TouchpadDirectionBindItem> CardinalDirectionItems => cardinalDirectionItems;

        private List<TouchpadDirectionBindItem> diagonalDirectionItems;
        public List<TouchpadDirectionBindItem> DiagonalDirectionItems => diagonalDirectionItems;

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightDeadZoneType
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }
        public event EventHandler HighlightDeadZoneTypeChanged;

        public bool HighlightDiagonalRange
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DIAGONAL_RANGE);
        }
        public event EventHandler HighlightDiagonalRangeChanged;

        public bool HighlightRequiresClick
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.REQUIRES_CLICK);
        }
        public event EventHandler HighlightRequiresClickChanged;

        public bool HighlightPadMode
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.PAD_MODE);
        }
        public event EventHandler HighlightPadModeChanged;

        public bool HighlightUseOuterRing
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.USE_OUTER_RING);
        }
        public event EventHandler HighlightUseOuterRingChanged;

        public bool HighlightOuterRingInvert
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.USE_AS_OUTER_RING);
        }
        public event EventHandler HighlightOuterRingInvertChanged;

        public bool HighlightOuterRingDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
        }
        public event EventHandler HighlightOuterRingDeadZoneChanged;

        public bool HighlightOuterRingRangeChoice
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
        }
        public event EventHandler HighlightOuterRingRangeChoiceChanged;

        public bool HighlightCounterMovementReleasePressEnabled
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        }
        public event EventHandler HighlightCounterMovementReleasePressEnabledChanged;

        public bool HighlightTapLengthPreset
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
        }
        public event EventHandler HighlightTapLengthPresetChanged;

        public bool HighlightOppositeTapLengthMinimumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
        }
        public event EventHandler HighlightOppositeTapLengthMinimumMsChanged;

        public bool HighlightOppositeTapLengthMaximumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
        }
        public event EventHandler HighlightOppositeTapLengthMaximumMsChanged;

        public bool HighlightOppositeTapStartDelayMinimumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        }
        public event EventHandler HighlightOppositeTapStartDelayMinimumMsChanged;

        public bool HighlightOppositeTapStartDelayMaximumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        }
        public event EventHandler HighlightOppositeTapStartDelayMaximumMsChanged;

        public bool HighlightBrakeMinimumHoldMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
        }
        public event EventHandler HighlightBrakeMinimumHoldMsChanged;

        public override event EventHandler ActionPropertyChanged;

        public TouchpadActionPadPropViewModel(Mapper mapper, TouchpadMapAction action)
        {
            this.mapper = mapper;
            this.action = action as TouchpadActionPad;
            this.baseAction = action;

            padModeItems = new List<PadModeItem>();

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                TouchpadActionPad baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as TouchpadActionPad;
                TouchpadActionPad tempAction = new TouchpadActionPad();
                tempAction.SoftCopyFromParent(baseLayerAction);
                //int tempLayerId = mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.Index;
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;
                //tempAction.MappingId = this.action.MappingId;

                this.action = tempAction;
                this.baseAction = this.action;
                usingRealAction = false;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            PrepareModel();
            PrepareDirectionItems();

            NameChanged += TouchpadActionPadPropViewModel_NameChanged;
            DeadZoneChanged += TouchpadActionPadPropViewModel_DeadZoneChanged;
            DeadZoneTypeChanged += TouchpadActionPadPropViewModel_DeadZoneTypeChanged;
            DiagonalRangeChanged += TouchpadActionPadPropViewModel_DiagonalRangeChanged;
            RequiresClickChanged += TouchpadActionPadPropViewModel_RequiresClickChanged;
            UseOuterRingChanged += TouchpadActionPadPropViewModel_UseOuterRingChanged;
            OuterRingDeadZoneChanged += TouchpadActionPadPropViewModel_OuterRingDeadZoneChanged;
            OuterRingInvertChanged += TouchpadActionPadPropViewModel_OuterRingInvertChanged;
            SelectedPadModeIndexChanged += ChangeStickPadMode;
            SelectedPadModeIndexChanged += TouchpadActionPadPropViewModel_SelectedPadModeIndexChanged;
            OuterRingRangeChoiceChanged += TouchpadActionPadPropViewModel_OuterRingRangeChoiceChanged;
            ActionPresetChoiceChanged += TouchpadActionPadPropViewModel_ActionPresetChoiceChanged;
            CounterMovementReleasePressEnabledChanged += TouchpadActionPadPropViewModel_CounterMovementReleasePressEnabledChanged;
            TapLengthPresetChanged += TouchpadActionPadPropViewModel_TapLengthPresetChanged;
            OppositeTapLengthMinimumMsChanged += TouchpadActionPadPropViewModel_OppositeTapLengthMinimumMsChanged;
            OppositeTapLengthMaximumMsChanged += TouchpadActionPadPropViewModel_OppositeTapLengthMaximumMsChanged;
            OppositeTapStartDelayMinimumMsChanged += TouchpadActionPadPropViewModel_OppositeTapStartDelayMinimumMsChanged;
            OppositeTapStartDelayMaximumMsChanged += TouchpadActionPadPropViewModel_OppositeTapStartDelayMaximumMsChanged;
            BrakeMinimumHoldMsChanged += TouchpadActionPadPropViewModel_BrakeMinimumHoldMsChanged;
        }

        private void TouchpadActionPadPropViewModel_CounterMovementReleasePressEnabledChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            HighlightCounterMovementReleasePressEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_TapLengthPresetChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            HighlightTapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_OppositeTapLengthMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            HighlightOppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_OppositeTapLengthMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            HighlightOppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_OppositeTapStartDelayMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            HighlightOppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_OppositeTapStartDelayMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            HighlightOppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_BrakeMinimumHoldMsChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.BRAKE_MIN_HOLD_MS))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
            HighlightBrakeMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE_TYPE))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE_TYPE);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE_TYPE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_ActionPresetChoiceChanged(object sender, EventArgs e)
        {
            SwitchDefinedPreset();
        }

        private void TouchpadActionPadPropViewModel_OuterRingInvertChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.USE_AS_OUTER_RING))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.USE_AS_OUTER_RING);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.USE_AS_OUTER_RING);
            HighlightOuterRingInvertChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_OuterRingDeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_DEAD_ZONE))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
            HighlightOuterRingDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_UseOuterRingChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.USE_OUTER_RING))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.USE_OUTER_RING);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.USE_OUTER_RING);
            HighlightUseOuterRingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_OuterRingRangeChoiceChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_FULL_RANGE))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
            HighlightOuterRingRangeChoiceChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_SelectedPadModeIndexChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.PAD_MODE))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_MODE);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_MODE);
            HighlightPadModeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ChangeStickPadMode(object sender, EventArgs e)
        {
            action.CurrentMode = padModeItems[selectedPadModeIndex].DPadMode;

            ShowCardinalPadChanged?.Invoke(this, EventArgs.Empty);
            ShowDiagonalPadChanged?.Invoke(this, EventArgs.Empty);
            ShowReleaseBrakeSectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_DiagonalRangeChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DIAGONAL_RANGE))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DIAGONAL_RANGE);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.DIAGONAL_RANGE);
            HighlightDiagonalRangeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_RequiresClickChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.REQUIRES_CLICK))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.REQUIRES_CLICK);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.REQUIRES_CLICK);
            HighlightRequiresClickChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadActionPadPropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PrepareModel()
        {
            padModeItems.AddRange(new PadModeItem[]
            {
                new PadModeItem("Standard", TouchpadActionPad.DPadMode.Standard),
                new PadModeItem("Eight Way", TouchpadActionPad.DPadMode.EightWay),
                new PadModeItem("Four Way Cardinal", TouchpadActionPad.DPadMode.FourWayCardinal),
                new PadModeItem("Four Way Diagonal", TouchpadActionPad.DPadMode.FourWayDiagonal),
            });

            int index = padModeItems.FindIndex((item) => item.DPadMode == action.CurrentMode);
            if (index >= 0)
            {
                selectedPadModeIndex = index;
            }
        }

        private void PrepareDirectionItems()
        {
            cardinalDirectionItems = new List<TouchpadDirectionBindItem>()
            {
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.Up, "Up", "Cardinal zone"),
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.Down, "Down", "Cardinal zone"),
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.Left, "Left", "Cardinal zone"),
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.Right, "Right", "Cardinal zone"),
            };

            diagonalDirectionItems = new List<TouchpadDirectionBindItem>()
            {
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.UpLeft, "Up Left", "Diagonal zone"),
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.UpRight, "Up Right", "Diagonal zone"),
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.DownLeft, "Down Left", "Diagonal zone"),
                new TouchpadDirectionBindItem(this, TouchpadActionPad.DpadDirections.DownRight, "Down Right", "Diagonal zone"),
            };
        }

        internal ButtonAction GetDirectionAction(TouchpadActionPad.DpadDirections direction)
        {
            return action.EventCodes4[(int)direction];
        }

        internal void EnsureEditableAction()
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }
        }

        internal ButtonAction EnsureEditableDirectionAction(TouchpadActionPad.DpadDirections direction)
        {
            EnsureEditableAction();

            ButtonAction dirAction = action.EventCodes4[(int)direction];
            if (dirAction == null)
            {
                dirAction = new AxisDirButton(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                action.EventCodes4[(int)direction] = dirAction;
            }

            MarkDirectionChanged(direction, dirAction);
            return dirAction;
        }

        internal void MarkDirectionChanged(TouchpadActionPad.DpadDirections direction, ButtonAction dirAction)
        {
            string propertyName = GetDirectionPropertyName(direction);
            if (!action.ChangedProperties.Contains(propertyName))
            {
                action.ChangedProperties.Add(propertyName);
            }

            action.UseParentActionButton[(int)direction] = false;
            action.RaiseNotifyPropertyChange(mapper, propertyName);
            FaceButtonBindingItem.MarkFunctionsChanged(dirAction);
        }

        internal EditFaceBindingContext PrepareDirectionEdit(TouchpadDirectionBindItem item)
        {
            ButtonAction dirAction = EnsureEditableDirectionAction(item.Direction);
            ActionFunc func = dirAction.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault();
            if (func == null)
            {
                func = new NormalPressFunc(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                mapper.ProcessMappingChangeAction(() =>
                {
                    dirAction.Release(mapper, ignoreReleaseActions: true);
                    dirAction.ActionFuncs.Insert(0, func);
                    MarkDirectionChanged(item.Direction, dirAction);
                });
            }

            return new EditFaceBindingContext(mapper, dirAction, func);
        }

        internal void RefreshDirectionBindings()
        {
            foreach (TouchpadDirectionBindItem item in cardinalDirectionItems)
            {
                item.Refresh();
            }

            foreach (TouchpadDirectionBindItem item in diagonalDirectionItems)
            {
                item.Refresh();
            }
        }

        private static string GetDirectionPropertyName(TouchpadActionPad.DpadDirections direction)
        {
            return direction switch
            {
                TouchpadActionPad.DpadDirections.Up => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP,
                TouchpadActionPad.DpadDirections.Down => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWN,
                TouchpadActionPad.DpadDirections.Left => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_LEFT,
                TouchpadActionPad.DpadDirections.Right => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_RIGHT,
                TouchpadActionPad.DpadDirections.UpLeft => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UPLEFT,
                TouchpadActionPad.DpadDirections.UpRight => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UPRIGHT,
                TouchpadActionPad.DpadDirections.DownLeft => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWNLEFT,
                TouchpadActionPad.DpadDirections.DownRight => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWNRIGHT,
                _ => TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP,
            };
        }

        public void UpdateUpDirAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Up] = newAction;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Up] = false;
            });


        }

        public void UpdateDownDirAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Down] = newAction;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWN);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Down] = false;
            });
        }

        public void UpdateLeftDirAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Left] = newAction;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_LEFT);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Left] = false;
            });
        }

        public void UpdateRightAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Right] = newAction;
                }
                
                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_RIGHT);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Right] = false;
            });
        }

        public void UpdateDownLeftAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.DownLeft] = newAction;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWNLEFT);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.DownLeft] = false;
            });
        }

        public void UpdateDownRightAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.DownRight] = newAction;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWNRIGHT);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.DownRight] = false;
            });
        }

        public void UpdateUpLeftAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.UpLeft] = newAction;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UPLEFT);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.UpLeft] = false;
            });
        }

        public void UpdateUpRightAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.UpRight] = newAction;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UPRIGHT);
                action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.UpRight] = false;
            });
        }

        public void UpdateRingButton(ButtonAction oldAction, ButtonAction newAction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            //ExecuteInMapperThread(() =>
            mapper.ProcessMappingChangeAction(() =>
            {
                if (oldAction != null)
                {
                    oldAction.Release(mapper, ignoreReleaseActions: true);
                    action.RingButton = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_BUTTON);
                action.UseParentRingButton = false;
            });
        }

        public void SwitchDefinedPreset()
        {
            // Do nothing on first (None) choice
            if (actionPresetChoice == ActionPresetChoices.None) return;

            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            ExecuteInMapperThread(() =>
            {
                // Find and release all currently active buttons
                List<TouchpadActionPad.DpadDirections> tempList = new List<TouchpadActionPad.DpadDirections>()
                {
                    TouchpadActionPad.DpadDirections.Up, TouchpadActionPad.DpadDirections.Down,
                    TouchpadActionPad.DpadDirections.Left, TouchpadActionPad.DpadDirections.Right,
                    TouchpadActionPad.DpadDirections.UpLeft, TouchpadActionPad.DpadDirections.UpRight,
                    TouchpadActionPad.DpadDirections.DownLeft, TouchpadActionPad.DpadDirections.DownRight,
                };

                foreach (TouchpadActionPad.DpadDirections dir in tempList)
                {
                    ButtonAction oldAction = action.EventCodes4[(int)dir];
                    if (oldAction != null)
                    {
                        oldAction?.Release(mapper, ignoreReleaseActions: true);
                    }
                }

                if (actionPresetChoice == ActionPresetChoices.WASD)
                {
                    OutputActionData tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                    (int)VirtualKeys.W,
                    (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.W));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.W];
                    AxisDirButton newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Up] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.S,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.S));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.S];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Down] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.A,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.A));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.A];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Left] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.D,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.D));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.D];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Right] = newAction as AxisDirButton;

                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Up] = false;
                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Down] = false;
                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Left] = false;
                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Right] = false;

                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP);
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_RIGHT);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_RIGHT);
                }
                else if (actionPresetChoice == ActionPresetChoices.Arrows)
                {
                    OutputActionData tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                    (int)VirtualKeys.Up,
                    (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Up));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Up];
                    AxisDirButton newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Up] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Down,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Down));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Down];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Down] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Left,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Left));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Left];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Left] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Right,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Right));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Right];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Right] = newAction as AxisDirButton;

                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Up] = false;
                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Down] = false;
                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Left] = false;
                    this.action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Right] = false;

                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP);
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_RIGHT);
                    action.RaiseNotifyPropertyChange(mapper, TouchpadActionPad.PropertyKeyStrings.PAD_DIR_RIGHT);
                }
            });

            ActionUpBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            ActionDownBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            ActionLeftBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            ActionRightBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            RefreshDirectionBindings();
        }
    }

    public class TouchpadDirectionBindItem : INotifyPropertyChanged, IQuickBindTarget
    {
        private readonly TouchpadActionPadPropViewModel owner;

        public event PropertyChangedEventHandler PropertyChanged;

        public TouchpadActionPad.DpadDirections Direction { get; }
        public string DisplayName { get; }
        public string Subtitle { get; }

        public string DisplayBind
        {
            get
            {
                ButtonAction action = owner.GetDirectionAction(Direction);
                string result = action?.DescribeActions(((IQuickBindTarget)this).Mapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        public TouchpadDirectionBindItem(TouchpadActionPadPropViewModel owner,
            TouchpadActionPad.DpadDirections direction, string displayName, string subtitle)
        {
            this.owner = owner;
            Direction = direction;
            DisplayName = displayName;
            Subtitle = subtitle;
        }

        Mapper IQuickBindTarget.Mapper => owner.Napper;
        string IQuickBindTarget.RowLabel => DisplayName;
        string IQuickBindTarget.SlotLabel => "Regular Press";
        bool IQuickBindTarget.IsComplexBinding =>
            !QuickBindActionApplier.IsSimpleFunc(
                owner.GetDirectionAction(Direction)?.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault());

        EditFaceBindingContext IQuickBindTarget.GetEditContext()
        {
            return owner.PrepareDirectionEdit(this);
        }

        void IQuickBindTarget.NotifyBindingChanged()
        {
            owner.MarkDirectionChanged(Direction, owner.GetDirectionAction(Direction));
            Refresh();
        }

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayBind)));
        }
    }

    public class PadModeItem
    {
        private string displayName;
        public string DisplayName
        {
            get => displayName;
        }

        private TouchpadActionPad.DPadMode dpadMode = TouchpadActionPad.DPadMode.Standard;
        public TouchpadActionPad.DPadMode DPadMode
        {
            get => dpadMode;
            set
            {
                if (dpadMode == value) return;
                dpadMode = value;
                DPadModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DPadModeChanged;

        public PadModeItem(string displayName, TouchpadActionPad.DPadMode dpadMode)
        {
            this.displayName = displayName;
            this.dpadMode = dpadMode;
        }
    }
}
