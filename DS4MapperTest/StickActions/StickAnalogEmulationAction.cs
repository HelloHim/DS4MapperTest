using System;
using System.Collections.Generic;
using System.Linq;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickModifiers;

namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Emulates analogue stick movement through the existing four directional keyboard bindings.
    /// A primary direction is held continuously while an adjacent secondary direction is pulsed
    /// to approximate intermediate angles (Input Labs-style), with an optional secondary layer
    /// that gates overall movement activity to approximate analogue speed. See AnalogEmulationMath
    /// for the deterministic calculations driving both layers.
    /// </summary>
    public class StickAnalogEmulationAction : StickMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string DEAD_ZONE_TYPE = "DeadZoneType";
            public const string DEAD_ZONE = "DeadZone";
            public const string SEPARATE_AXIS_DEAD_ZONES = "SeparateAxisDeadZones";
            public const string DEAD_ZONE_X = "DeadZoneX";
            public const string DEAD_ZONE_Y = "DeadZoneY";
            public const string MAX_ZONE = "MaxZone";
            public const string ROTATION = "Rotation";

            public const string DIR_UP = "DirUp";
            public const string DIR_DOWN = "DirDown";
            public const string DIR_LEFT = "DirLeft";
            public const string DIR_RIGHT = "DirRight";

            public const string DIRECTION_MODE = "DirectionMode";
            public const string DIRECTION_PULSE_TIME_MS = "DirectionPulseTimeMs";
            public const string SPEED_ENABLED = "AnalogSpeedEmulationEnabled";
            public const string SPEED_ACTIVE_PERCENT = "AnalogEmulationActivePercent";
            public const string SPEED_PULSE_TIME_MS = "AnalogEmulationPulseTimeMs";
            public const string FULL_SPEED_THRESHOLD_PERCENT = "FullSpeedThresholdPercent";

            public const string COUNTER_MOVEMENT_ENABLED = "CounterMovementReleasePressEnabled";
            public const string COUNTER_MOVEMENT_USE_ARROW_KEYS = "UseArrowKeysForCounterMovementPresses";
            public const string COUNTER_MOVEMENT_TAP_LENGTH_PRESET = "CounterMovementTapLengthPreset";
            public const string COUNTER_MOVEMENT_TAP_LENGTH_MODE = "OppositeTapLengthMode";
            public const string COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS = "OppositeTapLengthMs";
            public const string COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT = "OppositeTapLengthVariancePercent";
            public const string COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS = "OppositeTapLengthMinimumMs";
            public const string COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS = "OppositeTapLengthMaximumMs";
            public const string COUNTER_MOVEMENT_START_DELAY_MIN_MS = "OppositeTapStartDelayMinimumMs";
            public const string COUNTER_MOVEMENT_START_DELAY_MAX_MS = "OppositeTapStartDelayMaximumMs";
            // Unchanged from the pre-rename "Digital Release Brake" feature: neither the
            // concept nor its serialised name changed, only the feature it belongs to.
            public const string BRAKE_MIN_HOLD_MS = "BrakeMinimumHoldMs";
            public const string BRAKE_ARMING_THRESHOLD = "BrakeArmingThreshold";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE_TYPE,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES,
            PropertyKeyStrings.DEAD_ZONE_X,
            PropertyKeyStrings.DEAD_ZONE_Y,
            PropertyKeyStrings.MAX_ZONE,
            PropertyKeyStrings.ROTATION,
            PropertyKeyStrings.DIR_UP,
            PropertyKeyStrings.DIR_DOWN,
            PropertyKeyStrings.DIR_LEFT,
            PropertyKeyStrings.DIR_RIGHT,
            PropertyKeyStrings.DIRECTION_MODE,
            PropertyKeyStrings.DIRECTION_PULSE_TIME_MS,
            PropertyKeyStrings.SPEED_ENABLED,
            PropertyKeyStrings.SPEED_ACTIVE_PERCENT,
            PropertyKeyStrings.SPEED_PULSE_TIME_MS,
            PropertyKeyStrings.FULL_SPEED_THRESHOLD_PERCENT,
            PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED,
            PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS,
            PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET,
            PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE,
            PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS,
            PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT,
            PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS,
            PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS,
            PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS,
            PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS,
            PropertyKeyStrings.BRAKE_MIN_HOLD_MS,
            PropertyKeyStrings.BRAKE_ARMING_THRESHOLD,
        };
        public HashSet<string> FullPropertySet => fullPropertySet;

        public const string ACTION_TYPE_NAME = "StickAnalogEmulationAction";

        public enum DirSlot : int
        {
            Up = 0,
            Down = 1,
            Left = 2,
            Right = 3,
        }

        public const int DEFAULT_DIRECTION_PULSE_MS = 30;
        public const int DEFAULT_SPEED_ACTIVE_PERCENT = 15;
        public const int DEFAULT_SPEED_PULSE_MS = 30;
        public const int DEFAULT_FULL_SPEED_THRESHOLD_PERCENT = 80;

        private AxisDirButton[] dirButtons = new AxisDirButton[4];
        public AxisDirButton[] DirButtons { get => dirButtons; set => dirButtons = value; }

        private StickDeadZone deadMod;
        public StickDeadZone DeadMod => deadMod;

        // + = Clockwise. - = Counter-clockwise
        private int rotation;
        public int Rotation
        {
            get => rotation;
            set => rotation = value;
        }

        // Shared with StickPadAction's Counter Movement Release Press. A 13-slot adapter
        // array keyed by StickPadAction.DpadDirections lets the same
        // CounterMovementReleasePressProcessor implementation pulse this action's four
        // AxisDirButtons; only the four cardinal indices are ever populated since Analog
        // Emulation has no dedicated diagonal buttons.
        private CounterMovementReleasePressProcessor counterMovementReleasePress = new CounterMovementReleasePressProcessor();
        public CounterMovementReleasePressProcessor CounterMovementReleasePress => counterMovementReleasePress;
        private AxisDirButton[] brakeSlotButtons = new AxisDirButton[13];

        private double xNorm, yNorm;

        private AnalogEmulationMath.ResolutionMode directionMode = AnalogEmulationMath.ResolutionMode.Continuous;
        public AnalogEmulationMath.ResolutionMode DirectionMode
        {
            get => directionMode;
            set => directionMode = value;
        }

        private int directionPulseTimeMs = DEFAULT_DIRECTION_PULSE_MS;
        public int DirectionPulseTimeMs
        {
            get => directionPulseTimeMs;
            set => directionPulseTimeMs = Math.Clamp(value, 1, 1000);
        }

        private bool speedEmulationEnabled;
        public bool SpeedEmulationEnabled
        {
            get => speedEmulationEnabled;
            set => speedEmulationEnabled = value;
        }

        private int speedActivePercent = DEFAULT_SPEED_ACTIVE_PERCENT;
        public int SpeedActivePercent
        {
            get => speedActivePercent;
            set => speedActivePercent = Math.Clamp(value, 0, 100);
        }

        private int speedPulseTimeMs = DEFAULT_SPEED_PULSE_MS;
        public int SpeedPulseTimeMs
        {
            get => speedPulseTimeMs;
            set => speedPulseTimeMs = Math.Clamp(value, 1, 1000);
        }

        private int fullSpeedThresholdPercent = DEFAULT_FULL_SPEED_THRESHOLD_PERCENT;
        public int FullSpeedThresholdPercent
        {
            get => fullSpeedThresholdPercent;
            set => fullSpeedThresholdPercent = Math.Clamp(value, 1, 100);
        }

        // Independent monotonic phase accumulators (ms), advanced only by validated mapper
        // report dt. Reset whenever the action (re)enters the dead zone or is released, so
        // reactivation always starts from a deterministic phase.
        private double directionPhaseMs;
        private double speedPhaseMs;
        private bool wasInSafeZone;

        private AnalogEmulationMath.Direction currentPrimary = AnalogEmulationMath.Direction.None;
        private AnalogEmulationMath.Direction currentSecondary = AnalogEmulationMath.Direction.None;
        private double currentSecondaryBlend;
        private bool currentSpeedGateOn;
        private bool currentDirectionGateOn;
        private bool[] slotOn = new bool[4];

        protected StickAnalogEmulationAction parentAnalogAction;
        protected bool useParentActions;
        protected bool[] useParentDataDraft = new bool[4];
        public bool[] UsingParentActionButton => useParentDataDraft;

        public StickAnalogEmulationAction()
        {
            actionTypeName = ACTION_TYPE_NAME;
            stickDefinition = new StickDefinition(new StickDefinition.StickAxisData(),
                new StickDefinition.StickAxisData(), StickActionCodes.Empty);
            deadMod = new StickDeadZone(0.30, 1.0, 0.0);
            deadMod.DeadZoneType = StickDeadZone.DeadZoneTypes.Radial;
            FillDirectionButtons();
        }

        public StickAnalogEmulationAction(StickDefinition stickDefinition)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.stickDefinition = stickDefinition;
            deadMod = new StickDeadZone(0.30, 1.0, 0.0);
            deadMod.DeadZoneType = StickDeadZone.DeadZoneTypes.Radial;
            FillDirectionButtons();
        }

        public StickAnalogEmulationAction(StickAnalogEmulationAction parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            if (parentAction != null)
            {
                this.parentAction = parentAction;
                parentAction.hasLayeredAction = true;
                parentAnalogAction = parentAction;
                this.stickDefinition = new StickDefinition(parentAction.stickDefinition);
                this.deadMod = new StickDeadZone(parentAction.deadMod);
                mappingId = parentAction.mappingId;
                useParentActions = true;

                for (int i = 0; i < dirButtons.Length; i++)
                {
                    AxisDirButton srcBtn = parentAction.dirButtons[i];
                    dirButtons[i] = srcBtn != null ? (AxisDirButton)srcBtn.DuplicateAction() : null;
                    useParentDataDraft[i] = true;
                }

                rotation = parentAction.rotation;

                directionMode = parentAction.directionMode;
                directionPulseTimeMs = parentAction.directionPulseTimeMs;
                speedEmulationEnabled = parentAction.speedEmulationEnabled;
                speedActivePercent = parentAction.speedActivePercent;
                speedPulseTimeMs = parentAction.speedPulseTimeMs;
                fullSpeedThresholdPercent = parentAction.fullSpeedThresholdPercent;

                counterMovementReleasePress.Enabled = parentAction.counterMovementReleasePress.Enabled;
                counterMovementReleasePress.UseArrowKeysForCounterMovementPresses = parentAction.counterMovementReleasePress.UseArrowKeysForCounterMovementPresses;
                counterMovementReleasePress.TapLengthPreset = parentAction.counterMovementReleasePress.TapLengthPreset;
                counterMovementReleasePress.OppositeTapLengthMinimumMs = parentAction.counterMovementReleasePress.OppositeTapLengthMinimumMs;
                counterMovementReleasePress.OppositeTapLengthMaximumMs = parentAction.counterMovementReleasePress.OppositeTapLengthMaximumMs;
                counterMovementReleasePress.OppositeTapStartDelayMinimumMs = parentAction.counterMovementReleasePress.OppositeTapStartDelayMinimumMs;
                counterMovementReleasePress.OppositeTapStartDelayMaximumMs = parentAction.counterMovementReleasePress.OppositeTapStartDelayMaximumMs;
                counterMovementReleasePress.MinimumHoldMs = parentAction.counterMovementReleasePress.MinimumHoldMs;
                counterMovementReleasePress.ArmingThreshold = parentAction.counterMovementReleasePress.ArmingThreshold;
            }
            else
            {
                deadMod = new StickDeadZone(0.30, 1.0, 0.0);
                deadMod.DeadZoneType = StickDeadZone.DeadZoneTypes.Radial;
                FillDirectionButtons();
            }
        }

        private void FillDirectionButtons()
        {
            AxisDirButton.AxisDirection[] axisDirs =
            {
                AxisDirButton.AxisDirection.YNeg, // Up
                AxisDirButton.AxisDirection.YPos, // Down
                AxisDirButton.AxisDirection.XNeg, // Left
                AxisDirButton.AxisDirection.XPos, // Right
            };

            for (int i = 0; i < dirButtons.Length; i++)
            {
                AxisDirButton tempBtn = new AxisDirButton();
                tempBtn.Direction = axisDirs[i];
                dirButtons[i] = tempBtn;
            }
        }

        private static int SlotIndex(AnalogEmulationMath.Direction dir)
        {
            switch (dir)
            {
                case AnalogEmulationMath.Direction.Up: return (int)DirSlot.Up;
                case AnalogEmulationMath.Direction.Down: return (int)DirSlot.Down;
                case AnalogEmulationMath.Direction.Left: return (int)DirSlot.Left;
                case AnalogEmulationMath.Direction.Right: return (int)DirSlot.Right;
                default: return -1;
            }
        }

        private static StickPadAction.DpadDirections ToDpadBit(AnalogEmulationMath.Direction dir)
        {
            switch (dir)
            {
                case AnalogEmulationMath.Direction.Up: return StickPadAction.DpadDirections.Up;
                case AnalogEmulationMath.Direction.Down: return StickPadAction.DpadDirections.Down;
                case AnalogEmulationMath.Direction.Left: return StickPadAction.DpadDirections.Left;
                case AnalogEmulationMath.Direction.Right: return StickPadAction.DpadDirections.Right;
                default: return StickPadAction.DpadDirections.Centered;
            }
        }

        private void RefreshBrakeSlotButtons()
        {
            brakeSlotButtons[(int)StickPadAction.DpadDirections.Up] = dirButtons[(int)DirSlot.Up];
            brakeSlotButtons[(int)StickPadAction.DpadDirections.Down] = dirButtons[(int)DirSlot.Down];
            brakeSlotButtons[(int)StickPadAction.DpadDirections.Left] = dirButtons[(int)DirSlot.Left];
            brakeSlotButtons[(int)StickPadAction.DpadDirections.Right] = dirButtons[(int)DirSlot.Right];
        }

        public override void Prepare(Mapper mapper, int axisXVal, int axisYVal, bool alterState = true)
        {
            xNorm = 0.0; yNorm = 0.0;

            if (rotation != 0)
            {
                StickMethods.RotatedCoordinates(rotation, axisXVal, axisYVal,
                    stickDefinition, out axisXVal, out axisYVal);
            }

            int axisXMid = stickDefinition.xAxis.mid, axisYMid = stickDefinition.yAxis.mid;
            int axisXDir = axisXVal - axisXMid, axisYDir = axisYVal - axisYMid;
            bool xNegative = axisXDir < 0;
            bool yNegative = axisYDir < 0;
            int maxDirX = (!xNegative ? stickDefinition.xAxis.max : stickDefinition.xAxis.min) - axisXMid;
            int maxDirY = (!yNegative ? stickDefinition.yAxis.max : stickDefinition.yAxis.min) - axisYMid;

            deadMod.CalcOutValues(axisXDir, axisYDir, maxDirX, maxDirY, out xNorm, out yNorm);
            bool inSafeZone = deadMod.inSafeZone;

            if (!inSafeZone)
            {
                if (wasInSafeZone)
                {
                    directionPhaseMs = 0.0;
                    speedPhaseMs = 0.0;
                }

                currentPrimary = AnalogEmulationMath.Direction.None;
                currentSecondary = AnalogEmulationMath.Direction.None;
                currentSecondaryBlend = 0.0;
                currentSpeedGateOn = false;
                currentDirectionGateOn = false;
            }
            else
            {
                if (!wasInSafeZone)
                {
                    directionPhaseMs = 0.0;
                    speedPhaseMs = 0.0;
                }

                double dt = mapper.CurrentLatency;
                bool dtValid = dt > 0.0 && dt <= 0.5;
                double dtMs = dtValid ? dt * 1000.0 : 0.0;

                AnalogEmulationMath.ComputeDirectionBlend(xNorm, yNorm, directionMode,
                    out currentPrimary, out currentSecondary, out currentSecondaryBlend);

                directionPhaseMs += dtMs;
                currentDirectionGateOn = AnalogEmulationMath.ComputeDutyGate(
                    directionPhaseMs, directionPulseTimeMs, currentSecondaryBlend);

                if (speedEmulationEnabled)
                {
                    // Post-deadzone normalised radius; matches the existing StickPadAction outer-ring
                    // magnitude calculation (sqrt of the per-axis normalised components).
                    double radius = Math.Sqrt((xNorm * xNorm) + (yNorm * yNorm));
                    double speedActive = AnalogEmulationMath.ComputeSpeedActive(
                        radius, speedActivePercent / 100.0, fullSpeedThresholdPercent / 100.0);

                    speedPhaseMs += dtMs;
                    currentSpeedGateOn = AnalogEmulationMath.ComputeDutyGate(
                        speedPhaseMs, speedPulseTimeMs, speedActive);
                }
                else
                {
                    currentSpeedGateOn = true;
                    speedPhaseMs = 0.0;
                }
            }

            wasInSafeZone = inSafeZone;

            // Digital Release Brake: derive an independent raw 8-way digital bucket purely for
            // spring snap-back detection, kept separate from the selected Direction Resolution's
            // blend rounding so the brake behaves consistently regardless of mode. Only whichever
            // cardinal bit(s) the brake actually removes from that bucket this tick are then
            // masked out of the smooth primary/secondary emission below.
            RefreshBrakeSlotButtons();
            AnalogEmulationMath.ComputeDirectionBlend(xNorm, yNorm, AnalogEmulationMath.ResolutionMode.EightWay,
                out AnalogEmulationMath.Direction rawPrimary, out AnalogEmulationMath.Direction rawSecondary, out double rawBlend);
            StickPadAction.DpadDirections rawDpadDir = ToDpadBit(rawPrimary);
            if (rawBlend >= 1.0) rawDpadDir |= ToDpadBit(rawSecondary);

            StickPadAction.DpadDirections effectiveDpadDir =
                counterMovementReleasePress.Prepare(mapper, axisXDir, axisYDir, maxDirX, maxDirY, rawDpadDir);
            StickPadAction.DpadDirections suppressedThisTick = rawDpadDir & ~effectiveDpadDir;

            bool primarySuppressed = (suppressedThisTick & ToDpadBit(currentPrimary)) != 0;
            bool secondarySuppressed = (suppressedThisTick & ToDpadBit(currentSecondary)) != 0;

            for (int i = 0; i < slotOn.Length; i++) slotOn[i] = false;

            int primaryIdx = SlotIndex(currentPrimary);
            if (primaryIdx >= 0 && !primarySuppressed) slotOn[primaryIdx] = currentSpeedGateOn;

            int secondaryIdx = SlotIndex(currentSecondary);
            if (secondaryIdx >= 0 && !secondarySuppressed) slotOn[secondaryIdx] = currentSpeedGateOn && currentDirectionGateOn;

            active = true;
            activeEvent = true;
        }

        public override void Event(Mapper mapper)
        {
            bool anyActive = false;

            for (int i = 0; i < dirButtons.Length; i++)
            {
                AxisDirButton btn = dirButtons[i];
                if (btn == null) continue;

                double val = slotOn[i] ? 1.0 : 0.0;
                btn.PrepareAnalog(mapper, val, val);
                btn.Event(mapper);

                if (btn.active) anyActive = true;
            }

            counterMovementReleasePress.Event(mapper, brakeSlotButtons);

            active = anyActive;
            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            RefreshBrakeSlotButtons();
            counterMovementReleasePress.Cleanup(mapper, brakeSlotButtons);

            for (int i = 0; i < dirButtons.Length; i++)
            {
                AxisDirButton btn = dirButtons[i];
                if (btn == null) continue;

                btn.PrepareAnalog(mapper, 0.0, 0.0);
                btn.Event(mapper);
                btn.Release(mapper, resetState, ignoreReleaseActions);
            }

            ResetRuntimeState();

            if (resetState)
            {
                stateData.Reset();
            }
        }

        public override void SoftRelease(Mapper mapper, MapAction checkAction, bool resetState = true)
        {
            StickAnalogEmulationAction checkAnalog = checkAction as StickAnalogEmulationAction;

            RefreshBrakeSlotButtons();
            counterMovementReleasePress.Cleanup(mapper, brakeSlotButtons);

            for (int i = 0; i < dirButtons.Length; i++)
            {
                AxisDirButton btn = dirButtons[i];
                if (btn == null) continue;

                bool sharedWithCheckAction = useParentDataDraft[i] && checkAnalog != null &&
                    checkAnalog.dirButtons[i] == btn;
                if (!sharedWithCheckAction)
                {
                    btn.PrepareAnalog(mapper, 0.0, 0.0);
                    btn.Event(mapper);
                    btn.Release(mapper, resetState);
                }
            }

            ResetRuntimeState();

            if (resetState)
            {
                stateData.Reset();
            }
        }

        private void ResetRuntimeState()
        {
            directionPhaseMs = 0.0;
            speedPhaseMs = 0.0;
            wasInSafeZone = false;
            currentPrimary = AnalogEmulationMath.Direction.None;
            currentSecondary = AnalogEmulationMath.Direction.None;
            currentSecondaryBlend = 0.0;
            currentSpeedGateOn = false;
            currentDirectionGateOn = false;
            for (int i = 0; i < slotOn.Length; i++) slotOn[i] = false;
            active = false;
        }

        public override StickMapAction DuplicateAction()
        {
            return new StickAnalogEmulationAction(this);
        }

        public override void SoftCopyFromParent(StickMapAction parentAction)
        {
            if (parentAction is StickAnalogEmulationAction tempAction)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                tempAction.hasLayeredAction = true;
                parentAnalogAction = tempAction;

                this.stickDefinition = new StickDefinition(tempAction.stickDefinition);
                mappingId = tempAction.mappingId;
                useParentActions = true;

                tempAction.NotifyPropertyChanged += TempAction_NotifyPropertyChanged;

                IEnumerable<string> useParentProList = fullPropertySet.Except(changedProperties);
                foreach (string parentPropType in useParentProList)
                {
                    ApplyParentProperty(parentPropType, tempAction);
                }
            }
        }

        private void TempAction_NotifyPropertyChanged(object sender, NotifyPropertyChangeArgs e)
        {
            CascadePropertyChange(e.Mapper, e.PropertyName);
        }

        protected override void CascadePropertyChange(Mapper mapper, string propertyName)
        {
            if (changedProperties.Contains(propertyName))
            {
                return;
            }

            if (parentAction is not StickAnalogEmulationAction tempAction)
            {
                return;
            }

            ApplyParentProperty(propertyName, tempAction);
        }

        private void ApplyParentProperty(string key, StickAnalogEmulationAction tempAction)
        {
            switch (key)
            {
                case PropertyKeyStrings.NAME:
                    name = tempAction.name;
                    break;
                case PropertyKeyStrings.DEAD_ZONE_TYPE:
                    deadMod.DeadZoneType = tempAction.deadMod.DeadZoneType;
                    break;
                case PropertyKeyStrings.DEAD_ZONE:
                    deadMod.DeadZone = tempAction.deadMod.DeadZone;
                    break;
                case PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES:
                    deadMod.SeparateAxisDeadZones = tempAction.deadMod.SeparateAxisDeadZones;
                    break;
                case PropertyKeyStrings.DEAD_ZONE_X:
                    deadMod.DeadZoneX = tempAction.deadMod.DeadZoneX;
                    break;
                case PropertyKeyStrings.DEAD_ZONE_Y:
                    deadMod.DeadZoneY = tempAction.deadMod.DeadZoneY;
                    break;
                case PropertyKeyStrings.MAX_ZONE:
                    deadMod.MaxZone = tempAction.deadMod.MaxZone;
                    break;
                case PropertyKeyStrings.ROTATION:
                    rotation = tempAction.rotation;
                    break;
                case PropertyKeyStrings.DIR_UP:
                    CopyDirButton((int)DirSlot.Up, tempAction);
                    break;
                case PropertyKeyStrings.DIR_DOWN:
                    CopyDirButton((int)DirSlot.Down, tempAction);
                    break;
                case PropertyKeyStrings.DIR_LEFT:
                    CopyDirButton((int)DirSlot.Left, tempAction);
                    break;
                case PropertyKeyStrings.DIR_RIGHT:
                    CopyDirButton((int)DirSlot.Right, tempAction);
                    break;
                case PropertyKeyStrings.DIRECTION_MODE:
                    directionMode = tempAction.directionMode;
                    break;
                case PropertyKeyStrings.DIRECTION_PULSE_TIME_MS:
                    directionPulseTimeMs = tempAction.directionPulseTimeMs;
                    break;
                case PropertyKeyStrings.SPEED_ENABLED:
                    speedEmulationEnabled = tempAction.speedEmulationEnabled;
                    break;
                case PropertyKeyStrings.SPEED_ACTIVE_PERCENT:
                    speedActivePercent = tempAction.speedActivePercent;
                    break;
                case PropertyKeyStrings.SPEED_PULSE_TIME_MS:
                    speedPulseTimeMs = tempAction.speedPulseTimeMs;
                    break;
                case PropertyKeyStrings.FULL_SPEED_THRESHOLD_PERCENT:
                    fullSpeedThresholdPercent = tempAction.fullSpeedThresholdPercent;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED:
                    counterMovementReleasePress.Enabled = tempAction.counterMovementReleasePress.Enabled;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS:
                    counterMovementReleasePress.UseArrowKeysForCounterMovementPresses = tempAction.counterMovementReleasePress.UseArrowKeysForCounterMovementPresses;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET:
                    counterMovementReleasePress.TapLengthPreset = tempAction.counterMovementReleasePress.TapLengthPreset;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE:
                    counterMovementReleasePress.OppositeTapLengthMode = tempAction.counterMovementReleasePress.OppositeTapLengthMode;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS:
                    counterMovementReleasePress.OppositeTapLengthMs = tempAction.counterMovementReleasePress.OppositeTapLengthMs;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT:
                    counterMovementReleasePress.OppositeTapLengthVariancePercent = tempAction.counterMovementReleasePress.OppositeTapLengthVariancePercent;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS:
                    counterMovementReleasePress.OppositeTapLengthMinimumMs = tempAction.counterMovementReleasePress.OppositeTapLengthMinimumMs;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS:
                    counterMovementReleasePress.OppositeTapLengthMaximumMs = tempAction.counterMovementReleasePress.OppositeTapLengthMaximumMs;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS:
                    counterMovementReleasePress.OppositeTapStartDelayMinimumMs = tempAction.counterMovementReleasePress.OppositeTapStartDelayMinimumMs;
                    break;
                case PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS:
                    counterMovementReleasePress.OppositeTapStartDelayMaximumMs = tempAction.counterMovementReleasePress.OppositeTapStartDelayMaximumMs;
                    break;
                case PropertyKeyStrings.BRAKE_MIN_HOLD_MS:
                    counterMovementReleasePress.MinimumHoldMs = tempAction.counterMovementReleasePress.MinimumHoldMs;
                    break;
                case PropertyKeyStrings.BRAKE_ARMING_THRESHOLD:
                    counterMovementReleasePress.ArmingThreshold = tempAction.counterMovementReleasePress.ArmingThreshold;
                    break;
                default:
                    break;
            }
        }

        private void CopyDirButton(int idx, StickAnalogEmulationAction tempAction)
        {
            dirButtons[idx] = tempAction.dirButtons[idx] != null ?
                (AxisDirButton)tempAction.dirButtons[idx].DuplicateAction() : null;
            useParentDataDraft[idx] = true;
        }
    }
}
