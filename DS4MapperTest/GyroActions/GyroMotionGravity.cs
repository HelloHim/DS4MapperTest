using System;

namespace DS4MapperTest.GyroActions
{
    /// <summary>
    /// Direct C# port of the gravity-tracking half of Jibb Smart's GamepadMotion.hpp
    /// (MIT licence, https://github.com/JibbSmart/GamepadMotionHelpers), version 9.
    ///
    /// Only <c>Motion::Update</c> is ported - the auto-calibration and orientation
    /// accessors are intentionally omitted because this app performs its own gyro
    /// calibration upstream. The values fed in here are expected to be ALREADY
    /// calibrated (drift-corrected), exactly as JoyShockMapper feeds
    /// GetCalibratedGyro() output into its gyro space code.
    ///
    /// Coordinate space is the DualShock 4 / GamepadMotion space (Y-up):
    ///   X = pitch axis, Y = yaw axis, Z = roll axis.
    ///   Gyro in degrees/second. Accelerometer in g (1g = 9.8 m/s^2).
    ///   Resting flat and face-up, accel reads approximately (0, +1, 0)
    ///   and the resulting Grav vector settles at approximately (0, -1, 0).
    /// </summary>
    public class GyroMotionGravity
    {
        // GamepadMotionSettings defaults, copied verbatim so behaviour matches JSM.
        private const double GRAVITY_CORRECTION_SHAKINESS_MAX_THRESHOLD = 0.4;
        private const double GRAVITY_CORRECTION_SHAKINESS_MIN_THRESHOLD = 0.01;
        private const double GRAVITY_CORRECTION_STILL_SPEED = 1.0;
        private const double GRAVITY_CORRECTION_SHAKY_SPEED = 0.1;
        private const double GRAVITY_CORRECTION_GYRO_FACTOR = 0.1;
        private const double GRAVITY_CORRECTION_GYRO_MIN_THRESHOLD = 0.05;
        private const double GRAVITY_CORRECTION_GYRO_MAX_THRESHOLD = 0.25;
        private const double GRAVITY_CORRECTION_MINIMUM_SPEED = 0.01;
        private const double SHORT_STEADINESS_HALF_TIME = 0.25;

        // Assumed gravity magnitude. GamepadMotion derives this from its own
        // calibration samples and falls back to 1.0 when uncalibrated; we always
        // use 1.0 because calibration is handled elsewhere in this app.
        private const double GRAVITY_LENGTH = 1.0;

        private Quat quaternion = Quat.Identity;
        private Vec grav;
        private Vec accel;
        private Vec smoothAccel;
        private double shakiness;

        /// <summary>Current gravity estimate, GamepadMotion space. Not normalised.</summary>
        public Vec Grav => grav;

        /// <summary>True once gravity has converged enough to be usable.</summary>
        public bool HasGravity => grav.LengthSquared() > 0.0;

        public void Reset()
        {
            quaternion = Quat.Identity;
            grav = new Vec();
            accel = new Vec();
            smoothAccel = new Vec();
            shakiness = 0.0;
        }

