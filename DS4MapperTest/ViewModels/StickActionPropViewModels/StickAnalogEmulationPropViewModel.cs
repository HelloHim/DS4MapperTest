using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.ViewModels;
using DS4MapperTest.ViewModels.Common;

namespace DS4MapperTest.ViewModels.StickActionPropViewModels
{
    public class StickAnalogEmulationPropViewModel
    {
        public enum ActionPresetChoices
        {
            None,
            WASD,
            Arrows,
        }

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

        private List<StickAnalogDirectionBindItem> cardinalDirectionItems;
        public List<StickAnalogDirectionBindItem> CardinalDirectionItems => cardinalDirectionItems;

        private List<EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>> directionResolutionItems =
            new List<EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>>()
            {
                new EnumChoiceSelection<AnalogEmulationMath.ResolutionMode>("8 Directions (D-Pad Mode)", AnalogEmulationMath.ResolutionMode.EightWay),
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

        private List<EnumChoiceSelection<OppositeTapLengthMode>> tapLengthModeItems =
            new List<EnumChoiceSelection<OppositeTapLengthMode>>()
            {
                new EnumChoiceSelection<OppositeTapLengthMode>("Fixed", OppositeTapLengthMode.Fixed),
                new EnumChoiceSelection<OppositeTapLengthMode>("Time Variance (%)", OppositeTapLengthMode.WaitVariancePercentage),
                new EnumChoiceSelection<OppositeTapLengthMode>("Time Variance (Range)", OppositeTapLengthMode.MinimumAndMaximum),
            };
        public List<EnumChoiceSelection<OppositeTapLengthMode>> TapLengthModeItems => tapLengthModeItems;

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
        public event EventHandler OppositeTapLengthModeChanged;

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

        public int BrakeMinimumHoldMs
        {
            get => action.CounterMovementReleasePress.MinimumHoldMs;
            set
            {
                if (action.CounterMovementReleasePress.MinimumHoldMs == value) return;
                action.CounterMovementReleasePress.MinimumHoldMs = value;
                BrakeMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler BrakeMinimumHoldMsChanged;

        public int BrakeArmingThresholdPercent
        {
            get => (int)Math.Round(action.CounterMovementReleasePress.ArmingThreshold * 100.0);
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                double threshold = clamped / 100.0;
                if (Math.Abs(action.CounterMovementReleasePress.ArmingThreshold - threshold) < double.Epsilon) return;
                action.CounterMovementReleasePress.ArmingThreshold = threshold;
                BrakeArmingThresholdPercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler BrakeArmingThresholdPercentChanged;

        public bool HighlightCounterMovementReleasePressEnabled =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        public bool HighlightTapLengthPreset =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
        public bool HighlightOppositeTapLengthMode =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
        public bool HighlightOppositeTapLengthMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
        public bool HighlightOppositeTapLengthVariancePercent =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
        public bool HighlightOppositeTapLengthMinimumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
        public bool HighlightOppositeTapLengthMaximumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
        public bool HighlightOppositeTapStartDelayMode =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
        public bool HighlightOppositeTapStartDelayMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
        public bool HighlightOppositeTapStartDelayVariancePercent =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
        public bool HighlightOppositeTapStartDelayMinimumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        public bool HighlightOppositeTapStartDelayMaximumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        public bool HighlightBrakeMinimumHoldMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
        public bool HighlightBrakeArmingThreshold =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_ARMING_THRESHOLD);

        public string ActionUpBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Up]?.DescribeActions(mapper);
        public event EventHandler ActionUpBtnDisplayBindChanged;
        public string ActionDownBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Down]?.DescribeActions(mapper);
        public event EventHandler ActionDownBtnDisplayBindChanged;
        public string ActionLeftBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Left]?.DescribeActions(mapper);
        public event EventHandler ActionLeftBtnDisplayBindChanged;
        public string ActionRightBtnDisplayBind => action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Right]?.DescribeActions(mapper);
        public event EventHandler ActionRightBtnDisplayBindChanged;

