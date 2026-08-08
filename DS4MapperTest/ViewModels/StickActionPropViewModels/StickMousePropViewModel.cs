using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4MapperTest.ViewModels.Common;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.StickActions;
using System.Threading;

namespace DS4MapperTest.ViewModels.StickActionPropViewModels
{
    public class StickMousePropViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        private StickMouse action;
        public StickMouse Action
        {
            get => action;
        }

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

        public double DeadZone
        {
            get => action.DeadMod.DeadZone;
            set
            {
                action.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeadZoneChanged;

        public int MouseSpeed
        {
            get => action.MouseSpeed;
            set
            {
                action.MouseSpeed = value;
                MouseSpeedChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MouseSpeedChanged;

        public string MouseSpeedOutput
        {
            get => (action.MouseSpeed * 20).ToString();
        }
        public event EventHandler MouseSpeedOutputChanged;

        public double VerticalScale
        {
            get => action.VerticalScale;
            set
            {
                double verticalScale = Math.Clamp(value, 0.0, StickMouse.MaxVerticalScale);
                if (action.VerticalScale == verticalScale) return;
                action.VerticalScale = verticalScale;
                VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler VerticalScaleChanged;

        public int DiagonalRange
        {
            get => action.DiagonalRange;
            set
            {
                int diagonalRange = Math.Clamp(value, 0, 90);
                if (action.DiagonalRange == diagonalRange) return;
                action.DiagonalRange = diagonalRange;
                DiagonalRangeChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DiagonalRangeChanged;

        public double VerticalSensitivity
        {
            get => Math.Round(action.MouseSpeed * action.VerticalScale, 4);
            set
            {
                double mouseSpeedD = action.MouseSpeed;
                double verticalScale = Math.Abs(mouseSpeedD) < 1e-10
                    ? 0.0
                    : value / mouseSpeedD;
                verticalScale = Math.Clamp(verticalScale, 0.0, StickMouse.MaxVerticalScale);
                if (action.VerticalScale == verticalScale) return;
                action.VerticalScale = verticalScale;
                VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool verticalScaleIsAbsoluteMode = false;
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

        public bool HighlightVerticalScale
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.VERTICAL_SCALE);
        }
        public event EventHandler HighlightVerticalScaleChanged;

        public bool HighlightDiagonalRange
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DIAGONAL_RANGE);
        }
        public event EventHandler HighlightDiagonalRangeChanged;

        private List<EnumChoiceSelection<StickOutCurve.Curve>> outputCurveChoiceItems =
            new List<EnumChoiceSelection<StickOutCurve.Curve>>()
        {
            new EnumChoiceSelection<StickOutCurve.Curve>("Linear", StickOutCurve.Curve.Linear),
            new EnumChoiceSelection<StickOutCurve.Curve>("Enhanced Precision", StickOutCurve.Curve.EnhancedPrecision),
            new EnumChoiceSelection<StickOutCurve.Curve>("Quadratic", StickOutCurve.Curve.Quadratic),
            new EnumChoiceSelection<StickOutCurve.Curve>("Cubic", StickOutCurve.Curve.Cubic),
            new EnumChoiceSelection<StickOutCurve.Curve>("EaseOut Quadratic", StickOutCurve.Curve.EaseoutQuad),
            new EnumChoiceSelection<StickOutCurve.Curve>("EaseOut Cubic", StickOutCurve.Curve.EaseoutCubic),
        };
        public List<EnumChoiceSelection<StickOutCurve.Curve>> OutputCurveChoiceItems => outputCurveChoiceItems;

        public StickOutCurve.Curve OutputCurveChoice
        {
            get => action.OutputCurve;
            set
            {
                action.OutputCurve = value;
                OutputCurveChoiceChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OutputCurveChoiceChanged;

        public bool DeltaEnabled
        {
            get => action.MouseDeltaSettings.enabled;
            set
            {
                action.MouseDeltaSettings.enabled = value;
                DeltaEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeltaEnabledChanged;

        public double DeltaMultiplier
        {
            get => action.MouseDeltaSettings.multiplier;
            set
            {
                action.MouseDeltaSettings.multiplier = value;
                DeltaMultiplierChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeltaMultiplierChanged;

        public double DeltaMinTravel
        {
            get => action.MouseDeltaSettings.minTravel;
            set
            {
                action.MouseDeltaSettings.minTravel = value;
                DeltaMinTravelChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeltaMinTravelChanged;

        public double DeltaMaxTravel
        {
            get => action.MouseDeltaSettings.maxTravel;
            set
            {
                action.MouseDeltaSettings.maxTravel = value;
                DeltaMaxTravelChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeltaMaxTravelChanged;

        public double DeltaEasingDuration
        {
            get => action.MouseDeltaSettings.easingDuration;
            set
            {
                action.MouseDeltaSettings.easingDuration = value;
                DeltaEasingDurationChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeltaEasingDurationChanged;

        public double DeltaMinFactor
        {
            get => action.MouseDeltaSettings.minfactor;
            set
            {
                action.MouseDeltaSettings.minfactor = value;
                DeltaMinFactorChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DeltaMinFactorChanged;

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightMouseSpeed
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.MOUSE_SPEED);
        }
        public event EventHandler HighlightMouseSpeedChanged;

        public bool HighlightOutputCurveChoice
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.OUTPUT_CURVE);
        }
        public event EventHandler HighlightOutputCurveChoiceChanged;

        public bool HighlightDelta
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        }
        public event EventHandler HighlightDeltaChanged;

        //public bool HighlightDeltaEnabled
        //{
        //    get => action.ParentAction == null ||
        //        action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        //}
        //public event EventHandler HighlightDeltaEnabledChanged;

        //public bool HighlightHighlightDeltaMultiplier
        //{
        //    get => action.ParentAction == null ||
        //        action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        //}
        //public event EventHandler HighlightDeltaMultiplierChanged;

        //public bool HighlightDeltaMinTravel
        //{
        //    get => action.ParentAction == null ||
        //        action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        //}
        //public event EventHandler HighlightDeltaMinTravelChanged;

        //public bool HighlightDeltaMaxTravel
        //{
        //    get => action.ParentAction == null ||
        //        action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        //}
        //public event EventHandler HighlightDeltaMaxTravelChanged;

        //public bool HighlightDeltaEasingDuration
        //{
        //    get => action.ParentAction == null ||
        //        action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        //}
        //public event EventHandler HighlightDeltaEasingDurationChanged;

        //public bool HighlightDeltaMinFactor
        //{
        //    get => action.ParentAction == null ||
        //        action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        //}
        //public event EventHandler HighlightDeltaMinFactorChanged;


        public event EventHandler ActionPropertyChanged;
        public event EventHandler<StickMapAction> ActionChanged;

        private bool usingRealAction = false;

        public StickMousePropViewModel(Mapper mapper, StickMapAction action)
        {
            this.mapper = mapper;
            this.action = action as StickMouse;

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                StickMouse baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as StickMouse;
                StickMouse tempAction = new StickMouse();
                tempAction.SoftCopyFromParent(baseLayerAction);
                //int tempLayerId = mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.Index;
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;
                //tempAction.MappingId = this.action.MappingId;

                this.action = tempAction;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            PrepareModel();

            NameChanged += StickMousePropViewModel_NameChanged;
            DeadZoneChanged += StickMousePropViewModel_DeadZoneChanged;
            MouseSpeedChanged += StickMousePropViewModel_MouseSpeedChanged;
            MouseSpeedChanged += RenderUpdatedOutputMouseSpeed;
            MouseSpeedChanged += StickMousePropViewModel_MouseSpeedChangedForVerticalSensitivity;
            VerticalScaleChanged += StickMousePropViewModel_VerticalScaleChanged;
            DiagonalRangeChanged += StickMousePropViewModel_DiagonalRangeChanged;
            OutputCurveChoiceChanged += StickMousePropViewModel_OutputCurveChoiceChanged;
            DeltaEnabledChanged += StickMousePropViewModel_DeltaEnabledChanged;
            DeltaMultiplierChanged += StickMousePropViewModel_DeltaMultiplierChanged;
            DeltaMinTravelChanged += StickMousePropViewModel_DeltaMinTravelChanged;
            DeltaMaxTravelChanged += StickMousePropViewModel_DeltaMaxTravelChanged;
            DeltaEasingDurationChanged += StickMousePropViewModel_DeltaEasingDurationChanged;
            DeltaMinFactorChanged += StickMousePropViewModel_DeltaMinFactorChanged;
        }

        private void StickMousePropViewModel_MouseSpeedChangedForVerticalSensitivity(object sender, EventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalSensitivity)));
        }

        private void StickMousePropViewModel_VerticalScaleChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.VERTICAL_SCALE))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.VERTICAL_SCALE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.VERTICAL_SCALE);
            HighlightVerticalScaleChanged?.Invoke(this, EventArgs.Empty);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalScale)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerticalSensitivity)));
        }

        private void StickMousePropViewModel_DiagonalRangeChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DIAGONAL_RANGE))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DIAGONAL_RANGE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DIAGONAL_RANGE);
            HighlightDiagonalRangeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_DeltaMinFactorChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            HighlightDeltaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_DeltaEasingDurationChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            HighlightDeltaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_DeltaMaxTravelChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            HighlightDeltaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_DeltaMinTravelChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            HighlightDeltaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_DeltaMultiplierChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            HighlightDeltaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_DeltaEnabledChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DELTA_SETTINGS))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
            HighlightDeltaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RenderUpdatedOutputMouseSpeed(object sender, EventArgs e)
        {
            MouseSpeedOutputChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_MouseSpeedChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.MOUSE_SPEED))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.MOUSE_SPEED);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.MOUSE_SPEED);
            HighlightMouseSpeedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.DEAD_ZONE))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.DEAD_ZONE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_OutputCurveChoiceChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.OUTPUT_CURVE))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.OUTPUT_CURVE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.OUTPUT_CURVE);
            HighlightOutputCurveChoiceChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMousePropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouse.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(StickMouse.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouse.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
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

        private void PrepareModel()
        {

        }
    }
}
