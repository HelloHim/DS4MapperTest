using System;
using System.Collections.Generic;
using System.ComponentModel;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.ViewModels.GyroActionPropViewModels;

namespace DS4MapperTest.ViewModels
{
    // Profile-wide gyro sensitivity/acceleration tuning for the Gyro & Sensitivity >
    // Sensitivity subsection. Mirrors GyroCalibrationViewModel: reads from the first
    // bound GyroMouse action found in the profile and broadcasts every change to all
    // GyroMouse actions, so every gyro binding in the profile shares one tuned feel.
    public class GyroSensitivityViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _modelReady = false;

        private Mapper mapper;
        public Mapper Mapper => mapper;

        private GyroMouse RepresentativeAction()
        {
            foreach (var set in mapper.ActionProfile.ActionSets)
                foreach (var layer in set.ActionLayers)
                    foreach (var mapAction in layer.normalActionDict.Values)
                        if (mapAction is GyroMouse gyroMouse) return gyroMouse;
            return null;
        }

        public bool HasGyroMouseAction => RepresentativeAction() != null;

        private void BroadcastToAllGyroMouseActions(Action<GyroMouse> apply)
        {
            mapper.ProcessMappingChangeAction(() =>
            {
                foreach (var set in mapper.ActionProfile.ActionSets)
                    foreach (var layer in set.ActionLayers)
                        foreach (var mapAction in layer.normalActionDict.Values)
                            if (mapAction is GyroMouse gyroMouse) apply(gyroMouse);
            });
        }

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
            get => RepresentativeAction()?.mouseParams.accelCurve ?? GyroMouseAccelCurveChoice.None;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null || action.mouseParams.accelCurve == value) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.accelCurve = value);
                RaisePropertyChanged(nameof(AccelCurveChoice), nameof(StaticSensUsed), nameof(AccelCurveUsed),
                    nameof(UsesMaxThreshold), nameof(PowerCurveUsed), nameof(NaturalCurveUsed));
            }
        }

        public bool StaticSensUsed => AccelCurveChoice == GyroMouseAccelCurveChoice.None;
        public bool AccelCurveUsed => AccelCurveChoice != GyroMouseAccelCurveChoice.None;

        public bool UsesMaxThreshold
        {
            get
            {
                switch (AccelCurveChoice)
                {
                    case GyroMouseAccelCurveChoice.Linear:
                    case GyroMouseAccelCurveChoice.Quadratic:
                    case GyroMouseAccelCurveChoice.Cubic:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool PowerCurveUsed => AccelCurveChoice == GyroMouseAccelCurveChoice.Power;
        public bool NaturalCurveUsed => AccelCurveChoice == GyroMouseAccelCurveChoice.Natural;

        public double Sensitivity
        {
            get => RepresentativeAction()?.mouseParams.sensitivity ?? GyroMouseParams.SENSITIVITY_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double sensitivity = Math.Clamp(value, 0.0, 10.0);
                if (action.mouseParams.sensitivity == sensitivity) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.sensitivity = sensitivity);
                RaisePropertyChanged(nameof(Sensitivity), nameof(VerticalScaleMultiplier));
            }
        }

        public double VerticalScale
        {
            get => RepresentativeAction()?.mouseParams.verticalScale ?? GyroMouseParams.VERTICAL_SCALE_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double verticalScale = Math.Clamp(value, 0.0, 10.0);
                if (action.mouseParams.verticalScale == verticalScale) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.verticalScale = verticalScale);
                RaisePropertyChanged(nameof(VerticalScale), nameof(VerticalScaleMultiplier));
            }
        }

        private bool verticalScaleIsAbsoluteMode = true;
        public bool VerticalScaleIsAbsoluteMode
        {
            get => verticalScaleIsAbsoluteMode;
            set
            {
                if (!value || verticalScaleIsAbsoluteMode) return;
                verticalScaleIsAbsoluteMode = value;
                RaisePropertyChanged(nameof(VerticalScaleIsAbsoluteMode), nameof(VerticalScaleIsMultiplierMode));
            }
        }

        public bool VerticalScaleIsMultiplierMode
        {
            get => !verticalScaleIsAbsoluteMode;
            set
            {
                if (!value || !verticalScaleIsAbsoluteMode) return;
                verticalScaleIsAbsoluteMode = !value;
                RaisePropertyChanged(nameof(VerticalScaleIsAbsoluteMode), nameof(VerticalScaleIsMultiplierMode));
            }
        }

        public double VerticalScaleMultiplier
        {
            get
            {
                var action = RepresentativeAction();
                if (action == null) return 1.0;
                double sens = action.mouseParams.sensitivity;
                if (Math.Abs(sens) < 1e-10) return action.mouseParams.verticalScale;
                return Math.Round(action.mouseParams.verticalScale / sens, 4);
            }
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double sens = action.mouseParams.sensitivity;
                double abs = Math.Abs(sens) < 1e-10 ? value : value * sens;
                double verticalScale = Math.Clamp(abs, 0.0, 10.0);
                if (action.mouseParams.verticalScale == verticalScale) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.verticalScale = verticalScale);
                RaisePropertyChanged(nameof(VerticalScale), nameof(VerticalScaleMultiplier));
            }
        }

        public double MinAccelXSens
        {
            get => RepresentativeAction()?.mouseParams.minAccelXSens ?? GyroMouseParams.MIN_ACCEL_SENS_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.0, 100.0);
                if (action.mouseParams.minAccelXSens == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.minAccelXSens = clamped);
                RaisePropertyChanged(nameof(MinAccelXSens));
            }
        }

        public double MaxAccelXSens
        {
            get => RepresentativeAction()?.mouseParams.maxAccelXSens ?? GyroMouseParams.MAX_ACCEL_SENS_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.0, 100.0);
                if (action.mouseParams.maxAccelXSens == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.maxAccelXSens = clamped);
                RaisePropertyChanged(nameof(MaxAccelXSens));
            }
        }

        public double MinAccelYSens
        {
            get => RepresentativeAction()?.mouseParams.minAccelYSens ?? GyroMouseParams.VERTICAL_SCALE_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.0, 100.0);
                if (action.mouseParams.minAccelYSens == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.minAccelYSens = clamped);
                RaisePropertyChanged(nameof(MinAccelYSens));
            }
        }

        public double MaxAccelYSens
        {
            get => RepresentativeAction()?.mouseParams.maxAccelYSens ?? GyroMouseParams.VERTICAL_SCALE_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.0, 100.0);
                if (action.mouseParams.maxAccelYSens == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.maxAccelYSens = clamped);
                RaisePropertyChanged(nameof(MaxAccelYSens));
            }
        }

        public double MinGyroThreshold
        {
            get => RepresentativeAction()?.mouseParams.minGyroThreshold ?? GyroMouseParams.MIN_GYRO_THRESHOLD_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.0, 500.0);
                if (action.mouseParams.minGyroThreshold == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.minGyroThreshold = clamped);
                RaisePropertyChanged(nameof(MinGyroThreshold));
            }
        }

        public double MaxGyroThreshold
        {
            get => RepresentativeAction()?.mouseParams.maxGyroThreshold ?? GyroMouseParams.MAX_GYRO_THRESHOLD_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.0, 500.0);
                if (action.mouseParams.maxGyroThreshold == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.maxGyroThreshold = clamped);
                RaisePropertyChanged(nameof(MaxGyroThreshold));
            }
        }

        public double PowerCurveVRef
        {
            get => RepresentativeAction()?.mouseParams.powerVRef ?? GyroMouseParams.POWER_VREF_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.1, 500.0);
                if (action.mouseParams.powerVRef == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.powerVRef = clamped);
                RaisePropertyChanged(nameof(PowerCurveVRef));
            }
        }

        public double PowerCurveExponent
        {
            get => RepresentativeAction()?.mouseParams.powerExponent ?? GyroMouseParams.POWER_EXPONENT_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 1.0, 500.0);
                if (action.mouseParams.powerExponent == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.powerExponent = clamped);
                RaisePropertyChanged(nameof(PowerCurveExponent));
            }
        }

        public double NaturalVHalf
        {
            get => RepresentativeAction()?.mouseParams.naturalVHalf ?? GyroMouseParams.NATURAL_VHALF_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 1.0, 500.0);
                if (action.mouseParams.naturalVHalf == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.naturalVHalf = clamped);
                RaisePropertyChanged(nameof(NaturalVHalf));
            }
        }

        private BasicActionCommand resetAccelerationDefaultsComm;
        public BasicActionCommand ResetAccelerationDefaultsComm => resetAccelerationDefaultsComm;

        public GyroSensitivityViewModel(Mapper mapper)
        {
            this.mapper = mapper;

            resetAccelerationDefaultsComm = new BasicActionCommand((parameter) =>
            {
                if (RepresentativeAction() == null) return;
                BroadcastToAllGyroMouseActions(g =>
                {
                    g.mouseParams.sensitivity = GyroMouseParams.SENSITIVITY_DEFAULT;
                    g.mouseParams.verticalScale = GyroMouseParams.VERTICAL_SCALE_DEFAULT;
                    g.mouseParams.minAccelXSens = GyroMouseParams.MIN_ACCEL_SENS_DEFAULT;
                    g.mouseParams.minAccelYSens = GyroMouseParams.VERTICAL_SCALE_DEFAULT;
                    g.mouseParams.maxAccelXSens = GyroMouseParams.MAX_ACCEL_SENS_DEFAULT;
                    g.mouseParams.maxAccelYSens = GyroMouseParams.VERTICAL_SCALE_DEFAULT;
                    g.mouseParams.accelCurve = GyroMouseParams.ACCEL_CURVE_DEFAULT;
                    g.mouseParams.minGyroThreshold = GyroMouseParams.MIN_GYRO_THRESHOLD_DEFAULT;
                    g.mouseParams.maxGyroThreshold = GyroMouseParams.MAX_GYRO_THRESHOLD_DEFAULT;
                    g.mouseParams.naturalVHalf = GyroMouseParams.NATURAL_VHALF_DEFAULT;
                    g.mouseParams.powerVRef = GyroMouseParams.POWER_VREF_DEFAULT;
                    g.mouseParams.powerExponent = GyroMouseParams.POWER_EXPONENT_DEFAULT;
                });
                RaisePropertyChanged(nameof(Sensitivity), nameof(VerticalScale), nameof(VerticalScaleMultiplier),
                    nameof(MinAccelXSens), nameof(MaxAccelXSens), nameof(MinAccelYSens), nameof(MaxAccelYSens),
                    nameof(AccelCurveChoice), nameof(StaticSensUsed), nameof(AccelCurveUsed), nameof(UsesMaxThreshold),
                    nameof(PowerCurveUsed), nameof(NaturalCurveUsed), nameof(MinGyroThreshold), nameof(MaxGyroThreshold),
                    nameof(NaturalVHalf), nameof(PowerCurveVRef), nameof(PowerCurveExponent));
            });

            // HandyControl's NumericUpDown fires ValueChanged(Minimum) during control
            // init before bindings populate real values; deferring _modelReady until
            // after Loaded-priority control events avoids broadcasting a corrupted
            // zero across every GyroMouse action in the profile.
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            _modelReady = true;
                        }));
                }));
        }

        private void RaisePropertyChanged(params string[] names)
        {
            foreach (string name in names)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
