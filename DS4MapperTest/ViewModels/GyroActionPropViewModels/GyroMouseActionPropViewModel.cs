using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DS4MapperTest.ViewModels.GyroActionPropViewModels
{
    public class GyroMouseActionPropViewModel : GyroActionPropVMBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected GyroMouse action;
        public GyroMouse Action
        {
            get => action;
        }

        public double DeadZone
        {
            get => action.mouseParams.deadzone;
            set
            {
                action.mouseParams.deadzone = value;
                DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeadZoneChanged;

        public double VerticalDeadZone
        {
            get => action.mouseParams.verticalDeadZone;
            set
            {
                action.mouseParams.verticalDeadZone = value;
                VerticalDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler VerticalDeadZoneChanged;

        public double GyroAngleSnapDegrees
        {
            get => action.mouseParams.gyroAngleSnapDegrees;
            set
            {
                action.mouseParams.gyroAngleSnapDegrees = Math.Clamp(value, 0.0, 45.0);
                GyroAngleSnapDegreesChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler GyroAngleSnapDegreesChanged;

        public bool GyroSmoothAngleSnap
        {
            get => action.mouseParams.gyroSmoothAngleSnap;
            set
            {
                if (action.mouseParams.gyroSmoothAngleSnap == value) return;
                action.mouseParams.gyroSmoothAngleSnap = value;
                GyroSmoothAngleSnapChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler GyroSmoothAngleSnapChanged;

        private List<GyroTriggerButtonItem> triggerButtonItems;
        public List<GyroTriggerButtonItem> TriggerButtonItems => triggerButtonItems;

        public string GyroTriggerString
        {
            get
            {
                List<string> tempList = new List<string>();
                foreach (JoypadActionCodes code in action.mouseParams.gyroTriggerButtons)
                {
                    GyroTriggerButtonItem tempItem =
                        triggerButtonItems.Find((item) => item.Code == code);

                    if (tempItem != null)
                    {
                        tempList.Add(tempItem.DisplayString);
                    }
                }

                if (tempList.Count == 0)
                {
                    tempList.Add(DEFAULT_EMPTY_TRIGGER_STR);
                }

                string result = string.Join(", ", tempList);
                return result;
            }
        }
        public event EventHandler GyroTriggerStringChanged;

        public bool GyroTriggerCondChoice
        {
            get => action.mouseParams.andCond;
            set
            {
                if (action.mouseParams.andCond == value) return;
                action.mouseParams.andCond = value;
                GyroTriggerCondChoiceChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler GyroTriggerCondChoiceChanged;

        public bool GyroTriggerActivates
        {
            get => action.mouseParams.triggerActivates;
            set
            {
                if (action.mouseParams.triggerActivates == value) return;
                action.mouseParams.triggerActivates = value;
                GyroTriggerActivatesChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler GyroTriggerActivatesChanged;

        public double RealWorldCalibration
        {
            get => action.mouseParams.realWorldCalibration;
            set
            {
                if (!_modelReady) return;
                if (action.mouseParams.realWorldCalibration == value) return;
                action.mouseParams.realWorldCalibration = value;
                if (!_applyingPreset) TryMatchPreset();
                RealWorldCalibrationChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
            }
        }
        public event EventHandler RealWorldCalibrationChanged;

        public double InGameSens
        {
            get => action.mouseParams.inGameSens;
            set
            {
                // Reject changes until after the window has rendered. HandyControl's
                // NumericUpDown fires ValueChanged(Minimum) during control init before
                // the binding has populated the control with the real value, which would
                // corrupt CalibInGameSens. _modelReady is set via a low-priority
                // dispatcher post that runs after all Loaded-priority control events.
                if (!_modelReady) return;
                if (action.mouseParams.inGameSens == value) return;
                action.mouseParams.inGameSens = value;
                if (IsCountsMode) CalculateRwcFromCounts();
                InGameSensChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
            }
        }
        public event EventHandler InGameSensChanged;

        public bool StaticSensUsed
        {
            get => action.mouseParams.accelCurve == GyroMouseAccelCurveChoice.None;
        }
        public event EventHandler StaticSensUsedChanged;

        public bool AccelCurveUsed
        {
            get => action.mouseParams.accelCurve != GyroMouseAccelCurveChoice.None;
        }
        public event EventHandler AccelCurveUsedChanged;

        private List<AccelCurveChoiceItem> accelCurveChoiceItems = new List<AccelCurveChoiceItem>()
        {
            new AccelCurveChoiceItem("None", GyroMouseAccelCurveChoice.None),
            new AccelCurveChoiceItem("Linear", GyroMouseAccelCurveChoice.Linear),
            new AccelCurveChoiceItem("Quadratic", GyroMouseAccelCurveChoice.Quadratic),
            new AccelCurveChoiceItem("Cubic", GyroMouseAccelCurveChoice.Cubic),
            new AccelCurveChoiceItem("Power", GyroMouseAccelCurveChoice.Power),
            new AccelCurveChoiceItem("Natural", GyroMouseAccelCurveChoice.Natural),
        };
        public List<AccelCurveChoiceItem> AccelCurveChoiceItems => accelCurveChoiceItems;

        public GyroMouseAccelCurveChoice AccelCurveChoice
        {
            get => action.mouseParams.accelCurve;
            set
            {
                if (action.mouseParams.accelCurve == value) return;
                _prevAccelCurve = action.mouseParams.accelCurve;
                action.mouseParams.accelCurve = value;
                AccelCurveChoiceChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler AccelCurveChoiceChanged;

        public double MinAccelXSens
        {
            get => action.mouseParams.minAccelXSens;
            set
            {
                action.mouseParams.minAccelXSens = Math.Clamp(value, 0.0, 100.0);
                MinAccelXSensChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MinAccelXSensChanged;

        public double MaxAccelXSens
        {
            get => action.mouseParams.maxAccelXSens;
            set
            {
                action.mouseParams.maxAccelXSens = Math.Clamp(value, 0.0, 100.0);
                MaxAccelXSensChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MaxAccelXSensChanged;



        public double MinAccelYSens
        {
            get => action.mouseParams.minAccelYSens;
            set
            {
                action.mouseParams.minAccelYSens = Math.Clamp(value, 0.0, 100.0);
                MinAccelYSensChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MinAccelYSensChanged;

        public double MaxAccelYSens
        {
            get => action.mouseParams.maxAccelYSens;
            set
            {
                action.mouseParams.maxAccelYSens = Math.Clamp(value, 0.0, 100.0);
                MaxAccelYSensChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MaxAccelYSensChanged;


        public double MinGyroThreshold
        {
            get => action.mouseParams.minGyroThreshold;
            set
            {
                action.mouseParams.minGyroThreshold = Math.Clamp(value, 0.0, 500.0);
                MinGyroThresholdChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MinGyroThresholdChanged;

        public double MaxGyroThreshold
        {
            get => action.mouseParams.maxGyroThreshold;
            set
            {
                action.mouseParams.maxGyroThreshold = Math.Clamp(value, 0.0, 500.0);
                MaxGyroThresholdChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MaxGyroThresholdChanged;

        public bool UsesMaxThreshold
        {
            get
            {
                bool result = false;
                switch(action.mouseParams.accelCurve)
                {
                    case GyroMouseAccelCurveChoice.Linear:
                    case GyroMouseAccelCurveChoice.Quadratic:
                    case GyroMouseAccelCurveChoice.Cubic:
                        result = true;
                        break;
                    default:
                        result = false; break;
                }
                return result;
            }
        }
        public event EventHandler UsesMaxThresholdChanged;

        public double NaturalVHalf
        {
            get => action.mouseParams.naturalVHalf;
            set
            {
                if (action.mouseParams.naturalVHalf == value) return;
                action.mouseParams.naturalVHalf = Math.Clamp(value, 1.0, 500.0);
                NaturalVHalfChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler NaturalVHalfChanged;

        public bool PowerCurveUsed
        {
            get => action.mouseParams.accelCurve == GyroMouseAccelCurveChoice.Power;
        }
        public event EventHandler PowerCurveUsedChanged;

        public bool NaturalCurveUsed
        {
            get => action.mouseParams.accelCurve == GyroMouseAccelCurveChoice.Natural;
        }

        public double PowerCurveVRef
        {
            get => action.mouseParams.powerVRef;
            set
            {
                if (action.mouseParams.powerVRef == value) return;
                action.mouseParams.powerVRef = Math.Clamp(value, 0.1, 500.0);
                PowerCurveVRefChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler PowerCurveVRefChanged;

        public double PowerCurveExponent
        {
            get => action.mouseParams.powerExponent;
            set
            {
                if (action.mouseParams.powerExponent == value) return;
                action.mouseParams.powerExponent = Math.Clamp(value, 1.0, 500.0);
                PowerCurveExponentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler PowerCurveExponentChanged;

        public double Sensitivity
        {
            get => action.mouseParams.sensitivity;
            set
            {
                if (!_modelReady) return;
                double sensitivity = Math.Clamp(value, 0.0, 10.0);
                if (action.mouseParams.sensitivity == sensitivity) return;

                double verticalMultiplier = VerticalScaleMultiplier;
                action.mouseParams.sensitivity = sensitivity;
                if (VerticalScaleIsMultiplierMode)
                {
                    action.mouseParams.verticalScale = Math.Clamp(
                        sensitivity * verticalMultiplier, 0.0, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(VerticalScale)));
                }
                SensitivityChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SensitivityChanged;

        public double VerticalScale
        {
            get => action.mouseParams.verticalScale;
            set
            {
                if (!_modelReady) return;
                double verticalScale = Math.Clamp(value, 0.0, 10.0);
                if (action.mouseParams.verticalScale == verticalScale) return;
                action.mouseParams.verticalScale = verticalScale;
                VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler VerticalScaleChanged;

        private bool verticalScaleIsAbsoluteMode = true;
        public bool VerticalScaleIsAbsoluteMode
        {
            get => verticalScaleIsAbsoluteMode;
            set
            {
                if (!value || verticalScaleIsAbsoluteMode) return;
                verticalScaleIsAbsoluteMode = value;
                VerticalScaleIsAbsoluteModeChanged?.Invoke(this, EventArgs.Empty);
                VerticalScaleIsMultiplierModeChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalScaleIsAbsoluteMode)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalScaleIsMultiplierMode)));
            }
        }
        public event EventHandler VerticalScaleIsAbsoluteModeChanged;

        public bool VerticalScaleIsMultiplierMode
        {
            get => !verticalScaleIsAbsoluteMode;
            set
            {
                if (!value || !verticalScaleIsAbsoluteMode) return;
                verticalScaleIsAbsoluteMode = !value;
                VerticalScaleIsAbsoluteModeChanged?.Invoke(this, EventArgs.Empty);
                VerticalScaleIsMultiplierModeChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalScaleIsAbsoluteMode)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalScaleIsMultiplierMode)));
            }
        }
        public event EventHandler VerticalScaleIsMultiplierModeChanged;

        public double VerticalScaleMultiplier
        {
            get
            {
                double sens = action.mouseParams.sensitivity;
                if (Math.Abs(sens) < 1e-10) return action.mouseParams.verticalScale;
                return Math.Round(action.mouseParams.verticalScale / sens, 4);
            }
            set
            {
                if (!_modelReady) return;
                double sens = action.mouseParams.sensitivity;
                double abs = Math.Abs(sens) < 1e-10 ? value : value * sens;
                double verticalScale = Math.Clamp(abs, 0.0, 10.0);
                if (action.mouseParams.verticalScale == verticalScale) return;
                action.mouseParams.verticalScale = verticalScale;
                VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler VerticalScaleMultiplierChanged;

        private List<InvertChoiceItem> invertChoiceItems = new List<InvertChoiceItem>()
        {
            new InvertChoiceItem("None", InvertChocies.None),
            new InvertChoiceItem("X", InvertChocies.InvertX),
            new InvertChoiceItem("Y", InvertChocies.InvertY),
            new InvertChoiceItem("X+Y", InvertChocies.InvertXY),
        };
        public List<InvertChoiceItem> InvertChoiceItems => invertChoiceItems;

        public InvertChocies InvertChoice
        {
            get
            {
                InvertChocies result = InvertChocies.None;
                if (action.mouseParams.invertX && action.mouseParams.invertY)
                {
                    result = InvertChocies.InvertXY;
                }
                else if (action.mouseParams.invertX || action.mouseParams.invertY)
                {
                    if (action.mouseParams.invertX)
                    {
                        result = InvertChocies.InvertX;
                    }
                    else
                    {
                        result = InvertChocies.InvertY;
                    }
                }

                return result;
            }
            set
            {
                action.mouseParams.invertX = action.mouseParams.invertY = false;

                switch (value)
                {
                    case InvertChocies.None:
                        break;
                    case InvertChocies.InvertX:
                        action.mouseParams.invertX = true;
                        break;
                    case InvertChocies.InvertY:
                        action.mouseParams.invertY = true;
                        break;
                    case InvertChocies.InvertXY:
                        action.mouseParams.invertX = action.mouseParams.invertY = true;
                        break;
                    default:
                        break;
                }

                InvertChoicesChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler InvertChoicesChanged;

        public bool GyroJitterCompensation
        {
            get => action.mouseParams.jitterCompensation;
            set
            {
                if (action.mouseParams.jitterCompensation == value) return;
                action.mouseParams.jitterCompensation = value;
                GyroJitterCompensationChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler GyroJitterCompensationChanged;

        public bool MultiplierCompensation
        {
            get => action.mouseParams.multiplierCompensation;
            set
            {
                if (action.mouseParams.multiplierCompensation == value) return;
                action.mouseParams.multiplierCompensation = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(MultiplierCompensation)));
                MultiplierCompensationChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MultiplierCompensationChanged;

        public double AccelerationMultiplier
        {
            get => action.mouseParams.accelerationMultiplier;
            set
            {
                if (!_modelReady) return;
                double accelerationMultiplier = Math.Clamp(value, 0.01, 100.0);
                if (action.mouseParams.accelerationMultiplier == accelerationMultiplier) return;
                double verticalAccelerationScale = VerticalAccelerationScale;
                action.mouseParams.accelerationMultiplier = accelerationMultiplier;
                if (VerticalAccelerationIsScaleMode)
                {
                    action.mouseParams.verticalAccelerationMultiplier = Math.Clamp(
                        accelerationMultiplier * verticalAccelerationScale, 0.01, 100.0);
                    VerticalAccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(VerticalAccelerationMultiplier)));
                    PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(VerticalAccelerationScale)));
                }
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(AccelerationMultiplier)));
                AccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationEffectiveMultiplier)));
            }
        }
        public event EventHandler AccelerationMultiplierChanged;

        public bool VerticalAccelerationIsScaleMode
        {
            get => action.mouseParams.verticalAccelerationScaleMode;
            set
            {
                if (!value || action.mouseParams.verticalAccelerationScaleMode) return;
                action.mouseParams.verticalAccelerationScaleMode = value;
                VerticalAccelerationScaleModeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationIsScaleMode)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationIsAbsoluteMode)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationScale)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationEffectiveMultiplier)));
            }
        }

        public bool VerticalAccelerationIsAbsoluteMode
        {
            get => !action.mouseParams.verticalAccelerationScaleMode;
            set
            {
                if (!value || !action.mouseParams.verticalAccelerationScaleMode) return;
                action.mouseParams.verticalAccelerationScaleMode = !value;
                VerticalAccelerationScaleModeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationIsScaleMode)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationIsAbsoluteMode)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationScale)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationEffectiveMultiplier)));
            }
        }
        public event EventHandler VerticalAccelerationScaleModeChanged;

        public double VerticalAccelerationMultiplier
        {
            get => action.mouseParams.verticalAccelerationMultiplier;
            set
            {
                if (!_modelReady) return;
                double verticalAccelerationMultiplier = Math.Clamp(value, 0.01, 100.0);
                if (action.mouseParams.verticalAccelerationMultiplier == verticalAccelerationMultiplier) return;
                action.mouseParams.verticalAccelerationMultiplier = verticalAccelerationMultiplier;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationMultiplier)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationEffectiveMultiplier)));
                VerticalAccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler VerticalAccelerationMultiplierChanged;

        public double VerticalAccelerationScale
        {
            get
            {
                double horizontalMultiplier = action.mouseParams.accelerationMultiplier;
                if (Math.Abs(horizontalMultiplier) < 1e-10)
                {
                    return action.mouseParams.verticalAccelerationMultiplier;
                }

                return Math.Round(action.mouseParams.verticalAccelerationMultiplier /
                    horizontalMultiplier, 4);
            }
            set
            {
                if (!_modelReady) return;
                double horizontalMultiplier = action.mouseParams.accelerationMultiplier;
                double verticalMultiplier = Math.Abs(horizontalMultiplier) < 1e-10 ?
                    value : horizontalMultiplier * value;
                verticalMultiplier = Math.Clamp(verticalMultiplier, 0.01, 100.0);
                if (action.mouseParams.verticalAccelerationMultiplier == verticalMultiplier) return;
                action.mouseParams.verticalAccelerationMultiplier = verticalMultiplier;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationMultiplier)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationScale)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(VerticalAccelerationEffectiveMultiplier)));
                VerticalAccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public double VerticalAccelerationEffectiveMultiplier
        {
            get
            {
                return Math.Round(Math.Clamp(
                    action.mouseParams.verticalAccelerationMultiplier, 0.01, 100.0), 4);
            }
        }

        public bool SmoothingEnabled
        {
            get => action.mouseParams.smoothing;
            set
            {
                if (action.mouseParams.smoothing == value) return;
                action.mouseParams.smoothing = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SmoothingEnabled)));
                SmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SmoothingEnabledChanged;

        private List<SmoothPresetChoiceItem> smoothPresetChoiceItems = new List<SmoothPresetChoiceItem>()
        {
            new SmoothPresetChoiceItem("", SmoothPresetChoices.None, 1.5, 0.8),
            new SmoothPresetChoiceItem("Stiff", SmoothPresetChoices.Stiff, 0.4, 0.6),
            new SmoothPresetChoiceItem("Normie", SmoothPresetChoices.Normie, 1.0, 1.0),
            new SmoothPresetChoiceItem("Loose", SmoothPresetChoices.Loose, 1.5, 0.8),
        };
        public List<SmoothPresetChoiceItem> SmoothPresetChoiceItems => smoothPresetChoiceItems;

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
            get => action.mouseParams.smoothingFilterSettings.minCutOff;
            set
            {
                if (!_modelReady) return;
                double minCutoff = Math.Clamp(value, 0.0, 10.0);
                if (action.mouseParams.smoothingFilterSettings.minCutOff ==
                    minCutoff) return;
                action.mouseParams.smoothingFilterSettings.minCutOff =
                    minCutoff;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(SmoothingMinCutoff)));
                SmoothingMinCutoffChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SmoothingMinCutoffChanged;

        public double SmoothingBeta
        {
            get => action.mouseParams.smoothingFilterSettings.beta;
            set
            {
                if (!_modelReady) return;
                double beta = Math.Clamp(value, 0.0, 1.0);
                if (action.mouseParams.smoothingFilterSettings.beta ==
                    beta) return;
                action.mouseParams.smoothingFilterSettings.beta = beta;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(SmoothingBeta)));
                SmoothingBetaChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SmoothingBetaChanged;

        private bool _modelReady = false;
        private bool _syncingAccelSens = false;
        private GyroMouseAccelCurveChoice _prevAccelCurve;

        private GameCalibPreset _selectedPreset = GameCalibPreset.Custom;
        private bool _applyingPreset = false;

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

        public CalibMode CalibMode
        {
            get => mapper.ActionProfile.CalibMode;
            set
            {
                if (!_modelReady) return;
                if (mapper.ActionProfile.CalibMode == value) return;
                mapper.ActionProfile.CalibMode = value;
                RaiseCalibModePropertyChanges();
                SyncCalibFromGyroMouseToProfile();
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

        private void SetSelectedPresetCustom()
        {
            _selectedPreset = GameCalibPreset.Custom;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        }

        private void TryMatchPreset()
        {
            double rwc = action.mouseParams.realWorldCalibration;
            double sens = action.mouseParams.inGameSens;
            GameCalibPreset match = GameCalibPreset.All.FirstOrDefault(
                p => !p.IsCustom &&
                     Math.Abs(p.RWC - rwc) < 1e-3 &&
                     Math.Abs(p.InGameSens - sens) < 1e-3);
            GameCalibPreset next = match ?? GameCalibPreset.Custom;
            if (_selectedPreset == next) return;
            _selectedPreset = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        }

        private double fullTurnCounts = 10.0;
        public double FullTurnCounts
        {
            get => fullTurnCounts;
            set
            {
                if (!_modelReady) return;
                if (value == 0.0) return; // Avoid division by zero
                bool countsChanged = fullTurnCounts != value;
                fullTurnCounts = value;
                CalculateTestRWC();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
                if (!countsChanged) return;
                if (!IsCountsMode) return;
                CalculateRwcFromCounts();
                SyncCalibFromGyroMouseToProfile();
            }
        }
        //public event EventHandler FullTurnCountsChanged;

        private double calculatedRWC = 0.0;
        public double CalculatedRWC
        {
            get => calculatedRWC;
            set
            {
                if (calculatedRWC == value) return;
                calculatedRWC = value;
                CalculatedRWCChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalculatedRWC)));
            }
        }
        public event EventHandler CalculatedRWCChanged;

        private BasicActionCommand copyTestRWCComm;
        public BasicActionCommand CopyTestRWCComm
        {
            get => copyTestRWCComm;
        }

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                baseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                baseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightVerticalDeadZone
        {
            get => action.ParentAction == null ||
                baseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
        }

        private BasicActionCommand resetAccelerationDefaultsComm;
        public BasicActionCommand ResetAccelerationDefaultsComm =>
            resetAccelerationDefaultsComm;
        public event EventHandler HighlightVerticalDeadZoneChanged;

        public bool HighlightGyroAngleSnapDegrees
        {
            get => action.ParentAction == null ||
                baseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
        }
        public event EventHandler HighlightGyroAngleSnapDegreesChanged;

        public bool HighlightGyroSmoothAngleSnap
        {
            get => action.ParentAction == null ||
                baseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
        }
        public event EventHandler HighlightGyroSmoothAngleSnapChanged;

        public bool HighlightGyroTriggerCond
        {
            get => action.ParentAction == null ||
                baseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.TRIGGER_EVAL_COND);
        }
        public event EventHandler HighlightGyroTriggerCondChanged;

        public bool HighlightGyroTriggers
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.TRIGGER_BUTTONS);
        }
        public event EventHandler HighlightGyroTriggersChanged;

        public bool HighlightGyroTriggerActivates
        {
            get => action.ParentAction == null ||
                baseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.TRIGGER_ACTIVATE);
        }
        public event EventHandler HighlightGyroTriggerActivatesChanged;

        public bool HighlightRealWorldCalibration
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.REAL_WORLD_CALIBRATION);
        }
        public event EventHandler HighlightRealWorldCalibrationChanged;

        public bool HighlightInGameSens
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.IN_GAME_SENS);
        }
        public event EventHandler HighlightInGameSensChanged;

        public bool HighlightAccelCurve
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ACCEL_CURVE);
        }
        public event EventHandler HighlightAccelCurveChanged;

        public bool HighlightMinAccelXSens
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
        }
        public event EventHandler HighlightMinAccelXSensChanged;

        public bool HighlightMaxAccelXSens
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);
        }
        public event EventHandler HighlightMaxAccelXSensChanged;


        public bool HighlightMinAccelYSens
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
        }
        public event EventHandler HighlightMinAccelYSensChanged;

        public bool HighlightMaxAccelYSens
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);
        }
        public event EventHandler HighlightMaxAccelYSensChanged;


        public bool HighlightMinGyroThreshold
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_GYRO_THRESHOLD);
        }
        public event EventHandler HighlightMinGyroThresholdChanged;

        public bool HighlightMaxGyroThreshold
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_GYRO_THRESHOLD);
        }
        public event EventHandler HighlightMaxGyroThresholdChanged;

        public bool HighlightNaturalVHalf
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF);
        }
        public event EventHandler HighlightNaturalVHalfChanged;

        public bool HighlightPowerCurveVRef
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.POWER_CURVE_VREF);
        }
        public event EventHandler HighlightPowerCurveVRefChanged;

        public bool HighlightPowerCurveExponent
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT);
        }
        public event EventHandler HighlightPowerCurveExponentChanged;

        public bool HighlightSensitivity
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SENSITIVITY);
        }
        public event EventHandler HighlightSensitivityChanged;

        public bool HighlightVerticalScale
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_SCALE);
        }
        public event EventHandler HighlightVerticalScaleChanged;

        public bool HighlightGyroJitterCompensation
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.JITTER_COMPENSATION);
        }
        public event EventHandler HighlightGyroJitterCompensationChanged;

        public bool HighlightMultiplierCompensation
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
        }
        public event EventHandler HighlightMultiplierCompensationChanged;

        public bool HighlightAccelerationMultiplier
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
        }
        public event EventHandler HighlightAccelerationMultiplierChanged;

        public bool HighlightVerticalAccelerationMultiplier
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER);
        }
        public event EventHandler HighlightVerticalAccelerationMultiplierChanged;

        public bool HighlightVerticalAccelerationScaleMode
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE);
        }
        public event EventHandler HighlightVerticalAccelerationScaleModeChanged;

        public bool HighlightSmoothingEnabled
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
        }
        public event EventHandler HighlightSmoothingEnabledChanged;

        public bool HighlightSmoothingFilter
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
        }
        public event EventHandler HighlightSmoothingFilterChanged;

        public bool HighlightInvert
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_X) ||
                action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_Y);
        }
        public event EventHandler HighlightInvertChanged;

        public override event EventHandler ActionPropertyChanged;

        public GyroMouseActionPropViewModel(Mapper mapper, GyroMapAction action)
        {
            this.mapper = mapper;
            this.action = action as GyroMouse;
            this.baseAction = action;
            triggerButtonItems = new List<GyroTriggerButtonItem>();

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.ActionProfile.CurrentActionSet.UsingCompositeLayer &&
                !mapper.ActionProfile.CurrentActionSet.RecentAppliedLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.ActionProfile.CurrentActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                GyroMouse baseLayerAction = mapper.ActionProfile.CurrentActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as GyroMouse;
                GyroMouse tempAction = new GyroMouse();
                tempAction.SoftCopyFromParent(baseLayerAction);
                //int tempLayerId = mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.Index;
                int tempId = mapper.ActionProfile.CurrentActionSet.RecentAppliedLayer.FindNextAvailableId();
                tempAction.Id = tempId;
                //tempAction.MappingId = this.action.MappingId;

                this.action = tempAction;
                this.baseAction = tempAction;
                usingRealAction = false;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            PopulateModel();
            if (this.action.mouseParams.accelCurve ==
                GyroMouseAccelCurveChoice.None)
            {
                this.action.mouseParams.minAccelXSens =
                    this.action.mouseParams.sensitivity;
                this.action.mouseParams.minAccelYSens =
                    this.action.mouseParams.verticalScale;
            }
            else
            {
                this.action.mouseParams.sensitivity = Math.Clamp(
                    this.action.mouseParams.minAccelXSens, 0.0, 10.0);
                this.action.mouseParams.verticalScale = Math.Clamp(
                    this.action.mouseParams.minAccelYSens, 0.0, 10.0);
            }
            _prevAccelCurve = this.action.mouseParams.accelCurve;

            copyTestRWCComm = new BasicActionCommand((parameter) =>
            {
                RealWorldCalibration = CalculatedRWC;
            });
            resetAccelerationDefaultsComm = new BasicActionCommand((parameter) =>
            {
                _syncingAccelSens = true;
                try
                {
                    this.action.mouseParams.sensitivity = GyroMouseParams.SENSITIVITY_DEFAULT;
                    this.action.mouseParams.verticalScale = GyroMouseParams.VERTICAL_SCALE_DEFAULT;
                    this.action.mouseParams.minAccelXSens = this.action.mouseParams.sensitivity;
                    this.action.mouseParams.minAccelYSens = this.action.mouseParams.verticalScale;
                    this.action.mouseParams.maxAccelXSens = this.action.mouseParams.minAccelXSens;
                    this.action.mouseParams.maxAccelYSens = this.action.mouseParams.minAccelYSens;
                    foreach (string key in new[]
                    {
                        GyroMouse.PropertyKeyStrings.SENSITIVITY,
                        GyroMouse.PropertyKeyStrings.VERTICAL_SCALE,
                        GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS,
                        GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS,
                        GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS,
                        GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS,
                    })
                    {
                        if (!this.action.ChangedProperties.Contains(key))
                            this.action.ChangedProperties.Add(key);
                        this.action.RaiseNotifyPropertyChange(mapper, key);
                    }
                    AccelCurveChoice = GyroMouseParams.ACCEL_CURVE_DEFAULT;
                    MinGyroThreshold = GyroMouseParams.MIN_GYRO_THRESHOLD_DEFAULT;
                    MaxGyroThreshold = GyroMouseParams.MAX_GYRO_THRESHOLD_DEFAULT;
                    NaturalVHalf = GyroMouseParams.NATURAL_VHALF_DEFAULT;
                    PowerCurveVRef = GyroMouseParams.POWER_VREF_DEFAULT;
                    PowerCurveExponent = GyroMouseParams.POWER_EXPONENT_DEFAULT;
                }
                finally
                {
                    _syncingAccelSens = false;
                }
                RaiseAccelerationPropertyChanges();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sensitivity)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScaleMultiplier)));
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            });

            NameChanged += GyroMouseActionPropViewModel_NameChanged;
            DeadZoneChanged += GyroMouseActionPropViewModel_DeadZoneChanged;
            VerticalDeadZoneChanged += GyroMouseActionPropViewModel_VerticalDeadZoneChanged;
            GyroAngleSnapDegreesChanged += GyroMouseActionPropViewModel_GyroAngleSnapDegreesChanged;
            GyroSmoothAngleSnapChanged += GyroMouseActionPropViewModel_GyroSmoothAngleSnapChanged;
            GyroTriggerCondChoiceChanged += GyroMouseActionPropViewModel_GyroTriggerCondChoiceChanged;
            GyroTriggerActivatesChanged += GyroMouseActionPropViewModel_TriggerActivatesChanged;
            RealWorldCalibrationChanged += GyroMouseActionPropViewModel_RealWorldCalibrationChanged;
            InGameSensChanged += GyroMouseActionPropViewModel_InGameSensChanged;
            AccelCurveChoiceChanged += GyroMouseActionPropViewModel_AccelCurveChoiceChanged;
            MinAccelXSensChanged += GyroMouseActionPropViewModel_MinAccelXSensChanged;
            MaxAccelXSensChanged += GyroMouseActionPropViewModel_MaxAccelXSensChanged;
            MinAccelYSensChanged += GyroMouseActionPropViewModel_MinAccelYSensChanged;
            MaxAccelYSensChanged += GyroMouseActionPropViewModel_MaxAccelYSensChanged;
            MinGyroThresholdChanged += GyroMouseActionPropViewModel_MinGyroThresholdChanged;
            MaxGyroThresholdChanged += GyroMouseActionPropViewModel_MaxGyroThresholdChanged;
            PowerCurveVRefChanged += GyroMouseActionPropViewModel_PowerCurveVRefChanged;
            PowerCurveExponentChanged += GyroMouseActionPropViewModel_PowerCurveExponentChanged;
            NaturalVHalfChanged += GyroMouseActionPropViewModel_NaturalVHalfChanged;
            SensitivityChanged += GyroMouseActionPropViewModel_SensitivityChanged;
            VerticalScaleChanged += GyroMouseActionPropViewModel_VerticalScaleChanged;
            InvertChoicesChanged += GyroMouseActionPropViewModel_InvertChoicesChanged;
            GyroJitterCompensationChanged += GyroMouseActionPropViewModel_GyroJitterCompensationChanged;
            MultiplierCompensationChanged += GyroMouseActionPropViewModel_MultiplierCompensationChanged;
            AccelerationMultiplierChanged += GyroMouseActionPropViewModel_AccelerationMultiplierChanged;
            VerticalAccelerationMultiplierChanged += GyroMouseActionPropViewModel_VerticalAccelerationMultiplierChanged;
            VerticalAccelerationScaleModeChanged += GyroMouseActionPropViewModel_VerticalAccelerationScaleModeChanged;
            SmoothingEnabledChanged += GyroMouseActionPropViewModel_SmoothingEnabledChanged;
            SmoothingMinCutoffChanged += GyroMouseActionPropViewModel_SmoothingMinCutoffChanged;
            SmoothingBetaChanged += GyroMouseActionPropViewModel_SmoothingBetaChanged;
            mapper.ActionProfile.CalibModeChanged += ActionProfile_CalibModeChanged;

            double savedInGameSens = this.action.mouseParams.inGameSens;
            double savedRwc = this.action.mouseParams.realWorldCalibration;
            double savedSensitivity = this.action.mouseParams.sensitivity;
            double savedVerticalScale = this.action.mouseParams.verticalScale;
            double savedAccelerationMultiplier = this.action.mouseParams.accelerationMultiplier;
            double savedVerticalAccelerationMultiplier = this.action.mouseParams.verticalAccelerationMultiplier;
            double savedCounts = fullTurnCounts;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    this.action.mouseParams.inGameSens = savedInGameSens;
                    this.action.mouseParams.realWorldCalibration = savedRwc;
                    this.action.mouseParams.sensitivity = savedSensitivity;
                    this.action.mouseParams.verticalScale = savedVerticalScale;
                    this.action.mouseParams.accelerationMultiplier = savedAccelerationMultiplier;
                    this.action.mouseParams.verticalAccelerationMultiplier = savedVerticalAccelerationMultiplier;
                    fullTurnCounts = savedCounts;
                    CalculateTestRWC();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sensitivity)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccelerationMultiplier)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalAccelerationMultiplier)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalAccelerationScale)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalAccelerationEffectiveMultiplier)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                    RaiseCalibModePropertyChanges();
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            this.action.mouseParams.inGameSens = savedInGameSens;
                            this.action.mouseParams.realWorldCalibration = savedRwc;
                            this.action.mouseParams.sensitivity = savedSensitivity;
                            this.action.mouseParams.verticalScale = savedVerticalScale;
                            this.action.mouseParams.accelerationMultiplier = savedAccelerationMultiplier;
                            this.action.mouseParams.verticalAccelerationMultiplier = savedVerticalAccelerationMultiplier;
                            fullTurnCounts = savedCounts;
                            CalculateTestRWC();
                            TryMatchPreset();
                            _modelReady = true;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sensitivity)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccelerationMultiplier)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalAccelerationMultiplier)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalAccelerationScale)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalAccelerationEffectiveMultiplier)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                            RaiseCalibModePropertyChanges();
                        }));
                }));
        }

        private void CalculateTestRWC()
        {
            CalculatedRWC = InGameSens / (360.0 / fullTurnCounts);
        }

        private void CalculateRwcFromCounts()
        {
            double rwc = fullTurnCounts * InGameSens / 360.0;
            if (action.mouseParams.realWorldCalibration == rwc) return;
            action.mouseParams.realWorldCalibration = rwc;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
        }

        private void SyncCalibFromGyroMouseToProfile()
        {
            double inGameSens = action.mouseParams.inGameSens;
            double rwc = IsCountsMode
                ? fullTurnCounts * inGameSens / 360.0
                : action.mouseParams.realWorldCalibration;
            action.mouseParams.realWorldCalibration = rwc;
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
                            if (mapAction is GyroMouse gyroMouse)
                            {
                                gyroMouse.mouseParams.realWorldCalibration = rwc;
                                gyroMouse.mouseParams.inGameSens = inGameSens;
                            }
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

        private void GyroMouseActionPropViewModel_NaturalVHalfChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF);
            HighlightNaturalVHalfChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_MaxAccelYSensChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);
            HighlightMaxAccelYSensChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_MinAccelYSensChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
            HighlightMinAccelYSensChanged?.Invoke(this, EventArgs.Empty);

            if (!_syncingAccelSens)
            {
                try
                {
                    _syncingAccelSens = true;
                    double newVScale = Math.Clamp(action.mouseParams.minAccelYSens, 0.0, 10.0);
                    if (action.mouseParams.verticalScale != newVScale)
                    {
                        action.mouseParams.verticalScale = newVScale;
                        MarkChangedProperty(GyroMouse.PropertyKeyStrings.VERTICAL_SCALE);
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScaleMultiplier)));
                        HighlightVerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                finally
                {
                    _syncingAccelSens = false;
                }
            }
        }

        private void GyroMouseActionPropViewModel_PowerCurveExponentChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT);
            HighlightPowerCurveExponentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_PowerCurveVRefChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.POWER_CURVE_VREF))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.POWER_CURVE_VREF);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.POWER_CURVE_VREF);
            HighlightPowerCurveVRefChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_AccelCurveChoiceChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ACCEL_CURVE))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.ACCEL_CURVE);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.ACCEL_CURVE);
            HighlightAccelCurveChanged?.Invoke(this, EventArgs.Empty);

            if (!_syncingAccelSens &&
                _prevAccelCurve == GyroMouseAccelCurveChoice.None &&
                action.mouseParams.accelCurve != GyroMouseAccelCurveChoice.None)
            {
                try
                {
                    _syncingAccelSens = true;
                    SyncGyroMinimumsFromBase();
                    if (!action.ChangedProperties.Contains(
                            GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS) &&
                        action.mouseParams.maxAccelXSens ==
                            GyroMouseParams.MAX_ACCEL_SENS_DEFAULT)
                    {
                        action.mouseParams.maxAccelXSens =
                            action.mouseParams.minAccelXSens;
                        MarkChangedProperty(
                            GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);
                    }
                    if (!action.ChangedProperties.Contains(
                            GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS) &&
                        action.mouseParams.maxAccelYSens ==
                            GyroMouseParams.MAX_ACCEL_SENS_DEFAULT)
                    {
                        action.mouseParams.maxAccelYSens =
                            action.mouseParams.minAccelYSens;
                        MarkChangedProperty(
                            GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);
                    }
                }
                finally
                {
                    _syncingAccelSens = false;
                }
            }
            else if (_prevAccelCurve != GyroMouseAccelCurveChoice.None &&
                action.mouseParams.accelCurve == GyroMouseAccelCurveChoice.None)
            {
                MarkChangedProperty(GyroMouse.PropertyKeyStrings.SENSITIVITY);
                MarkChangedProperty(GyroMouse.PropertyKeyStrings.VERTICAL_SCALE);
            }

            StaticSensUsedChanged?.Invoke(this, EventArgs.Empty);
            AccelCurveUsedChanged?.Invoke(this, EventArgs.Empty);
            PowerCurveUsedChanged?.Invoke(this, EventArgs.Empty);
            UsesMaxThresholdChanged?.Invoke(this, EventArgs.Empty);
            RaiseAccelerationPropertyChanges();
        }

        private void RaiseAccelerationPropertyChanges()
        {
            string[] propertyNames =
            {
                nameof(AccelCurveChoice),
                nameof(StaticSensUsed),
                nameof(AccelCurveUsed),
                nameof(UsesMaxThreshold),
                nameof(PowerCurveUsed),
                nameof(NaturalCurveUsed),
                nameof(MinAccelXSens),
                nameof(MinAccelYSens),
                nameof(MaxAccelXSens),
                nameof(MaxAccelYSens),
                nameof(MinGyroThreshold),
                nameof(MaxGyroThreshold),
                nameof(NaturalVHalf),
                nameof(PowerCurveVRef),
                nameof(PowerCurveExponent),
            };

            foreach (string propertyName in propertyNames)
            {
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(propertyName));
            }
        }

        private void GyroMouseActionPropViewModel_MaxAccelXSensChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);
            HighlightMaxAccelXSensChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_MinAccelXSensChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
            HighlightMinAccelXSensChanged?.Invoke(this, EventArgs.Empty);

            if (!_syncingAccelSens)
            {
                try
                {
                    _syncingAccelSens = true;
                    double newSens = Math.Clamp(action.mouseParams.minAccelXSens, 0.0, 10.0);
                    if (action.mouseParams.sensitivity != newSens)
                    {
                        action.mouseParams.sensitivity = newSens;
                        MarkChangedProperty(GyroMouse.PropertyKeyStrings.SENSITIVITY);
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sensitivity)));
                        HighlightSensitivityChanged?.Invoke(this, EventArgs.Empty);
                        VerticalScaleMultiplierChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                finally
                {
                    _syncingAccelSens = false;
                }
            }
        }

        private void GyroMouseActionPropViewModel_MaxGyroThresholdChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_GYRO_THRESHOLD))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MAX_GYRO_THRESHOLD);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.MAX_GYRO_THRESHOLD);
            HighlightMaxGyroThresholdChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_MinGyroThresholdChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_GYRO_THRESHOLD))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MIN_GYRO_THRESHOLD);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.MIN_GYRO_THRESHOLD);
            HighlightMinGyroThresholdChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_InGameSensChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.IN_GAME_SENS))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.IN_GAME_SENS);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.IN_GAME_SENS);
            HighlightInGameSensChanged?.Invoke(this, EventArgs.Empty);
            CalculateTestRWC();
            SyncCalibFromGyroMouseToProfile();
        }

        private void GyroMouseActionPropViewModel_RealWorldCalibrationChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.REAL_WORLD_CALIBRATION))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.REAL_WORLD_CALIBRATION);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.REAL_WORLD_CALIBRATION);
            HighlightRealWorldCalibrationChanged?.Invoke(this, EventArgs.Empty);
            SyncCalibFromGyroMouseToProfile();
        }

        private void GyroMouseActionPropViewModel_GyroJitterCompensationChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.JITTER_COMPENSATION))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.JITTER_COMPENSATION);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.JITTER_COMPENSATION);
            HighlightGyroJitterCompensationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_MultiplierCompensationChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MULTIPLIER_COMPENSATION))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
            HighlightMultiplierCompensationChanged?.Invoke(this, EventArgs.Empty);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(MultiplierCompensation)));
        }

        private void GyroMouseActionPropViewModel_AccelerationMultiplierChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ACCELERATION_MULTIPLIER))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
            HighlightAccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_VerticalAccelerationMultiplierChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER);
            HighlightVerticalAccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_VerticalAccelerationScaleModeChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE);
            HighlightVerticalAccelerationScaleModeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_GyroTriggerCondChoiceChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.TRIGGER_EVAL_COND))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.TRIGGER_EVAL_COND);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.TRIGGER_EVAL_COND);
            HighlightGyroTriggerCondChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_SmoothingBetaChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
                action.mouseParams.smoothingFilterSettings.UpdateSmoothingFilters();
            });

            HighlightSmoothingFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_SmoothingMinCutoffChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
                action.mouseParams.smoothingFilterSettings.UpdateSmoothingFilters();
            });

            HighlightSmoothingFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_SmoothingEnabledChanged(object sender, EventArgs e)
        {
            if (!this.action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_ENABLED))
            {
                this.action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
            }

            action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
            HighlightSmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_InvertChoicesChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_X))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_X);
            }

            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_Y))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_Y);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.INVERT_X);
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.INVERT_Y);
            });

            HighlightInvertChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_VerticalScaleChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_SCALE))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_SCALE);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.VERTICAL_SCALE);
            });

            HighlightVerticalScaleChanged?.Invoke(this, EventArgs.Empty);
            VerticalScaleMultiplierChanged?.Invoke(this, EventArgs.Empty);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(VerticalScaleMultiplier)));

            if (!_syncingAccelSens)
            {
                try
                {
                    _syncingAccelSens = true;
                    SyncGyroMinimumYFromBase();
                }
                finally
                {
                    _syncingAccelSens = false;
                }
            }
        }

        private void GyroMouseActionPropViewModel_SensitivityChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SENSITIVITY))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SENSITIVITY);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.SENSITIVITY);
            });

            HighlightSensitivityChanged?.Invoke(this, EventArgs.Empty);
            VerticalScaleMultiplierChanged?.Invoke(this, EventArgs.Empty);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(VerticalScaleMultiplier)));

            if (!_syncingAccelSens)
            {
                try
                {
                    _syncingAccelSens = true;
                    SyncGyroMinimumXFromBase();
                }
                finally
                {
                    _syncingAccelSens = false;
                }
            }
        }

        private void SyncGyroMinimumsFromBase()
        {
            SyncGyroMinimumXFromBase();
            SyncGyroMinimumYFromBase();
        }

        private void SyncGyroMinimumXFromBase()
        {
            double minX = Math.Clamp(action.mouseParams.sensitivity, 0.0, 100.0);
            if (action.mouseParams.minAccelXSens == minX) return;

            action.mouseParams.minAccelXSens = minX;
            MarkChangedProperty(GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(MinAccelXSens)));
            HighlightMinAccelXSensChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SyncGyroMinimumYFromBase()
        {
            double minY = Math.Clamp(action.mouseParams.verticalScale, 0.0, 100.0);
            if (action.mouseParams.minAccelYSens == minY) return;

            action.mouseParams.minAccelYSens = minY;
            MarkChangedProperty(GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(MinAccelYSens)));
            HighlightMinAccelYSensChanged?.Invoke(this, EventArgs.Empty);
        }

        private void MarkChangedProperty(string propertyKey)
        {
            if (!action.ChangedProperties.Contains(propertyKey))
            {
                action.ChangedProperties.Add(propertyKey);
            }

            action.RaiseNotifyPropertyChange(mapper, propertyKey);
        }

        private void GyroMouseActionPropViewModel_TriggerActivatesChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.TRIGGER_ACTIVATE))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.TRIGGER_ACTIVATE);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.TRIGGER_ACTIVATE);
            });

            HighlightGyroTriggerActivatesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.DEAD_ZONE))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.DEAD_ZONE);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.DEAD_ZONE);
            });

            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_VerticalDeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
            });

            HighlightVerticalDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_GyroAngleSnapDegreesChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
            });

            HighlightGyroAngleSnapDegreesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_GyroSmoothAngleSnapChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
            });

            HighlightGyroSmoothAngleSnapChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GyroMouseActionPropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.NAME);
            }

            ExecuteInMapperThread(() =>
            {
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.NAME);
            });

            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PopulateModel()
        {
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0 ? mapper.ActionProfile.CalibCounts : fullTurnCounts;
            CalculateTestRWC();

            //triggerButtonItems.AddRange(new GyroTriggerButtonItem[]
            //{
            //    new GyroTriggerButtonItem("Always On", JoypadActionCodes.AlwaysOn),
            //    new GyroTriggerButtonItem("A", JoypadActionCodes.BtnSouth),
            //    new GyroTriggerButtonItem("B", JoypadActionCodes.BtnEast),
            //    new GyroTriggerButtonItem("X", JoypadActionCodes.BtnWest),
            //    new GyroTriggerButtonItem("Y", JoypadActionCodes.BtnNorth),
            //    new GyroTriggerButtonItem("Left Bumper", JoypadActionCodes.BtnLShoulder),
            //    new GyroTriggerButtonItem("Right Bumper", JoypadActionCodes.BtnRShoulder),
            //    new GyroTriggerButtonItem("Left Trigger", JoypadActionCodes.AxisLTrigger),
            //    new GyroTriggerButtonItem("Right Trigger", JoypadActionCodes.AxisRTrigger),
            //    new GyroTriggerButtonItem("Left Grip", JoypadActionCodes.BtnLGrip),
            //    new GyroTriggerButtonItem("Right Grip", JoypadActionCodes.BtnRGrip),
            //    new GyroTriggerButtonItem("Stick Click", JoypadActionCodes.BtnThumbL),
            //    new GyroTriggerButtonItem("Left Touchpad Touch", JoypadActionCodes.LPadTouch),
            //    new GyroTriggerButtonItem("Right Touchpad Touch", JoypadActionCodes.RPadTouch),
            //    new GyroTriggerButtonItem("Left Touchpad Click", JoypadActionCodes.LPadClick),
            //    new GyroTriggerButtonItem("Right Touchpad Click", JoypadActionCodes.RPadClick),
            //    new GyroTriggerButtonItem("Back", JoypadActionCodes.BtnSelect),
            //    new GyroTriggerButtonItem("Start", JoypadActionCodes.BtnStart),
            //    new GyroTriggerButtonItem("Steam", JoypadActionCodes.BtnMode),
            //});

            foreach(ActionTriggerItem item in mapper.ActionTriggerItems)
            {
                triggerButtonItems.Add(new GyroTriggerButtonItem(item.DisplayName, item.Code));
            }

            foreach(JoypadActionCodes code in action.mouseParams.gyroTriggerButtons)
            {
                GyroTriggerButtonItem tempItem = triggerButtonItems.Find((item) => item.Code == code);
                if (tempItem != null)
                {
                    tempItem.Enabled = true;
                }
            }

            triggerButtonItems.ForEach((item) =>
            {
                item.EnabledChanged += GyroTriggerItem_EnabledChanged;
            });
        }

        private void GyroTriggerItem_EnabledChanged(object sender, EventArgs e)
        {
            GyroTriggerButtonItem tempItem = sender as GyroTriggerButtonItem;

            // Convert current array to temp List for convenience
            List<JoypadActionCodes> tempList = action.mouseParams.gyroTriggerButtons.ToList();

            // Add or remove code based on current enabled status
            if (tempItem.Enabled)
            {
                tempList.Add(tempItem.Code);
            }
            else
            {
                tempList.Remove(tempItem.Code);
            }

            if (!action.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.TRIGGER_BUTTONS))
            {
                action.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.TRIGGER_BUTTONS);
            }

            ExecuteInMapperThread(() =>
            {
                // Convert to array and save to action
                action.mouseParams.gyroTriggerButtons = tempList.ToArray();
                action.RaiseNotifyPropertyChange(mapper, GyroMouse.PropertyKeyStrings.TRIGGER_BUTTONS);
            });

            HighlightGyroTriggersChanged?.Invoke(this, EventArgs.Empty);
            GyroTriggerStringChanged?.Invoke(this, EventArgs.Empty);
            ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class GyroTriggerButtonItem
    {
        private string displayString;
        public string DisplayString
        {
            get => displayString;
        }

        private JoypadActionCodes code;
        public JoypadActionCodes Code => code;

        private bool enabled;
        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler EnabledChanged;

        public GyroTriggerButtonItem(string displayString, JoypadActionCodes code,
            bool enabled=false)
        {
            this.displayString = displayString;
            this.code = code;
            this.enabled = enabled;
        }
    }
}
