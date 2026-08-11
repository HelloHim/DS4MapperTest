using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4MapperTest.Common;
using DS4MapperTest.StickActions;

namespace DS4MapperTest.TouchpadActions
{
    public class TouchpadFlickStick : TouchpadMapAction
    {
        public const string ACTION_TYPE_NAME = "TouchFlickStickAction";

        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string REAL_WORLD_CALIBRATION = "RealWorldCalibration";
            public const string FLICK_THRESHOLD = "FlickThreshold";
            public const string FLICK_TIME = "FlickTime";
            public const string FLICK_TIME_EXPONENT = "FlickTimeExponent";
            public const string MIN_ANGLE_THRESHOLD = "MinAngleThreshold";
            public const string IN_GAME_SENS = "InGameSens";
            public const string RELEASE_DAMPENING_SPEED = "ReleaseDampeningSpeed";
            public const string MULTIPLIER_COMPENSATION = "MultiplierCompensation";
            public const string ACCELERATION_MULTIPLIER = "AccelerationMultiplier";
            public const string ROTATE_SMOOTH_OVERRIDE = "RotateSmoothOverride";
            public const string SUB_MODE = "SubMode";
            public const string SNAP_ANGLE = "SnapAngle";
            public const string SNAP_STRENGTH = "SnapStrength";
            public const string SWEEP_SENSITIVITY = "SweepSensitivity";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.REAL_WORLD_CALIBRATION,
            PropertyKeyStrings.FLICK_THRESHOLD,
            PropertyKeyStrings.FLICK_TIME,
            PropertyKeyStrings.FLICK_TIME_EXPONENT,
            PropertyKeyStrings.MIN_ANGLE_THRESHOLD,
            PropertyKeyStrings.IN_GAME_SENS,
            PropertyKeyStrings.RELEASE_DAMPENING_SPEED,
            PropertyKeyStrings.MULTIPLIER_COMPENSATION,
            PropertyKeyStrings.ACCELERATION_MULTIPLIER,
            PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE,
            PropertyKeyStrings.SUB_MODE,
            PropertyKeyStrings.SNAP_ANGLE,
            PropertyKeyStrings.SNAP_STRENGTH,
            PropertyKeyStrings.SWEEP_SENSITIVITY,
        };

        public class FlickStickMappingData
        {
            //public const double DEFAULT_MINCUTOFF = 0.4;
            //public const double DEFAULT_BETA = 0.4;

            public const double DEFAULT_FLICK_PROGRESS = 0.0;
            public const double DEFAULT_FLICK_SIZE = 0.0;
            public const double DEFAULT_FLICK_ANGLE_REMAINDER = 0.0;
            public const double DEFAULT_FLICK_TIME_ACTUAL = 0.0;

            //public OneEuroFilter flickFilter = new OneEuroFilter(DEFAULT_MINCUTOFF, DEFAULT_BETA);
            public double flickProgress = DEFAULT_FLICK_PROGRESS;
            public double flickSize = DEFAULT_FLICK_SIZE;
            public double flickAngleRemainder = DEFAULT_FLICK_ANGLE_REMAINDER;
            public double flickTimeActual = DEFAULT_FLICK_TIME_ACTUAL;

            // This is intentionally the same size as JoyShockMapper's flick-stick
            // rotation buffer. The active portion is capped to a 64 ms window.
            public const int FLICK_SMOOTH_SAMPLE_COUNT = 256;
            private readonly double[] flickRotationSamples =
                new double[FLICK_SMOOTH_SAMPLE_COUNT];
            private int frontFlickRotationSample;

            public void ResetRotationSmoothing()
            {
                Array.Clear(flickRotationSamples, 0, flickRotationSamples.Length);
                frontFlickRotationSample = 0;
            }

