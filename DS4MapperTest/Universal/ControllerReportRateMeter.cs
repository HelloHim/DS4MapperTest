using System;

namespace DS4MapperTest.Universal
{
    /// <summary>
    /// Measures how many input reports a controller actually sends per second.
    /// </summary>
    /// <remarks>
    /// The alternative was asking SDL for the device's sensor data rate, which
    /// is a per model lookup: it answers for a pad with a gyro SDL recognises
    /// and says nothing at all for anything else, so a controller without
    /// motion fell back to a hardcoded guess. Counting reports works for every
    /// device on any backend, and needs no one to have described the hardware
    /// in advance.
    /// </remarks>
    public sealed class ControllerReportRateMeter
    {
        // Long enough that a device reporting at 125 Hz still contributes
        // hundreds of samples, short enough that a rate is available soon
        // after a controller is plugged in.
        public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

        private readonly object syncRoot = new object();
        private DateTimeOffset windowStartUtc;
        private int windowReports;
        private double? measuredRateHz;
        private bool started;

        /// <summary>
        /// Reports per second, or null until a full window has elapsed.
        /// </summary>
        public double? MeasuredRateHz
        {
            get { lock (syncRoot) return measuredRateHz; }
        }

        public void RecordReport(DateTimeOffset nowUtc)
        {
            lock (syncRoot)
            {
                if (!started)
                {
                    started = true;
                    windowStartUtc = nowUtc;
                    windowReports = 0;
                }

                windowReports++;
                CloseWindowIfElapsed(nowUtc);
            }
        }

        /// <summary>
        /// Closes an elapsed window even when no report arrived, so a device
        /// that has gone quiet does not keep reporting a stale rate.
        /// </summary>
        public void Tick(DateTimeOffset nowUtc)
        {
            lock (syncRoot)
            {
                if (!started) return;
                CloseWindowIfElapsed(nowUtc);
            }
        }

        // Always called with syncRoot held.
        private void CloseWindowIfElapsed(DateTimeOffset nowUtc)
        {
            double elapsedSeconds = (nowUtc - windowStartUtc).TotalSeconds;
            if (elapsedSeconds < Window.TotalSeconds) return;

            // A window that somehow spans no time cannot produce a rate, and
            // dividing by it would produce an infinity that then propagates
            // into the poll period.
            measuredRateHz = elapsedSeconds > 0.0
                ? windowReports / elapsedSeconds
                : null;

            if (measuredRateHz <= 0.0) measuredRateHz = null;

            windowStartUtc = nowUtc;
            windowReports = 0;
        }
    }
}
