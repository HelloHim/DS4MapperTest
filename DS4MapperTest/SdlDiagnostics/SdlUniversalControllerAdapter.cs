using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NLog;
using DS4MapperTest.Universal;

namespace DS4MapperTest.SdlDiagnostics
{
    internal enum SdlUniversalTouchSurfaceTarget
    {
        Primary,
        Left,
        Right,
    }

    internal interface ISdlTouchpadMappingPolicy
    {
        IReadOnlyDictionary<int, SdlUniversalTouchSurfaceTarget> MapTouchpads(SdlRawGamepadInfo info);
    }

    internal sealed class DefaultSdlTouchpadMappingPolicy : ISdlTouchpadMappingPolicy
    {
        public IReadOnlyDictionary<int, SdlUniversalTouchSurfaceTarget> MapTouchpads(SdlRawGamepadInfo info)
        {
            if (info?.Touchpads == null || info.Touchpads.Count == 0)
            {
                return new ReadOnlyDictionary<int, SdlUniversalTouchSurfaceTarget>(
                    new Dictionary<int, SdlUniversalTouchSurfaceTarget>());
            }

            if (info.Touchpads.Count == 1)
            {
                return new ReadOnlyDictionary<int, SdlUniversalTouchSurfaceTarget>(
                    new Dictionary<int, SdlUniversalTouchSurfaceTarget>
                    {
                        [info.Touchpads[0].TouchpadIndex] = SdlUniversalTouchSurfaceTarget.Primary,
                    });
            }

            if (info.Touchpads.Count == 2)
            {
                return new ReadOnlyDictionary<int, SdlUniversalTouchSurfaceTarget>(
                    new Dictionary<int, SdlUniversalTouchSurfaceTarget>
                    {
                        [info.Touchpads[0].TouchpadIndex] = SdlUniversalTouchSurfaceTarget.Left,
                        [info.Touchpads[1].TouchpadIndex] = SdlUniversalTouchSurfaceTarget.Right,
                    });
            }

            // Do not assign more complex layouts without verified device metadata.
            return new ReadOnlyDictionary<int, SdlUniversalTouchSurfaceTarget>(
                new Dictionary<int, SdlUniversalTouchSurfaceTarget>());
        }
    }

    internal sealed class SdlUniversalStateTranslator
    {
        private static readonly IReadOnlyDictionary<string, UniversalInputId> ButtonMap =
            new Dictionary<string, UniversalInputId>(StringComparer.OrdinalIgnoreCase)
            {
                ["South"] = UniversalInputId.FaceButtonSouth,
                ["East"] = UniversalInputId.FaceButtonEast,
                ["West"] = UniversalInputId.FaceButtonWest,
                ["North"] = UniversalInputId.FaceButtonNorth,
                ["DpadUp"] = UniversalInputId.DPadUp,
                ["DpadDown"] = UniversalInputId.DPadDown,
                ["DpadLeft"] = UniversalInputId.DPadLeft,
                ["DpadRight"] = UniversalInputId.DPadRight,
                ["LeftShoulder"] = UniversalInputId.LeftShoulder,
                ["RightShoulder"] = UniversalInputId.RightShoulder,
                ["LeftStick"] = UniversalInputId.LeftStickClick,
                ["RightStick"] = UniversalInputId.RightStickClick,
                ["LeftStickTouch"] = UniversalInputId.LeftStickTouch,
                ["RightStickTouch"] = UniversalInputId.RightStickTouch,
                ["LeftStickTouchSensor"] = UniversalInputId.LeftStickTouch,
                ["RightStickTouchSensor"] = UniversalInputId.RightStickTouch,
                ["LeftStickCapsense"] = UniversalInputId.LeftStickTouch,
                ["RightStickCapsense"] = UniversalInputId.RightStickTouch,
                ["Start"] = UniversalInputId.Menu,
                ["Back"] = UniversalInputId.View,
                ["Guide"] = UniversalInputId.System,
                ["Touchpad"] = UniversalInputId.PrimaryTouchSurfaceClick,
                ["RightPaddle1"] = UniversalInputId.RightRearPrimary,
                ["LeftPaddle1"] = UniversalInputId.LeftRearPrimary,
                ["RightPaddle2"] = UniversalInputId.RightRearSecondary,
                ["LeftPaddle2"] = UniversalInputId.LeftRearSecondary,
                ["Misc1"] = UniversalInputId.MiscButton1,
                ["Misc2"] = UniversalInputId.MiscButton2,
                ["Misc3"] = UniversalInputId.MiscButton3,
                ["Misc4"] = UniversalInputId.MiscButton4,
                ["Misc5"] = UniversalInputId.MiscButton5,
                ["Misc6"] = UniversalInputId.MiscButton6,
            };

