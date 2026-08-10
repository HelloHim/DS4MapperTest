using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ViewModels.Common;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.StickActions;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.ViewModels.StickActionPropViewModels
{
    public class StickPadActionPropViewModel
    {
        public enum ActionPresetChoices
        {
            None,
            WASD,
            Arrows,
        }

        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        private StickPadAction action;
        public StickPadAction Action
        {
            get => action;
        }

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
            get => action.CurrentMode == StickPadAction.DPadMode.EightWay ||
                action.CurrentMode == StickPadAction.DPadMode.FourWayDiagonal;
        }
        public event EventHandler ShowDiagonalPadChanged;

        public bool ShowDiagonalZoneWidth => action.CurrentMode == StickPadAction.DPadMode.Standard ||
            action.CurrentMode == StickPadAction.DPadMode.EightWay;
        public event EventHandler ShowDiagonalZoneWidthChanged;

        public bool ShowCardinalPad
        {
            get => action.CurrentMode == StickPadAction.DPadMode.Standard ||
                action.CurrentMode == StickPadAction.DPadMode.EightWay ||
                action.CurrentMode == StickPadAction.DPadMode.FourWayCardinal;
        }
        public event EventHandler ShowCardinalPadChanged;

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

        public bool SeparateAxisDeadZones
        {
            get => action.DeadMod.SeparateAxisDeadZones;
            set
            {
                if (action.DeadMod.SeparateAxisDeadZones == value) return;
                if (value)
                {
                    action.DeadMod.DeadZoneX = action.DeadMod.DeadZone;
                    action.DeadMod.DeadZoneY = action.DeadMod.DeadZone;
                    DeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                    DeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                }
                action.DeadMod.SeparateAxisDeadZones = value;
                SeparateAxisDeadZonesChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SeparateAxisDeadZonesChanged;

        public string DeadZoneX
        {
            get => action.DeadMod.DeadZoneX.ToString();
            set
            {
                if (!double.TryParse(value, out double result)) return;
                action.DeadMod.DeadZoneX = Math.Clamp(result, 0.0, 1.0);
                DeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeadZoneXChanged;

        public string DeadZoneY
        {
            get => action.DeadMod.DeadZoneY.ToString();
            set
            {
                if (!double.TryParse(value, out double result)) return;
                action.DeadMod.DeadZoneY = Math.Clamp(result, 0.0, 1.0);
                DeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeadZoneYChanged;

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

        public int Rotation
        {
            get => action.Rotation;
            set
            {
                if (action.Rotation == value) return;
                action.Rotation = value;
                RotationChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RotationChanged;

        public string ActionUpBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.Up].DescribeActions(mapper);
        }
        public event EventHandler ActionUpBtnDisplayBindChanged;

        public string ActionDownBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.Down].DescribeActions(mapper);
        }
        public event EventHandler ActionDownBtnDisplayBindChanged;

        public string ActionLeftBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.Left].DescribeActions(mapper);
        }
        public event EventHandler ActionLeftBtnDisplayBindChanged;

        public string ActionRightBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.Right].DescribeActions(mapper);
        }
        public event EventHandler ActionRightBtnDisplayBindChanged;

        public string ActionUpLeftBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.UpLeft].DescribeActions(mapper);
        }
        public event EventHandler ActionUpLeftBtnDisplayBindChanged;

        public string ActionUpRightBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.UpRight].DescribeActions(mapper);
        }
        public event EventHandler ActionUpRightBtnDisplayBindChanged;

        public string ActionDownLeftBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.DownLeft].DescribeActions(mapper);
        }
        public event EventHandler ActionDownLeftBtnDisplayBindChanged;

        public string ActionDownRightBtnDisplayBind
        {
            get => action.EventCodes4[(int)StickPadAction.DpadDirections.DownRight].DescribeActions(mapper);
        }
        public event EventHandler ActionDownRightBtnDisplayBindChanged;

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

        private List<StickPadDirectionBindItem> cardinalDirectionItems;
        public List<StickPadDirectionBindItem> CardinalDirectionItems => cardinalDirectionItems;

        private List<StickPadDirectionBindItem> diagonalDirectionItems;
        public List<StickPadDirectionBindItem> DiagonalDirectionItems => diagonalDirectionItems;

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightPadMode
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.PAD_MODE);
        }
        public event EventHandler HighlightPadModeChanged;

        public bool HighlightDiagonalRange
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DIAGONAL_RANGE);
        }
        public event EventHandler HighlightDiagonalRangeChanged;

        public bool HighlightDeadZoneType
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }
        public event EventHandler HighlightDeadZoneTypeChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightRotation
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.ROTATION);
        }
        public event EventHandler HighlightRotationChanged;

        // This VM is only used while Stick Mode is DPad, and Counter Movement Release Press
        // is available for every D-Pad sub-mode.
        public bool ShowCounterMovementReleasePressSection => true;
        public event EventHandler ShowCounterMovementReleasePressSectionChanged;

        public bool CounterMovementReleasePressEnabled
        {
            get => action.CounterMovementReleasePress.Enabled;
            set
            {
                if (action.CounterMovementReleasePress.Enabled == value) return;
                action.CounterMovementReleasePress.Enabled = value;

                if (value)
                {
                    // Enabling always lands on Wait Variance Percentage mode and the CS2
                    // preset, so turning this on never surfaces stale/legacy tap-length
                    // values or a stale mode as an unexpected "Custom".
                    action.CounterMovementReleasePress.OppositeTapLengthMode = DS4MapperTest.StickActions.OppositeTapLengthMode.WaitVariancePercentage;
                    action.CounterMovementReleasePress.ApplyCs2Preset();
                    OppositeTapLengthModeChanged?.Invoke(this, EventArgs.Empty);
                    OppositeTapLengthModeDescriptionChanged?.Invoke(this, EventArgs.Empty);
                    ShowFixedModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                    ShowWaitVariancePercentageModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                    ShowMinimumAndMaximumModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                    TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                    OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                    OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                    OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                    OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                }

                CounterMovementReleasePressEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterMovementReleasePressEnabledChanged;

        public bool UseArrowKeysForCounterMovementPresses
        {
            get => action.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses;
            set
            {
                if (action.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses == value) return;
                action.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = value;
                UseArrowKeysForCounterMovementPressesChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler UseArrowKeysForCounterMovementPressesChanged;

        private List<EnumChoiceSelection<OppositeTapLengthMode>> tapLengthModeItems =
            new List<EnumChoiceSelection<OppositeTapLengthMode>>()
            {
                new EnumChoiceSelection<OppositeTapLengthMode>("Fixed", OppositeTapLengthMode.Fixed),
                new EnumChoiceSelection<OppositeTapLengthMode>("Time Variance (%)", OppositeTapLengthMode.WaitVariancePercentage),
                new EnumChoiceSelection<OppositeTapLengthMode>("Time Variance (Range)", OppositeTapLengthMode.MinimumAndMaximum),
            };
        public List<EnumChoiceSelection<OppositeTapLengthMode>> TapLengthModeItems => tapLengthModeItems;

        // Changing the mode alone only changes which representation is visible/authoritative
        // at runtime: all four numeric values are already kept synchronised, so this never
        // touches the preset or any numeric value.
        public OppositeTapLengthMode OppositeTapLengthMode
        {
            get => action.CounterMovementReleasePress.OppositeTapLengthMode;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapLengthMode == value) return;
                action.CounterMovementReleasePress.OppositeTapLengthMode = value;
                OppositeTapLengthModeChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthModeDescriptionChanged?.Invoke(this, EventArgs.Empty);
                ShowFixedModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowWaitVariancePercentageModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowMinimumAndMaximumModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Short, visible description of the currently selected mode, shown directly under the
        // mode dropdown rather than only on hover.
        public string OppositeTapLengthModeDescription
        {
            get
            {
                switch (action.CounterMovementReleasePress.OppositeTapLengthMode)
                {
                    case DS4MapperTest.StickActions.OppositeTapLengthMode.Fixed:
                        return "Uses the same total duration for every qualifying release.";
                    case DS4MapperTest.StickActions.OppositeTapLengthMode.WaitVariancePercentage:
                        return "Varies the total duration below and above the fixed value by the selected percentage.";
                    default:
                        return "Selects a total duration at random from the specified inclusive range.";
                }
            }
        }
        public event EventHandler OppositeTapLengthModeDescriptionChanged;
        public event EventHandler OppositeTapLengthModeChanged;

        public bool ShowFixedModeFields =>
            action.CounterMovementReleasePress.OppositeTapLengthMode == DS4MapperTest.StickActions.OppositeTapLengthMode.Fixed;
        public event EventHandler ShowFixedModeFieldsChanged;

        public bool ShowWaitVariancePercentageModeFields =>
            action.CounterMovementReleasePress.OppositeTapLengthMode == DS4MapperTest.StickActions.OppositeTapLengthMode.WaitVariancePercentage;
        public event EventHandler ShowWaitVariancePercentageModeFieldsChanged;

        public bool ShowMinimumAndMaximumModeFields =>
            action.CounterMovementReleasePress.OppositeTapLengthMode == DS4MapperTest.StickActions.OppositeTapLengthMode.MinimumAndMaximum;
        public event EventHandler ShowMinimumAndMaximumModeFieldsChanged;

        private List<EnumChoiceSelection<CounterMovementTapLengthPreset>> tapLengthPresetItems =
            new List<EnumChoiceSelection<CounterMovementTapLengthPreset>>()
            {
                new EnumChoiceSelection<CounterMovementTapLengthPreset>("Custom", CounterMovementTapLengthPreset.Custom),
                new EnumChoiceSelection<CounterMovementTapLengthPreset>("CS2", CounterMovementTapLengthPreset.CS2),
            };
        public List<EnumChoiceSelection<CounterMovementTapLengthPreset>> TapLengthPresetItems => tapLengthPresetItems;

        // The numeric tap-length values are authoritative: a stored CS2 preset whose values
        // no longer match 75/120 (e.g. edited directly, or loaded from a malformed profile)
        // must display as Custom rather than silently overwriting those numeric values.
        public CounterMovementTapLengthPreset TapLengthPreset
        {
            get => action.CounterMovementReleasePress.EffectiveTapLengthPreset;
            set
            {
                if (action.CounterMovementReleasePress.EffectiveTapLengthPreset == value) return;

                if (value == CounterMovementTapLengthPreset.CS2)
                {
                    action.CounterMovementReleasePress.ApplyCs2Preset();
                }
                else
                {
                    action.CounterMovementReleasePress.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                }

                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler TapLengthPresetChanged;

        public int OppositeTapLengthMs
        {
            get => action.CounterMovementReleasePress.OppositeTapLengthMs;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapLengthMs == value) return;
                action.CounterMovementReleasePress.ApplyFixedAndPercentage(value, action.CounterMovementReleasePress.OppositeTapLengthVariancePercent);
                // Editing the fixed duration by hand always drops the preset to Custom, even
                // if the edited value happens to still reproduce CS2's numbers.
                action.CounterMovementReleasePress.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapLengthMsChanged;

        public int OppositeTapLengthVariancePercent
        {
            get => action.CounterMovementReleasePress.OppositeTapLengthVariancePercent;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapLengthVariancePercent == value) return;
                action.CounterMovementReleasePress.ApplyFixedAndPercentage(action.CounterMovementReleasePress.OppositeTapLengthMs, value);
                action.CounterMovementReleasePress.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapLengthVariancePercentChanged;

        public int OppositeTapLengthMinimumMs
        {
            get => action.CounterMovementReleasePress.OppositeTapLengthMinimumMs;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapLengthMinimumMs == value) return;
                action.CounterMovementReleasePress.ApplyMinimumAndMaximum(value, action.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
                // Editing the tap-length range by hand always drops the preset to Custom,
                // even if the edited values happen to still match CS2's numbers.
                action.CounterMovementReleasePress.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapLengthMinimumMsChanged;

        public int OppositeTapLengthMaximumMs
        {
            get => action.CounterMovementReleasePress.OppositeTapLengthMaximumMs;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapLengthMaximumMs == value) return;
                action.CounterMovementReleasePress.ApplyMinimumAndMaximum(action.CounterMovementReleasePress.OppositeTapLengthMinimumMs, value);
                action.CounterMovementReleasePress.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                TapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapLengthMaximumMsChanged;

        private List<EnumChoiceSelection<OppositeTapStartDelayMode>> startDelayModeItems =
            new List<EnumChoiceSelection<OppositeTapStartDelayMode>>()
            {
                new EnumChoiceSelection<OppositeTapStartDelayMode>("Fixed", OppositeTapStartDelayMode.Fixed),
                new EnumChoiceSelection<OppositeTapStartDelayMode>("Time Variance (%)", OppositeTapStartDelayMode.WaitVariancePercentage),
                new EnumChoiceSelection<OppositeTapStartDelayMode>("Time Variance (Range)", OppositeTapStartDelayMode.MinimumAndMaximum),
            };
        public List<EnumChoiceSelection<OppositeTapStartDelayMode>> StartDelayModeItems => startDelayModeItems;

        public OppositeTapStartDelayMode OppositeTapStartDelayMode
        {
            get => action.CounterMovementReleasePress.OppositeTapStartDelayMode;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapStartDelayMode == value) return;
                action.CounterMovementReleasePress.OppositeTapStartDelayMode = value;
                OppositeTapStartDelayModeChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayModeDescriptionChanged?.Invoke(this, EventArgs.Empty);
                ShowStartDelayFixedModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowStartDelayWaitVariancePercentageModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowStartDelayMinimumAndMaximumModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapStartDelayModeChanged;

        public string OppositeTapStartDelayModeDescription
        {
            get
            {
                switch (action.CounterMovementReleasePress.OppositeTapStartDelayMode)
                {
                    case OppositeTapStartDelayMode.Fixed:
                        return "Uses the same neutral delay before every generated opposite press.";
                    case OppositeTapStartDelayMode.WaitVariancePercentage:
                        return "Varies the neutral delay below and above the fixed value by the selected percentage.";
                    default:
                        return "Selects a neutral delay at random from the specified inclusive range.";
                }
            }
        }
        public event EventHandler OppositeTapStartDelayModeDescriptionChanged;

        public bool ShowStartDelayFixedModeFields =>
            action.CounterMovementReleasePress.OppositeTapStartDelayMode == OppositeTapStartDelayMode.Fixed;
        public event EventHandler ShowStartDelayFixedModeFieldsChanged;

        public bool ShowStartDelayWaitVariancePercentageModeFields =>
            action.CounterMovementReleasePress.OppositeTapStartDelayMode == OppositeTapStartDelayMode.WaitVariancePercentage;
        public event EventHandler ShowStartDelayWaitVariancePercentageModeFieldsChanged;

        public bool ShowStartDelayMinimumAndMaximumModeFields =>
            action.CounterMovementReleasePress.OppositeTapStartDelayMode == OppositeTapStartDelayMode.MinimumAndMaximum;
        public event EventHandler ShowStartDelayMinimumAndMaximumModeFieldsChanged;

        public int OppositeTapStartDelayMs
        {
            get => action.CounterMovementReleasePress.OppositeTapStartDelayMs;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapStartDelayMs == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayFixedAndPercentage(value, action.CounterMovementReleasePress.OppositeTapStartDelayVariancePercent);
                action.CounterMovementReleasePress.NormalizeRanges();
                OppositeTapStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapStartDelayMsChanged;

        public int OppositeTapStartDelayVariancePercent
        {
            get => action.CounterMovementReleasePress.OppositeTapStartDelayVariancePercent;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapStartDelayVariancePercent == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayFixedAndPercentage(action.CounterMovementReleasePress.OppositeTapStartDelayMs, value);
                action.CounterMovementReleasePress.NormalizeRanges();
                OppositeTapStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapStartDelayVariancePercentChanged;

        public int OppositeTapStartDelayMinimumMs
        {
            get => action.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayMinimumAndMaximum(value, action.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs);
                // Start delay edits never change the selected tap-length preset.
                OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapStartDelayMinimumMsChanged;

        public int OppositeTapStartDelayMaximumMs
        {
            get => action.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs;
            set
            {
                if (action.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayMinimumAndMaximum(action.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs, value);
                OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                OppositeTapStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OppositeTapStartDelayMaximumMsChanged;

        public int CounterMovementMinimumHoldMs
        {
            get => action.CounterMovementReleasePress.MinimumHoldMs;
            set
            {
                if (action.CounterMovementReleasePress.MinimumHoldMs == value) return;
                action.CounterMovementReleasePress.MinimumHoldMs = value;
                CounterMovementMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterMovementMinimumHoldMsChanged;

        public int RequiredStickDeflectionThresholdPercent
        {
            get => (int)Math.Round(action.CounterMovementReleasePress.ArmingThreshold * 100.0);
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                double threshold = clamped / 100.0;
                if (Math.Abs(action.CounterMovementReleasePress.ArmingThreshold - threshold) < double.Epsilon) return;
                action.CounterMovementReleasePress.ArmingThreshold = threshold;
                RequiredStickDeflectionThresholdPercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RequiredStickDeflectionThresholdPercentChanged;

        public bool HighlightCounterMovementReleasePressEnabled
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        }
        public event EventHandler HighlightCounterMovementReleasePressEnabledChanged;

        public bool HighlightUseArrowKeysForCounterMovementPresses =>
            action.ParentAction == null || action.ChangedProperties.Contains(
                StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
        public event EventHandler HighlightUseArrowKeysForCounterMovementPressesChanged;

        public bool HighlightTapLengthPreset
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
        }
        public event EventHandler HighlightTapLengthPresetChanged;

        public bool HighlightOppositeTapLengthMode
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
        }
        public event EventHandler HighlightOppositeTapLengthModeChanged;

        public bool HighlightOppositeTapLengthMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
        }
        public event EventHandler HighlightOppositeTapLengthMsChanged;

        public bool HighlightOppositeTapLengthVariancePercent
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
        }
        public event EventHandler HighlightOppositeTapLengthVariancePercentChanged;

        public bool HighlightOppositeTapLengthMinimumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
        }
        public event EventHandler HighlightOppositeTapLengthMinimumMsChanged;

        public bool HighlightOppositeTapLengthMaximumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
        }
        public event EventHandler HighlightOppositeTapLengthMaximumMsChanged;

        public bool HighlightOppositeTapStartDelayMode
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
        }
        public event EventHandler HighlightOppositeTapStartDelayModeChanged;

        public bool HighlightOppositeTapStartDelayMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
        }
        public event EventHandler HighlightOppositeTapStartDelayMsChanged;

        public bool HighlightOppositeTapStartDelayVariancePercent
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
        }
        public event EventHandler HighlightOppositeTapStartDelayVariancePercentChanged;

        public bool HighlightOppositeTapStartDelayMinimumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        }
        public event EventHandler HighlightOppositeTapStartDelayMinimumMsChanged;

        public bool HighlightOppositeTapStartDelayMaximumMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        }
        public event EventHandler HighlightOppositeTapStartDelayMaximumMsChanged;

        public bool HighlightCounterMovementMinimumHoldMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_MIN_HOLD_MS);
        }
        public event EventHandler HighlightCounterMovementMinimumHoldMsChanged;

        public bool HighlightRequiredStickDeflectionThreshold
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
        }
        public event EventHandler HighlightRequiredStickDeflectionThresholdChanged;

        public event EventHandler ActionPropertyChanged;
        public event EventHandler<StickMapAction> ActionChanged;

        private bool usingRealAction = false;

        public StickPadActionPropViewModel(Mapper mapper, StickMapAction action)
        {
            this.mapper = mapper;
            this.action = action as StickPadAction;
            padModeItems = new List<PadModeItem>();
            usingRealAction = true;

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                StickPadAction baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as StickPadAction;
                StickPadAction tempAction = new StickPadAction();
                tempAction.SoftCopyFromParent(baseLayerAction);
                //int tempLayerId = mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.Index;
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;
                //tempAction.MappingId = this.action.MappingId;

                this.action = tempAction;
                usingRealAction = false;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            PrepareModel();
            PrepareDirectionItems();

            NameChanged += StickPadActionPropViewModel_NameChanged;
            DeadZoneChanged += StickPadActionPropViewModel_DeadZoneChanged;
            DeadZoneTypeChanged += StickPadActionPropViewModel_DeadZoneTypeChanged;
            SeparateAxisDeadZonesChanged += StickPadActionPropViewModel_SeparateAxisDeadZonesChanged;
            DeadZoneXChanged += StickPadActionPropViewModel_DeadZoneXChanged;
            DeadZoneYChanged += StickPadActionPropViewModel_DeadZoneYChanged;
            RotationChanged += StickPadActionPropViewModel_RotationChanged;
            ActionPresetChoiceChanged += StickPadActionPropViewModel_ActionPresetChoiceChanged;
            SelectedPadModeIndexChanged += ChangeStickPadMode;
            SelectedPadModeIndexChanged += StickPadActionPropViewModel_SelectedPadModeIndexChanged;
            CounterMovementReleasePressEnabledChanged += StickPadActionPropViewModel_CounterMovementReleasePressEnabledChanged;
            UseArrowKeysForCounterMovementPressesChanged += StickPadActionPropViewModel_UseArrowKeysForCounterMovementPressesChanged;
            TapLengthPresetChanged += StickPadActionPropViewModel_TapLengthPresetChanged;
            OppositeTapLengthModeChanged += StickPadActionPropViewModel_OppositeTapLengthModeChanged;
            OppositeTapLengthMsChanged += StickPadActionPropViewModel_OppositeTapLengthMsChanged;
            OppositeTapLengthVariancePercentChanged += StickPadActionPropViewModel_OppositeTapLengthVariancePercentChanged;
            OppositeTapLengthMinimumMsChanged += StickPadActionPropViewModel_OppositeTapLengthMinimumMsChanged;
            OppositeTapLengthMaximumMsChanged += StickPadActionPropViewModel_OppositeTapLengthMaximumMsChanged;
            OppositeTapStartDelayModeChanged += StickPadActionPropViewModel_OppositeTapStartDelayModeChanged;
            OppositeTapStartDelayMsChanged += StickPadActionPropViewModel_OppositeTapStartDelayMsChanged;
            OppositeTapStartDelayVariancePercentChanged += StickPadActionPropViewModel_OppositeTapStartDelayVariancePercentChanged;
            OppositeTapStartDelayMinimumMsChanged += StickPadActionPropViewModel_OppositeTapStartDelayMinimumMsChanged;
            OppositeTapStartDelayMaximumMsChanged += StickPadActionPropViewModel_OppositeTapStartDelayMaximumMsChanged;
            CounterMovementMinimumHoldMsChanged += StickPadActionPropViewModel_CounterMovementMinimumHoldMsChanged;
            RequiredStickDeflectionThresholdPercentChanged += StickPadActionPropViewModel_RequiredStickDeflectionThresholdPercentChanged;
        }

        private void StickPadActionPropViewModel_CounterMovementReleasePressEnabledChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            HighlightCounterMovementReleasePressEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_UseArrowKeysForCounterMovementPressesChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
            HighlightUseArrowKeysForCounterMovementPressesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_TapLengthPresetChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            HighlightTapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapLengthModeChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
            HighlightOppositeTapLengthModeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapLengthMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
            HighlightOppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapLengthVariancePercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
            HighlightOppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapLengthMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            HighlightOppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapLengthMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            HighlightOppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapStartDelayModeChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
            HighlightOppositeTapStartDelayModeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapStartDelayMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
            HighlightOppositeTapStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapStartDelayVariancePercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
            HighlightOppositeTapStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapStartDelayMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            HighlightOppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_OppositeTapStartDelayMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            HighlightOppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_CounterMovementMinimumHoldMsChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_MIN_HOLD_MS))
            {
                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_MIN_HOLD_MS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_MIN_HOLD_MS);
            HighlightCounterMovementMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_RequiredStickDeflectionThresholdPercentChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD))
            {
                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
            }

            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
            HighlightRequiredStickDeflectionThresholdChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_ActionPresetChoiceChanged(object sender, EventArgs e)
        {
            SwitchDefinedPreset();
        }

        private void StickPadActionPropViewModel_RotationChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.ROTATION))
            {
                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.ROTATION);
            }

            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.ROTATION);
            HighlightRotationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE_TYPE))
            {
                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
            HighlightDeadZoneTypeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE))
            {
                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.DEAD_ZONE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_SeparateAxisDeadZonesChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);
        }

        private void StickPadActionPropViewModel_DeadZoneXChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_X);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.DEAD_ZONE_X);
        }

        private void StickPadActionPropViewModel_DeadZoneYChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_Y);
            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.DEAD_ZONE_Y);
        }

        private void ChangeStickPadMode(object sender, EventArgs e)
        {
            action.CurrentMode = padModeItems[selectedPadModeIndex].DPadMode;

            ShowCardinalPadChanged?.Invoke(this, EventArgs.Empty);
            ShowDiagonalPadChanged?.Invoke(this, EventArgs.Empty);
            ShowDiagonalZoneWidthChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_SelectedPadModeIndexChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.PAD_MODE))
            {
                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_MODE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_MODE);
            HighlightPadModeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickPadActionPropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
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

        private void PrepareModel()
        {
            padModeItems.AddRange(new PadModeItem[]
            {
                new PadModeItem("8 Way (Overlap)", StickPadAction.DPadMode.Standard),
                new PadModeItem("8 Way (Separate Diagonals)", StickPadAction.DPadMode.EightWay),
                new PadModeItem("4 Way (Cardinal)", StickPadAction.DPadMode.FourWayCardinal),
                new PadModeItem("4 Way (Diagonal)", StickPadAction.DPadMode.FourWayDiagonal),
                new PadModeItem("Analog Emulation", StickPadAction.DPadMode.AnalogEmulation),
            });

            int index = padModeItems.FindIndex((item) => item.DPadMode == action.CurrentMode);
            if (index >= 0)
            {
                selectedPadModeIndex = index;
            }
        }

        private void PrepareDirectionItems()
        {
            cardinalDirectionItems = new List<StickPadDirectionBindItem>()
            {
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.Up, "Up", "Cardinal zone"),
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.Down, "Down", "Cardinal zone"),
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.Left, "Left", "Cardinal zone"),
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.Right, "Right", "Cardinal zone"),
            };

            diagonalDirectionItems = new List<StickPadDirectionBindItem>()
            {
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.UpLeft, "Up Left", "Diagonal zone"),
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.UpRight, "Up Right", "Diagonal zone"),
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.DownLeft, "Down Left", "Diagonal zone"),
                new StickPadDirectionBindItem(this, StickPadAction.DpadDirections.DownRight, "Down Right", "Diagonal zone"),
            };
        }

        internal ButtonAction GetDirectionAction(StickPadAction.DpadDirections direction)
        {
            return action.EventCodes4[(int)direction];
        }

        internal AxisDirButton EnsureEditableDirectionAction(StickPadAction.DpadDirections direction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            AxisDirButton dirAction = action.EventCodes4[(int)direction];
            if (dirAction == null)
            {
                dirAction = new AxisDirButton(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                action.EventCodes4[(int)direction] = dirAction;
            }

            MarkDirectionChanged(direction, dirAction);
            return dirAction;
        }

        internal void MarkDirectionChanged(StickPadAction.DpadDirections direction, ButtonAction dirAction)
        {
            string propertyName = GetDirectionPropertyName(direction);
            if (!action.ChangedProperties.Contains(propertyName))
            {
                action.ChangedProperties.Add(propertyName);
            }

            action.UsingParentActionButton[(int)direction] = false;
            action.RaiseNotifyPropertyChange(mapper, propertyName);
            FaceButtonBindingItem.MarkFunctionsChanged(dirAction);
        }

        internal EditFaceBindingContext PrepareDirectionEdit(StickPadDirectionBindItem item)
        {
            AxisDirButton dirAction = EnsureEditableDirectionAction(item.Direction);
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
            foreach (StickPadDirectionBindItem item in cardinalDirectionItems)
            {
                item.Refresh();
            }

            foreach (StickPadDirectionBindItem item in diagonalDirectionItems)
            {
                item.Refresh();
            }
        }

        private static string GetDirectionPropertyName(StickPadAction.DpadDirections direction)
        {
            return direction switch
            {
                StickPadAction.DpadDirections.Up => StickPadAction.PropertyKeyStrings.PAD_DIR_UP,
                StickPadAction.DpadDirections.Down => StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN,
                StickPadAction.DpadDirections.Left => StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT,
                StickPadAction.DpadDirections.Right => StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT,
                StickPadAction.DpadDirections.UpLeft => StickPadAction.PropertyKeyStrings.PAD_DIR_UPLEFT,
                StickPadAction.DpadDirections.UpRight => StickPadAction.PropertyKeyStrings.PAD_DIR_UPRIGHT,
                StickPadAction.DpadDirections.DownLeft => StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNLEFT,
                StickPadAction.DpadDirections.DownRight => StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNRIGHT,
                _ => StickPadAction.PropertyKeyStrings.PAD_DIR_UP,
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
                    oldAction?.Release(mapper, ignoreReleaseActions: true);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Up] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UP);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Up] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_UP);
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
                    oldAction?.Release(mapper, ignoreReleaseActions: true);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Down] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Down] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
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
                    oldAction?.Release(mapper, ignoreReleaseActions: true);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Left] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Left] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
            });
        }

        public void UpdateRightDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            ExecuteInMapperThread(() =>
            {
                if (oldAction != null)
                {
                    oldAction?.Release(mapper, ignoreReleaseActions: true);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Right] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Right] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
            });
        }

        public void UpdateUpLeftDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            ExecuteInMapperThread(() =>
            {
                if (oldAction != null)
                {
                    oldAction?.Release(mapper, ignoreReleaseActions: true);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.UpLeft] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UPLEFT);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.UpLeft] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_UPLEFT);
            });
        }

        public void UpdateUpRightDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            ExecuteInMapperThread(() =>
            {
                if (oldAction != null)
                {
                    oldAction?.Release(mapper, ignoreReleaseActions: true);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.UpRight] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UPRIGHT);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.UpRight] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_UPRIGHT);
            });
        }

        public void UpdateDownLeftDirAction(ButtonAction oldAction, ButtonAction newAction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            ExecuteInMapperThread(() =>
            {
                if (oldAction != null)
                {
                    oldAction?.Release(mapper, ignoreReleaseActions: true);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.DownLeft] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNLEFT);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.DownLeft] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNLEFT);
            });
        }

        public void UpdateDownRightDirAction(ButtonAction oldAction, ButtonAction newAction)
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
                    action.EventCodes4[(int)StickPadAction.DpadDirections.DownRight] = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNRIGHT);
                this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.DownRight] = false;
                action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNRIGHT);
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
                List<StickPadAction.DpadDirections> tempList = new List<StickPadAction.DpadDirections>()
                {
                    StickPadAction.DpadDirections.Up, StickPadAction.DpadDirections.Down,
                    StickPadAction.DpadDirections.Left, StickPadAction.DpadDirections.Right,
                    StickPadAction.DpadDirections.UpLeft, StickPadAction.DpadDirections.UpRight,
                    StickPadAction.DpadDirections.DownLeft, StickPadAction.DpadDirections.DownRight,
                };

                foreach(StickPadAction.DpadDirections dir in tempList)
                {
                    AxisDirButton oldAction = action.EventCodes4[(int)dir];
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
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Up] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.S,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.S));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.S];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Down] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.A,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.A));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.A];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Left] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.D,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.D));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.D];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Right] = newAction as AxisDirButton;

                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Up] = false;
                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Down] = false;
                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Left] = false;
                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Right] = false;

                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UP);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_UP);
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                }
                else if (actionPresetChoice == ActionPresetChoices.Arrows)
                {
                    OutputActionData tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                    (int)VirtualKeys.Up,
                    (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Up));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Up];
                    AxisDirButton newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Up] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Down,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Down));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Down];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Down] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Left,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Left));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Left];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Left] = newAction as AxisDirButton;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Right,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Right));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Right];
                    newAction = new AxisDirButton(tempData);
                    action.EventCodes4[(int)StickPadAction.DpadDirections.Right] = newAction as AxisDirButton;

                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Up] = false;
                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Down] = false;
                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Left] = false;
                    this.action.UsingParentActionButton[(int)StickPadAction.DpadDirections.Right] = false;

                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UP);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_UP);
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                    action.RaiseNotifyPropertyChange(mapper, StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                }
            });

            ActionUpBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            ActionDownBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            ActionLeftBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            ActionRightBtnDisplayBindChanged?.Invoke(this, EventArgs.Empty);
            RefreshDirectionBindings();
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

    public class StickPadDirectionBindItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly StickPadActionPropViewModel owner;

        public event PropertyChangedEventHandler PropertyChanged;

        public StickPadAction.DpadDirections Direction { get; }
        public string DisplayName { get; }
        public string Subtitle { get; }
        public ObservableCollection<ActionOutputItem> OutputItems { get; } =
            new ObservableCollection<ActionOutputItem>();

        public string DisplayBind
        {
            get
            {
                ButtonAction action = owner.GetDirectionAction(Direction);
                string result = action?.DescribeActions(((IQuickBindTarget)this).Mapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        public StickPadDirectionBindItem(StickPadActionPropViewModel owner,
            StickPadAction.DpadDirections direction, string displayName, string subtitle)
        {
            this.owner = owner;
            Direction = direction;
            DisplayName = displayName;
            Subtitle = subtitle;
            RefreshOutputItems();
        }

        Mapper IQuickBindTarget.Mapper => owner.Mapper;
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

        Mapper IActionOutputListOwner.Mapper => owner.Mapper;
        string IActionOutputListOwner.RowLabel => DisplayName;
        string IActionOutputListOwner.SlotLabel => "Regular Press";
        ActionFunc IActionOutputListOwner.Func => CurrentFunc;
        EditFaceBindingContext IActionOutputListOwner.PrepareEdit(ActionOutputItem item) => PrepareEdit(item);
        void IActionOutputListOwner.AddOutputAction() => AddOutputAction();
        void IActionOutputListOwner.RemoveOutputAction(ActionOutputItem item) => RemoveOutputAction(item);
        void IActionOutputListOwner.NotifyBindingChanged()
        {
            owner.MarkDirectionChanged(Direction, owner.GetDirectionAction(Direction));
            Refresh();
        }

        private ActionFunc CurrentFunc =>
            owner.GetDirectionAction(Direction)?.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault();

        public EditFaceBindingContext PrepareEdit(ActionOutputItem item)
        {
            EditFaceBindingContext ctx = owner.PrepareDirectionEdit(this);
            int index = item?.Index ?? 0;
            EnsureOutputSlot(ctx, index);
            return new EditFaceBindingContext(ctx.Mapper, ctx.Action, ctx.Func, index);
        }

        public void AddOutputAction()
        {
            EditFaceBindingContext ctx = owner.PrepareDirectionEdit(this);
            owner.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Mapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                owner.MarkDirectionChanged(Direction, ctx.Action);
            });

            RefreshOutputItems();
        }

        public void RemoveOutputAction(ActionOutputItem item)
        {
            if (item == null || item.Index <= 0)
            {
                return;
            }

            EditFaceBindingContext ctx = owner.PrepareDirectionEdit(this);
            if (item.Index >= ctx.Func.OutputActions.Count)
            {
                return;
            }

            owner.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Mapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.RemoveAt(item.Index);
                owner.MarkDirectionChanged(Direction, ctx.Action);
            });

            RefreshOutputItems();
        }

        private void EnsureOutputSlot(EditFaceBindingContext ctx, int index)
        {
            if (ctx.Func.OutputActions.Count > index)
            {
                return;
            }

            owner.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Mapper, ignoreReleaseActions: true);
                while (ctx.Func.OutputActions.Count <= index)
                {
                    ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                }

                owner.MarkDirectionChanged(Direction, ctx.Action);
            });
        }

        private void RefreshOutputItems()
        {
            OutputItems.Clear();
            int count = Math.Max(1, CurrentFunc?.OutputActions.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                OutputItems.Add(new ActionOutputItem(this, i));
            }
        }

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayBind)));
            RefreshOutputItems();
        }
    }

    public class PadModeItem
    {
        private string displayName;
        public string DisplayName
        {
            get => displayName;
        }

        private StickPadAction.DPadMode dpadMode = StickPadAction.DPadMode.Standard;
        public StickPadAction.DPadMode DPadMode
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

        public PadModeItem(string displayName, StickPadAction.DPadMode dpadMode)
        {
            this.displayName = displayName;
            this.dpadMode = dpadMode;
        }
    }
}
