using DS4MapperTest.SdlDiagnostics;
using System.Collections.Concurrent;

namespace DS4MapperUnitTests
{
    // SDL learns that a controller has arrived from a Windows message-only
    // window, and it only drains that window from the thread that created it.
    // Initialising SDL on one thread and polling it from another therefore made
    // controller hotplug silently stop working, so "every SDL call runs on the
    // same thread" is the invariant these tests defend.
    [TestClass]
    public class SdlNativeCallDispatcherTests
    {
        [TestMethod]
        public void CallsFromDifferentThreadsAllRunOnOneDedicatedThread()
        {
            ConcurrentBag<int> observedThreadIds = new ConcurrentBag<int>();
            ConcurrentBag<int> callingThreadIds = new ConcurrentBag<int>();

            Thread[] callers = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
            {
                callingThreadIds.Add(Environment.CurrentManagedThreadId);
                for (int pass = 0; pass < 25; pass++)
                {
                    observedThreadIds.Add(
                        SdlNativeCallDispatcher.Invoke(() => Environment.CurrentManagedThreadId));
                }
            })).ToArray();

            foreach (Thread caller in callers) caller.Start();
            foreach (Thread caller in callers) Assert.IsTrue(caller.Join(TimeSpan.FromSeconds(10)));

            int[] distinct = observedThreadIds.Distinct().ToArray();
            Assert.AreEqual(1, distinct.Length,
                "Every SDL call must run on the same thread regardless of the caller.");
            CollectionAssert.DoesNotContain(callingThreadIds.ToArray(), distinct[0],
                "SDL work must not run inline on a caller thread.");
        }

        [TestMethod]
        public void NestedCallsRunInlineRatherThanDeadlocking()
        {
            bool nestedRanOnDispatcherThread = SdlNativeCallDispatcher.Invoke(() =>
            {
                int outerThreadId = Environment.CurrentManagedThreadId;
                int innerThreadId = SdlNativeCallDispatcher.Invoke(
                    () => Environment.CurrentManagedThreadId);
                return outerThreadId == innerThreadId;
            });

            Assert.IsTrue(nestedRanOnDispatcherThread);
        }

        [TestMethod]
        public void FailuresSurfaceOnTheCallingThread()
        {
            InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(
                () => SdlNativeCallDispatcher.Invoke<int>(
                    () => throw new InvalidOperationException("native call failed")));

            Assert.AreEqual("native call failed", thrown.Message);

            // The dispatcher has to stay usable after a call throws, otherwise
            // one transient SDL error would end controller polling.
            Assert.AreEqual(7, SdlNativeCallDispatcher.Invoke(() => 7));
        }
    }
}
