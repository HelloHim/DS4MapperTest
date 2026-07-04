using System;
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
        public bool HasStartPress => HasFunc<StartPressFunc>();
        public bool HasReleasePress => HasFunc<ReleaseFunc>();
        public bool CanAddHoldPress => !HasHoldPress;
        public bool CanAddDoublePress => !HasDoublePress;
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
            int index = buttonAction.ActionFuncs.IndexOf(item.Func);
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
            ActionFunc func = item.Func;

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
                item = functionItems.FirstOrDefault(temp => temp.Kind == item.Kind);
            }

            return new EditFaceBindingContext(owner.DeviceMapper, buttonAction, item?.Func ?? func);
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
                FaceBindingFuncKind.Double => CreateOutputFunc(new DoublePressFunc(), emptyOutput),
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
            OnPropertyChanged(nameof(HasStartPress));
            OnPropertyChanged(nameof(HasReleasePress));
            OnPropertyChanged(nameof(CanAddHoldPress));
            OnPropertyChanged(nameof(CanAddDoublePress));
            OnPropertyChanged(nameof(CanAddStartPress));
            OnPropertyChanged(nameof(CanAddReleasePress));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FaceButtonFuncItem : INotifyPropertyChanged
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
        public bool SupportsReleaseOptions => func is ReleaseFunc;

        public string DisplayName => Kind switch
        {
            FaceBindingFuncKind.Regular => "Regular Press",
            FaceBindingFuncKind.Hold => "Hold Press",
            FaceBindingFuncKind.Double => "Double Press",
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

        public bool ToggleEnabled
        {
            get => func?.toggleEnabled ?? false;
            set
            {
                if (func == null || func.toggleEnabled == value) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    func.toggleEnabled = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(ToggleEnabled));
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
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    switch (func)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboEnabled = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboEnabled = value;
                            break;
                    }
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(TurboEnabled));
                OnPropertyChanged(nameof(IsTurboEnabled));
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
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    switch (func)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboDurationMs = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboDurationMs = value;
                            break;
                    }
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(TurboDurationMs));
            }
        }

        public int FireDelayMs
        {
            get => func is NormalPressFunc normalPress ? normalPress.FireDelayMs : 0;
            set
            {
                if (func is not NormalPressFunc normalPress) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    normalPress.FireDelayMs = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(FireDelayMs));
            }
        }

        public int HoldMs
        {
            get => func is HoldPressFunc holdPress ? holdPress.DurationMs : 0;
            set
            {
                if (func is not HoldPressFunc holdPress) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    holdPress.DurationMs = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(HoldMs));
            }
        }

        public string ReleaseDurationMs
        {
            get => func is ReleaseFunc releaseFunc ? releaseFunc.DurationMs.ToString() : "0";
            set
            {
                if (func is not ReleaseFunc releaseFunc || !int.TryParse(value, out int temp)) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    releaseFunc.DurationMs = temp;
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(ReleaseDurationMs));
            }
        }

        public string ReleaseDelayMs
        {
            get => func is ReleaseFunc releaseFunc ? releaseFunc.DelayDurationMs.ToString() : "0";
            set
            {
                if (func is not ReleaseFunc releaseFunc || !int.TryParse(value, out int temp)) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    releaseFunc.DelayDurationMs = temp;
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(ReleaseDelayMs));
            }
        }

        public bool ReleaseInterruptable
        {
            get => func is ReleaseFunc releaseFunc && releaseFunc.interruptable;
            set
            {
                if (func is not ReleaseFunc releaseFunc || releaseFunc.interruptable == value) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    releaseFunc.interruptable = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(owner.MappedAction as ButtonAction);
                });
                OnPropertyChanged(nameof(ReleaseInterruptable));
            }
        }

        public FaceButtonFuncItem(FaceButtonBindingItem owner, FaceBindingFuncKind kind,
            ActionFunc func)
        {
            this.owner = owner;
            Kind = kind;
            this.func = func;
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(DisplayBind));
            OnPropertyChanged(nameof(ToggleEnabled));
            OnPropertyChanged(nameof(TurboEnabled));
            OnPropertyChanged(nameof(TurboDurationMs));
            OnPropertyChanged(nameof(FireDelayMs));
            OnPropertyChanged(nameof(HoldMs));
            OnPropertyChanged(nameof(ReleaseDurationMs));
            OnPropertyChanged(nameof(ReleaseDelayMs));
            OnPropertyChanged(nameof(ReleaseInterruptable));
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
