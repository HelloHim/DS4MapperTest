using DS4MapperTest.GyroActions;

namespace DS4MapperUnitTests
{
    /// <summary>
    /// Behaviour pins for the gravity-aware gyro spaces ported from
    /// JoyShockMapper's main.cpp. These are written as physical statements
    /// ("held flat, turning the controller must move the camera sideways")
    /// rather than as transcriptions of the formulas, so that they still catch
    /// a regression if someone rearranges the maths.
    ///
    /// Space is GamepadMotion / DualShock 4 (Y-up):
    ///   gyro X = pitch rate, Y = yaw rate, Z = roll rate, all deg/s
    ///   gravity points down, so held flat and face-up it is (0, -1, 0)
    /// </summary>
    [TestClass]
    public class GyroSpaceResolverTests
    {
        private const double Rate = 30.0;
        private const double Tol = 1e-9;

        // Held flat and face-up.
        private const double FlatGravX = 0.0, FlatGravY = -1.0, FlatGravZ = 0.0;

        private static void Resolve(GyroSpaceChoice space,
            double gyroX, double gyroY, double gyroZ,
            double gravX, double gravY, double gravZ,
            out double h, out double v) =>
            GyroSpaceResolver.Resolve(space, gyroX, gyroY, gyroZ,
                gravX, gravY, gravZ, out h, out v);

        // Positive rotation about the up axis is a leftward turn, so it must
        // produce leftward (negative) horizontal output - the same sign Local
        // Space produces via its "gyroX -= inGyroY" default.
        [TestMethod]
        public void PlayerTurnHeldFlatTurnsFromYaw()
        {
            Resolve(GyroSpaceChoice.PlayerTurn, 0.0, Rate, 0.0,
                FlatGravX, FlatGravY, FlatGravZ, out double h, out double v);

            Assert.AreEqual(-Rate, h, Tol);
            Assert.AreEqual(0.0, v, Tol);
        }

        [TestMethod]
        public void PlayerTurnHeldFlatIgnoresRoll()
        {
            Resolve(GyroSpaceChoice.PlayerTurn, 0.0, 0.0, Rate,
                FlatGravX, FlatGravY, FlatGravZ, out double h, out double v);

            Assert.AreEqual(0.0, h, Tol);
            Assert.AreEqual(0.0, v, Tol);
        }

        // Lean is the complement of turn: it reads roll and ignores yaw.
        [TestMethod]
        public void PlayerLeanHeldFlatTurnsFromRoll()
        {
            Resolve(GyroSpaceChoice.PlayerLean, 0.0, 0.0, Rate,
                FlatGravX, FlatGravY, FlatGravZ, out double h, out double v);

            Assert.AreEqual(-Rate, h, Tol);
            Assert.AreEqual(0.0, v, Tol);
        }

        [TestMethod]
        public void PlayerLeanHeldFlatIgnoresYaw()
        {
            Resolve(GyroSpaceChoice.PlayerLean, 0.0, Rate, 0.0,
                FlatGravX, FlatGravY, FlatGravZ, out double h, out double v);

            Assert.AreEqual(0.0, h, Tol);
            Assert.AreEqual(0.0, v, Tol);
        }

        // Both player spaces take vertical straight from the local pitch axis.
        [TestMethod]
        public void PlayerSpacesTakeVerticalFromLocalPitch()
        {
            foreach (GyroSpaceChoice space in new[] { GyroSpaceChoice.PlayerTurn, GyroSpaceChoice.PlayerLean })
            {
                Resolve(space, Rate, 0.0, 0.0,
                    FlatGravX, FlatGravY, FlatGravZ, out _, out double v);

                Assert.AreEqual(-Rate, v, Tol, $"{space} vertical");
            }
        }

        // Held flat there is no difference between the player and world
        // variants: the local axes already line up with the world ones.
        [TestMethod]
        public void HeldFlatWorldSpacesAgreeWithPlayerSpaces()
        {
            Resolve(GyroSpaceChoice.PlayerTurn, Rate, Rate, 0.0,
                FlatGravX, FlatGravY, FlatGravZ, out double playerTurnH, out double playerTurnV);
            Resolve(GyroSpaceChoice.WorldTurn, Rate, Rate, 0.0,
                FlatGravX, FlatGravY, FlatGravZ, out double worldTurnH, out double worldTurnV);

            Assert.AreEqual(playerTurnH, worldTurnH, Tol);
            Assert.AreEqual(playerTurnV, worldTurnV, Tol);

            Resolve(GyroSpaceChoice.PlayerLean, Rate, 0.0, Rate,
                FlatGravX, FlatGravY, FlatGravZ, out double playerLeanH, out double playerLeanV);
            Resolve(GyroSpaceChoice.WorldLean, Rate, 0.0, Rate,
                FlatGravX, FlatGravY, FlatGravZ, out double worldLeanH, out double worldLeanV);

            Assert.AreEqual(playerLeanH, worldLeanH, Tol);
            Assert.AreEqual(playerLeanV, worldLeanV, Tol);
        }

