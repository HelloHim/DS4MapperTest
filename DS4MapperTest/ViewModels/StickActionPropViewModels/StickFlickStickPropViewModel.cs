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

        private Mapper mapper;
        public Mapper Mapper => mapper;

        private StickFlickStick action;
        public StickFlickStick Action => action;

        // The profile-level angle calibration that converts this action's degree-based
        // settings into mouse counts. Shared with the Gyro/Stick/Touchpad Mouse and
        // Touchpad Flick Stick panels, and surfaced here through the same CalibrationModeControl.
        private GyroCalibrationViewModel calibration;
        public GyroCalibrationViewModel Calibration =>
            calibration ??= new GyroCalibrationViewModel(mapper);

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

        public double SweepSensitivity
        {
            get => action.SweepSensitivity;
            set
            {
                if (action.SweepSensitivity == value) return;
                action.SweepSensitivity = value;
                SweepSensitivityChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SweepSensitivityChanged;

        public double FrontAngleDeadzone
        {
            get => action.FrontAngleDeadzone;
            set
            {
                if (action.FrontAngleDeadzone == value) return;
                action.FrontAngleDeadzone = value;
                FrontAngleDeadzoneChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler FrontAngleDeadzoneChanged;

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

        public bool HighlightSweepSensitivity
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SWEEP_SENSITIVITY);
        }
        public event EventHandler HighlightSweepSensitivityChanged;

        public bool HighlightFrontAngleDeadzone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FRONT_ANGLE_DEADZONE);
        }
        public event EventHandler HighlightFrontAngleDeadzoneChanged;

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
            SweepSensitivityChanged += StickFlickStickPropViewModel_SweepSensitivityChanged;
            FrontAngleDeadzoneChanged += StickFlickStickPropViewModel_FrontAngleDeadzoneChanged;
            SnapAngleChanged += StickFlickStickPropViewModel_SnapAngleChanged;
            SnapStrengthChanged += StickFlickStickPropViewModel_SnapStrengthChanged;
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

        private void StickFlickStickPropViewModel_SweepSensitivityChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SWEEP_SENSITIVITY))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.SWEEP_SENSITIVITY);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.SWEEP_SENSITIVITY);
            HighlightSweepSensitivityChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickFlickStickPropViewModel_FrontAngleDeadzoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FRONT_ANGLE_DEADZONE))
            {
                action.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.FRONT_ANGLE_DEADZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickFlickStick.PropertyKeyStrings.FRONT_ANGLE_DEADZONE);
            HighlightFrontAngleDeadzoneChanged?.Invoke(this, EventArgs.Empty);
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
