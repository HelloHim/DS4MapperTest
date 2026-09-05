using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        // Kept here so legacy analogue-emulation actions are edited through the same
        // D-Pad mode selector as newly-created D-Pad actions.
        private readonly List<PadModeItem> padModeItems = new List<PadModeItem>()
        {
            new PadModeItem("8 Way (Overlap)", StickPadAction.DPadMode.Standard),
            new PadModeItem("8 Way (Separate Diagonals)", StickPadAction.DPadMode.EightWay),
            new PadModeItem("4 Way (Cardinal)", StickPadAction.DPadMode.FourWayCardinal),
            new PadModeItem("4 Way (Diagonal)", StickPadAction.DPadMode.FourWayDiagonal),
            new PadModeItem("Analog Emulation", StickPadAction.DPadMode.AnalogEmulation),
        };
        public List<PadModeItem> PadModeItems => padModeItems;
        public StickPadAction.DPadMode SelectedPadMode => padModeItems[selectedPadModeIndex].DPadMode;
        private int selectedPadModeIndex = 4;
        public int SelectedPadModeIndex
        {
            get => selectedPadModeIndex;
            set
            {
                if (selectedPadModeIndex == value) return;
                selectedPadModeIndex = value;
                SelectedPadModeIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedPadModeIndexChanged;

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

        public double DeadZone
        {
            get => action.DeadMod.DeadZone;
            set
            {
                double next = Math.Clamp(value, 0.0, 1.0);
                if (action.DeadMod.DeadZone == next) return;
                action.DeadMod.DeadZone = next;
                DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
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

        public double DeadZoneX
        {
            get => action.DeadMod.DeadZoneX;
            set
            {
                double next = Math.Clamp(value, 0.0, 1.0);
                if (action.DeadMod.DeadZoneX == next) return;
                action.DeadMod.DeadZoneX = next;
                DeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeadZoneXChanged;

        public double DeadZoneY
        {
            get => action.DeadMod.DeadZoneY;
            set
            {
                double next = Math.Clamp(value, 0.0, 1.0);
                if (action.DeadMod.DeadZoneY == next) return;
                action.DeadMod.DeadZoneY = next;
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

        public bool IsEightWayDirectionResolution =>
            action.DirectionMode == AnalogEmulationMath.ResolutionMode.EightWay;
        public event EventHandler IsEightWayDirectionResolutionChanged;

        public int DiagonalZoneWidth
        {
            get => action.DiagonalZoneWidth;
            set
            {
                if (action.DiagonalZoneWidth == value) return;
                action.DiagonalZoneWidth = value;
                DiagonalZoneWidthChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DiagonalZoneWidthChanged;

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
                    // Enabling always lands on Time Variance (Range) mode and the CS2
                    // preset, so turning this on never surfaces stale/legacy press-length
                    // values or a stale mode as an unexpected "Custom".
                    action.CounterMovementReleasePress.CounterPressLengthMode = DS4MapperTest.StickActions.CounterPressLengthMode.MinimumAndMaximum;
                    action.CounterMovementReleasePress.ApplyCs2Preset();
                    CounterPressLengthModeChanged?.Invoke(this, EventArgs.Empty);
                    CounterPressLengthModeDescriptionChanged?.Invoke(this, EventArgs.Empty);
                    ShowFixedModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                    ShowWaitVariancePercentageModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                    ShowMinimumAndMaximumModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                    PressLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                    CounterPressLengthMsChanged?.Invoke(this, EventArgs.Empty);
                    CounterPressLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                    CounterPressLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                    CounterPressLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
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

        private List<EnumChoiceSelection<CounterPressLengthMode>> pressLengthModeItems =
            new List<EnumChoiceSelection<CounterPressLengthMode>>()
            {
                new EnumChoiceSelection<CounterPressLengthMode>("Fixed", CounterPressLengthMode.Fixed),
                new EnumChoiceSelection<CounterPressLengthMode>("Time Variance (%)", CounterPressLengthMode.WaitVariancePercentage),
                new EnumChoiceSelection<CounterPressLengthMode>("Time Variance (Range)", CounterPressLengthMode.MinimumAndMaximum),
            };
        public List<EnumChoiceSelection<CounterPressLengthMode>> PressLengthModeItems => pressLengthModeItems;

        public CounterPressLengthMode CounterPressLengthMode
        {
            get => action.CounterMovementReleasePress.CounterPressLengthMode;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressLengthMode == value) return;
                action.CounterMovementReleasePress.CounterPressLengthMode = value;
                CounterPressLengthModeChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthModeDescriptionChanged?.Invoke(this, EventArgs.Empty);
                ShowFixedModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowWaitVariancePercentageModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowMinimumAndMaximumModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressLengthModeChanged;

        // Short, visible description of the currently selected mode, shown directly under the
        // mode dropdown rather than only on hover.
        public string CounterPressLengthModeDescription
        {
            get
            {
                switch (action.CounterMovementReleasePress.CounterPressLengthMode)
                {
                    case DS4MapperTest.StickActions.CounterPressLengthMode.Fixed:
                        return "Uses the same total duration for every qualifying release.";
                    case DS4MapperTest.StickActions.CounterPressLengthMode.WaitVariancePercentage:
                        return "Varies the total duration below and above the fixed value by the selected percentage.";
                    default:
                        return "Selects a total duration at random from the specified inclusive range.";
                }
            }
        }
        public event EventHandler CounterPressLengthModeDescriptionChanged;

        public bool ShowFixedModeFields =>
            action.CounterMovementReleasePress.CounterPressLengthMode == DS4MapperTest.StickActions.CounterPressLengthMode.Fixed;
        public event EventHandler ShowFixedModeFieldsChanged;

        public bool ShowWaitVariancePercentageModeFields =>
            action.CounterMovementReleasePress.CounterPressLengthMode == DS4MapperTest.StickActions.CounterPressLengthMode.WaitVariancePercentage;
        public event EventHandler ShowWaitVariancePercentageModeFieldsChanged;

        public bool ShowMinimumAndMaximumModeFields =>
            action.CounterMovementReleasePress.CounterPressLengthMode == DS4MapperTest.StickActions.CounterPressLengthMode.MinimumAndMaximum;
        public event EventHandler ShowMinimumAndMaximumModeFieldsChanged;

        private List<EnumChoiceSelection<CounterMovementPressLengthPreset>> pressLengthPresetItems =
            new List<EnumChoiceSelection<CounterMovementPressLengthPreset>>()
            {
                new EnumChoiceSelection<CounterMovementPressLengthPreset>("Custom", CounterMovementPressLengthPreset.Custom),
                new EnumChoiceSelection<CounterMovementPressLengthPreset>("CS2", CounterMovementPressLengthPreset.CS2),
            };
        public List<EnumChoiceSelection<CounterMovementPressLengthPreset>> PressLengthPresetItems => pressLengthPresetItems;

        public CounterMovementPressLengthPreset PressLengthPreset
        {
            get => action.CounterMovementReleasePress.EffectivePressLengthPreset;
            set
            {
                if (action.CounterMovementReleasePress.EffectivePressLengthPreset == value) return;

                if (value == CounterMovementPressLengthPreset.CS2)
                {
                    action.CounterMovementReleasePress.ApplyCs2Preset();
                }
                else
                {
                    action.CounterMovementReleasePress.PressLengthPreset = CounterMovementPressLengthPreset.Custom;
                }

                PressLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler PressLengthPresetChanged;

        public int CounterPressLengthMs
        {
            get => action.CounterMovementReleasePress.CounterPressLengthMs;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressLengthMs == value) return;
                action.CounterMovementReleasePress.ApplyFixedAndPercentage(value, action.CounterMovementReleasePress.CounterPressLengthVariancePercent);
                action.CounterMovementReleasePress.PressLengthPreset = CounterMovementPressLengthPreset.Custom;
                CounterPressLengthMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                PressLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressLengthMsChanged;

        public int CounterPressLengthVariancePercent
        {
            get => action.CounterMovementReleasePress.CounterPressLengthVariancePercent;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressLengthVariancePercent == value) return;
                action.CounterMovementReleasePress.ApplyFixedAndPercentage(action.CounterMovementReleasePress.CounterPressLengthMs, value);
                action.CounterMovementReleasePress.PressLengthPreset = CounterMovementPressLengthPreset.Custom;
                CounterPressLengthMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                PressLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressLengthVariancePercentChanged;

        public int CounterPressLengthMinimumMs
        {
            get => action.CounterMovementReleasePress.CounterPressLengthMinimumMs;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressLengthMinimumMs == value) return;
                action.CounterMovementReleasePress.ApplyMinimumAndMaximum(value, action.CounterMovementReleasePress.CounterPressLengthMaximumMs);
                action.CounterMovementReleasePress.PressLengthPreset = CounterMovementPressLengthPreset.Custom;
                CounterPressLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                PressLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressLengthMinimumMsChanged;

        public int CounterPressLengthMaximumMs
        {
            get => action.CounterMovementReleasePress.CounterPressLengthMaximumMs;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressLengthMaximumMs == value) return;
                action.CounterMovementReleasePress.ApplyMinimumAndMaximum(action.CounterMovementReleasePress.CounterPressLengthMinimumMs, value);
                action.CounterMovementReleasePress.PressLengthPreset = CounterMovementPressLengthPreset.Custom;
                CounterPressLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                PressLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressLengthMaximumMsChanged;

        private List<EnumChoiceSelection<CounterPressStartDelayMode>> startDelayModeItems =
            new List<EnumChoiceSelection<CounterPressStartDelayMode>>()
            {
                new EnumChoiceSelection<CounterPressStartDelayMode>("Fixed", CounterPressStartDelayMode.Fixed),
                new EnumChoiceSelection<CounterPressStartDelayMode>("Time Variance (%)", CounterPressStartDelayMode.WaitVariancePercentage),
                new EnumChoiceSelection<CounterPressStartDelayMode>("Time Variance (Range)", CounterPressStartDelayMode.MinimumAndMaximum),
            };
        public List<EnumChoiceSelection<CounterPressStartDelayMode>> StartDelayModeItems => startDelayModeItems;

        public CounterPressStartDelayMode CounterPressStartDelayMode
        {
            get => action.CounterMovementReleasePress.CounterPressStartDelayMode;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressStartDelayMode == value) return;
                action.CounterMovementReleasePress.CounterPressStartDelayMode = value;
                CounterPressStartDelayModeChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayModeDescriptionChanged?.Invoke(this, EventArgs.Empty);
                ShowStartDelayFixedModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowStartDelayWaitVariancePercentageModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ShowStartDelayMinimumAndMaximumModeFieldsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressStartDelayModeChanged;

        public string CounterPressStartDelayModeDescription
        {
            get
            {
                switch (action.CounterMovementReleasePress.CounterPressStartDelayMode)
                {
                    case CounterPressStartDelayMode.Fixed:
                        return "Uses the same neutral delay before every generated opposite press.";
                    case CounterPressStartDelayMode.WaitVariancePercentage:
                        return "Varies the neutral delay below and above the fixed value by the selected percentage.";
                    default:
                        return "Selects a neutral delay at random from the specified inclusive range.";
                }
            }
        }
        public event EventHandler CounterPressStartDelayModeDescriptionChanged;

        public bool ShowStartDelayFixedModeFields =>
            action.CounterMovementReleasePress.CounterPressStartDelayMode == CounterPressStartDelayMode.Fixed;
        public event EventHandler ShowStartDelayFixedModeFieldsChanged;

        public bool ShowStartDelayWaitVariancePercentageModeFields =>
            action.CounterMovementReleasePress.CounterPressStartDelayMode == CounterPressStartDelayMode.WaitVariancePercentage;
        public event EventHandler ShowStartDelayWaitVariancePercentageModeFieldsChanged;

        public bool ShowStartDelayMinimumAndMaximumModeFields =>
            action.CounterMovementReleasePress.CounterPressStartDelayMode == CounterPressStartDelayMode.MinimumAndMaximum;
        public event EventHandler ShowStartDelayMinimumAndMaximumModeFieldsChanged;

        public int CounterPressStartDelayMs
        {
            get => action.CounterMovementReleasePress.CounterPressStartDelayMs;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressStartDelayMs == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayFixedAndPercentage(value, action.CounterMovementReleasePress.CounterPressStartDelayVariancePercent);
                action.CounterMovementReleasePress.NormalizeRanges();
                CounterPressStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressStartDelayMsChanged;

        public int CounterPressStartDelayVariancePercent
        {
            get => action.CounterMovementReleasePress.CounterPressStartDelayVariancePercent;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressStartDelayVariancePercent == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayFixedAndPercentage(action.CounterMovementReleasePress.CounterPressStartDelayMs, value);
                action.CounterMovementReleasePress.NormalizeRanges();
                CounterPressStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressStartDelayVariancePercentChanged;

        public int CounterPressStartDelayMinimumMs
        {
            get => action.CounterMovementReleasePress.CounterPressStartDelayMinimumMs;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressStartDelayMinimumMs == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayMinimumAndMaximum(value, action.CounterMovementReleasePress.CounterPressStartDelayMaximumMs);
                CounterPressStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressStartDelayMinimumMsChanged;

        public int CounterPressStartDelayMaximumMs
        {
            get => action.CounterMovementReleasePress.CounterPressStartDelayMaximumMs;
            set
            {
                if (action.CounterMovementReleasePress.CounterPressStartDelayMaximumMs == value) return;
                action.CounterMovementReleasePress.ApplyStartDelayMinimumAndMaximum(action.CounterMovementReleasePress.CounterPressStartDelayMinimumMs, value);
                CounterPressStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayMsChanged?.Invoke(this, EventArgs.Empty);
                CounterPressStartDelayVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CounterPressStartDelayMaximumMsChanged;

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

        public bool HighlightCounterMovementReleasePressEnabled =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        public bool HighlightUseArrowKeysForCounterMovementPresses =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
        public bool HighlightPressLengthPreset =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_PRESET);
        public bool HighlightCounterPressLengthMode =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MODE);
        public bool HighlightCounterPressLengthMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_FIXED_MS);
        public bool HighlightCounterPressLengthVariancePercent =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_VARIANCE_PERCENT);
        public bool HighlightCounterPressLengthMinimumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MIN_MS);
        public bool HighlightCounterPressLengthMaximumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MAX_MS);
        public bool HighlightCounterPressStartDelayMode =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
        public bool HighlightCounterPressStartDelayMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
        public bool HighlightCounterPressStartDelayVariancePercent =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
        public bool HighlightCounterPressStartDelayMinimumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        public bool HighlightCounterPressStartDelayMaximumMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        public bool HighlightCounterMovementMinimumHoldMs =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_MIN_HOLD_MS);
        public bool HighlightRequiredStickDeflectionThreshold =>
            action.ParentAction == null || action.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);

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
            DiagonalZoneWidthChanged += StickAnalogEmulationPropViewModel_DiagonalZoneWidthChanged;
            DirectionPulseTimeMsChanged += StickAnalogEmulationPropViewModel_DirectionPulseTimeMsChanged;
            AnalogSpeedEmulationEnabledChanged += StickAnalogEmulationPropViewModel_AnalogSpeedEmulationEnabledChanged;
            AnalogEmulationActivePercentChanged += StickAnalogEmulationPropViewModel_AnalogEmulationActivePercentChanged;
            AnalogEmulationPulseTimeMsChanged += StickAnalogEmulationPropViewModel_AnalogEmulationPulseTimeMsChanged;
            FullSpeedThresholdPercentChanged += StickAnalogEmulationPropViewModel_FullSpeedThresholdPercentChanged;
            CounterMovementReleasePressEnabledChanged += StickAnalogEmulationPropViewModel_CounterMovementReleasePressEnabledChanged;
            UseArrowKeysForCounterMovementPressesChanged += StickAnalogEmulationPropViewModel_UseArrowKeysForCounterMovementPressesChanged;
            PressLengthPresetChanged += StickAnalogEmulationPropViewModel_PressLengthPresetChanged;
            CounterPressLengthModeChanged += StickAnalogEmulationPropViewModel_CounterPressLengthModeChanged;
            CounterPressLengthMsChanged += StickAnalogEmulationPropViewModel_CounterPressLengthMsChanged;
            CounterPressLengthVariancePercentChanged += StickAnalogEmulationPropViewModel_CounterPressLengthVariancePercentChanged;
            CounterPressLengthMinimumMsChanged += StickAnalogEmulationPropViewModel_CounterPressLengthMinimumMsChanged;
            CounterPressLengthMaximumMsChanged += StickAnalogEmulationPropViewModel_CounterPressLengthMaximumMsChanged;
            CounterPressStartDelayModeChanged += StickAnalogEmulationPropViewModel_CounterPressStartDelayModeChanged;
            CounterPressStartDelayMsChanged += StickAnalogEmulationPropViewModel_CounterPressStartDelayMsChanged;
            CounterPressStartDelayVariancePercentChanged += StickAnalogEmulationPropViewModel_CounterPressStartDelayVariancePercentChanged;
            CounterPressStartDelayMinimumMsChanged += StickAnalogEmulationPropViewModel_CounterPressStartDelayMinimumMsChanged;
            CounterPressStartDelayMaximumMsChanged += StickAnalogEmulationPropViewModel_CounterPressStartDelayMaximumMsChanged;
            CounterMovementMinimumHoldMsChanged += StickAnalogEmulationPropViewModel_CounterMovementMinimumHoldMsChanged;
            RequiredStickDeflectionThresholdPercentChanged += StickAnalogEmulationPropViewModel_RequiredStickDeflectionThresholdPercentChanged;
        }

        private void StickAnalogEmulationPropViewModel_CounterMovementReleasePressEnabledChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        }

        private void StickAnalogEmulationPropViewModel_PressLengthPresetChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_PRESET);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_PRESET);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressLengthModeChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MODE);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MODE);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressLengthMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_FIXED_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_FIXED_MS);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressLengthVariancePercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_VARIANCE_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_VARIANCE_PERCENT);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressLengthMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MIN_MS);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressLengthMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_PRESS_LENGTH_MAX_MS);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressStartDelayModeChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressStartDelayMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressStartDelayVariancePercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressStartDelayMinimumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        }

        private void StickAnalogEmulationPropViewModel_CounterPressStartDelayMaximumMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        }

        private void StickAnalogEmulationPropViewModel_CounterMovementMinimumHoldMsChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_MIN_HOLD_MS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_MIN_HOLD_MS);
        }

        private void StickAnalogEmulationPropViewModel_RequiredStickDeflectionThresholdPercentChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
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

        private void StickAnalogEmulationPropViewModel_UseArrowKeysForCounterMovementPressesChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
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
            IsEightWayDirectionResolutionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickAnalogEmulationPropViewModel_DiagonalZoneWidthChanged(object sender, EventArgs e)
        {
            action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIAGONAL_ZONE_WIDTH);
            action.RaiseNotifyPropertyChange(mapper, StickAnalogEmulationAction.PropertyKeyStrings.DIAGONAL_ZONE_WIDTH);
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

    public class StickAnalogDirectionBindItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly StickAnalogEmulationPropViewModel owner;

        public event PropertyChangedEventHandler PropertyChanged;

        public StickAnalogEmulationAction.DirSlot Direction { get; }
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

        public StickAnalogDirectionBindItem(StickAnalogEmulationPropViewModel owner,
            StickAnalogEmulationAction.DirSlot direction, string displayName, string subtitle)
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
}