            public double GetSmoothedStickRotation(double value, double bottomThreshold,
                double topThreshold, int maxSamples)
            {
                frontFlickRotationSample--;
                if (frontFlickRotationSample < 0)
                {
                    frontFlickRotationSample = FLICK_SMOOTH_SAMPLE_COUNT - 1;
                }

                double immediateFactor = topThreshold <= bottomThreshold ? 1.0 :
                    (Math.Abs(value) - bottomThreshold) / (topThreshold - bottomThreshold);
                immediateFactor = Math.Clamp(immediateFactor, 0.0, 1.0);
                double frontSample = flickRotationSamples[frontFlickRotationSample] =
                    value * (1.0 - immediateFactor);

                double result = frontSample / maxSamples;
                for (int i = 1; i < maxSamples; i++)
                {
                    int rotatedIndex = (frontFlickRotationSample + i) % FLICK_SMOOTH_SAMPLE_COUNT;
                    result += flickRotationSamples[rotatedIndex] / maxSamples;
                }

                return result + value * immediateFactor;
            }

            public void Reset()
            {
                //flickFilter = new OneEuroFilter(DEFAULT_MINCUTOFF, DEFAULT_BETA);
                flickProgress = DEFAULT_FLICK_PROGRESS;
                flickSize = DEFAULT_FLICK_SIZE;
                flickAngleRemainder = DEFAULT_FLICK_ANGLE_REMAINDER;
                flickTimeActual = DEFAULT_FLICK_TIME_ACTUAL;
                ResetRotationSmoothing();
            }
        }

        private const double IN_GAME_SENS_DEFAULT = 1.0;
        public const bool MULTIPLIER_COMPENSATION_DEFAULT = false;
        public const double ACCELERATION_MULTIPLIER_DEFAULT = 1.0;
        public const double ACCELERATION_MULTIPLIER_MIN = 0.01;
        public const double ACCELERATION_MULTIPLIER_MAX = 100.0;

        private double realWorldCalibration = 5.00;
        public double RealWorldCalibration
        {
            get => realWorldCalibration; set => realWorldCalibration = value;
        }

        private double flickThreshold = 0.9;
        public double FlickThreshold
        {
            get => flickThreshold; set => flickThreshold = value;
        }

        private double flickTime = 0.1;
        public double FlickTime
        {
            get => flickTime; set => flickTime = value;
        }

        private double flickTimeExponent = 0.0;
        public double FlickTimeExponent
        {
            get => flickTimeExponent;
            set => flickTimeExponent = Math.Clamp(value, 0.0, 1.0);
        }

        // Retained for backward-compatible profile loading. It is deliberately
        // disabled: small rotation steps are preserved and smoothed instead of
        // accumulated behind a threshold, matching the current sweep approach.
        private const double MIN_ANGLE_THRESHOLD_DISABLED = 0.0;
        private double minAngleThreshold = MIN_ANGLE_THRESHOLD_DISABLED;
        public double MinAngleThreshold
        {
            get => MIN_ANGLE_THRESHOLD_DISABLED;
            set => minAngleThreshold = MIN_ANGLE_THRESHOLD_DISABLED;
        }

        private double inGameSens = IN_GAME_SENS_DEFAULT;
        public double InGameSens
        {
            get => inGameSens;
            set => inGameSens = Math.Clamp(value, 0.01, 10.0);
        }

        private double releaseDampeningSpeed = 2.5;
        public double ReleaseDampeningSpeed
        {
            get => releaseDampeningSpeed;
            set => releaseDampeningSpeed = Math.Clamp(value, 0.0, 10.0);
        }

        private bool multiplierCompensation = MULTIPLIER_COMPENSATION_DEFAULT;
        public bool MultiplierCompensation
        {
            get => multiplierCompensation; set => multiplierCompensation = value;
        }

        private double accelerationMultiplier = ACCELERATION_MULTIPLIER_DEFAULT;
        public double AccelerationMultiplier
        {
            get => accelerationMultiplier;
            set => accelerationMultiplier = Math.Clamp(value,
                ACCELERATION_MULTIPLIER_MIN, ACCELERATION_MULTIPLIER_MAX);
        }

