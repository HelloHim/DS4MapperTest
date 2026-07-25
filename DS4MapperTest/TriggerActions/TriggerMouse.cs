using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using DS4MapperTest.AxisModifiers;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.MouseModifiers;
using DS4MapperTest.StickModifiers;

namespace DS4MapperTest.TriggerActions
{
    public class TriggerMouse : TriggerMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string DEAD_ZONE = "DeadZone";
            public const string MAX_ZONE = "MaxZone";
            public const string OUTPUT_CURVE = "OutputCurve";
            public const string MOUSE_SPEED = "MouseSpeed";
            public const string DELTA_SETTINGS = "DeltaSettings";
            public const string DIRECTION_DEGREES = "DirectionDegrees";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.MAX_ZONE,
            PropertyKeyStrings.OUTPUT_CURVE,
            PropertyKeyStrings.MOUSE_SPEED,
            PropertyKeyStrings.DELTA_SETTINGS,
            PropertyKeyStrings.DIRECTION_DEGREES,
        };

        private const int MOUSESPEEDFACTOR = 20;
        public const int DefaultMouseSpeed = MouseMotionSettings.DefaultMouseSpeed;
        public const int MaxMouseSpeed = MouseMotionSettings.MaxMouseSpeed;
        public const double DefaultDirectionDegreesRight = 90.0;
        public const double DefaultDirectionDegreesLeft = -90.0;
        public const string ACTION_TYPE_NAME = "TriggerMouseAction";

        private AxisDeadZone deadMod;
        private MouseMotionSettings motion = new MouseMotionSettings();
        private double axisNorm;
        private double xMotion;
        private double yMotion;
        private double directionDegrees = DefaultDirectionDegreesRight;

        public int MouseSpeed
        {
            get => motion.MouseSpeed;
            set => motion.MouseSpeed = value;
        }

        public AxisDeadZone DeadMod { get => deadMod; }
        public StickOutCurve.Curve OutputCurve
        {
            get => motion.OutputCurve;
            set => motion.OutputCurve = value;
        }

        public MouseMotionSettings.DeltaAccelSettings MouseDeltaSettings
        {
            get => motion.DeltaSettings;
            set => motion.DeltaSettings = value;
        }

        // 0 degrees points up, increasing clockwise (90 = right, 180/-180 =
        // down, -90 = left), matching Flick Stick's angle convention
        // elsewhere in this codebase.
        public double DirectionDegrees
        {
            get => directionDegrees;
            set
            {
                double normalised = value % 360.0;
                if (normalised > 180.0) normalised -= 360.0;
                else if (normalised < -180.0) normalised += 360.0;
                directionDegrees = normalised;
            }
        }

        public static double DefaultDirectionForSide(TriggerActionCodes trigCode)
        {
            return trigCode == TriggerActionCodes.LeftTrigger
                ? DefaultDirectionDegreesLeft
                : DefaultDirectionDegreesRight;
        }

        public TriggerMouse()
        {
            actionTypeName = ACTION_TYPE_NAME;
            deadMod = new AxisDeadZone(0.0, 1.0, 0.0);
        }

        double previousPointerDepth = 0.0;
        double accelCurrentMulti = 0.0;
        double accelEasingMulti = 0.0;
        double accelTravel = 0.0;
        Stopwatch deltaEasingTime = new Stopwatch();
        double totalTravel = 0.0;

        public override void Prepare(Mapper mapper, ref TriggerEventFrame eventFrame, bool alterState = true)
        {
            active = false;
            activeEvent = false;

            int maxDir = triggerDefinition.trigAxis.max;
            deadMod.CalcOutValues(eventFrame.axisValue, maxDir, out axisNorm);

            if (axisNorm != 0.0)
            {
                double curvedDepth = axisNorm;
                if (motion.OutputCurve != StickOutCurve.Curve.Linear)
                {
                    curvedDepth = AxisOutCurve.CalcOutValue(ToAxisCurve(motion.OutputCurve), axisNorm);
                }

                // Calculate delta acceleration slope and offset, mirroring
                // the joystick mouse behaviour but driven off trigger depth
                // instead of radial stick travel.
                bool testDeltaAccel = motion.DeltaSettings.Enabled;
                double testAccelMulti = motion.DeltaSettings.Multiplier;
                double testAccelMaxTravel = motion.DeltaSettings.MaxTravel;
                double testAccelMinTravel = motion.DeltaSettings.MinTravel;
                double testAccelEasingDuration = motion.DeltaSettings.EasingDuration;
                double minfactor = Math.Max(1.0, motion.DeltaSettings.MinFactor);
                double minTravelStop = Math.Max(0.1, testAccelMinTravel);

                double accelSlope = (testAccelMulti - minfactor) / (testAccelMaxTravel - testAccelMinTravel);
                double accelOffset = minfactor - (accelSlope * testAccelMinTravel);

                double outDepth = curvedDepth;

                if (testDeltaAccel)
                {
                    if (axisNorm > 0.0 &&
                        Math.Abs(axisNorm - previousPointerDepth) >= testAccelMinTravel &&
                        (axisNorm - previousPointerDepth >= 0.0))
                    {
                        double tempTravel = Math.Abs(axisNorm - previousPointerDepth);
                        double tempDist = tempTravel;

                        if (totalTravel == 0.0)
                        {
                            totalTravel = tempTravel;
                            accelEasingMulti = (accelSlope * tempDist + accelOffset);
                        }
                        else
                        {
                            totalTravel += tempDist;
                            accelEasingMulti = (accelSlope * totalTravel + accelOffset);
                        }

                        accelCurrentMulti = (accelSlope * tempDist + accelOffset);
                        outDepth = outDepth * accelCurrentMulti;
                        accelTravel = tempTravel;

                        deltaEasingTime.Restart();
                        previousPointerDepth = axisNorm;
                    }
                    else if (axisNorm > 0.0 && accelCurrentMulti > 0.0 &&
                        Math.Abs(previousPointerDepth - axisNorm) < minTravelStop)
                    {
                        double timeElapsed = deltaEasingTime.ElapsedMilliseconds;
                        double elapsedDiff = 1.0;
                        double tempAccel = accelCurrentMulti;
                        double tempTravel = accelTravel;

                        if (axisNorm - previousPointerDepth <= 0.0)
                        {
                            double tempmix2 = Math.Min(Math.Abs(axisNorm - previousPointerDepth), minTravelStop);
                            double tempmixslope = (testAccelMinTravel - tempTravel) / minTravelStop;
                            double finalmanham = (tempmixslope * tempmix2 + tempTravel);

                            tempTravel = finalmanham;
                            tempAccel = (accelSlope * tempTravel + accelOffset);
                        }

                        double elapsedDuration = testAccelEasingDuration * (accelEasingMulti / testAccelMulti);
                        if (elapsedDuration > 0.0 && (timeElapsed * 0.001) < elapsedDuration)
                        {
                            elapsedDiff = ((timeElapsed * 0.001) / elapsedDuration);
                            elapsedDiff = (1.0 - tempAccel) * (elapsedDiff * elapsedDiff * elapsedDiff) + tempAccel;
                            outDepth = elapsedDiff * outDepth;
                        }
                        else
                        {
                            previousPointerDepth = axisNorm;
                            accelCurrentMulti = 0.0;
                            accelTravel = 0.0;
                            deltaEasingTime.Reset();
                            accelEasingMulti = 0.0;
                            totalTravel = 0.0;
                        }
                    }
                    else
                    {
                        previousPointerDepth = axisNorm;
                        accelCurrentMulti = 0.0;
                        accelTravel = 0.0;
                        accelEasingMulti = 0.0;
                        totalTravel = 0.0;
                        deltaEasingTime.Reset();
                    }
                }
                else
                {
                    previousPointerDepth = axisNorm;
                    accelCurrentMulti = 0.0;
                    accelTravel = 0.0;
                    accelEasingMulti = 0.0;
                    totalTravel = 0.0;
                    deltaEasingTime.Reset();
                }

                double timeDelta = mapper.CurrentLatency;
                timeDelta = timeDelta - (mapper.remainderCutoff(timeDelta * 10000.0, 1.0) / 10000.0);

                int mouseVelocity = motion.MouseSpeed * MOUSESPEEDFACTOR;

                // 0 degrees is up, so the X/Y split comes from sin/-cos
                // rather than the usual cos/sin pairing for a 0 = right
                // angle convention. There is only one direction of travel
                // here, so both axes share the same speed; scaling them
                // independently (as vertical scale would) would bend the
                // motion away from the configured direction.
                double directionRadians = directionDegrees * Math.PI / 180.0;
                double xDirection = Math.Sin(directionRadians);
                double yDirection = -Math.Cos(directionRadians);

                xMotion = mouseVelocity * outDepth * timeDelta * xDirection;
                yMotion = mouseVelocity * outDepth * timeDelta * yDirection;

                active = true;
                activeEvent = true;
            }
            else
            {
                previousPointerDepth = 0.0;
                accelCurrentMulti = 0.0;
                accelTravel = 0.0;
                accelEasingMulti = 0.0;
                totalTravel = 0.0;
                deltaEasingTime.Reset();
            }
        }

        private static AxisOutCurve.Curve ToAxisCurve(StickOutCurve.Curve curve)
        {
            switch (curve)
            {
                case StickOutCurve.Curve.EnhancedPrecision: return AxisOutCurve.Curve.EnhancedPrecision;
                case StickOutCurve.Curve.Quadratic: return AxisOutCurve.Curve.Quadratic;
                case StickOutCurve.Curve.Cubic: return AxisOutCurve.Curve.Cubic;
                case StickOutCurve.Curve.EaseoutQuad: return AxisOutCurve.Curve.EaseoutQuad;
                case StickOutCurve.Curve.EaseoutCubic: return AxisOutCurve.Curve.EaseoutCubic;
                default: return AxisOutCurve.Curve.Linear;
            }
        }

        public override void Event(Mapper mapper)
        {
            mapper.MouseX = xMotion; mapper.MouseY = yMotion;
            mapper.MouseSync = true;
            active = axisNorm != 0.0;
            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            xMotion = yMotion = 0.0;
            active = false;
            activeEvent = false;

            if (resetState)
            {
                stateData.Reset();
            }
        }

        public override void SoftRelease(Mapper mapper, MapAction checkAction, bool resetState = true)
        {
            xMotion = yMotion = 0.0;
            active = false;
            activeEvent = false;

            if (resetState)
            {
                stateData.Reset();
            }
        }

        public override void SoftCopyFromParent(TriggerMapAction parentAction)
        {
            if (parentAction is TriggerMouse tempMouseAction)
            {
                base.SoftCopyFromParent(parentAction);

                tempMouseAction.NotifyPropertyChanged += TempMouseAction_NotifyPropertyChanged;

                IEnumerable<string> useParentProList =
                    fullPropertySet.Except(changedProperties);

                foreach (string parentPropType in useParentProList)
                {
                    switch (parentPropType)
                    {
                        case PropertyKeyStrings.NAME:
                            name = tempMouseAction.name;
                            break;
                        case PropertyKeyStrings.DEAD_ZONE:
                            deadMod.DeadZone = tempMouseAction.deadMod.DeadZone;
                            break;
                        case PropertyKeyStrings.MAX_ZONE:
                            deadMod.MaxZone = tempMouseAction.deadMod.MaxZone;
                            break;
                        case PropertyKeyStrings.OUTPUT_CURVE:
                            motion.OutputCurve = tempMouseAction.motion.OutputCurve;
                            break;
                        case PropertyKeyStrings.MOUSE_SPEED:
                            motion.MouseSpeed = tempMouseAction.motion.MouseSpeed;
                            break;
                        case PropertyKeyStrings.DELTA_SETTINGS:
                            motion.DeltaSettings = new MouseMotionSettings.DeltaAccelSettings(tempMouseAction.motion.DeltaSettings);
                            break;
                        case PropertyKeyStrings.DIRECTION_DEGREES:
                            directionDegrees = tempMouseAction.directionDegrees;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void TempMouseAction_NotifyPropertyChanged(object sender, NotifyPropertyChangeArgs e)
        {
            CascadePropertyChange(e.Mapper, e.PropertyName);
        }

        protected override void CascadePropertyChange(Mapper mapper, string propertyName)
        {
            if (changedProperties.Contains(propertyName))
            {
                return;
            }
            else if (parentAction == null)
            {
                return;
            }

            TriggerMouse tempMouseAction = parentAction as TriggerMouse;

            switch (propertyName)
            {
                case PropertyKeyStrings.NAME:
                    name = tempMouseAction.name;
                    break;
                case PropertyKeyStrings.DEAD_ZONE:
                    deadMod.DeadZone = tempMouseAction.deadMod.DeadZone;
                    break;
                case PropertyKeyStrings.MAX_ZONE:
                    deadMod.MaxZone = tempMouseAction.deadMod.MaxZone;
                    break;
                case PropertyKeyStrings.OUTPUT_CURVE:
                    motion.OutputCurve = tempMouseAction.motion.OutputCurve;
                    break;
                case PropertyKeyStrings.MOUSE_SPEED:
                    motion.MouseSpeed = tempMouseAction.motion.MouseSpeed;
                    break;
                case PropertyKeyStrings.DELTA_SETTINGS:
                    motion.DeltaSettings = new MouseMotionSettings.DeltaAccelSettings(tempMouseAction.motion.DeltaSettings);
                    break;
                case PropertyKeyStrings.DIRECTION_DEGREES:
                    directionDegrees = tempMouseAction.directionDegrees;
                    break;
                default:
                    break;
            }
        }
    }
}
