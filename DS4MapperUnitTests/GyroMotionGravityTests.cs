using System;
using DS4MapperTest.GyroActions;

namespace DS4MapperUnitTests
{
    /// <summary>
    /// Behaviour pins for the gravity estimator ported from GamepadMotion.hpp.
    /// Everything the gravity-aware gyro spaces do rests on this converging to
    /// the real down direction, so the tests state that physically rather than
    /// re-deriving the correction maths.
    /// </summary>
    [TestClass]
    public class GyroMotionGravityTests
    {
        private const double Tick = 0.004; // 250 Hz

        private static void Feed(GyroMotionGravity motion, int ticks,
            double gyroX, double gyroY, double gyroZ,
            double accelX, double accelY, double accelZ)
        {
            for (int i = 0; i < ticks; i++)
            {
                motion.Update(gyroX, gyroY, gyroZ, accelX, accelY, accelZ, Tick);
            }
        }

        [TestMethod]
        public void ReportsNoGravityBeforeAnySample()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            Assert.IsFalse(motion.HasGravity);
        }

        // Held still and face-up the accelerometer reads +1g on its up axis, so
        // gravity must settle pointing straight down.
        [TestMethod]
        public void ConvergesToDownWhenRestingFaceUp()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            Feed(motion, 2000, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);

            Assert.IsTrue(motion.HasGravity);
            Assert.AreEqual(0.0, motion.Grav.x, 1e-6);
            Assert.AreEqual(-1.0, motion.Grav.y, 1e-3);
            Assert.AreEqual(0.0, motion.Grav.z, 1e-6);
        }

        // Stood on its left edge, gravity runs along the pitch axis instead.
        // This is the pose the world spaces pinch their vertical away in, so the
        // estimator has to actually report it rather than staying at down.
        [TestMethod]
        public void ConvergesToTheSideWhenRestingOnItsEdge()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            Feed(motion, 2000, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0);

            Assert.AreEqual(-1.0, motion.Grav.x, 1e-3);
            Assert.AreEqual(0.0, motion.Grav.y, 1e-6);
            Assert.AreEqual(0.0, motion.Grav.z, 1e-6);
        }

        // Direction is what the spaces consume, and it is right from the very
        // first sample even though the magnitude takes about a second to arrive.
        [TestMethod]
        public void GravityDirectionIsCorrectFromTheFirstSample()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            motion.Update(0.0, 0.0, 0.0, 0.0, 1.0, 0.0, Tick);

            Assert.IsTrue(motion.HasGravity);
            GyroMotionGravity.Vec dir = motion.Grav.Normalized();
            Assert.AreEqual(0.0, dir.x, 1e-9);
            Assert.AreEqual(-1.0, dir.y, 1e-9);
            Assert.AreEqual(0.0, dir.z, 1e-9);
        }

        // A pose change has to be tracked, otherwise the spaces keep resolving
        // against a stale down direction after the controller is turned over.
        [TestMethod]
        public void TracksAChangeOfPose()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            Feed(motion, 2000, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);
            Feed(motion, 2000, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0);

            Assert.AreEqual(0.0, motion.Grav.x, 1e-6);
            Assert.AreEqual(0.0, motion.Grav.y, 1e-3);
            Assert.AreEqual(-1.0, motion.Grav.z, 1e-3);
        }

        [TestMethod]
        public void IgnoresNonPositiveDeltaTime()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            motion.Update(0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0);
            motion.Update(0.0, 0.0, 0.0, 0.0, 1.0, 0.0, -Tick);

            Assert.IsFalse(motion.HasGravity);
        }

        // An all-zero sample means the device has not reported real motion data
        // yet; treating it as a genuine reading would drag gravity to nothing.
        [TestMethod]
        public void IgnoresAnAllZeroSample()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            Feed(motion, 2000, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);
            double settled = motion.Grav.y;

            Feed(motion, 100, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);

            Assert.AreEqual(settled, motion.Grav.y, 1e-12);
        }

        [TestMethod]
        public void ResetClearsGravity()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            Feed(motion, 500, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);
            Assert.IsTrue(motion.HasGravity);

            motion.Reset();

            Assert.IsFalse(motion.HasGravity);
        }

        // Rotating the controller while gravity is already settled must keep the
        // estimate pointing at the real world down, not follow the controller.
        [TestMethod]
        public void HoldsWorldDownWhileRotatingInPlace()
        {
            GyroMotionGravity motion = new GyroMotionGravity();

            // Settle face-up, then yaw steadily. Yawing about the gravity axis
            // does not change which way down is, and the accelerometer agrees.
            Feed(motion, 2000, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);
            Feed(motion, 500, 0.0, 90.0, 0.0, 0.0, 1.0, 0.0);

            Assert.AreEqual(0.0, motion.Grav.x, 1e-3);
            Assert.AreEqual(-1.0, motion.Grav.y, 1e-2);
            Assert.AreEqual(0.0, motion.Grav.z, 1e-3);
        }

        // Magnitude ramps in over roughly two seconds from cold, held back at
        // first because a fresh smoothed-acceleration history reads as
        // shakiness and shakiness throttles the correction speed. That lag is
        // harmless only because the spaces normalise gravity before using it,
        // which the direction test above covers. This test exists to catch the
        // ramp changing shape, since a much slower one would delay the point
        // where a pose change is fully tracked.
        [TestMethod]
        public void MagnitudeRampsInOverAboutTwoSeconds()
        {
            double LengthAfter(double seconds)
            {
                GyroMotionGravity motion = new GyroMotionGravity();
                Feed(motion, (int)Math.Round(seconds / Tick), 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);
                return motion.Grav.Length();
            }

            Assert.IsTrue(LengthAfter(0.5) < 0.2, "should still be ramping at half a second");
            Assert.AreEqual(0.86, LengthAfter(1.5), 0.05);
            Assert.AreEqual(0.94, LengthAfter(2.0), 0.05);
        }
    }
}
