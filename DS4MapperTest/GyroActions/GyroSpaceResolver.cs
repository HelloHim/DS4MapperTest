using System;

namespace DS4MapperTest.GyroActions
{
    /// <summary>
    /// Gravity-aware gyro spaces, ported line-for-line from JoyShockMapper's
    /// main.cpp (processGyroControls / GYRO_SPACE block) so that behaviour is
    /// identical to JSM for a user migrating a profile across.
    ///
    /// IMPORTANT: this deliberately follows JSM's main.cpp and NOT the
    /// CalculatePlayerSpaceGyro / CalculateWorldSpaceGyro helpers in
    /// GamepadMotion.hpp. They differ in ways that are audible in feel:
    ///   - JSM uses yawRelaxFactor 2.0 (60 degree buffer); GamepadMotion
    ///     defaults to 1.41 (45 degree buffer).
    ///   - JSM's PLAYER_LEAN / WORLD_LEAN variants have no GamepadMotion
    ///     equivalent at all.
    ///   - Sign conventions differ (GamepadMotion negates worldYaw).
    /// Copying main.cpp is what makes this feel the same.
    ///
    /// All inputs and outputs are in GamepadMotion / DualShock 4 space (Y-up):
    ///   gyroX = pitch rate, gyroY = yaw rate, gyroZ = roll rate (deg/s)
    ///   grav = gravity vector, need not be normalized (this normalizes it)
    /// Outputs:
    ///   outHorizontal / outVertical, in the same units JSM writes into its
    ///   gyroX / gyroY accumulators, ready to be used as mouse deltas.
    /// </summary>
    public static class GyroSpaceResolver
    {
        // JSM main.cpp: const float yawRelaxFactor = 2.f; // 60 degree buffer
        public const double YAW_RELAX_FACTOR = 2.0;

        // JSM main.cpp PLAYER_LEAN: const float rollRelaxFactor = 1.41f; // 45 degree buffer
        public const double ROLL_RELAX_FACTOR = 1.41;

        // JSM main.cpp: sideReduction threshold, hardcoded 0.125
        public const double SIDE_REDUCTION_THRESHOLD = 0.125;

