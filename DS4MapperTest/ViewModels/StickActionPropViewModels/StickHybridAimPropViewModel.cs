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
    public class StickHybridAimPropViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        private StickHybridAim action;
        public StickHybridAim Action
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

        public double MaxZone
        {
            get => action.DeadMod.MaxZone;
            set
            {
                action.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MaxZoneChanged;

        public int StickSens
        {
            get => action.StickSens;
            set
            {
                action.StickSens = value;
                StickSensChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler StickSensChanged;

        public double MouselikeFactor
        {
            get => action.MouselikeFactor;
            set
            {
                action.MouselikeFactor = value;
                MouselikeFactorChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MouselikeFactorChanged;

        public bool EdgePushEnabled
        {
            get => action.EdgePushEnabled;
            set
            {
                if (action.EdgePushEnabled == value) return;
                action.EdgePushEnabled = value;
                EdgePushEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler EdgePushEnabledChanged;

        public bool ReturnDeadzoneEnabled
        {
            get => action.ReturnDeadzoneEnabled;
            set
            {
                if (action.ReturnDeadzoneEnabled == value) return;
                action.ReturnDeadzoneEnabled = value;
                ReturnDeadzoneEnabledChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReturnDeadzoneEnabled)));
            }
        }
        public event EventHandler ReturnDeadzoneEnabledChanged;

        public double ReturnDeadzoneAngle
        {
            get => action.ReturnDeadzoneAngle;
            set
            {
                action.ReturnDeadzoneAngle = value;
                ReturnDeadzoneAngleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ReturnDeadzoneAngleChanged;

        public double ReturnDeadzoneCutoffAngle
        {
            get => action.ReturnDeadzoneCutoffAngle;
            set
            {
                action.ReturnDeadzoneCutoffAngle = value;
                ReturnDeadzoneCutoffAngleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ReturnDeadzoneCutoffAngleChanged;

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

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightMaxZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.MAX_ZONE);
        }
        public event EventHandler HighlightMaxZoneChanged;

        public bool HighlightStickSens
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.STICK_SENS);
        }
        public event EventHandler HighlightStickSensChanged;

        public bool HighlightMouselikeFactor
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.MOUSELIKE_FACTOR);
        }
        public event EventHandler HighlightMouselikeFactorChanged;

        public bool HighlightOutputCurveChoice
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.OUTPUT_CURVE);
        }
        public event EventHandler HighlightOutputCurveChoiceChanged;

        public event EventHandler ActionPropertyChanged;
        public event EventHandler<StickMapAction> ActionChanged;

        private bool usingRealAction = false;

        public StickHybridAimPropViewModel(Mapper mapper, StickMapAction action)
        {
            this.mapper = mapper;
            this.action = action as StickHybridAim;

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                StickHybridAim baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as StickHybridAim;
                StickHybridAim tempAction = new StickHybridAim();
                tempAction.SoftCopyFromParent(baseLayerAction);
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;

                this.action = tempAction;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            NameChanged += StickHybridAimPropViewModel_NameChanged;
            DeadZoneChanged += StickHybridAimPropViewModel_DeadZoneChanged;
            MaxZoneChanged += StickHybridAimPropViewModel_MaxZoneChanged;
            StickSensChanged += StickHybridAimPropViewModel_StickSensChanged;
            MouselikeFactorChanged += StickHybridAimPropViewModel_MouselikeFactorChanged;
            EdgePushEnabledChanged += StickHybridAimPropViewModel_EdgePushEnabledChanged;
            ReturnDeadzoneEnabledChanged += StickHybridAimPropViewModel_ReturnDeadzoneEnabledChanged;
            ReturnDeadzoneAngleChanged += StickHybridAimPropViewModel_ReturnDeadzoneAngleChanged;
            ReturnDeadzoneCutoffAngleChanged += StickHybridAimPropViewModel_ReturnDeadzoneCutoffAngleChanged;
            OutputCurveChoiceChanged += StickHybridAimPropViewModel_OutputCurveChoiceChanged;
        }

        private void StickHybridAimPropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickHybridAimPropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.DEAD_ZONE))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.DEAD_ZONE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickHybridAimPropViewModel_MaxZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.MAX_ZONE))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.MAX_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.MAX_ZONE);
            HighlightMaxZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickHybridAimPropViewModel_StickSensChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.STICK_SENS))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.STICK_SENS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.STICK_SENS);
            HighlightStickSensChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickHybridAimPropViewModel_MouselikeFactorChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.MOUSELIKE_FACTOR))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.MOUSELIKE_FACTOR);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.MOUSELIKE_FACTOR);
            HighlightMouselikeFactorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickHybridAimPropViewModel_EdgePushEnabledChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.EDGE_PUSH_ENABLED))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.EDGE_PUSH_ENABLED);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.EDGE_PUSH_ENABLED);
        }

        private void StickHybridAimPropViewModel_ReturnDeadzoneEnabledChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ENABLED))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ENABLED);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ENABLED);
        }

        private void StickHybridAimPropViewModel_ReturnDeadzoneAngleChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ANGLE))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ANGLE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ANGLE);
        }

        private void StickHybridAimPropViewModel_ReturnDeadzoneCutoffAngleChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_CUTOFF_ANGLE))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_CUTOFF_ANGLE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_CUTOFF_ANGLE);
        }

        private void StickHybridAimPropViewModel_OutputCurveChoiceChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickHybridAim.PropertyKeyStrings.OUTPUT_CURVE))
            {
                action.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.OUTPUT_CURVE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickHybridAim.PropertyKeyStrings.OUTPUT_CURVE);
            HighlightOutputCurveChoiceChanged?.Invoke(this, EventArgs.Empty);
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