        private readonly ISdlTouchpadMappingPolicy touchpadMappingPolicy;

        public SdlUniversalStateTranslator(ISdlTouchpadMappingPolicy touchpadMappingPolicy = null)
        {
            this.touchpadMappingPolicy = touchpadMappingPolicy ?? new DefaultSdlTouchpadMappingPolicy();
        }

        public UniversalDeviceIdentity CreateDeviceIdentity(SdlRawGamepadInfo info)
        {
            return new UniversalDeviceIdentity(
                UniversalControllerBackendIds.Sdl3,
                info.InstanceId.ToString(),
                info.BestEffortPersistentKey,
                info.VendorId,
                info.ProductId,
                info.SerialNumber,
                info.DevicePath,
                info.Guid,
                OriginalSteamControllerIdentity.IsOriginalSteamController(info.VendorId, info.ProductId),
                info.IdentityNotes);
        }

        public ControllerCapabilities CreateCapabilities(SdlRawGamepadInfo info)
        {
            List<ControllerInputDescriptor> descriptors = new List<ControllerInputDescriptor>();
            string nativeId = info.InstanceId.ToString();

            foreach (SdlRawButtonState button in info.Buttons.Where(item => item.Supported))
            {
                if (TryMapButton(info, button.Name, out UniversalInputId inputId))
                {
                    inputId = ApplyFaceButtonSwap(info, inputId);
                    AddDescriptor(descriptors, inputId, info, $"button:{button.Name}", button.Name);
                }
            }

            if (HasAxis(info, "LeftX") && HasAxis(info, "LeftY"))
            {
                AddDescriptor(descriptors, UniversalInputId.LeftStick, info, "axes:LeftX,LeftY", "Left Stick");
            }

            if (HasAxis(info, "RightX") && HasAxis(info, "RightY"))
            {
                AddDescriptor(descriptors, UniversalInputId.RightStick, info, "axes:RightX,RightY", "Right Stick");
            }

            if (HasAxis(info, "LeftTrigger"))
            {
                AddDescriptor(descriptors, UniversalInputId.LeftTrigger, info, "axis:LeftTrigger", "Left Trigger");
            }

            if (HasAxis(info, "RightTrigger"))
            {
                AddDescriptor(descriptors, UniversalInputId.RightTrigger, info, "axis:RightTrigger", "Right Trigger");
            }

            foreach (KeyValuePair<int, SdlUniversalTouchSurfaceTarget> mapping in touchpadMappingPolicy.MapTouchpads(info))
            {
                AddDescriptor(descriptors, TouchSurfaceId(mapping.Value), info, $"touchpad:{mapping.Key}", TouchLabel(mapping.Value));
                if (mapping.Value == SdlUniversalTouchSurfaceTarget.Primary)
                {
                    AddDescriptor(descriptors, UniversalInputId.LeftTouchSurface, info, $"touchpad:{mapping.Key}:left", "Left Touchpad");
                    AddDescriptor(descriptors, UniversalInputId.RightTouchSurface, info, $"touchpad:{mapping.Key}:right", "Right Touchpad");
                    AddDescriptor(descriptors, UniversalInputId.LeftTouchSurfaceClick, info, $"touchpad:{mapping.Key}:left-click", "Left Touchpad Click");
                    AddDescriptor(descriptors, UniversalInputId.RightTouchSurfaceClick, info, $"touchpad:{mapping.Key}:right-click", "Right Touchpad Click");
                }
                else
                {
                    AddDescriptor(descriptors, TouchClickId(mapping.Value), info,
                        $"touchpad:{mapping.Key}:click", $"{TouchLabel(mapping.Value)} Click");
                }
            }

            if (HasEnabledSensor(info, "Gyro"))
            {
                AddDescriptor(descriptors, UniversalInputId.Gyroscope, info, "sensor:Gyro", "Gyro");
            }

            if (HasEnabledSensor(info, "Accel"))
            {
                AddDescriptor(descriptors, UniversalInputId.Accelerometer, info, "sensor:Accel", "Accelerometer");
            }

            return new ControllerCapabilities(CreateDisplayInfo(info), descriptors);
        }

