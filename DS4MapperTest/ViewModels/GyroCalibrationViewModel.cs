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
    public class GyroCalibrationViewModel : INotifyPropertyChanged, ICalibrationPanelViewModel
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _modelReady = false;
        private bool _applyingPreset = false;
        // Guards ActionProfile_CalibValuesChanged against reacting to this instance's
        // own in-flight writes: SyncCalibToProfile/CalculateRwcFromCounts write CalibRwc,
        // CalibInGameSens and CalibCounts one at a time, so between those writes the
        // profile is briefly inconsistent. Without this guard, the CalibRwcChanged fired
        // by the first write re-enters ActionProfile_CalibValuesChanged, which re-derives
        // fullTurnCounts from the not-yet-updated CalibCounts and clobbers the edit the
        // user just made before it reaches the profile.
        private bool _syncingProfile = false;

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
                _syncingProfile = true;
                try { SyncCalibToProfile(); }
                finally { _syncingProfile = false; }
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

        public string MasterCalibrationLabel => IsCountsMode ? "Counts" : "RWC";

        public double MasterCalibrationValue
        {
            get => IsCountsMode ? FullTurnCounts : RealWorldCalibration;
            set
            {
                if (IsCountsMode) FullTurnCounts = value;
                else RealWorldCalibration = value;
            }
        }

        // The value NOT currently editable as the mode's primary field, shown
        // read-only so the user can see both representations without switching modes.
        public string DerivedLabel => IsCountsMode ? "RWC" : "Counts";
        public double DerivedValue => IsCountsMode ? RealWorldCalibration : FullTurnCounts;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DerivedValue)));
                if (!countsChanged) return;
                if (IsCountsMode)
                {
                    _syncingProfile = true;
                    try
                    {
                        CalculateRwcFromCounts();
                        SyncCalibToProfile();
                        UpdatePresetFromCurrentRwc();
                    }
                    finally { _syncingProfile = false; }
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
                _syncingProfile = true;
                try
                {
                    // Counts is the derived half in RWC mode and has to be recomputed from the
                    // new RWC right here. ActionProfile_CalibValuesChanged is suppressed while
                    // this instance is writing the profile, so nothing else refreshes the
                    // cached fullTurnCounts: the panel would keep showing the pre-edit Counts,
                    // and a later switch to Counts mode re-derives RWC from that stale number,
                    // silently reverting the edit.
                    if (IsRwcMode) CalculateCountsFromRwc();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DerivedValue)));
                    SyncCalibToProfile();
                    UpdatePresetFromCurrentRwc();
                }
                finally { _syncingProfile = false; }
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
                _syncingProfile = true;
                try
                {
                    if (IsCountsMode) CalculateRwcFromCounts();
                    else CalculateCountsFromRwc();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                    SyncCalibToProfile();
                    UpdatePresetFromCurrentRwc();
                }
                finally { _syncingProfile = false; }
            }
        }

        public IReadOnlyList<GameCalibPreset> GamePresets => GameCalibPreset.All;

        public GameCalibPreset SelectedPreset
        {
            get => GameCalibPreset.FindByName(mapper.ActionProfile.CalibPresetName) ??
                GameCalibPreset.Custom;
            set
            {
                if (!_modelReady) return;
                GameCalibPreset next = value ?? GameCalibPreset.Custom;
                if (mapper.ActionProfile.CalibPresetName == next.Name) return;
                mapper.ActionProfile.CalibPresetName = next.Name;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
                if (next.IsCustom) return;
                _applyingPreset = true;
                if (IsCountsMode)
                {
                    // A preset only ever names an RWC, and In-Game Sensitivity is the player's
                    // own game setting, so it stays exactly as they had it in either mode.
                    // From Counts mode that means moving Counts to whatever reproduces the
                    // preset's RWC at that sensitivity.
                    if (InGameSens > 0.0) FullTurnCounts = next.RWC * 360.0 / InGameSens;
                }
                else
                {
                    // RWC is this mode's fixed master: move it directly to the preset's value
                    // and leave sensitivity exactly as the user had it.
                    RealWorldCalibration = next.RWC;
                }
                _applyingPreset = false;
            }
        }

        public GyroCalibrationViewModel(Mapper mapper)
        {
            this.mapper = mapper;
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0
                ? mapper.ActionProfile.CalibCounts : fullTurnCounts;

            // Profile subscriptions are owned by CalibrationModeControl's Loaded/Unloaded
            // (see AttachProfileCalibEvents), not by this constructor: the profile outlives
            // every panel that shows it, so subscribing here left one dead ViewModel wired
            // to it per panel rebuild.
            BeginPanelInit();
        }

        // HandyControl's NumericUpDown fires ValueChanged(Minimum) during
        // control init before the binding has populated the control with
        // the real value, which would corrupt the profile calibration
        // fields. _modelReady is set via a low-priority dispatcher post
        // that runs after all Loaded-priority control events, mirroring
        // GyroMouseActionPropViewModel/StickFlickStickPropViewModel.
        // Run once per control, not once per ViewModel: this instance is cached by its
        // panel and can outlive the control showing it, and the replacement control
        // initialises its fields all over again.
        public void BeginPanelInit()
        {
            _modelReady = false;
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
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
                            RaiseCalibModePropertyChanges();
                        }));
                }));
        }

        // Reference counted so the several panels that bind one shared instance (the three
        // stick side sections, for example) can each attach and detach independently.
        private int _panelAttachCount = 0;

        public void AttachProfileCalibEvents()
        {
            _panelAttachCount++;
            if (_panelAttachCount == 1)
            {
                mapper.ActionProfile.CalibModeChanged += ActionProfile_CalibModeChanged;
                mapper.ActionProfile.CalibRwcChanged += ActionProfile_CalibValuesChanged;
                mapper.ActionProfile.CalibInGameSensChanged += ActionProfile_CalibValuesChanged;
                mapper.ActionProfile.CalibCountsChanged += ActionProfile_CalibValuesChanged;
                mapper.ActionProfile.CalibPresetNameChanged += ActionProfile_CalibPresetNameChanged;
            }

            // Calibration is one profile-wide setting, so anything another panel changed
            // while this one was off screen is the current truth: pull it back in before
            // this panel is shown, rather than displaying (and later writing back) the
            // values it happened to be holding when it was detached. BeginPanelInit then
            // holds the fields read-only until the control showing them has settled.
            RefreshFromProfile();
            BeginPanelInit();
        }

        public void DetachProfileCalibEvents()
        {
            if (_panelAttachCount == 0) return;
            _panelAttachCount--;
            if (_panelAttachCount > 0) return;

            mapper.ActionProfile.CalibModeChanged -= ActionProfile_CalibModeChanged;
            mapper.ActionProfile.CalibRwcChanged -= ActionProfile_CalibValuesChanged;
            mapper.ActionProfile.CalibInGameSensChanged -= ActionProfile_CalibValuesChanged;
            mapper.ActionProfile.CalibCountsChanged -= ActionProfile_CalibValuesChanged;
            mapper.ActionProfile.CalibPresetNameChanged -= ActionProfile_CalibPresetNameChanged;
        }

        private void RefreshFromProfile()
        {
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0
                ? mapper.ActionProfile.CalibCounts : fullTurnCounts;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
            RaiseCalibModePropertyChanges();
        }

        private void CalculateRwcFromCounts()
        {
            double rwc = fullTurnCounts * InGameSens / 360.0;
            if (mapper.ActionProfile.CalibRwc == rwc) return;
            mapper.ActionProfile.CalibRwc = rwc;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DerivedValue)));
        }

        private void CalculateCountsFromRwc()
        {
            double counts = InGameSens > 0.0
                ? mapper.ActionProfile.CalibRwc * 360.0 / InGameSens
                : 0.0;
            if (fullTurnCounts == counts) return;
            fullTurnCounts = counts;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DerivedValue)));
        }

        // Whenever RWC's authoritative value settles (direct edit, derived from Counts, or
        // derived from a sensitivity change), check whether it now matches a known game
        // preset within tolerance and reflect that in the preset dropdown; falls back to
        // Custom when it doesn't. Skipped while a preset is actively being applied, since
        // that flow already knows exactly which preset it is setting.
        private void UpdatePresetFromCurrentRwc()
        {
            if (_applyingPreset) return;
            string matchedName = (GameCalibPreset.MatchByRwc(mapper.ActionProfile.CalibRwc, SelectedPreset) ??
                GameCalibPreset.Custom).Name;
            mapper.ActionProfile.CalibPresetName = matchedName;
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DerivedLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DerivedValue)));
        }

        private void ActionProfile_CalibModeChanged(object sender, EventArgs e)
        {
            RaiseCalibModePropertyChanges();
        }

        private void ActionProfile_CalibPresetNameChanged(object sender, EventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        }

        // Another calibration panel (Gyro/Stick Flick Stick/Touchpad Flick Stick all
        // share the same profile-level RWC/In-Game Sens/Counts) changed a value.
        // Refresh this instance's own cached counts and bound properties to match.
        // Skipped while this instance is mid-write to the profile itself (_syncingProfile):
        // its own multi-field writes leave the profile briefly inconsistent between steps,
        // and re-deriving fullTurnCounts from that in-between state would clobber the edit
        // before it finishes reaching the profile.
        private void ActionProfile_CalibValuesChanged(object sender, EventArgs e)
        {
            if (_syncingProfile) return;
            RefreshFromProfile();
        }
    }
}
