using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.ViewModels.StickActionPropViewModels;

namespace DS4MapperTest.ViewModels
{
    public class StickModeItem
    {
        public string DisplayName { get; }
        public int Index { get; }

        public StickModeItem(string displayName, int index)
        {
            DisplayName = displayName;
            Index = index;
        }
    }

    public class StickSideViewModel : INotifyPropertyChanged
    {
        private static readonly List<StickModeItem> sharedModeItems = new List<StickModeItem>
        {
            new StickModeItem("Unbound", 0),
            new StickModeItem("Joystick", 1),
            new StickModeItem("Directional Pad", 2),
            new StickModeItem("Joystick Mouse", 3),
            new StickModeItem("Flick Stick", 6),
            new StickModeItem("Hybrid Aim", 8),
            new StickModeItem("Mouse Ring", 9),
            new StickModeItem("Circular", 4),
            new StickModeItem("Absolute Mouse", 5),
        };

        // The original Steam Controller registers its single stick as "Stick";
        // every other mapper uses "LS"/"RS"
        private static readonly string[] leftStickAliases = new string[] { "LS", "Stick" };
        private static readonly string[] rightStickAliases = new string[] { "RS" };

        private readonly ProfileEditorTestViewModel owner;
        private readonly string side;
        private readonly ObservableCollection<StickExtraBindingItem> extraBindings =
            new ObservableCollection<StickExtraBindingItem>();

        private int selectedModeIndex = -1;
        private bool suppressModeChange;
        private object settingsViewModel;
        private StickPadActionPropViewModel padSettingsVM;
        private StickAnalogEmulationPropViewModel analogSettingsVM;

        public event PropertyChangedEventHandler PropertyChanged;

        public ProfileEditorTestViewModel Owner => owner;
        public string Side => side;
        public string SideLabel => side == "LS" ? "Left Stick" : "Right Stick";
        public List<StickModeItem> ModeItems => sharedModeItems;
        public ObservableCollection<StickExtraBindingItem> ExtraBindings => extraBindings;
        public bool HasExtraBindings => extraBindings.Count > 0;

        public ObservableCollection<FaceButtonBindingItem> ClickBindingItems =>
            side == "LS" ? owner.LeftStickClickBinding : owner.RightStickClickBinding;

        public ObservableCollection<FaceButtonBindingItem> TouchBindingItems =>
            side == "LS" ? owner.LeftStickTouchBinding : owner.RightStickTouchBinding;

