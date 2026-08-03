using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.TriggerActions;

namespace DS4MapperTest.ViewModels
{
    public enum FaceBindingFuncKind
    {
        Regular,
        Hold,
        Double,
        Distance,
        Chorded,
        SimPress,
        Start,
        Release,
        // Permanent activators for a pressure-capable touchpad click binding
        // (Steam Controller 2). Replace Regular for that binding only - every
        // other binding category never constructs these kinds.
        SoftPress,
        FullPress,
    }

    public class FaceButtonBindingItem : INotifyPropertyChanged
    {
        private readonly ProfileEditorTestViewModel owner;
        private ButtonMapAction mappedAction;
        private readonly bool isTouchpadPressureCapable;
        private readonly ObservableCollection<FaceButtonFuncItem> functionItems =
            new ObservableCollection<FaceButtonFuncItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public ProfileEditorTestViewModel Owner => owner;
        public string BindingName { get; }
        public string DisplayName { get; }

        // This binding's own JoypadActionCodes identity, used to mirror a Sim Press pairing
        // onto whichever button is picked as its trigger (and back). Empty for a binding
        // with no representable code (mirroring becomes a no-op for those, same as the
        // Chorded Press trigger picker already tolerates).
        internal JoypadActionCodes OwnTriggerCode => owner.FindTriggerCodeForBindingName(BindingName);
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
        public bool HasSimPress => HasFunc<SimPressFunc>();
        public bool HasStartPress => HasFunc<StartPressFunc>();
        public bool HasReleasePress => HasFunc<ReleaseFunc>();
        public bool CanAddHoldPress => !HasHoldPress;
        public bool CanAddDoublePress => !HasDoublePress;
        public bool CanAddDistancePress => !HasDistancePress;
        public bool CanAddChordedPress => !HasChordedPress;
        public bool CanAddSimPress => !HasSimPress;
        public bool CanAddStartPress => !HasStartPress;
        public bool CanAddReleasePress => !HasReleasePress;

        // True for a Steam Controller 2 touchpad click binding, set at construction time
        // (ProfileEditorTestViewModel.AddTouchpadButtonBinding) based on device type and
        // binding name - NOT derived solely from the current action's runtime type, since a
        // fresh/never-edited binding is still a plain ButtonNoAction and Soft Press/Full Press
        // must appear (permanently, unbound) from the very first render, not just after the
        // action gets lazily upgraded on first edit. Drives the Activation Style panel and the
        // Soft Press/Full Press row layout in SharedButtonKeybindsControl.
        public bool IsTouchpadPressureBinding =>
            isTouchpadPressureCapable || mappedAction is TouchpadPressureDualStageAction;

        private TouchpadPressureDualStageAction TouchPressureAction =>
            mappedAction as TouchpadPressureDualStageAction;

        public TriggerDualStageAction.DualStageMode ActivationStyle
        {
            get => TouchPressureAction?.ActivationStyle ?? TriggerDualStageAction.DualStageMode.Threshold;
            set
            {
                TouchpadPressureDualStageAction action = owner.EnsureEditableTouchpadPressureAction(this);
                if (action == null || action.ActivationStyle == value) return;
                action.ActivationStyle = value;
                OnPropertyChanged(nameof(ActivationStyle));
            }
        }

        public int SoftPressThreshold
        {
            get => TouchPressureAction?.SoftPressThreshold ?? TouchpadPressureDualStageAction.DEFAULT_SOFT_THRESHOLD;
            set
            {
                TouchpadPressureDualStageAction action = owner.EnsureEditableTouchpadPressureAction(this);
                if (action == null) return;
                action.SoftPressThreshold = value;
                OnPropertyChanged(nameof(SoftPressThreshold));
                OnPropertyChanged(nameof(SoftPressThresholdText));
                OnPropertyChanged(nameof(FullPressThreshold));
                OnPropertyChanged(nameof(FullPressThresholdText));
            }
        }

        public int FullPressThreshold
        {
            get => TouchPressureAction?.FullPressThreshold ?? TouchpadPressureDualStageAction.DEFAULT_FULL_THRESHOLD;
            set
            {
                TouchpadPressureDualStageAction action = owner.EnsureEditableTouchpadPressureAction(this);
                if (action == null) return;
                action.FullPressThreshold = value;
                OnPropertyChanged(nameof(FullPressThreshold));
                OnPropertyChanged(nameof(FullPressThresholdText));
                OnPropertyChanged(nameof(SoftPressThreshold));
                OnPropertyChanged(nameof(SoftPressThresholdText));
            }
        }

