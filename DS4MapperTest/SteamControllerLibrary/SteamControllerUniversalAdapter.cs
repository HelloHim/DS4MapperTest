using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DS4MapperTest.Universal;

namespace DS4MapperTest.SteamControllerLibrary
{
    internal interface ISteamControllerNativeStateSource : IDisposable
    {
        string SessionId { get; }
        string DisplayName { get; }
        string DevicePath { get; }
        string SerialNumber { get; }
        ushort? VendorId { get; }
        ushort? ProductId { get; }
        bool IsConnected { get; }
        bool OwnsReader { get; }
        int? BatteryPercent { get; }
        SteamControllerState ReadState();
    }

    internal sealed class SteamControllerDeviceStateSource : ISteamControllerNativeStateSource
    {
        private readonly SteamControllerDevice device;

        public string SessionId => string.IsNullOrWhiteSpace(DevicePath)
            ? RuntimeHelpers.GetHashCode(device).ToString()
            : DevicePath;
        public string DisplayName => string.IsNullOrWhiteSpace(device.DevTypeStr)
            ? "Steam Controller"
            : device.DevTypeStr;
        public string DevicePath => device.HidDevice?.DevicePath ?? string.Empty;
        public string SerialNumber => device.Serial ?? string.Empty;
        public ushort? VendorId => device.HidDevice == null ? null : (ushort?)device.HidDevice.Attributes.VendorId;
        public ushort? ProductId => device.HidDevice == null ? null : (ushort?)device.HidDevice.Attributes.ProductId;
        public bool IsConnected => device.Synced;
        public bool OwnsReader => false;
        public int? BatteryPercent => device.Battery <= 100 ? (int?)device.Battery : null;

        public SteamControllerDeviceStateSource(SteamControllerDevice device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public SteamControllerState ReadState()
        {
            return device.CurrentState;
        }

        public void Dispose()
        {
            // The existing mapper owns the native reader and device lifetime.
        }
    }

    internal sealed class SteamControllerReaderStateSource : ISteamControllerNativeStateSource
    {
        private readonly SteamControllerDevice device;
        private readonly DeviceReaderBase reader;

        public string SessionId => string.IsNullOrWhiteSpace(DevicePath)
            ? RuntimeHelpers.GetHashCode(device).ToString()
            : DevicePath;
        public string DisplayName => string.IsNullOrWhiteSpace(device.DevTypeStr)
            ? "Steam Controller"
            : device.DevTypeStr;
        public string DevicePath => device.HidDevice?.DevicePath ?? string.Empty;
        public string SerialNumber => device.Serial ?? string.Empty;
        public ushort? VendorId => device.HidDevice == null ? null : (ushort?)device.HidDevice.Attributes.VendorId;
        public ushort? ProductId => device.HidDevice == null ? null : (ushort?)device.HidDevice.Attributes.ProductId;
        public bool IsConnected => device.Synced;
        public bool OwnsReader => true;
        public int? BatteryPercent => device.Battery <= 100 ? (int?)device.Battery : null;

        public SteamControllerReaderStateSource(SteamControllerDevice device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            reader = device.ConType == SteamControllerDevice.ConnectionType.Bluetooth &&
                device is SteamControllerBTDevice bluetoothDevice
                    ? new SteamControllerBTReader(bluetoothDevice)
                    : new SteamControllerReader(device);
            reader.StartUpdate();
        }

        public SteamControllerState ReadState()
        {
            return device.CurrentState;
        }

        public void Dispose()
        {
            reader.StopUpdate();
        }
    }

    internal sealed class SteamControllerUniversalController : IUniversalController
    {
        private readonly ISteamControllerNativeStateSource source;
        private readonly bool ownsSource;
        private long sequence;
        private UniversalControllerStateSnapshot state;

        public UniversalControllerIdentity Identity { get; }
        public UniversalControllerConnectionState ConnectionState { get; private set; }
        public ControllerCapabilities Capabilities { get; }
        public ControllerDisplayInfo DisplayInfo => Capabilities.DisplayInfo;
        public UniversalControllerStateSnapshot State => state;
        public int? BatteryPercent => source.BatteryPercent;

