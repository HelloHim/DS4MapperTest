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
    // Port of JoyShockMapper's HYBRID_AIM stick mode: a deflection-proportional
    // "turn rate" term (shared math with StickMouse) added to a raw stick-delta
    // "mouselike" term, with an edge-push sustain while pegged at the outer
    // deadzone and a return-deadzone that damps output while the stick is
    // snapping back toward center.
    public class StickHybridAim : StickMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string DEAD_ZONE = "DeadZone";
            public const string MAX_ZONE = "MaxZone";
            public const string OUTPUT_CURVE = "OutputCurve";
            public const string STICK_SENS = "StickSens";
            public const string MOUSELIKE_FACTOR = "MouselikeFactor";
            public const string EDGE_PUSH_ENABLED = "EdgePushEnabled";
            public const string RETURN_DEADZONE_ENABLED = "ReturnDeadzoneEnabled";
            public const string RETURN_DEADZONE_ANGLE = "ReturnDeadzoneAngle";
            public const string RETURN_DEADZONE_CUTOFF_ANGLE = "ReturnDeadzoneCutoffAngle";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.MAX_ZONE,
            PropertyKeyStrings.OUTPUT_CURVE,
            PropertyKeyStrings.STICK_SENS,
            PropertyKeyStrings.MOUSELIKE_FACTOR,
            PropertyKeyStrings.EDGE_PUSH_ENABLED,
            PropertyKeyStrings.RETURN_DEADZONE_ENABLED,
            PropertyKeyStrings.RETURN_DEADZONE_ANGLE,
            PropertyKeyStrings.RETURN_DEADZONE_CUTOFF_ANGLE,
        };

        public const string ACTION_TYPE_NAME = "StickHybridAimAction";

        // Matches JoyShockMapper's Stick::SMOOTHING_STEPS.
        private const int SMOOTHING_STEPS = 4;

        private const int MOUSESPEEDFACTOR = 20;
        private const double MOUSE_VELOCITY_OFFSET = 0.013;

        public const int DefaultStickSens = MouseMotionSettings.DefaultMouseSpeed;
        public const int MaxStickSens = MouseMotionSettings.MaxMouseSpeed;
        public const double DefaultMouselikeFactor = 1500.0;
        public const double MaxMouselikeFactor = 10000.0;
        public const double DefaultReturnDeadzoneAngle = 45.0;
        public const double DefaultReturnDeadzoneCutoffAngle = 90.0;

        private StickDeadZone deadMod;
        private MouseMotionSettings motion = new MouseMotionSettings();

        private double mouselikeFactor = DefaultMouselikeFactor;
        private bool edgePushEnabled = true;
        private bool returnDeadzoneEnabled = true;
        private double returnDeadzoneAngle = DefaultReturnDeadzoneAngle;
        private double returnDeadzoneCutoffAngle = DefaultReturnDeadzoneCutoffAngle;

        // Per-frame carry-over state (mirrors JoyShockMapper's Stick struct).
        private double prevRawX = 0.0, prevRawY = 0.0;
        private double edgePushAmount = 0.0;
        private double smallestMagnitude = 0.0;
        private int smoothingCounter = 0;
        private double[] previousVelocitiesX = new double[SMOOTHING_STEPS];
        private double[] previousVelocitiesY = new double[SMOOTHING_STEPS];
        private double[] previousOutputX = new double[SMOOTHING_STEPS];
        private double[] previousOutputY = new double[SMOOTHING_STEPS];
        private double[] previousOutputRadial = new double[SMOOTHING_STEPS];

        private double xMotion;
        private double yMotion;

        public StickDeadZone DeadMod { get => deadMod; }

        public StickOutCurve.Curve OutputCurve
        {
            get => motion.OutputCurve;
            set => motion.OutputCurve = value;
        }

        // Deflection-proportional turn-rate speed, expressed in the same units
        // as StickMouse.MouseSpeed for UI consistency.
        public int StickSens
        {
            get => motion.MouseSpeed;
            set => motion.MouseSpeed = value;
        }

        // Scalar applied to the raw per-frame stick delta (JSM's MOUSELIKE_FACTOR).
        public double MouselikeFactor
        {
            get => mouselikeFactor;
            set => mouselikeFactor = Math.Clamp(value, 0.0, MaxMouselikeFactor);
        }

        public bool EdgePushEnabled { get => edgePushEnabled; set => edgePushEnabled = value; }
        public bool ReturnDeadzoneEnabled { get => returnDeadzoneEnabled; set => returnDeadzoneEnabled = value; }

        public double ReturnDeadzoneAngle
        {
            get => returnDeadzoneAngle;
            set => returnDeadzoneAngle = Math.Clamp(value, 0.0, 180.0);
        }

        public double ReturnDeadzoneCutoffAngle
        {
            get => returnDeadzoneCutoffAngle;
            set => returnDeadzoneCutoffAngle = Math.Clamp(value, 0.0, 180.0);
        }

        public StickHybridAim()
        {
            actionTypeName = ACTION_TYPE_NAME;
            deadMod = new StickDeadZone(0.10, 1.0, 0.0);
            deadMod.CircleDead = true;
        }

        public StickHybridAim(StickDefinition stickDefinition)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.stickDefinition = stickDefinition;
            deadMod = new StickDeadZone(0.10, 1.0, 0.0);
            deadMod.CircleDead = true;
        }

        public StickHybridAim(StickHybridAim parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.parentAction = parentAction;
            parentAction.hasLayeredAction = true;
            mappingId = parentAction.mappingId;
            this.stickDefinition = new StickDefinition(parentAction.stickDefinition);
            deadMod = new StickDeadZone(parentAction.deadMod);
            motion = new MouseMotionSettings(parentAction.motion);
            mouselikeFactor = parentAction.mouselikeFactor;
            edgePushEnabled = parentAction.edgePushEnabled;
            returnDeadzoneEnabled = parentAction.returnDeadzoneEnabled;
            returnDeadzoneAngle = parentAction.returnDeadzoneAngle;
            returnDeadzoneCutoffAngle = parentAction.returnDeadzoneCutoffAngle;
        }

        // JoyShockMapper's radial() helper: signed projection of vector (vX,vY)
        // onto the direction of (x,y).
        private static double Radial(double vX, double vY, double x, double y)
        {
            if (x != 0.0 && y != 0.0)
            {
                return (vX * x + vY * y) / Math.Sqrt(x * x + y * y);
            }

            return 0.0;
        }

        // JoyShockMapper's angleBasedDeadzone() helper.
        private static double AngleBasedDeadzone(double theta, double returnDeadzone, double returnDeadzoneCutoff)
        {
            if (theta <= returnDeadzoneCutoff)
            {
                return (theta - returnDeadzone) / returnDeadzoneCutoff;
            }

            return 0.0;
        }

        private static double ApplyCurveScalar(StickOutCurve.Curve curve, double value)
        {
            if (curve == StickOutCurve.Curve.Linear || value == 0.0)
            {
                return value;
            }

            StickOutCurve.CalcOutValue(curve, value, 0.0, out double curved, out _);
            return curved;
        }

        public override void Prepare(Mapper mapper, int axisXVal, int axisYVal,
            bool alterState = true)
        {
            active = false;
            activeEvent = false;

            int axisXMid = stickDefinition.xAxis.mid, axisYMid = stickDefinition.yAxis.mid;
            int axisXDir = axisXVal - axisXMid, axisYDir = axisYVal - axisYMid;
            bool xNegative = axisXDir < 0;
            bool yNegative = axisYDir < 0;
            int maxDirX = (!xNegative ? stickDefinition.xAxis.max : stickDefinition.xAxis.min) - axisXMid;
            int maxDirY = (!yNegative ? stickDefinition.yAxis.max : stickDefinition.yAxis.min) - axisYMid;

            double rawX = maxDirX != 0 ? axisXDir / (double)maxDirX : 0.0;
            double rawY = maxDirY != 0 ? axisYDir / (double)maxDirY : 0.0;

            double innerDeadzone = deadMod.DeadZone;
            double outerDeadzone = deadMod.MaxZone;

            double deflection = Math.Sqrt(rawX * rawX + rawY * rawY);
            double previousDeflection = Math.Sqrt(prevRawX * prevRawX + prevRawY * prevRawY);
            double angle = Math.Atan2(-rawY, rawX);

            double velocityX = rawX - prevRawX;
            double velocityY = -(rawY - prevRawY);
            double velocityRadial = Radial(velocityX, velocityY, rawX, -rawY);

            double magnitude = 0.0;
            bool inDeadzone;

            if (deflection > innerDeadzone)
            {
                inDeadzone = false;
                magnitude = outerDeadzone > innerDeadzone
                    ? (deflection - innerDeadzone) / (outerDeadzone - innerDeadzone)
                    : 1.0;

                if (deflection > outerDeadzone)
                {
                    if (velocityRadial > 0.0)
                    {
                        double dot = velocityX * Math.Sin(angle) + velocityY * -Math.Cos(angle);
                        velocityX = dot * Math.Sin(angle);
                        velocityY = dot * -Math.Cos(angle);
                    }

                    magnitude = 1.0;

                    if (previousDeflection <= outerDeadzone)
                    {
                        double averageVelocityX = 0.0, averageVelocityY = 0.0;
                        int steps = 0;
                        int counter = smoothingCounter;
                        while (steps < SMOOTHING_STEPS)
                        {
                            averageVelocityX += previousVelocitiesX[counter];
                            averageVelocityY += previousVelocitiesY[counter];
                            counter = counter == 0 ? SMOOTHING_STEPS - 1 : counter - 1;
                            steps++;
                        }

                        if (edgePushEnabled)
                        {
                            edgePushAmount *= smallestMagnitude;
                            edgePushAmount += Radial(averageVelocityX, averageVelocityY, rawX, -rawY) / steps;
                            smallestMagnitude = 1.0;
                        }
                    }
                }
            }
            else
            {
                edgePushAmount = 0.0;
                inDeadzone = true;
            }

            if (magnitude < smallestMagnitude)
            {
                smallestMagnitude = magnitude;
            }

            // Position term: deflection-proportional turn rate, reusing the same
            // deadzone/curve/speed pipeline as StickMouse (Joystick Mouse mode).
            deadMod.CalcOutValues(axisXDir, axisYDir, maxDirX, maxDirY, out double posXNorm, out double posYNorm);
            if (motion.OutputCurve != StickOutCurve.Curve.Linear && (posXNorm != 0.0 || posYNorm != 0.0))
            {
                StickOutCurve.CalcOutValue(motion.OutputCurve, posXNorm, posYNorm, out posXNorm, out posYNorm);
            }

            double timeDelta = mapper.CurrentLatency;
            timeDelta = timeDelta - (mapper.remainderCutoff(timeDelta * 10000.0, 1.0) / 10000.0);
            int mouseVelocity = motion.MouseSpeed * MOUSESPEEDFACTOR;
            double mouseOffset = MOUSE_VELOCITY_OFFSET * mouseVelocity;

            double outX = 0.0, outY = 0.0;
            if (posXNorm != 0.0 || posYNorm != 0.0)
            {
                double xSign = posXNorm >= 0.0 ? 1.0 : -1.0;
                double ySign = posYNorm >= 0.0 ? 1.0 : -1.0;
                double absXNorm = Math.Abs(posXNorm);
                double absYNorm = Math.Abs(posYNorm);
                outX = ((mouseVelocity - mouseOffset) * timeDelta * absXNorm + (mouseOffset * timeDelta)) * xSign;
                outY = ((mouseVelocity - mouseOffset) * timeDelta * absYNorm + (mouseOffset * timeDelta)) * -ySign;
            }

            // Edge-push (sustained motion while pegged at the outer deadzone) and
            // mouselike (direct stick-delta-as-mouse-delta) terms.
            double curvedSmallestMag = ApplyCurveScalar(motion.OutputCurve, smallestMagnitude);
            outX += mouselikeFactor * curvedSmallestMag * Math.Cos(angle) * edgePushAmount;
            outY += mouselikeFactor * curvedSmallestMag * Math.Sin(angle) * edgePushAmount;
            outX += mouselikeFactor * velocityX;
            outY += mouselikeFactor * velocityY;

            smoothingCounter = smoothingCounter < SMOOTHING_STEPS - 1 ? smoothingCounter + 1 : 0;
            previousVelocitiesX[smoothingCounter] = velocityX;
            previousVelocitiesY[smoothingCounter] = velocityY;
            previousOutputRadial[smoothingCounter] = Radial(outX, outY, rawX, -rawY);
            previousOutputX[smoothingCounter] = outX;
            previousOutputY[smoothingCounter] = outY;

            if (returnDeadzoneEnabled)
            {
                double averageOutputX = 0.0, averageOutputY = 0.0;
                for (int i = 0; i < SMOOTHING_STEPS; i++)
                {
                    averageOutputX += previousOutputX[i];
                    averageOutputY += previousOutputY[i];
                }

                double averageOutput = Math.Sqrt(averageOutputX * averageOutputX + averageOutputY * averageOutputY) / SMOOTHING_STEPS;
                averageOutputX /= SMOOTHING_STEPS;
                averageOutputY /= SMOOTHING_STEPS;

                double averageOutputRadial = 0.0;
                for (int i = 0; i < SMOOTHING_STEPS; i++)
                {
                    averageOutputRadial += previousOutputRadial[i];
                }
                averageOutputRadial /= SMOOTHING_STEPS;

                double returnDeadzoneAngleRad = returnDeadzoneAngle / 180.0 * Math.PI;
                double returnDeadzoneCutoffRad = returnDeadzoneCutoffAngle / 180.0 * Math.PI;

                // JoyShockMapper's own return-deadzone angle geometry is flagged
                // "STILL WRONG" in its source comments. We keep the same shape
                // and intent but guard the trig against divide-by-zero/NaN and
                // clamp the resulting damping factor to [0,1] so a fragile edge
                // case can't flip the output's sign or amplify it.
                double returnDeadzone1 = 1.0;
                if (averageOutputRadial < 0.0 && averageOutput > 0.0 && previousDeflection > 0.0)
                {
                    double cosVal = (averageOutputX * prevRawX + averageOutputY * -prevRawY) / (averageOutput * previousDeflection);
                    cosVal = Math.Clamp(cosVal, -1.0, 1.0);
                    double angleOutputToCenter = Math.Abs(Math.PI - Math.Acos(cosVal));
                    returnDeadzone1 = AngleBasedDeadzone(angleOutputToCenter, returnDeadzoneAngleRad, returnDeadzoneCutoffRad);
                }

                double returnDeadzone2 = 1.0;
                if (inDeadzone)
                {
                    if (averageOutputRadial < 0.0 && averageOutput > 0.0)
                    {
                        double angleEquivalent = Math.Abs(prevRawX * averageOutputY + prevRawY * averageOutputX) / averageOutput;
                        returnDeadzone2 = AngleBasedDeadzone(angleEquivalent, returnDeadzoneAngleRad, returnDeadzoneCutoffRad);
                    }
                    else if (innerDeadzone > 0.0)
                    {
                        double ratio = Math.Clamp(previousDeflection / innerDeadzone, -1.0, 1.0);
                        double angleEquivalent = Math.Asin(ratio);
                        returnDeadzone2 = AngleBasedDeadzone(angleEquivalent, returnDeadzoneAngleRad, returnDeadzoneCutoffRad);
                    }
                }

                double returnDeadzoneFactor = Math.Clamp(Math.Min(returnDeadzone1, returnDeadzone2), 0.0, 1.0);
                outX *= returnDeadzoneFactor;
                outY *= returnDeadzoneFactor;
                if (returnDeadzoneFactor == 0.0)
                {
                    edgePushAmount = 0.0;
                }
            }

            prevRawX = rawX;
            prevRawY = rawY;

            xMotion = outX;
            yMotion = outY;

            active = outX != 0.0 || outY != 0.0;
            activeEvent = active;
        }

        public override void Event(Mapper mapper)
        {
            mapper.SetRouteRelativeMouseMotion(MouseOutputRoute.JoystickMouse, xMotion, yMotion);
            mapper.SetRouteRelativeMouseSync(MouseOutputRoute.JoystickMouse, true);
            active = xMotion != 0.0 || yMotion != 0.0;
            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            ResetMotionState();
            active = false;
            activeEvent = false;
        }

        public override void SoftRelease(Mapper mapper, MapAction _, bool resetState = true)
        {
            ResetMotionState();
            active = false;
            activeEvent = false;
        }

        private void ResetMotionState()
        {
            xMotion = yMotion = 0.0;
            prevRawX = prevRawY = 0.0;
            edgePushAmount = 0.0;
            smallestMagnitude = 0.0;
            smoothingCounter = 0;
            Array.Clear(previousVelocitiesX, 0, SMOOTHING_STEPS);
            Array.Clear(previousVelocitiesY, 0, SMOOTHING_STEPS);
            Array.Clear(previousOutputX, 0, SMOOTHING_STEPS);
            Array.Clear(previousOutputY, 0, SMOOTHING_STEPS);
            Array.Clear(previousOutputRadial, 0, SMOOTHING_STEPS);
        }

        public override StickMapAction DuplicateAction()
        {
            return new StickHybridAim(this);
        }

        public override void SoftCopyFromParent(StickMapAction parentAction)
        {
            if (parentAction is StickHybridAim tempHybridAction)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                tempHybridAction.hasLayeredAction = true;
                mappingId = tempHybridAction.mappingId;

                this.stickDefinition =
                    new StickDefinition(tempHybridAction.stickDefinition);

                tempHybridAction.NotifyPropertyChanged += TempHybridAction_NotifyPropertyChanged;

                IEnumerable<string> useParentProList =
                    fullPropertySet.Except(changedProperties);

                foreach (string parentPropType in useParentProList)
                {
                    ApplyParentProperty(tempHybridAction, parentPropType);
                }
            }
        }

        private void TempHybridAction_NotifyPropertyChanged(object sender, NotifyPropertyChangeArgs e)
        {
            CascadePropertyChange(e.Mapper, e.PropertyName);
        }

        protected override void CascadePropertyChange(Mapper mapper, string propertyName)
        {
            if (changedProperties.Contains(propertyName))
            {
                // Property already overridden in action. Leave
                return;
            }
            else if (parentAction == null)
            {
                // No parent action. Leave
                return;
            }

            StickHybridAim tempHybridAction = parentAction as StickHybridAim;
            ApplyParentProperty(tempHybridAction, propertyName);
        }

        private void ApplyParentProperty(StickHybridAim tempHybridAction, string propertyType)
        {
            switch (propertyType)
            {
                case PropertyKeyStrings.NAME:
                    name = tempHybridAction.name;
                    break;
                case PropertyKeyStrings.DEAD_ZONE:
                    deadMod.DeadZone = tempHybridAction.deadMod.DeadZone;
                    break;
                case PropertyKeyStrings.MAX_ZONE:
                    deadMod.MaxZone = tempHybridAction.deadMod.MaxZone;
                    break;
                case PropertyKeyStrings.OUTPUT_CURVE:
                    motion.OutputCurve = tempHybridAction.motion.OutputCurve;
                    break;
                case PropertyKeyStrings.STICK_SENS:
                    motion.MouseSpeed = tempHybridAction.motion.MouseSpeed;
                    break;
                case PropertyKeyStrings.MOUSELIKE_FACTOR:
                    mouselikeFactor = tempHybridAction.mouselikeFactor;
                    break;
                case PropertyKeyStrings.EDGE_PUSH_ENABLED:
                    edgePushEnabled = tempHybridAction.edgePushEnabled;
                    break;
                case PropertyKeyStrings.RETURN_DEADZONE_ENABLED:
                    returnDeadzoneEnabled = tempHybridAction.returnDeadzoneEnabled;
                    break;
                case PropertyKeyStrings.RETURN_DEADZONE_ANGLE:
                    returnDeadzoneAngle = tempHybridAction.returnDeadzoneAngle;
                    break;
                case PropertyKeyStrings.RETURN_DEADZONE_CUTOFF_ANGLE:
                    returnDeadzoneCutoffAngle = tempHybridAction.returnDeadzoneCutoffAngle;
                    break;
                default:
                    break;
            }
        }
    }
}
