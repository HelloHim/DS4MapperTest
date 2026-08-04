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
    // Wraps the profile-level calibration fields (Mapper.ActionProfile.CalibMode/
    // CalibRwc/CalibInGameSens/CalibCounts) that are shared across GyroMouse,
    // StickFlickStick, TouchpadFlickStick and camera-turn button outputs. Mirrors
    // the calibration section of GyroMouseActionPropViewModel/StickFlickStickPropViewModel,
    // but is not tied to any single bound action, so it can be surfaced once for
    // the whole profile on the Gyro subsection.
    public class GyroCalibrationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _modelReady = false;
        private bool _applyingPreset = false;

        private Mapper mapper;
        public Mapper Mapper => mapper;

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
            }
        }

        public bool IsRwcMode
        {
            get => CalibMode == CalibMode.RwcMode;
            set { if (value) CalibMode = CalibMode.RwcMode; }
        }

        public bool IsCountsMode
        {
            get => CalibMode == CalibMode.CountsMode;
            set { if (value) CalibMode = CalibMode.CountsMode; }
        }

        public string MasterCalibrationLabel => IsCountsMode ? "Counts" : "Real World Calibration";

        public double MasterCalibrationValue
        {
            get => IsCountsMode ? FullTurnCounts : RealWorldCalibration;
            set
            {
                if (IsCountsMode) FullTurnCounts = value;
                else RealWorldCalibration = value;
            }
        }

        private double fullTurnCounts = 1800.0;
        public double FullTurnCounts
        {
            get => fullTurnCounts;
            set
            {
                if (!_modelReady) return;
                if (value == 0.0) return;
                bool countsChanged = fullTurnCounts != value;
                fullTurnCounts = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
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
                if (IsCountsMode) CalculateRwcFromCounts();
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
                InGameSens = value.RWC * 360.0 / FullTurnCounts;
                RealWorldCalibration = value.RWC;
                _applyingPreset = false;
            }
        }

        public GyroCalibrationViewModel(Mapper mapper)
        {
            this.mapper = mapper;
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0
                ? mapper.ActionProfile.CalibCounts : fullTurnCounts;

            mapper.ActionProfile.CalibModeChanged += ActionProfile_CalibModeChanged;
            mapper.ActionProfile.CalibRwcChanged += ActionProfile_CalibValuesChanged;
            mapper.ActionProfile.CalibInGameSensChanged += ActionProfile_CalibValuesChanged;
            mapper.ActionProfile.CalibCountsChanged += ActionProfile_CalibValuesChanged;

            // HandyControl's NumericUpDown fires ValueChanged(Minimum) during
            // control init before the binding has populated the control with
            // the real value, which would corrupt the profile calibration
            // fields. _modelReady is set via a low-priority dispatcher post
            // that runs after all Loaded-priority control events, mirroring
            // GyroMouseActionPropViewModel/StickFlickStickPropViewModel.
            double savedRwc = mapper.ActionProfile.CalibRwc;
            double savedInGameSens = mapper.ActionProfile.CalibInGameSens;
            double savedCounts = fullTurnCounts;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    mapper.ActionProfile.CalibRwc = savedRwc;
                    mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                    fullTurnCounts = savedCounts;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                    RaiseCalibModePropertyChanges();
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            mapper.ActionProfile.CalibRwc = savedRwc;
                            mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                            fullTurnCounts = savedCounts;
                            _modelReady = true;
                            TryMatchPreset();
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                            RaiseCalibModePropertyChanges();
                        }));
                }));
        }

        private void CalculateRwcFromCounts()
        {
            double rwc = fullTurnCounts * InGameSens / 360.0;
            if (mapper.ActionProfile.CalibRwc == rwc) return;
            mapper.ActionProfile.CalibRwc = rwc;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
        }

        private void TryMatchPreset()
        {
            double rwc = mapper.ActionProfile.CalibRwc;
            GameCalibPreset match = GameCalibPreset.All.FirstOrDefault(
                p => !p.IsCustom &&
                     Math.Abs(p.RWC - rwc) < 1e-3);
            GameCalibPreset next = match ?? GameCalibPreset.Custom;
            if (_selectedPreset == next) return;
            _selectedPreset = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
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
        }

        // Another calibration panel (Gyro/Stick Flick Stick/Touchpad Flick Stick all
        // share the same profile-level RWC/In-Game Sens/Counts) changed a value.
        // Refresh this instance's own cached counts and bound properties to match.
        private void ActionProfile_CalibValuesChanged(object sender, EventArgs e)
        {
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0
                ? mapper.ActionProfile.CalibCounts : fullTurnCounts;
            if (!_applyingPreset) TryMatchPreset();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
        }
    }
}