        public SteamControllerUniversalController(
            ISteamControllerNativeStateSource source,
            bool ownsSource = false)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.ownsSource = ownsSource;
            Capabilities = BuildCapabilities(source);
            Identity = new UniversalControllerIdentity(
                Guid.NewGuid(),
                UniversalControllerBackendIds.SteamControllerNative,
                source.SessionId,
                BuildDeviceIdentity(source),
                DateTimeOffset.UtcNow);
            state = UniversalControllerStateSnapshot.Disconnected();
            Refresh();
        }

        public void Refresh()
        {
            if (!source.IsConnected)
            {
                sequence++;
                state = UniversalControllerStateSnapshot.Disconnected(sequence);
                ConnectionState = UniversalControllerConnectionState.Disconnected;
                return;
            }

            sequence++;
            state = CreateState(source.ReadState(), sequence);
            ConnectionState = UniversalControllerConnectionState.Connected;
        }

        private UniversalControllerStateSnapshot CreateState(SteamControllerState input, long sequence)
        {
            Dictionary<UniversalInputId, UniversalInputValue> values =
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [UniversalInputId.FaceButtonSouth] = UniversalInputValue.DigitalButton(input.A),
                    [UniversalInputId.FaceButtonEast] = UniversalInputValue.DigitalButton(input.B),
                    [UniversalInputId.FaceButtonWest] = UniversalInputValue.DigitalButton(input.X),
                    [UniversalInputId.FaceButtonNorth] = UniversalInputValue.DigitalButton(input.Y),
                    [UniversalInputId.LeftShoulder] = UniversalInputValue.DigitalButton(input.LB),
                    [UniversalInputId.RightShoulder] = UniversalInputValue.DigitalButton(input.RB),
                    [UniversalInputId.View] = UniversalInputValue.DigitalButton(input.Back),
                    [UniversalInputId.Menu] = UniversalInputValue.DigitalButton(input.Start),
                    [UniversalInputId.System] = UniversalInputValue.DigitalButton(input.Guide),
                    [UniversalInputId.LeftTrigger] = UniversalInputValue.AnalogAxis(UniversalValueNormalizer.NormalizeSteamControllerTrigger(input.LT)),
                    [UniversalInputId.RightTrigger] = UniversalInputValue.AnalogAxis(UniversalValueNormalizer.NormalizeSteamControllerTrigger(input.RT)),
                    [UniversalInputId.LeftTriggerFullPull] = UniversalInputValue.DigitalButton(input.LTClick),
                    [UniversalInputId.RightTriggerFullPull] = UniversalInputValue.DigitalButton(input.RTClick),
                    [UniversalInputId.LeftRearPrimary] = UniversalInputValue.DigitalButton(input.LGrip),
                    [UniversalInputId.RightRearPrimary] = UniversalInputValue.DigitalButton(input.RGrip),
                    [UniversalInputId.LeftStick] = UniversalInputValue.Stick(
                        UniversalValueNormalizer.NormalizeSignedAxis(input.LX),
                        UniversalValueNormalizer.NormalizeSignedAxis(input.LY)),
                    [UniversalInputId.LeftStickClick] = UniversalInputValue.DigitalButton(input.LSClick),
                    [UniversalInputId.DPadUp] = UniversalInputValue.DigitalButton(input.DPadUp),
                    [UniversalInputId.DPadDown] = UniversalInputValue.DigitalButton(input.DPadDown),
                    [UniversalInputId.DPadLeft] = UniversalInputValue.DigitalButton(input.DPadLeft),
                    [UniversalInputId.DPadRight] = UniversalInputValue.DigitalButton(input.DPadRight),
                    [UniversalInputId.LeftTouchSurfaceClick] = UniversalInputValue.DigitalButton(input.LeftPad.Click),
                    [UniversalInputId.RightTouchSurfaceClick] = UniversalInputValue.DigitalButton(input.RightPad.Click),
                    [UniversalInputId.LeftTouchSurface] = CreatePadValue(input.LeftPad),
                    [UniversalInputId.RightTouchSurface] = CreatePadValue(input.RightPad),
                    [UniversalInputId.LeftTouchContact] = UniversalInputValue.DigitalButton(input.LeftPad.Touch),
                    [UniversalInputId.RightTouchContact] = UniversalInputValue.DigitalButton(input.RightPad.Touch),
                    [UniversalInputId.Gyroscope] = UniversalInputValue.Gyroscope(
                        UniversalValueNormalizer.DegreesPerSecondToRadiansPerSecond(input.Motion.AngGyroPitch),
                        UniversalValueNormalizer.DegreesPerSecondToRadiansPerSecond(input.Motion.AngGyroYaw),
                        UniversalValueNormalizer.DegreesPerSecondToRadiansPerSecond(input.Motion.AngGyroRoll)),
                    [UniversalInputId.Accelerometer] = UniversalInputValue.Accelerometer(
                        UniversalValueNormalizer.GToMetresPerSecondSquared(input.Motion.AccelXG),
                        UniversalValueNormalizer.GToMetresPerSecondSquared(input.Motion.AccelYG),
                        UniversalValueNormalizer.GToMetresPerSecondSquared(input.Motion.AccelZG)),
                };

