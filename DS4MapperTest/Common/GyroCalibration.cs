using System;
using System.Diagnostics;

namespace DS4MapperTest.Common
{
    public sealed class GyroCalibrationStatus
    {
        public bool IsWaitingToStart { get; }
        public bool IsCalibrating { get; }
        public long RemainingMilliseconds { get; }

        public GyroCalibrationStatus(bool isWaitingToStart, bool isCalibrating,
            long remainingMilliseconds)
        {
            IsWaitingToStart = isWaitingToStart;
            IsCalibrating = isCalibrating;
            RemainingMilliseconds = remainingMilliseconds;
        }
    }

    /// <summary>
    /// Direct C# port of the Manual-mode calibration accumulator from Jibb Smart's
    /// GamepadMotion.hpp (MIT licence, https://github.com/JibbSmart/GamepadMotionHelpers) -
    /// the same engine JoyShockMapper itself uses (GamepadMotionHelpers, not
    /// JoyShockLibrary; JSL only supplies raw sensor reports, all of JSM's motion
    /// math including calibration is GamepadMotionHelpers).
    ///
    /// This intentionally ports ONLY GamepadMotion::GyroCalibration /
    /// PushSensorSamples / GetCalibratedSensor - the Manual mode accumulator.
    /// GamepadMotion's Stillness/SensorFusion auto-calibration is not ported and
    /// is not wanted here; Manual mode is also what JSM itself defaults to
    /// (AUTO_CALIBRATE_GYRO ships OFF).
    ///
    /// Offset is the plain running mean of every sample collected while
    /// calibrating - sum / count, recomputed on read, exactly like
    /// GamepadMotion::GetCalibratedSensor. There is no windowing and no
    /// duration weighting; the previous JoyShockLibrary-style behaviour
    /// inherited from upstream Ryochan7/DS4MapperTest is gone.
    ///
    /// Kept in the device's raw integer units rather than deg/s: mean-subtraction
    /// is linear, so averaging raw counts and subtracting the raw-count mean is
    /// equivalent to converting to deg/s first and doing it there. That
    /// equivalence only holds for Manual mode - GamepadMotion's auto-calibration
    /// thresholds are hardcoded in deg/s and g, so if Stillness/SensorFusion is
    /// ever wanted later, it needs to run downstream on already-converted values.
    ///
    /// The 5-second collection window, 1-second start delay, and
    /// reconnect/relaunch retrigger are this app's own UI timing layered on top
    /// of JSM's plain Start/Pause/Reset primitives - JSM's own app leaves manual
    /// calibration open until FINISH_GYRO_CALIBRATION is pressed by hand. Nothing
    /// about a timed auto-stop is part of GamepadMotion itself; it's just this
    /// app calling Pause on a timer instead of on a button.
    /// </summary>
    public class GyroCalibration
    {
        private const int CalibrationWindowMs = 5000;

        private readonly object calibrationLock = new object();
        private readonly Stopwatch gyroCalibrationTimer = new Stopwatch();
        private DateTime? delayedCalibrationStartUtc;

        // GamepadMotionHelpers::GyroCalibration - running sums, not windows.
        private double sumX;
        private double sumY;
        private double sumZ;
        private double sumAccelMagnitude;
        private long numSamples;

        // GamepadMotion::GetCalibratedSensor: mean of accumulated samples,
        // computed on demand. Zero samples -> zero offset (no correction yet).
        public int gyro_offset_x => numSamples > 0 ? (int)Math.Round(sumX / numSamples) : 0;
        public int gyro_offset_y => numSamples > 0 ? (int)Math.Round(sumY / numSamples) : 0;
        public int gyro_offset_z => numSamples > 0 ? (int)Math.Round(sumZ / numSamples) : 0;
        public double gyro_accel_magnitude => numSamples > 0 ? sumAccelMagnitude / numSamples : 1.0;

        public long CntCalibrating
        {
            get
            {
                return gyroCalibrationTimer.IsRunning ? gyroCalibrationTimer.ElapsedMilliseconds : 0;
            }
        }

        public GyroCalibrationStatus Status
        {
            get
            {
                lock (calibrationLock)
                {
                    if (delayedCalibrationStartUtc.HasValue)
                    {
                        long remaining = Math.Max(0, (long)(delayedCalibrationStartUtc.Value - DateTime.UtcNow).TotalMilliseconds);
                        return new GyroCalibrationStatus(true, false, remaining);
                    }

                    if (gyroCalibrationTimer.IsRunning)
                    {
                        long remaining = Math.Max(0, CalibrationWindowMs - gyroCalibrationTimer.ElapsedMilliseconds);
                        return new GyroCalibrationStatus(false, true, remaining);
                    }

                    return new GyroCalibrationStatus(false, false, 0);
                }
            }
        }