        public bool HighlightName =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.NAME);
        public bool HighlightDeadZoneType =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
        public bool HighlightDeadZone =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE);
        public bool HighlightRotation =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.ROTATION);
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

            PrepareDirectionItems();

            NameChanged += StickAnalogEmulationPropViewModel_NameChanged;
            DeadZoneTypeChanged += StickAnalogEmulationPropViewModel_DeadZoneTypeChanged;
            DeadZoneChanged += StickAnalogEmulationPropViewModel_DeadZoneChanged;
            SeparateAxisDeadZonesChanged += StickAnalogEmulationPropViewModel_SeparateAxisDeadZonesChanged;
            DeadZoneXChanged += StickAnalogEmulationPropViewModel_DeadZoneXChanged;
            DeadZoneYChanged += StickAnalogEmulationPropViewModel_DeadZoneYChanged;
            RotationChanged += StickAnalogEmulationPropViewModel_RotationChanged;
            ActionPresetChoiceChanged += StickAnalogEmulationPropViewModel_ActionPresetChoiceChanged;
            DirectionResolutionChanged += StickAnalogEmulationPropViewModel_DirectionResolutionChanged;
            DirectionPulseTimeMsChanged += StickAnalogEmulationPropViewModel_DirectionPulseTimeMsChanged;
            AnalogSpeedEmulationEnabledChanged += StickAnalogEmulationPropViewModel_AnalogSpeedEmulationEnabledChanged;
            AnalogEmulationActivePercentChanged += StickAnalogEmulationPropViewModel_AnalogEmulationActivePercentChanged;
            AnalogEmulationPulseTimeMsChanged += StickAnalogEmulationPropViewModel_AnalogEmulationPulseTimeMsChanged;
            FullSpeedThresholdPercentChanged += StickAnalogEmulationPropViewModel_FullSpeedThresholdPercentChanged;
            CounterMovementReleasePressEnabledChanged += StickAnalogEmulationPropViewModel_CounterMovementReleasePressEnabledChanged;
            TapLengthPresetChanged += StickAnalogEmulationPropViewModel_TapLengthPresetChanged;
            OppositeTapLengthModeChanged += StickAnalogEmulationPropViewModel_OppositeTapLengthModeChanged;
            OppositeTapLengthMsChanged += StickAnalogEmulationPropViewModel_OppositeTapLengthMsChanged;
            OppositeTapLengthVariancePercentChanged += StickAnalogEmulationPropViewModel_OppositeTapLengthVariancePercentChanged;
            OppositeTapLengthMinimumMsChanged += StickAnalogEmulationPropViewModel_OppositeTapLengthMinimumMsChanged;
            OppositeTapLengthMaximumMsChanged += StickAnalogEmulationPropViewModel_OppositeTapLengthMaximumMsChanged;
            OppositeTapStartDelayModeChanged += StickAnalogEmulationPropViewModel_OppositeTapStartDelayModeChanged;
            OppositeTapStartDelayMsChanged += StickAnalogEmulationPropViewModel_OppositeTapStartDelayMsChanged;
            OppositeTapStartDelayVariancePercentChanged += StickAnalogEmulationPropViewModel_OppositeTapStartDelayVariancePercentChanged;
            OppositeTapStartDelayMinimumMsChanged += StickAnalogEmulationPropViewModel_OppositeTapStartDelayMinimumMsChanged;
            OppositeTapStartDelayMaximumMsChanged += StickAnalogEmulationPropViewModel_OppositeTapStartDelayMaximumMsChanged;
            BrakeMinimumHoldMsChanged += StickAnalogEmulationPropViewModel_BrakeMinimumHoldMsChanged;
            BrakeArmingThresholdPercentChanged += StickAnalogEmulationPropViewModel_BrakeArmingThresholdPercentChanged;
        }

        private void StickAnalogEmulationPropViewModel_CounterMovementReleasePressEnabledChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        }

        private void StickAnalogEmulationPropViewModel_TapLengthPresetChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapLengthModeChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapLengthMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapLengthVariancePercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapLengthMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapLengthMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapStartDelayModeChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapStartDelayMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapStartDelayVariancePercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapStartDelayMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        }

        private void StickAnalogEmulationPropViewModel_OppositeTapStartDelayMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        }

        private void StickAnalogEmulationPropViewModel_BrakeMinimumHoldMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
        }

        private void StickAnalogEmulationPropViewModel_BrakeArmingThresholdPercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_ARMING_THRESHOLD);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_ARMING_THRESHOLD);
        }

        private void StickAnalogEmulationPropViewModel_NameChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.NAME);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.NAME);
        }

        private void StickAnalogEmulationPropViewModel_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }

        private void StickAnalogEmulationPropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE);
        }

        private void StickAnalogEmulationPropViewModel_SeparateAxisDeadZonesChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);
        }

        private void StickAnalogEmulationPropViewModel_DeadZoneXChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_X);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_X);
        }

        private void StickAnalogEmulationPropViewModel_DeadZoneYChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_Y);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_Y);
        }

        private void StickAnalogEmulationPropViewModel_RotationChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.ROTATION);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.ROTATION);
        }

        private void StickAnalogEmulationPropViewModel_ActionPresetChoiceChanged(object sender, EventArgs e)
        {
            SwitchDefinedPreset();
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

        private void PrepareDirectionItems()
        {
            cardinalDirectionItems = new List<StickAnalogDirectionBindItem>()
            {
                new StickAnalogDirectionBindItem(this, StickAnalogEmulationAction.DirSlot.Up, "Up", "Cardinal direction"),
                new StickAnalogDirectionBindItem(this, StickAnalogEmulationAction.DirSlot.Down, "Down", "Cardinal direction"),
                new StickAnalogDirectionBindItem(this, StickAnalogEmulationAction.DirSlot.Left, "Left", "Cardinal direction"),
                new StickAnalogDirectionBindItem(this, StickAnalogEmulationAction.DirSlot.Right, "Right", "Cardinal direction"),
            };
        }

        internal ButtonAction GetDirectionAction(StickAnalogEmulationAction.DirSlot direction)
        {
            return action.DirButtons[(int)direction];
        }

        internal AxisDirButton EnsureEditableDirectionAction(StickAnalogEmulationAction.DirSlot direction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            AxisDirButton dirAction = action.DirButtons[(int)direction];
            if (dirAction == null)
            {
                dirAction = new AxisDirButton(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                action.DirButtons[(int)direction] = dirAction;
            }

            MarkDirectionChanged(direction, dirAction);
            return dirAction;
        }

        internal void MarkDirectionChanged(StickAnalogEmulationAction.DirSlot direction, ButtonAction dirAction)
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

        internal EditFaceBindingContext PrepareDirectionEdit(StickAnalogDirectionBindItem item)
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
            foreach (StickAnalogDirectionBindItem item in cardinalDirectionItems)
            {
                item.Refresh();
            }
        }

        private static string GetDirectionPropertyName(StickAnalogEmulationAction.DirSlot direction)
        {
            return direction switch
            {
                StickAnalogEmulationAction.DirSlot.Up => StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP,
                StickAnalogEmulationAction.DirSlot.Down => StickAnalogEmulationAction.PropertyKeyStrings.DIR_DOWN,
                StickAnalogEmulationAction.DirSlot.Left => StickAnalogEmulationAction.PropertyKeyStrings.DIR_LEFT,
                StickAnalogEmulationAction.DirSlot.Right => StickAnalogEmulationAction.PropertyKeyStrings.DIR_RIGHT,
                _ => StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP,
            };
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
                List<StickAnalogEmulationAction.DirSlot> tempList = new List<StickAnalogEmulationAction.DirSlot>()
                {
                    StickAnalogEmulationAction.DirSlot.Up, StickAnalogEmulationAction.DirSlot.Down,
                    StickAnalogEmulationAction.DirSlot.Left, StickAnalogEmulationAction.DirSlot.Right,
                };

                foreach (StickAnalogEmulationAction.DirSlot slot in tempList)
                {
                    AxisDirButton oldAction = action.DirButtons[(int)slot];
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
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Up] = newAction;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.S,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.S));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.S];
                    newAction = new AxisDirButton(tempData);
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Down] = newAction;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.A,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.A));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.A];
                    newAction = new AxisDirButton(tempData);
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Left] = newAction;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.D,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.D));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.D];
                    newAction = new AxisDirButton(tempData);
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Right] = newAction;

                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Up] = false;
                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Down] = false;
                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Left] = false;
                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Right] = false;

                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP);
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_DOWN);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_DOWN);
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_LEFT);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_LEFT);
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_RIGHT);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_RIGHT);
                }
                else if (actionPresetChoice == ActionPresetChoices.Arrows)
                {
                    OutputActionData tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                    (int)VirtualKeys.Up,
                    (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Up));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Up];
                    AxisDirButton newAction = new AxisDirButton(tempData);
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Up] = newAction;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Down,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Down));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Down];
                    newAction = new AxisDirButton(tempData);
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Down] = newAction;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Left,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Left));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Left];
                    newAction = new AxisDirButton(tempData);
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Left] = newAction;

                    tempData = new OutputActionData(OutputActionData.ActionType.Keyboard,
                        (int)VirtualKeys.Right,
                        (int)mapper.EventInputMapping.GetRealEventKey((uint)VirtualKeys.Right));
                    tempData.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[VirtualKeys.Right];
                    newAction = new AxisDirButton(tempData);
                    action.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Right] = newAction;

                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Up] = false;
                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Down] = false;
                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Left] = false;
                    action.UsingParentActionButton[(int)StickAnalogEmulationAction.DirSlot.Right] = false;

                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP);
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_DOWN);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_DOWN);
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_LEFT);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_LEFT);
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_RIGHT);
                    action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIR_RIGHT);
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

    public class StickAnalogDirectionBindItem : INotifyPropertyChanged, IQuickBindTarget
    {
        private readonly StickAnalogEmulationPropViewModel owner;

        public event PropertyChangedEventHandler PropertyChanged;

        public StickAnalogEmulationAction.DirSlot Direction { get; }
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

        public StickAnalogDirectionBindItem(StickAnalogEmulationPropViewModel owner,
            StickAnalogEmulationAction.DirSlot direction, string displayName, string subtitle)
        {
            this.owner = owner;
            Direction = direction;
            DisplayName = displayName;
            Subtitle = subtitle;
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

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayBind)));
        }
    }
}
