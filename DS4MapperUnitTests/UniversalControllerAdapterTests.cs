using DS4MapperTest.SdlDiagnostics;
using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.Universal;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalControllerAdapterTests
    {
        [TestMethod]
        public void UniversalValuesPreserveTheirShapes()
        {
            UniversalInputValue button = UniversalInputValue.DigitalButton(true);
            UniversalInputValue trigger = UniversalInputValue.AnalogAxis(0.42);
            UniversalInputValue stick = UniversalInputValue.Stick(-0.25, 0.75);
            UniversalInputValue touch = UniversalInputValue.TouchSurface(new[]
            {
                new UniversalTouchContact(2, true, 0.1, 0.2, 0.3),
                new UniversalTouchContact(3, false, 0.4, 0.5, null),
            }, clickPressed: true);
            UniversalInputValue gyro = UniversalInputValue.Gyroscope(1, 2, 3);
            UniversalInputValue accel = UniversalInputValue.Accelerometer(4, 5, 6);

            Assert.AreEqual(UniversalInputValueKind.DigitalButton, button.Kind);
            Assert.IsTrue(button.Pressed);
            Assert.AreEqual(0.42, trigger.AxisValue, 0.0001);
            Assert.AreEqual(-0.25, stick.Vector2.X, 0.0001);
            Assert.AreEqual(0.75, stick.Vector2.Y, 0.0001);
            Assert.AreEqual(2, touch.Contacts.Count);
            Assert.IsTrue(touch.TouchClickPressed);
            Assert.AreEqual(UniversalInputValueKind.Gyroscope, gyro.Kind);
            Assert.AreEqual(UniversalInputValueKind.Accelerometer, accel.Kind);
        }

        [TestMethod]
        public void SupportedInactiveInputIsDistinguishableFromUnsupported()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Pad"),
                new[]
                {
                    Descriptor(UniversalInputId.LeftTrigger),
                });
            UniversalControllerStateSnapshot state = new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow,
                1,
                true,
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [UniversalInputId.LeftTrigger] = UniversalInputValue.AnalogAxis(0),
                });

            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftTrigger));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.RightTrigger));
            Assert.IsTrue(state.TryGetValue(UniversalInputId.LeftTrigger, out UniversalInputValue value));
            Assert.IsFalse(value.IsActive);
            Assert.IsFalse(state.TryGetValue(UniversalInputId.RightTrigger, out _));
        }

        [TestMethod]
        public void PublishedSnapshotsCannotBeMutatedThroughSourceDictionary()
        {
            Dictionary<UniversalInputId, UniversalInputValue> source =
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(true),
                };
            UniversalControllerStateSnapshot snapshot = new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow,
                1,
                true,
                source);

            source[UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(false);

            Assert.IsTrue(snapshot.Values[UniversalInputId.FaceButtonSouth].Pressed);
        }

        [TestMethod]
        public void RemovalInvalidatesLiveState()
        {
            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    "test",
                    "1",
                    new UniversalDeviceIdentity("test", "1"),
                    DateTimeOffset.UtcNow),
                new ControllerCapabilities(new ControllerDisplayInfo("Pad"), new[] { Descriptor(UniversalInputId.FaceButtonSouth) }),
                new UniversalControllerStateSnapshot(
                    DateTimeOffset.UtcNow,
                    1,
                    true,
                    new Dictionary<UniversalInputId, UniversalInputValue>
                    {
                        [UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(true),
                    }));

            controller.MarkDisconnected();

            Assert.AreEqual(UniversalControllerConnectionState.Disconnected, controller.ConnectionState);
            Assert.IsFalse(controller.State.IsConnected);
            Assert.AreEqual(0, controller.State.Values.Count);
        }

        [TestMethod]
        public void SuppressionInvalidatesLiveStateWithoutLookingDisconnected()
        {
            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    "test",
                    "1",
                    new UniversalDeviceIdentity("test", "1"),
                    DateTimeOffset.UtcNow),
                new ControllerCapabilities(new ControllerDisplayInfo("Pad"), new[] { Descriptor(UniversalInputId.FaceButtonSouth) }),
                new UniversalControllerStateSnapshot(
                    DateTimeOffset.UtcNow,
                    1,
                    true,
                    new Dictionary<UniversalInputId, UniversalInputValue>
                    {
                        [UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(true),
                    }));

            controller.MarkSuppressed();

            Assert.AreEqual(UniversalControllerConnectionState.Suppressed, controller.ConnectionState);
            Assert.IsFalse(controller.State.IsConnected);
            Assert.AreEqual(0, controller.State.Values.Count);
        }

        [TestMethod]
        public void SdlMapsStandardButtonsByPositionAndLeavesMiscNeutral()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Buttons.Add(new SdlRawButtonState { Name = "South", Supported = true, Pressed = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "East", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "West", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "North", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "DpadUp", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "LeftShoulder", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "RightStick", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "Start", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "Back", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "Guide", Supported = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "Misc1", Supported = true, Pressed = true });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(capabilities.Supports(UniversalInputId.FaceButtonSouth));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.FaceButtonEast));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.FaceButtonWest));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.FaceButtonNorth));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.DPadUp));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftShoulder));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightStickClick));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.Menu));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.View));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.System));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.MiscButton1));
            Assert.IsTrue(state.Values[UniversalInputId.FaceButtonSouth].Pressed);
            Assert.IsTrue(state.Values[UniversalInputId.MiscButton1].Pressed);
            Assert.IsFalse(capabilities.Supports(UniversalInputId.Mute));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.Capture));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.QuickAccessMenu));
        }

        [TestMethod]
        public void SdlMapsCapsenseButtonsToStickTouchCapabilities()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Buttons.Add(new SdlRawButtonState { Name = "LeftStickTouch", Supported = true, Pressed = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "RightStickCapsense", Supported = true, Pressed = false });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftStickTouch));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightStickTouch));
            Assert.IsTrue(state.Values[UniversalInputId.LeftStickTouch].Pressed);
            Assert.IsFalse(state.Values[UniversalInputId.RightStickTouch].Pressed);
        }

        [TestMethod]
        public void SdlNormalisesAxesAndPairsSticksWithoutDeadzones()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Axes.Add(new SdlRawAxisState { Name = "LeftX", Supported = true, RawValue = -32768 });
            info.Axes.Add(new SdlRawAxisState { Name = "LeftY", Supported = true, RawValue = -32768 });
            info.Axes.Add(new SdlRawAxisState { Name = "RightX", Supported = true, RawValue = 32767 });
            info.Axes.Add(new SdlRawAxisState { Name = "RightY", Supported = true, RawValue = 32767 });
            info.Axes.Add(new SdlRawAxisState { Name = "LeftTrigger", Supported = true, RawValue = 16384 });
            info.Axes.Add(new SdlRawAxisState { Name = "RightTrigger", Supported = true, RawValue = 32767 });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.AreEqual(-1.0, state.Values[UniversalInputId.LeftStick].Vector2.X, 0.0001);
            Assert.AreEqual(1.0, state.Values[UniversalInputId.LeftStick].Vector2.Y, 0.0001);
            Assert.AreEqual(1.0, state.Values[UniversalInputId.RightStick].Vector2.X, 0.0001);
            Assert.AreEqual(-1.0, state.Values[UniversalInputId.RightStick].Vector2.Y, 0.0001);
            Assert.AreEqual(16384 / 32767.0, state.Values[UniversalInputId.LeftTrigger].AxisValue, 0.0001);
            Assert.AreEqual(1.0, state.Values[UniversalInputId.RightTrigger].AxisValue, 0.0001);
        }

        [TestMethod]
        public void SdlUnavailableAxesAreAbsent()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Axes.Add(new SdlRawAxisState { Name = "LeftX", Supported = true, RawValue = 100 });

            ControllerCapabilities capabilities = new SdlUniversalStateTranslator().CreateCapabilities(info);

            Assert.IsFalse(capabilities.Supports(UniversalInputId.LeftStick));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.LeftTrigger));
        }

        [TestMethod]
        public void SdlOneTouchpadMapsToPrimaryAndDerivedHalves()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Buttons.Add(new SdlRawButtonState { Name = "Touchpad", Supported = true, Pressed = true });
            info.Touchpads.Add(new SdlRawTouchpadState
            {
                TouchpadIndex = 0,
                FingerCapacity = 2,
                Fingers = new List<SdlRawTouchFingerState>
                {
                    new SdlRawTouchFingerState { FingerIndex = 7, Active = true, X = 0.25f, Y = 0.75f, Pressure = 0.5f },
                    new SdlRawTouchFingerState { FingerIndex = 8, Active = true, X = 0.50f, Y = 0.25f, Pressure = 1.0f },
                },
            });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(capabilities.Supports(UniversalInputId.PrimaryTouchSurface));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftTouchSurface));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightTouchSurface));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftTouchSurfaceClick));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightTouchSurfaceClick));
            Assert.IsTrue(state.Values[UniversalInputId.PrimaryTouchSurfaceClick].Pressed);
            Assert.IsTrue(state.Values[UniversalInputId.PrimaryTouchSurface].TouchClickPressed);
            Assert.AreEqual(2, state.Values[UniversalInputId.PrimaryTouchSurface].Contacts.Count);
            Assert.AreEqual(7, state.Values[UniversalInputId.PrimaryTouchSurface].Contacts[0].ContactId);
            Assert.AreEqual(1, state.Values[UniversalInputId.LeftTouchSurface].Contacts.Count);
            Assert.AreEqual(7, state.Values[UniversalInputId.LeftTouchSurface].Contacts[0].ContactId);
            Assert.AreEqual(1, state.Values[UniversalInputId.RightTouchSurface].Contacts.Count);
            Assert.AreEqual(8, state.Values[UniversalInputId.RightTouchSurface].Contacts[0].ContactId);
            Assert.IsTrue(state.Values[UniversalInputId.LeftTouchSurfaceClick].Pressed);
            Assert.IsTrue(state.Values[UniversalInputId.RightTouchSurfaceClick].Pressed);
        }

        [TestMethod]
        public void SdlSingleTouchpadHalfClickRequiresTouchOnThatHalf()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Buttons.Add(new SdlRawButtonState { Name = "Touchpad", Supported = true, Pressed = true });
            info.Touchpads.Add(new SdlRawTouchpadState { TouchpadIndex = 0, FingerCapacity = 1 });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(state.Values[UniversalInputId.PrimaryTouchSurfaceClick].Pressed);
            Assert.IsFalse(state.Values[UniversalInputId.LeftTouchSurfaceClick].Pressed);
            Assert.IsFalse(state.Values[UniversalInputId.RightTouchSurfaceClick].Pressed);
        }

        [TestMethod]
        public void NintendoFaceSwapChangesLiveRoutingOnly()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Name = "Nintendo Switch Pro Controller";
            info.Buttons.Add(new SdlRawButtonState { Name = "South", Supported = true, Pressed = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "East", Supported = true, Pressed = false });
            info.Buttons.Add(new SdlRawButtonState { Name = "North", Supported = true, Pressed = true });
            info.Buttons.Add(new SdlRawButtonState { Name = "West", Supported = true, Pressed = false });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);

            UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled = false;
            UniversalControllerStateSnapshot unswapped = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);
            Assert.IsTrue(unswapped.Values[UniversalInputId.FaceButtonSouth].Pressed);
            Assert.IsFalse(unswapped.Values[UniversalInputId.FaceButtonEast].Pressed);
            Assert.IsFalse(unswapped.Values[UniversalInputId.FaceButtonWest].Pressed);
            Assert.IsTrue(unswapped.Values[UniversalInputId.FaceButtonNorth].Pressed);

            UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled = true;
            try
            {
                UniversalControllerStateSnapshot swapped = translator.CreateState(info, capabilities, true, 2, DateTimeOffset.UtcNow);
                Assert.IsFalse(swapped.Values[UniversalInputId.FaceButtonSouth].Pressed);
                Assert.IsTrue(swapped.Values[UniversalInputId.FaceButtonEast].Pressed);
                Assert.IsTrue(swapped.Values[UniversalInputId.FaceButtonWest].Pressed);
                Assert.IsFalse(swapped.Values[UniversalInputId.FaceButtonNorth].Pressed);
            }
            finally
            {
                UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled = false;
            }
        }

        [TestMethod]
        public void SdlBackendSuppressesKnownVirtualOutputControllers()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            api.AddDevice(CreateSdlDevice(91));
            SdlRawGamepadInfo virtualOutput = CreateSdlDevice(92);
            virtualOutput.Name = "Xbox 360 Controller for Windows";
            virtualOutput.DevicePath = @"root\vigem\0000";
            api.AddDevice(virtualOutput);
            using SdlUniversalControllerBackend backend = new SdlUniversalControllerBackend(api);

            Assert.IsTrue(backend.Start(out string error), error);

            Assert.AreEqual(1, backend.Controllers.Count);
            Assert.AreEqual("91", backend.Controllers[0].Identity.BackendSessionId);
            CollectionAssert.Contains(api.ClosedInstances, 92u);
        }

        [TestMethod]
        public void SdlBackendPublishesBatteryPercent()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            SdlRawGamepadInfo device = CreateSdlDevice(93);
            device.BatteryPercent = 67;
            api.AddDevice(device);
            using SdlUniversalControllerBackend backend = new SdlUniversalControllerBackend(api);

            Assert.IsTrue(backend.Start(out string error), error);

            Assert.AreEqual(67, backend.Controllers[0].BatteryPercent);
        }

        [TestMethod]
        public void SdlTwoTouchpadsRequireVerifiedPolicyForLeftAndRight()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Touchpads.Add(new SdlRawTouchpadState { TouchpadIndex = 0 });
            info.Touchpads.Add(new SdlRawTouchpadState { TouchpadIndex = 1 });

            ControllerCapabilities defaultCapabilities = new SdlUniversalStateTranslator().CreateCapabilities(info);
            Assert.IsFalse(defaultCapabilities.Supports(UniversalInputId.LeftTouchSurface));
            Assert.IsFalse(defaultCapabilities.Supports(UniversalInputId.RightTouchSurface));

            ControllerCapabilities verifiedCapabilities =
                new SdlUniversalStateTranslator(new VerifiedDualTouchpadPolicy()).CreateCapabilities(info);
            Assert.IsTrue(verifiedCapabilities.Supports(UniversalInputId.LeftTouchSurface));
            Assert.IsTrue(verifiedCapabilities.Supports(UniversalInputId.RightTouchSurface));
        }

        [TestMethod]
        public void SdlSensorsPublishOnlyWhenEnablementSucceeded()
        {
            SdlRawGamepadInfo info = CreateSdlDevice();
            info.Sensors.Add(new SdlRawSensorState { Name = "Gyro", Supported = true, Enabled = true, EnableAttempted = true, EnableSucceeded = true, Values = new[] { 1f, 2f, 3f }, Units = "radians/s" });
            info.Sensors.Add(new SdlRawSensorState { Name = "Accel", Supported = true, Enabled = false, EnableAttempted = true, EnableSucceeded = false, LastError = "denied", Units = "m/s^2" });

            SdlUniversalStateTranslator translator = new SdlUniversalStateTranslator();
            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow);

            Assert.IsTrue(capabilities.Supports(UniversalInputId.Gyroscope));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.Accelerometer));
            Assert.AreEqual(1.0, state.Values[UniversalInputId.Gyroscope].Vector3.X, 0.0001);
            Assert.IsFalse(state.Values.ContainsKey(UniversalInputId.Accelerometer));
        }

        [TestMethod]
        public void SdlOriginalSteamControllerIsSuppressedFromProductionBackend()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            api.AddDevice(CreateSdlDevice(
                88,
                OriginalSteamControllerIdentity.ValveVendorId,
                OriginalSteamControllerIdentity.WiredProductId));
            using SdlUniversalControllerBackend backend = new SdlUniversalControllerBackend(api);

            Assert.IsTrue(backend.Start(out string error), error);

            Assert.AreEqual(0, backend.Controllers.Count);
            Assert.AreEqual(1, api.ClosedInstances.Count);
        }

        [TestMethod]
        public void SdlBackendHandlesDuplicateAddRemovalAndServiceDisposal()
        {
            FakeSdlDiagnosticApi api = new FakeSdlDiagnosticApi();
            api.AddDevice(CreateSdlDevice(90));
            using SdlUniversalControllerBackend backend = new SdlUniversalControllerBackend(api);
            Assert.IsTrue(backend.Start(out _));

            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceAdded, InstanceId = 90 });
            backend.Refresh();
            Assert.AreEqual(1, backend.Controllers.Count);
            Assert.AreEqual(1, api.OpenCount[90]);

            api.QueueEvent(new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceRemoved, InstanceId = 90 });
            backend.Refresh();
            Assert.AreEqual(0, backend.Controllers.Count);
            Assert.AreEqual(1, api.ClosedInstances.Count);
        }

        [TestMethod]
        public void NativeSteamControllerMapsEstablishedInputs()
        {
            SteamControllerState state = new SteamControllerState
            {
                A = true,
                B = true,
                LB = true,
                Back = true,
                Start = true,
                Guide = true,
                LT = 128,
                RT = 255,
                LTClick = true,
                RGrip = true,
                LX = 32767,
                LY = -32768,
                LSClick = true,
                DPadUp = true,
                LeftPad = new SteamControllerState.TouchPadInfo { Touch = true, Click = true, X = -32768, Y = 32767 },
                RightPad = new SteamControllerState.TouchPadInfo { Touch = true, X = 32767, Y = -32768 },
                Motion = new SteamControllerState.SteamControllerMotion
                {
                    AngGyroPitch = 180,
                    AngGyroYaw = 90,
                    AngGyroRoll = -90,
                    AccelXG = 1,
                    AccelYG = 2,
                    AccelZG = -1,
                },
            };
            FakeSteamStateSource source = new FakeSteamStateSource { Connected = true, State = state };
            using SteamControllerUniversalController controller = new SteamControllerUniversalController(source);

            Assert.IsTrue(controller.Capabilities.Supports(UniversalInputId.LeftTouchSurface));
            Assert.IsTrue(controller.Capabilities.Supports(UniversalInputId.RightTouchSurface));
            Assert.IsTrue(controller.State.Values[UniversalInputId.FaceButtonSouth].Pressed);
            Assert.AreEqual(128 / 255.0, controller.State.Values[UniversalInputId.LeftTrigger].AxisValue, 0.0001);
            Assert.AreEqual(1.0, controller.State.Values[UniversalInputId.RightTrigger].AxisValue, 0.0001);
            Assert.IsTrue(controller.State.Values[UniversalInputId.LeftTriggerFullPull].Pressed);
            Assert.IsTrue(controller.State.Values[UniversalInputId.RightRearPrimary].Pressed);
            Assert.AreEqual(1.0, controller.State.Values[UniversalInputId.LeftStick].Vector2.X, 0.0001);
            Assert.AreEqual(-1.0, controller.State.Values[UniversalInputId.LeftStick].Vector2.Y, 0.0001);
            Assert.IsTrue(controller.State.Values[UniversalInputId.LeftTouchSurfaceClick].Pressed);
            Assert.AreEqual(Math.PI, controller.State.Values[UniversalInputId.Gyroscope].Vector3.X, 0.0001);
            Assert.AreEqual(9.80665, controller.State.Values[UniversalInputId.Accelerometer].Vector3.X, 0.0001);
        }

        [TestMethod]
        public void NativeSteamControllerDisconnectionStopsLiveStatePublication()
        {
            FakeSteamStateSource source = new FakeSteamStateSource
            {
                Connected = true,
                State = new SteamControllerState { A = true },
            };
            using SteamControllerUniversalController controller = new SteamControllerUniversalController(source);
            Assert.IsTrue(controller.State.Values[UniversalInputId.FaceButtonSouth].Pressed);

            source.Connected = false;
            controller.Refresh();

            Assert.AreEqual(UniversalControllerConnectionState.Disconnected, controller.ConnectionState);
            Assert.IsFalse(controller.State.IsConnected);
            Assert.AreEqual(0, controller.State.Values.Count);
        }

        [TestMethod]
        public void NativeSteamAdapterDisposalDoesNotDisposeBorrowedReader()
        {
            FakeSteamStateSource source = new FakeSteamStateSource { Connected = true, Owns = false };
            new SteamControllerUniversalController(source).Dispose();

            Assert.IsFalse(source.Disposed);
        }

        [TestMethod]
        public void NativeSteamAdapterDisposesOwnedSource()
        {
            FakeSteamStateSource source = new FakeSteamStateSource { Connected = true, Owns = true };
            new SteamControllerUniversalController(source).Dispose();

            Assert.IsTrue(source.Disposed);
        }

        [TestMethod]
        public void ArbitrationKeepsOriginalSteamControllerOnNativeBackend()
        {
            IUniversalController native = CreateController(
                UniversalControllerBackendIds.SteamControllerNative,
                "native-1",
                true);
            IUniversalController sdl = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "sdl-1",
                true);
            IUniversalController xbox = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "sdl-2",
                false);

            IReadOnlyList<IUniversalController> selected =
                UniversalBackendArbitrator.SelectAuthoritativeControllers(new[] { sdl, native, xbox });

            Assert.AreEqual(2, selected.Count);
            Assert.IsTrue(selected.Contains(native));
            Assert.IsTrue(selected.Contains(xbox));
            Assert.IsFalse(selected.Contains(sdl));
        }

        [TestMethod]
        public void ArbitrationDoesNotCollapseIdenticalModelsWithDifferentSessions()
        {
            IUniversalController first = CreateController(UniversalControllerBackendIds.Sdl3, "1", false, 0x045E, 0x02EA);
            IUniversalController second = CreateController(UniversalControllerBackendIds.Sdl3, "2", false, 0x045E, 0x02EA);

            IReadOnlyList<IUniversalController> selected =
                UniversalBackendArbitrator.SelectAuthoritativeControllers(new[] { first, second });

            Assert.AreEqual(2, selected.Count);
        }

        [TestMethod]
        public void ManagerSuppressesDuplicateBackendSessionIds()
        {
            FakeUniversalBackend backend = new FakeUniversalBackend(
                CreateController(UniversalControllerBackendIds.Sdl3, "same", false),
                CreateController(UniversalControllerBackendIds.Sdl3, "same", false));
            using UniversalControllerManager manager = new UniversalControllerManager(new[] { backend });
            manager.Start(out _);

            Assert.AreEqual(1, manager.Controllers.Count);
        }

        [TestMethod]
        public void ManagerOmitsDisconnectedAndSuppressedControllers()
        {
            UniversalController connected = (UniversalController)CreateController(
                UniversalControllerBackendIds.Sdl3, "connected", false);
            UniversalController disconnected = (UniversalController)CreateController(
                UniversalControllerBackendIds.Sdl3, "disconnected", false);
            UniversalController suppressed = (UniversalController)CreateController(
                UniversalControllerBackendIds.Sdl3, "suppressed", false);
            disconnected.MarkDisconnected();
            suppressed.MarkSuppressed();

            FakeUniversalBackend backend = new FakeUniversalBackend(connected, disconnected, suppressed);
            using UniversalControllerManager manager = new UniversalControllerManager(new[] { backend });
            manager.Start(out _);

            Assert.AreEqual(1, manager.Controllers.Count);
            Assert.AreSame(connected, manager.Controllers[0]);
        }

        [TestMethod]
        public void KnownVirtualOutputDevicesAreSuppressedBeforeSelection()
        {
            SdlRawGamepadInfo info = CreateSdlDevice(vendorId: 0, productId: 0);
            info.Name = "Xbox 360 Controller for Windows";
            info.MappingName = "xinput";
            info.DevicePath = @"usbip\viiper\virtual-xbox";

            Assert.IsTrue(SdlUniversalStateTranslator.IsKnownVirtualOutputController(info));
            Assert.IsTrue(new SdlUniversalStateTranslator().ShouldSuppressForNativeSteamController(info));
        }

        [TestMethod]
        public void UnknownIdentityFieldsRemainUnknown()
        {
            UniversalDeviceIdentity identity = new UniversalDeviceIdentity("sdl3", "42");

            Assert.IsNull(identity.VendorId);
            Assert.IsNull(identity.ProductId);
            Assert.AreEqual(string.Empty, identity.SerialNumber);
            Assert.AreEqual(string.Empty, identity.StrongPhysicalKey);
        }

        private static ControllerInputDescriptor Descriptor(UniversalInputId id)
        {
            return new ControllerInputDescriptor(id, UniversalInputCatalog.GetMetadata(id).ValueKind);
        }

        private static SdlRawGamepadInfo CreateSdlDevice(
            uint instanceId = 1,
            ushort vendorId = 0x1234,
            ushort productId = 0x5678)
        {
            return new SdlRawGamepadInfo
            {
                InstanceId = instanceId,
                Name = "Synthetic SDL Pad",
                Guid = $"guid-{instanceId}",
                VendorId = vendorId,
                ProductId = productId,
                SerialNumber = string.Empty,
                BestEffortPersistentKey = $"guid-{instanceId}|vid-{vendorId:X4}|pid-{productId:X4}|serial-unknown",
                IsMappedGamepad = true,
                IdentityNotes = "test identity",
            };
        }

        private static IUniversalController CreateController(
            string backend,
            string session,
            bool originalSteam,
            ushort? vendorId = null,
            ushort? productId = null)
        {
            UniversalDeviceIdentity deviceIdentity = new UniversalDeviceIdentity(
                backend,
                session,
                vendorId.HasValue && productId.HasValue
                    ? $"vid-{vendorId.Value:X4}|pid-{productId.Value:X4}"
                    : string.Empty,
                vendorId,
                productId,
                string.Empty,
                string.Empty,
                string.Empty,
                originalSteam);

            return new UniversalController(
                new UniversalControllerIdentity(Guid.NewGuid(), backend, session, deviceIdentity, DateTimeOffset.UtcNow),
                new ControllerCapabilities(new ControllerDisplayInfo("Pad"), new[] { Descriptor(UniversalInputId.FaceButtonSouth) }),
                new UniversalControllerStateSnapshot(
                    DateTimeOffset.UtcNow,
                    1,
                    true,
                    new Dictionary<UniversalInputId, UniversalInputValue>
                    {
                        [UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(false),
                    }));
        }

        private sealed class VerifiedDualTouchpadPolicy : ISdlTouchpadMappingPolicy
        {
            public IReadOnlyDictionary<int, SdlUniversalTouchSurfaceTarget> MapTouchpads(SdlRawGamepadInfo info)
            {
                return new Dictionary<int, SdlUniversalTouchSurfaceTarget>
                {
                    [0] = SdlUniversalTouchSurfaceTarget.Left,
                    [1] = SdlUniversalTouchSurfaceTarget.Right,
                };
            }
        }

        private sealed class FakeSteamStateSource : ISteamControllerNativeStateSource
        {
            public string SessionId { get; set; } = "steam-session";
            public string DisplayName { get; set; } = "Steam Controller";
            public string DevicePath { get; set; } = @"hid\vid_28de&pid_1102";
            public string SerialNumber { get; set; } = string.Empty;
            public ushort? VendorId { get; set; } = OriginalSteamControllerIdentity.ValveVendorId;
            public ushort? ProductId { get; set; } = OriginalSteamControllerIdentity.WiredProductId;
            public bool Connected { get; set; }
            public bool IsConnected => Connected;
            public bool Owns { get; set; }
            public bool OwnsReader => Owns;
            public int? BatteryPercent { get; set; }
            public bool Disposed { get; private set; }
            public SteamControllerState State { get; set; }

            public SteamControllerState ReadState() => State;
            public void Dispose() => Disposed = true;
        }

        private sealed class FakeSdlDiagnosticApi : ISdlDiagnosticApi
        {
            private readonly Dictionary<uint, SdlRawGamepadInfo> devices = new Dictionary<uint, SdlRawGamepadInfo>();
            private readonly Queue<SdlDiagnosticEvent> events = new Queue<SdlDiagnosticEvent>();
            private readonly Dictionary<IntPtr, uint> handles = new Dictionary<IntPtr, uint>();

            public SdlDiagnosticVersionInfo VersionInfo { get; } = new SdlDiagnosticVersionInfo();
            public Dictionary<uint, int> OpenCount { get; } = new Dictionary<uint, int>();
            public List<uint> ClosedInstances { get; } = new List<uint>();

            public void AddDevice(SdlRawGamepadInfo info) => devices[info.InstanceId] = info;
            public void QueueEvent(SdlDiagnosticEvent diagnosticEvent) => events.Enqueue(diagnosticEvent);
            public bool Initialise(out string error) { error = string.Empty; return true; }
            public void Shutdown() { }
            public IReadOnlyList<uint> EnumerateGamepads(out string error) { error = string.Empty; return devices.Keys.ToList(); }
            public SdlRawGamepadInfo QueryGamepadInfo(uint instanceId, SdlGamepadHandle handle) => devices[instanceId].Clone();

            public SdlGamepadHandle OpenGamepad(uint instanceId, out string error)
            {
                OpenCount.TryGetValue(instanceId, out int count);
                OpenCount[instanceId] = count + 1;
                IntPtr handle = new IntPtr(instanceId + 1000);
                handles[handle] = instanceId;
                error = string.Empty;
                return new SdlGamepadHandle(handle);
            }

            public void CloseGamepad(SdlGamepadHandle handle)
            {
                if (handles.TryGetValue(handle.NativeHandle, out uint instanceId))
                {
                    ClosedInstances.Add(instanceId);
                }
            }

            public bool PollEvent(out SdlDiagnosticEvent diagnosticEvent)
            {
                if (events.Count == 0)
                {
                    diagnosticEvent = null;
                    return false;
                }

                diagnosticEvent = events.Dequeue();
                return true;
            }

            public void RefreshGamepads() { }
            public void RefreshSensors() { }
            public void RefreshLiveState(SdlGamepadHandle handle, SdlRawGamepadInfo info) { }
        }

        private sealed class FakeUniversalBackend : IUniversalControllerBackend
        {
            public string BackendName => "fake";
            public IReadOnlyList<IUniversalController> Controllers { get; }
            public event EventHandler ControllersChanged;

            public FakeUniversalBackend(params IUniversalController[] controllers)
            {
                Controllers = controllers;
            }

            public bool Start(out string error)
            {
                error = string.Empty;
                ControllersChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }

            public void Refresh() { }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
