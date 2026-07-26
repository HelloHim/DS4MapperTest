using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.TriggerActions;
using DS4MapperTest.ViewModels.Common;
using DS4MapperTest.ViewModels.TriggerActionPropViewModels;

namespace DS4MapperTest.ViewModels
{
    public enum TriggerBindingMode
    {
        NoAction,
        Button,
        DualStage,
        TriggerTranslate,
        TriggerMouse,
    }

    public class TriggerKeybindItem : INotifyPropertyChanged
    {
        private readonly ProfileEditorTestViewModel owner;
        private TriggerMapAction mappedAction;
        private readonly ObservableCollection<TriggerButtonFuncItem> functionItems =
            new ObservableCollection<TriggerButtonFuncItem>();
        private readonly TriggerDualStageBindItem fullPullBindItem;
        private readonly TriggerDualStageBindItem softPullBindItem;
        private readonly List<EnumChoiceSelection<TriggerBindingMode>> modeItems =
            new List<EnumChoiceSelection<TriggerBindingMode>>
            {
                new EnumChoiceSelection<TriggerBindingMode>("No Action", TriggerBindingMode.NoAction),
                new EnumChoiceSelection<TriggerBindingMode>("Button", TriggerBindingMode.Button),
                new EnumChoiceSelection<TriggerBindingMode>("Dual Stage", TriggerBindingMode.DualStage),
                new EnumChoiceSelection<TriggerBindingMode>("Trigger Translate", TriggerBindingMode.TriggerTranslate),
                new EnumChoiceSelection<TriggerBindingMode>("Trigger Mouse", TriggerBindingMode.TriggerMouse),
            };
        private readonly List<OutputTriggerItem> outputTriggerItems =
            new List<OutputTriggerItem>
            {
                new OutputTriggerItem("Unbound", JoypadActionCodes.Empty),
                new OutputTriggerItem("Left Trigger", JoypadActionCodes.X360_LT),
                new OutputTriggerItem("Right Trigger", JoypadActionCodes.X360_RT),
            };
        private readonly List<EnumChoiceSelection<MapAction.HapticsIntensity>> hapticsIntensityItems =
            new List<EnumChoiceSelection<MapAction.HapticsIntensity>>
            {
                new EnumChoiceSelection<MapAction.HapticsIntensity>("Off", MapAction.HapticsIntensity.Off),
                new EnumChoiceSelection<MapAction.HapticsIntensity>("Light", MapAction.HapticsIntensity.Light),
                new EnumChoiceSelection<MapAction.HapticsIntensity>("Medium", MapAction.HapticsIntensity.Medium),
                new EnumChoiceSelection<MapAction.HapticsIntensity>("Heavy", MapAction.HapticsIntensity.Heavy),
                new EnumChoiceSelection<MapAction.HapticsIntensity>("Full", MapAction.HapticsIntensity.Full),
            };

        public event PropertyChangedEventHandler PropertyChanged;

        public ProfileEditorTestViewModel Owner => owner;
        public string BindingName { get; }
        public string DisplayName { get; }
        public ObservableCollection<TriggerButtonFuncItem> FunctionItems => functionItems;
        public List<EnumChoiceSelection<TriggerBindingMode>> ModeItems => modeItems;
        public List<OutputTriggerItem> OutputTriggerItems => outputTriggerItems;
        public List<EnumChoiceSelection<MapAction.HapticsIntensity>> HapticsIntensityItems => hapticsIntensityItems;
        public TriggerDualStageBindItem FullPullBindItem => fullPullBindItem;
        public TriggerDualStageBindItem SoftPullBindItem => softPullBindItem;

        public TriggerMapAction MappedAction => mappedAction;
        public TriggerButtonAction ButtonAction => mappedAction as TriggerButtonAction;
        public TriggerDualStageAction DualStageAction => mappedAction as TriggerDualStageAction;
        public TriggerTranslate TranslateAction => mappedAction as TriggerTranslate;
        public TriggerMouse MouseAction => mappedAction as TriggerMouse;

        public bool IsButtonMode => mappedAction is TriggerButtonAction;
        public bool IsDualStageMode => mappedAction is TriggerDualStageAction;
        public bool IsTriggerTranslateMode => mappedAction is TriggerTranslate;
        public bool IsTriggerMouseMode => mappedAction is TriggerMouse;
        public bool IsNoActionMode => mappedAction is TriggerNoAction;
        public bool HasHoldPress => HasFunc<HoldPressFunc>();
        public bool HasDoublePress => HasFunc<DoublePressFunc>();
        public bool HasDistancePress => HasFunc<DistanceFunc>();
        public bool HasChordedPress => HasFunc<ChordedPressFunc>();
        public bool HasStartPress => HasFunc<StartPressFunc>();
        public bool HasReleasePress => HasFunc<ReleaseFunc>();
        public bool CanAddHoldPress => IsButtonMode && !HasHoldPress;
        public bool CanAddDoublePress => IsButtonMode && !HasDoublePress;
        public bool CanAddDistancePress => IsButtonMode && !HasDistancePress;
        public bool CanAddChordedPress => IsButtonMode && !HasChordedPress;
        public bool CanAddStartPress => IsButtonMode && !HasStartPress;
        public bool CanAddReleasePress => IsButtonMode && !HasReleasePress;

        public TriggerBindingMode CurrentMode
        {
            get
            {
                return mappedAction switch
                {
                    TriggerDualStageAction => TriggerBindingMode.DualStage,
                    TriggerTranslate => TriggerBindingMode.TriggerTranslate,
                    TriggerMouse => TriggerBindingMode.TriggerMouse,
                    TriggerNoAction => TriggerBindingMode.NoAction,
                    _ => TriggerBindingMode.Button,
                };
            }
            set
            {
                if (CurrentMode == value) return;

                TriggerMapAction newAction = value switch
                {
                    TriggerBindingMode.DualStage => new TriggerDualStageAction(),
                    TriggerBindingMode.TriggerTranslate => new TriggerTranslate(),
                    TriggerBindingMode.TriggerMouse => new TriggerMouse(),
                    TriggerBindingMode.NoAction => new TriggerNoAction(),
                    _ => new TriggerButtonAction(),
                };

                newAction.CopyBaseMapProps(mappedAction);
                newAction.Id = owner.GetNextTriggerActionId(mappedAction);

                if (newAction is TriggerMouse mouseAction)
                {
                    mouseAction.DirectionDegrees = TriggerMouse.DefaultDirectionForSide(mouseAction.TriggerDef.trigCode);
                }

                owner.UpdateTriggerKeybindAction(this, newAction);
            }
        }

