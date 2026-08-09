using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels.Common;

namespace DS4MapperTest.ViewModels.StickActionPropViewModels
{
    public class StickFlickStickPropViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _modelReady = false;
        private Mapper mapper;
        public Mapper Mapper => mapper;

        private StickFlickStick action;
        public StickFlickStick Action => action;

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

        public int SelectedSubModeIndex
        {
            get => (int)action.SubMode;
            set
            {
                FlickStickSubMode subMode = (FlickStickSubMode)value;
                if (action.SubMode == subMode) return;
                action.SubMode = subMode;
                SubModeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSubModeIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowFlickSettings)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowRotationSettings)));
            }
        }
        public event EventHandler SubModeChanged;

        public bool ShowFlickSettings => action.SubMode != FlickStickSubMode.RotateOnly;

        public bool ShowRotationSettings => action.SubMode != FlickStickSubMode.FlickOnly;

        public int SelectedSnapAngleIndex
        {
            get => (int)action.SnapAngle;
            set
            {
                FlickSnapAngle snapAngle = (FlickSnapAngle)value;
                if (action.SnapAngle == snapAngle) return;
                action.SnapAngle = snapAngle;
                SnapAngleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSnapAngleIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SnapEnabled)));
            }
        }
        public event EventHandler SnapAngleChanged;

        public bool SnapEnabled => action.SnapAngle != FlickSnapAngle.Off;

        public double SnapStrength
        {
            get => action.SnapStrength;
            set
            {
                if (action.SnapStrength == value) return;
                action.SnapStrength = value;
                SnapStrengthChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SnapStrengthChanged;

        public bool HighlightSnapAngle
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SNAP_ANGLE);
        }
        public event EventHandler HighlightSnapAngleChanged;

        public bool HighlightSnapStrength
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SNAP_STRENGTH);
        }
        public event EventHandler HighlightSnapStrengthChanged;

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
                CalculateTestRWC();
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
                if (IsCountsMode) CalculateCountsFromRwc();
                if (!_applyingPreset) TryMatchPreset();
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

        private bool _applyingPreset = false;
        private GameCalibPreset _selectedPreset = GameCalibPreset.Custom;

        public IReadOnlyList<GameCalibPreset> GamePresets => GameCalibPreset.All;

        public GameCalibPreset SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (!_modelReady) return;
                if (_selectedPreset == value) return;
                _selectedPreset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
                if (value == null || value.IsCustom) return;

                // A preset supplies the game's RWC. The measured 360-degree
                // count total belongs to the user's mouse/game setup, so keep it
                // unchanged and derive the sensitivity needed for that game.
                if (FullTurnCounts <= 0.0) return;
                _applyingPreset = true;
                double preservedCounts = FullTurnCounts;
                mapper.ActionProfile.CalibInGameSens = value.RWC * 360.0 / preservedCounts;
                mapper.ActionProfile.CalibRwc = value.RWC;
                fullTurnCounts = preservedCounts;
                CalculateTestRWC();
                SyncCalibToProfile();
                _applyingPreset = false;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // --- End calibration fields ---

        public double FlickThreshold
        {
            get => action.FlickThreshold;
            set
            {
                if (action.FlickThreshold == value) return;
                action.FlickThreshold = value;
                FlickThresholdChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler FlickThresholdChanged;

        public double FlickTime
        {
            get => action.FlickTime * 1000.0;
            set
            {
                double seconds = value / 1000.0;
                if (action.FlickTime == seconds) return;
                action.FlickTime = seconds;
                FlickTimeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler FlickTimeChanged;

        public double FlickTimeExponent
        {
            get => action.FlickTimeExponent;
            set
            {
                if (action.FlickTimeExponent == value) return;
                action.FlickTimeExponent = value;
                FlickTimeExponentChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler FlickTimeExponentChanged;

        public double MinAngleThreshold
        {
            get => action.MinAngleThreshold;
            set
            {
                if (action.MinAngleThreshold == value) return;
                action.MinAngleThreshold = value;
                MinAngleThresholdChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MinAngleThresholdChanged;

        public double ReleaseDampeningSpeed
        {
            get => action.ReleaseDampeningSpeed;
            set
            {
                if (action.ReleaseDampeningSpeed == value) return;
                action.ReleaseDampeningSpeed = value;
                ReleaseDampeningSpeedChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ReleaseDampeningSpeedChanged;

        public bool MultiplierCompensation
        {
            get => action.MultiplierCompensation;
            set
            {
                if (action.MultiplierCompensation == value) return;
                action.MultiplierCompensation = value;
                MultiplierCompensationChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MultiplierCompensation)));
            }
        }
        public event EventHandler MultiplierCompensationChanged;

        public double AccelerationMultiplier
        {
            get => action.AccelerationMultiplier;
            set
            {
                if (action.AccelerationMultiplier == value) return;
                action.AccelerationMultiplier = value;
                AccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler AccelerationMultiplierChanged;

        public double RotateSmoothOverride
        {
            get => action.RotateSmoothOverride;
            set
            {
                if (action.RotateSmoothOverride == value) return;
                action.RotateSmoothOverride = value;
                RotateSmoothOverrideChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RotateSmoothOverrideChanged;

        public bool HighlightReleaseDampeningSpeed
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED);
        }
        public event EventHandler HighlightReleaseDampeningSpeedChanged;

        public bool HighlightMultiplierCompensation
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
        }
        public event EventHandler HighlightMultiplierCompensationChanged;

        public bool HighlightAccelerationMultiplier
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
        }
        public event EventHandler HighlightAccelerationMultiplierChanged;

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightFlickThreshold
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_THRESHOLD);
        }
        public event EventHandler HighlightFlickThresholdChanged;

        public bool HighlightFlickTime
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_TIME);
        }
        public event EventHandler HighlightFlickTimeChanged;

        public bool HighlightFlickTimeExponent
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT);
        }
        public event EventHandler HighlightFlickTimeExponentChanged;

        public bool HighlightMinAngleThreshold
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD);
        }
        public event EventHandler HighlightMinAngleThresholdChanged;

        public bool HighlightRotateSmoothOverride
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE);
        }
        public event EventHandler HighlightRotateSmoothOverrideChanged;

        public event EventHandler ActionPropertyChanged;
        public event EventHandler<StickMapAction> ActionChanged;

        private bool usingRealAction = false;

        public StickFlickStickPropViewModel(Mapper mapper, StickMapAction action)
        {
            this.mapper = mapper;
            this.action = action as StickFlickStick;
            usingRealAction = true;

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                StickFlickStick baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as StickFlickStick;
                StickFlickStick tempAction = new StickFlickStick();
                tempAction.SoftCopyFromParent(baseLayerAction);
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;

                this.action = tempAction;
                usingRealAction = false;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            PrepareModel();

            copyTestRWCComm = new BasicActionCommand((parameter) =>
            {
                RealWorldCalibration = CalculatedRWC;
            });

            NameChanged += StickFlickStickPropViewModel_NameChanged;
            SubModeChanged += StickFlickStickPropViewModel_SubModeChanged;
            FlickThresholdChanged += StickFlickStickPropViewModel_FlickThresholdChanged;
            FlickTimeChanged += StickFlickStickPropViewModel_FlickTimeChanged;
            FlickTimeExponentChanged += StickFlickStickPropViewModel_FlickTimeExponentChanged;
            MinAngleThresholdChanged += StickFlickStickPropViewModel_MinAngleThresholdChanged;
            ReleaseDampeningSpeedChanged += StickFlickStickPropViewModel_ReleaseDampeningSpeedChanged;
            MultiplierCompensationChanged += StickFlickStickPropViewModel_MultiplierCompensationChanged;
            AccelerationMultiplierChanged += StickFlickStickPropViewModel_AccelerationMultiplierChanged;
            RotateSmoothOverrideChanged += StickFlickStickPropViewModel_RotateSmoothOverrideChanged;
            SnapAngleChanged += StickFlickStickPropViewModel_SnapAngleChanged;
            SnapStrengthChanged += StickFlickStickPropViewModel_SnapStrengthChanged;
            mapper.ActionProfile.CalibModeChanged += ActionProfile_CalibModeChanged;
            mapper.ActionProfile.CalibRwcChanged += ActionProfile_CalibValuesChanged;
            mapper.ActionProfile.CalibInGameSensChanged += ActionProfile_CalibValuesChanged;
            mapper.ActionProfile.CalibCountsChanged += ActionProfile_CalibValuesChanged;

            double savedInGameSens = mapper.ActionProfile.CalibInGameSens;
            double savedRwc = mapper.ActionProfile.CalibRwc;
            double savedCounts = fullTurnCounts;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                    mapper.ActionProfile.CalibRwc = savedRwc;
                    fullTurnCounts = savedCounts;
                    CalculateTestRWC();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                    RaiseCalibModePropertyChanges();
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            mapper.ActionProfile.CalibInGameSens = savedInGameSens;
                            mapper.ActionProfile.CalibRwc = savedRwc;
                            fullTurnCounts = savedCounts;
                            CalculateTestRWC();
                            _modelReady = true;
                            TryMatchPreset();
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
                            RaiseCalibModePropertyChanges();
                        }));
                }));
        }

        private void PrepareModel()
        {
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0 ? mapper.ActionProfile.CalibCounts : fullTurnCounts;
            CalculateTestRWC();
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

        private void CalculateCountsFromRwc()
        {
            double counts = InGameSens > 0.0
                ? mapper.ActionProfile.CalibRwc * 360.0 / InGameSens
                : 0.0;
            if (fullTurnCounts == counts) return;
            fullTurnCounts = counts;
            CalculateTestRWC();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
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
            if (IsCountsMode)
            {
                CalculateTestRWC();
            }
        }

        // Another calibration panel (Gyro/Stick Flick Stick/Touchpad Flick Stick all
        // share the same profile-level RWC/In-Game Sens/Counts) changed a value.
        // Refresh this instance's own cached counts and bound properties to match.
        private void ActionProfile_CalibValuesChanged(object sender, EventArgs e)
        {
            fullTurnCounts = mapper.ActionProfile.CalibCounts > 0.0
                ? mapper.ActionProfile.CalibCounts : fullTurnCounts;
            if (!_applyingPreset) TryMatchPreset();
            CalculateTestRWC();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealWorldCalibration)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTurnCounts)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
        }

        private void StickFlickStickPropViewModel_MinAngleThresholdChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD);
            HighlightMinAngleThresholdChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_ReleaseDampeningSpeedChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED);
            HighlightReleaseDampeningSpeedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_MultiplierCompensationChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
            HighlightMultiplierCompensationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_AccelerationMultiplierChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
            HighlightAccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_RotateSmoothOverrideChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE);
            HighlightRotateSmoothOverrideChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_FlickTimeChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_TIME))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.FLICK_TIME);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.FLICK_TIME);
            HighlightFlickTimeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_FlickTimeExponentChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT);
            HighlightFlickTimeExponentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_FlickThresholdChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_THRESHOLD))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.FLICK_THRESHOLD);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.FLICK_THRESHOLD);
            HighlightFlickThresholdChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_SnapAngleChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SNAP_ANGLE))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.SNAP_ANGLE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.SNAP_ANGLE);
            HighlightSnapAngleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_SnapStrengthChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SNAP_STRENGTH))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.SNAP_STRENGTH);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.SNAP_STRENGTH);
            HighlightSnapStrengthChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_SubModeChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SUB_MODE))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.SUB_MODE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.SUB_MODE);
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
    }
}
