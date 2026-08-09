using System;
using System.Collections.Generic;
using System.Linq;
using DS4MapperTest;
using DS4MapperTest.MapperUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class MouseOutputDispatcherTests
    {
        private sealed class RecordingBackend : IMouseOutputBackend
        {
            public RecordingBackend(MouseOutputDestination destination, bool supportsAbsolute = false)
            {
                Destination = destination;
                Identity = new MouseOutputBackendIdentity(destination,
                    handle: (nuint)((int)destination + 1), supportsAbsolute: supportsAbsolute);
            }

            public MouseOutputDestination Destination { get; }
            public bool IsAvailable { get; set; } = true;
            public bool ThrowOnSubmit { get; set; }
            public bool AcceptSubmission { get; set; } = true;
            public MouseOutputBackendIdentity Identity { get; set; }
            public List<MouseOutputBackendSubmission> Submissions { get; } =
                new List<MouseOutputBackendSubmission>();
            public int DisposeCount { get; private set; }

            public bool TrySubmit(MouseOutputBackendSubmission submission)
            {
                if (ThrowOnSubmit)
                {
                    throw new InvalidOperationException("backend failure");
                }

                if (!IsAvailable || !AcceptSubmission)
                {
                    return false;
                }

                if (submission.PreviousButtons != submission.Buttons ||
                    submission.RelativeX != 0 || submission.RelativeY != 0 ||
                    submission.Wheel != 0 || submission.Pan != 0 ||
                    submission.HasAbsolute)
                {
                    Submissions.Add(submission);
                }

                return true;
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class FakeViiperMouseApi : IMouseOutputNativeViiperApi
        {
            public sealed class CreatedDevice
            {
                public nuint Handle { get; set; }
                public uint BusId { get; set; }
                public ushort VendorId { get; set; }
                public ushort ProductId { get; set; }
            }

            private uint nextBusId = 100;
            private nuint nextHandle = 1000;
            public List<CreatedDevice> CreatedDevices { get; } = new List<CreatedDevice>();
            public List<nuint> RemovedHandles { get; } = new List<nuint>();
            public List<(nuint Handle, MouseDeviceState State)> SubmittedStates { get; } =
                new List<(nuint Handle, MouseDeviceState State)>();
            public HashSet<nuint> FailingHandles { get; } = new HashSet<nuint>();

            public bool CreateUSBBus(nuint serverHandle, ref uint busId)
            {
                busId = nextBusId++;
                return true;
            }

            public bool CreateMouseDevice(nuint serverHandle, out nuint handle, uint busId,
                bool autoAttachLocalhost, ushort vendorId, ushort productId)
            {
                handle = nextHandle++;
                CreatedDevices.Add(new CreatedDevice
                {
                    Handle = handle,
                    BusId = busId,
                    VendorId = vendorId,
                    ProductId = productId,
                });
                return true;
            }

            public bool SetMouseDeviceState(nuint deviceHandle, MouseDeviceState state)
            {
                SubmittedStates.Add((deviceHandle, state));
                return !FailingHandles.Contains(deviceHandle);
            }

            public bool RemoveMouseDevice(nuint deviceHandle)
            {
                RemovedHandles.Add(deviceHandle);
                return true;
            }
        }

        private static AppGlobalData CreateAppGlobal(Action<AppSettingsStore> configure = null,
            bool fakerInputInstalled = true)
        {
            AppGlobalData appGlobal = new AppGlobalData
            {
                fakerInputInstalled = fakerInputInstalled,
                appSettings = new AppSettingsStore(),
            };
            configure?.Invoke(appGlobal.appSettings);
            return appGlobal;
        }

        private static IMouseOutputBackend[] CreateBackends(
            out RecordingBackend sendInput,
            out RecordingBackend fakerInput,
            out RecordingBackend viiper1,
            out RecordingBackend viiper2,
            out RecordingBackend viiper3)
        {
            sendInput = new RecordingBackend(MouseOutputDestination.SendInput, supportsAbsolute: true);
            fakerInput = new RecordingBackend(MouseOutputDestination.FakerInputMouse, supportsAbsolute: true);
            viiper1 = new RecordingBackend(MouseOutputDestination.ViiperMouse1);
            viiper2 = new RecordingBackend(MouseOutputDestination.ViiperMouse2);
            viiper3 = new RecordingBackend(MouseOutputDestination.ViiperMouse3);
            return new IMouseOutputBackend[]
            {
                sendInput,
                fakerInput,
                viiper1,
                viiper2,
                viiper3,
            };
        }

        [TestMethod]
        public void AllRoutesAffectDispatcherBehaviour()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.GyroMouseDestination = MouseOutputDestination.ViiperMouse1;
                settings.JoystickMouseDestination = MouseOutputDestination.ViiperMouse2;
                settings.FlickStickMouseDestination = MouseOutputDestination.ViiperMouse3;
                settings.TrackpadMouseDestination = MouseOutputDestination.FakerInputMouse;
                settings.TriggerMouseDestination = MouseOutputDestination.SendInput;
                settings.OtherMouseDestination = MouseOutputDestination.FakerInputMouse;
                settings.AbsoluteMouseDestination = MouseOutputDestination.SendInput;
            });
            IMouseOutputBackend[] backends = CreateBackends(out RecordingBackend sendInput,
                out RecordingBackend fakerInput, out RecordingBackend viiper1,
                out RecordingBackend viiper2, out RecordingBackend viiper3);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.QueueRelative(producer, MouseOutputRoute.Gyro, 11, -1);
            dispatcher.QueueRelative(producer, MouseOutputRoute.JoystickMouse, 22, -2);
            dispatcher.QueueRelative(producer, MouseOutputRoute.FlickStick, 33, -3);
            dispatcher.QueueRelative(producer, MouseOutputRoute.Trackpad, 44, -4);
            dispatcher.QueueRelative(producer, MouseOutputRoute.TriggerMouse, 55, -5);
            dispatcher.QueueRelative(producer, MouseOutputRoute.Other, 66, -6);
            dispatcher.QueueWheel(producer, MouseOutputRoute.Trackpad, 120, -120);
            dispatcher.QueueWheel(producer, MouseOutputRoute.Other, 240, 0);
            dispatcher.SetButton(producer, MouseOutputRoute.Other,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, true);
            dispatcher.QueueAbsolute(producer, 0.25, 0.75);

            dispatcher.FlushProducer(producer, flushSharedFakerInput: false);

            Assert.AreEqual(1, viiper1.Submissions.Count);
            Assert.AreEqual(11L, viiper1.Submissions[0].RelativeX);
            Assert.AreEqual(-1L, viiper1.Submissions[0].RelativeY);

            Assert.AreEqual(1, viiper2.Submissions.Count);
            Assert.AreEqual(22L, viiper2.Submissions[0].RelativeX);
            Assert.AreEqual(-2L, viiper2.Submissions[0].RelativeY);

            Assert.AreEqual(1, viiper3.Submissions.Count);
            Assert.AreEqual(33L, viiper3.Submissions[0].RelativeX);
            Assert.AreEqual(-3L, viiper3.Submissions[0].RelativeY);

            Assert.AreEqual(1, fakerInput.Submissions.Count);
            Assert.AreEqual(110L, fakerInput.Submissions[0].RelativeX);
            Assert.AreEqual(-10L, fakerInput.Submissions[0].RelativeY);
            Assert.AreEqual(360L, fakerInput.Submissions[0].Wheel);
            Assert.AreEqual(-120L, fakerInput.Submissions[0].Pan);
            Assert.AreEqual(VIIPERMouseButton.Left, fakerInput.Submissions[0].Buttons);

            Assert.AreEqual(1, sendInput.Submissions.Count);
            Assert.AreEqual(55L, sendInput.Submissions[0].RelativeX);
            Assert.AreEqual(-5L, sendInput.Submissions[0].RelativeY);
            Assert.IsTrue(sendInput.Submissions[0].HasAbsolute);
            Assert.AreEqual(0.25, sendInput.Submissions[0].AbsoluteX, 1e-9);
            Assert.AreEqual(0.75, sendInput.Submissions[0].AbsoluteY, 1e-9);
        }

        [TestMethod]
        public void MultipleProducersOnSameRouteKeepButtonHeldUntilFinalRelease()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.OtherMouseDestination = MouseOutputDestination.SendInput;
            });
            IMouseOutputBackend[] backends = CreateBackends(out RecordingBackend sendInput,
                out _, out _, out _, out _);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer1 = dispatcher.RegisterProducer();
            MouseOutputProducerId producer2 = dispatcher.RegisterProducer();

            dispatcher.SetButton(producer1, MouseOutputRoute.Other,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, true);
            dispatcher.FlushProducer(producer1, false);
            dispatcher.SetButton(producer2, MouseOutputRoute.Other,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, true);
            dispatcher.FlushProducer(producer2, false);
            dispatcher.SetButton(producer1, MouseOutputRoute.Other,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, false);
            dispatcher.FlushProducer(producer1, false);
            dispatcher.SetButton(producer2, MouseOutputRoute.Other,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, false);
            dispatcher.FlushProducer(producer2, false);

            Assert.AreEqual(2, sendInput.Submissions.Count);
            Assert.AreEqual((byte)0, sendInput.Submissions[0].PreviousButtons);
            Assert.AreEqual(VIIPERMouseButton.Left, sendInput.Submissions[0].Buttons);
            Assert.AreEqual(VIIPERMouseButton.Left, sendInput.Submissions[1].PreviousButtons);
            Assert.AreEqual((byte)0, sendInput.Submissions[1].Buttons);
        }

        [TestMethod]
        public void MultipleRoutesSharingDestinationDoNotReleaseEachOther()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.GyroMouseDestination = MouseOutputDestination.SendInput;
                settings.TrackpadMouseDestination = MouseOutputDestination.SendInput;
            });
            IMouseOutputBackend[] backends = CreateBackends(out RecordingBackend sendInput,
                out _, out _, out _, out _);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.SetButton(producer, MouseOutputRoute.Gyro,
                MouseButtonCodes.MOUSE_RIGHT_BUTTON, true);
            dispatcher.FlushProducer(producer, false);
            dispatcher.SetButton(producer, MouseOutputRoute.Trackpad,
                MouseButtonCodes.MOUSE_RIGHT_BUTTON, true);
            dispatcher.FlushProducer(producer, false);
            dispatcher.SetButton(producer, MouseOutputRoute.Gyro,
                MouseButtonCodes.MOUSE_RIGHT_BUTTON, false);
            dispatcher.FlushProducer(producer, false);
            dispatcher.SetButton(producer, MouseOutputRoute.Trackpad,
                MouseButtonCodes.MOUSE_RIGHT_BUTTON, false);
            dispatcher.FlushProducer(producer, false);

            Assert.AreEqual(2, sendInput.Submissions.Count);
            Assert.AreEqual(VIIPERMouseButton.Right, sendInput.Submissions[0].Buttons);
            Assert.AreEqual((byte)0, sendInput.Submissions[1].Buttons);
        }

        [TestMethod]
        public void RouteChangeTransfersHeldButtonsAndDropsPendingMovement()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.GyroMouseDestination = MouseOutputDestination.ViiperMouse1;
            });
            IMouseOutputBackend[] backends = CreateBackends(out _,
                out _, out RecordingBackend viiper1, out RecordingBackend viiper2, out _);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.SetButton(producer, MouseOutputRoute.Gyro,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, true);
            dispatcher.FlushProducer(producer, false);

            dispatcher.QueueRelative(producer, MouseOutputRoute.Gyro, 99, 0);
            appGlobal.appSettings.GyroMouseDestination = MouseOutputDestination.ViiperMouse2;
            dispatcher.FlushProducer(producer, false);

            Assert.AreEqual(2, viiper1.Submissions.Count);
            Assert.AreEqual((byte)0, viiper1.Submissions[0].PreviousButtons);
            Assert.AreEqual(VIIPERMouseButton.Left, viiper1.Submissions[0].Buttons);
            Assert.AreEqual(VIIPERMouseButton.Left, viiper1.Submissions[1].PreviousButtons);
            Assert.AreEqual((byte)0, viiper1.Submissions[1].Buttons);
            Assert.AreEqual(0L, viiper1.Submissions[1].RelativeX);

            Assert.AreEqual(1, viiper2.Submissions.Count);
            Assert.AreEqual((byte)0, viiper2.Submissions[0].PreviousButtons);
            Assert.AreEqual(VIIPERMouseButton.Left, viiper2.Submissions[0].Buttons);
            Assert.AreEqual(0L, viiper2.Submissions[0].RelativeX);
        }

        [TestMethod]
        public void RefreshRoutingUsesUpdatedConfiguredRoutes()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.GyroMouseDestination = MouseOutputDestination.ViiperMouse1;
            });
            IMouseOutputBackend[] backends = CreateBackends(out _,
                out _, out RecordingBackend viiper1, out RecordingBackend viiper2, out _);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.SetButton(producer, MouseOutputRoute.Gyro,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, true);
            dispatcher.FlushProducer(producer, false);

            appGlobal.appSettings.GyroMouseDestination = MouseOutputDestination.ViiperMouse2;
            dispatcher.RefreshRouting(flushSharedFakerInput: false);

            Assert.AreEqual(2, viiper1.Submissions.Count);
            Assert.AreEqual(VIIPERMouseButton.Left, viiper1.Submissions[0].Buttons);
            Assert.AreEqual((byte)0, viiper1.Submissions[1].Buttons);
            Assert.AreEqual(1, viiper2.Submissions.Count);
            Assert.AreEqual(VIIPERMouseButton.Left, viiper2.Submissions[0].Buttons);
        }

        [TestMethod]
        public void FallbackPreservesConfiguredDestinationAndRestoresHeldButtons()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.TrackpadMouseDestination = MouseOutputDestination.FakerInputMouse;
            });
            IMouseOutputBackend[] backends = CreateBackends(out _,
                out RecordingBackend fakerInput, out RecordingBackend viiper1, out _, out _);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.SetButton(producer, MouseOutputRoute.Trackpad,
                MouseButtonCodes.MOUSE_LEFT_BUTTON, true);
            dispatcher.FlushProducer(producer, false);

            fakerInput.IsAvailable = false;
            dispatcher.FlushProducer(producer, false);

            Assert.AreEqual(MouseOutputDestination.FakerInputMouse,
                appGlobal.appSettings.TrackpadMouseDestination);
            Assert.AreEqual(1, viiper1.Submissions.Count);
            Assert.AreEqual(VIIPERMouseButton.Left, viiper1.Submissions[0].Buttons);

            fakerInput.IsAvailable = true;
            dispatcher.FlushProducer(producer, false);

            Assert.AreEqual(2, fakerInput.Submissions.Count);
            Assert.AreEqual((byte)0, fakerInput.Submissions[1].PreviousButtons);
            Assert.AreEqual(VIIPERMouseButton.Left, fakerInput.Submissions[1].Buttons);
            Assert.AreEqual(2, viiper1.Submissions.Count);
            Assert.AreEqual(VIIPERMouseButton.Left, viiper1.Submissions[1].PreviousButtons);
            Assert.AreEqual((byte)0, viiper1.Submissions[1].Buttons);
        }

        [TestMethod]
        public void AbsoluteMouseNeverDispatchesToViiper()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.AbsoluteMouseDestination = MouseOutputDestination.ViiperMouse1;
            }, fakerInputInstalled: false);
            IMouseOutputBackend[] backends = CreateBackends(out RecordingBackend sendInput,
                out RecordingBackend fakerInput, out RecordingBackend viiper1, out _, out _);
            fakerInput.IsAvailable = false;
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.QueueAbsolute(producer, 0.1, 0.9);
            dispatcher.FlushProducer(producer, false);

            Assert.AreEqual(1, sendInput.Submissions.Count);
            Assert.IsTrue(sendInput.Submissions[0].HasAbsolute);
            Assert.AreEqual(0, viiper1.Submissions.Count);
        }

        [TestMethod]
        public void BackendExceptionDoesNotPreventOtherDestinationsFromDispatching()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.GyroMouseDestination = MouseOutputDestination.SendInput;
                settings.TrackpadMouseDestination = MouseOutputDestination.FakerInputMouse;
            });
            IMouseOutputBackend[] backends = CreateBackends(out RecordingBackend sendInput,
                out RecordingBackend fakerInput, out _, out _, out _);
            sendInput.ThrowOnSubmit = true;
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.QueueRelative(producer, MouseOutputRoute.Gyro, 1, 2);
            dispatcher.QueueRelative(producer, MouseOutputRoute.Trackpad, 3, 4);
            dispatcher.FlushProducer(producer, false);

            Assert.AreEqual(0, sendInput.Submissions.Count);
            Assert.AreEqual(1, fakerInput.Submissions.Count);
            Assert.AreEqual(3L, fakerInput.Submissions[0].RelativeX);
            Assert.AreEqual(4L, fakerInput.Submissions[0].RelativeY);
        }

        [TestMethod]
        public void UnregisterProducerReleasesOwnedButtons()
        {
            AppGlobalData appGlobal = CreateAppGlobal(settings =>
            {
                settings.OtherMouseDestination = MouseOutputDestination.SendInput;
            });
            IMouseOutputBackend[] backends = CreateBackends(out RecordingBackend sendInput,
                out _, out _, out _, out _);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);
            MouseOutputProducerId producer = dispatcher.RegisterProducer();

            dispatcher.SetButton(producer, MouseOutputRoute.Other,
                MouseButtonCodes.MOUSE_MIDDLE_BUTTON, true);
            dispatcher.FlushProducer(producer, false);
            dispatcher.UnregisterProducer(producer);

            Assert.AreEqual(2, sendInput.Submissions.Count);
            Assert.AreEqual(VIIPERMouseButton.Middle, sendInput.Submissions[0].Buttons);
            Assert.AreEqual((byte)0, sendInput.Submissions[1].Buttons);
        }

        [TestMethod]
        public void ViiperManagerCreatesThreeIndependentMouseDevices()
        {
            FakeViiperMouseApi nativeApi = new FakeViiperMouseApi();
            MouseOutputViiperManager manager = new MouseOutputViiperManager(nativeApi);
            manager.Initialise((nuint)1234);

            Dictionary<MouseOutputDestination, MouseOutputBackendIdentity> identities =
                manager.CreateBackends().ToDictionary(backend => backend.Destination,
                    backend => backend.Identity);

            Assert.AreEqual(3, nativeApi.CreatedDevices.Count);
            CollectionAssert.AreEquivalent(
                new[] { 0x1011, 0x1012, 0x1013 },
                nativeApi.CreatedDevices.Select(item => (int)item.ProductId).ToArray());
            Assert.AreEqual(3, identities.Values.Select(identity => identity.Handle).Distinct().Count());
            Assert.AreEqual(3, identities.Values.Select(identity => identity.BusId).Distinct().Count());
            Assert.IsTrue(identities.Values.All(identity => !identity.SupportsAbsolute));
        }

        [TestMethod]
        public void ViiperMouseDeviceSplitsLargeRelativeAndWheelReports()
        {
            FakeViiperMouseApi nativeApi = new FakeViiperMouseApi();
            MouseOutputViiperMouseDevice device = new MouseOutputViiperMouseDevice(
                MouseOutputDestination.ViiperMouse1, 0x2E8A, 0x1011, nativeApi);
            Assert.IsTrue(device.TryCreate((nuint)55));

            bool result = device.TrySubmitState(VIIPERMouseButton.Button4,
                40000, -40000, 50000, -50000);

            Assert.IsTrue(result);
            Assert.AreEqual(2, nativeApi.SubmittedStates.Count);
            Assert.AreEqual(short.MaxValue, nativeApi.SubmittedStates[0].State.DX);
            Assert.AreEqual(short.MinValue, nativeApi.SubmittedStates[0].State.DY);
            Assert.AreEqual(short.MaxValue, nativeApi.SubmittedStates[0].State.Wheel);
            Assert.AreEqual(short.MinValue, nativeApi.SubmittedStates[0].State.Pan);
            Assert.AreEqual(40000 - short.MaxValue, nativeApi.SubmittedStates[1].State.DX);
            Assert.AreEqual(-40000 - short.MinValue, nativeApi.SubmittedStates[1].State.DY);
        }

        [TestMethod]
        public void ViiperManagerRefreshRecreatesFailedDevice()
        {
            FakeViiperMouseApi nativeApi = new FakeViiperMouseApi();
            MouseOutputViiperManager manager = new MouseOutputViiperManager(nativeApi);
            manager.Initialise((nuint)77);
            MouseOutputViiperMouseDevice device = manager.GetDevice(MouseOutputDestination.ViiperMouse1);
            nativeApi.FailingHandles.Add(device.Handle);

            Assert.IsFalse(device.TrySubmitState(0, 1, 0, 0, 0));
            Assert.IsFalse(device.IsAvailable);

            manager.RefreshAvailability();

            Assert.IsTrue(device.IsAvailable);
            Assert.AreEqual(4, nativeApi.CreatedDevices.Count);
            Assert.AreNotEqual(nativeApi.CreatedDevices[0].Handle, device.Handle);
        }

        [TestMethod]
        public void DispatcherDisposeDisposesEachBackend()
        {
            AppGlobalData appGlobal = CreateAppGlobal();
            IMouseOutputBackend[] backends = CreateBackends(out RecordingBackend sendInput,
                out RecordingBackend fakerInput, out RecordingBackend viiper1,
                out RecordingBackend viiper2, out RecordingBackend viiper3);
            MouseOutputDispatcher dispatcher = new MouseOutputDispatcher(appGlobal, backends);

            dispatcher.Dispose();

            Assert.AreEqual(1, sendInput.DisposeCount);
            Assert.AreEqual(1, fakerInput.DisposeCount);
            Assert.AreEqual(1, viiper1.DisposeCount);
            Assert.AreEqual(1, viiper2.DisposeCount);
            Assert.AreEqual(1, viiper3.DisposeCount);
        }

        [TestMethod]
        public void ViiperManagerDisposeRemovesEveryHandle()
        {
            FakeViiperMouseApi nativeApi = new FakeViiperMouseApi();
            MouseOutputViiperManager manager = new MouseOutputViiperManager(nativeApi);
            manager.Initialise((nuint)99);

            manager.Dispose();

            Assert.AreEqual(3, nativeApi.RemovedHandles.Count);
            Assert.AreEqual(3, nativeApi.RemovedHandles.Distinct().Count());
        }
    }
}
