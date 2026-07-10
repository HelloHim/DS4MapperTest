using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels.Common;
using DS4MapperTest.ViewModels.GyroActionPropViewModels;

namespace DS4MapperTest.ViewModels.TouchpadActionPropViewModels
{
    public class TouchpadMousePropViewModel : TouchpadActionPropVMBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _modelReady = false;
        private bool _syncingAccelSens = false;
        private GyroMouseAccelCurveChoice _prevAccelCurve;
        private TouchpadMouse action;
        public TouchpadMouse Action => action;

        public string DeadZone
        {
            get => action.DeadZone.ToString();
            set
            {
                if (int.TryParse(value, out int temp))
                {
                    action.DeadZone = Math.Clamp(temp, 0, 10000);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                    ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler DeadZoneChanged;

        public string VerticalDeadZone
        {
            get => action.VerticalDeadZone.ToString();
            set
            {
                if (int.TryParse(value, out int temp))
                {
                    action.VerticalDeadZone = Math.Clamp(temp, 0, 10000);
                    VerticalDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                    ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler VerticalDeadZoneChanged;

        public double TrackpadAngleSnapDegrees
        {
            get => action.TrackpadAngleSnapDegrees;
            set
            {
                action.TrackpadAngleSnapDegrees = Math.Clamp(value, 0.0, 45.0);
                TrackpadAngleSnapDegreesChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler TrackpadAngleSnapDegreesChanged;

        public bool TrackpadSmoothAngleSnap
        {
            get => action.TrackpadSmoothAngleSnap;
            set
            {
                if (action.TrackpadSmoothAngleSnap == value) return;
                action.TrackpadSmoothAngleSnap = value;
                TrackpadSmoothAngleSnapChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler TrackpadSmoothAngleSnapChanged;

        private readonly List<AccelCurveChoiceItem> accelCurveChoiceItems =
            new List<AccelCurveChoiceItem>()
            {
                new AccelCurveChoiceItem("None", GyroMouseAccelCurveChoice.None),
                new AccelCurveChoiceItem("Linear", GyroMouseAccelCurveChoice.Linear),
                new AccelCurveChoiceItem("Quadratic", GyroMouseAccelCurveChoice.Quadratic),
                new AccelCurveChoiceItem("Cubic", GyroMouseAccelCurveChoice.Cubic),
                new AccelCurveChoiceItem("Power", GyroMouseAccelCurveChoice.Power),
                new AccelCurveChoiceItem("Natural", GyroMouseAccelCurveChoice.Natural),
            };
        public List<AccelCurveChoiceItem> AccelCurveChoiceItems =>
            accelCurveChoiceItems;

        public GyroMouseAccelCurveChoice AccelCurveChoice
        {
            get => action.AccelCurve;
            set
            {
                if (!_modelReady) return;
                if (action.AccelCurve == value) return;
                _prevAccelCurve = action.AccelCurve;
                action.AccelCurve = value;
                MarkAccelerationProperty(TouchpadMouse.PropertyKeyStrings.ACCEL_CURVE);
                if (!_syncingAccelSens &&
                    _prevAccelCurve == GyroMouseAccelCurveChoice.None &&
                    value != GyroMouseAccelCurveChoice.None)
                {
                    try
                    {
                        _syncingAccelSens = true;
                        SyncTrackpadMinimumsFromBase();
                    }
                    finally
                    {
                        _syncingAccelSens = false;
                    }
                }
                else if (_prevAccelCurve != GyroMouseAccelCurveChoice.None &&
                    value == GyroMouseAccelCurveChoice.None)
                {
                    MarkAccelerationProperty(
                        TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360);
                    MarkAccelerationProperty(
                        TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
                }
                RaiseAccelerationPropertyChanges();
            }
        }

        public bool AccelCurveUsed =>
            action.AccelCurve != GyroMouseAccelCurveChoice.None;
        public bool UsesMaxThreshold =>
            action.AccelCurve == GyroMouseAccelCurveChoice.Linear ||
            action.AccelCurve == GyroMouseAccelCurveChoice.Quadratic ||
            action.AccelCurve == GyroMouseAccelCurveChoice.Cubic;
        public bool NaturalCurveUsed =>
            action.AccelCurve == GyroMouseAccelCurveChoice.Natural;
        public bool PowerCurveUsed =>
            action.AccelCurve == GyroMouseAccelCurveChoice.Power;

        public double MinAccelXSens
        {
            get => action.MinAccelXSens;
            set
            {
                if (!_modelReady) return;
                SetAccelerationValue(
                    Math.Clamp(value, 0.0, 100.0),
                    () => action.MinAccelXSens,
                    v => action.MinAccelXSens = v,
                    TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS,
                    nameof(MinAccelXSens));
                if (!_syncingAccelSens)
                {
                    try
                    {
                        _syncingAccelSens = true;
                        double newSwipes = Math.Clamp(action.MinAccelXSens, 0.0, 100.0);
                        if (action.SwipesPer360 != newSwipes)
                        {
                            double verticalRws = VerticalRws;
                            action.SwipesPer360 = newSwipes;
                            action.VerticalScale = Math.Abs(newSwipes) < 1e-10
                                ? verticalRws
                                : Math.Clamp(verticalRws / newSwipes, 0.0, 10.0);
                            MarkAccelerationProperty(
                                TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360);
                            MarkAccelerationProperty(
                                TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
                            PropertyChanged?.Invoke(this,
                                new PropertyChangedEventArgs(nameof(SwipesPer360)));
                            PropertyChanged?.Invoke(this,
                                new PropertyChangedEventArgs(nameof(VerticalScale)));
                            PropertyChanged?.Invoke(this,
                                new PropertyChangedEventArgs(nameof(VerticalRws)));
                            PropertyChanged?.Invoke(this,
                                new PropertyChangedEventArgs(nameof(LegacySensitivity)));
                            HighlightSwipesPer360Changed?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    finally
                    {
                        _syncingAccelSens = false;
                    }
                }
            }
        }

        public double MaxAccelXSens
        {
            get => action.MaxAccelXSens;
            set => SetAccelerationValue(
                Math.Clamp(value, 0.0, 100.0),
                () => action.MaxAccelXSens,
                v => action.MaxAccelXSens = v,
                TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS,
                nameof(MaxAccelXSens));
        }

        public double MinAccelYSens
        {
            get => action.MinAccelYSens;
            set
            {
                if (!_modelReady) return;
                SetAccelerationValue(
                    Math.Clamp(value, 0.0, 100.0),
                    () => action.MinAccelYSens,
                    v => action.MinAccelYSens = v,
                    TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS,
                    nameof(MinAccelYSens));
                if (!_syncingAccelSens)
                {
                    try
                    {
                        _syncingAccelSens = true;
                        double swipesPer360 = action.SwipesPer360;
                        double newVScale = Math.Abs(swipesPer360) < 1e-10
                            ? action.MinAccelYSens
                            : action.MinAccelYSens / swipesPer360;
                        newVScale = Math.Clamp(newVScale, 0.0, 10.0);
                        if (action.VerticalScale != newVScale)
                        {
                            action.VerticalScale = newVScale;
                            MarkAccelerationProperty(
                                TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
                            PropertyChanged?.Invoke(this,
                                new PropertyChangedEventArgs(nameof(VerticalScale)));
                            PropertyChanged?.Invoke(this,
                                new PropertyChangedEventArgs(nameof(VerticalRws)));
                            HighlightVerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    finally
                    {
                        _syncingAccelSens = false;
                    }
                }
            }
        }

        public double MaxAccelYSens
        {
            get => action.MaxAccelYSens;
            set => SetAccelerationValue(
                Math.Clamp(value, 0.0, 100.0),
                () => action.MaxAccelYSens,
                v => action.MaxAccelYSens = v,
                TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS,
                nameof(MaxAccelYSens));
        }

        public double MinAccelThreshold
        {
            get => action.MinAccelThreshold;
            set => SetAccelerationValue(
                Math.Clamp(value, 0.0, 10000.0),
                () => action.MinAccelThreshold,
                v => action.MinAccelThreshold = v,
                TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_THRESHOLD,
                nameof(MinAccelThreshold));
        }

        public double MaxAccelThreshold
        {
            get => action.MaxAccelThreshold;
            set => SetAccelerationValue(
                Math.Clamp(value, 0.0, 10000.0),
                () => action.MaxAccelThreshold,
                v => action.MaxAccelThreshold = v,
                TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_THRESHOLD,
                nameof(MaxAccelThreshold));
        }

        public double NaturalVHalf
        {
            get => action.NaturalVHalf;
            set => SetAccelerationValue(
                Math.Clamp(value, 0.01, 10000.0),
                () => action.NaturalVHalf,
                v => action.NaturalVHalf = v,
                TouchpadMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF,
                nameof(NaturalVHalf));
        }

        public double PowerVRef
        {
            get => action.PowerVRef;
            set => SetAccelerationValue(
                Math.Clamp(value, 0.01, 10000.0),
                () => action.PowerVRef,
                v => action.PowerVRef = v,
                TouchpadMouse.PropertyKeyStrings.POWER_CURVE_VREF,
                nameof(PowerVRef));
        }

        public double PowerExponent
        {
            get => action.PowerExponent;
            set => SetAccelerationValue(
                Math.Clamp(value, 0.01, 100.0),
                () => action.PowerExponent,
                v => action.PowerExponent = v,
                TouchpadMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT,
                nameof(PowerExponent));
        }

        public bool TrackballEnabled
        {
            get => action.TrackballEnabled;
            set
            {
                action.TrackballEnabled = value;
                TrackballEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler TrackballEnabledChanged;

        public string TrackballFriction
        {
            get => action.TrackballFriction.ToString();
            set
            {
                if (int.TryParse(value, out int temp))
                {
                    action.TrackballFriction = Math.Clamp(temp, 0, 100);
                    TrackballFrictionChanged?.Invoke(this, EventArgs.Empty);
                    ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler TrackballFrictionChanged;

        // --- Calibration fields (profile-level, synced across all actions) ---

        public CalibMode CalibMode
        {
            get => mapper.ActionProfile.CalibMode;
            set
            {
                if (!_modelReady) return;
                if (mapper.ActionProfile.CalibMode == value) return;
                mapper.ActionProfile.CalibMode = value;
                RaiseCalibModePropertyChanges();
                SyncCalibToProfile();
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsRwcMode
        {
            get => CalibMode == DS4MapperTest.CalibMode.RwcMode;
            set
            {
                if (value) CalibMode = DS4MapperTest.CalibMode.RwcMode;
            }
        }

        public bool IsCountsMode
        {
            get => CalibMode == DS4MapperTest.CalibMode.CountsMode;
            set
            {
                if (value) CalibMode = DS4MapperTest.CalibMode.CountsMode;
            }
        }

        public string MasterCalibrationLabel => IsCountsMode ? "Counts" : "RWC";

        public double MasterCalibrationValue
        {
            get => IsCountsMode ? FullTurnCounts : RealWorldCalibration;
            set
            {
                if (IsCountsMode)
                {
                    FullTurnCounts = value;
                }
                else
                {
                    RealWorldCalibration = value;
                }
            }
        }

        private double fullTurnCounts = 10.0;
        public double FullTurnCounts
        {
            get => fullTurnCounts;
            set
            {
                if (!_modelReady) return;
                if (value == 0.0) return;
                bool countsChanged = fullTurnCounts != value;
                fullTurnCounts = value;
                CalculateTestRWC();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivity)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
                if (!countsChanged) return;
                if (IsCountsMode)
                {
                    CalculateRwcFromCounts();
                    SyncCalibToProfile();
                }
            }
        }

        public double RealWorldCalibration
        {
            get => mapper.ActionProfile.CalibRwc;
            set
            {
                if (!_modelReady) return;
                if (mapper.ActionProfile.CalibRwc == value) return;
                mapper.ActionProfile.CalibRwc = value;
                if (!_applyingPreset) TryMatchPreset();
                RealWorldCalibrationChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
                SyncCalibToProfile();
            }
        }
        public event EventHandler RealWorldCalibrationChanged;

        public double InGameSens
        {
            get => mapper.ActionProfile.CalibInGameSens;
            set
            {
                if (!_modelReady) return;
                if (mapper.ActionProfile.CalibInGameSens == value) return;
                mapper.ActionProfile.CalibInGameSens = value;
                if (IsCountsMode) CalculateRwcFromCounts();
                InGameSensChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                SyncCalibToProfile();
            }
        }
        public event EventHandler InGameSensChanged;

        private double calculatedRWC = 0.0;
        public double CalculatedRWC
        {
            get => calculatedRWC;
            set
            {
                if (calculatedRWC == value) return;
                calculatedRWC = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalculatedRWC)));
            }
        }

        private BasicActionCommand copyTestRWCComm;
        public BasicActionCommand CopyTestRWCComm => copyTestRWCComm;
        private BasicActionCommand resetAccelerationDefaultsComm;
        public BasicActionCommand ResetAccelerationDefaultsComm =>
            resetAccelerationDefaultsComm;

        private bool _applyingPreset = false;
        private GameCalibPreset _selectedPreset = GameCalibPreset.Custom;

        public IReadOnlyList<GameCalibPreset> GamePresets => GameCalibPreset.All;

        public GameCalibPreset SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset == value) return;
                _selectedPreset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
                if (value == null || value.IsCustom) return;
                _applyingPreset = true;
                if (IsCountsMode)
                {
                    FullTurnCounts = value.RWC * 360.0 / InGameSens;
                }
                else
                {
                    InGameSens = value.InGameSens;
                    RealWorldCalibration = value.RWC;
                }
                _applyingPreset = false;
            }
        }

        private void SetSelectedPresetCustom()
        {
            _selectedPreset = GameCalibPreset.Custom;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        }

        private void TryMatchPreset()
        {
            double rwc = mapper.ActionProfile.CalibRwc;
            double sens = mapper.ActionProfile.CalibInGameSens;
            GameCalibPreset match = GameCalibPreset.All.FirstOrDefault(
                p => !p.IsCustom &&
                     Math.Abs(p.RWC - rwc) < 1e-3 &&
                     Math.Abs(p.InGameSens - sens) < 1e-3);
            GameCalibPreset next = match ?? GameCalibPreset.Custom;
            if (_selectedPreset == next) return;
            _selectedPreset = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        }

        private void CalculateTestRWC()
        {
            CalculatedRWC = InGameSens / (360.0 / fullTurnCounts);
        }

        private void CalculateRwcFromCounts()
        {
            double rwc = fullTurnCounts * InGameSens / 360.0;
            if (mapper.ActionProfile.CalibRwc == rwc) return;
            mapper.ActionProfile.CalibRwc = rwc;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
        }

        public double SwipesPer360
        {
            get => action.SwipesPer360;
            set
            {
                if (!_modelReady) return;
                double swipesPer360 = Math.Clamp(value, 0.0, 100.0);
                if (action.SwipesPer360 == swipesPer360) return;

                double verticalRws = VerticalRws;
                action.SwipesPer360 = swipesPer360;
                if (VerticalScaleIsAbsoluteMode)
                {
                    action.VerticalScale = Math.Abs(swipesPer360) < 1e-10
                        ? verticalRws
                        : Math.Clamp(verticalRws / swipesPer360, 0.0, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(VerticalScale)));
                }
                SwipesPer360Changed?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SwipesPer360)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalRws)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivity)));
            }
        }
        public event EventHandler SwipesPer360Changed;

        private const double LEGACY_MOUSE_SCALE = 0.0132;

        private bool _legacySensitivityEditable = false;
        public bool LegacySensitivityEditable
        {
            get => _legacySensitivityEditable;
            set
            {
                if (_legacySensitivityEditable == value) return;
                _legacySensitivityEditable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivityEditable)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivityReadOnly)));
                if (!value)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivity)));
            }
        }

        public bool LegacySensitivityReadOnly => !_legacySensitivityEditable;

        public double LegacySensitivity
        {
            get
            {
                double counts = mapper.ActionProfile.CalibCounts;
                if (counts <= 0.0) return 0.0;
                return (action.SwipesPer360 * counts) / (LEGACY_MOUSE_SCALE * 65535.0);
            }
            set
            {
                if (!_modelReady) return;
                double counts = mapper.ActionProfile.CalibCounts;
                if (counts <= 0.0) return;
                double swipes = (value * LEGACY_MOUSE_SCALE * 65535.0) / counts;
                SwipesPer360 = Math.Clamp(swipes, 0.0, 100.0);
                LegacySensitivityEditable = false;
            }
        }

        // --- End calibration fields ---

        public double VerticalScale
        {
            get => action.VerticalScale;
            set
            {
                if (!_modelReady) return;
                double verticalScale = Math.Clamp(value, 0.0, 10.0);
                if (action.VerticalScale == verticalScale) return;
                action.VerticalScale = verticalScale;
                VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler VerticalScaleChanged;

        public double VerticalRws
        {
            get => Math.Round(action.SwipesPer360 * action.VerticalScale, 4);
            set
            {
                if (!_modelReady) return;
                double swipesPer360 = action.SwipesPer360;
                double verticalScale = Math.Abs(swipesPer360) < 1e-10
                    ? value
                    : value / swipesPer360;
                verticalScale = Math.Clamp(verticalScale, 0.0, 10.0);
                if (action.VerticalScale == verticalScale) return;
                action.VerticalScale = verticalScale;
                VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool verticalScaleIsAbsoluteMode = true;
        public bool VerticalScaleIsAbsoluteMode
        {
            get => verticalScaleIsAbsoluteMode;
            set
            {
                if (!value || verticalScaleIsAbsoluteMode) return;
                verticalScaleIsAbsoluteMode = true;
                NotifyVerticalScaleModeChanged();
            }
        }

        public bool VerticalScaleIsMultiplierMode
        {
            get => !verticalScaleIsAbsoluteMode;
            set
            {
                if (!value || !verticalScaleIsAbsoluteMode) return;
                verticalScaleIsAbsoluteMode = false;
                NotifyVerticalScaleModeChanged();
            }
        }

        private void NotifyVerticalScaleModeChanged()
        {
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(VerticalScaleIsAbsoluteMode)));
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(VerticalScaleIsMultiplierMode)));
        }

        public bool SmoothingEnabled
        {
            get => action.SmoothingEnabled;
            set
            {
                if (action.SmoothingEnabled == value) return;
                action.SmoothingEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SmoothingEnabled)));
                SmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SmoothingEnabledChanged;

        private List<SmoothPresetChoiceItem> smoothPresetChoiceItems = new List<SmoothPresetChoiceItem>()
        {
            new SmoothPresetChoiceItem("", SmoothPresetChoices.None, 1.0, 1.0),
            new SmoothPresetChoiceItem("Stiff", SmoothPresetChoices.Stiff, 0.4, 0.6),
            new SmoothPresetChoiceItem("Normie", SmoothPresetChoices.Normie, 1.0, 1.0),
            new SmoothPresetChoiceItem("Loose", SmoothPresetChoices.Loose, 1.5, 0.8),
        };
        public List<SmoothPresetChoiceItem> SmoothPresetChoiceItems => smoothPresetChoiceItems;

        private readonly List<EnumChoiceSelection<TouchpadStabilityMode>> stabilityModeItems =
            new List<EnumChoiceSelection<TouchpadStabilityMode>>()
            {
                new EnumChoiceSelection<TouchpadStabilityMode>("Off", TouchpadStabilityMode.Off),
                new EnumChoiceSelection<TouchpadStabilityMode>("Feather", TouchpadStabilityMode.Feather),
                new EnumChoiceSelection<TouchpadStabilityMode>("Gentle", TouchpadStabilityMode.Gentle),
                new EnumChoiceSelection<TouchpadStabilityMode>("Mild", TouchpadStabilityMode.Mild),
                new EnumChoiceSelection<TouchpadStabilityMode>("Light", TouchpadStabilityMode.Light),
                new EnumChoiceSelection<TouchpadStabilityMode>("Balanced", TouchpadStabilityMode.Balanced),
                new EnumChoiceSelection<TouchpadStabilityMode>("Steady", TouchpadStabilityMode.Steady),
                new EnumChoiceSelection<TouchpadStabilityMode>("Strong", TouchpadStabilityMode.Strong),
                new EnumChoiceSelection<TouchpadStabilityMode>("Custom", TouchpadStabilityMode.Custom),
            };
        public List<EnumChoiceSelection<TouchpadStabilityMode>> StabilityModeItems =>
            stabilityModeItems;

        private bool stabilityAdvancedExpanded;
        public bool StabilityAdvancedExpanded
        {
            get => stabilityAdvancedExpanded;
            set
            {
                if (stabilityAdvancedExpanded == value) return;
                stabilityAdvancedExpanded = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(StabilityAdvancedExpanded)));
            }
        }

        public TouchpadStabilityMode StabilityMode
        {
            get => action.StabilitySettings.Mode;
            set
            {
                if (action.StabilitySettings.Mode == value) return;
                if (action.StabilitySettings.Mode == TouchpadStabilityMode.Custom)
                {
                    action.StabilitySettings.CaptureCustomPreset();
                }

                if (value == TouchpadStabilityMode.Custom)
                {
                    action.StabilitySettings.RestoreCustomPreset();
                    action.StabilitySettings.Mode = TouchpadStabilityMode.Custom;
                }
                else
                {
                    action.StabilitySettings.ApplyPreset(value);
                }

                StabilityModeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                RaiseStabilityPropertyChanges();
            }
        }
        public event EventHandler StabilityModeChanged;

        public double StabilityTouchSettleMs
        {
            get => action.StabilitySettings.TouchSettleMs;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 30.0),
                () => action.StabilitySettings.TouchSettleMs,
                v => action.StabilitySettings.TouchSettleMs = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_TOUCH_SETTLE,
                nameof(StabilityTouchSettleMs));
        }

        public double StabilityBaseNoiseFloor
        {
            get => action.StabilitySettings.BaseNoiseFloor;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 100.0),
                () => action.StabilitySettings.BaseNoiseFloor,
                v => action.StabilitySettings.BaseNoiseFloor = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE,
                nameof(StabilityBaseNoiseFloor));
        }

        public double StabilityHysteresisExitMultiplier
        {
            get => action.StabilitySettings.HysteresisExitMultiplier;
            set => SetStabilityValue(Math.Clamp(value, 1.0, 3.0),
                () => action.StabilitySettings.HysteresisExitMultiplier,
                v => action.StabilitySettings.HysteresisExitMultiplier = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE,
                nameof(StabilityHysteresisExitMultiplier));
        }

        public double StabilityFastPassthroughThreshold
        {
            get => action.StabilitySettings.FastPassthroughThreshold;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 200.0),
                () => action.StabilitySettings.FastPassthroughThreshold,
                v => action.StabilitySettings.FastPassthroughThreshold = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE,
                nameof(StabilityFastPassthroughThreshold));
        }

        public bool StabilityEdgeGuardEnabled
        {
            get => action.StabilitySettings.EdgeGuardEnabled;
            set => SetStabilityValue(value,
                () => action.StabilitySettings.EdgeGuardEnabled,
                v => action.StabilitySettings.EdgeGuardEnabled = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD,
                nameof(StabilityEdgeGuardEnabled));
        }

        public double StabilityLeftEdgePercent
        {
            get => action.StabilitySettings.LeftEdgePercent;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 30.0),
                () => action.StabilitySettings.LeftEdgePercent,
                v => action.StabilitySettings.LeftEdgePercent = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD,
                nameof(StabilityLeftEdgePercent));
        }

        public double StabilityTopEdgePercent
        {
            get => action.StabilitySettings.TopEdgePercent;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 30.0),
                () => action.StabilitySettings.TopEdgePercent,
                v => action.StabilitySettings.TopEdgePercent = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD,
                nameof(StabilityTopEdgePercent));
        }

        public double StabilityTopLeftCornerMultiplier
        {
            get => action.StabilitySettings.TopLeftCornerMultiplier;
            set => SetStabilityValue(Math.Clamp(value, 1.0, 6.0),
                () => action.StabilitySettings.TopLeftCornerMultiplier,
                v => action.StabilitySettings.TopLeftCornerMultiplier = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD,
                nameof(StabilityTopLeftCornerMultiplier));
        }

        public bool StabilityEdgeStartGateEnabled
        {
            get => action.StabilitySettings.EdgeStartGateEnabled;
            set => SetStabilityValue(value,
                () => action.StabilitySettings.EdgeStartGateEnabled,
                v => action.StabilitySettings.EdgeStartGateEnabled = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_START_GATE,
                nameof(StabilityEdgeStartGateEnabled));
        }

        public double StabilityEdgeStartThreshold
        {
            get => action.StabilitySettings.EdgeStartThreshold;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 300.0),
                () => action.StabilitySettings.EdgeStartThreshold,
                v => action.StabilitySettings.EdgeStartThreshold = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_START_GATE,
                nameof(StabilityEdgeStartThreshold));
        }

        public bool StabilityEdgeLockEnabled
        {
            get => action.StabilitySettings.EdgeLockEnabled;
            set => SetStabilityValue(value,
                () => action.StabilitySettings.EdgeLockEnabled,
                v => action.StabilitySettings.EdgeLockEnabled = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD,
                nameof(StabilityEdgeLockEnabled));
        }

        public bool StabilityStationaryHoldEnabled
        {
            get => action.StabilitySettings.StationaryHoldEnabled;
            set => SetStabilityValue(value,
                () => action.StabilitySettings.StationaryHoldEnabled,
                v => action.StabilitySettings.StationaryHoldEnabled = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY,
                nameof(StabilityStationaryHoldEnabled));
        }

        public double StabilityStationaryDetectionMs
        {
            get => action.StabilitySettings.StationaryDetectionMs;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 200.0),
                () => action.StabilitySettings.StationaryDetectionMs,
                v => action.StabilitySettings.StationaryDetectionMs = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY,
                nameof(StabilityStationaryDetectionMs));
        }

        public double StabilityStationaryNoiseMultiplier
        {
            get => action.StabilitySettings.StationaryNoiseMultiplier;
            set => SetStabilityValue(Math.Clamp(value, 1.0, 4.0),
                () => action.StabilitySettings.StationaryNoiseMultiplier,
                v => action.StabilitySettings.StationaryNoiseMultiplier = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY,
                nameof(StabilityStationaryNoiseMultiplier));
        }

        public double StabilityStationaryBreakoutThreshold
        {
            get => action.StabilitySettings.StationaryBreakoutThreshold;
            set => SetStabilityValue(Math.Clamp(value, 0.0, 200.0),
                () => action.StabilitySettings.StationaryBreakoutThreshold,
                v => action.StabilitySettings.StationaryBreakoutThreshold = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY,
                nameof(StabilityStationaryBreakoutThreshold));
        }

        public bool StabilityDeltaClampEnabled
        {
            get => action.StabilitySettings.DeltaClampEnabled;
            set => SetStabilityValue(value,
                () => action.StabilitySettings.DeltaClampEnabled,
                v => action.StabilitySettings.DeltaClampEnabled = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_DELTA_CLAMP,
                nameof(StabilityDeltaClampEnabled));
        }

        public double StabilityMaxDeltaPerFrame
        {
            get => action.StabilitySettings.MaxDeltaPerFrame;
            set => SetStabilityValue(Math.Clamp(value, 10.0, 500.0),
                () => action.StabilitySettings.MaxDeltaPerFrame,
                v => action.StabilitySettings.MaxDeltaPerFrame = v,
                TouchpadMouse.PropertyKeyStrings.STABILITY_DELTA_CLAMP,
                nameof(StabilityMaxDeltaPerFrame));
        }

        private SmoothPresetChoices smoothPresetChoice = SmoothPresetChoices.None;
        public SmoothPresetChoices SmoothPresetChoice
        {
            get => smoothPresetChoice;
            set
            {
                if (!_modelReady) return;
                if (smoothPresetChoice == value) return;
                smoothPresetChoice = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(SmoothPresetChoice)));
                SmoothPresetChoiceItem currentItem =
                    smoothPresetChoiceItems.FirstOrDefault(t => t.Choice == value);
                if (currentItem != null)
                {
                    SmoothingMinCutoff = currentItem.MinCutoffValue;
                    SmoothingBeta = currentItem.BetaValue;
                }
            }
        }

        public double SmoothingMinCutoff
        {
            get => action.ActionSmoothingSettings.minCutOff;
            set
            {
                if (!_modelReady) return;
                double minCutoff = Math.Clamp(value, 0.0, 10.0);
                if (action.ActionSmoothingSettings.minCutOff == minCutoff)
                    return;
                action.ActionSmoothingSettings.minCutOff = minCutoff;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(SmoothingMinCutoff)));
                SmoothingMinCutoffChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SmoothingMinCutoffChanged;

        public double SmoothingBeta
        {
            get => action.ActionSmoothingSettings.beta;
            set
            {
                if (!_modelReady) return;
                double beta = Math.Clamp(value, 0.0, 1.0);
                if (action.ActionSmoothingSettings.beta == beta) return;
                action.ActionSmoothingSettings.beta = beta;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(SmoothingBeta)));
                SmoothingBetaChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SmoothingBetaChanged;

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightVerticalDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
        }
        public event EventHandler HighlightVerticalDeadZoneChanged;

        public bool HighlightTrackpadAngleSnapDegrees
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
        }
        public event EventHandler HighlightTrackpadAngleSnapDegreesChanged;

        public bool HighlightTrackpadSmoothAngleSnap
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
        }
        public event EventHandler HighlightTrackpadSmoothAngleSnapChanged;

        public bool HighlightTrackballEnabled
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.TRACKBALL_MODE);
        }
        public event EventHandler HighlightTrackballEnabledChanged;

        public bool HighlightTrackballFriction
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.TRACKBALL_FRICTION);
        }
        public event EventHandler HighlightTrackballFrictionChanged;

        public bool HighlightSwipesPer360
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360);
        }
        public event EventHandler HighlightSwipesPer360Changed;

        public bool HighlightVerticalScale
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
        }
        public event EventHandler HighlightVerticalScaleChanged;

        public bool HighlightSmoothingEnabled
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
        }
        public event EventHandler HighlightSmoothingEnabledChanged;

        public bool HighlightSmoothingFilter
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
        }
        public event EventHandler HighlightSmoothingFilterChanged;

        public override event EventHandler ActionPropertyChanged;

        public TouchpadMousePropViewModel(Mapper mapper, TouchpadMapAction action)
        {
            this.mapper = mapper;
            this.action = action as TouchpadMouse;
            this.baseAction = action;

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                TouchpadMouse baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as TouchpadMouse;
                TouchpadMouse tempAction = new TouchpadMouse();
                tempAction.SoftCopyFromParent(baseLayerAction);
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;

                this.action = tempAction;
                this.baseAction = this.action;
                usingRealAction = false;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            PopulateModel();
            _prevAccelCurve = this.action.AccelCurve;

            copyTestRWCComm = new BasicActionCommand((parameter) =>
            {
                RealWorldCalibration = CalculatedRWC;
            });
            resetAccelerationDefaultsComm = new BasicActionCommand((parameter) =>
            {
                _syncingAccelSens = true;
                try
                {
                    this.action.SwipesPer360 = TouchpadMouse.DEFAULT_SWIPES_PER_360;
                    this.action.VerticalScale = TouchpadMouse.DEFAULT_VERTICAL_SCALE;
                    this.action.MinAccelXSens = Math.Clamp(this.action.SwipesPer360, 0.0, 100.0);
                    this.action.MinAccelYSens = VerticalRws;
                    this.action.MaxAccelXSens = TouchpadMouse.DEFAULT_MAX_ACCEL_SENS;
                    this.action.MaxAccelYSens = TouchpadMouse.DEFAULT_MAX_ACCEL_SENS;
                    foreach (string key in new[]
                    {
                        TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360,
                        TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE,
                        TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS,
                        TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS,
                        TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS,
                        TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS,
                    })
                    {
                        if (!this.action.ChangedProperties.Contains(key))
                        {
                            this.action.ChangedProperties.Add(key);
                        }
                        this.action.RaiseNotifyPropertyChange(mapper, key);
                    }
                    AccelCurveChoice = TouchpadMouse.DEFAULT_ACCEL_CURVE;
                    MinAccelThreshold = TouchpadMouse.DEFAULT_MIN_ACCEL_THRESHOLD;
                    MaxAccelThreshold = TouchpadMouse.DEFAULT_MAX_ACCEL_THRESHOLD;
                    NaturalVHalf = TouchpadMouse.DEFAULT_NATURAL_VHALF;
                    PowerVRef = TouchpadMouse.DEFAULT_POWER_VREF;
                    PowerExponent = TouchpadMouse.DEFAULT_POWER_EXPONENT;
                }
                finally
                {
                    _syncingAccelSens = false;
                }
                RaiseAccelerationPropertyChanges();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SwipesPer360)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalRws)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivity)));
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            });

            NameChanged += TouchpadMousePropViewModel_NameChanged;
            DeadZoneChanged += TouchpadMousePropViewModel_DeadZoneChanged;
            VerticalDeadZoneChanged += TouchpadMousePropViewModel_VerticalDeadZoneChanged;
            TrackpadAngleSnapDegreesChanged += TouchpadMousePropViewModel_TrackpadAngleSnapDegreesChanged;
            TrackpadSmoothAngleSnapChanged += TouchpadMousePropViewModel_TrackpadSmoothAngleSnapChanged;
            TrackballEnabledChanged += TouchpadMousePropViewModel_TrackballEnabledChanged;
            TrackballFrictionChanged += TouchpadMousePropViewModel_TrackballFrictionChanged;
            SwipesPer360Changed += TouchpadMousePropViewModel_SwipesPer360Changed;
            VerticalScaleChanged += TouchpadMousePropViewModel_VerticalScaleChanged;
            SmoothingEnabledChanged += TouchpadMousePropViewModel_SmoothingEnabledChanged;
            SmoothingMinCutoffChanged += TouchpadMousePropViewModel_SmoothingMinCutoffChanged;
            SmoothingBetaChanged += TouchpadMousePropViewModel_SmoothingBetaChanged;
            StabilityModeChanged += TouchpadMousePropViewModel_StabilityModeChanged;
            ActionPropertyChanged += SetProfileDirty;
            mapper.ActionProfile.CalibModeChanged += ActionProfile_CalibModeChanged;

            double savedSwipesPer360 = this.action.SwipesPer360;
            double savedVerticalScale = this.action.VerticalScale;
            double savedInGameSens = mapper.ActionProfile.CalibInGameSens;
            double savedRwc = mapper.ActionProfile.CalibRwc;
            double savedCounts = fullTurnCounts;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    this.action.SwipesPer360 = savedSwipesPer360;
                    this.action.VerticalScale = savedVerticalScale;
                    mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                    mapper.ActionProfile.CalibRwc = savedRwc;
                    fullTurnCounts = savedCounts;
                    CalculateTestRWC();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SwipesPer360)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivity)));
                    RaiseCalibModePropertyChanges();
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            this.action.SwipesPer360 = savedSwipesPer360;
                            this.action.VerticalScale = savedVerticalScale;
                            mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                            mapper.ActionProfile.CalibRwc = savedRwc;
                            fullTurnCounts = savedCounts;
                            CalculateTestRWC();
                            _modelReady = true;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SwipesPer360)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacySensitivity)));
                            RaiseCalibModePropertyChanges();
                        }));
                }));
        }

        private void PopulateModel()
        {
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0 ? mapper.ActionProfile.CalibCounts : fullTurnCounts;
            CalculateTestRWC();
        }

        private void SyncCalibToProfile()
        {
            double inGameSens = mapper.ActionProfile.CalibInGameSens;
            double rwc = IsCountsMode
                ? fullTurnCounts * inGameSens / 360.0
                : mapper.ActionProfile.CalibRwc;
            double counts = IsCountsMode || inGameSens <= 0.0
                ? fullTurnCounts
                : rwc * 360.0 / inGameSens;
            mapper.ActionProfile.CalibRwc = rwc;
            mapper.ActionProfile.CalibInGameSens = inGameSens;
            mapper.ActionProfile.CalibCounts = counts;
            ExecuteInMapperThread(() =>
            {
                foreach (var set in mapper.ActionProfile.ActionSets)
                    foreach (var layer in set.ActionLayers)
                        foreach (var mapAction in layer.normalActionDict.Values)
                        {
                            if (mapAction is ButtonAction btnAction)
                                foreach (var func in btnAction.ActionFuncs)
                                    foreach (var data in func.OutputActions)
                                        if (data.OutputType == OutputActionData.ActionType.CameraTurn)
                                            data.cameraTurnCounts360 = counts;
                            if (mapAction is StickFlickStick sfs)
                            {
                                sfs.RealWorldCalibration = rwc;
                                sfs.InGameSens = inGameSens;
                            }
                            if (mapAction is TouchpadFlickStick tfs)
                            {
                                tfs.RealWorldCalibration = rwc;
                                tfs.InGameSens = inGameSens;
                            }
                        }
            });
        }

        private void RaiseCalibModePropertyChanges()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalibMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRwcMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCountsMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
        }

        private void ActionProfile_CalibModeChanged(object sender, EventArgs e)
        {
            RaiseCalibModePropertyChanges();
            if (IsCountsMode)
            {
                CalculateTestRWC();
            }
        }

        private void TouchpadMousePropViewModel_SmoothingMinCutoffChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
                action.ActionSmoothingSettings.UpdateSmoothingFilters();
            });

            HighlightSmoothingFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadMousePropViewModel_StabilityModeChanged(object sender, EventArgs e)
        {
            MarkStabilityProperty(TouchpadMouse.PropertyKeyStrings.STABILITY_MODE);
            if (action.StabilitySettings.Mode == TouchpadStabilityMode.Custom)
            {
                MarkAllStabilityValueProperties();
            }
        }

        private void SetStabilityValue<T>(T value, Func<T> getter,
            Action<T> setter, string propertyKey, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(getter(), value)) return;
            setter(value);

            TouchpadStabilityMode previousMode = action.StabilitySettings.Mode;
            if (action.StabilitySettings.TryMatchPreset(out TouchpadStabilityMode matchedMode))
            {
                action.StabilitySettings.Mode = matchedMode;
            }
            else
            {
                action.StabilitySettings.Mode = TouchpadStabilityMode.Custom;
                action.StabilitySettings.CaptureCustomPreset();
            }

            if (previousMode != action.StabilitySettings.Mode)
            {
                MarkStabilityProperty(TouchpadMouse.PropertyKeyStrings.STABILITY_MODE);
            }

            MarkStabilityProperty(propertyKey);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            RaiseStabilityPropertyChanges();
            ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
        }

        private void MarkStabilityProperty(string propertyKey)
        {
            if (!action.ChangedProperties.Contains(propertyKey))
            {
                action.ChangedProperties.Add(propertyKey);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, propertyKey);
            });
        }

        private void MarkAllStabilityValueProperties()
        {
            foreach (string key in new[]
            {
                TouchpadMouse.PropertyKeyStrings.STABILITY_TOUCH_SETTLE,
                TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD,
                TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_START_GATE,
                TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY,
                TouchpadMouse.PropertyKeyStrings.STABILITY_DELTA_CLAMP,
            })
            {
                MarkStabilityProperty(key);
            }
        }

        private void RaiseStabilityPropertyChanges()
        {
            string[] propertyNames =
            {
                nameof(StabilityMode),
                nameof(StabilityAdvancedExpanded),
                nameof(StabilityTouchSettleMs),
                nameof(StabilityBaseNoiseFloor),
                nameof(StabilityHysteresisExitMultiplier),
                nameof(StabilityFastPassthroughThreshold),
                nameof(StabilityEdgeGuardEnabled),
                nameof(StabilityLeftEdgePercent),
                nameof(StabilityTopEdgePercent),
                nameof(StabilityTopLeftCornerMultiplier),
                nameof(StabilityEdgeStartGateEnabled),
                nameof(StabilityEdgeStartThreshold),
                nameof(StabilityEdgeLockEnabled),
                nameof(StabilityStationaryHoldEnabled),
                nameof(StabilityStationaryDetectionMs),
                nameof(StabilityStationaryNoiseMultiplier),
                nameof(StabilityStationaryBreakoutThreshold),
                nameof(StabilityDeltaClampEnabled),
                nameof(StabilityMaxDeltaPerFrame),
            };

            foreach (string propertyName in propertyNames)
            {
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(propertyName));
            }
        }

        private void TouchpadMousePropViewModel_SmoothingBetaChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
                action.ActionSmoothingSettings.UpdateSmoothingFilters();
            });

            HighlightSmoothingFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadMousePropViewModel_SmoothingEnabledChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_ENABLED))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
            HighlightSmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadMousePropViewModel_VerticalScaleChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
            });

            HighlightVerticalScaleChanged?.Invoke(this, EventArgs.Empty);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(VerticalScale)));
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(VerticalRws)));

            if (!_syncingAccelSens)
            {
                try
                {
                    _syncingAccelSens = true;
                    SyncTrackpadMinimumYFromBase();
                }
                finally
                {
                    _syncingAccelSens = false;
                }
            }
        }

        private void TouchpadMousePropViewModel_VerticalDeadZoneChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
            });

            HighlightVerticalDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetAccelerationValue(
            double value,
            Func<double> getter,
            Action<double> setter,
            string propertyKey,
            string propertyName)
        {
            if (!_modelReady) return;
            if (getter() == value) return;
            setter(value);
            MarkAccelerationProperty(propertyKey);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));
        }

        private void MarkAccelerationProperty(string propertyKey)
        {
            if (!action.ChangedProperties.Contains(propertyKey))
            {
                action.ChangedProperties.Add(propertyKey);
            }
            action.RaiseNotifyPropertyChange(mapper, propertyKey);
            ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseAccelerationPropertyChanges()
        {
            string[] propertyNames =
            {
                nameof(AccelCurveChoice),
                nameof(AccelCurveUsed),
                nameof(UsesMaxThreshold),
                nameof(NaturalCurveUsed),
                nameof(PowerCurveUsed),
                nameof(MinAccelXSens),
                nameof(MaxAccelXSens),
                nameof(MinAccelYSens),
                nameof(MaxAccelYSens),
                nameof(MinAccelThreshold),
                nameof(MaxAccelThreshold),
                nameof(NaturalVHalf),
                nameof(PowerVRef),
                nameof(PowerExponent),
            };

            foreach (string propertyName in propertyNames)
            {
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(propertyName));
            }
        }

        private void TouchpadMousePropViewModel_TrackpadAngleSnapDegreesChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES))
            {
                action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
            });

            HighlightTrackpadAngleSnapDegreesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadMousePropViewModel_TrackpadSmoothAngleSnapChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP))
            {
                action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
            });

            HighlightTrackpadSmoothAngleSnapChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadMousePropViewModel_SwipesPer360Changed(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360);
            HighlightSwipesPer360Changed?.Invoke(this, EventArgs.Empty);

            if (!_syncingAccelSens)
            {
                try
                {
                    _syncingAccelSens = true;
                    SyncTrackpadMinimumsFromBase();
                }
                finally
                {
                    _syncingAccelSens = false;
                }
            }
        }

        private void SyncTrackpadMinimumsFromBase()
        {
            SyncTrackpadMinimumXFromBase();
            SyncTrackpadMinimumYFromBase();
        }

        private void SyncTrackpadMinimumXFromBase()
        {
            double minX = Math.Clamp(action.SwipesPer360, 0.0, 100.0);
            if (action.MinAccelXSens == minX) return;

            action.MinAccelXSens = minX;
            MarkAccelerationProperty(
                TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(MinAccelXSens)));
        }

        private void SyncTrackpadMinimumYFromBase()
        {
            double minY = Math.Clamp(VerticalRws, 0.0, 100.0);
            if (action.MinAccelYSens == minY) return;

            action.MinAccelYSens = minY;
            MarkAccelerationProperty(
                TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(MinAccelYSens)));
        }

        private void TouchpadMousePropViewModel_TrackballFrictionChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.TRACKBALL_FRICTION))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.TRACKBALL_FRICTION);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.TRACKBALL_FRICTION);
            });

            HighlightTrackballFrictionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadMousePropViewModel_TrackballEnabledChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.TRACKBALL_MODE))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.TRACKBALL_MODE);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.TRACKBALL_MODE);
            });

            HighlightTrackballEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetProfileDirty(object sender, EventArgs e)
        {
            mapper.ActionProfile.Dirty = true;
        }

        private void TouchpadMousePropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.DEAD_ZONE))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.DEAD_ZONE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TouchpadMousePropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.NAME))
            {
                this.action.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, TouchpadMouse.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