        // String-formatted (thousands separator, e.g. "13,096") mirrors of the two threshold
        // properties above, for direct TextBox entry per the UI requirement that large
        // threshold values display with a separator. Parsing tolerates a typed/pasted
        // separator; the underlying model setter still clamps to 0-32767 and maintains the
        // Soft < Full invariant.
        public string SoftPressThresholdText
        {
            get => SoftPressThreshold.ToString("N0");
            set
            {
                if (TryParseThreshold(value, out int temp)) SoftPressThreshold = temp;
            }
        }

        public string FullPressThresholdText
        {
            get => FullPressThreshold.ToString("N0");
            set
            {
                if (TryParseThreshold(value, out int temp)) FullPressThreshold = temp;
            }
        }

        private static bool TryParseThreshold(string value, out int result)
        {
            return int.TryParse(value,
                System.Globalization.NumberStyles.Integer | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.CurrentCulture, out result);
        }

        public int HipFireDelayMs
        {
            get => TouchPressureAction?.HipFireDelayMs ?? TouchpadPressureDualStageAction.DEFAULT_HIPFIRE_DELAY_MS;
            set
            {
                TouchpadPressureDualStageAction action = owner.EnsureEditableTouchpadPressureAction(this);
                if (action == null) return;
                action.HipFireDelayMs = value;
                OnPropertyChanged(nameof(HipFireDelayMs));
            }
        }

        public bool ForceHipFireDelay
        {
            get => TouchPressureAction?.ForceHipFireDelay ?? false;
            set
            {
                TouchpadPressureDualStageAction action = owner.EnsureEditableTouchpadPressureAction(this);
                if (action == null) return;
                action.ForceHipFireDelay = value;
                OnPropertyChanged(nameof(ForceHipFireDelay));
            }
        }

        public List<ActivationStyleChoice> ActivationStyleItems { get; } =
            new List<ActivationStyleChoice>
            {
                new ActivationStyleChoice("Threshold", TriggerDualStageAction.DualStageMode.Threshold),
                new ActivationStyleChoice("Exclusive Buttons", TriggerDualStageAction.DualStageMode.ExclusiveButtons),
                new ActivationStyleChoice("Hair Trigger", TriggerDualStageAction.DualStageMode.HairTrigger),
                new ActivationStyleChoice("Hip Fire", TriggerDualStageAction.DualStageMode.HipFire),
                new ActivationStyleChoice("Hip Fire Exclusive Buttons", TriggerDualStageAction.DualStageMode.HipFireExclusiveButtons),
            };

        // Resolves the ButtonAction that actually owns a given activator kind's ActionFuncs.
        // For every existing binding type this is unchanged (the top-level ButtonAction).
        // For a touchpad pressure binding, Soft Press owns its own sub-button while Full
        // Press and every optional activator (Hold/Double/etc.) live on the Full Press
        // sub-button - Full Press is the migrated successor of the old Regular Press button,
        // so anything that used to attach to Regular Press now attaches there.
        internal ButtonAction ResolveHostButtonAction(FaceBindingFuncKind kind)
        {
            if (IsTouchpadPressureBinding)
            {
                TouchpadPressureDualStageAction touchAction = mappedAction as TouchpadPressureDualStageAction;
                if (touchAction == null) return null;

                return kind == FaceBindingFuncKind.SoftPress ?
                    touchAction.SoftPressActButton : touchAction.FullPressActButton;
            }

            return mappedAction as ButtonAction;
        }

        // Same resolution as ResolveHostButtonAction, but clones the binding into the
        // current layer first (copy-on-write) so edits never land on a shared/inherited
        // default-layer object. Mirrors EnsureEditableFaceButtonAction for the plain
        // ButtonAction case. Uses IsTouchpadPressureBinding (not a type check on mappedAction)
        // so this still routes correctly the very first time a never-edited Soft/Full Press
        // row is opened, while mappedAction is still ButtonNoAction.
        internal ButtonAction EnsureEditableHostButtonAction(FaceBindingFuncKind kind)
        {
            if (IsTouchpadPressureBinding)
            {
                TouchpadPressureDualStageAction action = owner.EnsureEditableTouchpadPressureAction(this);
                if (action == null) return null;

                return kind == FaceBindingFuncKind.SoftPress ?
                    action.SoftPressActButton : action.FullPressActButton;
            }

            return owner.EnsureEditableFaceButtonAction(this);
        }

