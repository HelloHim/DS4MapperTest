using System;
using System.Diagnostics;

namespace DS4MapperTest.Universal
{
    // Timestamp source for controller state snapshots.
    //
    // The mapper turns the gap between two consecutive snapshots into pointer
    // movement, so the only thing that matters here is that differences are
    // accurate and always move forward. DateTimeOffset.UtcNow satisfies
    // neither: it jumps whenever Windows corrects the clock against a time
    // server, and its resolution is coarse enough that an eight millisecond
    // frame carries a noticeable quantisation error into every gyro reading.
    //
    // Stopwatch is backed by the high resolution performance counter, which is
    // monotonic and fine grained. The wall clock is read once to anchor the
    // values, so a snapshot timestamp still reads as a real UTC time for logs
    // and diagnostics.
    public static class UniversalMonotonicClock
    {
        private static readonly DateTimeOffset Origin = DateTimeOffset.UtcNow;
        private static readonly Stopwatch Elapsed = Stopwatch.StartNew();

        public static DateTimeOffset UtcNow => Origin + Elapsed.Elapsed;
    }
}
