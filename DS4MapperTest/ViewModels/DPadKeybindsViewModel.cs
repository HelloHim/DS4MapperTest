using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.DPadActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.ViewModels.DPadActionPropViewModels;

namespace DS4MapperTest.ViewModels
{
    public enum DPadTopLevelMode
    {
        ActionPad,
        Translate,
        NoAction,
    }

    public enum DPadDirectionKind
    {
        Up,
        Down,
        Left,
        Right,
    }

    public class DPadKeybindsViewModel : INotifyPropertyChanged
    {
        private readonly ProfileEditorTestViewModel owner;
        private readonly ObservableCollection<DPadDirectionBindingItem> directions =
            new ObservableCollection<DPadDirectionBindingItem>();
        private readonly List<PadModeItem> padModeItems = new List<PadModeItem>
        {
            new PadModeItem("Standard", DPadAction.DPadMode.Standard),
            new PadModeItem("Eight Way", DPadAction.DPadMode.EightWay),
            new PadModeItem("Four Way Cardinal", DPadAction.DPadMode.FourWayCardinal),
            new PadModeItem("Four Way Diagonal", DPadAction.DPadMode.FourWayDiagonal),
        };
        private readonly List<DPadTopLevelModeItem> topLevelModeItems = new List<DPadTopLevelModeItem>
        {
            new DPadTopLevelModeItem("Action Pad", DPadTopLevelMode.ActionPad),
            new DPadTopLevelModeItem("Translate", DPadTopLevelMode.Translate),
            new DPadTopLevelModeItem("No Action", DPadTopLevelMode.NoAction),
        };
        private readonly List<DPadOutputItem> outputDPadItems = new List<DPadOutputItem>
        {
            new DPadOutputItem("Unbound", DPadActionCodes.Empty),
            new DPadOutputItem("X360 D-Pad", DPadActionCodes.DPad1),
            new DPadOutputItem("D-Pad 2", DPadActionCodes.DPad2),
        };

        private bool suppressPadModeChange;
        private bool suppressTopLevelModeChange;
        private int selectedPadModeIndex;
        private int selectedTopLevelModeIndex;

        public event PropertyChangedEventHandler PropertyChanged;

        public ProfileEditorTestViewModel Owner => owner;
        public ObservableCollection<DPadDirectionBindingItem> Directions => directions;
        public List<PadModeItem> PadModeItems => padModeItems;
        public List<DPadTopLevelModeItem> TopLevelModeItems => topLevelModeItems;
        public List<DPadOutputItem> OutputDPadItems => outputDPadItems;

        public int SelectedTopLevelModeIndex
        {
            get => selectedTopLevelModeIndex;
            set
            {
                if (selectedTopLevelModeIndex == value) return;
                selectedTopLevelModeIndex = value;
                OnPropertyChanged(nameof(SelectedTopLevelModeIndex));

                if (suppressTopLevelModeChange || value < 0 || value >= topLevelModeItems.Count) return;
                owner.SetDPadTopLevelMode(topLevelModeItems[value].Mode);
                Refresh();
            }
        }

        public int SelectedPadModeIndex
        {
            get => selectedPadModeIndex;
            set
            {
                if (selectedPadModeIndex == value) return;
                selectedPadModeIndex = value;
                OnPropertyChanged(nameof(SelectedPadModeIndex));

                if (suppressPadModeChange || value < 0 || value >= padModeItems.Count) return;
                owner.SetDPadMode(padModeItems[value].DPadMode);
                RefreshBehaviourState();
            }
        }

        public bool IsActionPad => owner.GetCurrentDPadMapAction() is DPadAction;
        public bool IsTranslate => owner.GetCurrentDPadMapAction() is DPadTranslate;
        public bool IsNoAction => owner.GetCurrentDPadMapAction() is DPadNoAction;

        public string ModeHelperText
        {
            get
            {
                return owner.GetCurrentDPadMapAction() switch
                {
                    DPadAction => "Bind each D-Pad direction to actions.",
                    DPadTranslate => "Translate D-Pad input using the existing DS4MapperTest translate settings.",
                    DPadNoAction => "Disable D-Pad action output for this profile set/layer.",
                    _ => "Select how this D-Pad input should behave.",
                };
            }
        }

        public string TranslateName
        {
            get => owner.GetCurrentDPadMapAction() is DPadTranslate action ? action.Name : "";
            set => owner.SetDPadTranslateName(value);
        }

        public DPadActionCodes TranslateOutputDPad
        {
            get => owner.GetCurrentDPadMapAction() is DPadTranslate action
                ? action.OutputAction.DpadCode
                : DPadActionCodes.Empty;
            set => owner.SetDPadTranslateOutputDPad(value);
        }