        // World Turn reads yaw about the true vertical, so tipping the
        // controller nose-down must not change how much a world-vertical
        // rotation turns the camera.
        [TestMethod]
        public void WorldTurnFollowsTrueVerticalWhenPitchedForward()
        {
            // Nose down 90 degrees: gravity now runs along the local roll axis.
            Resolve(GyroSpaceChoice.WorldTurn, 0.0, 0.0, Rate,
                0.0, 0.0, -1.0, out double h, out _);

            Assert.AreEqual(-Rate, h, Tol);
        }

        // Turned on its side, gravity lies along the local pitch axis, and the
        // pitch vector the world spaces rely on becomes meaningless. JSM pinches
        // the vertical away to nothing there rather than emitting noise.
        [TestMethod]
        public void WorldTurnPinchesVerticalWhenHeldOnItsSide()
        {
            Resolve(GyroSpaceChoice.WorldTurn, Rate, Rate, Rate,
                -1.0, 0.0, 0.0, out _, out double v);

            Assert.AreEqual(0.0, v, Tol);
        }

        // Horizontal survives that same pose in World Turn, because the yaw term
        // is added outside the pinched block.
        [TestMethod]
        public void WorldTurnKeepsHorizontalWhenHeldOnItsSide()
        {
            Resolve(GyroSpaceChoice.WorldTurn, Rate, 0.0, 0.0,
                -1.0, 0.0, 0.0, out double h, out _);

            Assert.AreEqual(-Rate, h, Tol);
        }

        // Gravity is only supplied once the estimate has converged, but the
        // resolver must not divide by zero if it ever arrives empty.
        [TestMethod]
        public void ZeroGravityProducesFiniteOutput()
        {
            foreach (GyroSpaceChoice space in new[]
            {
                GyroSpaceChoice.PlayerTurn, GyroSpaceChoice.PlayerLean,
                GyroSpaceChoice.WorldTurn, GyroSpaceChoice.WorldLean,
            })
            {
                Resolve(space, Rate, Rate, Rate, 0.0, 0.0, 0.0,
                    out double h, out double v);

                Assert.IsFalse(double.IsNaN(h) || double.IsInfinity(h), $"{space} horizontal");
                Assert.IsFalse(double.IsNaN(v) || double.IsInfinity(v), $"{space} vertical");
            }
        }

        // Gravity arrives unnormalised from the estimator, whose magnitude grows
        // over the first second or so of convergence. Only its direction may
        // affect the result.
        [TestMethod]
        public void GravityMagnitudeDoesNotAffectResult()
        {
            foreach (GyroSpaceChoice space in new[]
            {
                GyroSpaceChoice.PlayerTurn, GyroSpaceChoice.PlayerLean,
                GyroSpaceChoice.WorldTurn, GyroSpaceChoice.WorldLean,
            })
            {
                Resolve(space, Rate, Rate * 0.5, Rate * 0.25,
                    0.1, -0.9, 0.3, out double unitH, out double unitV);
                Resolve(space, Rate, Rate * 0.5, Rate * 0.25,
                    0.001, -0.009, 0.003, out double tinyH, out double tinyV);

                Assert.AreEqual(unitH, tinyH, 1e-9, $"{space} horizontal");
                Assert.AreEqual(unitV, tinyV, 1e-9, $"{space} vertical");
            }
        }

        // The relax factors are what keep a turn from being clipped to the raw
        // axis rate the moment the controller is held at an angle; JSM uses a
        // wider buffer for turn than for lean.
        [TestMethod]
        public void UsesJoyShockMapperRelaxFactors()
        {
            Assert.AreEqual(2.0, GyroSpaceResolver.YAW_RELAX_FACTOR, Tol);
            Assert.AreEqual(1.41, GyroSpaceResolver.ROLL_RELAX_FACTOR, Tol);
            Assert.AreEqual(0.125, GyroSpaceResolver.SIDE_REDUCTION_THRESHOLD, Tol);
        }

        // Relaxation lets a partly-tilted turn read at full rate, but never
        // faster than the combined yaw/roll rate actually measured.
        [TestMethod]
        public void TurnRelaxationIsCappedAtTheMeasuredRate()
        {
            // Tilted 45 degrees, so the yaw axis only projects half onto gravity.
            double diag = -System.Math.Sqrt(0.5);
            Resolve(GyroSpaceChoice.PlayerTurn, 0.0, Rate, 0.0,
                0.0, diag, diag, out double h, out _);

            // 0.7071 * 2.0 relaxation would exceed the measured rate, so it clips.
            Assert.AreEqual(-Rate, h, Tol);
        }
    }
}