        public GyroCalibration()
        {
            StartContinuousCalibration();
        }

        /// <summary>
        /// Samples a report while calibration is active, begins a requested delayed
        /// calibration once its delay has elapsed, and freezes the accumulator once
        /// the collection window has run its course - mirrors this app's existing
        /// 5-second burst UX. Called by the input thread.
        /// </summary>
        public void Update(ref int currentYaw, ref int currentPitch, ref int currentRoll,
            ref int accelX, ref int accelY, ref int accelZ)
        {
            lock (calibrationLock)
            {
                if (delayedCalibrationStartUtc.HasValue && DateTime.UtcNow >= delayedCalibrationStartUtc.Value)
                {
                    delayedCalibrationStartUtc = null;
                    ResetContinuousCalibrationInternal();
                }

                if (gyroCalibrationTimer.IsRunning)
                {
                    if (gyroCalibrationTimer.ElapsedMilliseconds >= CalibrationWindowMs)
                    {
                        // Collection window elapsed: freeze here, same as JSM's
                        // FINISH_GYRO_CALIBRATION / PauseContinuousCalibration.
                        // Do NOT clear the accumulator - the mean gathered so far
                        // is the offset going forward.
                        PauseContinuousCalibrationInternal();
                    }
                    else
                    {
                        PushSensorSamples(currentYaw, currentPitch, currentRoll, accelX, accelY, accelZ);
                    }
                }
            }
        }

        /// <summary>Begins a fresh calibration window after the requested delay.</summary>
        public void RequestCalibrationAfterDelay(int delayMilliseconds)
        {
            lock (calibrationLock)
            {
                delayedCalibrationStartUtc = DateTime.UtcNow.AddMilliseconds(delayMilliseconds);
            }
        }

        /// <summary>GamepadMotion::StartContinuousCalibration - begin collecting. Does not clear the accumulator.</summary>
        public void StartContinuousCalibration()
        {
            lock (calibrationLock)
            {
                StartContinuousCalibrationInternal();
            }
        }

        private void StartContinuousCalibrationInternal()
        {
            gyroCalibrationTimer.Restart();
        }

        /// <summary>GamepadMotion::PauseContinuousCalibration - stop collecting, keep the settled offset.</summary>
        public void PauseContinuousCalibration()
        {
            lock (calibrationLock)
            {
                PauseContinuousCalibrationInternal();
            }
        }

        private void PauseContinuousCalibrationInternal()
        {
            gyroCalibrationTimer.Stop();
        }

        /// <summary>
        /// Zeroes the accumulator, then immediately begins a fresh collection window.
        ///
        /// Upstream GamepadMotion::ResetContinuousCalibration only zeroes
        /// GyroCalibration and leaves IsCalibrating untouched - it relies on a
        /// separate, already-active StartContinuousCalibration to still be in
        /// effect. This app has exactly one call site for this method: once per
        /// (re)connect, with no paired Start call alongside it, because a
        /// reconnect must restart the 5-second window from zero elapsed time
        /// rather than inherit however much of the constructor's original
        /// window had already ticked by. So unlike the upstream one-liner, this
        /// override also restarts the window, folding in what would otherwise be
        /// a separate StartContinuousCalibration call at every call site.
        /// </summary>
        public void ResetContinuousCalibration()
        {
            lock (calibrationLock)
            {
                ResetContinuousCalibrationInternal();
            }
        }

        private void ResetContinuousCalibrationInternal()
        {
            sumX = 0.0;
            sumY = 0.0;
            sumZ = 0.0;
            sumAccelMagnitude = 0.0;
            numSamples = 0;
            StartContinuousCalibrationInternal();
        }

        // GamepadMotion::PushSensorSamples - plain accumulation, no windowing.
        private void PushSensorSamples(int gyroX, int gyroY, int gyroZ, int accelX, int accelY, int accelZ)
        {
            double accelMagnitude = Math.Sqrt(
                (double)accelX * accelX + (double)accelY * accelY + (double)accelZ * accelZ);

            numSamples++;
            sumX += gyroX;
            sumY += gyroY;
            sumZ += gyroZ;
            sumAccelMagnitude += accelMagnitude;
        }
    }
}