        public UniversalControllerStateSnapshot CreateState(
            SdlRawGamepadInfo info,
            ControllerCapabilities capabilities,
            bool connected,
            long sequence,
            DateTimeOffset timestampUtc)
        {
            if (!connected)
            {
                return new UniversalControllerStateSnapshot(
                    timestampUtc,
                    sequence,
                    false,
                    new Dictionary<UniversalInputId, UniversalInputValue>());
            }

            Dictionary<UniversalInputId, UniversalInputValue> values =
                new Dictionary<UniversalInputId, UniversalInputValue>();

            foreach (SdlRawButtonState button in info.Buttons.Where(item => item.Supported))
            {
                if (TryMapButton(info, button.Name, out UniversalInputId inputId))
                {
                    inputId = ApplyFaceButtonSwap(info, inputId);
                    if (capabilities.Supports(inputId))
                    {
                        values[inputId] = UniversalInputValue.DigitalButton(button.Pressed);
                    }
                }
            }

            AddDerivedDualPadButtonAliases(info, capabilities, values);

            AddStickValue(info, capabilities, values, UniversalInputId.LeftStick, "LeftX", "LeftY");
            AddStickValue(info, capabilities, values, UniversalInputId.RightStick, "RightX", "RightY");
            AddTriggerValue(info, capabilities, values, UniversalInputId.LeftTrigger, "LeftTrigger");
            AddTriggerValue(info, capabilities, values, UniversalInputId.RightTrigger, "RightTrigger");
            AddTouchpadValues(info, capabilities, values);
            AddSensorValue(info, capabilities, values, UniversalInputId.Gyroscope, "Gyro");
            AddSensorValue(info, capabilities, values, UniversalInputId.Accelerometer, "Accel");

            return new UniversalControllerStateSnapshot(
                timestampUtc,
                sequence,
                true,
                values);
        }

        public bool ShouldSuppressForNativeSteamController(SdlRawGamepadInfo info)
        {
            return OriginalSteamControllerIdentity.IsOriginalSteamController(info.VendorId, info.ProductId) ||
                IsKnownVirtualOutputController(info);
        }

        public static bool IsKnownVirtualOutputController(SdlRawGamepadInfo info)
        {
            string text = $"{info?.Name} {info?.MappingName} {info?.DevicePath}".ToLowerInvariant();
            bool hasKnownPhysicalHardwareId = HasKnownPhysicalHardwareId(info);
            bool hasExplicitVirtualMarker =
                text.Contains("fakerinput") ||
                text.Contains("viiper") ||
                text.Contains("vigem") ||
                text.Contains("nefarius");
            bool hasGenericVirtualMarker =
                !hasKnownPhysicalHardwareId &&
                text.Contains("virtual");
            bool hasUsbIpOutputIdentity =
                text.Contains("usbip") &&
                HasVirtualOutputIdentity(info, text);

            return hasExplicitVirtualMarker ||
                hasGenericVirtualMarker ||
                hasUsbIpOutputIdentity ||
                IsVirtualDevicePath(info?.DevicePath) ||
                (!hasKnownPhysicalHardwareId &&
                    (text.Contains("virtual xbox") ||
                    text.Contains("virtual dualshock") ||
                    text.Contains("virtual dualsense")));
        }

        private static bool HasVirtualOutputIdentity(SdlRawGamepadInfo info, string text)
        {
            if (info == null ||
                HasKnownPhysicalHardwareId(info))
            {
                return false;
            }

            return text.Contains("xbox 360 controller for windows") ||
                text.Contains("dualshock 4") ||
                text.Contains("ps4 controller") ||
                text.Contains("dualsense") ||
                text.Contains("switch pro controller 2");
        }

        private static bool HasKnownPhysicalHardwareId(SdlRawGamepadInfo info)
        {
            return (info.VendorId.HasValue && info.VendorId.Value != 0) ||
                (info.ProductId.HasValue && info.ProductId.Value != 0);
        }

        private static bool IsVirtualDevicePath(string devicePath)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
            {
                return false;
            }

            try
            {
                return Util.CheckIfVirtualDevice(devicePath);
            }
            catch
            {
                return false;
            }
        }