        public DPadKeybindsViewModel(ProfileEditorTestViewModel owner)
        {
            this.owner = owner;

            directions.Add(new DPadDirectionBindingItem(this, DPadDirectionKind.Up, "D-Pad Up"));
            directions.Add(new DPadDirectionBindingItem(this, DPadDirectionKind.Down, "D-Pad Down"));
            directions.Add(new DPadDirectionBindingItem(this, DPadDirectionKind.Left, "D-Pad Left"));
            directions.Add(new DPadDirectionBindingItem(this, DPadDirectionKind.Right, "D-Pad Right"));

            Refresh();
        }

        public void Refresh()
        {
            DPadMapAction current = owner.GetCurrentDPadMapAction();
            DPadTopLevelMode topLevelMode = current switch
            {
                DPadAction => DPadTopLevelMode.ActionPad,
                DPadTranslate => DPadTopLevelMode.Translate,
                DPadNoAction => DPadTopLevelMode.NoAction,
                _ => DPadTopLevelMode.NoAction,
            };
            int topLevelIndex = topLevelModeItems.FindIndex(item => item.Mode == topLevelMode);

            suppressTopLevelModeChange = true;
            SelectedTopLevelModeIndex = topLevelIndex >= 0 ? topLevelIndex : 0;
            suppressTopLevelModeChange = false;

            DPadAction.DPadMode mode = current is DPadAction dpadAction
                ? dpadAction.CurrentMode
                : DPadAction.DPadMode.Standard;

            int index = padModeItems.FindIndex(item => item.DPadMode == mode);

            suppressPadModeChange = true;
            SelectedPadModeIndex = index >= 0 ? index : 0;
            suppressPadModeChange = false;

            foreach (DPadDirectionBindingItem item in directions)
            {
                item.RefreshFunctions();
            }

            RefreshBehaviourState();
        }

        private void RefreshBehaviourState()
        {
            OnPropertyChanged(nameof(IsActionPad));
            OnPropertyChanged(nameof(IsTranslate));
            OnPropertyChanged(nameof(IsNoAction));
            OnPropertyChanged(nameof(ModeHelperText));
            OnPropertyChanged(nameof(TranslateName));
            OnPropertyChanged(nameof(TranslateOutputDPad));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DPadDirectionBindingItem : INotifyPropertyChanged
    {
        private readonly DPadKeybindsViewModel owner;
        private readonly ObservableCollection<DPadDirectionFuncItem> functionItems =
            new ObservableCollection<DPadDirectionFuncItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public DPadKeybindsViewModel Owner => owner;
        public ProfileEditorTestViewModel ProfileVm => owner.Owner;
        public DPadDirectionKind Kind { get; }
        public string DisplayName { get; }
        public ObservableCollection<DPadDirectionFuncItem> FunctionItems => functionItems;

        public bool HasHoldPress => HasFunc<HoldPressFunc>();
        public bool HasStartPress => HasFunc<StartPressFunc>();
        public bool HasReleasePress => HasFunc<ReleaseFunc>();
        public bool CanAddHoldPress => !HasHoldPress;
        public bool CanAddStartPress => !HasStartPress;
        public bool CanAddReleasePress => !HasReleasePress;

        public DPadDirectionBindingItem(DPadKeybindsViewModel owner, DPadDirectionKind kind, string displayName)
        {
            this.owner = owner;
            Kind = kind;
            DisplayName = displayName;

            RefreshFunctions();
        }

        public ButtonAction CurrentButtonAction()
        {
            return ProfileVm.PeekDPadDirectionAction(Kind);
        }

        public string TranslatedOutputDisplay()
        {
            return ProfileVm.GetDPadTranslatedDirectionDisplay(Kind);
        }


        public void RefreshFunctions()
        {
            functionItems.Clear();

            ButtonAction buttonAction = CurrentButtonAction();
            functionItems.Add(new DPadDirectionFuncItem(this, FaceBindingFuncKind.Regular));

            if (buttonAction != null)
            {
                foreach (ActionFunc func in buttonAction.ActionFuncs)
                {
                    switch (func)
                    {
                        case HoldPressFunc:
                            functionItems.Add(new DPadDirectionFuncItem(this, FaceBindingFuncKind.Hold));
                            break;
                        case StartPressFunc:
                            functionItems.Add(new DPadDirectionFuncItem(this, FaceBindingFuncKind.Start));
                            break;
                        case ReleaseFunc:
                            functionItems.Add(new DPadDirectionFuncItem(this, FaceBindingFuncKind.Release));
                            break;
                    }
                }
            }

            RaiseAvailabilityChanged();
        }

        public DPadDirectionFuncItem AddExtraBinding(FaceBindingFuncKind kind)
        {
            if (kind == FaceBindingFuncKind.Regular || HasKind(kind)) return null;

            ButtonAction buttonAction = ProfileVm.EnsureEditableDPadDirectionAction(Kind);
            ActionFunc func = CreateFunc(kind);
            if (func == null) return null;

            ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(ProfileVm.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.Add(func);
                MarkFunctionsChanged(buttonAction);
            });

            owner.Refresh();
            return functionItems.FirstOrDefault(item => item.Kind == kind);
        }

        public void RemoveBinding(DPadDirectionFuncItem item)
        {
            if (item == null || item.Kind == FaceBindingFuncKind.Regular || item.Func == null) return;

            ButtonAction buttonAction = ProfileVm.EnsureEditableDPadDirectionAction(Kind);
            int index = buttonAction.ActionFuncs.IndexOf(item.Func);
            if (index < 0) return;

            ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(ProfileVm.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.RemoveAt(index);
                MarkFunctionsChanged(buttonAction);
            });

            owner.Refresh();
        }

