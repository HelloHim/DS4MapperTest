using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.ViewModels.Common;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.StickActions;
using DS4MapperTest.ViewModels;
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

        private List<StickMouseDirectionBindItem> cardinalDirectionItems;
        public List<StickMouseDirectionBindItem> CardinalDirectionItems => cardinalDirectionItems;

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
            PrepareDirectionItems();
        }

        private void PrepareDirectionItems()
        {
            cardinalDirectionItems = new List<StickMouseDirectionBindItem>()
            {
                new StickMouseDirectionBindItem(this, StickMouse.DirSlot.Up, "Up", "Cardinal direction"),
                new StickMouseDirectionBindItem(this, StickMouse.DirSlot.Down, "Down", "Cardinal direction"),
                new StickMouseDirectionBindItem(this, StickMouse.DirSlot.Left, "Left", "Cardinal direction"),
                new StickMouseDirectionBindItem(this, StickMouse.DirSlot.Right, "Right", "Cardinal direction"),
            };
        }

        internal ButtonAction GetDirectionAction(StickMouse.DirSlot direction)
        {
            return action.DirButtons[(int)direction];
        }

        internal AxisDirButton EnsureEditableDirectionAction(StickMouse.DirSlot direction)
        {
            if (!usingRealAction)
            {
                ReplaceExistingLayerAction(this, EventArgs.Empty);
            }

            AxisDirButton dirAction = action.DirButtons[(int)direction];
            if (dirAction == null)
            {
                dirAction = new AxisDirButton(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                action.DirButtons[(int)direction] = dirAction;
            }

            MarkDirectionChanged(direction, dirAction);
            return dirAction;
        }

        internal void MarkDirectionChanged(StickMouse.DirSlot direction, ButtonAction dirAction)
        {
            string propertyName = GetDirectionPropertyName(direction);
            if (!action.ChangedProperties.Contains(propertyName))
            {
                action.ChangedProperties.Add(propertyName);
            }

            action.RaiseNotifyPropertyChange(mapper, propertyName);
            FaceButtonBindingItem.MarkFunctionsChanged(dirAction);
        }

        internal EditFaceBindingContext PrepareDirectionEdit(StickMouseDirectionBindItem item)
        {
            AxisDirButton dirAction = EnsureEditableDirectionAction(item.Direction);
            ActionFunc func = dirAction.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault();
            if (func == null)
            {
                func = new NormalPressFunc(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                mapper.ProcessMappingChangeAction(() =>
                {
                    dirAction.Release(mapper, ignoreReleaseActions: true);
                    dirAction.ActionFuncs.Insert(0, func);
                    MarkDirectionChanged(item.Direction, dirAction);
                });
            }

            return new EditFaceBindingContext(mapper, dirAction, func);
        }

        internal void RefreshDirectionBindings()
        {
            foreach (StickMouseDirectionBindItem item in cardinalDirectionItems)
            {
                item.Refresh();
            }
        }

        private static string GetDirectionPropertyName(StickMouse.DirSlot direction)
        {
            return direction switch
            {
                StickMouse.DirSlot.Up => StickMouse.PropertyKeyStrings.DIR_UP,
                StickMouse.DirSlot.Down => StickMouse.PropertyKeyStrings.DIR_DOWN,
                StickMouse.DirSlot.Left => StickMouse.PropertyKeyStrings.DIR_LEFT,
                StickMouse.DirSlot.Right => StickMouse.PropertyKeyStrings.DIR_RIGHT,
                _ => StickMouse.PropertyKeyStrings.DIR_UP,
            };
        }
    }

    public class StickMouseDirectionBindItem : INotifyPropertyChanged, IQuickBindTarget,
        IActionOutputListOwner
    {
        private readonly StickMousePropViewModel owner;

        public event PropertyChangedEventHandler PropertyChanged;

        public StickMouse.DirSlot Direction { get; }
        public string DisplayName { get; }
        public string Subtitle { get; }
        public ObservableCollection<ActionOutputItem> OutputItems { get; } =
            new ObservableCollection<ActionOutputItem>();

        public string DisplayBind
        {
            get
            {
                ButtonAction action = owner.GetDirectionAction(Direction);
                string result = action?.DescribeActions(((IQuickBindTarget)this).Mapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        public StickMouseDirectionBindItem(StickMousePropViewModel owner,
            StickMouse.DirSlot direction, string displayName, string subtitle)
        {
            this.owner = owner;
            Direction = direction;
            DisplayName = displayName;
            Subtitle = subtitle;
            RefreshOutputItems();
        }

        Mapper IQuickBindTarget.Mapper => owner.Mapper;
        string IQuickBindTarget.RowLabel => DisplayName;
        string IQuickBindTarget.SlotLabel => "Regular Press";
        bool IQuickBindTarget.IsComplexBinding =>
            !QuickBindActionApplier.IsSimpleFunc(
                owner.GetDirectionAction(Direction)?.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault());

        EditFaceBindingContext IQuickBindTarget.GetEditContext()
        {
            return owner.PrepareDirectionEdit(this);
        }

        void IQuickBindTarget.NotifyBindingChanged()
        {
            owner.MarkDirectionChanged(Direction, owner.GetDirectionAction(Direction));
            Refresh();
        }

        Mapper IActionOutputListOwner.Mapper => owner.Mapper;
        string IActionOutputListOwner.RowLabel => DisplayName;
        string IActionOutputListOwner.SlotLabel => "Regular Press";
        ActionFunc IActionOutputListOwner.Func => CurrentFunc;
        EditFaceBindingContext IActionOutputListOwner.PrepareEdit(ActionOutputItem item) => PrepareEdit(item);
        void IActionOutputListOwner.AddOutputAction() => AddOutputAction();
        void IActionOutputListOwner.RemoveOutputAction(ActionOutputItem item) => RemoveOutputAction(item);
        void IActionOutputListOwner.NotifyBindingChanged()
        {
            owner.MarkDirectionChanged(Direction, owner.GetDirectionAction(Direction));
            Refresh();
        }

        private ActionFunc CurrentFunc =>
            owner.GetDirectionAction(Direction)?.ActionFuncs.OfType<NormalPressFunc>().FirstOrDefault();

        public EditFaceBindingContext PrepareEdit(ActionOutputItem item)
        {
            EditFaceBindingContext ctx = owner.PrepareDirectionEdit(this);
            int index = item?.Index ?? 0;
            EnsureOutputSlot(ctx, index);
            return new EditFaceBindingContext(ctx.Mapper, ctx.Action, ctx.Func, index);
        }

        public void AddOutputAction()
        {
            EditFaceBindingContext ctx = owner.PrepareDirectionEdit(this);
            owner.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Mapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                owner.MarkDirectionChanged(Direction, ctx.Action);
            });

            RefreshOutputItems();
        }

        public void RemoveOutputAction(ActionOutputItem item)
        {
            if (item == null || item.Index <= 0)
            {
                return;
            }

            EditFaceBindingContext ctx = owner.PrepareDirectionEdit(this);
            if (item.Index >= ctx.Func.OutputActions.Count)
            {
                return;
            }

            owner.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Mapper, ignoreReleaseActions: true);
                ctx.Func.OutputActions.RemoveAt(item.Index);
                owner.MarkDirectionChanged(Direction, ctx.Action);
            });

            RefreshOutputItems();
        }

        private void EnsureOutputSlot(EditFaceBindingContext ctx, int index)
        {
            if (ctx.Func.OutputActions.Count > index)
            {
                return;
            }

            owner.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(owner.Mapper, ignoreReleaseActions: true);
                while (ctx.Func.OutputActions.Count <= index)
                {
                    ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                }

                owner.MarkDirectionChanged(Direction, ctx.Action);
            });
        }

        private void RefreshOutputItems()
        {
            OutputItems.Clear();
            int count = Math.Max(1, CurrentFunc?.OutputActions.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                OutputItems.Add(new ActionOutputItem(this, i));
            }
        }

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayBind)));
            RefreshOutputItems();
        }
    }
}
