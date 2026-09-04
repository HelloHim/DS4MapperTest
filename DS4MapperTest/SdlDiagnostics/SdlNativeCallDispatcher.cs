using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using NLog;

namespace DS4MapperTest.SdlDiagnostics
{
    /// <summary>
    /// Runs every SDL native call on one dedicated thread for the life of the
    /// process.
    /// </summary>
    /// <remarks>
    /// This is not about thread safety in the usual sense. On Windows SDL
    /// learns that a controller has been plugged in from a hidden message-only
    /// window it creates during initialisation, and it drains that window's
    /// queue only when polled from the thread that created it. Windows message
    /// queues belong to a thread, so a poll from anywhere else retrieves
    /// nothing.
    ///
    /// The backend used to initialise SDL from whichever thread pool thread ran
    /// the service start and then poll it from the mapping thread, so the
    /// arrival notifications were posted to a thread pool thread that had long
    /// since gone back to the pool. Controllers present when the service
    /// started worked; anything connected afterwards was invisible until the
    /// app was restarted. Removals still worked, because SDL spots those when
    /// reading from a device handle that has died rather than from the
    /// notification window, which is why the failure looked so selective.
    /// </remarks>
    internal static class SdlNativeCallDispatcher
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly object queueLock = new object();
        private static readonly Queue<WorkItem> queue = new Queue<WorkItem>();
        private static Thread worker;

        // One reusable signal per calling thread. The mapping loop dispatches
        // several calls per pass at 125 Hz, and allocating a fresh wait handle
        // for each of them would be pure garbage.
        [ThreadStatic]
        private static ManualResetEventSlim callerSignal;

        private sealed class WorkItem
        {
            public Action Work;
            public ManualResetEventSlim Completed;
            public ExceptionDispatchInfo Error;
        }

        /// <summary>
        /// True when the caller already is the SDL thread, in which case work
        /// runs inline instead of deadlocking against itself.
        /// </summary>
        public static bool OnDispatcherThread =>
            ReferenceEquals(Thread.CurrentThread, Volatile.Read(ref worker));

        public static void Invoke(Action work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            if (OnDispatcherThread)
            {
                work();
                return;
            }

            EnsureWorkerStarted();
            Dispatch(work);
        }

        public static T Invoke<T>(Func<T> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            if (OnDispatcherThread)
            {
                return work();
            }

            EnsureWorkerStarted();
            T result = default;
            Dispatch(() => result = work());
            return result;
        }

        private static void EnsureWorkerStarted()
        {
            lock (queueLock)
            {
                if (worker != null) return;

                Thread thread = new Thread(PumpWorkQueue)
                {
                    IsBackground = true,
                    // Above the mapping loop would starve nothing but costs
                    // nothing either; below it would add latency to every
                    // controller poll, which all go through here.
                    Priority = ThreadPriority.AboveNormal,
                    Name = "SDL Native Calls",
                };

                Volatile.Write(ref worker, thread);
                thread.Start();
                logger.Info($"SDL native call thread started (managed id {thread.ManagedThreadId}). " +
                    "All SDL initialisation, device discovery and polling run here so Windows " +
                    "device arrival notifications reach the thread that registered for them.");
            }
        }

        private static void Dispatch(Action work)
        {
            ManualResetEventSlim signal = callerSignal ??= new ManualResetEventSlim(false);
            signal.Reset();

            WorkItem item = new WorkItem
            {
                Work = work,
                Completed = signal,
            };

            lock (queueLock)
            {
                queue.Enqueue(item);
                Monitor.Pulse(queueLock);
            }

            signal.Wait();
            item.Error?.Throw();
        }

        private static void PumpWorkQueue()
        {
            while (true)
            {
                WorkItem item;
                lock (queueLock)
                {
                    while (queue.Count == 0)
                    {
                        Monitor.Wait(queueLock);
                    }

                    item = queue.Dequeue();
                }

                try
                {
                    item.Work();
                }
                catch (Exception ex)
                {
                    // Rethrown on the calling thread so callers keep the
                    // error handling they already have around SDL failures.
                    item.Error = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    item.Completed.Set();
                }
            }
        }
    }
}