        public string Name
        {
            get => mappedAction.Name;
            set
            {
                TriggerMapAction action = EnsureEditableAction();
                if (action.Name == value) return;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.Name = value;
                    MarkChanged(action, "Name");
                });
                RefreshAll();
            }
        }

        public double ButtonDeadZone
        {
            get => ButtonAction?.DeadZone.DeadZone ?? 0.0;
            set
            {
                if (ButtonAction == null) return;
                TriggerButtonAction action = EnsureEditableAction() as TriggerButtonAction;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.DeadZone.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    MarkChanged(action, TriggerButtonAction.PropertyKeyStrings.DEAD_ZONE);
                });
                OnPropertyChanged(nameof(ButtonDeadZone));
            }
        }

        public double DualDeadZone
        {
            get => DualStageAction?.DeadMod.DeadZone ?? 0.0;
            set => UpdateDualZone(value, TriggerDualStageAction.PropertyKeyStrings.DEAD_ZONE, nameof(DualDeadZone),
                action => action.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0));
        }

        public double DualAntiDeadZone
        {
            get => DualStageAction?.DeadMod.AntiDeadZone ?? 0.0;
            set => UpdateDualZone(value, TriggerDualStageAction.PropertyKeyStrings.ANTIDEAD_ZONE, nameof(DualAntiDeadZone),
                action => action.DeadMod.AntiDeadZone = Math.Clamp(value, 0.0, 1.0));
        }

        public double DualMaxZone
        {
            get => DualStageAction?.DeadMod.MaxZone ?? 0.0;
            set => UpdateDualZone(value, TriggerDualStageAction.PropertyKeyStrings.MAX_ZONE, nameof(DualMaxZone),
                action => action.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0));
        }

        public int HipFireDelay
        {
            get => DualStageAction?.HipFireMS ?? 0;
            set
            {
                if (DualStageAction == null) return;
                TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.HipFireMS = Math.Clamp(value, 0, 10000);
                    MarkChanged(action, TriggerDualStageAction.PropertyKeyStrings.HIPFIRE_DELAY);
                });
                OnPropertyChanged(nameof(HipFireDelay));
            }
        }

        public bool ForceHipFireDelay
        {
            get => DualStageAction?.ForceHipTime ?? false;
            set
            {
                if (DualStageAction == null) return;
                TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.ForceHipTime = value;
                    MarkChanged(action, TriggerDualStageAction.PropertyKeyStrings.FORCE_HIP_FIRE_TIME);
                });
                OnPropertyChanged(nameof(ForceHipFireDelay));
            }
        }

        public int SelectedDualStageModeIndex
        {
            get => DualStageAction == null ? 0 : (int)DualStageAction.TriggerStateMode;
            set
            {
                if (DualStageAction == null) return;
                TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.TriggerStateMode = (TriggerDualStageAction.DualStageMode)Math.Clamp(value, 0, 4);
                    MarkChanged(action, TriggerDualStageAction.PropertyKeyStrings.DUALSTAGE_MODE);
                });
                OnPropertyChanged(nameof(SelectedDualStageModeIndex));
            }
        }

        public MapAction.HapticsIntensity HapticsChoice
        {
            get => DualStageAction?.FullPullActionHapticsIntensity ?? MapAction.HapticsIntensity.Off;
            set
            {
                if (DualStageAction == null) return;
                TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.FullPullActionHapticsIntensity = value;
                    MarkChanged(action, TriggerDualStageAction.PropertyKeyStrings.FULL_PULL_HAPTICS_INTENSITY);
                });
                OnPropertyChanged(nameof(HapticsChoice));
            }
        }

        public MapAction.HapticsIntensity SoftPullHapticsChoice
        {
            get => DualStageAction?.SoftPullActionHapticsIntensity ?? MapAction.HapticsIntensity.Off;
            set
            {
                if (DualStageAction == null) return;
                TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.SoftPullActionHapticsIntensity = value;
                    MarkChanged(action, TriggerDualStageAction.PropertyKeyStrings.SOFT_PULL_HAPTICS_INTENSITY);
                });
                OnPropertyChanged(nameof(SoftPullHapticsChoice));
            }
        }

        public string FullPullDisplayBind => DualStageAction?.FullPullActButton.DescribeActions(owner.DeviceMapper) ?? "Unbound";
        public string SoftPullDisplayBind => DualStageAction?.SoftPullActButton.DescribeActions(owner.DeviceMapper) ?? "Unbound";

        public JoypadActionCodes OutputTrigger
        {
            get => TranslateAction?.OutputData.JoypadCode ?? JoypadActionCodes.Empty;
            set
            {
                if (TranslateAction == null) return;
                TriggerTranslate action = EnsureEditableAction() as TriggerTranslate;
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.OutputData.JoypadCode = value;
                    MarkChanged(action, TriggerTranslate.PropertyKeyStrings.OUTPUT_TRIGGER);
                });
                OnPropertyChanged(nameof(OutputTrigger));
            }
        }

        public double TranslateDeadZone
        {
            get => TranslateAction?.DeadMod.DeadZone ?? 0.0;
            set => UpdateTranslateZone(value, TriggerTranslate.PropertyKeyStrings.DEAD_ZONE, nameof(TranslateDeadZone),
                action => action.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0));
        }

        public double TranslateAntiDeadZone
        {
            get => TranslateAction?.DeadMod.AntiDeadZone ?? 0.0;
            set => UpdateTranslateZone(value, TriggerTranslate.PropertyKeyStrings.ANTIDEAD_ZONE, nameof(TranslateAntiDeadZone),
                action => action.DeadMod.AntiDeadZone = Math.Clamp(value, 0.0, 1.0));
        }

        public double TranslateMaxZone
        {
            get => TranslateAction?.DeadMod.MaxZone ?? 0.0;
            set => UpdateTranslateZone(value, TriggerTranslate.PropertyKeyStrings.MAX_ZONE, nameof(TranslateMaxZone),
                action => action.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0));
        }

        private readonly List<EnumChoiceSelection<StickOutCurve.Curve>> mouseOutputCurveChoiceItems =
            new List<EnumChoiceSelection<StickOutCurve.Curve>>
            {
                new EnumChoiceSelection<StickOutCurve.Curve>("Linear", StickOutCurve.Curve.Linear),
                new EnumChoiceSelection<StickOutCurve.Curve>("Enhanced Precision", StickOutCurve.Curve.EnhancedPrecision),
                new EnumChoiceSelection<StickOutCurve.Curve>("Quadratic", StickOutCurve.Curve.Quadratic),
                new EnumChoiceSelection<StickOutCurve.Curve>("Cubic", StickOutCurve.Curve.Cubic),
                new EnumChoiceSelection<StickOutCurve.Curve>("EaseOut Quadratic", StickOutCurve.Curve.EaseoutQuad),
                new EnumChoiceSelection<StickOutCurve.Curve>("EaseOut Cubic", StickOutCurve.Curve.EaseoutCubic),
            };
        public List<EnumChoiceSelection<StickOutCurve.Curve>> MouseOutputCurveChoiceItems => mouseOutputCurveChoiceItems;

        public double MouseDeadZone
        {
            get => MouseAction?.DeadMod.DeadZone ?? 0.0;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DEAD_ZONE, nameof(MouseDeadZone),
                action => action.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0));
        }

        public int MouseSpeed
        {
            get => MouseAction?.MouseSpeed ?? TriggerMouse.DefaultMouseSpeed;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.MOUSE_SPEED, nameof(MouseSpeed),
                action => action.MouseSpeed = value);
        }

        public StickOutCurve.Curve MouseOutputCurveChoice
        {
            get => MouseAction?.OutputCurve ?? StickOutCurve.Curve.Linear;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.OUTPUT_CURVE, nameof(MouseOutputCurveChoice),
                action => action.OutputCurve = value);
        }

        public double MouseDirectionDegrees
        {
            get => MouseAction?.DirectionDegrees ?? 0.0;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DIRECTION_DEGREES, nameof(MouseDirectionDegrees),
                action => action.DirectionDegrees = value);
        }

        public bool MouseDeltaEnabled
        {
            get => MouseAction?.MouseDeltaSettings.Enabled ?? false;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DELTA_SETTINGS, nameof(MouseDeltaEnabled),
                action => action.MouseDeltaSettings.Enabled = value);
        }

        public double MouseDeltaMultiplier
        {
            get => MouseAction?.MouseDeltaSettings.Multiplier ?? 4.0;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DELTA_SETTINGS, nameof(MouseDeltaMultiplier),
                action => action.MouseDeltaSettings.Multiplier = value);
        }

        public double MouseDeltaMinTravel
        {
            get => MouseAction?.MouseDeltaSettings.MinTravel ?? 0.01;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DELTA_SETTINGS, nameof(MouseDeltaMinTravel),
                action => action.MouseDeltaSettings.MinTravel = value);
        }

        public double MouseDeltaMaxTravel
        {
            get => MouseAction?.MouseDeltaSettings.MaxTravel ?? 0.2;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DELTA_SETTINGS, nameof(MouseDeltaMaxTravel),
                action => action.MouseDeltaSettings.MaxTravel = value);
        }

        public double MouseDeltaEasingDuration
        {
            get => MouseAction?.MouseDeltaSettings.EasingDuration ?? 0.2;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DELTA_SETTINGS, nameof(MouseDeltaEasingDuration),
                action => action.MouseDeltaSettings.EasingDuration = value);
        }

        public double MouseDeltaMinFactor
        {
            get => MouseAction?.MouseDeltaSettings.MinFactor ?? 1.0;
            set => UpdateMouseAction(TriggerMouse.PropertyKeyStrings.DELTA_SETTINGS, nameof(MouseDeltaMinFactor),
                action => action.MouseDeltaSettings.MinFactor = value);
        }

        public TriggerKeybindItem(ProfileEditorTestViewModel owner,
            TriggerBindingItemsTest sourceItem, string displayName)
        {
            this.owner = owner;
            BindingName = sourceItem.BindingName;
            DisplayName = displayName;
            mappedAction = sourceItem.MappedAction;
            fullPullBindItem = new TriggerDualStageBindItem(this, true);
            softPullBindItem = new TriggerDualStageBindItem(this, false);
            RefreshFunctions();
        }

        public void UpdateAction(TriggerMapAction action)
        {
            mappedAction = action;
            RefreshAll();
        }

        public TriggerButtonFuncItem AddExtraBinding(FaceBindingFuncKind kind)
        {
            if (!IsButtonMode || kind == FaceBindingFuncKind.Regular || HasKind(kind)) return null;

            TriggerButtonAction triggerAction = EnsureEditableAction() as TriggerButtonAction;
            AxisDirButton buttonAction = triggerAction.EventButton;
            ActionFunc func = CreateFunc(kind);
            if (func == null) return null;

            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.Add(func);
                MarkChanged(triggerAction, TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
                FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
            });

            RefreshFunctions();
            return functionItems.FirstOrDefault(item => item.Kind == kind);
        }

        public void RemoveBinding(TriggerButtonFuncItem item)
        {
            if (item == null || item.Kind == FaceBindingFuncKind.Regular || item.Func == null || !IsButtonMode) return;

            TriggerButtonAction triggerAction = EnsureEditableAction() as TriggerButtonAction;
            AxisDirButton buttonAction = triggerAction.EventButton;
            int index = buttonAction.ActionFuncs.IndexOf(item.Func);
            if (index < 0) return;

            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                buttonAction.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                buttonAction.ActionFuncs.RemoveAt(index);
                MarkChanged(triggerAction, TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
                FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
            });

            RefreshFunctions();
        }

        public EditTriggerButtonBindingContext PrepareEdit(TriggerButtonFuncItem item)
        {
            if (item == null || !IsButtonMode) return null;

            TriggerButtonAction triggerAction = EnsureEditableAction() as TriggerButtonAction;
            AxisDirButton buttonAction = triggerAction.EventButton;
            ActionFunc func = item.Func;

            if (func == null)
            {
                func = CreateFunc(item.Kind);
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    buttonAction.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                    buttonAction.ActionFuncs.Add(func);
                    MarkChanged(triggerAction, TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
                    FaceButtonBindingItem.MarkFunctionsChanged(buttonAction);
                });

                RefreshFunctions();
                item = functionItems.FirstOrDefault(temp => temp.Kind == item.Kind);
            }

            return new EditTriggerButtonBindingContext(owner.DeviceMapper, triggerAction, buttonAction, item?.Func ?? func);
        }

        public TriggerButtonEditContext PrepareButtonActionEdit()
        {
            if (!IsButtonMode) return null;
            TriggerButtonAction triggerAction = EnsureEditableAction() as TriggerButtonAction;
            return new TriggerButtonEditContext(triggerAction.EventButton, !triggerAction.UseParentEventButton,
                (oldAction, newAction) => UpdateButtonEventAction(triggerAction, oldAction, newAction));
        }

        internal TriggerButtonAction EnsureEditableButtonActionForFunctionEdits()
        {
            if (!IsButtonMode) return null;

            TriggerButtonAction action = EnsureEditableAction() as TriggerButtonAction;
            if (action.UseParentEventButton)
            {
                owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.EventButton = new AxisDirButton(action.EventButton);
                    action.UseParentEventButton = false;
                    MarkChanged(action, TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
                });
                RefreshFunctions();
            }

            return action;
        }

        internal ActionFunc FindButtonFunc(FaceBindingFuncKind kind)
        {
            AxisDirButton buttonAction = ButtonAction?.EventButton;
            if (buttonAction == null) return null;

            return kind switch
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
        }

        public TriggerButtonEditContext PrepareFullPullEdit()
        {
            if (!IsDualStageMode) return null;
            TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
            return new TriggerButtonEditContext(action.FullPullActButton, !action.UseParentFullPullBtn,
                (oldAction, newAction) => UpdateDualStageButton(action, oldAction, newAction,
                    TriggerDualStageAction.PropertyKeyStrings.FULLPULL_BUTTON, true));
        }

        public TriggerButtonEditContext PrepareSoftPullEdit()
        {
            if (!IsDualStageMode) return null;
            TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
            return new TriggerButtonEditContext(action.SoftPullActButton, !action.UseParentSoftPullBtn,
                (oldAction, newAction) => UpdateDualStageButton(action, oldAction, newAction,
                    TriggerDualStageAction.PropertyKeyStrings.SOFTPULL_BUTTON, false));
        }

        public void RefreshFunctions()
        {
            functionItems.Clear();

            AxisDirButton buttonAction = ButtonAction?.EventButton;
            ActionFunc regularFunc = buttonAction?.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault();
            functionItems.Add(new TriggerButtonFuncItem(this, FaceBindingFuncKind.Regular, regularFunc));

            if (buttonAction != null)
            {
                foreach (ActionFunc func in buttonAction.ActionFuncs)
                {
                    switch (func)
                    {
                        case HoldPressFunc:
                            functionItems.Add(new TriggerButtonFuncItem(this, FaceBindingFuncKind.Hold, func));
                            break;
                        case DoublePressFunc:
                            functionItems.Add(new TriggerButtonFuncItem(this, FaceBindingFuncKind.Double, func));
                            break;
                        case DistanceFunc:
                            functionItems.Add(new TriggerButtonFuncItem(this, FaceBindingFuncKind.Distance, func));
                            break;
                        case ChordedPressFunc:
                            functionItems.Add(new TriggerButtonFuncItem(this, FaceBindingFuncKind.Chorded, func));
                            break;
                        case StartPressFunc:
                            functionItems.Add(new TriggerButtonFuncItem(this, FaceBindingFuncKind.Start, func));
                            break;
                        case ReleaseFunc:
                            functionItems.Add(new TriggerButtonFuncItem(this, FaceBindingFuncKind.Release, func));
                            break;
                    }
                }
            }

            RaiseAvailabilityChanged();
        }

        public void RefreshAfterEdit()
        {
            RefreshAll();
        }

        private TriggerMapAction EnsureEditableAction()
        {
            TriggerMapAction action = owner.EnsureEditableTriggerAction(this);
            if (!ReferenceEquals(action, mappedAction))
            {
                mappedAction = action;
            }

            EnsureRegularPressFunc();
            return mappedAction;
        }

        private void EnsureRegularPressFunc()
        {
            if (mappedAction is not TriggerButtonAction buttonAction) return;
            if (buttonAction.EventButton.ActionFuncs.OfType<NormalPressFunc>().Any()) return;

            buttonAction.EventButton.ActionFuncs.Insert(0, new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.Empty, 0)));
            FaceButtonBindingItem.MarkFunctionsChanged(buttonAction.EventButton);
            MarkChanged(buttonAction, TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
        }

        private void UpdateButtonEventAction(TriggerButtonAction action, ButtonAction oldAction, ButtonAction newAction)
        {
            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                oldAction?.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                action.EventButton = newAction as AxisDirButton;
                action.UseParentEventButton = false;
                MarkChanged(action, TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
            });
            RefreshAll();
        }

        private void UpdateDualStageButton(TriggerDualStageAction action, ButtonAction oldAction,
            ButtonAction newAction, string propertyName, bool fullPull)
        {
            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                oldAction?.Release(owner.DeviceMapper, ignoreReleaseActions: true);
                if (fullPull)
                {
                    action.FullPullActButton = newAction as AxisDirButton;
                    action.UseParentFullPullBtn = false;
                }
                else
                {
                    action.SoftPullActButton = newAction as AxisDirButton;
                    action.UseParentSoftPullBtn = false;
                }

                MarkChanged(action, propertyName);
            });
            RefreshAll();
        }

        private void UpdateDualZone(double value, string propertyName, string notifyName,
            Action<TriggerDualStageAction> update)
        {
            if (DualStageAction == null) return;
            TriggerDualStageAction action = EnsureEditableAction() as TriggerDualStageAction;
            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                update(action);
                MarkChanged(action, propertyName);
            });
            OnPropertyChanged(notifyName);
        }

        private void UpdateTranslateZone(double value, string propertyName, string notifyName,
            Action<TriggerTranslate> update)
        {
            if (TranslateAction == null) return;
            TriggerTranslate action = EnsureEditableAction() as TriggerTranslate;
            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                update(action);
                MarkChanged(action, propertyName);
            });
            OnPropertyChanged(notifyName);
        }

        private void UpdateMouseAction(string propertyName, string notifyName,
            Action<TriggerMouse> update, string alsoNotify = null)
        {
            if (MouseAction == null) return;
            TriggerMouse action = EnsureEditableAction() as TriggerMouse;
            owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                update(action);
                MarkChanged(action, propertyName);
            });
            OnPropertyChanged(notifyName);
            if (alsoNotify != null) OnPropertyChanged(alsoNotify);
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
            return ButtonAction?.EventButton.ActionFuncs.OfType<TFunc>().Any() == true;
        }

        internal static ActionFunc CreateFunc(FaceBindingFuncKind kind)
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

        private void MarkChanged(TriggerMapAction action, string propertyName)
        {
            if (!action.ChangedProperties.Contains(propertyName))
            {
                action.ChangedProperties.Add(propertyName);
            }

            action.RaiseNotifyPropertyChange(owner.DeviceMapper, propertyName);
        }

        private void RefreshAll()
        {
            RefreshFunctions();
            OnPropertyChanged(nameof(MappedAction));
            OnPropertyChanged(nameof(CurrentMode));
            OnPropertyChanged(nameof(IsButtonMode));
            OnPropertyChanged(nameof(IsDualStageMode));
            OnPropertyChanged(nameof(IsTriggerTranslateMode));
            OnPropertyChanged(nameof(IsTriggerMouseMode));
            OnPropertyChanged(nameof(IsNoActionMode));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ButtonDeadZone));
            OnPropertyChanged(nameof(DualDeadZone));
            OnPropertyChanged(nameof(DualAntiDeadZone));
            OnPropertyChanged(nameof(DualMaxZone));
            OnPropertyChanged(nameof(HipFireDelay));
            OnPropertyChanged(nameof(ForceHipFireDelay));
            OnPropertyChanged(nameof(SelectedDualStageModeIndex));
            OnPropertyChanged(nameof(HapticsChoice));
            OnPropertyChanged(nameof(SoftPullHapticsChoice));
            OnPropertyChanged(nameof(FullPullDisplayBind));
            OnPropertyChanged(nameof(SoftPullDisplayBind));
            fullPullBindItem.Refresh();
            softPullBindItem.Refresh();
            OnPropertyChanged(nameof(OutputTrigger));
            OnPropertyChanged(nameof(TranslateDeadZone));
            OnPropertyChanged(nameof(TranslateAntiDeadZone));
            OnPropertyChanged(nameof(TranslateMaxZone));
            OnPropertyChanged(nameof(MouseDeadZone));
            OnPropertyChanged(nameof(MouseSpeed));
            OnPropertyChanged(nameof(MouseOutputCurveChoice));
            OnPropertyChanged(nameof(MouseDirectionDegrees));
            OnPropertyChanged(nameof(MouseDeltaEnabled));
            OnPropertyChanged(nameof(MouseDeltaMultiplier));
            OnPropertyChanged(nameof(MouseDeltaMinTravel));
            OnPropertyChanged(nameof(MouseDeltaMaxTravel));
            OnPropertyChanged(nameof(MouseDeltaEasingDuration));
            OnPropertyChanged(nameof(MouseDeltaMinFactor));
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

    public class TriggerDualStageBindItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly TriggerKeybindItem owner;
        private readonly bool fullPull;
        private readonly ObservableCollection<ActionOutputItem> outputItems =
            new ObservableCollection<ActionOutputItem>();
        private readonly ObservableCollection<TriggerDualStageFuncItem> functionItems =
            new ObservableCollection<TriggerDualStageFuncItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public TriggerDualStageBindItem(TriggerKeybindItem owner, bool fullPull)
        {
            this.owner = owner;
            this.fullPull = fullPull;
            RefreshOutputItems();
            RefreshFunctions();
        }

        public ObservableCollection<ActionOutputItem> OutputItems => outputItems;
        public ObservableCollection<TriggerDualStageFuncItem> FunctionItems => functionItems;
        internal Mapper Mapper => owner.Owner.DeviceMapper;
        internal string RowLabel => owner.DisplayName;
        public bool CanAddHoldPress => !HasFunc<HoldPressFunc>();
        public bool CanAddDoublePress => !HasFunc<DoublePressFunc>();
        public bool CanAddChordedPress => !HasFunc<ChordedPressFunc>();
        public bool CanAddStartPress => !HasFunc<StartPressFunc>();
        public bool CanAddReleasePress => !HasFunc<ReleaseFunc>();

        public string DisplayBind
        {
            get
            {
                AxisDirButton action = StageAction;
                string result = action?.DescribeActions(owner.Owner.DeviceMapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        private AxisDirButton StageAction => fullPull
            ? owner.DualStageAction?.FullPullActButton
            : owner.DualStageAction?.SoftPullActButton;

        private string PropertyName => fullPull
            ? TriggerDualStageAction.PropertyKeyStrings.FULLPULL_BUTTON
            : TriggerDualStageAction.PropertyKeyStrings.SOFTPULL_BUTTON;

        public ActionFunc Func => FindNormalPressFunc(StageAction);

        public string DisplayName => fullPull ? "Full Pull" : "Soft Pull";

        // IQuickBindTarget
        Mapper IQuickBindTarget.Mapper => owner.Owner.DeviceMapper;
        string IQuickBindTarget.RowLabel => owner.DisplayName;
        string IQuickBindTarget.SlotLabel => fullPull ? "Full Pull" : "Soft Pull";
        bool IQuickBindTarget.IsComplexBinding =>
            !QuickBindActionApplier.IsSimpleFunc(FindNormalPressFunc(StageAction));

        EditFaceBindingContext IQuickBindTarget.GetEditContext()
        {
            TriggerButtonEditContext ctx = fullPull
                ? owner.PrepareFullPullEdit()
                : owner.PrepareSoftPullEdit();
            if (ctx?.Action == null) return null;

            ActionFunc func = EnsureNormalPressFunc(ctx.Action);
            MarkStageChanged(ctx.Action);
            return new EditFaceBindingContext(owner.Owner.DeviceMapper, ctx.Action, func);
        }

        void IQuickBindTarget.NotifyBindingChanged()
        {
            MarkStageChanged(StageAction);
            owner.RefreshAfterEdit();
            Refresh();
        }

        public void AddOutputAction()
        {
            EditFaceBindingContext ctx = PrepareEdit(null);
            if (ctx?.Func == null || ctx.Action == null) return;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                MarkStageChanged(ctx.Action);
            });

            owner.RefreshAfterEdit();
            Refresh();
        }

        public void RemoveOutputAction(ActionOutputItem item)
        {
            if (item == null) return;

            EditFaceBindingContext ctx = PrepareEdit(item);
            if (ctx?.Func == null || ctx.Action == null) return;
            int index = item.Index;
            if (index <= 0 || index >= ctx.Func.OutputActions.Count) return;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.RemoveAt(index);
                MarkStageChanged(ctx.Action);
            });

            owner.RefreshAfterEdit();
            Refresh();
        }

        public void Refresh()
        {
            RefreshOutputItems();
            RefreshFunctions();
            OnPropertyChanged(nameof(DisplayBind));
            OnPropertyChanged(nameof(OutputItems));
            OnPropertyChanged(nameof(FunctionItems));
            OnPropertyChanged(nameof(CanAddHoldPress));
            OnPropertyChanged(nameof(CanAddDoublePress));
            OnPropertyChanged(nameof(CanAddChordedPress));
            OnPropertyChanged(nameof(CanAddStartPress));
            OnPropertyChanged(nameof(CanAddReleasePress));
        }

        public void AddExtraBinding(FaceBindingFuncKind kind)
        {
            if (kind == FaceBindingFuncKind.Regular || FindFunc(kind) != null) return;
            ActionFunc func = TriggerKeybindItem.CreateFunc(kind);
            AxisDirButton action = StageAction;
            if (func == null || action == null) return;
            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                action.ActionFuncs.Add(func);
                MarkStageChanged(action);
            });
            owner.RefreshAfterEdit();
        }

        public void RemoveBinding(TriggerDualStageFuncItem item)
        {
            if (item?.Func == null || item.Kind == FaceBindingFuncKind.Regular) return;
            AxisDirButton action = StageAction;
            if (action == null || !action.ActionFuncs.Contains(item.Func)) return;
            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                action.ActionFuncs.Remove(item.Func);
                MarkStageChanged(action);
            });
            owner.RefreshAfterEdit();
        }

        internal ActionFunc FindFunc(FaceBindingFuncKind kind) => kind switch
        {
            FaceBindingFuncKind.Regular => FindNormalPressFunc(StageAction),
            FaceBindingFuncKind.Hold => StageAction?.ActionFuncs.OfType<HoldPressFunc>().FirstOrDefault(),
            FaceBindingFuncKind.Double => StageAction?.ActionFuncs.OfType<DoublePressFunc>().FirstOrDefault(),
            FaceBindingFuncKind.Chorded => StageAction?.ActionFuncs.OfType<ChordedPressFunc>().FirstOrDefault(),
            FaceBindingFuncKind.Start => StageAction?.ActionFuncs.OfType<StartPressFunc>().FirstOrDefault(),
            FaceBindingFuncKind.Release => StageAction?.ActionFuncs.OfType<ReleaseFunc>().FirstOrDefault(),
            _ => null,
        };

        internal EditFaceBindingContext PrepareFunctionEdit(TriggerDualStageFuncItem item, int? outputIndex = null)
        {
            AxisDirButton action = StageAction;
            ActionFunc func = item?.Func ?? FindNormalPressFunc(action);
            if (action == null) return null;
            if (func == null && item?.Kind == FaceBindingFuncKind.Regular)
            {
                func = new NormalPressFunc(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    action.ActionFuncs.Insert(0, func);
                    MarkStageChanged(action);
                });
            }
            if (func == null) return null;
            if (outputIndex.HasValue)
            {
                while (func.OutputActions.Count <= outputIndex.Value)
                    func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
            }
            return new EditFaceBindingContext(owner.Owner.DeviceMapper, action, func, outputIndex);
        }

        internal void MarkFunctionChanged()
        {
            MarkStageChanged(StageAction);
            owner.RefreshAfterEdit();
        }

        private bool HasFunc<T>() where T : ActionFunc => StageAction?.ActionFuncs.OfType<T>().Any() == true;

        private void RefreshFunctions()
        {
            functionItems.Clear();
            functionItems.Add(new TriggerDualStageFuncItem(this, FaceBindingFuncKind.Regular,
                FindNormalPressFunc(StageAction)));
            foreach (ActionFunc func in StageAction?.ActionFuncs ?? Enumerable.Empty<ActionFunc>())
            {
                FaceBindingFuncKind? kind = func switch
                {
                    HoldPressFunc => FaceBindingFuncKind.Hold,
                    DoublePressFunc => FaceBindingFuncKind.Double,
                    ChordedPressFunc => FaceBindingFuncKind.Chorded,
                    StartPressFunc => FaceBindingFuncKind.Start,
                    ReleaseFunc => FaceBindingFuncKind.Release,
                    _ => null,
                };
                if (kind.HasValue) functionItems.Add(new TriggerDualStageFuncItem(this, kind.Value, func));
            }
        }

        private EditFaceBindingContext PrepareEdit(ActionOutputItem item)
        {
            TriggerButtonEditContext ctx = fullPull
                ? owner.PrepareFullPullEdit()
                : owner.PrepareSoftPullEdit();
            if (ctx?.Action == null) return null;

            ActionFunc func = EnsureNormalPressFunc(ctx.Action);
            int index = item?.Index ?? 0;
            while (func.OutputActions.Count <= index)
            {
                func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
            }

            MarkStageChanged(ctx.Action);
            return new EditFaceBindingContext(owner.Owner.DeviceMapper, ctx.Action, func, item == null ? null : index);
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

        Mapper IActionOutputListOwner.Mapper => owner.Owner.DeviceMapper;
        string IActionOutputListOwner.RowLabel => owner.DisplayName;
        string IActionOutputListOwner.SlotLabel => DisplayName;
        ActionFunc IActionOutputListOwner.Func => Func;
        EditFaceBindingContext IActionOutputListOwner.PrepareEdit(ActionOutputItem item) => PrepareEdit(item);
        void IActionOutputListOwner.AddOutputAction() => AddOutputAction();
        void IActionOutputListOwner.RemoveOutputAction(ActionOutputItem item) => RemoveOutputAction(item);
        void IActionOutputListOwner.NotifyBindingChanged()
        {
            MarkStageChanged(StageAction);
            owner.RefreshAfterEdit();
            Refresh();
        }

        private ActionFunc EnsureNormalPressFunc(AxisDirButton action)
        {
            ActionFunc func = FindNormalPressFunc(action);
            if (func != null) return func;

            func = new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.Empty, 0));
            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                action.ActionFuncs.Insert(0, func);
                FaceButtonBindingItem.MarkFunctionsChanged(action);
            });

            return func;
        }

        private static ActionFunc FindNormalPressFunc(AxisDirButton action)
        {
            return action?.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault();
        }

        private void MarkStageChanged(ButtonAction action)
        {
            if (owner.DualStageAction == null) return;

            if (!owner.DualStageAction.ChangedProperties.Contains(PropertyName))
            {
                owner.DualStageAction.ChangedProperties.Add(PropertyName);
            }

            owner.DualStageAction.RaiseNotifyPropertyChange(
                owner.Owner.DeviceMapper, PropertyName);
            FaceButtonBindingItem.MarkFunctionsChanged(action);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// An activator belonging to one logical side of a dual-stage trigger.  The
    /// stage remains an AxisDirButton, so this deliberately uses the same
    /// ActionFunc implementations as an ordinary button binding.
    /// </summary>
    public class TriggerDualStageFuncItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly TriggerDualStageBindItem owner;
        public FaceBindingFuncKind Kind { get; }
        public ActionFunc Func { get; }
        private readonly ObservableCollection<ActionOutputItem> outputItems = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public TriggerDualStageFuncItem(TriggerDualStageBindItem owner,
            FaceBindingFuncKind kind, ActionFunc func)
        {
            this.owner = owner;
            Kind = kind;
            Func = func;
            RefreshOutputs();
        }

        public ObservableCollection<ActionOutputItem> OutputItems => outputItems;
        public bool CanRemove => Kind != FaceBindingFuncKind.Regular && Func != null;
        public bool IsTurboEnabled => SupportsTurbo && TurboEnabled;
        public bool SupportsToggle => Func is NormalPressFunc || Func is HoldPressFunc ||
            Func is DoublePressFunc || Func is StartPressFunc || Func is ReleaseFunc;
        public bool SupportsTurbo => Func is NormalPressFunc || Func is HoldPressFunc;
        public bool SupportsFireDelay => Func is NormalPressFunc;
        public bool SupportsHoldTime => Func is HoldPressFunc;
        public bool SupportsTapWindow => Func is DoublePressFunc;
        public bool SupportsStartWindow => Func is StartPressFunc;
        public bool SupportsReleaseOptions => Func is ReleaseFunc;
        public bool SupportsChordOptions => Func is ChordedPressFunc;
        public string DisplayBind
        {
            get
            {
                string value = Func?.DescribeOutputActions(owner.Mapper);
                return string.IsNullOrWhiteSpace(value) ? "Unbound" : value;
            }
        }
        public string DisplayName => Kind switch
        {
            FaceBindingFuncKind.Regular => "Regular Press",
            FaceBindingFuncKind.Hold => "Hold Press",
            FaceBindingFuncKind.Double => "Double Press",
            FaceBindingFuncKind.Chorded => "Chorded Press",
            FaceBindingFuncKind.Start => "Start Press",
            FaceBindingFuncKind.Release => "Release Press",
            _ => "Binding",
        };

        private EditFaceBindingContext Context(ActionOutputItem item = null) =>
            owner.PrepareFunctionEdit(this, item?.Index);

        public bool ToggleEnabled
        {
            get => Func?.toggleEnabled ?? false;
            set => UpdateFunc(func => func.toggleEnabled = value, nameof(ToggleEnabled));
        }

        public bool TurboEnabled
        {
            get => Func switch
            {
                NormalPressFunc normal => normal.TurboEnabled,
                HoldPressFunc hold => hold.TurboEnabled,
                _ => false,
            };
            set => UpdateFunc(func =>
            {
                switch (func)
                {
                    case NormalPressFunc normal:
                        normal.TurboEnabled = value;
                        break;
                    case HoldPressFunc hold:
                        hold.TurboEnabled = value;
                        break;
                }
            }, nameof(TurboEnabled), nameof(IsTurboEnabled));
        }

        public int TurboDurationMs
        {
            get => Func switch
            {
                NormalPressFunc normal => normal.TurboDurationMs,
                HoldPressFunc hold => hold.TurboDurationMs,
                _ => 0,
            };
            set => UpdateFunc(func =>
            {
                switch (func)
                {
                    case NormalPressFunc normal:
                        normal.TurboDurationMs = value;
                        break;
                    case HoldPressFunc hold:
                        hold.TurboDurationMs = value;
                        break;
                }
            }, nameof(TurboDurationMs));
        }

        public int FireDelayMs
        {
            get => Func is NormalPressFunc normal ? normal.FireDelayMs : 0;
            set => UpdateFunc(func =>
            {
                if (func is NormalPressFunc normal) normal.FireDelayMs = value;
            }, nameof(FireDelayMs));
        }

        public int HoldMs
        {
            get => Func is HoldPressFunc hold ? hold.DurationMs : 0;
            set => UpdateFunc(func =>
            {
                if (func is HoldPressFunc hold) hold.DurationMs = value;
            }, nameof(HoldMs));
        }

        public int TapWindowMs
        {
            get => Func is DoublePressFunc doublePress ? doublePress.DurationMs : 0;
            set => UpdateFunc(func =>
            {
                if (func is DoublePressFunc doublePress) doublePress.DurationMs = value;
            }, nameof(TapWindowMs));
        }

        public int StartWindowMs
        {
            get => Func is StartPressFunc start ? start.DurationMs : 0;
            set => UpdateFunc(func =>
            {
                if (func is StartPressFunc start) start.DurationMs = value;
            }, nameof(StartWindowMs));
        }

        public int ReleaseDelayMs
        {
            get => Func is ReleaseFunc release ? release.DelayDurationMs : 0;
            set => UpdateFunc(func =>
            {
                if (func is ReleaseFunc release) release.DelayDurationMs = value;
            }, nameof(ReleaseDelayMs));
        }

        public bool MaxHoldTimeEnabled
        {
            get => Func is ReleaseFunc release && release.MaxHoldTimeEnabled;
            set => UpdateFunc(func =>
            {
                if (func is ReleaseFunc release) release.MaxHoldTimeEnabled = value;
            }, nameof(MaxHoldTimeEnabled));
        }

        public int MaxHoldTimeMs
        {
            get => Func is ReleaseFunc release ? release.MaxHoldTimeMs : 0;
            set => UpdateFunc(func =>
            {
                if (func is ReleaseFunc release) release.MaxHoldTimeMs = value;
            }, nameof(MaxHoldTimeMs));
        }

        public List<ActionTriggerItem> ChordTriggerItems =>
            ChordedPressFuncUi.BuildTriggerItems(owner.Mapper);

        public JoypadActionCodes ChordTrigger
        {
            get => Func is ChordedPressFunc chord ? chord.TriggerButton : JoypadActionCodes.Empty;
            set => UpdateFunc(func =>
            {
                if (func is ChordedPressFunc chord) chord.TriggerButton = value;
            }, nameof(ChordTrigger));
        }

        public void AddOutputAction()
        {
            EditFaceBindingContext ctx = Context();
            if (ctx?.Func == null) return;
            ctx.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(ctx.Mapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                owner.MarkFunctionChanged();
            });
        }

        public void Remove() => owner.RemoveBinding(this);

        public void RemoveOutputAction(ActionOutputItem item)
        {
            EditFaceBindingContext ctx = Context(item);
            if (ctx?.Func == null || item == null || item.Index <= 0 ||
                item.Index >= ctx.Func.OutputActions.Count) return;
            ctx.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(ctx.Mapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.RemoveAt(item.Index);
                owner.MarkFunctionChanged();
            });
        }

        private void RefreshOutputs()
        {
            outputItems.Clear();
            for (int i = 0; i < Math.Max(1, Func?.OutputActions.Count ?? 0); i++)
                outputItems.Add(new ActionOutputItem(this, i));
        }

        private void UpdateFunc(Action<ActionFunc> update, params string[] propertyNames)
        {
            EditFaceBindingContext ctx = Context();
            if (ctx?.Func == null) return;

            ctx.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(ctx.Mapper, ignoreReleaseActions: true);
                update(ctx.Func);
                owner.MarkFunctionChanged();
            });

            foreach (string propertyName in propertyNames)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        Mapper IQuickBindTarget.Mapper => ((IActionOutputListOwner)this).Mapper;
        string IQuickBindTarget.RowLabel => ((IActionOutputListOwner)this).RowLabel;
        string IQuickBindTarget.SlotLabel => DisplayName;
        bool IQuickBindTarget.IsComplexBinding => !QuickBindActionApplier.IsSimpleFunc(Func);
        EditFaceBindingContext IQuickBindTarget.GetEditContext() => Context();
        void IQuickBindTarget.NotifyBindingChanged() => owner.MarkFunctionChanged();

        Mapper IActionOutputListOwner.Mapper => owner.Mapper;
        string IActionOutputListOwner.RowLabel => owner.RowLabel;
        string IActionOutputListOwner.SlotLabel => DisplayName;
        ActionFunc IActionOutputListOwner.Func => Func;
        EditFaceBindingContext IActionOutputListOwner.PrepareEdit(ActionOutputItem item) => Context(item);
        void IActionOutputListOwner.AddOutputAction() => AddOutputAction();
        void IActionOutputListOwner.RemoveOutputAction(ActionOutputItem item) => RemoveOutputAction(item);
        void IActionOutputListOwner.NotifyBindingChanged() => owner.MarkFunctionChanged();
    }

    public class TriggerButtonFuncItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly TriggerKeybindItem owner;
        private readonly ActionFunc func;
        private readonly ObservableCollection<ActionOutputItem> outputItems =
            new ObservableCollection<ActionOutputItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public TriggerKeybindItem Owner => owner;
        public FaceBindingFuncKind Kind { get; }
        public ActionFunc Func => func;
        public ObservableCollection<ActionOutputItem> OutputItems => outputItems;
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

        public bool ToggleEnabled
        {
            get => func?.toggleEnabled ?? false;
            set
            {
                if (func == null || func.toggleEnabled == value) return;
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                ActionFunc editFunc = owner.FindButtonFunc(Kind) ?? func;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    editFunc.toggleEnabled = value;
                    MarkButtonChanged();
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
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                ActionFunc editFunc = owner.FindButtonFunc(Kind) ?? func;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    switch (editFunc)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboEnabled = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboEnabled = value;
                            break;
                    }
                    MarkButtonChanged();
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
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                ActionFunc editFunc = owner.FindButtonFunc(Kind) ?? func;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    switch (editFunc)
                    {
                        case NormalPressFunc normalPress:
                            normalPress.TurboDurationMs = value;
                            break;
                        case HoldPressFunc holdPress:
                            holdPress.TurboDurationMs = value;
                            break;
                    }
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(TurboDurationMs));
            }
        }

        public int FireDelayMs
        {
            get => func is NormalPressFunc normalPress ? normalPress.FireDelayMs : 0;
            set
            {
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if ((owner.FindButtonFunc(Kind) ?? func) is not NormalPressFunc normalPress) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    normalPress.FireDelayMs = value;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(FireDelayMs));
            }
        }

        public int HoldMs
        {
            get => func is HoldPressFunc holdPress ? holdPress.DurationMs : 0;
            set
            {
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if ((owner.FindButtonFunc(Kind) ?? func) is not HoldPressFunc holdPress) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    holdPress.DurationMs = value;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(HoldMs));
            }
        }

        public int TapWindowMs
        {
            get => func is DoublePressFunc doublePress ? doublePress.DurationMs : 0;
            set
            {
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if ((owner.FindButtonFunc(Kind) ?? func) is not DoublePressFunc doublePress) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    doublePress.DurationMs = value;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(TapWindowMs));
            }
        }

        public string ReleaseDelayMs
        {
            get => func is ReleaseFunc releaseFunc ? releaseFunc.DelayDurationMs.ToString() : "0";
            set
            {
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if ((owner.FindButtonFunc(Kind) ?? func) is not ReleaseFunc releaseFunc ||
                    !int.TryParse(value, out int temp)) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    releaseFunc.DelayDurationMs = temp;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(ReleaseDelayMs));
            }
        }

        public bool MaxHoldTimeEnabled
        {
            get => func is ReleaseFunc releaseFunc && releaseFunc.MaxHoldTimeEnabled;
            set
            {
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if ((owner.FindButtonFunc(Kind) ?? func) is not ReleaseFunc releaseFunc ||
                    releaseFunc.MaxHoldTimeEnabled == value) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    releaseFunc.MaxHoldTimeEnabled = value;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(MaxHoldTimeEnabled));
            }
        }

        public string MaxHoldTimeMs
        {
            get => func is ReleaseFunc releaseFunc ? releaseFunc.MaxHoldTimeMs.ToString() : "0";
            set
            {
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if ((owner.FindButtonFunc(Kind) ?? func) is not ReleaseFunc releaseFunc ||
                    !int.TryParse(value, out int temp)) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    releaseFunc.MaxHoldTimeMs = temp;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(MaxHoldTimeMs));
            }
        }

        public string DistanceName
        {
            get => func is DistanceFunc distanceFunc ? distanceFunc.Name : "";
            set
            {
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if ((owner.FindButtonFunc(Kind) ?? func) is not DistanceFunc distanceFunc) return;
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    distanceFunc.Name = value;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(DistanceName));
            }
        }

        public double DistanceValue
        {
            get => func is DistanceFunc distanceFunc ? distanceFunc.distance : 0.0;
            set
            {
                if (double.IsNaN(value)) return;
                ActionFunc currentFunc = owner.FindButtonFunc(Kind) ?? func;
                if (currentFunc is not DistanceFunc distanceFunc) return;
                double clampedValue = Math.Clamp(value, 0.0, 1.0);
                if (distanceFunc.distance == clampedValue) return;
                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    distanceFunc.distance = clampedValue;
                    MarkButtonChanged();
                });
                OnPropertyChanged(nameof(DistanceValue));
            }
        }

        public List<ActionTriggerItem> ChordTriggerItems =>
            ChordedPressFuncUi.BuildTriggerItems(owner.Owner.DeviceMapper);

        public JoypadActionCodes ChordTrigger
        {
            get => func is ChordedPressFunc chordedPress ? chordedPress.TriggerButton : JoypadActionCodes.Empty;
            set
            {
                // Check the item currently displayed by the selector before cloning an
                // inherited binding. EnsureEditableButtonActionForFunctionEdits can
                // rebuild FunctionItems; doing that first invalidates this item's WPF
                // binding while SelectedValue is still being updated.
                if (func is not ChordedPressFunc currentChord || currentChord.TriggerButton == value)
                    return;

                TriggerButtonAction triggerAction = owner.EnsureEditableButtonActionForFunctionEdits();
                if (owner.FindButtonFunc(Kind) is not ChordedPressFunc chordedPress ||
                    chordedPress.TriggerButton == value) return;

                owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
                {
                    triggerAction?.EventButton.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                    chordedPress.TriggerButton = value;
                    MarkButtonChanged();
                });

                // The editable action may be a newly detached copy of an inherited
                // action. Refresh after the transaction so WPF binds to that copy,
                // rather than notifying the old function item and re-entering the
                // ComboBox selection update.
                owner.RefreshAfterEdit();
            }
        }

        public TriggerButtonFuncItem(TriggerKeybindItem owner, FaceBindingFuncKind kind, ActionFunc func)
        {
            this.owner = owner;
            Kind = kind;
            this.func = func;
            RefreshOutputItems();
        }

        public void AddOutputAction()
        {
            EditTriggerButtonBindingContext ctx = owner.PrepareEdit(this);
            if (ctx?.Func == null || ctx.Action == null) return;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                MarkButtonChanged();
            });

            owner.RefreshAfterEdit();
        }

        public void RemoveOutputAction(ActionOutputItem item)
        {
            if (item == null) return;

            EditTriggerButtonBindingContext ctx = owner.PrepareEdit(this);
            if (ctx?.Func == null || ctx.Action == null) return;
            int index = item.Index;
            if (index <= 0 || index >= ctx.Func.OutputActions.Count) return;

            owner.Owner.DeviceMapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Owner.DeviceMapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.RemoveAt(index);
                MarkButtonChanged();
            });

            owner.RefreshAfterEdit();
        }

        private EditFaceBindingContext PrepareEdit(ActionOutputItem item)
        {
            EditTriggerButtonBindingContext ctx = owner.PrepareEdit(this);
            if (ctx?.Func == null) return null;

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
        EditFaceBindingContext IQuickBindTarget.GetEditContext()
        {
            EditTriggerButtonBindingContext ctx = owner.PrepareEdit(this);
            return ctx == null ? null : new EditFaceBindingContext(ctx.Mapper, ctx.Action, ctx.Func);
        }
        void IQuickBindTarget.NotifyBindingChanged() => owner.RefreshAfterEdit();

        Mapper IActionOutputListOwner.Mapper => owner.Owner.DeviceMapper;
        string IActionOutputListOwner.RowLabel => owner.DisplayName;
        string IActionOutputListOwner.SlotLabel => DisplayName;
        ActionFunc IActionOutputListOwner.Func => func;
        EditFaceBindingContext IActionOutputListOwner.PrepareEdit(ActionOutputItem item) => PrepareEdit(item);
        void IActionOutputListOwner.AddOutputAction() => AddOutputAction();
        void IActionOutputListOwner.RemoveOutputAction(ActionOutputItem item) => RemoveOutputAction(item);
        void IActionOutputListOwner.NotifyBindingChanged() => owner.RefreshAfterEdit();

        private void MarkButtonChanged()
        {
            if (owner.ButtonAction != null &&
                !owner.ButtonAction.ChangedProperties.Contains(TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING))
            {
                owner.ButtonAction.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
            }

            owner.ButtonAction?.RaiseNotifyPropertyChange(owner.Owner.DeviceMapper,
                TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
            FaceButtonBindingItem.MarkFunctionsChanged(owner.ButtonAction?.EventButton);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class EditTriggerButtonBindingContext
    {
        public Mapper Mapper { get; }
        public TriggerButtonAction TriggerAction { get; }
        public ButtonAction Action { get; }
        public ActionFunc Func { get; }

        public EditTriggerButtonBindingContext(Mapper mapper, TriggerButtonAction triggerAction,
            ButtonAction action, ActionFunc func)
        {
            Mapper = mapper;
            TriggerAction = triggerAction;
            Action = action;
            Func = func;
        }
    }

    public class TriggerButtonEditContext
    {
        public AxisDirButton Action { get; }
        public bool IsRealAction { get; }
        public Action<ButtonAction, ButtonAction> UpdateAction { get; }

        public TriggerButtonEditContext(AxisDirButton action, bool isRealAction,
            Action<ButtonAction, ButtonAction> updateAction)
        {
            Action = action;
            IsRealAction = isRealAction;
            UpdateAction = updateAction;
        }
    }
}
