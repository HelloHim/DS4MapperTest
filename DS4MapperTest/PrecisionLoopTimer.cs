using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace DS4MapperTest
{
    /// <summary>
    /// Paces a polling loop without depending on the system timer resolution.
    /// </summary>
    /// <remarks>
    /// Thread.Sleep rounds up to the process timer resolution, which since
    /// Windows 10 2004 is per process and defaults to 15.625 ms. A process
    /// that has asked for 1 ms with timeBeginPeriod only keeps it while
    /// Windows chooses to honour the request: a background process placed
    /// under power throttling has its request ignored, and every Sleep(8) in
    /// the mapping loop silently becomes a Sleep(15.6). The poll rate halves
    /// from 125 Hz to 64 Hz with nothing in the app changing, which is felt as
    /// stuttering, laggy gyro output.
    ///
    /// A high resolution waitable timer (Windows 10 1803 and later) is not
    /// bound to that resolution at all, so the loop keeps its cadence no
    /// matter what the rest of the system, or the power manager, is doing.
    /// Thread.Sleep remains the fallback where the timer cannot be created.
    /// </remarks>
    internal sealed class PrecisionLoopTimer : IDisposable
    {
        private const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;
        private const uint TIMER_ALL_ACCESS = 0x001F0003;
        private const uint INFINITE = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWaitableTimerExW(
            IntPtr timerAttributes, string timerName, uint flags, uint desiredAccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWaitableTimer(
            IntPtr timer,
            ref long dueTime,
            int period,
            IntPtr completionRoutine,
            IntPtr completionArgument,
            [MarshalAs(UnmanagedType.Bool)] bool resume);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        private IntPtr timerHandle;

        public PrecisionLoopTimer()
        {
            try
            {
                timerHandle = CreateWaitableTimerExW(IntPtr.Zero, null,
                    CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
            }
            catch (EntryPointNotFoundException)
            {
                timerHandle = IntPtr.Zero;
            }
            catch (DllNotFoundException)
            {
                timerHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// False when the loop is running on the Thread.Sleep fallback and its
        /// cadence is therefore at the mercy of the process timer resolution.
        /// </summary>
        public bool IsHighResolution => timerHandle != IntPtr.Zero;

        public void Wait(double milliseconds)
        {
            if (milliseconds <= 0.0) return;

            if (timerHandle == IntPtr.Zero)
            {
                Thread.Sleep((int)Math.Ceiling(milliseconds));
                return;
            }

            // Negative due times are relative, in 100 nanosecond units.
            long dueTime = -(long)Math.Round(milliseconds * 10_000.0);
            if (dueTime == 0) dueTime = -1;

            if (!SetWaitableTimer(timerHandle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false))
            {
                Thread.Sleep((int)Math.Ceiling(milliseconds));
                return;
            }

            WaitForSingleObject(timerHandle, INFINITE);
        }

        public void Dispose()
        {
            IntPtr handle = Interlocked.Exchange(ref timerHandle, IntPtr.Zero);
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
            }
        }
    }
}