        // Matches JoyShockMapper's ROTATE_SMOOTH_OVERRIDE semantics. -1 uses its
        // controller-resolution default, 0 disables smoothing, and positive values
        // set the small-angle threshold in radians per mapper update.
        private double rotateSmoothOverride = -1.0;
        public double RotateSmoothOverride
        {
            get => rotateSmoothOverride;
            set => rotateSmoothOverride = Math.Clamp(value, -1.0, 1.0);
        }

        // Matches Steam Input's Flick Stick Sweep Sensitivity. A dimensionless
        // multiplier applied only to the continuous sweep rotation, not the
        // initial flick-turn. 1.0 is a natural 1:1 angular ratio; 0 disables
        // sweep rotation entirely.
        private double sweepSensitivity = 1.0;
        public double SweepSensitivity
        {
            get => sweepSensitivity;
            set => sweepSensitivity = Math.Clamp(value, 0.0, 6.0);
        }

        private FlickStickSubMode subMode = FlickStickSubMode.Standard;
        public FlickStickSubMode SubMode
        {
            get => subMode;
            set => subMode = value;
        }

        // Matches JoyShockMapper's FLICK_SNAP_MODE and FLICK_SNAP_STRENGTH. Only the
        // initial flick is snapped; sweeping a held touch is left untouched.
        private FlickSnapAngle snapAngle = FlickSnapping.DEFAULT_SNAP_ANGLE;
        public FlickSnapAngle SnapAngle
        {
            get => snapAngle;
            set => snapAngle = value;
        }

        private double snapStrength = FlickSnapping.DEFAULT_STRENGTH;
        public double SnapStrength
        {
            get => snapStrength;
            set => snapStrength = Math.Clamp(value, FlickSnapping.MinStrength,
                FlickSnapping.MaxStrength);
        }

        private FlickStickMappingData tempFlickData;

        private int prevAxisXVal;
        private int prevAxisYVal;

        private double tempMouseDeltaX;

        public TouchpadFlickStick()
        {
            actionTypeName = ACTION_TYPE_NAME;

            tempFlickData = new FlickStickMappingData();
        }

        public override void Prepare(Mapper mapper, ref TouchEventFrame touchFrame, bool alterState = true)
        {
            tempMouseDeltaX = 0.0;

            int axisXVal = touchFrame.X, axisYVal = touchFrame.Y;
            double angleChange = 0.0;

            angleChange = HandleFlickStickAngle(mapper, axisXVal, axisYVal, prevAxisXVal, prevAxisYVal);
            double lsangle = angleChange * 180.0 / Math.PI;
            // Never discard sub-threshold movement. The former implementation only
            // accumulated positive deltas, which made thresholded rotation uneven.
            lsangle += tempFlickData.flickAngleRemainder;
            tempFlickData.flickAngleRemainder = 0.0;

            if (lsangle != 0.0)
            {
                tempFlickData.flickAngleRemainder = 0.0;
                //flickAngleRemainder = lsangle - (int)lsangle;
                //lsangle = (int)lsangle;
                tempMouseDeltaX += lsangle * realWorldCalibration / inGameSens;
            }

            if (multiplierCompensation && tempMouseDeltaX != 0.0)
            {
                double accelMultiplier = Math.Clamp(accelerationMultiplier,
                    ACCELERATION_MULTIPLIER_MIN, ACCELERATION_MULTIPLIER_MAX);
                tempMouseDeltaX /= accelMultiplier;
            }

            if (tempMouseDeltaX != 0.0)
            {
                active = true;
                activeEvent = true;
            }
            else
            {
                active = false;
                activeEvent = false;
            }

            prevAxisXVal = axisXVal;
            prevAxisYVal = axisYVal;
        }

