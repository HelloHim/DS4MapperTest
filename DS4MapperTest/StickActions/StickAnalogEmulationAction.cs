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
            public const string DEAD_ZONE = "DeadZone";
            public const string MAX_ZONE = "MaxZone";

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
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.MAX_ZONE,
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

        private double xNorm, yNorm;

        private AnalogEmulationMath.ResolutionMode directionMode = AnalogEmulationMath.ResolutionMode.Sixteen;
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

                directionMode = parentAction.directionMode;
                directionPulseTimeMs = parentAction.directionPulseTimeMs;
                speedEmulationEnabled = parentAction.speedEmulationEnabled;
                speedActivePercent = parentAction.speedActivePercent;
                speedPulseTimeMs = parentAction.speedPulseTimeMs;
                fullSpeedThresholdPercent = parentAction.fullSpeedThresholdPercent;
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

        public override void Prepare(Mapper mapper, int axisXVal, int axisYVal, bool alterState = true)
        {
            xNorm = 0.0; yNorm = 0.0;

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

            for (int i = 0; i < slotOn.Length; i++) slotOn[i] = false;

            int primaryIdx = SlotIndex(currentPrimary);
            if (primaryIdx >= 0) slotOn[primaryIdx] = currentSpeedGateOn;

            int secondaryIdx = SlotIndex(currentSecondary);
            if (secondaryIdx >= 0) slotOn[secondaryIdx] = currentSpeedGateOn && currentDirectionGateOn;

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

            active = anyActive;
            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
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
                case PropertyKeyStrings.DEAD_ZONE:
                    deadMod.DeadZone = tempAction.deadMod.DeadZone;
                    break;
                case PropertyKeyStrings.MAX_ZONE:
                    deadMod.MaxZone = tempAction.deadMod.MaxZone;
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
