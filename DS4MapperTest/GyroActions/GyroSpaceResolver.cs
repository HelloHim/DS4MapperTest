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
    ///   grav = gravity vector, need not be normalised (this normalises it)
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
    /// GamepadMotion / DS4 convention (Y-up) that the ported JSM math expects.
    ///
    /// There is no single conversion that works for every controller. Each
    /// device family's reader parses a different HID report layout and applies
    /// its own negations, so the physical meaning of a given GyroEventFrame
    /// component differs between families. A conversion derived from one
    /// device and applied to all of them lands gravity on the wrong
    /// GamepadMotion axis for the others, which makes PLAYER_TURN respond to
    /// leaning and PLAYER_LEAN respond to turning. Every family therefore gets
    /// its own method here, derived from that family's own reader source.
    ///
    /// Derivation method, per family:
    ///   1. Read the reader to find which HID word feeds which rotation
    ///      (pitch / yaw / roll) and which accel component sits on the same
    ///      physical sensor axis, then follow the negations through the reader
    ///      and the mapper's GyroEventFrame construction.
    ///   2. Anchor the frame's physical directions on the two invariants that
    ///      local space already proves on real hardware for every device:
    ///      +AngGyroYaw is a rightward turn, and +AngGyroPitch is the app's
    ///      vertical-positive pitch direction.
    ///   3. Fix the remaining axis by right-handedness, then express the
    ///      GamepadMotion triad (X = pitch axis, Y = up, Z = roll axis) in
    ///      terms of the frame's components.
    /// The target in every case is the same: resting flat and face-up, the
    /// accel output here must be approximately (0, +1, 0), so that
    /// GyroMotionGravity settles gravity at approximately (0, -1, 0).
    ///
    /// The DualSense/DS4 and Steam Controller mappings are confirmed against
    /// real hardware. Switch Pro, Joy-Con and 8BitDo Ultimate 2 Wireless are
    /// derived from source only and still want a hardware check.
    /// </summary>
    public static class GyroMotionAxisAdapter
    {
        public static void ToMotionSpace(InputDeviceType deviceType,
            double frameAngGyroYaw, double frameAngGyroPitch, double frameAngGyroRoll,
            double frameAccelXG, double frameAccelYG, double frameAccelZG,
            out double gyroX, out double gyroY, out double gyroZ,
            out double accelX, out double accelY, out double accelZ)
        {
            switch (deviceType)
            {
                case InputDeviceType.SteamController:
                case InputDeviceType.SteamControllerTriton:
                    ToMotionSpaceSteamController(
                        frameAngGyroYaw, frameAngGyroPitch, frameAngGyroRoll,
                        frameAccelXG, frameAccelYG, frameAccelZG,
                        out gyroX, out gyroY, out gyroZ,
                        out accelX, out accelY, out accelZ);
                    break;

                case InputDeviceType.SwitchPro:
                    ToMotionSpaceSwitchPro(
                        frameAngGyroYaw, frameAngGyroPitch, frameAngGyroRoll,
                        frameAccelXG, frameAccelYG, frameAccelZG,
                        out gyroX, out gyroY, out gyroZ,
                        out accelX, out accelY, out accelZ);
                    break;

                case InputDeviceType.JoyCon:
                    ToMotionSpaceJoyCon(
                        frameAngGyroYaw, frameAngGyroPitch, frameAngGyroRoll,
                        frameAccelXG, frameAccelYG, frameAccelZG,
                        out gyroX, out gyroY, out gyroZ,
                        out accelX, out accelY, out accelZ);
                    break;

                case InputDeviceType.EightBitDoUltimate2Wireless:
                    ToMotionSpaceUltimate2Wireless(
                        frameAngGyroYaw, frameAngGyroPitch, frameAngGyroRoll,
                        frameAccelXG, frameAccelYG, frameAccelZG,
                        out gyroX, out gyroY, out gyroZ,
                        out accelX, out accelY, out accelZ);
                    break;

                // DS4Reader and DualSenseReader are identical in layout and in
                // sign, and DS4Mapper and DualSenseMapper build the frame the
                // same way, so both share one conversion. Anything unknown gets
                // the same treatment, since the DualShock convention is what
                // GamepadMotion space is defined in terms of.
                case InputDeviceType.DS4:
                case InputDeviceType.DualSense:
                default:
                    ToMotionSpaceDualSense(
                        frameAngGyroYaw, frameAngGyroPitch, frameAngGyroRoll,
                        frameAccelXG, frameAccelYG, frameAccelZG,
                        out gyroX, out gyroY, out gyroZ,
                        out accelX, out accelY, out accelZ);
                    break;
            }
        }

        /// <summary>
        /// DualSense and DS4. Confirmed against real hardware.
        ///
        /// The sensor triad is (right, up, backward): the reader takes pitch
        /// from the first gyro word, yaw from the second and roll from the
        /// third, and accel X/Y/Z sit on those same three axes in that order,
        /// so the axis carrying yaw (accel Y) is the up axis. That triad is
        /// exactly what GamepadMotion space is defined as, so the conversion
        /// is the identity once the reader's and mapper's negations are undone:
        ///   reader: yaw and roll negated, accel X and Y negated
        ///   mapper: pitch negated when building the frame
        /// leaving the frame as (-yaw, -pitch, -roll) of raw, hence all three
        /// gyro terms negate back. Flat and face-up the raw accel reads +1g on
        /// its up axis, so accel Y comes out at +1g as required.
        /// </summary>
        public static void ToMotionSpaceDualSense(
            double frameAngGyroYaw, double frameAngGyroPitch, double frameAngGyroRoll,
            double frameAccelXG, double frameAccelYG, double frameAccelZG,
            out double gyroX, out double gyroY, out double gyroZ,
            out double accelX, out double accelY, out double accelZ)
        {
            gyroX = -frameAngGyroPitch; // pitch -> GM X
            gyroY = -frameAngGyroYaw;   // yaw   -> GM Y
            gyroZ = -frameAngGyroRoll;  // roll  -> GM Z

            accelX = -frameAccelXG;
            accelY = -frameAccelYG;
            accelZ = frameAccelZG;
        }

        /// <summary>
        /// Steam Controller, both the USB/dongle reader and the Triton reader
        /// (identical report layout and identical negations). Confirmed against
        /// real hardware.
        ///
        /// Here the reader takes pitch from the first gyro word, roll from the
        /// second and yaw from the third, so relative to the DualShock the
        /// roll and yaw axes are exchanged: the sensor triad is
        /// (right, forward, up) and it is accel Z, not accel Y, that carries
        /// gravity. That is the whole reason a DualSense-derived conversion
        /// mis-behaves here, and it matches what the hardware showed: gravity
        /// landed on GamepadMotion Z, which makes the resolver read roll where
        /// it wants yaw and yaw where it wants roll.
        ///
        /// Because the frame's roll axis points forward rather than backward,
        /// the roll term is the one sign that differs from the DualSense
        /// conversion; the pitch and yaw terms are unchanged.
        /// </summary>
        public static void ToMotionSpaceSteamController(
            double frameAngGyroYaw, double frameAngGyroPitch, double frameAngGyroRoll,
            double frameAccelXG, double frameAccelYG, double frameAccelZG,
            out double gyroX, out double gyroY, out double gyroZ,
            out double accelX, out double accelY, out double accelZ)
        {
            gyroX = -frameAngGyroPitch; // pitch axis -> GM X
            gyroY = -frameAngGyroYaw;   // up axis    -> GM Y
            gyroZ = frameAngGyroRoll;   // roll axis points forward, so negate

            accelX = -frameAccelXG; // reader negates accel X
            accelY = frameAccelZG;  // accel Z is the up axis
            accelZ = -frameAccelYG; // accel Y points forward
        }

        /// <summary>
        /// Switch Pro Controller.
        ///
        /// The reader reorders Nintendo's report: accel X and Y are swapped as
        /// they are stored, and the gyro words are read as roll, pitch, yaw.
        /// Pairing each gyro word with the accel component from the same sensor
        /// axis gives axis 1 = roll, axis 2 = pitch, axis 3 = yaw, so accel Z
        /// holds the up axis. Following the reader's negations through
        /// (only yaw is negated) puts the pitch axis pointing left and the roll
        /// axis pointing forward, and the two sign flips that corrects cancel
        /// out in the gyro terms, leaving them identical to the DualSense ones.
        /// The accel terms do not cancel and are permuted.
        ///
        /// Not hardware confirmed. Both the accel and the gyro are scaled here
        /// by coefficients read from the controller's own SPI calibration at
        /// runtime, so a negative coefficient would silently invert an axis in
        /// a way no amount of reading the source can rule out.
        /// </summary>
        public static void ToMotionSpaceSwitchPro(
            double frameAngGyroYaw, double frameAngGyroPitch, double frameAngGyroRoll,
            double frameAccelXG, double frameAccelYG, double frameAccelZG,
            out double gyroX, out double gyroY, out double gyroZ,
            out double accelX, out double accelY, out double accelZ)
        {
            gyroX = -frameAngGyroPitch;
            gyroY = -frameAngGyroYaw;
            gyroZ = -frameAngGyroRoll;

            accelX = -frameAccelXG; // pitch axis points left
            accelY = frameAccelZG;  // accel Z is the up axis
            accelZ = -frameAccelYG; // roll axis points forward
        }

        /// <summary>
        /// Joy-Con. Only the right Joy-Con reaches this code: JoyConMapper
        /// calls PopulateStateGyro from its right-side branch alone, so gravity
        /// is always fed from the right side, matching JSM's IGNORE_LEFT
        /// default.
        ///
        /// The reader shares the Switch Pro parsing and then flips accel X,
        /// accel Z, yaw and pitch for the right side, which exists precisely to
        /// bring the right Joy-Con into the Pro Controller's frame. Working the
        /// flips through confirms that: the conversion comes out the same as
        /// the Switch Pro one. It is kept as its own method rather than shared
        /// because the two arrive at it by different routes, and a future
        /// change to either reader should not silently move the other.
        ///
        /// Not hardware confirmed, and subject to the same runtime calibration
        /// coefficient caveat as the Switch Pro.
        /// </summary>
        public static void ToMotionSpaceJoyCon(
            double frameAngGyroYaw, double frameAngGyroPitch, double frameAngGyroRoll,
            double frameAccelXG, double frameAccelYG, double frameAccelZG,
            out double gyroX, out double gyroY, out double gyroZ,
            out double accelX, out double accelY, out double accelZ)
        {
            gyroX = -frameAngGyroPitch;
            gyroY = -frameAngGyroYaw;
            gyroZ = -frameAngGyroRoll;

            accelX = -frameAccelXG;
            accelY = frameAccelZG;
            accelZ = -frameAccelYG;
        }

        /// <summary>
        /// 8BitDo Ultimate 2 Wireless.
        ///
        /// This reader labels each accel word with the rotation axis it shares,
        /// and stores them out of order: the word tagged pitch goes to accel Y,
        /// the word tagged yaw to accel X and the word tagged roll to accel Z.
        /// So it is accel X that carries gravity here. The reader negates yaw
        /// and pitch and leaves roll alone, which puts the roll axis forward,
        /// so the roll term negates the way the Steam Controller's does.
        ///
        /// Not hardware confirmed: the axis pairing rests on the reader's own
        /// comments rather than on anything measurable from the source.
        /// </summary>
        public static void ToMotionSpaceUltimate2Wireless(
            double frameAngGyroYaw, double frameAngGyroPitch, double frameAngGyroRoll,
            double frameAccelXG, double frameAccelYG, double frameAccelZG,
            out double gyroX, out double gyroY, out double gyroZ,
            out double accelX, out double accelY, out double accelZ)
        {
            gyroX = -frameAngGyroPitch;
            gyroY = -frameAngGyroYaw;
            gyroZ = frameAngGyroRoll; // roll axis points forward, so negate

            accelX = frameAccelYG; // accel Y is the pitch axis
            accelY = frameAccelXG; // accel X is the up axis
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