        /// <summary>
        /// Feed one sample. Call this every polling tick for the device, whether or
        /// not gyro output is currently active, so that the gravity estimate stays
        /// converged across gyro-button releases.
        /// </summary>
        /// <param name="gyroX">Pitch rate, deg/s, GamepadMotion space.</param>
        /// <param name="gyroY">Yaw rate, deg/s, GamepadMotion space.</param>
        /// <param name="gyroZ">Roll rate, deg/s, GamepadMotion space.</param>
        /// <param name="accelX">Accel X in g, GamepadMotion space.</param>
        /// <param name="accelY">Accel Y in g, GamepadMotion space.</param>
        /// <param name="accelZ">Accel Z in g, GamepadMotion space.</param>
        /// <param name="deltaTime">Seconds since previous sample.</param>
        public void Update(double gyroX, double gyroY, double gyroZ,
            double accelX, double accelY, double accelZ, double deltaTime)
        {
            if (deltaTime <= 0.0)
            {
                return;
            }

            // All zeroes are almost certainly not valid inputs.
            if (gyroX == 0.0 && gyroY == 0.0 && gyroZ == 0.0 &&
                accelX == 0.0 && accelY == 0.0 && accelZ == 0.0)
            {
                return;
            }

            Vec axis = new Vec(gyroX, gyroY, gyroZ);
            Vec inAccel = new Vec(accelX, accelY, accelZ);

            double angleSpeed = axis.Length() * Math.PI / 180.0;
            double angle = angleSpeed * deltaTime;

            // Rotate. Local rotation, so post-multiply.
            Quat rotation = Quat.AngleAxis(angle, axis.x, axis.y, axis.z);
            quaternion *= rotation;

            double accelMagnitude = inAccel.Length();
            if (accelMagnitude > 0.0)
            {
                Vec accelNorm = inAccel / accelMagnitude;

                // Account for rotation when tracking smoothed acceleration.
                smoothAccel *= rotation.Inverse();

                double smoothFactor = SHORT_STEADINESS_HALF_TIME <= 0.0
                    ? 0.0
                    : Math.Pow(2.0, -deltaTime / SHORT_STEADINESS_HALF_TIME);
                shakiness *= smoothFactor;
                shakiness = Math.Max(shakiness, (inAccel - smoothAccel).Length());
                smoothAccel = inAccel.Lerp(smoothAccel, smoothFactor);

                grav *= rotation.Inverse();

                // Close the gap between grav and raw acceleration.
                Vec gravToAccel = (accelNorm * -GRAVITY_LENGTH) - grav;
                Vec gravToAccelDir = gravToAccel.Normalized();

                double gravCorrectionSpeed;
                if (GRAVITY_CORRECTION_SHAKINESS_MIN_THRESHOLD < GRAVITY_CORRECTION_SHAKINESS_MAX_THRESHOLD)
                {
                    double t = Clamp((shakiness - GRAVITY_CORRECTION_SHAKINESS_MIN_THRESHOLD) /
                        (GRAVITY_CORRECTION_SHAKINESS_MAX_THRESHOLD - GRAVITY_CORRECTION_SHAKINESS_MIN_THRESHOLD), 0.0, 1.0);
                    gravCorrectionSpeed = GRAVITY_CORRECTION_STILL_SPEED +
                        (GRAVITY_CORRECTION_SHAKY_SPEED - GRAVITY_CORRECTION_STILL_SPEED) * t;
                }
                else
                {
                    gravCorrectionSpeed = shakiness < GRAVITY_CORRECTION_SHAKINESS_MAX_THRESHOLD
                        ? GRAVITY_CORRECTION_STILL_SPEED : GRAVITY_CORRECTION_SHAKY_SPEED;
                }

                // Limit correction to a proportion of the gyro rate, or the minimum speed.
                double gyroGravCorrectionLimit = Math.Max(
                    angleSpeed * GRAVITY_CORRECTION_GYRO_FACTOR, GRAVITY_CORRECTION_MINIMUM_SPEED);
                if (gravCorrectionSpeed > gyroGravCorrectionLimit)
                {
                    double closeEnoughFactor;
                    if (GRAVITY_CORRECTION_GYRO_MIN_THRESHOLD < GRAVITY_CORRECTION_GYRO_MAX_THRESHOLD)
                    {
                        closeEnoughFactor = Clamp((gravToAccel.Length() - GRAVITY_CORRECTION_GYRO_MIN_THRESHOLD) /
                            (GRAVITY_CORRECTION_GYRO_MAX_THRESHOLD - GRAVITY_CORRECTION_GYRO_MIN_THRESHOLD), 0.0, 1.0);
                    }
                    else
                    {
                        closeEnoughFactor = gravToAccel.Length() < GRAVITY_CORRECTION_GYRO_MAX_THRESHOLD ? 0.0 : 1.0;
                    }

                    gravCorrectionSpeed = gyroGravCorrectionLimit +
                        (gravCorrectionSpeed - gyroGravCorrectionLimit) * closeEnoughFactor;
                }

                Vec gravToAccelDelta = gravToAccelDir * gravCorrectionSpeed * deltaTime;
                if (gravToAccelDelta.LengthSquared() < gravToAccel.LengthSquared())
                {
                    grav += gravToAccelDelta;
                }
                else
                {
                    grav = accelNorm * -GRAVITY_LENGTH;
                }

                // Correct the orientation quaternion against the measured gravity
                // direction. Note the details: GamepadMotion rotates gravity by the
                // orientation itself (not its inverse), takes the cross product in
                // the order gravityDirection x down, and applies the correction as a
                // world-space pre-multiply. Reversing any of the three silently
                // produces a different orientation, and none of it is caught by the
                // gravity output because Quaternion never feeds back into Grav.
                Vec gravityDirection = grav.Normalized() * quaternion;
                Vec down = new Vec(0.0, -1.0, 0.0);
                double errorAngle = Math.Acos(Clamp(down.Dot(gravityDirection), -1.0, 1.0));
                Vec flattened = gravityDirection.Cross(down);
                Quat correctionQuat = Quat.AngleAxis(errorAngle, flattened.x, flattened.y, flattened.z);
                quaternion = correctionQuat * quaternion;

                accel = inAccel + grav;
            }
            else
            {
                grav *= rotation.Inverse();
                accel = grav;
            }

            quaternion.Normalize();
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        public struct Vec
        {
            public double x, y, z;

            public Vec(double inX, double inY, double inZ)
            {
                x = inX; y = inY; z = inZ;
            }

            public double Length() => Math.Sqrt(x * x + y * y + z * z);
            public double LengthSquared() => x * x + y * y + z * z;
            public double Dot(Vec o) => x * o.x + y * o.y + z * o.z;
            public Vec Cross(Vec o) => new Vec(
                y * o.z - z * o.y,
                z * o.x - x * o.z,
                x * o.y - y * o.x);

            public void Normalize()
            {
                double length = Length();
                if (length == 0.0) return;
                double f = 1.0 / length;
                x *= f; y *= f; z *= f;
            }

            public Vec Normalized()
            {
                Vec r = this;
                r.Normalize();
                return r;
            }

            public Vec Lerp(Vec other, double factor) =>
                this + (other - this) * factor;

            public static Vec operator +(Vec a, Vec b) => new Vec(a.x + b.x, a.y + b.y, a.z + b.z);
            public static Vec operator -(Vec a, Vec b) => new Vec(a.x - b.x, a.y - b.y, a.z - b.z);
            public static Vec operator *(Vec a, double s) => new Vec(a.x * s, a.y * s, a.z * s);
            public static Vec operator /(Vec a, double s) => new Vec(a.x / s, a.y / s, a.z / s);

            // Rotate a vector by a quaternion: q * v * q^-1
            public static Vec operator *(Vec v, Quat q)
            {
                Quat temp = q * new Quat(0.0, v.x, v.y, v.z) * q.Inverse();
                return new Vec(temp.x, temp.y, temp.z);
            }
        }

        public struct Quat
        {
            public double w, x, y, z;

            public Quat(double inW, double inX, double inY, double inZ)
            {
                w = inW; x = inX; y = inY; z = inZ;
            }

            public static Quat Identity => new Quat(1.0, 0.0, 0.0, 0.0);

            public static Quat AngleAxis(double angle, double inX, double inY, double inZ)
            {
                double sinHalf = Math.Sin(angle * 0.5);
                Vec axis = new Vec(inX, inY, inZ);
                axis.Normalize();
                axis *= sinHalf;
                return new Quat(Math.Cos(angle * 0.5), axis.x, axis.y, axis.z);
            }

            public static Quat operator *(Quat a, Quat b) => new Quat(
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w);

            public void Normalize()
            {
                double length = Math.Sqrt(w * w + x * x + y * y + z * z);
                if (length == 0.0) return;
                double f = 1.0 / length;
                w *= f; x *= f; y *= f; z *= f;
            }

            public Quat Inverse() => new Quat(w, -x, -y, -z);
        }
    }
}
