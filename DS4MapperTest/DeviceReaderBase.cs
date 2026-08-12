using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DS4MapperTest.Common;
using NLog;

namespace DS4MapperTest
{
    public abstract class DeviceReaderBase
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected ManualResetEventSlim readWaitEv = new ManualResetEventSlim();
        public ManualResetEventSlim ReadWaitEv { get => readWaitEv; }

        protected bool fireReport = true;

        /// <summary>
        /// Runs a reader's input loop on its background thread with a top-level catch.
        /// Disconnect handling (RaiseRemoval and its subscribers) runs synchronously inside
        /// the loop, so an unhandled exception there would otherwise escape the thread and
        /// take down the whole process via AppDomain.UnhandledException.
        /// </summary>
        protected void RunReadInputSafely(Action readInputAction)
        {
            try
            {
                readInputAction();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unhandled exception in device input read thread; thread is exiting");
            }
        }

        public abstract void StartUpdate();
        public abstract void StopUpdate();
        public abstract void WriteRumbleReport();

        public virtual GyroCalibrationStatus GyroCalibrationStatus =>
            new GyroCalibrationStatus(false, false, 0);

        public virtual void RequestGyroCalibration()
        {
        }

        // Callers (TestSave/TestFakeSave) wait up to 5s for this method to run their
        // action, so retry catching the input thread's brief per-cycle wait window
        // across nearly that whole budget. A single 500ms attempt only needs to land
        // during the (usually much shorter) processing portion of one input cycle to
        // miss entirely, which silently dropped the requested action and burned the
        // caller's full 5s wait doing nothing.
        private const int HaltTotalTimeoutMs = 4500;
        private const int HaltAttemptTimeoutMs = 500;

        /// <summary>
        /// Must not be run from input thread. Waits for input thread to be in a wait state
        /// and then tell thread to no longer invoke the Report event. Input thread will then
        /// resume followed by invoking the action passed. Flag will be set to have
        /// Report event to resume being invoked after
        /// </summary>
        /// <param name="act">Action to execute in current thread</param>
        public void HaltReportingRunAction(Action act)
        {
            // Wait for controller to be in a wait period, retrying until the overall
            // budget is spent rather than giving up after a single missed window.
            bool result = false;
            long deadline = Environment.TickCount64 + HaltTotalTimeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                int remaining = (int)(deadline - Environment.TickCount64);
                if (remaining <= 0) break;

                if (readWaitEv.Wait(Math.Min(HaltAttemptTimeoutMs, remaining)))
                {
                    result = true;
                    break;
                }
            }

            if (result)
            {
                readWaitEv.Reset();

                // Tell device to no longer fire reports
                fireReport = false;

                // Flag is set. Allow input thread to resume
                readWaitEv.Set();

                // Invoke main desired action
                act?.Invoke();

                // Start firing reports again
                fireReport = true;
            }
        }
    }
}
