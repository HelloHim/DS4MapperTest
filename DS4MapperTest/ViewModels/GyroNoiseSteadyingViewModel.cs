using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DS4MapperTest.GyroActions;
using DS4MapperTest.ViewModels.GyroActionPropViewModels;

namespace DS4MapperTest.ViewModels
{
    // Profile-wide gyro noise/steadying tuning for the Gyro & Sensitivity >
    // Noise & Steadying subsection. Mirrors GyroSensitivityViewModel/
    // GyroCalibrationViewModel: reads from the first bound GyroMouse action found in
    // the profile and broadcasts every change to all GyroMouse actions.
    public class GyroNoiseSteadyingViewModel : INotifyPropertyChanged
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

        public double DeadZone
        {
            get => RepresentativeAction()?.mouseParams.deadzone ?? GyroMouseParams.DEAD_ZONE_DEFAULT;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null || action.mouseParams.deadzone == value) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.deadzone = value);
                RaisePropertyChanged(nameof(DeadZone));
            }
        }

        public double VerticalDeadZone
        {
            get => RepresentativeAction()?.mouseParams.verticalDeadZone ?? 0.0;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null || action.mouseParams.verticalDeadZone == value) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.verticalDeadZone = value);
                RaisePropertyChanged(nameof(VerticalDeadZone));
            }
        }

        public bool SmoothingEnabled
        {
            get => RepresentativeAction()?.mouseParams.smoothing ?? false;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null || action.mouseParams.smoothing == value) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.smoothing = value);
                RaisePropertyChanged(nameof(SmoothingEnabled));
            }
        }

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
                RaisePropertyChanged(nameof(SmoothPresetChoice));
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
            get => RepresentativeAction()?.mouseParams.smoothingFilterSettings.minCutOff ?? SmoothingFilterSettings.DEFAULT_MIN_CUTOFF;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double minCutoff = Math.Clamp(value, 0.0, 10.0);
                if (action.mouseParams.smoothingFilterSettings.minCutOff == minCutoff) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.smoothingFilterSettings.minCutOff = minCutoff);
                RaisePropertyChanged(nameof(SmoothingMinCutoff));
            }
        }

        public double SmoothingBeta
        {
            get => RepresentativeAction()?.mouseParams.smoothingFilterSettings.beta ?? SmoothingFilterSettings.DEFAULT_BETA;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double beta = Math.Clamp(value, 0.0, 1.0);
                if (action.mouseParams.smoothingFilterSettings.beta == beta) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.smoothingFilterSettings.beta = beta);
                RaisePropertyChanged(nameof(SmoothingBeta));
            }
        }

        public bool GyroJitterCompensation
        {
            get => RepresentativeAction()?.mouseParams.jitterCompensation ?? false;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null || action.mouseParams.jitterCompensation == value) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.jitterCompensation = value);
                RaisePropertyChanged(nameof(GyroJitterCompensation));
            }
        }

        public double GyroAngleSnapDegrees
        {
            get => RepresentativeAction()?.mouseParams.gyroAngleSnapDegrees ?? 0.0;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null) return;
                double clamped = Math.Clamp(value, 0.0, 45.0);
                if (action.mouseParams.gyroAngleSnapDegrees == clamped) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.gyroAngleSnapDegrees = clamped);
                RaisePropertyChanged(nameof(GyroAngleSnapDegrees));
            }
        }

        public bool GyroSmoothAngleSnap
        {
            get => RepresentativeAction()?.mouseParams.gyroSmoothAngleSnap ?? false;
            set
            {
                if (!_modelReady) return;
                var action = RepresentativeAction();
                if (action == null || action.mouseParams.gyroSmoothAngleSnap == value) return;
                BroadcastToAllGyroMouseActions(g => g.mouseParams.gyroSmoothAngleSnap = value);
                RaisePropertyChanged(nameof(GyroSmoothAngleSnap));
            }
        }

        public GyroNoiseSteadyingViewModel(Mapper mapper)
        {
            this.mapper = mapper;

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
