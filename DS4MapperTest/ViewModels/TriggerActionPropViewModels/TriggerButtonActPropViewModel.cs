using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.TriggerActions;
using DS4MapperTest.ViewModels.Common;
using System.Windows;

namespace DS4MapperTest.ViewModels.TriggerActionPropViewModels
{
    public class TriggerButtonActPropViewModel
    {
        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        private TriggerButtonAction action;
        public TriggerButtonAction Action
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

        public string DeadZone
        {
            get => $"{action.DeadZone.DeadZone:N2}";
            set
            {
                if (double.TryParse(value, out double temp))
                {
                    action.DeadZone.DeadZone = Math.Clamp(temp, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                    ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler DeadZoneChanged;

        private List<EnumChoiceSelection<TriggerStyle>> triggerStyleItems =
            new List<EnumChoiceSelection<TriggerStyle>>()
            {
                new EnumChoiceSelection<TriggerStyle>("Simple Threshold", TriggerStyle.SimpleThreshold),
                new EnumChoiceSelection<TriggerStyle>("Full Pull Only", TriggerStyle.FullPullOnly),
                new EnumChoiceSelection<TriggerStyle>("Hip Fire", TriggerStyle.HipFire),
                new EnumChoiceSelection<TriggerStyle>("Hip Fire Exclusive", TriggerStyle.HipFireExclusive),
            };
        public List<EnumChoiceSelection<TriggerStyle>> TriggerStyleItems => triggerStyleItems;

        public TriggerStyle TriggerStyleChoice
        {
            get => action.TriggerStyle;
            set
            {
                if (action.TriggerStyle == value) return;
                action.TriggerStyle = value;
                TriggerStyleChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler TriggerStyleChanged;

        private List<EnumChoiceSelection<HipFirePreset>> hipFirePresetItems =
            new List<EnumChoiceSelection<HipFirePreset>>()
            {
                new EnumChoiceSelection<HipFirePreset>("Fast", HipFirePreset.Fast),
                new EnumChoiceSelection<HipFirePreset>("Balanced", HipFirePreset.Balanced),
                new EnumChoiceSelection<HipFirePreset>("Relaxed", HipFirePreset.Relaxed),
                new EnumChoiceSelection<HipFirePreset>("Custom", HipFirePreset.Custom),
            };
        public List<EnumChoiceSelection<HipFirePreset>> HipFirePresetItems => hipFirePresetItems;

        public HipFirePreset HipFirePresetChoice
        {
            get => action.HipFirePreset;
            set
            {
                if (action.HipFirePreset == value) return;
                action.HipFirePreset = value;
                switch (value)
                {
                    case HipFirePreset.Fast:
                        action.HipFireWindowMs = 75;
                        break;
                    case HipFirePreset.Balanced:
                        action.HipFireWindowMs = 150;
                        break;
                    case HipFirePreset.Relaxed:
                        action.HipFireWindowMs = 250;
                        break;
                }

                HipFirePresetChanged?.Invoke(this, EventArgs.Empty);
                HipFireWindowMsChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler HipFirePresetChanged;

        public string HipFireWindowMs
        {
            get => action.HipFireWindowMs.ToString();
            set
            {
                if (int.TryParse(value, out int temp))
                {
                    int clamped = Math.Clamp(temp, 0, 1000);
                    if (action.HipFireWindowMs == clamped) return;
                    action.HipFireWindowMs = clamped;
                    if (clamped != 75 && clamped != 150 && clamped != 250)
                    {
                        action.HipFirePreset = HipFirePreset.Custom;
                        HipFirePresetChanged?.Invoke(this, EventArgs.Empty);
                    }

                    HipFireWindowMsChanged?.Invoke(this, EventArgs.Empty);
                    ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler HipFireWindowMsChanged;

        public bool ShowHipFireTiming =>
            action.TriggerStyle == TriggerStyle.HipFire ||
            action.TriggerStyle == TriggerStyle.HipFireExclusive;
        public event EventHandler ShowHipFireTimingChanged;

        public bool SoftPullEnabled =>
            action.TriggerStyle != TriggerStyle.FullPullOnly;
        public event EventHandler SoftPullEnabledChanged;

        public string TriggerStyleDescription
        {
            get
            {
                return action.TriggerStyle switch
                {
                    TriggerStyle.FullPullOnly =>
                        "Ignores Soft Pull and activates only Full Pull.",
                    TriggerStyle.HipFire =>
                        "Quick full pulls skip Soft Pull. Slower pulls activate Soft Pull, then Full Pull.",
                    TriggerStyle.HipFireExclusive =>
                        "Each pull activates either Soft Pull or Full Pull, never both. Release the trigger before choosing again.",
                    _ =>
                        "Soft Pull activates first. Full Pull activates after its threshold. Both can remain active.",
                };
            }
        }
        public event EventHandler TriggerStyleDescriptionChanged;

        public string ActionBindName
        {
            get => action.EventButton.DescribeActions(mapper);
        }

        public string SoftPullBindName
        {
            get => DescribeFilteredActions(false);
        }

        public string FullPullBindName
        {
            get => DescribeFilteredActions(true);
        }

        public bool HighlightName
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.NAME);
        }
        public event EventHandler HighlightNameChanged;

        public bool HighlightDeadZone
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.DEAD_ZONE);
        }
        public event EventHandler HighlightDeadZoneChanged;

        public bool HighlightTriggerStyle
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.TRIGGER_STYLE);
        }
        public event EventHandler HighlightTriggerStyleChanged;

        public bool HighlightHipFirePreset
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_PRESET);
        }
        public event EventHandler HighlightHipFirePresetChanged;

        public bool HighlightHipFireWindowMs
        {
            get => action.ParentAction == null ||
                action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_WINDOW_MS);
        }
        public event EventHandler HighlightHipFireWindowMsChanged;

        public event EventHandler ActionPropertyChanged;
        public event EventHandler<TriggerMapAction> ActionChanged;

        private bool usingRealAction = true;

        public TriggerButtonActPropViewModel(Mapper mapper, TriggerMapAction action)
        {
            this.mapper = mapper;
            this.action = action as TriggerButtonAction;

            // Check if base ActionLayer action from composite layer
            if (action.ParentAction == null &&
                mapper.EditActionSet.UsingCompositeLayer &&
                !mapper.EditLayer.LayerActions.Contains(action) &&
                MapAction.IsSameType(mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId], action))
            {
                // Test with temporary object
                TriggerButtonAction baseLayerAction = mapper.EditActionSet.DefaultActionLayer.normalActionDict[action.MappingId] as TriggerButtonAction;
                TriggerButtonAction tempAction = new TriggerButtonAction();
                tempAction.SoftCopyFromParent(baseLayerAction);
                //int tempLayerId = mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.Index;
                int tempId = mapper.EditLayer.FindNextAvailableId();
                tempAction.Id = tempId;
                //tempAction.MappingId = this.action.MappingId;

                this.action = tempAction;
                usingRealAction = false;

                ActionPropertyChanged += ReplaceExistingLayerAction;
            }

            PrepareModel();

            NameChanged += TriggerButtonActPropViewModel_NameChanged;
            DeadZoneChanged += TriggerButtonActPropViewModel_DeadZoneChanged;
            TriggerStyleChanged += TriggerButtonActPropViewModel_TriggerStyleChanged;
            HipFirePresetChanged += TriggerButtonActPropViewModel_HipFirePresetChanged;
            HipFireWindowMsChanged += TriggerButtonActPropViewModel_HipFireWindowMsChanged;
        }

        private void TriggerButtonActPropViewModel_HipFireWindowMsChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_WINDOW_MS))
            {
                action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_WINDOW_MS);
            }

            action.RaiseNotifyPropertyChange(mapper, TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_WINDOW_MS);
            HighlightHipFireWindowMsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TriggerButtonActPropViewModel_HipFirePresetChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_PRESET))
            {
                action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_PRESET);
            }

            action.RaiseNotifyPropertyChange(mapper, TriggerButtonAction.PropertyKeyStrings.HIP_FIRE_PRESET);
            HighlightHipFirePresetChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TriggerButtonActPropViewModel_TriggerStyleChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.TRIGGER_STYLE))
            {
                action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.TRIGGER_STYLE);
            }

            mapper.ProcessMappingChangeAction(() => action.Release(mapper, ignoreReleaseActions: true));
            action.RaiseNotifyPropertyChange(mapper, TriggerButtonAction.PropertyKeyStrings.TRIGGER_STYLE);
            HighlightTriggerStyleChanged?.Invoke(this, EventArgs.Empty);
            ShowHipFireTimingChanged?.Invoke(this, EventArgs.Empty);
            SoftPullEnabledChanged?.Invoke(this, EventArgs.Empty);
            TriggerStyleDescriptionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TriggerButtonActPropViewModel_DeadZoneChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.DEAD_ZONE))
            {
                action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.DEAD_ZONE);
            }

            action.RaiseNotifyPropertyChange(mapper, TriggerButtonAction.PropertyKeyStrings.DEAD_ZONE);
            HighlightDeadZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TriggerButtonActPropViewModel_NameChanged(object sender, EventArgs e)
        {
            if (!action.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.NAME))
            {
                action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.NAME);
            }

            action.RaiseNotifyPropertyChange(mapper, TriggerButtonAction.PropertyKeyStrings.NAME);
            HighlightNameChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ReplaceExistingLayerAction(object sender, EventArgs e)
        {
            if (!usingRealAction)
            {
                mapper.ProcessMappingChangeAction(() =>
                {
                    this.action.ParentAction?.Release(mapper, ignoreReleaseActions: true);

                    mapper.EditLayer.AddTriggerAction(this.action);
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

        public void UpdateEventButton(ButtonAction oldAction, ButtonAction newAction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            //ExecuteInMapperThread(() =>
            mapper.ProcessMappingChangeAction(() =>
            {
                if (oldAction != null)
                {
                    oldAction.Release(mapper, ignoreReleaseActions: true);
                    action.EventButton = newAction as AxisDirButton;
                }

                action.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
                action.UseParentEventButton = false;
                action.RaiseNotifyPropertyChange(mapper, TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
            });
        }

        private string DescribeFilteredActions(bool distance)
        {
            List<string> tempList = new List<string>();
            foreach (ActionUtil.ActionFunc func in action.EventButton.ActionFuncs)
            {
                if (func.onDistance == distance)
                {
                    tempList.Add(func.Describe(mapper));
                }
            }

            return tempList.Count > 0 ? string.Join("|", tempList) : "Unbound";
        }
    }
}