        public EditFaceBindingContext PrepareEdit(DPadDirectionFuncItem item)
        {
            if (item == null) return null;

            ButtonAction buttonAction = ProfileVm.EnsureEditableDPadDirectionAction(Kind);
            ActionFunc func = item.Func;

            if (func == null)
            {
                func = CreateFunc(item.Kind);
                ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    buttonAction.Release(ProfileVm.DeviceMapper, ignoreReleaseActions: true);
                    buttonAction.ActionFuncs.Add(func);
                    MarkFunctionsChanged(buttonAction);
                });

                RefreshFunctions();
                item = functionItems.FirstOrDefault(temp => temp.Kind == item.Kind);
            }

            return new EditFaceBindingContext(ProfileVm.DeviceMapper, buttonAction, item?.Func ?? func);
        }

        public void RefreshAfterEdit()
        {
            owner.Refresh();
        }

        private bool HasKind(FaceBindingFuncKind kind)
        {
            return kind switch
            {
                FaceBindingFuncKind.Hold => HasHoldPress,
                FaceBindingFuncKind.Start => HasStartPress,
                FaceBindingFuncKind.Release => HasReleasePress,
                _ => false,
            };
        }

        private bool HasFunc<TFunc>() where TFunc : ActionFunc
        {
            return CurrentButtonAction() is ButtonAction buttonAction &&
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
            OnPropertyChanged(nameof(HasStartPress));
            OnPropertyChanged(nameof(HasReleasePress));
            OnPropertyChanged(nameof(CanAddHoldPress));
            OnPropertyChanged(nameof(CanAddStartPress));
            OnPropertyChanged(nameof(CanAddReleasePress));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DPadDirectionFuncItem : INotifyPropertyChanged
    {
        private readonly DPadDirectionBindingItem owner;

        public event PropertyChangedEventHandler PropertyChanged;

        public DPadDirectionBindingItem Owner => owner;
        public FaceBindingFuncKind Kind { get; }

        public ActionFunc Func => FindFunc(owner.CurrentButtonAction(), Kind);

        public bool IsExtraBinding => Kind != FaceBindingFuncKind.Regular && Func != null;
        public bool CanRemove => IsExtraBinding;
        public bool IsTurboEnabled => SupportsTurbo && TurboEnabled;
        public bool SupportsToggle => Func is NormalPressFunc || Func is HoldPressFunc || Func is StartPressFunc || Func is ReleaseFunc;
        public bool SupportsTurbo => Func is NormalPressFunc || Func is HoldPressFunc;
        public bool SupportsFireDelay => Func is NormalPressFunc;
        public bool SupportsHoldTime => Func is HoldPressFunc;
        public bool SupportsReleaseOptions => Func is ReleaseFunc;

        public string DisplayName => Kind switch
        {
            FaceBindingFuncKind.Regular => "Regular Press",
            FaceBindingFuncKind.Hold => "Hold Press",
            FaceBindingFuncKind.Start => "Start Press",
            FaceBindingFuncKind.Release => "Release Press",
            _ => "Binding",
        };

        public string DisplayBind
        {
            get
            {
                if (Func == null && Kind == FaceBindingFuncKind.Regular)
                {
                    string translatedResult = owner.TranslatedOutputDisplay();
                    if (!string.IsNullOrWhiteSpace(translatedResult))
                    {
                        return translatedResult;
                    }
                }


                string result = Func?.DescribeOutputActions(owner.ProfileVm.DeviceMapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        public bool ToggleEnabled
        {
            get => Func?.toggleEnabled ?? false;
            set
            {
                ActionFunc currentFunc = Func;
                if (currentFunc == null || currentFunc.toggleEnabled == value) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    ActionFunc target = FindFunc(editable, Kind);
                    if (target == null) return;

                    target.toggleEnabled = value;
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(ToggleEnabled));
            }
        }

        public bool TurboEnabled
        {
            get
            {
                return Func switch
                {
                    NormalPressFunc normalPress => normalPress.TurboEnabled,
                    HoldPressFunc holdPress => holdPress.TurboEnabled,
                    _ => false,
                };
            }
            set
            {
                if (!SupportsTurbo || TurboEnabled == value) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    ActionFunc target = FindFunc(editable, Kind);
                    switch (target)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboEnabled = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboEnabled = value;
                            break;
                    }
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(TurboEnabled));
                OnPropertyChanged(nameof(IsTurboEnabled));
            }
        }

        public int TurboDurationMs
        {
            get
            {
                return Func switch
                {
                    NormalPressFunc normalPress => normalPress.TurboDurationMs,
                    HoldPressFunc holdPress => holdPress.TurboDurationMs,
                    _ => 0,
                };
            }
            set
            {
                if (!SupportsTurbo) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    ActionFunc target = FindFunc(editable, Kind);
                    switch (target)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboDurationMs = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboDurationMs = value;
                            break;
                    }
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(TurboDurationMs));
            }
        }

        public int FireDelayMs
        {
            get => Func is NormalPressFunc normalPress ? normalPress.FireDelayMs : 0;
            set
            {
                if (Func is not NormalPressFunc) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    if (FindFunc(editable, Kind) is NormalPressFunc normalPress)
                    {
                        normalPress.FireDelayMs = value;
                    }
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(FireDelayMs));
            }
        }

        public int HoldMs
        {
            get => Func is HoldPressFunc holdPress ? holdPress.DurationMs : 0;
            set
            {
                if (Func is not HoldPressFunc) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    if (FindFunc(editable, Kind) is HoldPressFunc holdPress)
                    {
                        holdPress.DurationMs = value;
                    }
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(HoldMs));
            }
        }

        public string ReleaseDurationMs
        {
            get => Func is ReleaseFunc releaseFunc ? releaseFunc.DurationMs.ToString() : "0";
            set
            {
                if (Func is not ReleaseFunc || !int.TryParse(value, out int temp)) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    if (FindFunc(editable, Kind) is ReleaseFunc releaseFunc)
                    {
                        releaseFunc.DurationMs = temp;
                    }
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(ReleaseDurationMs));
            }
        }

        public string ReleaseDelayMs
        {
            get => Func is ReleaseFunc releaseFunc ? releaseFunc.DelayDurationMs.ToString() : "0";
            set
            {
                if (Func is not ReleaseFunc || !int.TryParse(value, out int temp)) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    if (FindFunc(editable, Kind) is ReleaseFunc releaseFunc)
                    {
                        releaseFunc.DelayDurationMs = temp;
                    }
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(ReleaseDelayMs));
            }
        }

        public bool ReleaseInterruptable
        {
            get => Func is ReleaseFunc releaseFunc && releaseFunc.interruptable;
            set
            {
                if (Func is not ReleaseFunc currentReleaseFunc || currentReleaseFunc.interruptable == value) return;

                owner.ProfileVm.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.ProfileVm.EnsureEditableDPadDirectionAction(owner.Kind);
                    if (FindFunc(editable, Kind) is ReleaseFunc releaseFunc)
                    {
                        releaseFunc.interruptable = value;
                    }
                    DPadDirectionBindingItem.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(ReleaseInterruptable));
            }
        }

        public DPadDirectionFuncItem(DPadDirectionBindingItem owner, FaceBindingFuncKind kind)
        {
            this.owner = owner;
            Kind = kind;
        }

        private static ActionFunc FindFunc(ButtonAction action, FaceBindingFuncKind kind)
        {
            if (action == null) return null;

            return kind switch
            {
                FaceBindingFuncKind.Regular => action.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Hold => action.ActionFuncs.OfType<HoldPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Start => action.ActionFuncs.OfType<StartPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Release => action.ActionFuncs.OfType<ReleaseFunc>().FirstOrDefault(),
                _ => null,
            };
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DPadTopLevelModeItem
    {
        public string DisplayName { get; }
        public DPadTopLevelMode Mode { get; }

        public DPadTopLevelModeItem(string displayName, DPadTopLevelMode mode)
        {
            DisplayName = displayName;
            Mode = mode;
        }
    }

    public class DPadOutputItem
    {
        public string DisplayName { get; }
        public DPadActionCodes Code { get; }

        public DPadOutputItem(string displayName, DPadActionCodes code)
        {
            DisplayName = displayName;
            Code = code;
        }
    }
}