            return new UniversalControllerStateSnapshot(
                UniversalMonotonicClock.UtcNow,
                sequence,
                true,
                values);
        }

        private static UniversalInputValue CreatePadValue(SteamControllerState.TouchPadInfo pad)
        {
            return UniversalInputValue.TouchSurface(
                new[]
                {
                    new UniversalTouchContact(
                        0,
                        pad.Touch,
                        UniversalValueNormalizer.NormalizeSignedTouchAxis(pad.X),
                        UniversalValueNormalizer.NormalizeSignedTouchAxis(pad.Y),
                        null),
                },
                pad.Click);
        }

        private static ControllerCapabilities BuildCapabilities(ISteamControllerNativeStateSource source)
        {
            UniversalInputId[] supported =
            {
                UniversalInputId.FaceButtonSouth,
                UniversalInputId.FaceButtonEast,
                UniversalInputId.FaceButtonWest,
                UniversalInputId.FaceButtonNorth,
                UniversalInputId.DPadUp,
                UniversalInputId.DPadDown,
                UniversalInputId.DPadLeft,
                UniversalInputId.DPadRight,
                UniversalInputId.LeftShoulder,
                UniversalInputId.RightShoulder,
                UniversalInputId.LeftTrigger,
                UniversalInputId.RightTrigger,
                UniversalInputId.LeftTriggerFullPull,
                UniversalInputId.RightTriggerFullPull,
                UniversalInputId.LeftStick,
                UniversalInputId.LeftStickClick,
                UniversalInputId.Menu,
                UniversalInputId.View,
                UniversalInputId.System,
                UniversalInputId.LeftRearPrimary,
                UniversalInputId.RightRearPrimary,
                UniversalInputId.LeftTouchSurface,
                UniversalInputId.RightTouchSurface,
                UniversalInputId.LeftTouchSurfaceClick,
                UniversalInputId.RightTouchSurfaceClick,
                UniversalInputId.LeftTouchContact,
                UniversalInputId.RightTouchContact,
                UniversalInputId.Gyroscope,
                UniversalInputId.Accelerometer,
            };

            return new ControllerCapabilities(
                new ControllerDisplayInfo(source.DisplayName, "steam-controller-2015", "steam-controller"),
                supported.Select(inputId => new ControllerInputDescriptor(
                    inputId,
                    UniversalInputCatalog.GetMetadata(inputId).ValueKind,
                    true,
                    NativeLabel(inputId),
                    string.Empty,
                    new ControllerInputSource(
                        UniversalControllerBackendIds.SteamControllerNative,
                        source.SessionId,
                        inputId.ToString()))));
        }

