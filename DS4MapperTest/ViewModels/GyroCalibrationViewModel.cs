using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;

namespace DS4MapperTest.ViewModels
{
    // Wraps the profile-level Real World Calibration / In-game Sensitivity
    // fields (Mapper.ActionProfile.CalibRwc/CalibInGameSens/CalibCounts) that
    // are already shared across GyroMouse, StickFlickStick, TouchpadFlickStick
    // and camera-turn button outputs. Not tied to any single bound action, so
    // it can be surfaced once for the whole profile on the Gyro subsection.
    public class GyroCalibrationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _modelReady = false;
        private bool _applyingPreset = false;

        private Mapper mapper;
        public Mapper Mapper => mapper;

        private double fullTurnCounts = 1800.0;

        public double RealWorldCalibration
        {
            get => mapper.ActionProfile.CalibRwc;
            set
            {
                if (!_modelReady) return;
                if (mapper.ActionProfile.CalibRwc == value) return;
                mapper.ActionProfile.CalibRwc = value;
                if (!_applyingPreset) TryMatchPreset();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                SyncCalibToProfile();
            }
        }

        public double InGameSens
        {
            get => mapper.ActionProfile.CalibInGameSens;
            set
            {
                if (!_modelReady) return;
                if (mapper.ActionProfile.CalibInGameSens == value) return;
                mapper.ActionProfile.CalibInGameSens = value;
                if (!_applyingPreset) TryMatchPreset();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                SyncCalibToProfile();
            }
        }

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
                InGameSens = value.InGameSens;
                RealWorldCalibration = value.RWC;
                fullTurnCounts = value.Counts;
                SyncCalibToProfile();
                _applyingPreset = false;
            }
        }

        public GyroCalibrationViewModel(Mapper mapper)
        {
            this.mapper = mapper;
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0
                ? mapper.ActionProfile.CalibCounts : fullTurnCounts;

            // HandyControl's NumericUpDown fires ValueChanged(Minimum) during
            // control init before the binding has populated the control with
            // the real value, which would corrupt the profile calibration
            // fields. _modelReady is set via a low-priority dispatcher post
            // that runs after all Loaded-priority control events, mirroring
            // GyroMouseActionPropViewModel/StickFlickStickPropViewModel.
            double savedRwc = mapper.ActionProfile.CalibRwc;
            double savedInGameSens = mapper.ActionProfile.CalibInGameSens;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    mapper.ActionProfile.CalibRwc = savedRwc;
                    mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            mapper.ActionProfile.CalibRwc = savedRwc;
                            mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                            _modelReady = true;
                            TryMatchPreset();
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                        }));
                }));
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

        private void SyncCalibToProfile()
        {
            double rwc = mapper.ActionProfile.CalibRwc;
            double inGameSens = mapper.ActionProfile.CalibInGameSens;
            double counts = fullTurnCounts;
            mapper.ActionProfile.CalibCounts = counts;
            mapper.ProcessMappingChangeAction(() =>
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
                            if (mapAction is ButtonAction ba)
                                foreach (var func in ba.ActionFuncs)
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
    }
}
