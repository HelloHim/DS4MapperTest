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

        /// <summary>
        /// Must not be run from input thread. Waits for input thread to be in a wait state
        /// and then tell thread to no longer invoke the Report event. Input thread will then
        /// resume followed by invoking the action passed. Flag will be set to have
        /// Report event to resume being invoked after
        /// </summary>
        /// <param name="act">Action to execute in current thread</param>
        public void HaltReportingRunAction(Action act)
        {
            // Wait for controller to be in a wait period
            bool result = readWaitEv.Wait(millisecondsTimeout: 500);
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
