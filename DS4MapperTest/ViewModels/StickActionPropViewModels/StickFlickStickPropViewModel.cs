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
            }
        }
        public event EventHandler SubModeChanged;

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
            mapper.ActionProfile.CalibModeChanged += ActionProfile_CalibModeChanged;

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