        public override void Event(Mapper mapper)
        {
            if (tempMouseDeltaX != 0.0)
            {
                mapper.AddRouteRelativeMouseMotion(MouseOutputRoute.Trackpad, tempMouseDeltaX, 0.0);
                mapper.SetRouteRelativeMouseSync(MouseOutputRoute.Trackpad, true);
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
            tempMouseDeltaX = 0.0;
            tempFlickData.Reset();
            active = false;
            activeEvent = false;
        }

        public override void SoftRelease(Mapper mapper, MapAction checkAction, bool resetState = true)
        {
            tempMouseDeltaX = 0.0;
            tempFlickData.Reset();
            active = false;
            activeEvent = false;
        }

        public override void SoftCopyFromParent(TouchpadMapAction parentAction)
        {
            if (parentAction is TouchpadFlickStick tempFlickAction)
            {
                base.SoftCopyFromParent(parentAction);

                tempFlickAction.NotifyPropertyChanged += TempFlickAction_NotifyPropertyChanged;

                // Determine the set with properties that should inherit
                // from the parent action
                IEnumerable<string> useParentProList =
                    fullPropertySet.Except(changedProperties);

                foreach (string parentPropType in useParentProList)
                {
                    switch (parentPropType)
                    {
                        case PropertyKeyStrings.NAME:
                            name = tempFlickAction.name;
                            break;
                        case PropertyKeyStrings.REAL_WORLD_CALIBRATION:
                            realWorldCalibration = tempFlickAction.realWorldCalibration;
                            break;
                        case PropertyKeyStrings.FLICK_THRESHOLD:
                            flickThreshold = tempFlickAction.flickThreshold;
                            break;
                        case PropertyKeyStrings.FLICK_TIME:
                            flickTime = tempFlickAction.flickTime;
                            break;
                        case PropertyKeyStrings.FLICK_TIME_EXPONENT:
                            flickTimeExponent = tempFlickAction.flickTimeExponent;
                            break;
                        case PropertyKeyStrings.MIN_ANGLE_THRESHOLD:
                            minAngleThreshold = tempFlickAction.minAngleThreshold;
                            break;
                        case PropertyKeyStrings.IN_GAME_SENS:
                            inGameSens = tempFlickAction.inGameSens;
                            break;
                        case PropertyKeyStrings.RELEASE_DAMPENING_SPEED:
                            releaseDampeningSpeed = tempFlickAction.releaseDampeningSpeed;
                            break;
                        case PropertyKeyStrings.MULTIPLIER_COMPENSATION:
                            multiplierCompensation = tempFlickAction.multiplierCompensation;
                            break;
                        case PropertyKeyStrings.ACCELERATION_MULTIPLIER:
                            accelerationMultiplier = tempFlickAction.accelerationMultiplier;
                            break;
                        case PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE:
                            rotateSmoothOverride = tempFlickAction.rotateSmoothOverride;
                            break;
                        case PropertyKeyStrings.SUB_MODE:
                            subMode = tempFlickAction.subMode;
                            break;
                        case PropertyKeyStrings.SNAP_ANGLE:
                            snapAngle = tempFlickAction.snapAngle;
                            break;
                        case PropertyKeyStrings.SNAP_STRENGTH:
                            snapStrength = tempFlickAction.snapStrength;
                            break;
                        case PropertyKeyStrings.SWEEP_SENSITIVITY:
                            sweepSensitivity = tempFlickAction.sweepSensitivity;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void TempFlickAction_NotifyPropertyChanged(object sender, NotifyPropertyChangeArgs e)
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

            TouchpadFlickStick tempFlickAction = parentAction as TouchpadFlickStick;

            switch (propertyName)
            {
                case PropertyKeyStrings.NAME:
                    name = tempFlickAction.name;
                    break;
                case PropertyKeyStrings.REAL_WORLD_CALIBRATION:
                    realWorldCalibration = tempFlickAction.realWorldCalibration;
                    break;
                case PropertyKeyStrings.FLICK_THRESHOLD:
                    flickThreshold = tempFlickAction.flickThreshold;
                    break;
                case PropertyKeyStrings.FLICK_TIME:
                    flickTime = tempFlickAction.flickTime;
                    break;
                case PropertyKeyStrings.FLICK_TIME_EXPONENT:
                    flickTimeExponent = tempFlickAction.flickTimeExponent;
                    break;
                case PropertyKeyStrings.MIN_ANGLE_THRESHOLD:
                    minAngleThreshold = tempFlickAction.minAngleThreshold;
                    break;
                case PropertyKeyStrings.IN_GAME_SENS:
                    inGameSens = tempFlickAction.inGameSens;
                    break;
                case PropertyKeyStrings.RELEASE_DAMPENING_SPEED:
                    releaseDampeningSpeed = tempFlickAction.releaseDampeningSpeed;
                    break;
                case PropertyKeyStrings.MULTIPLIER_COMPENSATION:
                    multiplierCompensation = tempFlickAction.multiplierCompensation;
                    break;
                case PropertyKeyStrings.ACCELERATION_MULTIPLIER:
                    accelerationMultiplier = tempFlickAction.accelerationMultiplier;
                    break;
                case PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE:
                    rotateSmoothOverride = tempFlickAction.rotateSmoothOverride;
                    break;
                case PropertyKeyStrings.SUB_MODE:
                    subMode = tempFlickAction.subMode;
                    break;
                case PropertyKeyStrings.SNAP_ANGLE:
                    snapAngle = tempFlickAction.snapAngle;
                    break;
                case PropertyKeyStrings.SNAP_STRENGTH:
                    snapStrength = tempFlickAction.snapStrength;
                    break;
                case PropertyKeyStrings.SWEEP_SENSITIVITY:
                    sweepSensitivity = tempFlickAction.sweepSensitivity;
                    break;
                default:
                    break;
            }
        }

        private double HandleFlickStickAngle(Mapper mapper, int axisXVal, int axisYVal,
            int prevXVal, int prevYVal)
        {
            double result = 0.0;

            FlickStickMappingData flickData = tempFlickData;

            int axisXMid = touchpadDefinition.xAxis.mid, axisYMid = touchpadDefinition.yAxis.mid;
            int axisXDir = axisXVal - axisXMid, axisYDir = axisYVal - axisYMid;
            int prevAxisXDir = prevXVal - axisXMid, prevAxisYDir = prevAxisYVal - axisYMid;
            bool xNegative = axisXDir < 0;
            bool yNegative = axisYDir < 0;
            double maxDirX = (!xNegative ? touchpadDefinition.xAxis.max : touchpadDefinition.xAxis.min) - axisXMid;
            double maxDirY = (!yNegative ? touchpadDefinition.yAxis.max : touchpadDefinition.yAxis.min) - axisYMid;
            double prevMaxDirX = (prevAxisXDir >= 0 ? touchpadDefinition.xAxis.max : touchpadDefinition.xAxis.min) - axisXMid;
            double prevMaxDirY = (prevAxisYDir >= 0 ? touchpadDefinition.yAxis.max : touchpadDefinition.yAxis.min) - axisYMid;

            double lastTestX = (prevAxisXDir) / prevMaxDirX;
            double lastTestY = (prevAxisYDir) / prevMaxDirY;
            double currentTestX = (axisXDir) / maxDirX;
            double currentTestY = (axisYDir) / maxDirY;

            double lastLength = (lastTestX * lastTestX) + (lastTestY * lastTestY);
            double length = (currentTestX * currentTestX) + (currentTestY * currentTestY);
            double testLength = flickThreshold * flickThreshold;

            double sweepDampen = 1.0;
            if (releaseDampeningSpeed > 0.0 && mapper.CurrentLatency > 0.0)
            {
                double prevMag = Math.Sqrt(lastLength);
                double currMag = Math.Sqrt(length);
                double returnVelocity = (prevMag - currMag) / mapper.CurrentLatency;
                if (returnVelocity > 0.0)
                {
                    double dampenFactor = Math.Clamp(returnVelocity / releaseDampeningSpeed, 0.0, 1.0);
                    sweepDampen = 1.0 - dampenFactor;
                }
            }

            if (length >= testLength)
            {
                if (lastLength < testLength)
                {
                    if (subMode != FlickStickSubMode.RotateOnly)
                    {
                        // Start a new flick unless this is the rotation-only variant.
                        flickData.flickProgress = 0.0;
                        // Snap before deriving the flick duration so a snapped flick
                        // is timed by the angle it actually travels.
                        flickData.flickSize = FlickSnapping.Apply(
                            Math.Atan2((axisXVal - axisXMid), (axisYVal - axisYMid)),
                            snapAngle, snapStrength);
                        flickData.flickTimeActual = flickTime * Math.Pow(Math.Abs(flickData.flickSize) / Math.PI, flickTimeExponent);
                        flickData.ResetRotationSmoothing();
                        //flickData.flickFilter.Filter(0.0, mapper.CurrentLatency);
                    }
                }
                else
                {
                    // Turn camera
                    double stickAngle = Math.Atan2((axisXVal - axisXMid), (axisYVal - axisYMid));
                    double lastStickAngle = Math.Atan2((prevXVal - axisXMid), (prevYVal - axisYMid));
                    double angleChange = (stickAngle - lastStickAngle);
                    double rawAngleChange = angleChange;
                    angleChange = (angleChange + Math.PI) % (2 * Math.PI);
                    if (angleChange < 0)
                    {
                        angleChange += 2 * Math.PI;
                    }
                    angleChange -= Math.PI;
                    //Trace.WriteLine(string.Format("ANGLE CHANGE: {0} {1} {2}", stickAngle, lastStickAngle, rawAngleChange));
                    //Trace.WriteLine(string.Format("{0} {1} | {2} {3}", axisXVal, prevXVal, axisYVal, prevYVal));
                    //angleChange = flickData.flickFilter.Filter(angleChange, mapper.CurrentLatency);
                    // Flick Only deliberately preserves the initial flick but blocks
                    // the camera rotation caused by sweeping a held touch.
                    if (subMode != FlickStickSubMode.FlickOnly)
                    {
                        // JoyShockMapper's soft-tiered smoothing: only tiny touch
                        // steps are buffered (up to 64 ms); larger rotations remain
                        // immediate. This hides low-resolution touch quantisation
                        // without making normal sweeping feel delayed.
                        double outputScale = realWorldCalibration / inGameSens;
                        if (outputScale != 0.0)
                        {
                            double rotationOutput = angleChange * sweepSensitivity * sweepDampen * outputScale;
                            int maxSmoothingSamples = mapper.CurrentLatency > 0.0
                                ? Math.Clamp((int)Math.Ceiling(0.064 / mapper.CurrentLatency), 1,
                                    FlickStickMappingData.FLICK_SMOOTH_SAMPLE_COUNT)
                                : 1;
                            double stepSize = rotateSmoothOverride < 0.0 ? 0.01 : rotateSmoothOverride;
                            rotationOutput = flickData.GetSmoothedStickRotation(rotationOutput,
                                outputScale * stepSize * 2.0,
                                outputScale * stepSize * 4.0,
                                maxSmoothingSamples);
                            result += rotationOutput / outputScale;
                        }
                    }
                }
            }
            else
            {
                // Cleanup
                //flickData.flickFilter.Filter(0.0, mapper.CurrentLatency);
                result = 0.0;
            }

            // Continue Flick motion
            double lastFlickProgress = flickData.flickProgress;
            double testFlickTime = flickData.flickTimeActual;
            if (subMode != FlickStickSubMode.RotateOnly &&
                lastFlickProgress < testFlickTime)
            {
                flickData.flickProgress = Math.Min(flickData.flickProgress + mapper.CurrentLatency,
                    testFlickTime);

                double lastPerOne = lastFlickProgress / testFlickTime;
                double thisPerOne = flickData.flickProgress / testFlickTime;

                double warpedLastPerOne = WarpEaseOut(lastPerOne);
                double warpedThisPerone = WarpEaseOut(thisPerOne);
                //Trace.WriteLine(string.Format("{0} {1}", warpedThisPerone, warpedLastPerOne));

                result += (warpedThisPerone - warpedLastPerOne) * flickData.flickSize;
            }

            return result;
        }

        private static double WarpEaseOut(double input)
        {
            double flipped = 1.0 - input;
            return 1.0 - flipped * flipped;
        }


    }
}