        public StickBindingItemsTest BindingItem
        {
            get
            {
                foreach (string alias in side == "LS" ? leftStickAliases : rightStickAliases)
                {
                    StickBindingItemsTest item = owner.StickBindings.FirstOrDefault(binding =>
                        string.Equals(binding.BindingName, alias, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        return item;
                    }
                }

                return null;
            }
        }

        public bool HasBinding => BindingItem != null;

        public StickMapAction CurrentAction => BindingItem?.MappedAction;

        public object SettingsViewModel
        {
            get => settingsViewModel;
            private set
            {
                settingsViewModel = value;
                OnPropertyChanged(nameof(SettingsViewModel));
                OnPropertyChanged(nameof(CurrentModeDescription));
                OnPropertyChanged(nameof(IsFlickStickMode));
                OnPropertyChanged(nameof(CurrentModeDisplayName));
            }
        }

        public string CurrentModeDescription =>
            settingsViewModel switch
            {
                StickTranslatePropViewModel => "Outputs standard analog joystick movement.",
                StickPadActionPropViewModel => "Maps stick position to directional button outputs.",
                StickAnalogEmulationPropViewModel => "Maps stick position to directional button outputs.",
                StickMousePropViewModel => "Uses stick tilt for relative mouse output, similar to gyro mouse.",
                StickCircularPropViewModel => "Uses circular stick movement for scroll-style clockwise and counter-clockwise output.",
                StickAbsMousePropViewModel => "Uses stick position for absolute mouse output.",
                StickFlickStickPropViewModel => "Flicks the camera to a stick angle instantly, with optional continued rotation while held.",
                StickHybridAimPropViewModel => "Combines aim-stick turn rate near centre with mouse-drag-like motion at the stick's edge.",
                StickMouseRingPropViewModel => "Places the mouse cursor on a fixed ring around screen centre based on stick angle.",
                _ => "This stick does not produce any output.",
            };

        public bool IsFlickStickMode => settingsViewModel is StickFlickStickPropViewModel;

        public string CurrentModeDisplayName =>
            (sharedModeItems.FirstOrDefault(item => item.Index == selectedModeIndex)
                ?? sharedModeItems[0]).DisplayName;

        public int SelectedModeIndex
        {
            get => selectedModeIndex;
            set
            {
                if (selectedModeIndex == value) return;
                selectedModeIndex = value;
                OnPropertyChanged(nameof(SelectedModeIndex));
                OnPropertyChanged(nameof(CurrentModeDisplayName));

                if (suppressModeChange) return;
                SwitchMode(value);
            }
        }

        public StickSideViewModel(ProfileEditorTestViewModel owner, string side)
        {
            this.owner = owner;
            this.side = side;

            Refresh();
        }

        public void Refresh()
        {
            int modeIndex = ResolveModeIndex(CurrentAction);

            suppressModeChange = true;
            SelectedModeIndex = modeIndex;
            suppressModeChange = false;

            RebuildSettingsViewModel();
            RebuildExtraBindings();

            OnPropertyChanged(nameof(HasBinding));
            OnPropertyChanged(nameof(ClickBindingItems));
        }

        private static int ResolveModeIndex(StickMapAction action)
        {
            return action switch
            {
                StickTranslate => 1,
                StickPadAction => 2,
                StickMouse => 3,
                StickCircular => 4,
                StickAbsMouse => 5,
                StickFlickStick => 6,
                StickHybridAim => 8,
                StickMouseRing => 9,
                // Analogue emulation is a D-Pad sub-mode, not a separate stick mode.
                StickAnalogEmulationAction => 2,
                _ => 0,
            };
        }

        private void SwitchMode(int modeIndex)
        {
            StickMapAction oldAction = CurrentAction;
            if (oldAction == null || ResolveModeIndex(oldAction) == modeIndex) return;

            StickBindEditViewModel editVM = new StickBindEditViewModel(owner.DeviceMapper, oldAction);
            StickMapAction newAction = editVM.PrepareNewAction(modeIndex);
            if (newAction == null) return;

            newAction.CopyBaseMapProps(oldAction);
            CarryOverSharedDigitalDirectionSettings(oldAction, newAction);
            editVM.MigrateActionId(newAction);
            editVM.SwitchAction(newAction);

            BindingItem?.UpdateAction(newAction);

            RebuildSettingsViewModel();
            RebuildExtraBindings();
        }

        // DPad and Analog Emulation both use the same four Up/Down/Left/Right bindings, the
        // same Counter Movement Release Press settings, and the same Dead Zone Type/Dead
        // Zone/Rotation stick-shaping settings. Switching between them shouldn't wipe those
        // out just because the rest of the mode's settings differ.
        private static void CarryOverSharedDigitalDirectionSettings(StickMapAction oldAction, StickMapAction newAction)
        {
            AxisDirButton[] oldDirs = null;
            CounterMovementReleasePressProcessor oldReleasePress = null;
            StickDeadZone oldDeadMod = null;
            int oldRotation = 0;

            if (oldAction is StickPadAction oldPad)
            {
                oldDirs = new AxisDirButton[4]
                {
                    oldPad.EventCodes4[(int)StickPadAction.DpadDirections.Up],
                    oldPad.EventCodes4[(int)StickPadAction.DpadDirections.Down],
                    oldPad.EventCodes4[(int)StickPadAction.DpadDirections.Left],
                    oldPad.EventCodes4[(int)StickPadAction.DpadDirections.Right],
                };
                oldReleasePress = oldPad.CounterMovementReleasePress;
                oldDeadMod = oldPad.DeadMod;
                oldRotation = oldPad.Rotation;
            }
            else if (oldAction is StickAnalogEmulationAction oldAnalog)
            {
                oldDirs = new AxisDirButton[4]
                {
                    oldAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Up],
                    oldAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Down],
                    oldAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Left],
                    oldAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Right],
                };
                oldReleasePress = oldAnalog.CounterMovementReleasePress;
                oldDeadMod = oldAnalog.DeadMod;
                oldRotation = oldAnalog.Rotation;
            }

            if (oldDirs == null) return;

            if (newAction is StickPadAction newPad)
            {
                newPad.EventCodes4[(int)StickPadAction.DpadDirections.Up] = oldDirs[0];
                newPad.EventCodes4[(int)StickPadAction.DpadDirections.Down] = oldDirs[1];
                newPad.EventCodes4[(int)StickPadAction.DpadDirections.Left] = oldDirs[2];
                newPad.EventCodes4[(int)StickPadAction.DpadDirections.Right] = oldDirs[3];
                CopyReleasePressSettings(oldReleasePress, newPad.CounterMovementReleasePress);
                CopyDeadZoneAndRotation(oldDeadMod, oldRotation, newPad.DeadMod, val => newPad.Rotation = val);
            }
            else if (newAction is StickAnalogEmulationAction newAnalog)
            {
                newAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Up] = oldDirs[0];
                newAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Down] = oldDirs[1];
                newAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Left] = oldDirs[2];
                newAnalog.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Right] = oldDirs[3];
                CopyReleasePressSettings(oldReleasePress, newAnalog.CounterMovementReleasePress);
                CopyDeadZoneAndRotation(oldDeadMod, oldRotation, newAnalog.DeadMod, val => newAnalog.Rotation = val);
            }
        }

        private static void CopyReleasePressSettings(CounterMovementReleasePressProcessor src, CounterMovementReleasePressProcessor dst)
        {
            if (src == null || dst == null) return;

            dst.Enabled = src.Enabled;
            dst.TapLengthPreset = src.TapLengthPreset;
            dst.OppositeTapLengthMinimumMs = src.OppositeTapLengthMinimumMs;
            dst.OppositeTapLengthMaximumMs = src.OppositeTapLengthMaximumMs;
            dst.OppositeTapStartDelayMinimumMs = src.OppositeTapStartDelayMinimumMs;
            dst.OppositeTapStartDelayMaximumMs = src.OppositeTapStartDelayMaximumMs;
            dst.MinimumHoldMs = src.MinimumHoldMs;
            dst.ArmingThreshold = src.ArmingThreshold;
        }

        private static void CopyDeadZoneAndRotation(StickDeadZone src, int srcRotation, StickDeadZone dst, Action<int> setRotation)
        {
            if (src == null || dst == null) return;

            dst.DeadZoneType = src.DeadZoneType;
            dst.DeadZone = src.DeadZone;
            setRotation(srcRotation);
        }

        private void RebuildSettingsViewModel()
        {
            if (padSettingsVM != null)
            {
                padSettingsVM.SelectedPadModeIndexChanged -= PadSettingsVM_SelectedPadModeIndexChanged;
                padSettingsVM = null;
            }
            if (analogSettingsVM != null)
            {
                analogSettingsVM.SelectedPadModeIndexChanged -= AnalogSettingsVM_SelectedPadModeIndexChanged;
                analogSettingsVM = null;
            }

            StickMapAction action = CurrentAction;
            SettingsViewModel = action switch
            {
                StickTranslate => new StickTranslatePropViewModel(owner.DeviceMapper, action),
                StickPadAction => new StickPadActionPropViewModel(owner.DeviceMapper, action),
                StickMouse => new StickMousePropViewModel(owner.DeviceMapper, action),
                StickCircular => new StickCircularPropViewModel(owner.DeviceMapper, action),
                StickAbsMouse => new StickAbsMousePropViewModel(owner.DeviceMapper, action),
                StickFlickStick => new StickFlickStickPropViewModel(owner.DeviceMapper, action),
                StickAnalogEmulationAction => new StickAnalogEmulationPropViewModel(owner.DeviceMapper, action),
                StickHybridAim => new StickHybridAimPropViewModel(owner.DeviceMapper, action),
                StickMouseRing => new StickMouseRingPropViewModel(owner.DeviceMapper, action),
                _ => (object)new StickNoActionPropViewModel(),
            };

            if (SettingsViewModel is StickPadActionPropViewModel padPropVM)
            {
                // ChangeStickPadMode is subscribed first inside the prop view
                // model constructor, so CurrentMode is already updated when
                // this handler fires
                padSettingsVM = padPropVM;
                padSettingsVM.SelectedPadModeIndexChanged += PadSettingsVM_SelectedPadModeIndexChanged;
            }
            else if (SettingsViewModel is StickAnalogEmulationPropViewModel analogPropVM)
            {
                analogSettingsVM = analogPropVM;
                analogPropVM.SelectedPadModeIndexChanged += AnalogSettingsVM_SelectedPadModeIndexChanged;
            }
        }

        private void PadSettingsVM_SelectedPadModeIndexChanged(object sender, EventArgs e)
        {
            if (padSettingsVM?.Action.CurrentMode == StickPadAction.DPadMode.AnalogEmulation)
            {
                SwitchDigitalPadMode(StickPadAction.DPadMode.AnalogEmulation);
                return;
            }

            RebuildExtraBindings();
        }

        private void AnalogSettingsVM_SelectedPadModeIndexChanged(object sender, EventArgs e)
        {
            StickAnalogEmulationPropViewModel analogVM = sender as StickAnalogEmulationPropViewModel;
            if (analogVM != null && analogVM.SelectedPadMode != StickPadAction.DPadMode.AnalogEmulation)
            {
                SwitchDigitalPadMode(analogVM.SelectedPadMode);
            }
        }

        private void SwitchDigitalPadMode(StickPadAction.DPadMode targetMode)
        {
            StickMapAction oldAction = CurrentAction;
            if (oldAction == null) return;

            bool useAnalogEmulation = targetMode == StickPadAction.DPadMode.AnalogEmulation;
            if ((useAnalogEmulation && oldAction is StickAnalogEmulationAction) ||
                (!useAnalogEmulation && oldAction is StickPadAction)) return;

            StickBindEditViewModel editVM = new StickBindEditViewModel(owner.DeviceMapper, oldAction);
            StickMapAction newAction = editVM.PrepareNewAction(useAnalogEmulation ? 7 : 2);
            if (newAction == null) return;

            newAction.CopyBaseMapProps(oldAction);
            CarryOverSharedDigitalDirectionSettings(oldAction, newAction);
            if (newAction is StickPadAction newPad)
            {
                newPad.CurrentMode = targetMode;
            }

            editVM.MigrateActionId(newAction);
            editVM.SwitchAction(newAction);
            BindingItem?.UpdateAction(newAction);
            RebuildSettingsViewModel();
            RebuildExtraBindings();
        }

        private void RebuildExtraBindings()
        {
            extraBindings.Clear();

            switch (CurrentAction)
            {
                case StickPadAction:
                    break;
                case StickCircular:
                    extraBindings.Add(new StickExtraBindingItem(this, "CW", "Clockwise"));
                    extraBindings.Add(new StickExtraBindingItem(this, "CCW", "Counter-clockwise"));
                    break;
                case StickAbsMouse:
                    extraBindings.Add(new StickExtraBindingItem(this, "Ring", "Ring / Outer Ring"));
                    break;
                case StickAnalogEmulationAction:
                    break;
                default:
                    break;
            }

            OnPropertyChanged(nameof(HasExtraBindings));
        }

        internal ButtonAction PeekExtraButtonAction(string slotKey)
        {
            return CurrentAction switch
            {
                StickPadAction pad when TryDirIndex(slotKey, out int idx) => pad.EventCodes4[idx],
                StickCircular circ when slotKey == "CW" => circ.ClockWiseBtn,
                StickCircular circ when slotKey == "CCW" => circ.CounterClockwiseBtn,
                StickAbsMouse abs when slotKey == "Ring" => abs.RingButton,
                StickAnalogEmulationAction analog when TryAnalogDirIndex(slotKey, out int aidx) => analog.DirButtons[aidx],
                _ => null,
            };
        }

        internal ButtonAction EnsureEditableExtraButtonAction(string slotKey)
        {
            switch (CurrentAction)
            {
                case StickPadAction pad when TryDirIndex(slotKey, out int idx):
                    return EnsurePadDirButton(pad, idx);
                case StickCircular circ when slotKey == "CW":
                    return EnsureCircularButton(circ, true);
                case StickCircular circ when slotKey == "CCW":
                    return EnsureCircularButton(circ, false);
                case StickAbsMouse abs when slotKey == "Ring":
                    return EnsureAbsMouseRingButton(abs);
                case StickAnalogEmulationAction analog when TryAnalogDirIndex(slotKey, out int aidx):
                    return EnsureAnalogDirButton(analog, aidx);
                default:
                    return null;
            }
        }

        private ButtonAction EnsurePadDirButton(StickPadAction pad, int idx)
        {
            AxisDirButton existing = pad.EventCodes4[idx];
            bool usingParent = pad.UsingParentActionButton[idx];
            if (!usingParent && HasNormalPressFunc(existing)) return existing;

            AxisDirButton newButton = new AxisDirButton();
            if (existing != null)
            {
                newButton.CopyBaseProps(existing);
                newButton.CopyAction(existing);
            }
            EnsureRegularPressFunc(newButton);

            StickPadActionPropViewModel propVm = settingsViewModel as StickPadActionPropViewModel;
            Action<ButtonAction, ButtonAction> updater = GetPadUpdater(propVm, idx);
            updater?.Invoke(existing, newButton);
            return newButton;
        }

        private static Action<ButtonAction, ButtonAction> GetPadUpdater(StickPadActionPropViewModel propVm, int idx)
        {
            if (propVm == null) return null;
            return (StickPadAction.DpadDirections)idx switch
            {
                StickPadAction.DpadDirections.Up => propVm.UpdateUpDirAction,
                StickPadAction.DpadDirections.Down => propVm.UpdateDownDirAction,
                StickPadAction.DpadDirections.Left => propVm.UpdateLeftDirAction,
                StickPadAction.DpadDirections.Right => propVm.UpdateRightDirAction,
                StickPadAction.DpadDirections.UpLeft => propVm.UpdateUpLeftDirAction,
                StickPadAction.DpadDirections.UpRight => propVm.UpdateUpRightDirAction,
                StickPadAction.DpadDirections.DownLeft => propVm.UpdateDownLeftDirAction,
                StickPadAction.DpadDirections.DownRight => propVm.UpdateDownRightDirAction,
                _ => null,
            };
        }

        private ButtonAction EnsureCircularButton(StickCircular circ, bool clockwise)
        {
            TouchpadCircularButton existing = clockwise ? circ.ClockWiseBtn : circ.CounterClockwiseBtn;
            bool usingParent = circ.UseParentCircButtons[clockwise ? 0 : 1];
            if (!usingParent && HasNormalPressFunc(existing)) return existing;

            TouchpadCircularButton newButton = new TouchpadCircularButton();
            if (existing != null)
            {
                newButton.CopyBaseProps(existing);
                newButton.CopyAction(existing);
            }
            EnsureRegularPressFunc(newButton);

            StickCircularPropViewModel propVm = settingsViewModel as StickCircularPropViewModel;
            if (clockwise)
            {
                propVm?.UpdateClockWiseBtn(existing, newButton);
            }
            else
            {
                propVm?.UpdateCounterClockWiseBtn(existing, newButton);
            }

            return newButton;
        }

        private ButtonAction EnsureAbsMouseRingButton(StickAbsMouse abs)
        {
            AxisDirButton existing = abs.RingButton;
            bool usingParent = abs.UseParentRingButton;
            if (!usingParent && HasNormalPressFunc(existing)) return existing;

            AxisDirButton newButton = new AxisDirButton();
            if (existing != null)
            {
                newButton.CopyBaseProps(existing);
                newButton.CopyAction(existing);
            }
            EnsureRegularPressFunc(newButton);

            StickAbsMousePropViewModel propVm = settingsViewModel as StickAbsMousePropViewModel;
            propVm?.UpdateRingButton(existing, newButton);
            return newButton;
        }

        private ButtonAction EnsureAnalogDirButton(StickAnalogEmulationAction analog, int idx)
        {
            AxisDirButton existing = analog.DirButtons[idx];
            bool usingParent = analog.UsingParentActionButton[idx];
            if (!usingParent && HasNormalPressFunc(existing)) return existing;

            AxisDirButton newButton = new AxisDirButton();
            if (existing != null)
            {
                newButton.CopyBaseProps(existing);
                newButton.CopyAction(existing);
            }
            EnsureRegularPressFunc(newButton);

            StickAnalogEmulationPropViewModel propVm = settingsViewModel as StickAnalogEmulationPropViewModel;
            Action<ButtonAction, ButtonAction> updater = GetAnalogUpdater(propVm, idx);
            updater?.Invoke(existing, newButton);
            return newButton;
        }

        private static Action<ButtonAction, ButtonAction> GetAnalogUpdater(StickAnalogEmulationPropViewModel propVm, int idx)
        {
            if (propVm == null) return null;
            return (StickAnalogEmulationAction.DirSlot)idx switch
            {
                StickAnalogEmulationAction.DirSlot.Up => propVm.UpdateUpDirAction,
                StickAnalogEmulationAction.DirSlot.Down => propVm.UpdateDownDirAction,
                StickAnalogEmulationAction.DirSlot.Left => propVm.UpdateLeftDirAction,
                StickAnalogEmulationAction.DirSlot.Right => propVm.UpdateRightDirAction,
                _ => null,
            };
        }

        private static bool TryAnalogDirIndex(string slotKey, out int index)
        {
            index = slotKey switch
            {
                "Up" => (int)StickAnalogEmulationAction.DirSlot.Up,
                "Down" => (int)StickAnalogEmulationAction.DirSlot.Down,
                "Left" => (int)StickAnalogEmulationAction.DirSlot.Left,
                "Right" => (int)StickAnalogEmulationAction.DirSlot.Right,
                _ => -1,
            };

            return index >= 0;
        }

        private static bool TryDirIndex(string slotKey, out int index)
        {
            index = slotKey switch
            {
                "Up" => (int)StickPadAction.DpadDirections.Up,
                "Down" => (int)StickPadAction.DpadDirections.Down,
                "Left" => (int)StickPadAction.DpadDirections.Left,
                "Right" => (int)StickPadAction.DpadDirections.Right,
                "UpLeft" => (int)StickPadAction.DpadDirections.UpLeft,
                "UpRight" => (int)StickPadAction.DpadDirections.UpRight,
                "DownLeft" => (int)StickPadAction.DpadDirections.DownLeft,
                "DownRight" => (int)StickPadAction.DpadDirections.DownRight,
                _ => -1,
            };

            return index >= 0;
        }

        private static bool HasNormalPressFunc(ButtonAction action)
        {
            return action != null && action.ActionFuncs.OfType<NormalPressFunc>().Any();
        }

        internal static void EnsureRegularPressFunc(ButtonAction action)
        {
            if (action.ActionFuncs.OfType<NormalPressFunc>().Any()) return;

            action.ActionFuncs.Insert(0, new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.Empty, 0)));
            MarkFunctionsChanged(action);
        }

        internal static void MarkFunctionsChanged(ButtonAction action)
        {
            if (action == null) return;
            if (!action.ChangedProperties.Contains(ButtonAction.PropertyKeyStrings.FUNCTIONS))
            {
                action.ChangedProperties.Add(ButtonAction.PropertyKeyStrings.FUNCTIONS);
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StickExtraBindingItem : INotifyPropertyChanged
    {
        private readonly StickSideViewModel owner;
        private readonly ObservableCollection<StickExtraFuncItem> functionItems =
            new ObservableCollection<StickExtraFuncItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public StickSideViewModel Owner => owner;
        public string SlotKey { get; }
        public string DisplayName { get; }
        public ObservableCollection<StickExtraFuncItem> FunctionItems => functionItems;

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

        public StickExtraBindingItem(StickSideViewModel owner, string slotKey, string displayName)
        {
            this.owner = owner;
            SlotKey = slotKey;
            DisplayName = displayName;

            RefreshFunctions();
        }

        public ButtonAction CurrentButtonAction() => owner.PeekExtraButtonAction(SlotKey);

        public void RefreshFunctions()
        {
            functionItems.Clear();

            ButtonAction buttonAction = CurrentButtonAction();
            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.Regular));

            if (buttonAction != null)
            {
                foreach (ActionFunc func in buttonAction.ActionFuncs)
                {
                    switch (func)
                    {
                        case HoldPressFunc:
                            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.Hold));
                            break;
                        case DoublePressFunc:
                            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.Double));
                            break;
                        case DistanceFunc:
                            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.Distance));
                            break;
                        case ChordedPressFunc:
                            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.Chorded));
                            break;
                        case SimPressFunc:
                            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.SimPress));
                            break;
                        case StartPressFunc:
                            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.Start));
                            break;
                        case ReleaseFunc:
                            functionItems.Add(new StickExtraFuncItem(this, FaceBindingFuncKind.Release));
                            break;
                    }
                }
            }

            RaiseAvailabilityChanged();
        }

        public StickExtraFuncItem AddExtraBinding(FaceBindingFuncKind kind)
        {
            if (kind == FaceBindingFuncKind.Regular || HasKind(kind)) return null;

            ButtonAction buttonAction = owner.EnsureEditableExtraButtonAction(SlotKey);
            if (buttonAction == null) return null;

            ActionFunc func = CreateFunc(kind);
            if (func == null) return null;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.Add(func);
                StickSideViewModel.MarkFunctionsChanged(buttonAction);
            });

            RefreshFunctions();
            return functionItems.FirstOrDefault(item => item.Kind == kind);
        }

        public void RemoveBinding(StickExtraFuncItem item)
        {
            if (item == null || item.Kind == FaceBindingFuncKind.Regular || item.Func == null) return;

            ButtonAction buttonAction = owner.EnsureEditableExtraButtonAction(SlotKey);
            if (buttonAction == null) return;

            int index = buttonAction.ActionFuncs.IndexOf(item.Func);
            if (index < 0) return;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.RemoveAt(index);
                StickSideViewModel.MarkFunctionsChanged(buttonAction);
            });

            RefreshFunctions();
        }

        public EditFaceBindingContext PrepareEdit(StickExtraFuncItem item)
        {
            if (item == null) return null;

            ButtonAction buttonAction = owner.EnsureEditableExtraButtonAction(SlotKey);
            if (buttonAction == null) return null;

            ActionFunc func = item.Func;
            if (func == null)
            {
                func = CreateFunc(item.Kind);
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    buttonAction.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    buttonAction.ActionFuncs.Add(func);
                    StickSideViewModel.MarkFunctionsChanged(buttonAction);
                });

                RefreshFunctions();
                item = functionItems.FirstOrDefault(temp => temp.Kind == item.Kind);
            }

            return new EditFaceBindingContext(owner.Owner.DeviceMapper, buttonAction, item?.Func ?? func);
        }

        public void RefreshAfterEdit() => RefreshFunctions();

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
            return CurrentButtonAction() is ButtonAction action &&
                action.ActionFuncs.OfType<TFunc>().Any();
        }

        private static ActionFunc CreateFunc(FaceBindingFuncKind kind)
        {
            OutputActionData emptyOutput = new OutputActionData(OutputActionData.ActionType.Empty, 0);

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

    public class StickExtraFuncItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly StickExtraBindingItem owner;
        private readonly ObservableCollection<ActionOutputItem> outputItems =
            new ObservableCollection<ActionOutputItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public StickExtraBindingItem Owner => owner;
        public FaceBindingFuncKind Kind { get; }
        public ObservableCollection<ActionOutputItem> OutputItems => outputItems;

        public ActionFunc Func => FindFunc(owner.CurrentButtonAction(), Kind);

        public bool IsExtraBinding => Kind != FaceBindingFuncKind.Regular && Func != null;
        public bool CanRemove => IsExtraBinding;
        public bool IsTurboEnabled => SupportsTurbo && TurboEnabled;
        public bool SupportsToggle => Func is NormalPressFunc || Func is HoldPressFunc || Func is DoublePressFunc || Func is StartPressFunc || Func is ReleaseFunc;
        public bool SupportsTurbo => Func is NormalPressFunc || Func is HoldPressFunc;
        public bool SupportsFireDelay => Func is NormalPressFunc;
        public bool SupportsHoldTime => Func is HoldPressFunc;
        public bool SupportsTapWindow => Func is DoublePressFunc;
        public bool SupportsReleaseOptions => Func is ReleaseFunc;
        public bool SupportsDistanceOptions => Func is DistanceFunc;
        public bool SupportsChordOptions => Func is ChordedPressFunc;
        public bool SupportsSimPressOptions => Func is SimPressFunc;

        public string DisplayName => Kind switch
        {
            FaceBindingFuncKind.Regular => "Regular Press",
            FaceBindingFuncKind.Hold => "Hold Press",
            FaceBindingFuncKind.Double => "Double Press",
            FaceBindingFuncKind.Distance => "Distance",
            FaceBindingFuncKind.Chorded => "Chorded Press",
            FaceBindingFuncKind.SimPress => "Simultaneous Press",
            FaceBindingFuncKind.Start => "Start Press",
            FaceBindingFuncKind.Release => "Release Press",
            _ => "Binding",
        };

        public string DisplayBind
        {
            get
            {
                string result = Func?.DescribeOutputActions(owner.Owner.Owner.DeviceMapper);
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

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    ActionFunc target = FindFunc(editable, Kind);
                    if (target == null) return;

                    target.toggleEnabled = value;
                    StickSideViewModel.MarkFunctionsChanged(editable);
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

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
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
                    StickSideViewModel.MarkFunctionsChanged(editable);
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

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
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
                    StickSideViewModel.MarkFunctionsChanged(editable);
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

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is NormalPressFunc normalPress)
                    {
                        normalPress.FireDelayMs = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
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

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is HoldPressFunc holdPress)
                    {
                        holdPress.DurationMs = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(HoldMs));
            }
        }

        public int TapWindowMs
        {
            get => Func is DoublePressFunc doublePress ? doublePress.DurationMs : 0;
            set
            {
                if (Func is not DoublePressFunc) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is DoublePressFunc doublePress)
                    {
                        doublePress.DurationMs = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(TapWindowMs));
            }
        }

        public string ReleaseDelayMs
        {
            get => Func is ReleaseFunc releaseFunc ? releaseFunc.DelayDurationMs.ToString() : "0";
            set
            {
                if (Func is not ReleaseFunc || !int.TryParse(value, out int temp)) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is ReleaseFunc releaseFunc)
                    {
                        releaseFunc.DelayDurationMs = temp;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(ReleaseDelayMs));
            }
        }

        public bool MaxHoldTimeEnabled
        {
            get => Func is ReleaseFunc releaseFunc && releaseFunc.MaxHoldTimeEnabled;
            set
            {
                if (Func is not ReleaseFunc currentReleaseFunc || currentReleaseFunc.MaxHoldTimeEnabled == value) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is ReleaseFunc releaseFunc)
                    {
                        releaseFunc.MaxHoldTimeEnabled = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(MaxHoldTimeEnabled));
            }
        }

        public string MaxHoldTimeMs
        {
            get => Func is ReleaseFunc releaseFunc ? releaseFunc.MaxHoldTimeMs.ToString() : "0";
            set
            {
                if (Func is not ReleaseFunc || !int.TryParse(value, out int temp)) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is ReleaseFunc releaseFunc)
                    {
                        releaseFunc.MaxHoldTimeMs = temp;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(MaxHoldTimeMs));
            }
        }

        public string DistanceName
        {
            get => Func is DistanceFunc distanceFunc ? distanceFunc.Name : "";
            set
            {
                if (Func is not DistanceFunc) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is DistanceFunc distanceFunc)
                    {
                        distanceFunc.Name = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(DistanceName));
            }
        }

        public double DistanceValue
        {
            get => Func is DistanceFunc distanceFunc ? distanceFunc.distance : 0.0;
            set
            {
                if (Func is not DistanceFunc currentDistanceFunc || double.IsNaN(value)) return;
                double clampedValue = Math.Clamp(value, 0.0, 1.0);
                if (currentDistanceFunc.distance == clampedValue) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is DistanceFunc distanceFunc)
                    {
                        distanceFunc.distance = clampedValue;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(DistanceValue));
            }
        }

        public List<ActionTriggerItem> ChordTriggerItems =>
            ChordedPressFuncUi.BuildTriggerItems(owner.Owner.Owner.DeviceMapper);

        public JoypadActionCodes ChordTrigger
        {
            get => Func is ChordedPressFunc chordedPress ? chordedPress.TriggerButton : JoypadActionCodes.Empty;
            set
            {
                if (Func is not ChordedPressFunc || ChordTrigger == value) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is ChordedPressFunc chordedPress)
                    {
                        chordedPress.TriggerButton = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(ChordTrigger));
            }
        }

        // Stick Action Pad directions have no JoypadActionCodes identity of their own (they
        // aren't in mapper.ActionTriggerItems), so unlike every other binding surface this
        // Sim Press pairing cannot be auto-mirrored onto its trigger button - it still works
        // standalone (the trigger button's own press is checked live), matching how Chorded
        // Press already behaves for this same binding.
        public List<ActionTriggerItem> SimPressTriggerItems =>
            ChordedPressFuncUi.BuildTriggerItems(owner.Owner.Owner.DeviceMapper);

        public JoypadActionCodes SimPressTrigger
        {
            get => Func is SimPressFunc simPress ? simPress.TriggerButton : JoypadActionCodes.Empty;
            set
            {
                if (Func is not SimPressFunc || SimPressTrigger == value) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is SimPressFunc simPress)
                    {
                        simPress.TriggerButton = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(SimPressTrigger));
            }
        }

        public int SimPressTimeMs
        {
            get => Func is SimPressFunc simPress ? simPress.SimPressTimeMs : SimPressFunc.DEFAULT_SIM_PRESS_MS;
            set
            {
                if (Func is not SimPressFunc) return;

                owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    ButtonAction editable = owner.Owner.EnsureEditableExtraButtonAction(owner.SlotKey);
                    if (FindFunc(editable, Kind) is SimPressFunc simPress)
                    {
                        simPress.SimPressTimeMs = value;
                    }
                    StickSideViewModel.MarkFunctionsChanged(editable);
                });
                OnPropertyChanged(nameof(SimPressTimeMs));
            }
        }

        public StickExtraFuncItem(StickExtraBindingItem owner, FaceBindingFuncKind kind)
        {
            this.owner = owner;
            Kind = kind;
            RefreshOutputItems();
        }

        public void AddOutputAction()
        {
            EditFaceBindingContext ctx = owner.PrepareEdit(this);
            if (ctx?.Func == null || ctx.Action == null) return;

            owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                StickSideViewModel.MarkFunctionsChanged(ctx.Action);
            });

            owner.RefreshAfterEdit();
        }

        public void RemoveOutputAction(ActionOutputItem item)
        {
            if (item == null) return;

            EditFaceBindingContext ctx = owner.PrepareEdit(this);
            if (ctx?.Func == null || ctx.Action == null) return;
            int index = item.Index;
            if (index <= 0 || index >= ctx.Func.OutputActions.Count) return;

            owner.Owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.RemoveAt(index);
                StickSideViewModel.MarkFunctionsChanged(ctx.Action);
            });

            owner.RefreshAfterEdit();
        }

        private EditFaceBindingContext PrepareEdit(ActionOutputItem item)
        {
            EditFaceBindingContext ctx = owner.PrepareEdit(this);
            if (ctx?.Func == null) return ctx;

            int index = item?.Index ?? 0;
            while (ctx.Func.OutputActions.Count <= index)
            {
                ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
            }

            return new EditFaceBindingContext(ctx.Mapper, ctx.Action, ctx.Func, index);
        }

        private void RefreshOutputItems()
        {
            outputItems.Clear();
            int count = Math.Max(1, Func?.OutputActions.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                outputItems.Add(new ActionOutputItem(this, i));
            }
        }

        // IQuickBindTarget
        Mapper IQuickBindTarget.Mapper => owner.Owner.Owner.DeviceMapper;
        string IQuickBindTarget.RowLabel => owner.DisplayName;
        string IQuickBindTarget.SlotLabel => DisplayName;
        bool IQuickBindTarget.IsComplexBinding => !QuickBindActionApplier.IsSimpleFunc(Func);
        EditFaceBindingContext IQuickBindTarget.GetEditContext() => owner.PrepareEdit(this);
        void IQuickBindTarget.NotifyBindingChanged() => owner.RefreshAfterEdit();

        Mapper IActionOutputListOwner.Mapper => owner.Owner.Owner.DeviceMapper;
        string IActionOutputListOwner.RowLabel => owner.DisplayName;
        string IActionOutputListOwner.SlotLabel => DisplayName;
        ActionFunc IActionOutputListOwner.Func => Func;
        EditFaceBindingContext IActionOutputListOwner.PrepareEdit(ActionOutputItem item) => PrepareEdit(item);
        void IActionOutputListOwner.AddOutputAction() => AddOutputAction();
        void IActionOutputListOwner.RemoveOutputAction(ActionOutputItem item) => RemoveOutputAction(item);
        void IActionOutputListOwner.NotifyBindingChanged() => owner.RefreshAfterEdit();

        private static ActionFunc FindFunc(ButtonAction action, FaceBindingFuncKind kind)
        {
            if (action == null) return null;

            return kind switch
            {
                FaceBindingFuncKind.Regular => action.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Hold => action.ActionFuncs.OfType<HoldPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Double => action.ActionFuncs.OfType<DoublePressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Distance => action.ActionFuncs.OfType<DistanceFunc>().FirstOrDefault(),
                FaceBindingFuncKind.Chorded => action.ActionFuncs.OfType<ChordedPressFunc>().FirstOrDefault(),
                FaceBindingFuncKind.SimPress => action.ActionFuncs.OfType<SimPressFunc>().FirstOrDefault(),
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
}
