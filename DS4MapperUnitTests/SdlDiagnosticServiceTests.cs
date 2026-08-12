using DS4MapperTest.SdlDiagnostics;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class SdlDiagnosticServiceTests
    {
        [TestMethod]
        public void ServiceInitialisesAndShutsDown()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            SdlDiagnosticService service = new SdlDiagnosticService(api);

            Assert.IsTrue(service.Start(out string error), error);
            service.Dispose();

            Assert.IsTrue(api.Initialised);
            Assert.IsTrue(api.ShutdownCalled);
        }

        [TestMethod]
        public void InitialisationFailureIsReported()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi { InitResult = false, InitError = "native load failed" };
            SdlDiagnosticService service = new SdlDiagnosticService(api);

            Assert.IsFalse(service.Start(out string error));
            Assert.AreEqual("native load failed", error);
            Assert.IsTrue(service.CreateSnapshot().Errors.Any(item => item.Contains("native load failed")));
        }

        [TestMethod]
        public void EnumerationSupportsZeroOneAndMultipleDevices()
        {
            using SdlDiagnosticService emptyService = new SdlDiagnosticService(new FakeSdlDiagnosticApi());
            Assert.IsTrue(emptyService.Start(out _));
            Assert.AreEqual(0, emptyService.CreateSnapshot().Devices.Count);

            FakeSdlDiagnosticApi oneApi = new FakeSdlDiagnosticApi();
            oneApi.AddDevice(CreateDevice(1, "One"));
            using SdlDiagnosticService oneService = new SdlDiagnosticService(oneApi);
            Assert.IsTrue(oneService.Start(out _));
            Assert.AreEqual(1, oneService.CreateSnapshot().Devices.Count);

            FakeSdlDiagnosticApi multiApi = new FakeSdlDiagnosticApi();
            multiApi.AddDevice(CreateDevice(2, "Two"));
            multiApi.AddDevice(CreateDevice(3, "Three"));
            using SdlDiagnosticService multiService = new SdlDiagnosticService(multiApi);
            Assert.IsTrue(multiService.Start(out _));
            Assert.AreEqual(2, multiService.CreateSnapshot().Devices.Count);
        }

        [TestMethod]
        public void OneDeviceFailingToOpenDoesNotBlockOthers()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            api.AddDevice(CreateDevice(10, "Fails"));
            api.AddDevice(CreateDevice(11, "Works"));
            api.OpenFailures.Add(10, "denied");
            using SdlDiagnosticService service = new SdlDiagnosticService(api);

            Assert.IsTrue(service.Start(out _));
            SdlDiagnosticSessionSnapshot snapshot = service.CreateSnapshot();

            Assert.AreEqual(1, snapshot.Devices.Count);
            Assert.AreEqual(11u, snapshot.Devices[0].InstanceId);
            Assert.IsTrue(snapshot.Errors.Any(item => item.Contains("denied")));
        }

        [TestMethod]
        public void AddRemoveDuplicateAndRepeatedConnectionAreHandled()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            using SdlDiagnosticService service = new SdlDiagnosticService(api);
            Assert.IsTrue(service.Start(out _));

            api.AddDevice(CreateDevice(20, "Pad"));
            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceAdded, InstanceId = 20 });
            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceAdded, InstanceId = 20 });
            service.Refresh();
            Assert.AreEqual(1, service.CreateSnapshot().Devices.Count);
            Assert.AreEqual(1, api.OpenCount[20]);

            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceRemoved, InstanceId = 20 });
            service.Refresh();
            Assert.IsFalse(service.CreateSnapshot().Devices.Single().Connected);
            Assert.IsTrue(api.ClosedInstances.Contains(20));

            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceAdded, InstanceId = 20 });
            service.Refresh();
            Assert.IsTrue(service.CreateSnapshot().Devices.Single().Connected);
            Assert.AreEqual(2, api.OpenCount[20]);
        }

        [TestMethod]
        public void RemovalClearsLiveState()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            SdlRawGamepadInfo device = CreateDevice(30, "Live");
            device.Buttons[0].Pressed = true;
            device.Axes[0].RawValue = 1234;
            device.Touchpads[0].Fingers[0].Active = true;
            device.Sensors[0].Enabled = true;
            device.Sensors[0].Values = new[] { 1f, 2f, 3f };
            api.AddDevice(device);
            using SdlDiagnosticService service = new SdlDiagnosticService(api);
            Assert.IsTrue(service.Start(out _));

            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceRemoved, InstanceId = 30 });
            service.Refresh();

            SdlRawGamepadInfo info = service.CreateSnapshot().Devices.Single().Info;
            Assert.IsFalse(info.Buttons[0].Pressed);
            Assert.AreEqual(0, info.Axes[0].RawValue);
            Assert.IsFalse(info.Touchpads[0].Fingers[0].Active);
            Assert.IsFalse(info.Sensors[0].Enabled);
        }

        [TestMethod]
        public void ButtonAxisTouchpadAndSensorUpdatesAreReflected()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            api.AddDevice(CreateDevice(40, "Stateful"));
            using SdlDiagnosticService service = new SdlDiagnosticService(api);
            Assert.IsTrue(service.Start(out _));

            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.ButtonChanged, InstanceId = 40, ControlIndex = 0, ControlName = "South", ButtonPressed = true });
            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.AxisChanged, InstanceId = 40, ControlIndex = 0, AxisValue = -32768 });
            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.TouchpadChanged, InstanceId = 40, TouchpadIndex = 0, FingerIndex = 0, TouchActive = true, X = 0.25f, Y = 0.5f, Pressure = 0.75f });
            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.SensorChanged, InstanceId = 40, SensorName = "Gyro", SensorValues = new[] { 4f, 5f, 6f } });
            service.Refresh();

            SdlRawGamepadInfo info = service.CreateSnapshot().Devices.Single().Info;
            Assert.IsTrue(info.Buttons[0].Pressed);
            Assert.AreEqual(-32768, info.Axes[0].RawValue);
            Assert.AreEqual(-1.0, info.Axes[0].NormalizedValue);
            Assert.IsTrue(info.Touchpads[0].Fingers[0].Active);
            CollectionAssert.AreEqual(new[] { 4f, 5f, 6f }, info.Sensors[0].Values);
        }

        [TestMethod]
        public void UnknownIdentityFieldsRemainUnknown()
        {
            SdlRawGamepadInfo device = CreateDevice(50, "Identity");
            device.Guid = string.Empty;
            device.VendorId = null;
            device.ProductId = null;
            device.SerialNumber = string.Empty;
            device.BestEffortPersistentKey = "guid-unknown|vid-unknown|pid-unknown|serial-unknown";
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            api.AddDevice(device);
            using SdlDiagnosticService service = new SdlDiagnosticService(api);

            Assert.IsTrue(service.Start(out _));
            SdlDiagnosticDeviceSnapshot snapshot = service.CreateSnapshot().Devices.Single();

            Assert.AreEqual(50u, snapshot.InstanceId);
            Assert.IsTrue(snapshot.Info.BestEffortPersistentKey.Contains("guid-unknown"));
            Assert.IsTrue(snapshot.Info.IdentityNotes.Contains("session-local"));
        }

        [TestMethod]
        public void SensorEnableFailureIsPreserved()
        {
            SdlRawGamepadInfo device = CreateDevice(60, "Sensor Failure");
            device.Sensors[0].EnableSucceeded = false;
            device.Sensors[0].LastError = "sensor denied";
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            api.AddDevice(device);
            using SdlDiagnosticService service = new SdlDiagnosticService(api);

            Assert.IsTrue(service.Start(out _));

            SdlRawSensorState sensor = service.CreateSnapshot().Devices.Single().Info.Sensors[0];
            Assert.IsTrue(sensor.Supported);
            Assert.IsTrue(sensor.EnableAttempted);
            Assert.IsFalse(sensor.EnableSucceeded);
            Assert.AreEqual("sensor denied", sensor.LastError);
        }

        private static SdlRawGamepadInfo CreateDevice(uint instanceId, string name)
        {
            return new SdlRawGamepadInfo
            {
                InstanceId = instanceId,
                Name = name,
                Guid = $"guid-{instanceId}",
                VendorId = 0x1234,
                ProductId = 0x5678,
                ProductVersion = 1,
                IsMappedGamepad = true,
                BestEffortPersistentKey = $"guid-{instanceId}|vid-1234|pid-5678|serial-unknown",
                IdentityNotes = "SDL joystick instance IDs are session-local.",
                Buttons = new List<SdlRawButtonState> { new SdlRawButtonState { Index = 0, Name = "South", Supported = true }, new SdlRawButtonState { Index = 1, Name = "Misc1", Supported = true } },
                Axes = new List<SdlRawAxisState> { new SdlRawAxisState { Index = 0, Name = "LeftX", Supported = true } },
                Touchpads = new List<SdlRawTouchpadState> { new SdlRawTouchpadState { TouchpadIndex = 0, FingerCapacity = 2, Fingers = new List<SdlRawTouchFingerState> { new SdlRawTouchFingerState { FingerIndex = 0 }, new SdlRawTouchFingerState { FingerIndex = 1 } } } },
                Sensors = new List<SdlRawSensorState> { new SdlRawSensorState { Name = "Gyro", Supported = true, Enabled = true, EnableAttempted = true, EnableSucceeded = true, Units = "radians/s" }, new SdlRawSensorState { Name = "Accel", Supported = true, Enabled = true, EnableAttempted = true, EnableSucceeded = true, Units = "m/s^2" } },
            };
        }

        private sealed class FakeSdlDiagnosticApi : ISdlDiagnosticApi
        {
            private readonly Queue<SdlDiagnosticEvent> events = new Queue<SdlDiagnosticEvent>();
            private readonly Dictionary<uint, SdlRawGamepadInfo> devices = new Dictionary<uint, SdlRawGamepadInfo>();
            private readonly Dictionary<IntPtr, uint> handles = new Dictionary<IntPtr, uint>();

            public bool InitResult { get; set; } = true;
            public string InitError { get; set; } = string.Empty;
            public bool Initialised { get; private set; }
            public bool ShutdownCalled { get; private set; }
            public Dictionary<uint, string> OpenFailures { get; } = new Dictionary<uint, string>();
            public Dictionary<uint, int> OpenCount { get; } = new Dictionary<uint, int>();
            public List<uint> ClosedInstances { get; } = new List<uint>();
            public SdlDiagnosticVersionInfo VersionInfo { get; } = new SdlDiagnosticVersionInfo();

            public void AddDevice(SdlRawGamepadInfo info) => devices[info.InstanceId] = info;
            public void QueueEvent(SdlDiagnosticEvent diagnosticEvent) => events.Enqueue(diagnosticEvent);
            public bool Initialise(out string error) { error = InitError; Initialised = InitResult; return InitResult; }
            public void Shutdown() => ShutdownCalled = true;
            public IReadOnlyList<uint> EnumerateGamepads(out string error) { error = string.Empty; return devices.Keys.ToList(); }
            public SdlRawGamepadInfo QueryGamepadInfo(uint instanceId, SdlGamepadHandle handle) => devices[instanceId].Clone();

            public SdlGamepadHandle OpenGamepad(uint instanceId, out string error)
            {
                OpenCount.TryGetValue(instanceId, out int count);
                OpenCount[instanceId] = count + 1;
                if (OpenFailures.TryGetValue(instanceId, out error)) return new SdlGamepadHandle(IntPtr.Zero);
                IntPtr handle = new IntPtr(instanceId + OpenCount[instanceId] * 1000);
                handles[handle] = instanceId;
                error = string.Empty;
                return new SdlGamepadHandle(handle);
            }

            public void CloseGamepad(SdlGamepadHandle handle)
            {
                if (handles.TryGetValue(handle.NativeHandle, out uint instanceId))
                {
                    ClosedInstances.Add(instanceId);
                    handles.Remove(handle.NativeHandle);
                }
            }

            public bool PollEvent(out SdlDiagnosticEvent diagnosticEvent)
            {
                if (events.Count == 0) { diagnosticEvent = null; return false; }
                diagnosticEvent = events.Dequeue();
                return true;
            }

            public void RefreshGamepads() { }
            public void RefreshSensors() { }
            public void RefreshLiveState(SdlGamepadHandle handle, SdlRawGamepadInfo info) { }
        }
    }
}