        private static void AddDescriptor(
            List<ControllerInputDescriptor> descriptors,
            UniversalInputId inputId,
            SdlRawGamepadInfo info,
            string nativeElement,
            string nativeLabel)
        {
            if (descriptors.Any(item => item.InputId == inputId))
            {
                return;
            }

            UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(inputId);
            descriptors.Add(new ControllerInputDescriptor(
                inputId,
                metadata.ValueKind,
                true,
                nativeLabel,
                string.Empty,
                new ControllerInputSource(
                    UniversalControllerBackendIds.Sdl3,
                    info.InstanceId.ToString(),
                    nativeElement)));
        }

        private static ControllerDisplayInfo CreateDisplayInfo(SdlRawGamepadInfo info)
        {
            string family = InferFamily(info);
            return new ControllerDisplayInfo(
                string.IsNullOrWhiteSpace(info.Name) ? $"SDL gamepad {info.InstanceId}" : info.Name,
                family,
                string.IsNullOrWhiteSpace(family) ? ControllerDisplayInfo.GenericGlyphFamily : family);
        }

        private static string InferFamily(SdlRawGamepadInfo info)
        {
            string text = $"{info.Name} {info.MappingName}".ToLowerInvariant();
            if (text.Contains("xbox")) return "xbox";
            if (text.Contains("playstation") || text.Contains("dualshock") || text.Contains("dualsense") || text.Contains("ps4") || text.Contains("ps5")) return "playstation";
            if (text.Contains("nintendo") || text.Contains("switch") || text.Contains("joy-con")) return "nintendo";
            if (text.Contains("steam")) return "steam";
            return "generic-sdl";
        }

        private static bool TryMapButton(
            SdlRawGamepadInfo info,
            string name,
            out UniversalInputId inputId)
        {
            if (HasTwoTouchpads(info) &&
                string.Equals(name, "Touchpad", StringComparison.OrdinalIgnoreCase))
            {
                inputId = UniversalInputId.LeftTouchSurfaceClick;
                return true;
            }

            return ButtonMap.TryGetValue(name ?? string.Empty, out inputId);
        }

        private static bool HasTwoTouchpads(SdlRawGamepadInfo info) =>
            info?.Touchpads?.Count == 2;

        private static UniversalInputId ApplyFaceButtonSwap(SdlRawGamepadInfo info, UniversalInputId inputId)
        {
            if (!UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled ||
                InferFamily(info) == "playstation")
            {
                return inputId;
            }

            return inputId switch
            {
                UniversalInputId.FaceButtonSouth => UniversalInputId.FaceButtonEast,
                UniversalInputId.FaceButtonEast => UniversalInputId.FaceButtonSouth,
                UniversalInputId.FaceButtonWest => UniversalInputId.FaceButtonNorth,
                UniversalInputId.FaceButtonNorth => UniversalInputId.FaceButtonWest,
                _ => inputId,
            };
        }