        private static string NativeLabel(UniversalInputId inputId)
        {
            return inputId switch
            {
                UniversalInputId.LeftRearPrimary => "Left Grip",
                UniversalInputId.RightRearPrimary => "Right Grip",
                UniversalInputId.LeftTouchSurface => "Left Touchpad",
                UniversalInputId.RightTouchSurface => "Right Touchpad",
                UniversalInputId.LeftTouchSurfaceClick => "Left Pad Click",
                UniversalInputId.RightTouchSurfaceClick => "Right Pad Click",
                UniversalInputId.System => "Steam",
                _ => UniversalInputCatalog.GetMetadata(inputId).DisplayName,
            };
        }

        private static UniversalDeviceIdentity BuildDeviceIdentity(ISteamControllerNativeStateSource source)
        {
            string persistentKey = !string.IsNullOrWhiteSpace(source.DevicePath)
                ? $"path:{source.DevicePath}"
                : string.Join("|",
                    source.VendorId.HasValue ? $"vid-{source.VendorId.Value:X4}" : "vid-unknown",
                    source.ProductId.HasValue ? $"pid-{source.ProductId.Value:X4}" : "pid-unknown",
                    string.IsNullOrWhiteSpace(source.SerialNumber) ? "serial-unknown" : $"serial-{source.SerialNumber}");

            return new UniversalDeviceIdentity(
                UniversalControllerBackendIds.SteamControllerNative,
                source.SessionId,
                persistentKey,
                source.VendorId,
                source.ProductId,
                source.SerialNumber,
                source.DevicePath,
                string.Empty,
                true,
                "Native Steam Controller identity is best-effort and does not move calibration into universal state.");
        }

        public void Dispose()
        {
            if (ownsSource || source.OwnsReader)
            {
                source.Dispose();
            }
        }
    }

    internal sealed class SteamControllerUniversalBackend : IUniversalControllerBackend
    {
        private readonly Dictionary<string, SteamControllerUniversalController> controllers =
            new Dictionary<string, SteamControllerUniversalController>(StringComparer.OrdinalIgnoreCase);

        public string BackendName => UniversalControllerBackendIds.SteamControllerNative;
        public IReadOnlyList<IUniversalController> Controllers =>
            new ReadOnlyCollection<IUniversalController>(controllers.Values.Cast<IUniversalController>().ToArray());
        public event EventHandler ControllersChanged;

        public SteamControllerUniversalBackend(IEnumerable<ISteamControllerNativeStateSource> sources)
        {
            AddOrReplaceSources(sources);
        }

        public bool Start(out string error)
        {
            error = string.Empty;
            Refresh();
            return true;
        }

        public void RefreshSources(IEnumerable<ISteamControllerNativeStateSource> sources)
        {
            if (AddOrReplaceSources(sources))
            {
                ControllersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Refresh()
        {
            foreach (SteamControllerUniversalController controller in controllers.Values)
            {
                controller.Refresh();
            }

            ControllersChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            foreach (SteamControllerUniversalController controller in controllers.Values)
            {
                controller.Refresh();
            }
        }

        public void Dispose()
        {
            foreach (SteamControllerUniversalController controller in controllers.Values)
            {
                controller.Dispose();
            }

            controllers.Clear();
        }

        private bool AddOrReplaceSources(IEnumerable<ISteamControllerNativeStateSource> sources)
        {
            bool changed = false;
            foreach (ISteamControllerNativeStateSource source in sources ?? Enumerable.Empty<ISteamControllerNativeStateSource>())
            {
                if (source == null) continue;

                string sessionId = source.SessionId ?? string.Empty;
                if (controllers.TryGetValue(sessionId, out SteamControllerUniversalController existing))
                {
                    existing.Refresh();
                    if (existing.ConnectionState == UniversalControllerConnectionState.Connected)
                    {
                        source.Dispose();
                        continue;
                    }

                    existing.Dispose();
                    controllers.Remove(sessionId);
                }

                controllers[sessionId] = new SteamControllerUniversalController(source, ownsSource: true);
                changed = true;
            }

            return changed;
        }
    }
}