        public static void Resolve(GyroSpaceChoice space,
            double inGyroX, double inGyroY, double inGyroZ,
            double inGravX, double inGravY, double inGravZ,
            out double outHorizontal, out double outVertical)
        {
            double gyroX = 0.0; // horizontal / mouse X accumulator
            double gyroY = 0.0; // vertical / mouse Y accumulator

            double gravLength = Math.Sqrt(inGravX * inGravX + inGravY * inGravY + inGravZ * inGravZ);
            double normGravX = 0.0;
            double normGravY = 0.0;
            double normGravZ = 0.0;
            if (gravLength > 0.0)
            {
                double gravNormalizer = 1.0 / gravLength;
                normGravX = inGravX * gravNormalizer;
                normGravY = inGravY * gravNormalizer;
                normGravZ = inGravZ * gravNormalizer;
            }

            double flatness = Math.Abs(normGravY);
            double upness = Math.Abs(normGravZ);
            double sideReduction = Clamp(
                (Math.Max(flatness, upness) - SIDE_REDUCTION_THRESHOLD) / SIDE_REDUCTION_THRESHOLD, 0.0, 1.0);

            if (space == GyroSpaceChoice.PlayerTurn || space == GyroSpaceChoice.PlayerLean)
            {
                if (space == GyroSpaceChoice.PlayerTurn)
                {
                    // grav dot gyro axis (but only Y (yaw) and Z (roll))
                    double worldYaw = normGravY * inGyroY + normGravZ * inGyroZ;
                    double worldYawSign = worldYaw < 0.0 ? -1.0 : 1.0;
                    gyroX += worldYawSign * Math.Min(Math.Abs(worldYaw) * YAW_RELAX_FACTOR,
                        Math.Sqrt(inGyroY * inGyroY + inGyroZ * inGyroZ));
                }
                else // PlayerLean
                {
                    // project local pitch axis (X) onto gravity plane
                    double gravDotPitchAxis = normGravX;
                    double pitchAxisX = 1.0 - normGravX * gravDotPitchAxis;
                    double pitchAxisY = -normGravY * gravDotPitchAxis;
                    double pitchAxisZ = -normGravZ * gravDotPitchAxis;

                    double pitchAxisLengthSquared = pitchAxisX * pitchAxisX +
                        pitchAxisY * pitchAxisY + pitchAxisZ * pitchAxisZ;
                    if (pitchAxisLengthSquared > 0.0)
                    {
                        // world roll axis is cross (yaw, pitch)
                        double rollAxisX = pitchAxisY * normGravZ - pitchAxisZ * normGravY;
                        double rollAxisY = pitchAxisZ * normGravX - pitchAxisX * normGravZ;
                        double rollAxisZ = pitchAxisX * normGravY - pitchAxisY * normGravX;

                        double rollAxisLengthSquared = rollAxisX * rollAxisX +
                            rollAxisY * rollAxisY + rollAxisZ * rollAxisZ;
                        if (rollAxisLengthSquared > 0.0)
                        {
                            double rollAxisLength = Math.Sqrt(rollAxisLengthSquared);
                            double lengthReciprocal = 1.0 / rollAxisLength;
                            rollAxisX *= lengthReciprocal;
                            rollAxisY *= lengthReciprocal;
                            rollAxisZ *= lengthReciprocal;

                            double worldRoll = rollAxisY * inGyroY + rollAxisZ * inGyroZ;
                            double worldRollSign = worldRoll < 0.0 ? -1.0 : 1.0;
                            gyroX += worldRollSign * Math.Min(Math.Abs(worldRoll) * ROLL_RELAX_FACTOR,
                                Math.Sqrt(inGyroY * inGyroY + inGyroZ * inGyroZ));
                            gyroX *= sideReduction;
                        }
                    }
                }

                gyroY -= inGyroX;
            }
            else // WorldTurn or WorldLean
            {
                // grav dot gyro axis
                double worldYaw = normGravX * inGyroX + normGravY * inGyroY + normGravZ * inGyroZ;

                // project local pitch axis (X) onto gravity plane
                double gravDotPitchAxis = normGravX;
                double pitchAxisX = 1.0 - normGravX * gravDotPitchAxis;
                double pitchAxisY = -normGravY * gravDotPitchAxis;
                double pitchAxisZ = -normGravZ * gravDotPitchAxis;

                double pitchAxisLengthSquared = pitchAxisX * pitchAxisX +
                    pitchAxisY * pitchAxisY + pitchAxisZ * pitchAxisZ;
                if (pitchAxisLengthSquared > 0.0)
                {
                    double pitchAxisLength = Math.Sqrt(pitchAxisLengthSquared);
                    double lengthReciprocal = 1.0 / pitchAxisLength;
                    pitchAxisX *= lengthReciprocal;
                    pitchAxisY *= lengthReciprocal;
                    pitchAxisZ *= lengthReciprocal;

                    // get global pitch factor (dot)
                    gyroY = -(pitchAxisX * inGyroX + pitchAxisY * inGyroY + pitchAxisZ * inGyroZ);
                    // pinch it towards the nonsense limit
                    gyroY *= sideReduction;

                    if (space == GyroSpaceChoice.WorldLean)
                    {
                        // world roll axis is cross (yaw, pitch)
                        double rollAxisX = pitchAxisY * normGravZ - pitchAxisZ * normGravY;
                        double rollAxisY = pitchAxisZ * normGravX - pitchAxisX * normGravZ;
                        double rollAxisZ = pitchAxisX * normGravY - pitchAxisY * normGravX;

                        double rollAxisLengthSquared = rollAxisX * rollAxisX +
                            rollAxisY * rollAxisY + rollAxisZ * rollAxisZ;
                        if (rollAxisLengthSquared > 0.0)
                        {
                            double rollAxisLength = Math.Sqrt(rollAxisLengthSquared);
                            lengthReciprocal = 1.0 / rollAxisLength;
                            rollAxisX *= lengthReciprocal;
                            rollAxisY *= lengthReciprocal;
                            rollAxisZ *= lengthReciprocal;

                            // get global roll factor (dot)
                            gyroX = rollAxisX * inGyroX + rollAxisY * inGyroY + rollAxisZ * inGyroZ;
                            // pinch because we rely on a good pitch vector here
                            gyroX *= sideReduction;
                        }
                    }
                }

                if (space == GyroSpaceChoice.WorldTurn)
                {
                    gyroX += worldYaw;
                }
            }

            outHorizontal = gyroX;
            outVertical = gyroY;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }

    /// <summary>
    /// Converts this app's common GyroEventFrame axis convention into the
    /// GamepadMotion / DS4 convention that the ported JSM math expects.
    ///
    /// The job of this adapter is to reconstruct the native DualShock-convention
    /// sensor values that SDL would have handed to GamepadMotion inside JSM.
    /// The sign choices here are a first-pass derivation from the reader-level
    /// axis flips already present in this codebase (verified separately, not as
    /// part of this task) — if gravity does not settle near (0, -1, 0) with the
    /// pad resting face-up, flip signs here and only here.
    /// </summary>
    public static class GyroMotionAxisAdapter
    {
        public static void ToMotionSpace(
            double frameAngGyroYaw, double frameAngGyroPitch, double frameAngGyroRoll,
            double frameAccelXG, double frameAccelYG, double frameAccelZG,
            out double gyroX, out double gyroY, out double gyroZ,
            out double accelX, out double accelY, out double accelZ)
        {
            // Gyro: this app's common frame negates all three axes relative to the
            // native DS4 report, so negate them back.
            gyroX = -frameAngGyroPitch; // pitch -> GM X
            gyroY = -frameAngGyroYaw;   // yaw   -> GM Y
            gyroZ = -frameAngGyroRoll;  // roll  -> GM Z

            // Accel: X and Y are negated relative to native, Z passes through.
            accelX = -frameAccelXG;
            accelY = -frameAccelYG;
            accelZ = frameAccelZG;
        }

        /// <summary>
        /// Converts the resolved space output back into this app's mouse delta
        /// convention. JSM's horizontal/vertical accumulators already match this
        /// app's deltaAngVelX / deltaAngVelY sign convention, so this is a
        /// pass-through kept as a named seam in case a device needs a tweak.
        /// </summary>
        public static void FromMotionSpace(double horizontal, double vertical,
            out double deltaAngVelX, out double deltaAngVelY)
        {
            deltaAngVelX = horizontal;
            deltaAngVelY = vertical;
        }
    }
}
