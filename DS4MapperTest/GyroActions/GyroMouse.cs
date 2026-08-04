using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sensorit.Base;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.Common;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.GyroActions
{
    public enum GyroMouseXAxisChoice
    {
        Yaw,
        Roll,
    }

    public enum GyroMouseAccelCurveChoice
    {
        None,
        Linear,
        Quadratic,
        Cubic,
        Power,
        Natural,
    }

    public struct SmoothingFilterSettings
    {
        public const double DEFAULT_MIN_CUTOFF = 1.5;
        public const double DEFAULT_BETA = 0.8;

        public OneEuroFilter filterX;
        public OneEuroFilter filterY;

        public double minCutOff;
        public double beta;

        public SmoothingFilterSettings()
        {
            minCutOff = DEFAULT_MIN_CUTOFF;
            beta = DEFAULT_BETA;
        }

        public void Init()
        {
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

    public struct GyroMouseParams
    {
        public const bool JITTER_COMPENSATION_DEFAULT = true;
        public const double DEAD_ZONE_DEFAULT = 0.2;
        public const double REAL_WORLD_CALIBRATION_DEFAULT = 45.4545;
        public const double IN_GAME_SENS_DEFAULT = 0.54;
        public const double COUNTS_CALIBRATION_DEFAULT = 30303.0303;
        public const GyroMouseAccelCurveChoice ACCEL_CURVE_DEFAULT =
            GyroMouseAccelCurveChoice.None;
        public const double SENSITIVITY_DEFAULT = 4.0;
        public const double VERTICAL_SCALE_DEFAULT = 0.6;
        public const double MIN_ACCEL_SENS_DEFAULT = SENSITIVITY_DEFAULT;
        public const double MAX_ACCEL_SENS_DEFAULT = SENSITIVITY_DEFAULT;
        public const double MIN_GYRO_THRESHOLD_DEFAULT = 0.0;
        public const double MAX_GYRO_THRESHOLD_DEFAULT = 0.0;
        public const double POWER_VREF_DEFAULT = 1.0;
        public const double POWER_EXPONENT_DEFAULT = 1.0;
        public const double NATURAL_VHALF_DEFAULT = 20.0;
        public const bool MULTIPLIER_COMPENSATION_DEFAULT = false;
        public const double ACCELERATION_MULTIPLIER_DEFAULT = 1.0;
        public const double VERTICAL_ACCELERATION_MULTIPLIER_DEFAULT = 1.0;
        public const bool VERTICAL_ACCELERATION_SCALE_MODE_DEFAULT = true;

        public double deadzone;
        public double verticalDeadZone;
        public double gyroAngleSnapDegrees;
        public bool gyroSmoothAngleSnap;
        public JoypadActionCodes[] gyroTriggerButtons;
        public bool andCond;
        public bool triggerActivates;
        public int activationHoldMs;
        public double realWorldCalibration;
        public double inGameSens;
        public GyroMouseAccelCurveChoice accelCurve;
        public double minGyroThreshold;
        public double maxGyroThreshold;
        public double minAccelXSens;
        public double maxAccelXSens;
        public double minAccelYSens;
        public double maxAccelYSens;
        public double powerVRef;
        public double powerExponent;
        public double naturalVHalf;
        public double sensitivity;
        public double verticalScale;
        // Legacy inversion/axis-selection fields. Retained only for backward-compatible
        // profile deserialization and migration into `orientation` (see
        // GyroMouseSerializer.MigrateLegacyOrientation) - no longer read by Prepare/Event.
        public bool invertX;
        public bool invertY;
        public GyroMouseXAxisChoice useForXAxis;
        public GyroOrientationSettings orientation;
        public double minThreshold;
        public bool toggleAction;
        public bool smoothing;
        public bool jitterCompensation;
        public bool multiplierCompensation;
        public double accelerationMultiplier;
        public double verticalAccelerationMultiplier;
        public bool verticalAccelerationScaleMode;
        public SmoothingFilterSettings smoothingFilterSettings;
        public TriggerSensitivityModifierSettings triggerSensitivityModifier;
        //public double oneEuroMinCutoff;
        //public double oneEuroMinBeta;
    }

    public class GyroMouse : GyroMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string DEAD_ZONE = "DeadZone";
            public const string VERTICAL_DEAD_ZONE = "VerticalDeadZone";
            public const string ANGLE_SNAP_DEGREES = "AngleSnapDegrees";
            public const string SMOOTH_ANGLE_SNAP = "SmoothAngleSnap";
            public const string SENSITIVITY = "Sensitivity";
            public const string VERTICAL_SCALE = "VerticalScale";
            public const string INVERT_X = "InvertX";
            public const string INVERT_Y = "InvertY";
            public const string X_AXIS = "XAxis";
            public const string GYRO_SPACE = "GyroSpace";
            public const string HORIZONTAL_CONTROL = "HorizontalControl";
            public const string VERTICAL_CONTROL = "VerticalControl";
            public const string HORIZONTAL_INVERT = "HorizontalInvert";
            public const string VERTICAL_INVERT = "VerticalInvert";
            public const string HORIZONTAL_YAW_CONTRIBUTION = "HorizontalYawContribution";
            public const string HORIZONTAL_ROLL_CONTRIBUTION = "HorizontalRollContribution";
            public const string VERTICAL_YAW_CONTRIBUTION = "VerticalYawContribution";
            public const string VERTICAL_ROLL_CONTRIBUTION = "VerticalRollContribution";
            public const string MIN_THRESHOLD = "MinThreshold";
            public const string REAL_WORLD_CALIBRATION = "RealWorldCalibration";
            public const string ACCEL_CURVE = "AccelCurve";
            public const string IN_GAME_SENS = "InGameSens";
            public const string MIN_ACCEL_X_SENS = "MinAccelXSens";
            public const string MAX_ACCEL_X_SENS = "MaxAccelXSens";
            public const string MIN_ACCEL_Y_SENS = "MinAccelYSens";
            public const string MAX_ACCEL_Y_SENS = "MaxAccelYSens";
            public const string MIN_GYRO_THRESHOLD = "MinGyroThreshold";
            public const string MAX_GYRO_THRESHOLD = "MaxGyroThreshold";
            public const string POWER_CURVE_VREF = "PowerCurveVRef";
            public const string POWER_CURVE_EXPONENT = "PowerCurveExponent";
            public const string NATURAL_CURVE_VHALF = "NaturalCurveVHalf";

            public const string TRIGGER_BUTTONS = "Triggers";
            public const string TRIGGER_ACTIVATE = "TriggersActivate";
            public const string ACTIVATION_HOLD_MS = "ActivationHoldMs";
            public const string TRIGGER_EVAL_COND = "TriggersEvalCond";
            public const string TOGGLE_ACTION = "ToggleAction";
            public const string JITTER_COMPENSATION = "JitterCompensation";
            public const string MULTIPLIER_COMPENSATION = "MultiplierCompensation";
            public const string ACCELERATION_MULTIPLIER = "AccelerationMultiplier";
            public const string VERTICAL_ACCELERATION_MULTIPLIER = "VerticalAccelerationMultiplier";
            public const string VERTICAL_ACCELERATION_SCALE_MODE = "VerticalAccelerationScaleMode";
            public const string SMOOTHING_ENABLED = "SmoothingEnabled";
            public const string SMOOTHING_FILTER = "SmoothingFilter";
            public const string TRIGGER_SENSITIVITY_MODIFIER = "TriggerSensitivityModifier";
            //public const string SMOOTHING_MINCUTOFF = "SmoothingMinCutoff";
            //public const string SMOOTHING_MINBETA = "SmoothingMinBeta";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.VERTICAL_DEAD_ZONE,
            PropertyKeyStrings.ANGLE_SNAP_DEGREES,
            PropertyKeyStrings.SMOOTH_ANGLE_SNAP,
            PropertyKeyStrings.SENSITIVITY,
            PropertyKeyStrings.VERTICAL_SCALE,
            PropertyKeyStrings.INVERT_X,
            PropertyKeyStrings.INVERT_Y,
            PropertyKeyStrings.X_AXIS,
            PropertyKeyStrings.GYRO_SPACE,
            PropertyKeyStrings.HORIZONTAL_CONTROL,
            PropertyKeyStrings.VERTICAL_CONTROL,
            PropertyKeyStrings.HORIZONTAL_INVERT,
            PropertyKeyStrings.VERTICAL_INVERT,
            PropertyKeyStrings.HORIZONTAL_YAW_CONTRIBUTION,
            PropertyKeyStrings.HORIZONTAL_ROLL_CONTRIBUTION,
            PropertyKeyStrings.VERTICAL_YAW_CONTRIBUTION,
            PropertyKeyStrings.VERTICAL_ROLL_CONTRIBUTION,
            PropertyKeyStrings.MIN_THRESHOLD,
            PropertyKeyStrings.REAL_WORLD_CALIBRATION,
            PropertyKeyStrings.IN_GAME_SENS,
            PropertyKeyStrings.ACCEL_CURVE,
            PropertyKeyStrings.MIN_ACCEL_X_SENS,
            PropertyKeyStrings.MAX_ACCEL_X_SENS,
            PropertyKeyStrings.MIN_ACCEL_Y_SENS,
            PropertyKeyStrings.MAX_ACCEL_Y_SENS,
            PropertyKeyStrings.MIN_GYRO_THRESHOLD,
            PropertyKeyStrings.MAX_GYRO_THRESHOLD,
            PropertyKeyStrings.POWER_CURVE_VREF,
            PropertyKeyStrings.POWER_CURVE_EXPONENT,
            PropertyKeyStrings.NATURAL_CURVE_VHALF,
            PropertyKeyStrings.TRIGGER_BUTTONS,
            PropertyKeyStrings.TRIGGER_ACTIVATE,
            PropertyKeyStrings.ACTIVATION_HOLD_MS,
            PropertyKeyStrings.TRIGGER_EVAL_COND,
            PropertyKeyStrings.TOGGLE_ACTION,
            PropertyKeyStrings.SMOOTHING_ENABLED,
            PropertyKeyStrings.SMOOTHING_FILTER,
            PropertyKeyStrings.TRIGGER_SENSITIVITY_MODIFIER,
            PropertyKeyStrings.MULTIPLIER_COMPENSATION,
            PropertyKeyStrings.ACCELERATION_MULTIPLIER,
            PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER,
            PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE,
            //PropertyKeyStrings.SMOOTHING_MINCUTOFF,
            //PropertyKeyStrings.SMOOTHING_MINBETA,
        };

        public const string ACTION_TYPE_NAME = "GyroMouseAction";
        private const bool DEFAULT_SMOOTHING_ENABLED = false;

        private double xMotion;
        private double yMotion;
        public GyroMouseParams mouseParams;
        private bool previousTriggerActivated;
        private bool toggleActiveState;
        private readonly GyroActivationHold activationHold = new GyroActivationHold();
        private bool useParentSmoothingFilter;

        //private OneEuroFilter smoothFilter = new OneEuroFilter(1.0, 1.0);

        public GyroMouse()
        {
            actionTypeName = ACTION_TYPE_NAME;
            mouseParams = new GyroMouseParams()
            {
                sensitivity = GyroMouseParams.SENSITIVITY_DEFAULT,
                deadzone = GyroMouseParams.DEAD_ZONE_DEFAULT,
                verticalDeadZone = 0.0,
                gyroAngleSnapDegrees = 0.0,
                gyroSmoothAngleSnap = false,
                realWorldCalibration = GyroMouseParams.REAL_WORLD_CALIBRATION_DEFAULT,
                inGameSens = GyroMouseParams.IN_GAME_SENS_DEFAULT,
                accelCurve = GyroMouseParams.ACCEL_CURVE_DEFAULT,
                minGyroThreshold = GyroMouseParams.MIN_GYRO_THRESHOLD_DEFAULT,
                maxGyroThreshold = GyroMouseParams.MAX_GYRO_THRESHOLD_DEFAULT,
                minAccelXSens = GyroMouseParams.MIN_ACCEL_SENS_DEFAULT,
                minAccelYSens = GyroMouseParams.VERTICAL_SCALE_DEFAULT,
                maxAccelXSens = GyroMouseParams.MAX_ACCEL_SENS_DEFAULT,
                maxAccelYSens = GyroMouseParams.VERTICAL_SCALE_DEFAULT,
                powerExponent = GyroMouseParams.POWER_EXPONENT_DEFAULT,
                powerVRef = GyroMouseParams.POWER_VREF_DEFAULT,
                naturalVHalf = GyroMouseParams.NATURAL_VHALF_DEFAULT,
                verticalScale = GyroMouseParams.VERTICAL_SCALE_DEFAULT,
                triggerActivates = true,
                activationHoldMs = 0,
                andCond = false,
                gyroTriggerButtons = new JoypadActionCodes[1]
                {
                    JoypadActionCodes.AlwaysOn,
                },
                jitterCompensation = false,
                smoothing = DEFAULT_SMOOTHING_ENABLED,
                multiplierCompensation = GyroMouseParams.MULTIPLIER_COMPENSATION_DEFAULT,
                accelerationMultiplier = GyroMouseParams.ACCELERATION_MULTIPLIER_DEFAULT,
                verticalAccelerationMultiplier = GyroMouseParams.VERTICAL_ACCELERATION_MULTIPLIER_DEFAULT,
                verticalAccelerationScaleMode = GyroMouseParams.VERTICAL_ACCELERATION_SCALE_MODE_DEFAULT,
                triggerSensitivityModifier = new TriggerSensitivityModifierSettings(
                    GyroMouseParams.SENSITIVITY_DEFAULT),
            };

            mouseParams.smoothingFilterSettings = new SmoothingFilterSettings();
            mouseParams.smoothingFilterSettings.Init();
            mouseParams.orientation = GyroOrientationSettings.CreateDefault();
            onlyOnPrimary = true;
        }

        public GyroMouse(GyroMouseParams mouseParams)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.mouseParams = mouseParams;
            onlyOnPrimary = true;
        }

        public GyroMouse(GyroMouse parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.parentAction = parentAction;
            this.mouseParams = parentAction.mouseParams;
            onlyOnPrimary = true;
        }

        public override void Prepare(Mapper mapper, ref GyroEventFrame gyroFrame, bool alterState = true)
        {
            //const int deadZone = 28;
            //const int deadZone = 18;
            const double GYRO_MOUSE_COEFFICIENT = 0.025;
            const double GYRO_MOUSE_OFFSET = 0.3;
            //const double GYRO_MOUSE_OFFSET = 0.0;

            JoypadActionCodes[] tempTriggerButtons = mouseParams.gyroTriggerButtons;
            //bool triggerButtonActive = tempTriggerButton == JoypadActionCodes.Empty ||
            //    mapper.IsButtonActive(mouseParams.gyroTriggerButton);

            bool triggerButtonActive = mapper.IsButtonsActiveDraft(tempTriggerButtons,
                mouseParams.andCond);

            bool triggerActivated = true;
            if (!mouseParams.triggerActivates && triggerButtonActive)
            {
                triggerActivated = false;
                //previousTriggerActivated = triggerActivated;
            }
            else if (mouseParams.triggerActivates && !triggerButtonActive)
            {
                triggerActivated = false;
                //previousTriggerActivated = triggerActivated;
            }

            if (mouseParams.toggleAction)
            {
                if (triggerActivated && triggerActivated != previousTriggerActivated)
                {
                    toggleActiveState = !toggleActiveState;
                }

                previousTriggerActivated = triggerActivated;
                triggerActivated = toggleActiveState;
            }
            else
            {
                previousTriggerActivated = triggerActivated;
            }

            triggerActivated = activationHold.Update(triggerActivated,
                mouseParams.activationHoldMs, gyroFrame.timeElapsed);

            if (!triggerActivated)
            {
                mapper.MouseXRemainder = mapper.MouseYRemainder = 0.0;
                mouseParams.smoothingFilterSettings.filterX.Filter(0.0, mapper.CurrentRate);
                mouseParams.smoothingFilterSettings.filterY.Filter(0.0, mapper.CurrentRate);

                active = false;
                activeEvent = false;
                return;
            }

            double offset = gyroSensDefinition.mouseOffset;
            //double coefficient = gyroSensDefinition.mouseCoefficient * mouseParams.sensitivity;
            //double coefficient = (120.0 / 3.0) * mouseParams.sensitivity; // RWC / InGameSens * sens_multiplier

            // RWC / InGameSens * sens_multiplier
            //double coefficient = (mouseParams.realWorldCalibration / mouseParams.inGameSens) * mouseParams.sensitivity;
            double coefficient = (mouseParams.realWorldCalibration / mouseParams.inGameSens);
            double sensMulti = mouseParams.sensitivity;
            double effectiveSensitivity = TriggerSensitivityModifier.Evaluate(
                mouseParams.triggerSensitivityModifier, sensMulti,
                mapper.GetNormalisedTriggerPosition(mouseParams.triggerSensitivityModifier.trigger));
            double triggerSensitivityScale = sensMulti > 0.0
                ? effectiveSensitivity / sensMulti : 1.0;
            double verticalTriggerSensitivityScale = mouseParams.triggerSensitivityModifier.modifyVerticalSensitivity
                ? triggerSensitivityScale : 1.0;
            double deadZone = mouseParams.deadzone;

            double timeElapsed = gyroFrame.timeElapsed;
            double oldTimeElapsed = timeElapsed;
            timeElapsed = timeElapsed - (mapper.remainderCutoff(timeElapsed * 10000.0, 1.0) / 10000.0);
            //Trace.WriteLine($"BEFORE: {oldTimeElapsed} | AFTER {timeElapsed}");
            //Trace.WriteLine(timeElapsed);
            //double timeElapsed = current.timeElapsed;
            // Take possible lag state into account. Main routine will make sure to skip this method
            //if (previous.timeElapsed <= 0.002)
            //{
            //    timeElapsed += previous.timeElapsed;
            //    currentRate = 1.0 / timeElapsed;
            //}

            // Base speed 5 ms
            //double tempDouble = timeElapsed * 3 * 66.67;
            //double tempDouble = timeElapsed * 3 * gyroFrame.elapsedReference;
            //double tempDouble = timeElapsed * gyroFrame.elapsedReference;
            double tempDouble = 1.0;
            int deltaX = (int)Math.Round(GyroOrientationResolver.Resolve(mouseParams.orientation.horizontal,
                gyroFrame.GyroYaw, gyroFrame.GyroRoll, gyroFrame.GyroPitch));
            int deltaY = (int)Math.Round(GyroOrientationResolver.Resolve(mouseParams.orientation.vertical,
                gyroFrame.GyroYaw, gyroFrame.GyroRoll, gyroFrame.GyroPitch));

            double tempAngle = Math.Atan2(-deltaY, deltaX);
            double normX = Math.Abs(Math.Cos(tempAngle));
            double normY = Math.Abs(Math.Sin(tempAngle));
            int signX = Math.Sign(deltaX);
            int signY = Math.Sign(deltaY);

            double deltaAngVelX = GyroOrientationResolver.Resolve(mouseParams.orientation.horizontal,
                gyroFrame.AngGyroYaw, gyroFrame.AngGyroRoll, gyroFrame.AngGyroPitch);
            double deltaAngVelY = GyroOrientationResolver.Resolve(mouseParams.orientation.vertical,
                gyroFrame.AngGyroYaw, gyroFrame.AngGyroRoll, gyroFrame.AngGyroPitch);

            //Trace.WriteLine($"{deltaX} {deltaY}");

            double deadzoneX = Math.Abs(normX * deadZone);
            double deadzoneY = Math.Abs(normY * deadZone);

            //Trace.WriteLine($"{gyroFrame.AngGyroYaw} {deltaX} {deadZone} {deadzoneX} {deadzoneY}");

            if (Math.Abs(deltaAngVelX) > deadzoneX)
            {
                deltaAngVelX -= signX * deadzoneX;
            }
            else
            {
                deltaAngVelX = 0;
            }

            if (Math.Abs(deltaAngVelY) > deadzoneY)
            {
                deltaAngVelY -= signY * deadzoneY;
            }
            else
            {
                deltaAngVelY = 0;
            }

            if (mouseParams.verticalDeadZone > 0.0 && Math.Abs(deltaAngVelY) < mouseParams.verticalDeadZone) deltaAngVelY = 0;

            AngleSnapping.Apply(ref deltaAngVelX, ref deltaAngVelY,
                mouseParams.gyroAngleSnapDegrees, mouseParams.gyroSmoothAngleSnap);

            if (mouseParams.gyroAngleSnapDegrees > 0.0)
            {
                double snappedMagnitude = Math.Sqrt((deltaAngVelX * deltaAngVelX) +
                    (deltaAngVelY * deltaAngVelY));
                if (snappedMagnitude > 0.0)
                {
                    normX = Math.Abs(deltaAngVelX) / snappedMagnitude;
                    normY = Math.Abs(deltaAngVelY) / snappedMagnitude;
                    signX = Math.Sign(deltaAngVelX);
                    signY = Math.Sign(deltaAngVelY);
                }
            }

            //double slope = (1.0 - 0.40) / (11.25 - 0.0);
            //double intercept = slope - 0.40;
            //double dps_test = 180.0 / 16.0;

            //if (deltaAngVelX != 0 && (deltaAngVelX * signX) < (dps_test * normX))
            //{
            //    deltaAngVelX = ((slope * Math.Abs(deltaAngVelX) - intercept) * deltaAngVelX);
            //    //Trace.WriteLine($"DANGEROUS: {deltaAngVelX}");
            //}

            //if (deltaAngVelY != 0 && (deltaAngVelY * signY) < (dps_test * normY))
            //{
            //    deltaAngVelY = ((slope * Math.Abs(deltaAngVelY) - intercept) * deltaAngVelY);
            //}

            //double finalCoefficient = coefficient * sensMulti;
            const double minThreshold = 0.0; // dps
            const double maxThreshold = 11.25; // dps

            double modSensMultiX = 1.0;
            double modSensMultiY = 1.0;
            if (mouseParams.accelCurve == GyroMouseAccelCurveChoice.None)
            {
                modSensMultiX = mouseParams.sensitivity;
                modSensMultiY = mouseParams.sensitivity;
            }
            else
            {
                double activeMinThreshold = Math.Min(mouseParams.minGyroThreshold,
                    mouseParams.maxGyroThreshold);
                double activeMaxThreshold = Math.Max(mouseParams.minGyroThreshold,
                    mouseParams.maxGyroThreshold);
                double minXSens = mouseParams.minAccelXSens;
                double maxXSens = mouseParams.maxAccelXSens;
                double minYSens = mouseParams.minAccelYSens;
                double maxYSens = mouseParams.maxAccelYSens;

                //double modSensMulti = 1.0;
                //double modSensMulti = minSens;
                modSensMultiX = minXSens;
                modSensMultiY = minYSens;

                double minThresSquared = activeMinThreshold * activeMinThreshold;
                double distSquared = (deltaAngVelX * deltaAngVelX) + (deltaAngVelY * deltaAngVelY);
                bool isPastMinThreshold = distSquared >= minThresSquared;
                if (isPastMinThreshold)
                {
                    //double alphaX = deltaAngVelX / dps_test;
                    //double alphaY = deltaAngVelY / dps_test;

                    //double dps_test = 180.0 / 16.0; // ~11.25 dps
                    double dps_test = activeMaxThreshold - activeMinThreshold;
                    double dpsTestSquared = dps_test * dps_test;
                    double dist = Math.Sqrt(distSquared);
                    double pastMinThreshold = dist - activeMinThreshold;
                    bool filled = false;
                    double alpha = 0.0;

                    switch (mouseParams.accelCurve)
                    {
                        case GyroMouseAccelCurveChoice.Linear:
                            if (pastMinThreshold < dps_test)
                            {
                                alpha = (dist - activeMinThreshold) / dps_test;
                            }
                            else
                            {
                                alpha = 1.0;
                            }

                            break;

                        case GyroMouseAccelCurveChoice.Quadratic:
                            if (pastMinThreshold < dps_test)
                            {
                                alpha = (dist - activeMinThreshold) / dps_test;
                                alpha = alpha * alpha;
                            }
                            else
                            {
                                alpha = 1.0;
                            }

                            break;
                        case GyroMouseAccelCurveChoice.Cubic:
                            if (pastMinThreshold < dps_test)
                            {
                                alpha = (dist - activeMinThreshold) / dps_test;
                                alpha = alpha * alpha * alpha;
                            }
                            else
                            {
                                alpha = 1.0;
                            }

                            break;
                        case GyroMouseAccelCurveChoice.Power:
                            double ratio = pastMinThreshold / mouseParams.powerVRef;
                            double x = Math.Pow(ratio, mouseParams.powerExponent);
                            alpha = 1.0 - Math.Exp(-x);
                            alpha = Math.Clamp(alpha, 0.0, 1.0);

                            break;
                        case GyroMouseAccelCurveChoice.Natural:
                            if (mouseParams.naturalVHalf <= 0.0)
                            {
                                modSensMultiX = maxXSens;
                                modSensMultiY = maxYSens;
                                break;
                            }

                            double sensRangeX = maxXSens - minXSens;
                            double sensRangeY = maxYSens - minYSens;
                            double temp = Math.Log(2.0) / mouseParams.naturalVHalf;
                            modSensMultiX = maxXSens - sensRangeX * Math.Exp(-temp * pastMinThreshold);
                            modSensMultiY = maxYSens - sensRangeY * Math.Exp(-temp * pastMinThreshold);
                            filled = true;

                            break;
                        default: break;
                    }

                    //Trace.WriteLine($"{deltaAngVelX} {deltaAngVelY} {distSquared} {alpha}");
                    //modSensMulti = 0.4 + (1.0 - 0.4) * alpha;
                    if (!filled)
                    {
                        modSensMultiX = minXSens + (maxXSens - minXSens) * alpha;
                        modSensMultiY = minYSens + (maxYSens - minYSens) * alpha;
                    }
                }
                //else if (isPastMinThreshold)
                //{
                //    modSensMultiX = maxXSens;
                //    modSensMultiY = maxYSens;
                //}
            }

            // Find degrees displacement for gamepad poll
            double xAng = deltaAngVelX * timeElapsed;
            double yAng = deltaAngVelY * timeElapsed;

            //double finalCoefficient = coefficient * sensMulti * modSensMulti;
            double finalCoefficient = coefficient * modSensMultiX;
            double finalCoefficientY = coefficient * modSensMultiY;
            finalCoefficient *= triggerSensitivityScale;
            finalCoefficientY *= verticalTriggerSensitivityScale;
            if (mouseParams.multiplierCompensation)
            {
                double accelMultiplier = Math.Clamp(mouseParams.accelerationMultiplier,
                    0.01, 100.0);
                double verticalAccelMultiplier = Math.Clamp(
                    mouseParams.verticalAccelerationMultiplier, 0.01, 100.0);
                finalCoefficient /= accelMultiplier;
                finalCoefficientY /= verticalAccelMultiplier;
            }

            xMotion = deltaAngVelX != 0 ? finalCoefficient * (xAng * tempDouble)
                + (normX * (offset * signX)) : 0;

            yMotion = deltaAngVelY != 0 ? finalCoefficientY * (yAng * tempDouble)
                + (normY * (offset * signY)) : 0;

            if (mouseParams.accelCurve == GyroMouseAccelCurveChoice.None)
            {
                double vertMultiplier = mouseParams.sensitivity > 0.0
                    ? mouseParams.verticalScale / mouseParams.sensitivity
                    : mouseParams.verticalScale;
                if (vertMultiplier != 1.0)
                {
                    yMotion = vertMultiplier * yMotion;
                }
            }

            if (mouseParams.jitterCompensation)
            {
                // Possibly expose threshold later
                const double threshold = 0.48;
                const float thresholdF = (float)threshold;

                double absX = Math.Abs(xMotion);
                if (absX <= normX * threshold)
                {
                    xMotion = signX * Math.Pow(absX / thresholdF, 1.408) * threshold;
                }

                double absY = Math.Abs(yMotion);
                if (absY <= normY * threshold)
                {
                    yMotion = signY * Math.Pow(absY / thresholdF, 1.408) * threshold;
                }
            }

            if (xMotion != 0.0 || yMotion != 0.0)
            {
                active = true;
            }
            else
            {
                active = false;

                mouseParams.smoothingFilterSettings.filterX.Filter(0.0, mapper.CurrentRate);
                mouseParams.smoothingFilterSettings.filterY.Filter(0.0, mapper.CurrentRate);
            }

            activeEvent = true;
        }

        public override void Event(Mapper mapper)
        {
            double tempX = xMotion, tempY = yMotion;
            /*if (mouseParams.smoothing)
            {
                tempX = smoothFilter.Filter(xMotion, mapper.CurrentRate);
                tempY = smoothFilter.Filter(yMotion, mapper.CurrentRate);
            }
            */

            // Inversion is resolved at the source in Prepare() via GyroOrientationResolver,
            // not here - legacy invertX/invertY are migration-only and not read here.
            double outXMotion = tempX;
            double outYMotion = tempY;

            bool mouseSync = true;
            if (mouseParams.minThreshold != 1.0)
            {
                double distSqu = (xMotion * xMotion) + (yMotion * yMotion);
                if (distSqu <= (mouseParams.minThreshold * mouseParams.minThreshold))
                {
                    outXMotion = 0.0; outYMotion = 0.0;
                    mapper.MouseXRemainder = outXMotion;
                    mapper.MouseYRemainder = outYMotion;
                    mouseSync = false;
                }
            }

            //mapper.MouseX = outXMotion; mapper.MouseY = outYMotion;
            //mapper.MouseSync = mouseSync;

            if (mouseParams.smoothing)
            {
                //mapper.MouseX = outXMotion; mapper.MouseY = outYMotion;
                mapper.GenerateMouseEventFilteredV2(mouseParams.smoothingFilterSettings.filterX,
                    mouseParams.smoothingFilterSettings.filterY,
                    ref outXMotion, ref outYMotion);

                mapper.MouseX += outXMotion; mapper.MouseY += outYMotion;
                mapper.MouseSync = mouseSync;
                //mapper.MouseEventFired = true;

                //tempX = mouseParams.smoothingFilterSettings.filterX.Filter(tempX,
                //    mapper.CurrentRate);

                //tempY = mouseParams.smoothingFilterSettings.filterY.Filter(tempY,
                //    mapper.CurrentRate);
            }
            else
            {
                // Allow mapper to handle event
                mapper.MouseX += outXMotion; mapper.MouseY += outYMotion;
                mapper.MouseSync = mouseSync;
            }

            if (xMotion != 0.0 || yMotion != 0.0)
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
            toggleActiveState = false;
            previousTriggerActivated = false;
            //smoothFilter.Reset();
            mouseParams.smoothingFilterSettings.filterX.Reset();
            mouseParams.smoothingFilterSettings.filterY.Reset();
        }

        public override void SoftRelease(Mapper mapper, MapAction checkAction, bool resetState = true)
        {
            xMotion = yMotion = 0.0;
            active = false;
            activeEvent = false;
            toggleActiveState = false;
            previousTriggerActivated = false;

            if (!useParentSmoothingFilter)
            {
                //smoothFilter.Reset();
                mouseParams.smoothingFilterSettings.filterX.Reset();
                mouseParams.smoothingFilterSettings.filterY.Reset();
            }
        }

        public override void BlankEvent(Mapper mapper)
        {
            mapper.MouseXRemainder = mapper.MouseYRemainder = 0.0;
            active = false;
            activeEvent = false;
            toggleActiveState = false;
            previousTriggerActivated = false;

            if (!useParentSmoothingFilter)
            {
                //smoothFilter.Reset();
                mouseParams.smoothingFilterSettings.filterX.Reset();
                mouseParams.smoothingFilterSettings.filterY.Reset();
            }
        }

        public override GyroMapAction DuplicateAction()
        {
            return new GyroMouse(this);
        }

        public override void SoftCopyFromParent(GyroMapAction parentAction)
        {
            if (parentAction is GyroMouse tempMouseAction)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                tempMouseAction.hasLayeredAction = true;
                mappingId = tempMouseAction.mappingId;

                gyroSensDefinition = new GyroSensDefinition(tempMouseAction.gyroSensDefinition);

                tempMouseAction.NotifyPropertyChanged += TempMouseAction_NotifyPropertyChanged;

                // Determine the set with properties that should inherit
                // from the parent action
                IEnumerable<string> useParentProList =
                    fullPropertySet.Except(changedProperties);

                //bool updateSmoothing = false;
                foreach (string parentPropType in useParentProList)
                {
                    switch(parentPropType)
                    {
                        case PropertyKeyStrings.NAME:
                            name = tempMouseAction.name;
                            break;
                        case PropertyKeyStrings.DEAD_ZONE:
                            mouseParams.deadzone = tempMouseAction.mouseParams.deadzone;
                            break;
                        case PropertyKeyStrings.VERTICAL_DEAD_ZONE:
                            mouseParams.verticalDeadZone = tempMouseAction.mouseParams.verticalDeadZone;
                            break;
                        case PropertyKeyStrings.ANGLE_SNAP_DEGREES:
                            mouseParams.gyroAngleSnapDegrees = tempMouseAction.mouseParams.gyroAngleSnapDegrees;
                            break;
                        case PropertyKeyStrings.SMOOTH_ANGLE_SNAP:
                            mouseParams.gyroSmoothAngleSnap = tempMouseAction.mouseParams.gyroSmoothAngleSnap;
                            break;
                        case PropertyKeyStrings.TRIGGER_BUTTONS:
                            mouseParams.gyroTriggerButtons = tempMouseAction.mouseParams.gyroTriggerButtons;
                            break;
                        case PropertyKeyStrings.TRIGGER_ACTIVATE:
                            mouseParams.triggerActivates = tempMouseAction.mouseParams.triggerActivates;
                            break;
                        case PropertyKeyStrings.ACTIVATION_HOLD_MS:
                            mouseParams.activationHoldMs = tempMouseAction.mouseParams.activationHoldMs;
                            break;
                        case PropertyKeyStrings.TRIGGER_EVAL_COND:
                            mouseParams.andCond = tempMouseAction.mouseParams.andCond;
                            break;
                        case PropertyKeyStrings.REAL_WORLD_CALIBRATION:
                            mouseParams.realWorldCalibration = tempMouseAction.mouseParams.realWorldCalibration;
                            break;
                        case PropertyKeyStrings.IN_GAME_SENS:
                            mouseParams.inGameSens = tempMouseAction.mouseParams.inGameSens;
                            break;
                        case PropertyKeyStrings.ACCEL_CURVE:
                            mouseParams.accelCurve = tempMouseAction.mouseParams.accelCurve;
                            break;
                        case PropertyKeyStrings.MIN_ACCEL_X_SENS:
                            mouseParams.minAccelXSens = tempMouseAction.mouseParams.minAccelXSens;
                            break;
                        case PropertyKeyStrings.MAX_ACCEL_X_SENS:
                            mouseParams.maxAccelXSens = tempMouseAction.mouseParams.maxAccelXSens;
                            break;
                        case PropertyKeyStrings.MIN_ACCEL_Y_SENS:
                            mouseParams.minAccelYSens = tempMouseAction.mouseParams.minAccelYSens;
                            break;
                        case PropertyKeyStrings.MAX_ACCEL_Y_SENS:
                            mouseParams.maxAccelYSens = tempMouseAction.mouseParams.maxAccelYSens;
                            break;
                        case PropertyKeyStrings.MIN_GYRO_THRESHOLD:
                            mouseParams.minGyroThreshold = tempMouseAction.mouseParams.minGyroThreshold;
                            break;
                        case PropertyKeyStrings.MAX_GYRO_THRESHOLD:
                            mouseParams.maxGyroThreshold = tempMouseAction.mouseParams.maxGyroThreshold;
                            break;
                        case PropertyKeyStrings.POWER_CURVE_VREF:
                            mouseParams.powerVRef = tempMouseAction.mouseParams.powerVRef;
                            break;
                        case PropertyKeyStrings.POWER_CURVE_EXPONENT:
                            mouseParams.powerExponent = tempMouseAction.mouseParams.powerExponent;
                            break;
                        case PropertyKeyStrings.NATURAL_CURVE_VHALF:
                            mouseParams.naturalVHalf = tempMouseAction.mouseParams.naturalVHalf;
                            break;
                        case PropertyKeyStrings.SENSITIVITY:
                            mouseParams.sensitivity = tempMouseAction.mouseParams.sensitivity;
                            break;
                        case PropertyKeyStrings.VERTICAL_SCALE:
                            mouseParams.verticalScale = tempMouseAction.mouseParams.verticalScale;
                            break;
                        case PropertyKeyStrings.INVERT_X:
                            mouseParams.invertX = tempMouseAction.mouseParams.invertX;
                            break;
                        case PropertyKeyStrings.INVERT_Y:
                            mouseParams.invertY = tempMouseAction.mouseParams.invertY;
                            break;
                        case PropertyKeyStrings.X_AXIS:
                            mouseParams.useForXAxis = tempMouseAction.mouseParams.useForXAxis;
                            break;
                        case PropertyKeyStrings.GYRO_SPACE:
                            mouseParams.orientation.gyroSpace = tempMouseAction.mouseParams.orientation.gyroSpace;
                            break;
                        case PropertyKeyStrings.HORIZONTAL_CONTROL:
                            mouseParams.orientation.horizontal.source = tempMouseAction.mouseParams.orientation.horizontal.source;
                            break;
                        case PropertyKeyStrings.VERTICAL_CONTROL:
                            mouseParams.orientation.vertical.source = tempMouseAction.mouseParams.orientation.vertical.source;
                            break;
                        case PropertyKeyStrings.HORIZONTAL_INVERT:
                            mouseParams.orientation.horizontal.invertSingle = tempMouseAction.mouseParams.orientation.horizontal.invertSingle;
                            break;
                        case PropertyKeyStrings.VERTICAL_INVERT:
                            mouseParams.orientation.vertical.invertSingle = tempMouseAction.mouseParams.orientation.vertical.invertSingle;
                            break;
                        case PropertyKeyStrings.HORIZONTAL_YAW_CONTRIBUTION:
                            mouseParams.orientation.horizontal.yawContribution = tempMouseAction.mouseParams.orientation.horizontal.yawContribution;
                            break;
                        case PropertyKeyStrings.HORIZONTAL_ROLL_CONTRIBUTION:
                            mouseParams.orientation.horizontal.rollContribution = tempMouseAction.mouseParams.orientation.horizontal.rollContribution;
                            break;
                        case PropertyKeyStrings.VERTICAL_YAW_CONTRIBUTION:
                            mouseParams.orientation.vertical.yawContribution = tempMouseAction.mouseParams.orientation.vertical.yawContribution;
                            break;
                        case PropertyKeyStrings.VERTICAL_ROLL_CONTRIBUTION:
                            mouseParams.orientation.vertical.rollContribution = tempMouseAction.mouseParams.orientation.vertical.rollContribution;
                            break;
                        case PropertyKeyStrings.MIN_THRESHOLD:
                            mouseParams.minThreshold = tempMouseAction.mouseParams.minThreshold;
                            break;
                        case PropertyKeyStrings.TOGGLE_ACTION:
                            mouseParams.toggleAction = tempMouseAction.mouseParams.toggleAction;
                            ResetToggleActiveState();
                            break;
                        case PropertyKeyStrings.JITTER_COMPENSATION:
                            mouseParams.jitterCompensation = tempMouseAction.mouseParams.jitterCompensation;
                            break;
                        case PropertyKeyStrings.SMOOTHING_ENABLED:
                            mouseParams.smoothing = tempMouseAction.mouseParams.smoothing;
                            break;
                        case PropertyKeyStrings.SMOOTHING_FILTER:
                            mouseParams.smoothingFilterSettings.minCutOff = tempMouseAction.mouseParams.smoothingFilterSettings.minCutOff;
                            mouseParams.smoothingFilterSettings.beta = tempMouseAction.mouseParams.smoothingFilterSettings.beta;
                            mouseParams.smoothingFilterSettings.UpdateSmoothingFilters();
                            useParentSmoothingFilter = true;
                            break;
                        case PropertyKeyStrings.MULTIPLIER_COMPENSATION:
                            mouseParams.multiplierCompensation = tempMouseAction.mouseParams.multiplierCompensation;
                            break;
                        case PropertyKeyStrings.ACCELERATION_MULTIPLIER:
                            mouseParams.accelerationMultiplier = tempMouseAction.mouseParams.accelerationMultiplier;
                            break;
                        case PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER:
                            mouseParams.verticalAccelerationMultiplier = tempMouseAction.mouseParams.verticalAccelerationMultiplier;
                            break;
                        case PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE:
                            mouseParams.verticalAccelerationScaleMode = tempMouseAction.mouseParams.verticalAccelerationScaleMode;
                            break;
                        //case PropertyKeyStrings.SMOOTHING_MINCUTOFF:
                        //    mouseParams.oneEuroMinCutoff = tempMouseAction.mouseParams.oneEuroMinCutoff;
                        //    updateSmoothing = true;
                        //    break;
                        //case PropertyKeyStrings.SMOOTHING_MINBETA:
                        //    mouseParams.oneEuroMinBeta = tempMouseAction.mouseParams.oneEuroMinBeta;
                        //    updateSmoothing = true;
                        //    break;
                        default:
                            break;
                    }
                }

                //if (updateSmoothing)
                //{
                //    UpdateSmoothingFilter();
                //}
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

            GyroMouse tempMouseAction = parentAction as GyroMouse;

            //bool updateSmoothing = false;
            switch (propertyName)
            {
                case PropertyKeyStrings.NAME:
                    name = tempMouseAction.name;
                    break;
                case PropertyKeyStrings.DEAD_ZONE:
                    mouseParams.deadzone = tempMouseAction.mouseParams.deadzone;
                    break;
                case PropertyKeyStrings.VERTICAL_DEAD_ZONE:
                    mouseParams.verticalDeadZone = tempMouseAction.mouseParams.verticalDeadZone;
                    break;
                case PropertyKeyStrings.ANGLE_SNAP_DEGREES:
                    mouseParams.gyroAngleSnapDegrees = tempMouseAction.mouseParams.gyroAngleSnapDegrees;
                    break;
                case PropertyKeyStrings.SMOOTH_ANGLE_SNAP:
                    mouseParams.gyroSmoothAngleSnap = tempMouseAction.mouseParams.gyroSmoothAngleSnap;
                    break;
                case PropertyKeyStrings.TRIGGER_BUTTONS:
                    mouseParams.gyroTriggerButtons = tempMouseAction.mouseParams.gyroTriggerButtons;
                    break;
                case PropertyKeyStrings.TRIGGER_ACTIVATE:
                    mouseParams.triggerActivates = tempMouseAction.mouseParams.triggerActivates;
                    break;
                case PropertyKeyStrings.ACTIVATION_HOLD_MS:
                    mouseParams.activationHoldMs = tempMouseAction.mouseParams.activationHoldMs;
                    break;
                case PropertyKeyStrings.TRIGGER_EVAL_COND:
                    mouseParams.andCond = tempMouseAction.mouseParams.andCond;
                    break;
                case PropertyKeyStrings.REAL_WORLD_CALIBRATION:
                    mouseParams.realWorldCalibration = tempMouseAction.mouseParams.realWorldCalibration;
                    break;
                case PropertyKeyStrings.IN_GAME_SENS:
                    mouseParams.inGameSens = tempMouseAction.mouseParams.inGameSens;
                    break;
                case PropertyKeyStrings.ACCEL_CURVE:
                    mouseParams.accelCurve = tempMouseAction.mouseParams.accelCurve;
                    break;
                case PropertyKeyStrings.MIN_ACCEL_X_SENS:
                    mouseParams.minAccelXSens = tempMouseAction.mouseParams.minAccelXSens;
                    break;
                case PropertyKeyStrings.MAX_ACCEL_X_SENS:
                    mouseParams.maxAccelXSens = tempMouseAction.mouseParams.maxAccelXSens;
                    break;
                case PropertyKeyStrings.MIN_ACCEL_Y_SENS:
                    mouseParams.minAccelYSens = tempMouseAction.mouseParams.minAccelYSens;
                    break;
                case PropertyKeyStrings.MAX_ACCEL_Y_SENS:
                    mouseParams.maxAccelYSens = tempMouseAction.mouseParams.maxAccelYSens;
                    break;
                case PropertyKeyStrings.MIN_GYRO_THRESHOLD:
                    mouseParams.minGyroThreshold = tempMouseAction.mouseParams.minGyroThreshold;
                    break;
                case PropertyKeyStrings.MAX_GYRO_THRESHOLD:
                    mouseParams.maxGyroThreshold = tempMouseAction.mouseParams.maxGyroThreshold;
                    break;
                case PropertyKeyStrings.POWER_CURVE_VREF:
                    mouseParams.powerVRef = tempMouseAction.mouseParams.powerVRef;
                    break;
                case PropertyKeyStrings.POWER_CURVE_EXPONENT:
                    mouseParams.powerExponent = tempMouseAction.mouseParams.powerExponent;
                    break;
                case PropertyKeyStrings.NATURAL_CURVE_VHALF:
                    mouseParams.naturalVHalf = tempMouseAction.mouseParams.naturalVHalf;
                    break;
                case PropertyKeyStrings.SENSITIVITY:
                    mouseParams.sensitivity = tempMouseAction.mouseParams.sensitivity;
                    break;
                case PropertyKeyStrings.VERTICAL_SCALE:
                    mouseParams.verticalScale = tempMouseAction.mouseParams.verticalScale;
                    break;
                case PropertyKeyStrings.INVERT_X:
                    mouseParams.invertX = tempMouseAction.mouseParams.invertX;
                    break;
                case PropertyKeyStrings.INVERT_Y:
                    mouseParams.invertY = tempMouseAction.mouseParams.invertY;
                    break;
                case PropertyKeyStrings.X_AXIS:
                    mouseParams.useForXAxis = tempMouseAction.mouseParams.useForXAxis;
                    break;
                case PropertyKeyStrings.GYRO_SPACE:
                    mouseParams.orientation.gyroSpace = tempMouseAction.mouseParams.orientation.gyroSpace;
                    break;
                case PropertyKeyStrings.HORIZONTAL_CONTROL:
                    mouseParams.orientation.horizontal.source = tempMouseAction.mouseParams.orientation.horizontal.source;
                    break;
                case PropertyKeyStrings.VERTICAL_CONTROL:
                    mouseParams.orientation.vertical.source = tempMouseAction.mouseParams.orientation.vertical.source;
                    break;
                case PropertyKeyStrings.HORIZONTAL_INVERT:
                    mouseParams.orientation.horizontal.invertSingle = tempMouseAction.mouseParams.orientation.horizontal.invertSingle;
                    break;
                case PropertyKeyStrings.VERTICAL_INVERT:
                    mouseParams.orientation.vertical.invertSingle = tempMouseAction.mouseParams.orientation.vertical.invertSingle;
                    break;
                case PropertyKeyStrings.HORIZONTAL_YAW_CONTRIBUTION:
                    mouseParams.orientation.horizontal.yawContribution = tempMouseAction.mouseParams.orientation.horizontal.yawContribution;
                    break;
                case PropertyKeyStrings.HORIZONTAL_ROLL_CONTRIBUTION:
                    mouseParams.orientation.horizontal.rollContribution = tempMouseAction.mouseParams.orientation.horizontal.rollContribution;
                    break;
                case PropertyKeyStrings.VERTICAL_YAW_CONTRIBUTION:
                    mouseParams.orientation.vertical.yawContribution = tempMouseAction.mouseParams.orientation.vertical.yawContribution;
                    break;
                case PropertyKeyStrings.VERTICAL_ROLL_CONTRIBUTION:
                    mouseParams.orientation.vertical.rollContribution = tempMouseAction.mouseParams.orientation.vertical.rollContribution;
                    break;
                case PropertyKeyStrings.MIN_THRESHOLD:
                    mouseParams.minThreshold = tempMouseAction.mouseParams.minThreshold;
                    break;
                case PropertyKeyStrings.TOGGLE_ACTION:
                    mouseParams.toggleAction = tempMouseAction.mouseParams.toggleAction;
                    ResetToggleActiveState();
                    break;
                case PropertyKeyStrings.JITTER_COMPENSATION:
                    mouseParams.jitterCompensation = tempMouseAction.mouseParams.jitterCompensation;
                    break;
                case PropertyKeyStrings.SMOOTHING_ENABLED:
                    mouseParams.smoothing = tempMouseAction.mouseParams.smoothing;
                    //updateSmoothing = true;
                    break;
                case PropertyKeyStrings.SMOOTHING_FILTER:
                    mouseParams.smoothingFilterSettings.minCutOff = tempMouseAction.mouseParams.smoothingFilterSettings.minCutOff;
                    mouseParams.smoothingFilterSettings.beta = tempMouseAction.mouseParams.smoothingFilterSettings.beta;
                    mouseParams.smoothingFilterSettings.UpdateSmoothingFilters();
                    useParentSmoothingFilter = true;
                    break;
                case PropertyKeyStrings.MULTIPLIER_COMPENSATION:
                    mouseParams.multiplierCompensation = tempMouseAction.mouseParams.multiplierCompensation;
                    break;
                case PropertyKeyStrings.ACCELERATION_MULTIPLIER:
                    mouseParams.accelerationMultiplier = tempMouseAction.mouseParams.accelerationMultiplier;
                    break;
                case PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER:
                    mouseParams.verticalAccelerationMultiplier = tempMouseAction.mouseParams.verticalAccelerationMultiplier;
                    break;
                case PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE:
                    mouseParams.verticalAccelerationScaleMode = tempMouseAction.mouseParams.verticalAccelerationScaleMode;
                    break;
                //case PropertyKeyStrings.SMOOTHING_MINCUTOFF:
                //    mouseParams.oneEuroMinCutoff = tempMouseAction.mouseParams.oneEuroMinCutoff;
                //    updateSmoothing = true;
                //    break;
                //case PropertyKeyStrings.SMOOTHING_MINBETA:
                //    mouseParams.oneEuroMinBeta = tempMouseAction.mouseParams.oneEuroMinBeta;
                //    updateSmoothing = true;
                //    break;
                default:
                    break;
            }

            //if (updateSmoothing)
            //{
            //    UpdateSmoothingFilter();
            //}
        }

        private void ResetToggleActiveState()
        {
            toggleActiveState = false;
            previousTriggerActivated = false;
        }

        //public void UpdateSmoothingFilter()
        //{
        //    smoothFilter = new OneEuroFilter(mouseParams.oneEuroMinCutoff,
        //        mouseParams.oneEuroMinBeta);
        //}
    }
}
