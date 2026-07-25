using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.AxisModifiers;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.MouseModifiers;
using System.Diagnostics;

namespace DS4MapperTest.StickActions
{
    public struct StickMouseParams
    {
        public int mouseSpeed;
    }

    public class StickMouse : StickMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string DEAD_ZONE = "DeadZone";
            public const string MAX_ZONE = "MaxZone";
            public const string OUTPUT_CURVE = "OutputCurve";
            public const string MOUSE_SPEED = "MouseSpeed";
            public const string DELTA_SETTINGS = "DeltaSettings";
            public const string VERTICAL_SCALE = "VerticalScale";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.MAX_ZONE,
            PropertyKeyStrings.OUTPUT_CURVE,
            PropertyKeyStrings.MOUSE_SPEED,
            PropertyKeyStrings.DELTA_SETTINGS,
            PropertyKeyStrings.VERTICAL_SCALE,
        };

        private const int MOUSESPEEDFACTOR = 20;
        private const double MOUSESTICKOFFSET = 0.0495;
        public const int DefaultMouseSpeed = MouseMotionSettings.DefaultMouseSpeed;
        public const int MaxMouseSpeed = MouseMotionSettings.MaxMouseSpeed;
        public const double DefaultVerticalScale = MouseMotionSettings.DefaultVerticalScale;
        public const double MaxVerticalScale = MouseMotionSettings.MaxVerticalScale;
        //private const double MOUSE_VELOCITY_OFFSET = 0.12;
        private const double MOUSE_VELOCITY_OFFSET = 0.013;
        public const string ACTION_TYPE_NAME = "StickMouseAction";

        private StickDeadZone deadMod;
        private MouseMotionSettings motion = new MouseMotionSettings();
        //private StickDefinition stickDefinition;
        private double xNorm = 0.0, yNorm = 0.0;
        private double xMotion;
        private double yMotion;
        public int MouseSpeed
        {
            get => motion.MouseSpeed;
            set => motion.MouseSpeed = value;
        }

        public double VerticalScale
        {
            get => motion.VerticalScale;
            set => motion.VerticalScale = value;
        }

        public StickDeadZone DeadMod { get => deadMod; }
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

        public StickMouse()
        {
            actionTypeName = ACTION_TYPE_NAME;
            //deadMod = new StickDeadZone(0.10, 0.9, 0.0);
            deadMod = new StickDeadZone(0.10, 1.0, 0.0);
            deadMod.CircleDead = true;
        }

        public StickMouse(StickDefinition stickDefinition)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.stickDefinition = stickDefinition;
            //deadMod = new StickDeadZone(0.10, 0.9, 0.0);
            deadMod = new StickDeadZone(0.10, 1.0, 0.0);
            deadMod.CircleDead = true;
        }

        public StickMouse(StickMouse parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.parentAction = parentAction;
            parentAction.hasLayeredAction = true;
            mappingId = parentAction.mappingId;
            this.stickDefinition = new StickDefinition(parentAction.stickDefinition);
            deadMod = new StickDeadZone(parentAction.deadMod);
            motion = new MouseMotionSettings(parentAction.motion);
        }

        double previousPointerX = 0.0;
        //double accelHelperX = 0.0;
        //double accelTravelX = 0.0;
        //Stopwatch deltaEasingTimeX = new Stopwatch();

        double previousPointerY = 0.0;
        //double accelHelperY = 0.0;
        //double accelTravelY = 0.0;
        //Stopwatch deltaEasingTimeY = new Stopwatch();

        double previousPointerRadial = 0.0;
        double accelCurrentMultiRadial = 0.0;
        double accelEasingMultiRadial = 0.0;
        double accelTravelRadial = 0.0;
        Stopwatch deltaEasingTimeRadial = new Stopwatch();
        double totalTravelRadial = 0.0;
        //bool inDuration = false;
        //long currentTime;
        //long previousTime;

        public override void Prepare(Mapper mapper, int axisXVal, int axisYVal,
            bool alterState = true)
        {
            active = false;
            activeEvent = false;

            xNorm = 0.0; yNorm = 0.0;
            //int axisMid = stickDefinition.axisMid;
            int axisXMid = stickDefinition.xAxis.mid, axisYMid = stickDefinition.yAxis.mid;
            int axisXDir = axisXVal - axisXMid, axisYDir = axisYVal - axisYMid;
            bool xNegative = axisXDir < 0;
            bool yNegative = axisYDir < 0;
            int maxDirX = (!xNegative ? stickDefinition.xAxis.max : stickDefinition.xAxis.min) - axisXMid;
            int maxDirY = (!yNegative ? stickDefinition.yAxis.max : stickDefinition.yAxis.min) - axisYMid;
            deadMod.CalcOutValues(axisXDir, axisYDir, maxDirX,
                    maxDirY, out xNorm, out yNorm);

            if (xNorm != 0.0 || yNorm != 0.0)
            {
                if (motion.OutputCurve != StickOutCurve.Curve.Linear)
                {
                    StickOutCurve.CalcOutValue(motion.OutputCurve, xNorm, yNorm,
                        out xNorm, out yNorm);
                    //StickOutCurve.CalcOutValue(StickOutCurve.Curve.EnhancedPrecision, xNorm, yNorm,
                    //    out xNorm, out yNorm);
                    //xNorm = AxisOutCurve.CalcOutValue(AxisOutCurve.Curve.EnhancedPrecision, xNorm);
                    //yNorm = AxisOutCurve.CalcOutValue(AxisOutCurve.Curve.EnhancedPrecision, yNorm);
                }

                double rawXNorm = axisXDir / (double)maxDirX;
                double rawYNorm = axisYDir / (double)maxDirY;

                double r = Math.Atan2(-axisYDir, axisXDir);
                double unitXRatio = Math.Abs(Math.Cos(r));
                double unitYRatio = Math.Abs(Math.Sin(r));
                double capX = axisXDir >= 0.0 ? unitXRatio * 1.0 : unitXRatio * 1.0;
                double capY = axisYDir >= 0.0 ? unitYRatio * 1.0 : unitYRatio * 1.0;
                double absSideX = Math.Abs(rawXNorm); double absSideY = Math.Abs(rawYNorm);
                if (absSideX > capX) capX = absSideX;
                if (absSideY > capY) capY = absSideY;
                double tempRatioX = capX > 0 ? rawXNorm / capX : 0;
                double tempRatioY = capY > 0 ? rawYNorm / capY : 0;

                // Calculate delta acceleration slope and offset.
                bool testDeltaAccel = motion.DeltaSettings.Enabled;
                double testAccelMulti = motion.DeltaSettings.Multiplier;
                double testAccelMaxTravel = motion.DeltaSettings.MaxTravel;
                double testAccelMinTravel = motion.DeltaSettings.MinTravel;
                double testAccelEasingDuration = motion.DeltaSettings.EasingDuration;
                double minfactor = Math.Max(1.0, motion.DeltaSettings.MinFactor); // default 1.0
                double minTravelStop = Math.Max(0.1, testAccelMinTravel);

                double accelSlope = (testAccelMulti - minfactor) / (testAccelMaxTravel - testAccelMinTravel);
                double accelOffset = minfactor - (accelSlope * testAccelMinTravel);

                double outXNorm = xNorm, outYNorm = yNorm;
                double absX = Math.Abs(outXNorm);
                double absY = Math.Abs(outYNorm);

                double hyp = Math.Sqrt((rawXNorm * rawXNorm) + (rawYNorm * rawYNorm));

                if (testDeltaAccel)
                {
                    //Trace.WriteLine("DELTA CHECK");
                    //double tempCheckTravel = !inDuration ? testAccelMinTravel : testAccelMinTravel;
                    if (hyp > 0.0 &&
                        Math.Abs(hyp - previousPointerRadial) >= testAccelMinTravel &&
                        (hyp - previousPointerRadial >= 0.0))
                    {
                        double tempTravel = Math.Abs(hyp - previousPointerRadial);
                        double tempDist = tempTravel;

                        if (totalTravelRadial == 0.0)
                        {
                            totalTravelRadial = tempTravel;
                            accelEasingMultiRadial = (accelSlope * tempDist + accelOffset);
                        }
                        else
                        {
                            totalTravelRadial += tempDist;
                            double tempEasingDist = totalTravelRadial;
                            //tempDist = tempEasingDist;
                            //tempTravel = tempDist;
                            accelEasingMultiRadial = (accelSlope * tempEasingDist + accelOffset);
                        }

                        accelCurrentMultiRadial = (accelSlope * tempDist + accelOffset);
                        outXNorm = outXNorm * accelCurrentMultiRadial;
                        outYNorm = outYNorm * accelCurrentMultiRadial;
                        accelTravelRadial = tempTravel;

                        deltaEasingTimeRadial.Restart();
                        //currentTime = Stopwatch.GetTimestamp();
                        //previousTime = currentTime;

                        previousPointerRadial = hyp;
                        previousPointerX = rawXNorm;
                        previousPointerY = rawYNorm;

                        //Trace.WriteLine($"WTF {hyp} {accelTravelRadial} {accelCurrentMultiRadial} {accelEasingMultiRadial}");
                    }
                    else if (hyp > 0.0 && accelCurrentMultiRadial > 0.0 &&
                        Math.Abs(previousPointerRadial - hyp) < minTravelStop &&
                        !(
                        (previousPointerX >= 0.0) != (rawXNorm >= 0.0) &&
                        (previousPointerY >= 0.0) != (rawYNorm >= 0.0))
                        )
                    {
                        //Trace.WriteLine("STAY ZONE");
                        //inDuration = true;

                        double timeElapsed = deltaEasingTimeRadial.ElapsedMilliseconds;
                        //currentTime = Stopwatch.GetTimestamp();
                        //double timeElapsed = (currentTime - previousTime) * (1.0 / Stopwatch.Frequency) * 1000.0;
                        double elapsedDiff = 1.0;
                        double tempAccel = accelCurrentMultiRadial;
                        double tempTravel = accelTravelRadial;

                        if (hyp - previousPointerRadial <= 0.0)
                        {
                            double tempmix2 = Math.Abs(hyp - previousPointerRadial);
                            tempmix2 = Math.Min(tempmix2, minTravelStop);
                            double tempmixslope = (testAccelMinTravel - tempTravel) / minTravelStop;
                            double tempshitintercept = tempTravel;
                            double finalmanham = (tempmixslope * tempmix2 + tempshitintercept);

                            tempTravel = finalmanham;
                            tempAccel = (accelSlope * (tempTravel) + accelOffset);
                        }

                        double elapsedDuration = testAccelEasingDuration * (accelEasingMultiRadial / testAccelMulti);
                        //Trace.WriteLine($"TIME ELAPSED: {timeElapsed} {tempAccel} {elapsedDuration}");
                        if (elapsedDuration > 0.0 && (timeElapsed * 0.001) < elapsedDuration)
                        {
                            elapsedDiff = ((timeElapsed * 0.001) / elapsedDuration);
                            elapsedDiff = (1.0 - tempAccel) * (elapsedDiff * elapsedDiff * elapsedDiff) + tempAccel;
                            outXNorm = elapsedDiff * outXNorm;
                            outYNorm = elapsedDiff * outYNorm;

                            //Trace.WriteLine($"CONITNUING {elapsedDiff}");
                        }
                        else
                        {
                            // Easing time has ended. Reset values.
                            previousPointerRadial = hyp;
                            accelCurrentMultiRadial = 0.0;
                            accelTravelRadial = 0.0;
                            deltaEasingTimeRadial.Reset();
                            accelEasingMultiRadial = 0.0;
                            totalTravelRadial = 0.0;
                            //previousTime = currentTime;
                            previousPointerX = rawXNorm;
                            previousPointerY = rawYNorm;
                            //inDuration = false;

                            //Trace.WriteLine($"DURATION ENDED");
                        }
                    }
                    else
                    {
                        //Trace.WriteLine("NEW RESET");
                        previousPointerRadial = hyp;
                        accelCurrentMultiRadial = 0.0;
                        accelTravelRadial = 0.0;
                        accelEasingMultiRadial = 0.0;
                        totalTravelRadial = 0.0;
                        deltaEasingTimeRadial.Reset();
                        //currentTime = Stopwatch.GetTimestamp();
                        //previousTime = currentTime;
                        previousPointerX = rawXNorm;
                        previousPointerY = rawYNorm;
                        //inDuration = false;
                    }
                }
                else
                {
                    previousPointerRadial = hyp;
                    previousPointerX = rawXNorm;
                    previousPointerY = rawYNorm;
                    accelCurrentMultiRadial = 0.0;
                    accelTravelRadial = 0.0;
                    accelEasingMultiRadial = 0.0;
                    totalTravelRadial = 0.0;
                    //inDuration = false;
                    //currentTime = Stopwatch.GetTimestamp();
                    //previousTime = currentTime;
                    //if (deltaEasingTimeRadial.IsRunning)
                    {
                        deltaEasingTimeRadial.Reset();
                    }
                }

                double timeDelta = mapper.CurrentLatency;
                timeDelta = timeDelta - (mapper.remainderCutoff(timeDelta * 10000.0, 1.0) / 10000.0);
                int mouseVelocity = motion.MouseSpeed * MOUSESPEEDFACTOR;
                double mouseOffset = MOUSE_VELOCITY_OFFSET * mouseVelocity;
                int verticalMouseVelocity = (int)Math.Round(mouseVelocity * motion.VerticalScale);
                double verticalMouseOffset = MOUSE_VELOCITY_OFFSET * verticalMouseVelocity;

                double xSign = xNorm >= 0.0 ? 1.0 : -1.0;
                double ySign = yNorm >= 0.0 ? 1.0 : -1.0;
                double absXNorm = Math.Abs(outXNorm);
                double absYNorm = Math.Abs(outYNorm);
                double tempMouseOffsetX = unitXRatio * mouseOffset;
                double tempMouseOffsetY = unitYRatio * verticalMouseOffset;

                xMotion = ((mouseVelocity - tempMouseOffsetX) * timeDelta * absXNorm + (tempMouseOffsetX * timeDelta)) * xSign;
                yMotion = ((verticalMouseVelocity - tempMouseOffsetY) * timeDelta * absYNorm + (tempMouseOffsetY * timeDelta)) * -ySign;

                active = true;
                activeEvent = true;
            }
        }

        public override void Event(Mapper mapper)
        {
            mapper.MouseX = xMotion; mapper.MouseY = yMotion;
            mapper.MouseSync = true;
            if (xNorm != 0.0 || yNorm != 0.0)
            {
                active = true;
            }
            else
            {
                active = false;
            }

            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            xMotion = yMotion = 0.0;
            active = false;
            activeEvent = false;

            //if (resetState)
            //{
            //    stateData.Reset();
            //}
        }

        public override StickMapAction DuplicateAction()
        {
            return new StickMouse(this);
        }

        public override void SoftRelease(Mapper mapper, MapAction _, bool resetState = true)
        {
            xMotion = yMotion = 0.0;
            active = false;
            activeEvent = false;
        }

        public override void SoftCopyFromParent(StickMapAction parentAction)
        {
            if (parentAction is StickMouse tempMouseAction)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                tempMouseAction.hasLayeredAction = true;
                mappingId = tempMouseAction.mappingId;

                this.stickDefinition =
                    new StickDefinition(tempMouseAction.stickDefinition);

                tempMouseAction.NotifyPropertyChanged += TempMouseAction_NotifyPropertyChanged;

                // Determine the set with properties that should inherit
                // from the parent action
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
                        case PropertyKeyStrings.VERTICAL_SCALE:
                            motion.VerticalScale = tempMouseAction.motion.VerticalScale;
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
                // Property already overrridden in action. Leave
                return;
            }
            else if (parentAction == null)
            {
                // No parent action. Leave
                return;
            }

            StickMouse tempMouseAction = parentAction as StickMouse;

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
                case PropertyKeyStrings.VERTICAL_SCALE:
                    motion.VerticalScale = tempMouseAction.motion.VerticalScale;
                    break;
                default:
                    break;
            }
        }
    }
}
