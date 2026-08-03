using System;
using System.ComponentModel;
using DS4MapperTest.StickActions;

namespace DS4MapperTest.ViewModels.StickActionPropViewModels
{
    public class StickMouseRingPropViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        private StickMouseRing action;
        public StickMouseRing Action
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

        public double RingRadius
        {
            get => action.RingRadius;
            set
            {
                action.RingRadius = value;
                RingRadiusChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RingRadiusChanged;

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightMaxZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.MAX_ZONE);
        }
        public event EventHandler HighlightMaxZoneChanged;

        public bool HighlightRingRadius
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.RING_RADIUS);
        }
        public event EventHandler HighlightRingRadiusChanged;

        public event EventHandler ActionPropertyChanged;
        public event EventHandler<StickMapAction> ActionChanged;

        private bool usingRealAction = false;

        public StickMouseRingPropViewModel(Mapper mapper, StickMapAction action)
        {
            this.mapper = mapper;
            this.action = action as StickMouseRing;

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                StickMouseRing baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as StickMouseRing;
                StickMouseRing tempAction = new StickMouseRing();
                tempAction.SoftCopyFromParent(baseLayerAction);
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;

                this.action = tempAction;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            NameChanged += StickMouseRingPropViewModel_NameChanged;
            DeadZoneChanged += StickMouseRingPropViewModel_DeadZoneChanged;
            MaxZoneChanged += StickMouseRingPropViewModel_MaxZoneChanged;
            RingRadiusChanged += StickMouseRingPropViewModel_RingRadiusChanged;
        }

        private void StickMouseRingPropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouseRing.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMouseRingPropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.DEAD_ZONE))
            {
                action.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouseRing.PropertyKeyStrings.DEAD_ZONE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMouseRingPropViewModel_MaxZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.MAX_ZONE))
            {
                action.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.MAX_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouseRing.PropertyKeyStrings.MAX_ZONE);
            HighlightMaxZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StickMouseRingPropViewModel_RingRadiusChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(StickMouseRing.PropertyKeyStrings.RING_RADIUS))
            {
                action.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.RING_RADIUS);
            }

            action.RaiseNotifyPropertyChange(mapper, StickMouseRing.PropertyKeyStrings.RING_RADIUS);
            HighlightRingRadiusChanged?.Invoke(this, EventArgs.Empty);
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
