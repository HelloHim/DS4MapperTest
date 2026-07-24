using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sensorit.Base;
//using System.Diagnostics;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.StickModifiers;

namespace DS4MapperTest.TouchpadActions
{
    public class TouchpadMouse : TouchpadMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string DEAD_ZONE = "DeadZone";
            public const string VERTICAL_DEAD_ZONE = "VerticalDeadZone";
            public const string ANGLE_SNAP_DEGREES = "AngleSnapDegrees";
            public const string SMOOTH_ANGLE_SNAP = "SmoothAngleSnap";
            public const string TRACKBALL_MODE = "Trackball";
            public const string TRACKBALL_FRICTION = "TrackballFriction";
            public const string SWIPES_PER_360 = "SwipesPer360";
            public const string VERTICAL_SCALE = "VerticalScale";
            public const string SMOOTHING_ENABLED = "SmoothingEnabled";
            public const string SMOOTHING_FILTER = "SmoothingFilter";
            public const string ACCEL_CURVE = "AccelCurve";
            public const string MIN_ACCEL_X_SENS = "MinAccelXSens";
            public const string MAX_ACCEL_X_SENS = "MaxAccelXSens";
            public const string MIN_ACCEL_Y_SENS = "MinAccelYSens";
            public const string MAX_ACCEL_Y_SENS = "MaxAccelYSens";
            public const string MIN_ACCEL_THRESHOLD = "MinAccelThreshold";
            public const string MAX_ACCEL_THRESHOLD = "MaxAccelThreshold";
            public const string POWER_CURVE_VREF = "PowerCurveVRef";
            public const string POWER_CURVE_EXPONENT = "PowerCurveExponent";
            public const string NATURAL_CURVE_VHALF = "NaturalCurveVHalf";
            public const string STABILITY_MODE = "StabilityMode";
            public const string STABILITY_TOUCH_SETTLE = "StabilityTouchSettle";
            public const string STABILITY_NOISE = "StabilityNoise";
            public const string STABILITY_EDGE_GUARD = "StabilityEdgeGuard";
            public const string STABILITY_EDGE_START_GATE = "StabilityEdgeStartGate";
            public const string STABILITY_STATIONARY = "StabilityStationary";
            public const string STABILITY_DELTA_CLAMP = "StabilityDeltaClamp";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.VERTICAL_DEAD_ZONE,
            PropertyKeyStrings.ANGLE_SNAP_DEGREES,
            PropertyKeyStrings.SMOOTH_ANGLE_SNAP,
            PropertyKeyStrings.TRACKBALL_MODE,
            PropertyKeyStrings.TRACKBALL_FRICTION,
            PropertyKeyStrings.SWIPES_PER_360,
            PropertyKeyStrings.VERTICAL_SCALE,
            PropertyKeyStrings.SMOOTHING_ENABLED,
            PropertyKeyStrings.SMOOTHING_FILTER,
            PropertyKeyStrings.ACCEL_CURVE,
            PropertyKeyStrings.MIN_ACCEL_X_SENS,
            PropertyKeyStrings.MAX_ACCEL_X_SENS,
            PropertyKeyStrings.MIN_ACCEL_Y_SENS,
            PropertyKeyStrings.MAX_ACCEL_Y_SENS,
            PropertyKeyStrings.MIN_ACCEL_THRESHOLD,
            PropertyKeyStrings.MAX_ACCEL_THRESHOLD,
            PropertyKeyStrings.POWER_CURVE_VREF,
            PropertyKeyStrings.POWER_CURVE_EXPONENT,
            PropertyKeyStrings.NATURAL_CURVE_VHALF,
            PropertyKeyStrings.STABILITY_MODE,
            PropertyKeyStrings.STABILITY_TOUCH_SETTLE,
            PropertyKeyStrings.STABILITY_NOISE,
            PropertyKeyStrings.STABILITY_EDGE_GUARD,
            PropertyKeyStrings.STABILITY_EDGE_START_GATE,
            PropertyKeyStrings.STABILITY_STATIONARY,
            PropertyKeyStrings.STABILITY_DELTA_CLAMP,
        };

        public const string ACTION_TYPE_NAME = "TouchMouseAction";

        private double xNorm = 0.0, yNorm = 0.0;
        private double xMotion;
        private double yMotion;

        private int deadZone;
        public int DeadZone
        {
            get => deadZone;
            set => deadZone = value;
        }

        private int verticalDeadZone = DEFAULT_VERTICAL_DEAD_ZONE;
        public int VerticalDeadZone
        {
            get => verticalDeadZone;
            set => verticalDeadZone = value;
        }

        private double trackpadAngleSnapDegrees;
        public double TrackpadAngleSnapDegrees
        {
            get => trackpadAngleSnapDegrees;
            set => trackpadAngleSnapDegrees = Math.Clamp(value,
                AngleSnapping.MinDegrees, AngleSnapping.MaxDegrees);
        }

        private bool trackpadSmoothAngleSnap;
        public bool TrackpadSmoothAngleSnap
        {
            get => trackpadSmoothAngleSnap;
            set => trackpadSmoothAngleSnap = value;
        }

        private const int TRACKBALL_INIT_FRICTION = 10;
        private const int TRACKBALL_JOY_FRICTION = 7;
        private const int TRACKBALL_MASS = 45;
        private const double TRACKBALL_RADIUS = 0.0245;
        private const double TOUCHPAD_MOUSE_OFFSET = 0.375;
        //private const double TOUCHPAD_COEFFICIENT = 0.012;
        private const double TOUCHPAD_COEFFICIENT = 0.012 * 1.1;

        private double TRACKBALL_INERTIA = 2.0 * (TRACKBALL_MASS * TRACKBALL_RADIUS * TRACKBALL_RADIUS) / 5.0;
        //private double TRACKBALL_SCALE = 0.000023;
        private double TRACKBALL_SCALE = 0.000023;
        private const int TRACKBALL_BUFFER_LEN = 8;

        private const int DEFAULT_DEADZONE = 1;
        private const int DEFAULT_VERTICAL_DEAD_ZONE = 0;
        public const double DEFAULT_SWIPES_PER_360 = 1.0;
        public const double DEFAULT_VERTICAL_SCALE = 0.5;
        private const bool DEFAULT_SMOOTHING_ENABLED = false;
        public const GyroMouseAccelCurveChoice DEFAULT_ACCEL_CURVE =
            GyroMouseAccelCurveChoice.None;
        public const double DEFAULT_MIN_ACCEL_SENS = DEFAULT_SWIPES_PER_360;
        public const double DEFAULT_MAX_ACCEL_SENS = 3.0;
        public const double DEFAULT_MIN_ACCEL_THRESHOLD = 0.0;
        public const double DEFAULT_MAX_ACCEL_THRESHOLD = 40.0;
        public const double DEFAULT_NATURAL_VHALF = 20.0;
        public const double DEFAULT_POWER_VREF = 10.0;
        public const double DEFAULT_POWER_EXPONENT = 1.0;

        public GyroMouseAccelCurveChoice AccelCurve { get; set; } =
            DEFAULT_ACCEL_CURVE;
        public double MinAccelXSens { get; set; } = DEFAULT_MIN_ACCEL_SENS;
        public double MaxAccelXSens { get; set; } = DEFAULT_MAX_ACCEL_SENS;
        public double MinAccelYSens { get; set; } = DEFAULT_MIN_ACCEL_SENS;
        public double MaxAccelYSens { get; set; } = DEFAULT_MAX_ACCEL_SENS;
        public double MinAccelThreshold { get; set; } =
            DEFAULT_MIN_ACCEL_THRESHOLD;
        public double MaxAccelThreshold { get; set; } =
            DEFAULT_MAX_ACCEL_THRESHOLD;
        public double NaturalVHalf { get; set; } = DEFAULT_NATURAL_VHALF;
        public double PowerVRef { get; set; } = DEFAULT_POWER_VREF;
        public double PowerExponent { get; set; } = DEFAULT_POWER_EXPONENT;

        private class TrackballVelData
        {
            public double[] trackballXBuffer = new double[TRACKBALL_BUFFER_LEN];
            public double[] trackballYBuffer = new double[TRACKBALL_BUFFER_LEN];
            public int trackballBufferTail = 0;
            public int trackballBufferHead = 0;
            public double trackballAccel = 0.0;
            public double trackballXVel = 0.0;
            public double trackballYVel = 0.0;
            public bool trackballActive = false;
            public double trackballDXRemain = 0.0;
            public double trackballDYRemain = 0.0;

            public void PurgeData()
            {
                Array.Clear(trackballXBuffer, 0, TRACKBALL_BUFFER_LEN);
                Array.Clear(trackballYBuffer, 0, TRACKBALL_BUFFER_LEN);
                trackballXVel = 0.0;
                trackballYVel = 0.0;
                trackballActive = false;
                trackballBufferTail = 0;
                trackballBufferHead = 0;
                trackballDXRemain = 0.0;
                trackballDYRemain = 0.0;
            }
        }

        private TrackballVelData trackData;

        private bool smoothingEnabled;
        public bool SmoothingEnabled
        {
            get => smoothingEnabled;
            set => smoothingEnabled = value;
        }

        public struct SmoothingFilterSettings
        {
            public const double DEFAULT_MIN_CUTOFF = 1.0;
            public const double DEFAULT_BETA = 0.7;
            
            public OneEuroFilter filterX;
            public OneEuroFilter filterY;

            public double minCutOff;
            public double beta;

            public void Init()
            {
                minCutOff = DEFAULT_MIN_CUTOFF;
                beta = DEFAULT_BETA;

                filterX = new OneEuroFilter(minCutoff: minCutOff,
                    beta: beta);
                filterY = new OneEuroFilter(minCutoff: minCutOff,
                    beta: beta);
            }

            public void ResetFilters()
            {
                filterX.Reset();
                filterY.Reset();
            }

            public void UpdateSmoothingFilters()
            {
                filterX.MinCutoff = minCutOff;
                filterX.Beta = beta;
                filterX.Reset();

                filterY.MinCutoff = minCutOff;
                filterY.Beta = beta;
                filterY.Reset();
            }
        }

        private SmoothingFilterSettings smoothingFilterSettings;
        public ref SmoothingFilterSettings ActionSmoothingSettings
        {
            get => ref smoothingFilterSettings;
        }

        private bool trackballEnabled = true;
        public bool TrackballEnabled
        {
            get => trackballEnabled;
            set
            {
                trackballEnabled = value;
                if (!value)
                {
                    // Purge any in-flight spin so it cannot resume if trackball is re-enabled
                    trackData.PurgeData();
                }
                CalcTrackAccel();
            }
        }
        //private bool useParentTrackball;

        private int trackballFriction = TRACKBALL_INIT_FRICTION;
        public int TrackballFriction
        {
            get => trackballFriction;
            set
            {
                trackballFriction = value;
                CalcTrackAccel();
            }
        }

        private double swipesPer360 = DEFAULT_SWIPES_PER_360;
        public double SwipesPer360
        {
            get => swipesPer360;
            set => swipesPer360 = value;
        }

        private double verticalScale = DEFAULT_VERTICAL_SCALE;
        public double VerticalScale
        {
            get => verticalScale;
            set => verticalScale = value;
        }

        private bool useParentTrackFriction;

        private bool useParentSmoothingFilter;

        // Settings follow profile/layer inheritance. The filter instance
        // holds runtime state and is private to this action
        private TouchpadStabilitySettings stabilitySettings =
            new TouchpadStabilitySettings();
        public TouchpadStabilitySettings StabilitySettings => stabilitySettings;

        private TouchpadStabilityFilter stabilityFilter;

        public TouchpadMouse()
        {
            actionTypeName = ACTION_TYPE_NAME;
            trackData = new TrackballVelData();
            stabilityFilter = new TouchpadStabilityFilter(stabilitySettings);
            smoothingFilterSettings.Init();
            smoothingEnabled = DEFAULT_SMOOTHING_ENABLED;
            //trackData.trackballAccel = TRACKBALL_RADIUS * TRACKBALL_JOY_FRICTION / TRACKBALL_INERTIA;
            trackData.trackballAccel = TRACKBALL_RADIUS * trackballFriction / TRACKBALL_INERTIA;
            deadZone = DEFAULT_DEADZONE;
        }

        public override void Prepare(Mapper mapper, ref TouchEventFrame touchFrame, bool alterState = true)
        {
            double previousXMotion = xMotion;
            double previousYMotion = yMotion;

            if (touchFrame.Touch && touchFrame.passDelta)
            {
                active = activeEvent = false;
                // Need a better way to tell mapper to not reset remainders
                mapper.MouseEventFired = true;
                return;
            }

            if (stabilityFilter.Enabled)
            {
                ref TouchEventFrame previousFrame =
                    ref mapper.GetPreviousTouchEventFrame(touchpadDefinition.touchCode);

                if (touchFrame.Touch && !previousFrame.Touch)
                {
                    stabilityFilter.OnTouchStart(ref touchFrame, touchpadDefinition);
                }
                else if (!touchFrame.Touch && previousFrame.Touch)
                {
                    stabilityFilter.OnTouchEnd();
                }
            }

            if (trackballEnabled)
            {
                TrackballMouseProcess(mapper, ref touchFrame);
            }
            else if (!trackballEnabled && touchFrame.Touch)
            {
                ref TouchEventFrame previousTouchFrame =
                    ref mapper.GetPreviousTouchEventFrame(touchpadDefinition.touchCode);

                if (previousTouchFrame.Touch)
                {
                    // Process normal mouse
                    ProcessTouchMouse(mapper, ref touchFrame, ref previousTouchFrame);
                }
            }
            else
            {
                // Trackball disabled and finger not touching — stop all motion immediately
                xNorm = yNorm = 0.0;
                xMotion = yMotion = 0.0;
            }

            if (xMotion != 0.0 || yMotion != 0.0)
            {
                active = activeEvent = true;
            }
            //else if (previousXMotion != xMotion || previousYMotion != yMotion)
            //{
            //    active = activeEvent = true;
            //}
            else
            {
                // Add smoothing even when finger is not touching
                smoothingFilterSettings.filterX.Filter(0.0, mapper.CurrentRate);
                smoothingFilterSettings.filterY.Filter(0.0, mapper.CurrentRate);
                active = activeEvent = false;
            }
        }

        public override void Event(Mapper mapper)
        {
            if (xMotion != 0.0 || yMotion != 0.0)
            {
                //mapper.MouseX = xMotion; mapper.MouseY = yMotion;

                if (smoothingEnabled)
                {
                    mapper.GenerateMouseEventFilteredV2(smoothingFilterSettings.filterX,
                        smoothingFilterSettings.filterY,
                        ref xMotion, ref yMotion);
                    mapper.MouseSync = true;
                    //mapper.MouseEventFired = true;
                }
                else
                {
                    // Allow mapper to handle event
                    mapper.MouseSync = true;
                }

                mapper.MouseX += xMotion; mapper.MouseY += yMotion;

                active = true;
            }
            else
            {
                active = false;

                if (smoothingEnabled)
                {
                    mapper.GenerateMouseEventFilteredV2(smoothingFilterSettings.filterX,
                        smoothingFilterSettings.filterY,
                        ref xMotion, ref yMotion);
                    mapper.MouseSync = true;
                    //mapper.MouseEventFired = true;
                }
                else
                {
                    // Allow mapper to handle event
                    mapper.MouseSync = true;
                }

                mapper.MouseX += xMotion; mapper.MouseY += yMotion;

                //mapper.MouseX = xMotion; mapper.MouseY = yMotion;
                //mapper.MouseXRemainder = mapper.MouseYRemainder = 0.0;
            }

            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            xNorm = yNorm = 0.0;
            xMotion = yMotion = 0.0;

            PurgeTrackballData();
            stabilityFilter.Reset();
            smoothingFilterSettings.filterX.Reset();
            smoothingFilterSettings.filterY.Reset();

            active = activeEvent = false;
        }

        public override void SoftRelease(Mapper mapper, MapAction checkAction, bool resetState = true)
        {
            if (active)
            {
                TouchpadMouse tempMouseAction = checkAction as TouchpadMouse;
                if (parentAction != null && !useParentTrackFriction)
                {
                    // Re-evaluate trackball friction with parent action setting
                    tempMouseAction.CalcTrackAccel();
                }

                if (parentAction != null &&
                    trackballEnabled != tempMouseAction.trackballEnabled)
                {
                    trackData.PurgeData();
                }
            }

            if (!useParentSmoothingFilter)
            {
                smoothingFilterSettings.filterX.Reset();
                smoothingFilterSettings.filterY.Reset();
            }

            // Runtime filter state is never shared between actions
            stabilityFilter.Reset();
        }

        private void PurgeTrackballData()
        {
            Array.Clear(trackData.trackballXBuffer, 0, TRACKBALL_BUFFER_LEN);
            Array.Clear(trackData.trackballYBuffer, 0, TRACKBALL_BUFFER_LEN);
            trackData.trackballXVel = 0.0;
            trackData.trackballYVel = 0.0;
            trackData.trackballActive = false;
            trackData.trackballBufferTail = 0;
            trackData.trackballBufferHead = 0;
            trackData.trackballDXRemain = 0.0;
            trackData.trackballDYRemain = 0.0;
        }

        private void TrackballMouseProcess(Mapper mapper, ref TouchEventFrame touchFrame)
        {
            ref TouchEventFrame previousTouchFrame =
                ref mapper.GetPreviousTouchEventFrame(touchpadDefinition.touchCode);

            if (touchFrame.Touch && !previousTouchFrame.Touch)
            {
                if (trackData.trackballActive)
                {
                    //Trace.WriteLine("CHECKING HERE");
                }

                // Initial touch
                Array.Clear(trackData.trackballXBuffer, 0, TRACKBALL_BUFFER_LEN);
                Array.Clear(trackData.trackballYBuffer, 0, TRACKBALL_BUFFER_LEN);
                trackData.trackballXVel = 0.0;
                trackData.trackballYVel = 0.0;
                trackData.trackballActive = false;
                trackData.trackballBufferTail = 0;
                trackData.trackballBufferHead = 0;
                trackData.trackballDXRemain = 0.0;
                trackData.trackballDYRemain = 0.0;

                //Trace.WriteLine("INITIAL");
            }
            else if (touchFrame.Touch && previousTouchFrame.Touch)
            {
                // Process normal mouse
                ProcessTouchMouse(mapper, ref touchFrame, ref previousTouchFrame);
                //Console.WriteLine("NORMAL");
            }
            else if (!touchFrame.Touch && previousTouchFrame.Touch)
            {
                // Initially released. Calculate velocity and start Trackball
                double currentWeight = 1.0;
                double finalWeight = 0.0;
                double x_out = 0.0, y_out = 0.0;
                int idx = -1;
                for (int i = 0; i < TRACKBALL_BUFFER_LEN && idx != trackData.trackballBufferHead; i++)
                {
                    idx = (trackData.trackballBufferTail - i - 1 + TRACKBALL_BUFFER_LEN) % TRACKBALL_BUFFER_LEN;
                    x_out += trackData.trackballXBuffer[idx] * currentWeight;
                    y_out += trackData.trackballYBuffer[idx] * currentWeight;
                    finalWeight += currentWeight;
                    currentWeight *= 1.0;
                }

                x_out /= finalWeight;
                trackData.trackballXVel = x_out;
                y_out /= finalWeight;
                trackData.trackballYVel = y_out;

                double dist = Math.Sqrt((trackData.trackballXVel * trackData.trackballXVel) + (trackData.trackballYVel * trackData.trackballYVel));
                if (dist >= 1.0)
                {
                    trackData.trackballActive = true;

                    //Debug.WriteLine("START TRACK {0}", dist);
                    ProcessTrackballFrame(mapper, ref touchFrame);
                }
                else
                {
                    //Debug.WriteLine("LESS THAN {0}", dist);
                    trackData.PurgeData();
                }
            }
            else if (!touchFrame.Touch && trackData.trackballActive)
            {
                //Console.WriteLine("CONTINUE TRACK");
                // Trackball Running
                ProcessTrackballFrame(mapper, ref touchFrame);
            }
            else if (!touchFrame.Touch)
            {
                xNorm = yNorm = 0.0;
                xMotion = yMotion = 0.0;
            }
        }

        private void ProcessTouchMouse(Mapper mapper, ref TouchEventFrame touchFrame,
            ref TouchEventFrame previousFrame)
        {
            int dx;
            int dy;
            if (stabilityFilter.Enabled)
            {
                // Filter raw coordinates before the deltas reach the
                // trackball buffer and the output pipeline
                stabilityFilter.Filter(ref touchFrame, ref previousFrame,
                    touchpadDefinition, out dx, out int dyPad);
                dy = -dyPad;
            }
            else
            {
                dx = touchFrame.X - previousFrame.X;
                dy = -(touchFrame.Y - previousFrame.Y);
            }
            //int rawDeltaX = dx, rawDeltaY = dy;

            //Console.WriteLine("DELTA X: {0} Y: {1}", dx, dy);

            if (trackballEnabled)
            {
                // Fill trackball entry
                int iIndex = trackData.trackballBufferTail;
                double trackBallScale = touchpadDefinition.trackballScale;
                trackData.trackballXBuffer[iIndex] = (dx * trackBallScale) / touchFrame.timeElapsed;
                trackData.trackballYBuffer[iIndex] = (dy * trackBallScale) / touchFrame.timeElapsed;
                trackData.trackballBufferTail = (iIndex + 1) % TRACKBALL_BUFFER_LEN;
                if (trackData.trackballBufferHead == trackData.trackballBufferTail)
                    trackData.trackballBufferHead = (trackData.trackballBufferHead + 1) % TRACKBALL_BUFFER_LEN;
            }

            TouchMoveMouse(mapper, dx, dy, ref touchFrame);
        }

        private void TouchMoveMouse(Mapper mapper, int dx, int dy, ref TouchEventFrame touchFrame)
        {
            //const int deadZone = 18;
            //const int deadZone = 12;
            //const int deadZone = 8;
            int deadZone = this.deadZone;

            double tempAngle = Math.Atan2(-dy, dx);
            double normX = Math.Abs(Math.Cos(tempAngle));
            double normY = Math.Abs(Math.Sin(tempAngle));
            int signX = Math.Sign(dx);
            int signY = Math.Sign(dy);

            double timeElapsed = touchFrame.timeElapsed;
            double oldTimeElapsed = timeElapsed;
            timeElapsed = timeElapsed - (mapper.remainderCutoff(timeElapsed * 10000.0, 1.0) / 10000.0);
            double padWidth = touchpadDefinition.xAxis.max - (double)touchpadDefinition.xAxis.min;
            double coefficient = mapper.ActionProfile.CalibCounts / padWidth;
            if (AccelCurve == GyroMouseAccelCurveChoice.None)
            {
                coefficient *= swipesPer360;
            }

            //double offset = TOUCHPAD_MOUSE_OFFSET;
            double offset = touchpadDefinition.mouseOffset;
            // Base speed 8 ms
            //double tempDouble = timeElapsed * touchpadDefinition.elapsedReference;
            double tempDouble = 1.0;

            int deadzoneX = (int)Math.Abs(normX * deadZone);
            int deadzoneY = (int)Math.Abs(normY * deadZone);

            if (Math.Abs(dx) > deadzoneX)
            {
                dx -= signX * deadzoneX;
            }
            else
            {
                dx = 0;
            }

            if (Math.Abs(dy) > deadzoneY)
            {
                dy -= signY * deadzoneY;
            }
            else
            {
                dy = 0;
            }

            if (verticalDeadZone > 0 && Math.Abs(dy) < verticalDeadZone) dy = 0;

            double movementX = dx;
            double movementY = dy;
            AngleSnapping.Apply(ref movementX, ref movementY,
                trackpadAngleSnapDegrees, trackpadSmoothAngleSnap);

            double snappedMagnitude = Math.Sqrt((movementX * movementX) +
                (movementY * movementY));
            if (snappedMagnitude > 0.0)
            {
                normX = Math.Abs(movementX) / snappedMagnitude;
                normY = Math.Abs(movementY) / snappedMagnitude;
                signX = Math.Sign(movementX);
                signY = Math.Sign(movementY);
            }

            double finalCoefficientX = coefficient;
            double finalCoefficientY = coefficient;
            if (touchpadDefinition.throttleRelMouse)
            {
                double sensMulti = 1.0;
                double distSquared = (movementX * movementX) + (movementY * movementY);
                //Trace.WriteLine($"{Math.Sqrt(distSquared)}");
                double testThreshold = touchpadDefinition.throttleRelMouseZone;
                double testSquared = testThreshold * testThreshold;

                if (distSquared != 0.0 && distSquared < testSquared)
                //if (distSquared != 0.0)// && distSquared < testSquared)
                {
                    double dist = Math.Sqrt(distSquared);
                    //double alpha = (dist - 0.0) / testThreshold;
                    double alpha = 0.0;
                    double distPastMin = (dist - 0.0);
                    double baconator = distPastMin / testThreshold;
                    double ratio = distPastMin / testThreshold;

                    /*
                    // Experimental Natural Curve changes
                    {
                        double sensRange = 1.0 - 0.0;
                        double temp = Math.Log(2.0) / 700.0;
                        sensMulti = 1.0 - sensRange * Math.Exp(-temp * distPastMin);

                        //Trace.WriteLine($"{distPastMin} {sensMulti} {Math.Exp(-temp * distPastMin)}");
                    }
                    */

                    //double pastMinThreshold = dist - activeMinThreshold;
                    //double ratio = pastMinThreshold / mouseParams.powerVRef;
                    double x = Math.Pow(ratio, touchpadDefinition.throttleRelMousePower);
                    alpha = 1.0 - Math.Exp(-x);
                    alpha = Math.Clamp(alpha, 0.0, 1.0);
                    //alpha = alpha * alpha;

                    // Alpha will likely max out around 0.65. Change max sens value to compensate
                    sensMulti = 0.0 + (1.45 - 0.0) * alpha;
                    sensMulti = Math.Clamp(sensMulti, 0.0, 1.0);
                    //Trace.WriteLine($"{baconator} {ratio} {alpha} {-x} {Math.Exp(-x)}");

                    finalCoefficientX = finalCoefficientY =
                        coefficient * sensMulti;
                }
            }

            if (AccelCurve != GyroMouseAccelCurveChoice.None)
            {
                double baseXSensitivity = Math.Clamp(
                    swipesPer360, 0.0, 100.0);
                double baseYSensitivity = Math.Clamp(
                    swipesPer360 * verticalScale, 0.0, 100.0);
                MouseAcceleration.CalculateMultipliers(
                    AccelCurve,
                    snappedMagnitude,
                    MinAccelThreshold,
                    MaxAccelThreshold,
                    baseXSensitivity,
                    MaxAccelXSens,
                    baseYSensitivity,
                    MaxAccelYSens,
                    PowerVRef,
                    PowerExponent,
                    NaturalVHalf,
                    out double accelMultiplierX,
                    out double accelMultiplierY);
                finalCoefficientX *= accelMultiplierX;
                finalCoefficientY *= accelMultiplierY;
            }

            double fakeXAng = movementX / (65535.0 / 360.0);
            double fakeYAng = movementY / (65535.0 / 360.0);

            //Trace.WriteLine($"DX {dx} {fakeXAng}");

            double xMotion = movementX != 0 ? finalCoefficientX * (movementX * tempDouble)
                + (normX * (offset * signX)) : 0;

            double yMotion = movementY != 0 ? finalCoefficientY * (movementY * tempDouble)
                + (normY * (offset * signY)) : 0;
            if (AccelCurve == GyroMouseAccelCurveChoice.None &&
                verticalScale != DEFAULT_VERTICAL_SCALE)
            {
                yMotion *= verticalScale;
            }

            this.xMotion = xMotion; this.yMotion = yMotion;
        }

        private void ProcessTrackballFrame(Mapper mapper, ref TouchEventFrame touchFrame)
        {
            double tempAngle = Math.Atan2(-trackData.trackballYVel, trackData.trackballXVel);
            double normX = Math.Abs(Math.Cos(tempAngle));
            double normY = Math.Abs(Math.Sin(tempAngle));
            int signX = Math.Sign(trackData.trackballXVel);
            int signY = Math.Sign(trackData.trackballYVel);

            double trackXvDecay = Math.Min(Math.Abs(trackData.trackballXVel), trackData.trackballAccel * touchFrame.timeElapsed * normX);
            double trackYvDecay = Math.Min(Math.Abs(trackData.trackballYVel), trackData.trackballAccel * touchFrame.timeElapsed * normY);
            double xVNew = trackData.trackballXVel - (trackXvDecay * signX);
            double yVNew = trackData.trackballYVel - (trackYvDecay * signY);
            double trackballScale = touchpadDefinition.trackballScale;
            double xMotion = (xVNew * touchFrame.timeElapsed) / trackballScale;
            double yMotion = (yVNew * touchFrame.timeElapsed) / trackballScale;
            if (xMotion != 0.0)
            {
                xMotion += trackData.trackballDXRemain;
            }
            else
            {
                trackData.trackballDXRemain = 0.0;
            }

            int dx = (int)xMotion;
            trackData.trackballDXRemain = xMotion - dx;

            if (yMotion != 0.0)
            {
                yMotion += trackData.trackballDYRemain;
            }
            else
            {
                trackData.trackballDYRemain = 0.0;
            }

            int dy = (int)yMotion;
            trackData.trackballDYRemain = yMotion - dy;

            trackData.trackballXVel = xVNew;
            trackData.trackballYVel = yVNew;

            //Console.WriteLine("DX: {0} DY: {1}", dx, dy);

            if (dx == 0 && dy == 0)
            {
                trackData.trackballActive = false;
                //Console.WriteLine("ENDING TRACK");
            }
            else
            {
                TouchMoveMouse(mapper, dx, dy, ref touchFrame);
            }
        }

        public override void SoftCopyFromParent(TouchpadMapAction parentAction)
        {
            if (parentAction is TouchpadMouse tempMouseAction)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                tempMouseAction.hasLayeredAction = true;
                mappingId = tempMouseAction.mappingId;

                this.touchpadDefinition = new TouchpadDefinition(tempMouseAction.touchpadDefinition);

                tempMouseAction.NotifyPropertyChanged += TempMouseAction_NotifyPropertyChanged;

                // Determine the set with properties that should inherit
                // from the parent action
                IEnumerable<string> useParentProList =
                    fullPropertySet.Except(changedProperties);

                foreach (string parentPropType in useParentProList)
                {
                    switch(parentPropType)
                    {
                        case PropertyKeyStrings.NAME:
                            name = tempMouseAction.name;
                            break;
                        case PropertyKeyStrings.DEAD_ZONE:
                            deadZone = tempMouseAction.deadZone;
                            break;
                        case PropertyKeyStrings.VERTICAL_DEAD_ZONE:
                            verticalDeadZone = tempMouseAction.verticalDeadZone;
                            break;
                        case PropertyKeyStrings.ANGLE_SNAP_DEGREES:
                            trackpadAngleSnapDegrees = tempMouseAction.trackpadAngleSnapDegrees;
                            break;
                        case PropertyKeyStrings.SMOOTH_ANGLE_SNAP:
                            trackpadSmoothAngleSnap = tempMouseAction.trackpadSmoothAngleSnap;
                            break;
                        case PropertyKeyStrings.TRACKBALL_MODE:
                            trackballEnabled = tempMouseAction.trackballEnabled;
                            // Copy parent ref
                            trackData = tempMouseAction.trackData;
                            break;
                        case PropertyKeyStrings.TRACKBALL_FRICTION:
                            trackballFriction = tempMouseAction.trackballFriction;
                            useParentTrackFriction = true;
                            CalcTrackAccel();
                            break;
                        case PropertyKeyStrings.SWIPES_PER_360:
                            swipesPer360 = tempMouseAction.swipesPer360;
                            break;
                        case PropertyKeyStrings.VERTICAL_SCALE:
                            verticalScale = tempMouseAction.verticalScale;
                            break;
                        case PropertyKeyStrings.SMOOTHING_ENABLED:
                            smoothingEnabled = tempMouseAction.smoothingEnabled;
                            break;
                        case PropertyKeyStrings.SMOOTHING_FILTER:
                            smoothingFilterSettings.minCutOff = tempMouseAction.smoothingFilterSettings.minCutOff;
                            smoothingFilterSettings.beta = tempMouseAction.smoothingFilterSettings.beta;
                            smoothingFilterSettings.UpdateSmoothingFilters();
                            useParentSmoothingFilter = true;
                            break;
                        case PropertyKeyStrings.ACCEL_CURVE:
                            AccelCurve = tempMouseAction.AccelCurve;
                            break;
                        case PropertyKeyStrings.MIN_ACCEL_X_SENS:
                            MinAccelXSens = tempMouseAction.MinAccelXSens;
                            break;
                        case PropertyKeyStrings.MAX_ACCEL_X_SENS:
                            MaxAccelXSens = tempMouseAction.MaxAccelXSens;
                            break;
                        case PropertyKeyStrings.MIN_ACCEL_Y_SENS:
                            MinAccelYSens = tempMouseAction.MinAccelYSens;
                            break;
                        case PropertyKeyStrings.MAX_ACCEL_Y_SENS:
                            MaxAccelYSens = tempMouseAction.MaxAccelYSens;
                            break;
                        case PropertyKeyStrings.MIN_ACCEL_THRESHOLD:
                            MinAccelThreshold = tempMouseAction.MinAccelThreshold;
                            break;
                        case PropertyKeyStrings.MAX_ACCEL_THRESHOLD:
                            MaxAccelThreshold = tempMouseAction.MaxAccelThreshold;
                            break;
                        case PropertyKeyStrings.POWER_CURVE_VREF:
                            PowerVRef = tempMouseAction.PowerVRef;
                            break;
                        case PropertyKeyStrings.POWER_CURVE_EXPONENT:
                            PowerExponent = tempMouseAction.PowerExponent;
                            break;
                        case PropertyKeyStrings.NATURAL_CURVE_VHALF:
                            NaturalVHalf = tempMouseAction.NaturalVHalf;
                            break;
                        case PropertyKeyStrings.STABILITY_MODE:
                        case PropertyKeyStrings.STABILITY_TOUCH_SETTLE:
                        case PropertyKeyStrings.STABILITY_NOISE:
                        case PropertyKeyStrings.STABILITY_EDGE_GUARD:
                        case PropertyKeyStrings.STABILITY_EDGE_START_GATE:
                        case PropertyKeyStrings.STABILITY_STATIONARY:
                        case PropertyKeyStrings.STABILITY_DELTA_CLAMP:
                            CopyStabilityGroupFromParent(tempMouseAction, parentPropType);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void CopyStabilityGroupFromParent(TouchpadMouse parent, string propertyName)
        {
            stabilitySettings.CopyGroupFrom(parent.stabilitySettings, propertyName);
        }

        private void TempMouseAction_NotifyPropertyChanged(object sender, NotifyPropertyChangeArgs e)
        {
            CascadePropertyChange(e.Mapper, e.PropertyName);
        }

        private void CalcTrackAccel()
        {
            if (trackballFriction >= 100)
            {
                // Friction at ceiling — decay exceeds any realistic velocity in one tick
                trackData.trackballAccel = 1e9;
            }
            else
            {
                trackData.trackballAccel = TRACKBALL_RADIUS * trackballFriction / TRACKBALL_INERTIA;
            }
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

            TouchpadMouse tempMouseAction = parentAction as TouchpadMouse;

            switch (propertyName)
            {
                case PropertyKeyStrings.NAME:
                    name = tempMouseAction.name;
                    break;
                case PropertyKeyStrings.DEAD_ZONE:
                    deadZone = tempMouseAction.deadZone;
                    break;
                case PropertyKeyStrings.VERTICAL_DEAD_ZONE:
                    verticalDeadZone = tempMouseAction.verticalDeadZone;
                    break;
                case PropertyKeyStrings.ANGLE_SNAP_DEGREES:
                    trackpadAngleSnapDegrees = tempMouseAction.trackpadAngleSnapDegrees;
                    break;
                case PropertyKeyStrings.SMOOTH_ANGLE_SNAP:
                    trackpadSmoothAngleSnap = tempMouseAction.trackpadSmoothAngleSnap;
                    break;
                case PropertyKeyStrings.TRACKBALL_MODE:
                    if (active)
                    {
                        Release(mapper, ignoreReleaseActions: true);
                    }

                    trackballEnabled = tempMouseAction.trackballEnabled;
                    // Copy parent ref
                    trackData = tempMouseAction.trackData;
                    break;
                case PropertyKeyStrings.TRACKBALL_FRICTION:
                    if (active)
                    {
                        Release(mapper, ignoreReleaseActions: true);
                    }

                    trackballFriction = tempMouseAction.trackballFriction;
                    useParentTrackFriction = true;
                    CalcTrackAccel();
                    break;
                case PropertyKeyStrings.SWIPES_PER_360:
                    swipesPer360 = tempMouseAction.swipesPer360;
                    break;
                case PropertyKeyStrings.VERTICAL_SCALE:
                    verticalScale = tempMouseAction.verticalScale;
                    break;
                case PropertyKeyStrings.SMOOTHING_ENABLED:
                    smoothingEnabled = tempMouseAction.smoothingEnabled;
                    break;
                case PropertyKeyStrings.SMOOTHING_FILTER:
                    smoothingFilterSettings.minCutOff = tempMouseAction.smoothingFilterSettings.minCutOff;
                    smoothingFilterSettings.beta = tempMouseAction.smoothingFilterSettings.beta;
                    smoothingFilterSettings.UpdateSmoothingFilters();
                    useParentSmoothingFilter = true;
                    break;
                case PropertyKeyStrings.ACCEL_CURVE:
                    AccelCurve = tempMouseAction.AccelCurve;
                    break;
                case PropertyKeyStrings.MIN_ACCEL_X_SENS:
                    MinAccelXSens = tempMouseAction.MinAccelXSens;
                    break;
                case PropertyKeyStrings.MAX_ACCEL_X_SENS:
                    MaxAccelXSens = tempMouseAction.MaxAccelXSens;
                    break;
                case PropertyKeyStrings.MIN_ACCEL_Y_SENS:
                    MinAccelYSens = tempMouseAction.MinAccelYSens;
                    break;
                case PropertyKeyStrings.MAX_ACCEL_Y_SENS:
                    MaxAccelYSens = tempMouseAction.MaxAccelYSens;
                    break;
                case PropertyKeyStrings.MIN_ACCEL_THRESHOLD:
                    MinAccelThreshold = tempMouseAction.MinAccelThreshold;
                    break;
                case PropertyKeyStrings.MAX_ACCEL_THRESHOLD:
                    MaxAccelThreshold = tempMouseAction.MaxAccelThreshold;
                    break;
                case PropertyKeyStrings.POWER_CURVE_VREF:
                    PowerVRef = tempMouseAction.PowerVRef;
                    break;
                case PropertyKeyStrings.POWER_CURVE_EXPONENT:
                    PowerExponent = tempMouseAction.PowerExponent;
                    break;
                case PropertyKeyStrings.NATURAL_CURVE_VHALF:
                    NaturalVHalf = tempMouseAction.NaturalVHalf;
                    break;
                case PropertyKeyStrings.STABILITY_MODE:
                case PropertyKeyStrings.STABILITY_TOUCH_SETTLE:
                case PropertyKeyStrings.STABILITY_NOISE:
                case PropertyKeyStrings.STABILITY_EDGE_GUARD:
                case PropertyKeyStrings.STABILITY_EDGE_START_GATE:
                case PropertyKeyStrings.STABILITY_STATIONARY:
                case PropertyKeyStrings.STABILITY_DELTA_CLAMP:
                    CopyStabilityGroupFromParent(tempMouseAction, propertyName);
                    stabilityFilter.Reset();
                    break;
                default:
                    break;
            }
        }
    }
}