        public FaceButtonBindingItem(ProfileEditorTestViewModel owner,
            BindingItemsTest sourceItem, string displayName, string subtitle = null,
            bool isTouchpadPressureCapable = false)
        {
            this.owner = owner;
            BindingName = sourceItem.BindingName;
            DisplayName = displayName;
            Subtitle = subtitle;
            mappedAction = sourceItem.MappedAction;
            this.isTouchpadPressureCapable = isTouchpadPressureCapable;

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

            if (IsTouchpadPressureBinding)
            {
                TouchpadPressureDualStageAction touchAction = mappedAction as TouchpadPressureDualStageAction;

                ActionFunc softFunc = touchAction?.SoftPressActButton.ActionFuncs
                    .OfType<NormalPressFunc>().FirstOrDefault();
                functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.SoftPress, softFunc));

                ActionFunc fullFunc = touchAction?.FullPressActButton.ActionFuncs
                    .OfType<NormalPressFunc>().FirstOrDefault();
                functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.FullPress, fullFunc));

                foreach (ActionFunc func in touchAction?.FullPressActButton.ActionFuncs ?? Enumerable.Empty<ActionFunc>())
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
                        case SimPressFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.SimPress, func));
                            break;
                        case StartPressFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Start, func));
                            break;
                        case ReleaseFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.Release, func));
                            break;
                    }
                }

                RaiseAvailabilityChanged();
                return;
            }

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
                        case SimPressFunc:
                            functionItems.Add(new FaceButtonFuncItem(this, FaceBindingFuncKind.SimPress, func));
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
            if (kind == FaceBindingFuncKind.Regular || kind == FaceBindingFuncKind.SoftPress ||
                kind == FaceBindingFuncKind.FullPress || HasKind(kind)) return null;

            ButtonAction buttonAction = EnsureEditableHostButtonAction(kind);
            if (buttonAction == null) return null;
            ActionFunc func = CreateFuncForKind(kind);
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
            if (item == null || item.Kind == FaceBindingFuncKind.Regular ||
                item.Kind == FaceBindingFuncKind.SoftPress || item.Kind == FaceBindingFuncKind.FullPress ||
                item.Func == null) return;

            ButtonAction buttonAction = EnsureEditableHostButtonAction(item.Kind);
            if (buttonAction == null) return;
            ActionFunc func = item.ResolveCurrentFunc(buttonAction);
            int index = buttonAction.ActionFuncs.IndexOf(func);
            if (index < 0) return;

            // A Sim Press being deleted outright (rather than its trigger being changed/
            // cleared) needs the same mirror cleanup as RemoveSimPressMirror already does
            // for the trigger-change case, or the paired button is left with a stale
            // one-sided pairing.
            JoypadActionCodes simPressTrigger = func is SimPressFunc simPressFunc ?
                simPressFunc.TriggerButton : JoypadActionCodes.Empty;

            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.RemoveAt(index);
                MarkFunctionsChanged(buttonAction);
            });

            RefreshFunctions();

            if (simPressTrigger != JoypadActionCodes.Empty)
            {
                owner.RemoveSimPressMirror(OwnTriggerCode, simPressTrigger);
            }
        }

        public EditFaceBindingContext PrepareEdit(FaceButtonFuncItem item)
        {
            if (item == null) return null;

            ButtonAction buttonAction = EnsureEditableHostButtonAction(item.Kind);
            if (buttonAction == null) return null;
            ActionFunc func = item.ResolveCurrentFunc(buttonAction);

            if (func == null)
            {
                func = CreateFuncForKind(item.Kind);
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
                FaceBindingFuncKind.SimPress => HasSimPress,
                FaceBindingFuncKind.Start => HasStartPress,
                FaceBindingFuncKind.Release => HasReleasePress,
                _ => false,
            };
        }

        private bool HasFunc<TFunc>() where TFunc : ActionFunc
        {
            ButtonAction hostAction = IsTouchpadPressureBinding ?
                (mappedAction as TouchpadPressureDualStageAction)?.FullPressActButton :
                mappedAction as ButtonAction;
            return hostAction != null && hostAction.ActionFuncs.OfType<TFunc>().Any();
        }

        internal static ActionFunc CreateFuncForKind(FaceBindingFuncKind kind)
        {
            OutputActionData emptyOutput =
                new OutputActionData(OutputActionData.ActionType.Empty, 0);

            return kind switch
            {
                FaceBindingFuncKind.Regular => new NormalPressFunc(emptyOutput),
                FaceBindingFuncKind.SoftPress => new NormalPressFunc(emptyOutput),
                FaceBindingFuncKind.FullPress => new NormalPressFunc(emptyOutput),
                FaceBindingFuncKind.Hold => CreateOutputFunc(new HoldPressFunc(), emptyOutput),
                FaceBindingFuncKind.Double => CreateOutputFunc(new DoublePressFunc()
                {
                    DurationMs = DoublePressFunc.DEFAULT_TAP_WINDOW_MS,
                }, emptyOutput),
                FaceBindingFuncKind.Distance => CreateOutputFunc(new DistanceFunc(), emptyOutput),
                FaceBindingFuncKind.Chorded => CreateOutputFunc(new ChordedPressFunc(), emptyOutput),
                FaceBindingFuncKind.SimPress => CreateOutputFunc(new SimPressFunc(), emptyOutput),
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
            OnPropertyChanged(nameof(HasSimPress));
            OnPropertyChanged(nameof(HasStartPress));
            OnPropertyChanged(nameof(HasReleasePress));
            OnPropertyChanged(nameof(CanAddHoldPress));
            OnPropertyChanged(nameof(CanAddDoublePress));
            OnPropertyChanged(nameof(CanAddDistancePress));
            OnPropertyChanged(nameof(CanAddChordedPress));
            OnPropertyChanged(nameof(CanAddSimPress));
            OnPropertyChanged(nameof(CanAddStartPress));
            OnPropertyChanged(nameof(CanAddReleasePress));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FaceButtonFuncItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly FaceButtonBindingItem owner;
        private readonly ActionFunc func;
        private readonly ObservableCollection<ActionOutputItem> outputItems =
            new ObservableCollection<ActionOutputItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public FaceButtonBindingItem Owner => owner;
        public FaceBindingFuncKind Kind { get; }
        public ActionFunc Func => func;
        public ObservableCollection<ActionOutputItem> OutputItems => outputItems;
        public bool IsExtraBinding => Kind != FaceBindingFuncKind.Regular &&
            Kind != FaceBindingFuncKind.SoftPress && Kind != FaceBindingFuncKind.FullPress && func != null;
        public bool CanRemove => IsExtraBinding;
        public bool IsTurboEnabled => SupportsTurbo && TurboEnabled;
        public bool SupportsToggle => func is NormalPressFunc || func is HoldPressFunc || func is DoublePressFunc || func is StartPressFunc || func is ReleaseFunc;
        public bool SupportsTurbo => func is NormalPressFunc || func is HoldPressFunc;
        public bool SupportsFireDelay => func is NormalPressFunc;
        public bool SupportsHoldTime => func is HoldPressFunc;
        public bool SupportsTapWindow => func is DoublePressFunc;
        public bool SupportsInterruptable => func is HoldPressFunc || func is DoublePressFunc;
        public bool SupportsReleaseOptions => func is ReleaseFunc;
        public bool SupportsDistanceOptions => func is DistanceFunc;
        public bool SupportsChordOptions => func is ChordedPressFunc;
        public bool SupportsSimPressOptions => func is SimPressFunc;

        public string DisplayName => Kind switch
        {
            FaceBindingFuncKind.Regular => "Regular Press",
            FaceBindingFuncKind.SoftPress => "Soft Press",
            FaceBindingFuncKind.FullPress => "Full Press",
            FaceBindingFuncKind.Hold => "Hold Press",
            FaceBindingFuncKind.Double => "Double Press",
            FaceBindingFuncKind.Distance => "Distance",
            FaceBindingFuncKind.Chorded => "Chorded Press",
            FaceBindingFuncKind.SimPress => "Sim Press",
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
                FaceBindingFuncKind.Regular or FaceBindingFuncKind.SoftPress or FaceBindingFuncKind.FullPress =>
                    buttonAction.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Hold => buttonAction.ActionFuncs.OfType<HoldPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Double => buttonAction.ActionFuncs.OfType<DoublePressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Distance => buttonAction.ActionFuncs.OfType<DistanceFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Chorded => buttonAction.ActionFuncs.OfType<ChordedPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.SimPress => buttonAction.ActionFuncs.OfType<SimPressFunc>().FirstOrDefault(),
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
            ButtonAction buttonAction = owner.EnsureEditableHostButtonAction(Kind);
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

        public bool Interruptable
        {
            get => func switch
            {
                HoldPressFunc hold => hold.InterruptRegularPress,
                DoublePressFunc doublePress => doublePress.InterruptRegularPress,
                _ => false,
            };
            set
            {
                if (!SupportsInterruptable || Interruptable == value) return;
                var (buttonAction, target) = BeginEdit();
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    switch (target)
                    {
                        case HoldPressFunc hold: hold.InterruptRegularPress = value; break;
                        case DoublePressFunc doublePress: doublePress.InterruptRegularPress = value; break;
                    }
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
                if (func is not DistanceFunc distanceFunc || double.IsNaN(value)) return;
                double clampedValue = Math.Clamp(value, 0.0, 1.0);
                if (distanceFunc.distance == clampedValue) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not DistanceFunc targetFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.distance = clampedValue;
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

        public List<ActionTriggerItem> SimPressTriggerItems =>
            ChordedPressFuncUi.BuildTriggerItems(owner.Owner.DeviceMapper);

        public JoypadActionCodes SimPressTrigger
        {
            get => func is SimPressFunc simPress ? simPress.TriggerButton : JoypadActionCodes.Empty;
            set
            {
                if (func is not SimPressFunc simPressGuard || simPressGuard.TriggerButton == value) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not SimPressFunc targetFunc) return;

                JoypadActionCodes oldTrigger = targetFunc.TriggerButton;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.TriggerButton = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();

                if (oldTrigger != JoypadActionCodes.Empty && oldTrigger != value)
                {
                    owner.Owner.RemoveSimPressMirror(owner.OwnTriggerCode, oldTrigger);
                }

                if (value != JoypadActionCodes.Empty)
                {
                    owner.Owner.ApplySimPressMirror(owner.OwnTriggerCode, targetFunc);
                }
            }
        }

        public int SimPressTimeMs
        {
            get => func is SimPressFunc simPress ? simPress.SimPressTimeMs : SimPressFunc.DEFAULT_SIM_PRESS_MS;
            set
            {
                if (func is not SimPressFunc) return;
                var (buttonAction, target) = BeginEdit();
                if (target is not SimPressFunc targetFunc || targetFunc.SimPressTimeMs == value) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    owner.Owner.ReleaseFaceAction(owner);
                    targetFunc.SimPressTimeMs = value;
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });
                owner.RefreshAfterEdit();

                if (targetFunc.TriggerButton != JoypadActionCodes.Empty)
                {
                    owner.Owner.ApplySimPressMirror(owner.OwnTriggerCode, targetFunc);
                }
            }
        }

        // Re-propagates a Sim Press pairing's current trigger, time, and output onto the
        // trigger button whenever an edit could have changed any of those - keeps both
        // sides identical after output edits, not just after the Trigger/Time setters.
        private void MirrorSimPressIfNeeded()
        {
            ButtonAction hostAction = owner.EnsureEditableHostButtonAction(Kind);
            if (hostAction != null && ResolveCurrentFunc(hostAction) is SimPressFunc simPressFunc &&
                simPressFunc.TriggerButton != JoypadActionCodes.Empty)
            {
                owner.Owner.ApplySimPressMirror(owner.OwnTriggerCode, simPressFunc);
            }
        }

        public FaceButtonFuncItem(FaceButtonBindingItem owner, FaceBindingFuncKind kind,
            ActionFunc func)
        {
            this.owner = owner;
            Kind = kind;
            this.func = func;
            RefreshOutputItems();
        }

        internal ActionFunc EnsureCurrentFunc(ButtonAction buttonAction)
        {
            ActionFunc target = ResolveCurrentFunc(buttonAction);
            if (target != null) return target;

            target = FaceButtonBindingItem.CreateFuncForKind(Kind);
            if (target == null) return null;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.Add(target);
                FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
            });

            return target;
        }

        public void AddOutputAction()
        {
            ButtonAction buttonAction = owner.EnsureEditableHostButtonAction(Kind);
            if (buttonAction == null) return;
            ActionFunc target = EnsureCurrentFunc(buttonAction);
            if (target == null) return;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                target.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
            });

            owner.RefreshAfterEdit();
            MirrorSimPressIfNeeded();
        }

        public void RemoveOutputAction(ActionOutputItem item)
        {
            if (item == null) return;

            ButtonAction buttonAction = owner.EnsureEditableHostButtonAction(Kind);
            if (buttonAction == null) return;
            ActionFunc target = EnsureCurrentFunc(buttonAction);
            if (target == null) return;

            int index = item.Index;
            if (index < 0 || index >= target.OutputActions.Count) return;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);

                if (target.OutputActions.Count <= 1)
                {
                    OutputActionData data = target.OutputActions[0];
                    data.Reset();
                    data.Prepare(OutputActionData.ActionType.Empty, 0);
                    data.OutputCodeStr = OutputActionData.ActionType.Empty.ToString();
                }
                else
                {
                    target.OutputActions.RemoveAt(index);
                }

                FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
            });

            owner.RefreshAfterEdit();
            MirrorSimPressIfNeeded();
        }

        internal EditFaceBindingContext PrepareEdit(ActionOutputItem item)
        {
            ButtonAction buttonAction = owner.EnsureEditableHostButtonAction(Kind);
            if (buttonAction == null) return null;
            ActionFunc target = EnsureCurrentFunc(buttonAction);
            if (target == null) return null;

            int index = item?.Index ?? 0;
            while (target.OutputActions.Count <= index)
            {
                target.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
            }

            return new EditFaceBindingContext(owner.Owner.DeviceMapper, buttonAction, target, index);
        }

        private void RefreshOutputItems()
        {
            outputItems.Clear();
            int count = Math.Max(1, func?.OutputActions.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                outputItems.Add(new ActionOutputItem(this, i));
            }
        }

        // IQuickBindTarget
        Mapper IQuickBindTarget.Mapper => owner.Owner.DeviceMapper;
        string IQuickBindTarget.RowLabel => owner.DisplayName;
        string IQuickBindTarget.SlotLabel => DisplayName;
        bool IQuickBindTarget.IsComplexBinding => !QuickBindActionApplier.IsSimpleFunc(func);
        EditFaceBindingContext IQuickBindTarget.GetEditContext() => owner.PrepareEdit(this);
        void IQuickBindTarget.NotifyBindingChanged()
        {
            owner.RefreshAfterEdit();
            MirrorSimPressIfNeeded();
        }

        Mapper IActionOutputListOwner.Mapper => owner.Owner.DeviceMapper;
        string IActionOutputListOwner.RowLabel => owner.DisplayName;
        string IActionOutputListOwner.SlotLabel => DisplayName;
        ActionFunc IActionOutputListOwner.Func => func;
        EditFaceBindingContext IActionOutputListOwner.PrepareEdit(ActionOutputItem item) => PrepareEdit(item);
        void IActionOutputListOwner.AddOutputAction() => AddOutputAction();
        void IActionOutputListOwner.RemoveOutputAction(ActionOutputItem item) => RemoveOutputAction(item);
        void IActionOutputListOwner.NotifyBindingChanged()
        {
            owner.RefreshAfterEdit();
            MirrorSimPressIfNeeded();
        }

        public void Refresh()
        {
            RefreshOutputItems();
            OnPropertyChanged(nameof(DisplayBind));
            OnPropertyChanged(nameof(OutputItems));
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
            OnPropertyChanged(nameof(SimPressTrigger));
            OnPropertyChanged(nameof(SimPressTimeMs));
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
            : this(mapper, action, func, null)
        {
        }

        public int? OutputIndex { get; }

        public EditFaceBindingContext(Mapper mapper, ButtonAction action, ActionFunc func,
            int? outputIndex)
        {
            Mapper = mapper;
            Action = action;
            Func = func;
            OutputIndex = outputIndex;
        }
    }

    // ComboBox item for the touchpad pressure Activation Style dropdown. Uses the same
    // five options, in the same order, as the trigger dual-stage Activation Style dropdown.
    public class ActivationStyleChoice
    {
        public string DisplayName { get; }
        public TriggerDualStageAction.DualStageMode Value { get; }

        public ActivationStyleChoice(string displayName, TriggerDualStageAction.DualStageMode value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
