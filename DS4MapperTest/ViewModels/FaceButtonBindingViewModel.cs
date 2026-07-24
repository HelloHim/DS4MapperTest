using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ViewModels
{
    public enum FaceBindingFuncKind
    {
        Regular,
        Hold,
        Double,
        Distance,
        Chorded,
        Start,
        Release,
    }

    public class FaceButtonBindingItem : INotifyPropertyChanged
    {
        private readonly ProfileEditorTestViewModel owner;
        private ButtonMapAction mappedAction;
        private readonly ObservableCollection<FaceButtonFuncItem> functionItems =
            new ObservableCollection<FaceButtonFuncItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public ProfileEditorTestViewModel Owner => owner;
        public string BindingName { get; }
        public string DisplayName { get; }
        public string Subtitle { get; }
        public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
        public ObservableCollection<FaceButtonFuncItem> FunctionItems => functionItems;

        public ButtonMapAction MappedAction
        {
            get => mappedAction;
            private set
            {
                if (mappedAction == value) return;
                mappedAction = value;
                OnPropertyChanged(nameof(MappedAction));
            }
        }

        public bool HasHoldPress => HasFunc<HoldPressFunc>();
        public bool HasDoublePress => HasFunc<DoublePressFunc>();
        public bool HasDistancePress => HasFunc<DistanceFunc>();
        public bool HasChordedPress => HasFunc<ChordedPressFunc>();
        public bool HasStartPress => HasFunc<StartPressFunc>();
        public bool HasReleasePress => HasFunc<ReleaseFunc>();
        public bool CanAddHoldPress => !HasHoldPress;
        public bool CanAddDoublePress => !HasDoublePress;
        public bool CanAddDistancePress => !HasDistancePress;
        public bool CanAddChordedPress => !HasChordedPress;
        public bool CanAddStartPress => !HasStartPress;
        public bool CanAddReleasePress => !HasReleasePress;

        public FaceButtonBindingItem(ProfileEditorTestViewModel owner,
            BindingItemsTest sourceItem, string displayName, string subtitle = null)
        {
            this.owner = owner;
            BindingName = sourceItem.BindingName;
            DisplayName = displayName;
            Subtitle = subtitle;
            mappedAction = sourceItem.MappedAction;

            RefreshFunctions();
        }

        public void UpdateAction(ButtonMapAction action)
        {
            MappedAction = action;
            RefreshFunctions();
        }

        public void RefreshFunctions()
        {
            functionItems.Clear();

            ButtonAction buttonAction = mappedAction as ButtonAction;
            ActionFunc regularFunc = buttonAction?.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault();
            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Regular, regularFunc));

            if (buttonAction != null)
            {
                foreach (ActionFunc func in buttonAction.ActionFuncs)
                {
                    switch (func)
                    {
                        case HoldPressFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Hold, func));
                            break;
                        case DoublePressFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Double, func));
                            break;
                        case DistanceFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Distance, func));
                            break;
                        case ChordedPressFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Chorded, func));
                            break;
                        case StartPressFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Start, func));
                            break;
                        case ReleaseFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Release, func));
                            break;
                    }
                }
            }

            RaiseAvailabilityChanged();
        }

        public FaceButtonFuncItem AddExtraBinding(FaceBindingFuncKind kind)
        {
            if (kind == FaceBindingFuncKind.Regular || HasKind(kind)) return null;

            ButtonAction buttonAction = owner.EnsureEditableFaceButtonAction(this);
            ActionFunc func = CreateFunc(kind);
            if (func == null) return null;

            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.Add(func);
                MarkFunctionsChanged(buttonAction);
            });

            RefreshFunctions();
            return functionItems.FirstOrDefault(item => item.Kind == kind);
        }

        public void RemoveBinding(FaceButtonFuncItem item)
        {
            if (item == null || item.Kind == FaceBindingFuncKind.Regular || item.Func == null) return;

            ButtonAction buttonAction = owner.EnsureEditableFaceButtonAction(this);
            ActionFunc func = item.ResolveCurrentFunc(buttonAction);
            int index = buttonAction.ActionFuncs.IndexOf(func);
            if (index < 0) return;

            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.RemoveAt(index);
                MarkFunctionsChanged(buttonAction);
            });

            RefreshFunctions();
        }

        public EditFaceBindingContext PrepareEdit(FaceButtonFuncItem item)
        {
            if (item == null) return null;

            ButtonAction buttonAction = owner.EnsureEditableFaceButtonAction(this);
            ActionFunc func = item.ResolveCurrentFunc(buttonAction);

            if (func == null)
            {
                func = CreateFunc(item.Kind);
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    buttonAction.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                    buttonAction.ActionFuncs.Add(func);
                    MarkFunctionsChanged(buttonAction);
                });

                RefreshFunctions();
            }

            return new EditFaceBindingContext(owner.DeviceMapper, buttonAction, func);
        }

        public void RefreshAfterEdit()
        {
            RefreshFunctions();
        }

        private bool HasKind(FaceBindingFuncKind kind)
        {
            return kind switch
            {
                FaceBindingFuncKind.Hold => HasHoldPress,
                FaceBindingFuncKind.Double => HasDoublePress,
                FaceBindingFuncKind.Distance => HasDistancePress,
                FaceBindingFuncKind.Chorded => HasChordedPress,
                FaceBindingFuncKind.Start => HasStartPress,
                FaceBindingFuncKind.Release => HasReleasePress,
                _ => false,
            };
        }

        private bool HasFunc<TFunc>() where TFunc : ActionFunc
        {
            return mappedAction is ButtonAction buttonAction &&
                buttonAction.ActionFuncs.OfType<TFunc>().Any();
        }

        private static ActionFunc CreateFunc(FaceBindingFuncKind kind)
        {
            OutputActionData emptyOutput =
                new OutputActionData(OutputActionData.ActionType.Empty, 0);

            return kind switch
            {
                FaceBindingFuncKind.Regular => new NormalPressFunc(emptyOutput),
                FaceBindingFuncKind.Hold => CreateOutputFunc(new HoldPressFunc(), emptyOutput),
                FaceBindingFuncKind.Double => CreateOutputFunc(new DoublePressFunc()
                {
                    DurationMs = DoublePressFunc.DEFAULT_TAP_WINDOW_MS,
                }, emptyOutput),
                FaceBindingFuncKind.Distance => CreateOutputFunc(new DistanceFunc(), emptyOutput),
                FaceBindingFuncKind.Chorded => CreateOutputFunc(new ChordedPressFunc(), emptyOutput),
                FaceBindingFuncKind.Start => CreateOutputFunc(new StartPressFunc(), emptyOutput),
                FaceBindingFuncKind.Release => CreateOutputFunc(new ReleaseFunc(), emptyOutput),
                _ => null,
            };
        }

        private static ActionFunc CreateOutputFunc(ActionFunc func, OutputActionData output)
        {
            func.OutputActions.Add(output);
            return func;
        }

        internal static void MarkFunctionsChanged(ButtonAction action)
        {
            if (action == null) return;
            if (!action.ChangedProperties.Contains(ButtonAction.PropertyKeyStrings.FUNCTIONS))
            {
                action.ChangedProperties.Add(ButtonAction.PropertyKeyStrings.FUNCTIONS);
            }
        }

        private void RaiseAvailabilityChanged()
        {
            OnPropertyChanged(nameof(HasHoldPress));
            OnPropertyChanged(nameof(HasDoublePress));
            OnPropertyChanged(nameof(HasDistancePress));
            OnPropertyChanged(nameof(HasChordedPress));
            OnPropertyChanged(nameof(HasStartPress));
            OnPropertyChanged(nameof(HasReleasePress));
            OnPropertyChanged(nameof(CanAddHoldPress));
            OnPropertyChanged(nameof(CanAddDoublePress));
            OnPropertyChanged(nameof(CanAddDistancePress));
            OnPropertyChanged(nameof(CanAddChordedPress));
            OnPropertyChanged(nameof(CanAddStartPress));
            OnPropertyChanged(nameof(CanAddReleasePress));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FaceButtonFuncItem : INotifyPropertyChanged, IQuickBindTarget
    {
        private readonly FaceButtonBindingItem owner;
        private readonly ActionFunc func;

        public event PropertyChangedEventHandler PropertyChanged;

        public FaceButtonBindingItem Owner => owner;
        public FaceBindingFuncKind Kind { get; }
        public ActionFunc Func => func;
        public bool IsExtraBinding => Kind != FaceBindingFuncKind.Regular && func != null;
        public bool CanRemove => IsExtraBinding;
        public bool IsTurboEnabled => SupportsTurbo && TurboEnabled;
        public bool SupportsToggle => func is NormalPressFunc || func is HoldPressFunc || func is DoublePressFunc || func is StartPressFunc || func is ReleaseFunc;
        public bool SupportsTurbo => func is NormalPressFunc || func is HoldPressFunc;
        public bool SupportsFireDelay => func is NormalPressFunc;
        public bool SupportsHoldTime => func is HoldPressFunc;
        public bool SupportsTapWindow => func is DoublePressFunc;
        public bool SupportsReleaseOptions => func is ReleaseFunc;
        public bool SupportsDistanceOptions => func is DistanceFunc;
        public bool SupportsChordOptions => func is ChordedPressFunc;

        public string DisplayName => Kind switch
        {
            FaceBindingFuncKind.Regular => "Regular Press",
            FaceBindingFuncKind.Hold => "Hold Press",
            FaceBindingFuncKind.Double => "Double Press",
            FaceBindingFuncKind.Distance => "Distance",
            FaceBindingFuncKind.Chorded => "Chorded Press",
            FaceBindingFuncKind.Start => "Start Press",
            FaceBindingFuncKind.Release => "Release Press",
            _ => "Binding",
        };

        public string DisplayBind
        {
            get
            {
                string result = func?.DescribeOutputActions(owner.Owner.DeviceMapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        // Locates the func matching this item's kind inside `buttonAction` (which may just
        // have been cloned into the current layer by EnsureEditableFaceButtonAction). The
        // `func` field captured at construction may be a stale, still-shared-with-default-layer
        // instance once a clone happens, so callers must never mutate `func` directly.
        internal ActionFunc ResolveCurrentFunc(ButtonAction buttonAction)
        {
            if (buttonAction == null) return func;

            ActionFunc resolved = Kind switch
            {
                FaceBindingFuncKind.Regular => buttonAction.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Hold => buttonAction.ActionFuncs.OfType<HoldPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Double => buttonAction.ActionFuncs.OfType<DoublePressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Distance => buttonAction.ActionFuncs.OfType<DistanceFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Chorded => buttonAction.ActionFuncs.OfType<ChordedPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Start => buttonAction.ActionFuncs.OfType<StartPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Release => buttonAction.ActionFuncs.OfType<ReleaseFunc>().FirstOrDefault(),
                _ => null,
            };

            return resolved ?? func;
        }

        // Ensures the owning button binding is editable in the current layer (cloning it in on
        // first edit) and resolves this item's func within that (possibly new) action, so edits
        // never land on a shared default-layer object.
        private (ButtonAction buttonAction, ActionFunc target) BeginEdit()
        {
            ButtonAction buttonAction = owner.Owner.EnsureEditableFaceButtonAction(owner);
            ActionFunc target = ResolveCurrentFunc(buttonAction);
            return (buttonAction, target);
        }

        public bool ToggleEnabled
        {
            get => func?.toggleEnabled ?? false;
            set
            {
                if (func == null || func.toggleEnabled == value) return;
                var (buttonAction, target) = BeginEdit();
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    target.toggleEnabled = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public bool TurboEnabled
        {
            get
            {
                return func switch
                {
                    NormalPressFunc normalPress => normalPress.TurboEnabled,
                    HoldPressFunc holdPress => holdPress.TurboEnabled,
                    _ => false,
                };
            }
            set
            {
                if (!SupportsTurbo || TurboEnabled == value) return;
                var (buttonAction, target) = BeginEdit();
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    switch (target)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboEnabled = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboEnabled = value;
                            break;
                    }
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public int TurboDurationMs
        {
            get
            {
                return func switch
                {
                    NormalPressFunc normalPress => normalPress.TurboDurationMs,
                    HoldPressFunc holdPress => holdPress.TurboDurationMs,
                    _ => 0,
                };
            }
            set
            {
                if (!SupportsTurbo) return;
                var (buttonAction, target) = BeginEdit();
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    switch (target)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboDurationMs = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboDurationMs = value;
                            break;
                    }
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public int FireDelayMs
        {
            get => func is NormalPressFunc normalPress ? normalPress.FireDelayMs : 0;
            set
            {
                if (func is not NormalPressFunc) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not NormalPressFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.FireDelayMs = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public int HoldMs
        {
            get => func is HoldPressFunc holdPress ? holdPress.DurationMs : 0;
            set
            {
                if (func is not HoldPressFunc) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not HoldPressFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.DurationMs = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public int TapWindowMs
        {
            get => func is DoublePressFunc doublePress ? doublePress.DurationMs : 0;
            set
            {
                if (func is not DoublePressFunc) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not DoublePressFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.DurationMs = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public string ReleaseDelayMs
        {
            get => func is ReleaseFunc releaseFunc ? releaseFunc.DelayDurationMs.ToString() : "0";
            set
            {
                if (func is not ReleaseFunc || !int.TryParse(value, out int temp)) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not ReleaseFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.DelayDurationMs = temp;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public bool MaxHoldTimeEnabled
        {
            get => func is ReleaseFunc releaseFunc && releaseFunc.MaxHoldTimeEnabled;
            set
            {
                if (func is not ReleaseFunc releaseFuncGuard || releaseFuncGuard.MaxHoldTimeEnabled == value) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not ReleaseFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.MaxHoldTimeEnabled = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public string MaxHoldTimeMs
        {
            get => func is ReleaseFunc releaseFunc ? releaseFunc.MaxHoldTimeMs.ToString() : "0";
            set
            {
                if (func is not ReleaseFunc || !int.TryParse(value, out int temp)) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not ReleaseFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.MaxHoldTimeMs = temp;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public string DistanceName
        {
            get => func is DistanceFunc distanceFunc ? distanceFunc.Name : "";
            set
            {
                if (func is not DistanceFunc) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not DistanceFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.Name = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public double DistanceValue
        {
            get => func is DistanceFunc distanceFunc ? distanceFunc.distance : 0.0;
            set
            {
                if (func is not DistanceFunc) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not DistanceFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.distance = Math.Clamp(value, 0.0, 1.0);
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public List<ActionTriggerItem> ChordTriggerItems =>
            ChordedPressFuncUi.BuildTriggerItems(owner.Owner.DeviceMapper);

        public JoypadActionCodes ChordTrigger
        {
            get => func is ChordedPressFunc chordedPress ? chordedPress.TriggerButton : JoypadActionCodes.Empty;
            set
            {
                if (func is not ChordedPressFunc chordedPressGuard || chordedPressGuard.TriggerButton == value) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not ChordedPressFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.TriggerButton = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();
            }
        }

        public FaceButtonFuncItem(FaceButtonBindingItem owner, FaceBindingFuncKind kind,
            ActionFunc func)
        {
            this.owner = owner;
            Kind = kind;
            this.func = func;
        }

        // IQuickBindTarget
        Mapper IQuickBindTarget.Mapper => owner.Owner.DeviceMapper;
        string IQuickBindTarget.RowLabel => owner.DisplayName;
        string IQuickBindTarget.SlotLabel => DisplayName;
        bool IQuickBindTarget.IsComplexBinding => !QuickBindActionApplier.IsSimpleFunc(func);
        EditFaceBindingContext IQuickBindTarget.GetEditContext() => owner.PrepareEdit(this);
        void IQuickBindTarget.NotifyBindingChanged() => owner.RefreshAfterEdit();

        public void Refresh()
        {
            OnPropertyChanged(nameof(DisplayBind));
            OnPropertyChanged(nameof(ToggleEnabled));
            OnPropertyChanged(nameof(TurboEnabled));
            OnPropertyChanged(nameof(TurboDurationMs));
            OnPropertyChanged(nameof(FireDelayMs));
            OnPropertyChanged(nameof(HoldMs));
            OnPropertyChanged(nameof(TapWindowMs));
            OnPropertyChanged(nameof(ReleaseDelayMs));
            OnPropertyChanged(nameof(MaxHoldTimeEnabled));
            OnPropertyChanged(nameof(MaxHoldTimeMs));
            OnPropertyChanged(nameof(DistanceName));
            OnPropertyChanged(nameof(DistanceValue));
            OnPropertyChanged(nameof(ChordTrigger));
            OnPropertyChanged(nameof(IsTurboEnabled));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class EditFaceBindingContext
    {
        public Mapper Mapper { get; }
        public ButtonAction Action { get; }
        public ActionFunc Func { get; }

        public EditFaceBindingContext(Mapper mapper, ButtonAction action, ActionFunc func)
        {
            Mapper = mapper;
            Action = action;
            Func = func;
        }
    }
}