        private static bool HasAxis(SdlRawGamepadInfo info, string name)
        {
            return info.Axes.Any(item => item.Supported &&
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static SdlRawAxisState Axis(SdlRawGamepadInfo info, string name)
        {
            return info.Axes.FirstOrDefault(item => item.Supported &&
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasEnabledSensor(SdlRawGamepadInfo info, string name)
        {
            return info.Sensors.Any(item => item.Supported &&
                item.EnableAttempted &&
                item.EnableSucceeded &&
                item.Enabled &&
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static SdlRawSensorState Sensor(SdlRawGamepadInfo info, string name)
        {
            return info.Sensors.FirstOrDefault(item => item.Supported &&
                item.EnableAttempted &&
                item.EnableSucceeded &&
                item.Enabled &&
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddStickValue(
            SdlRawGamepadInfo info,
            ControllerCapabilities capabilities,
            Dictionary<UniversalInputId, UniversalInputValue> values,
            UniversalInputId inputId,
            string xName,
            string yName)
        {
            if (!capabilities.Supports(inputId)) return;

            SdlRawAxisState x = Axis(info, xName);
            SdlRawAxisState y = Axis(info, yName);
            if (x == null || y == null) return;

            values[inputId] = UniversalInputValue.Stick(
                UniversalValueNormalizer.NormalizeSignedAxis(x.RawValue),
                UniversalValueNormalizer.NormalizeSdlStickY(y.RawValue));
        }

        private static void AddTriggerValue(
            SdlRawGamepadInfo info,
            ControllerCapabilities capabilities,
            Dictionary<UniversalInputId, UniversalInputValue> values,
            UniversalInputId inputId,
            string axisName)
        {
            if (!capabilities.Supports(inputId)) return;
            SdlRawAxisState axis = Axis(info, axisName);
            if (axis != null)
            {
                values[inputId] = UniversalInputValue.AnalogAxis(
                    UniversalValueNormalizer.NormalizeSdlTrigger(axis.RawValue));
            }
        }

        private void AddTouchpadValues(
            SdlRawGamepadInfo info,
            ControllerCapabilities capabilities,
            Dictionary<UniversalInputId, UniversalInputValue> values)
        {
            foreach (KeyValuePair<int, SdlUniversalTouchSurfaceTarget> mapping in touchpadMappingPolicy.MapTouchpads(info))
            {
                UniversalInputId inputId = TouchSurfaceId(mapping.Value);
                if (!capabilities.Supports(inputId)) continue;

                SdlRawTouchpadState touchpad = info.Touchpads.FirstOrDefault(item => item.TouchpadIndex == mapping.Key);
                if (touchpad == null) continue;

                UniversalInputId clickInputId = TouchClickId(mapping.Value);
                bool clickPressed = capabilities.Supports(clickInputId) &&
                    values.TryGetValue(clickInputId, out UniversalInputValue clickValue) &&
                    clickValue.Pressed;

                values[inputId] = UniversalInputValue.TouchSurface(
                    touchpad.Fingers.Select(finger => new UniversalTouchContact(
                        finger.FingerIndex,
                        finger.Active,
                        finger.X,
                        finger.Y,
                        finger.Pressure)),
                    clickPressed);

                if (mapping.Value == SdlUniversalTouchSurfaceTarget.Primary)
                {
                    AddDerivedSinglePadHalf(values, capabilities, touchpad,
                        UniversalInputId.LeftTouchSurface,
                        UniversalInputId.LeftTouchSurfaceClick,
                        clickPressed,
                        left: true);
                    AddDerivedSinglePadHalf(values, capabilities, touchpad,
                        UniversalInputId.RightTouchSurface,
                        UniversalInputId.RightTouchSurfaceClick,
                        clickPressed,
                        left: false);
                }
            }
        }

        private static void AddDerivedDualPadButtonAliases(
            SdlRawGamepadInfo info,
            ControllerCapabilities capabilities,
            Dictionary<UniversalInputId, UniversalInputValue> values)
        {
            if (!HasTwoTouchpads(info)) return;

            if (capabilities.Supports(UniversalInputId.RightTouchSurfaceClick) &&
                values.TryGetValue(UniversalInputId.MiscButton2, out UniversalInputValue rightClick))
            {
                values[UniversalInputId.RightTouchSurfaceClick] = rightClick;
            }
        }

        private static void AddDerivedSinglePadHalf(
            Dictionary<UniversalInputId, UniversalInputValue> values,
            ControllerCapabilities capabilities,
            SdlRawTouchpadState touchpad,
            UniversalInputId surfaceInput,
            UniversalInputId clickInput,
            bool physicalClickPressed,
            bool left)
        {
            UniversalTouchContact[] contacts = touchpad.Fingers
                .Where(finger => finger.Active && (left ? finger.X < 0.5f : finger.X >= 0.5f))
                .Select(finger => new UniversalTouchContact(
                    finger.FingerIndex,
                    finger.Active,
                    finger.X,
                    finger.Y,
                    finger.Pressure))
                .ToArray();

            bool halfTouched = contacts.Any();
            if (capabilities.Supports(surfaceInput))
            {
                values[surfaceInput] = UniversalInputValue.TouchSurface(
                    contacts,
                    physicalClickPressed && halfTouched);
            }

            // Single physical touchpads have one click switch. Side click
            // bindings fire only when that switch is pressed while an active
            // touch is on the same half. Dual-pad controllers bypass this
            // derived path and report independent pad clicks directly.
            if (capabilities.Supports(clickInput))
            {
                values[clickInput] = UniversalInputValue.DigitalButton(
                    physicalClickPressed && halfTouched);
            }
        }

        private static void AddSensorValue(
            SdlRawGamepadInfo info,
            ControllerCapabilities capabilities,
            Dictionary<UniversalInputId, UniversalInputValue> values,
            UniversalInputId inputId,
            string sensorName)
        {
            if (!capabilities.Supports(inputId)) return;

            SdlRawSensorState sensor = Sensor(info, sensorName);
            if (sensor == null)
            {
                values[inputId] = UniversalInputValue.TemporarilyUnavailable(
                    UniversalInputCatalog.GetMetadata(inputId).ValueKind);
                return;
            }

            float[] sensorValues = sensor.Values ?? Array.Empty<float>();
            double x = sensorValues.Length > 0 ? sensorValues[0] : 0;
            double y = sensorValues.Length > 1 ? sensorValues[1] : 0;
            double z = sensorValues.Length > 2 ? sensorValues[2] : 0;
            values[inputId] = inputId == UniversalInputId.Gyroscope
                ? UniversalInputValue.Gyroscope(x, y, z)
                : UniversalInputValue.Accelerometer(x, y, z);
        }

        private static UniversalInputId TouchSurfaceId(SdlUniversalTouchSurfaceTarget target)
        {
            return target switch
            {
                SdlUniversalTouchSurfaceTarget.Left => UniversalInputId.LeftTouchSurface,
                SdlUniversalTouchSurfaceTarget.Right => UniversalInputId.RightTouchSurface,
                _ => UniversalInputId.PrimaryTouchSurface,
            };
        }

        private static UniversalInputId TouchClickId(SdlUniversalTouchSurfaceTarget target)
        {
            return target switch
            {
                SdlUniversalTouchSurfaceTarget.Left => UniversalInputId.LeftTouchSurfaceClick,
                SdlUniversalTouchSurfaceTarget.Right => UniversalInputId.RightTouchSurfaceClick,
                _ => UniversalInputId.PrimaryTouchSurfaceClick,
            };
        }

        private static string TouchLabel(SdlUniversalTouchSurfaceTarget target)
        {
            return target switch
            {
                SdlUniversalTouchSurfaceTarget.Left => "Left Touchpad",
                SdlUniversalTouchSurfaceTarget.Right => "Right Touchpad",
                _ => "Touchpad",
            };
        }
    }

    internal sealed class SdlUniversalControllerBackend : IUniversalControllerBackend
    {
        private sealed class TrackedDevice
        {
            public SdlGamepadHandle Handle { get; set; }
            public UniversalController Controller { get; set; }
            public SdlRawGamepadInfo Info { get; set; }
            public long Sequence { get; set; }
            public int MissedEnumerations { get; set; }
        }

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ISdlDiagnosticApi api;
        private readonly SdlUniversalStateTranslator translator;
        private readonly Func<DateTimeOffset> utcNow;
        private readonly object syncRoot = new object();
        private readonly Dictionary<uint, TrackedDevice> devices = new Dictionary<uint, TrackedDevice>();
        private readonly HashSet<uint> suppressedInstanceIds = new HashSet<uint>();
        private static readonly TimeSpan EnumerationReconcileInterval = TimeSpan.FromMilliseconds(100);
        private const int MissedEnumerationsBeforeClose = 3;
        private DateTimeOffset nextEnumerationReconcileUtc = DateTimeOffset.MinValue;
        private bool started;
        private bool disposed;

        public string BackendName => UniversalControllerBackendIds.Sdl3;
        public IReadOnlyList<IUniversalController> Controllers
        {
            get
            {
                lock (syncRoot)
                {
                    return new ReadOnlyCollection<IUniversalController>(
                        devices.Values.Select(item => item.Controller).Where(item => item.ConnectionState == UniversalControllerConnectionState.Connected).Cast<IUniversalController>().ToArray());
                }
            }
        }

        public event EventHandler ControllersChanged;

        public SdlUniversalControllerBackend(
            ISdlDiagnosticApi api,
            SdlUniversalStateTranslator translator = null,
            Func<DateTimeOffset> utcNow = null)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.translator = translator ?? new SdlUniversalStateTranslator();
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public bool Start(out string error)
        {
            ThrowIfDisposed();
            lock (syncRoot)
            {
                if (started)
                {
                    error = string.Empty;
                    return true;
                }

                if (!api.Initialise(out error))
                {
                    return false;
                }

                started = true;
            }

            string enumError = ReconcileEnumeratedDevices("initial enumeration");
            nextEnumerationReconcileUtc = utcNow().Add(EnumerationReconcileInterval);

            error = enumError ?? string.Empty;
            ControllersChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            lock (syncRoot)
            {
                if (!started) return;

                api.RefreshGamepads();
                api.RefreshSensors();
                while (api.PollEvent(out SdlDiagnosticEvent diagnosticEvent))
                {
                    HandleEvent(diagnosticEvent);
                }

                DateTimeOffset now = utcNow();
                if (now >= nextEnumerationReconcileUtc)
                {
                    ReconcileEnumeratedDevices("enumeration reconcile");
                    nextEnumerationReconcileUtc = now.Add(EnumerationReconcileInterval);
                }

                foreach (TrackedDevice tracked in devices.Values.ToList())
                {
                    if (tracked.Controller.ConnectionState != UniversalControllerConnectionState.Connected)
                    {
                        continue;
                    }

                    try
                    {
                        api.RefreshLiveState(tracked.Handle, tracked.Info);
                        tracked.Controller.PublishBatteryPercent(tracked.Info.BatteryPercent);
                        tracked.Sequence++;
                        tracked.Controller.PublishState(translator.CreateState(
                            tracked.Info,
                            tracked.Controller.Capabilities,
                            true,
                            tracked.Sequence,
                            DateTimeOffset.UtcNow));
                    }
                    catch (Exception ex)
                    {
                        uint instanceId = tracked.Info.InstanceId;
                        CloseDevice(instanceId, "refresh failed");
                        logger.Warn($"SDL universal backend failed to refresh instance {instanceId}: {ex.Message}");
                    }
                }
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!started) return;
                foreach (uint instanceId in devices.Keys.ToArray())
                {
                    CloseDevice(instanceId, "backend stopped");
                }

                devices.Clear();
                suppressedInstanceIds.Clear();
                started = false;
                api.Shutdown();
            }

            ControllersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void HandleEvent(SdlDiagnosticEvent diagnosticEvent)
        {
            switch (diagnosticEvent.Kind)
            {
                case SdlDiagnosticInputEventKind.DeviceAdded:
                    suppressedInstanceIds.Remove(diagnosticEvent.InstanceId);
                    OpenDevice(diagnosticEvent.InstanceId, "device added");
                    break;
                case SdlDiagnosticInputEventKind.DeviceRemoved:
                    suppressedInstanceIds.Remove(diagnosticEvent.InstanceId);
                    CloseDevice(diagnosticEvent.InstanceId, "device removed");
                    break;
                case SdlDiagnosticInputEventKind.DeviceRemapped:
                    suppressedInstanceIds.Remove(diagnosticEvent.InstanceId);
                    RebuildDevice(diagnosticEvent.InstanceId);
                    break;
            }
        }

        private string ReconcileEnumeratedDevices(string reason)
        {
            IReadOnlyList<uint> instanceIds = api.EnumerateGamepads(out string enumError);
            foreach (uint instanceId in instanceIds)
            {
                if (!devices.ContainsKey(instanceId) &&
                    !suppressedInstanceIds.Contains(instanceId))
                {
                    OpenDevice(instanceId, reason);
                }
            }

            // Removal used to depend entirely on a GamepadRemoved event
            // arriving on this backend's poll. SDL's event queue is
            // process-wide, so any other consumer draining it (the diagnostics
            // window does) takes the event away, and a device that vanished
            // between polls stayed open and "connected" forever: its session
            // was never disposed and it kept its slot in the controller list.
            // The enumeration is authoritative, so trust it in both directions.
            //
            // Only trust an empty enumeration when it came back clean: an
            // enumeration that failed reports no devices, and dropping every
            // controller on a transient failure would be worse than waiting.
            if (instanceIds.Count == 0 && !string.IsNullOrWhiteSpace(enumError))
            {
                logger.Warn($"SDL universal backend enumeration reported: {enumError}");
                return enumError;
            }

            HashSet<uint> enumerated = new HashSet<uint>(instanceIds);
            foreach (TrackedDevice tracked in devices.Values.ToArray())
            {
                if (enumerated.Contains(tracked.Info.InstanceId))
                {
                    tracked.MissedEnumerations = 0;
                    continue;
                }

                // SDL can briefly stop reporting a device it is re-classifying
                // (a remap, for instance). Require a few consecutive misses so
                // a blip does not churn the session for a controller that is
                // still plugged in.
                tracked.MissedEnumerations++;
                if (tracked.MissedEnumerations >= MissedEnumerationsBeforeClose)
                {
                    CloseDevice(tracked.Info.InstanceId, "no longer enumerated");
                }
            }

            suppressedInstanceIds.RemoveWhere(item => !enumerated.Contains(item));

            if (!string.IsNullOrWhiteSpace(enumError))
            {
                logger.Warn($"SDL universal backend enumeration reported: {enumError}");
            }

            return enumError;
        }

        private void OpenDevice(uint instanceId, string reason)
        {
            if (devices.TryGetValue(instanceId, out TrackedDevice existing) &&
                existing.Controller.ConnectionState == UniversalControllerConnectionState.Connected)
            {
                return;
            }

            SdlGamepadHandle handle = api.OpenGamepad(instanceId, out string error);
            if (handle.IsNull)
            {
                logger.Warn($"SDL universal backend failed to open instance {instanceId}: {error}");
                return;
            }

            SdlRawGamepadInfo info;
            try
            {
                info = api.QueryGamepadInfo(instanceId, handle);
            }
            catch (Exception ex)
            {
                try
                {
                    api.CloseGamepad(handle);
                }
                catch (Exception closeEx)
                {
                    logger.Warn($"SDL universal backend failed to close uninspected instance {instanceId}: {closeEx.Message}");
                }

                logger.Warn($"SDL universal backend failed to inspect instance {instanceId}: {ex.Message}");
                return;
            }

            if (translator.ShouldSuppressForNativeSteamController(info))
            {
                try
                {
                    api.CloseGamepad(handle);
                }
                catch (Exception ex)
                {
                    logger.Warn($"SDL universal backend failed to close suppressed instance {instanceId}: {ex.Message}");
                }

                logger.Info($"SDL universal backend suppressed non-authoritative instance {instanceId}: {info.Name}");
                suppressedInstanceIds.Add(instanceId);
                return;
            }

            ControllerCapabilities capabilities = translator.CreateCapabilities(info);
            UniversalDeviceIdentity deviceIdentity = translator.CreateDeviceIdentity(info);
            UniversalController controller = new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    BackendName,
                    instanceId.ToString(),
                    deviceIdentity,
                    DateTimeOffset.UtcNow),
                capabilities,
                translator.CreateState(info, capabilities, true, 1, DateTimeOffset.UtcNow));
            controller.PublishBatteryPercent(info.BatteryPercent);

            devices[instanceId] = new TrackedDevice
            {
                Handle = handle,
                Info = info,
                Controller = controller,
                Sequence = 1,
            };

            logger.Info($"SDL universal backend opened instance {instanceId} ({reason}): {info.Name}");
            ControllersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RebuildDevice(uint instanceId)
        {
            if (!devices.TryGetValue(instanceId, out TrackedDevice tracked))
            {
                return;
            }

            SdlRawGamepadInfo info;
            try
            {
                info = api.QueryGamepadInfo(instanceId, tracked.Handle);
            }
            catch (Exception ex)
            {
                CloseDevice(instanceId, "remap failed");
                logger.Warn($"SDL universal backend failed to remap instance {instanceId}: {ex.Message}");
                return;
            }

            tracked.Info = info;
            ControllerCapabilities capabilities = translator.CreateCapabilities(tracked.Info);
            tracked.Controller.PublishCapabilities(capabilities);
            tracked.Controller.PublishBatteryPercent(tracked.Info.BatteryPercent);
            tracked.Sequence++;
            tracked.Controller.PublishState(translator.CreateState(
                tracked.Info,
                capabilities,
                true,
                tracked.Sequence,
                DateTimeOffset.UtcNow));
        }

        private void CloseDevice(uint instanceId, string reason)
        {
            if (!devices.TryGetValue(instanceId, out TrackedDevice tracked))
            {
                return;
            }

            try
            {
                api.CloseGamepad(tracked.Handle);
            }
            catch (Exception ex)
            {
                logger.Warn($"SDL universal backend failed to close instance {instanceId}: {ex.Message}");
            }

            tracked.Controller.MarkDisconnected();
            devices.Remove(instanceId);
            suppressedInstanceIds.Remove(instanceId);
            logger.Info($"SDL universal backend closed instance {instanceId} ({reason})");
            ControllersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SdlUniversalControllerBackend));
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
        }
    }
}
