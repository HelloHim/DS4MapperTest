using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace DS4MapperTest.SdlDiagnostics
{
    internal sealed class SdlDiagnosticService : IDisposable
    {
        private sealed class TrackedDevice
        {
            public SdlGamepadHandle Handle { get; set; }
            public SdlDiagnosticDeviceSnapshot Snapshot { get; set; }
        }

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ISdlDiagnosticApi api;
        private readonly object syncRoot = new object();
        private readonly Dictionary<uint, TrackedDevice> devices = new Dictionary<uint, TrackedDevice>();
        private readonly List<string> eventLog = new List<string>();
        private readonly List<string> errors = new List<string>();
        private bool started;
        private bool disposed;

        public bool Started => started;

        public SdlDiagnosticService(ISdlDiagnosticApi api)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
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
                    RecordError($"SDL initialisation failed: {error}");
                    return false;
                }

                started = true;
                AddEvent("SDL diagnostics started");
            }

            EnumerateInitialDevices();
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

                DateTimeOffset now = DateTimeOffset.UtcNow;
                foreach (TrackedDevice tracked in devices.Values.Where(item => item.Snapshot.Connected).ToList())
                {
                    try
                    {
                        api.RefreshLiveState(tracked.Handle, tracked.Snapshot.Info);
                        tracked.Snapshot.LastSeenUtc = now;
                    }
                    catch (Exception ex)
                    {
                        string message = $"Failed to refresh SDL instance {tracked.Snapshot.InstanceId}: {ex.Message}";
                        tracked.Snapshot.Info.Errors.Add(message);
                        RecordError(message);
                    }
                }
            }
        }

        public SdlDiagnosticSessionSnapshot CreateSnapshot()
        {
            lock (syncRoot)
            {
                return new SdlDiagnosticSessionSnapshot
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Version = api.VersionInfo,
                    Devices = devices.Values.OrderBy(item => item.Snapshot.InstanceId).Select(item => item.Snapshot.Clone()).ToList(),
                    Events = eventLog.ToList(),
                    Errors = errors.ToList(),
                };
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!started) return;

                foreach (TrackedDevice tracked in devices.Values.ToList())
                {
                    CloseDevice(tracked, "diagnostics stopped");
                }

                started = false;
                api.Shutdown();
                AddEvent("SDL diagnostics stopped");
            }
        }

        private void EnumerateInitialDevices()
        {
            IReadOnlyList<uint> instanceIds = api.EnumerateGamepads(out string error);
            if (!string.IsNullOrWhiteSpace(error))
            {
                RecordError($"SDL enumeration reported: {error}");
            }

            foreach (uint instanceId in instanceIds)
            {
                OpenDevice(instanceId, "initial enumeration");
            }
        }

        private void HandleEvent(SdlDiagnosticEvent diagnosticEvent)
        {
            switch (diagnosticEvent.Kind)
            {
                case SdlDiagnosticInputEventKind.DeviceAdded:
                    OpenDevice(diagnosticEvent.InstanceId, "device added");
                    break;
                case SdlDiagnosticInputEventKind.DeviceRemoved:
                    if (devices.TryGetValue(diagnosticEvent.InstanceId, out TrackedDevice tracked))
                    {
                        CloseDevice(tracked, "device removed");
                    }
                    break;
                case SdlDiagnosticInputEventKind.DeviceRemapped:
                    if (devices.TryGetValue(diagnosticEvent.InstanceId, out tracked) && tracked.Snapshot.Connected)
                    {
                        tracked.Snapshot.Info = api.QueryGamepadInfo(diagnosticEvent.InstanceId, tracked.Handle);
                        AddEvent($"SDL device remapped: {tracked.Snapshot.DisplayName}");
                    }
                    break;
                default:
                    ApplyInputEvent(diagnosticEvent);
                    break;
            }
        }

        private void ApplyInputEvent(SdlDiagnosticEvent diagnosticEvent)
        {
            if (!devices.TryGetValue(diagnosticEvent.InstanceId, out TrackedDevice tracked) || !tracked.Snapshot.Connected)
            {
                return;
            }

            SdlRawGamepadInfo info = tracked.Snapshot.Info;
            DateTimeOffset timestamp = diagnosticEvent.TimestampUtc;
            switch (diagnosticEvent.Kind)
            {
                case SdlDiagnosticInputEventKind.ButtonChanged:
                    SdlRawButtonState button = info.Buttons.FirstOrDefault(item => item.Index == diagnosticEvent.ControlIndex);
                    if (button != null)
                    {
                        button.Pressed = diagnosticEvent.ButtonPressed;
                        button.LastChangedUtc = timestamp;
                    }
                    AddEvent($"{tracked.Snapshot.DisplayName}: button {diagnosticEvent.ControlName} {(diagnosticEvent.ButtonPressed ? "pressed" : "released")}");
                    break;
                case SdlDiagnosticInputEventKind.AxisChanged:
                    SdlRawAxisState axis = info.Axes.FirstOrDefault(item => item.Index == diagnosticEvent.ControlIndex);
                    if (axis != null)
                    {
                        axis.RawValue = diagnosticEvent.AxisValue;
                        axis.NormalizedValue = SdlDiagnosticValueNormalizer.NormalizeAxis(diagnosticEvent.AxisValue);
                        axis.LastChangedUtc = timestamp;
                    }
                    break;
                case SdlDiagnosticInputEventKind.TouchpadChanged:
                    SdlRawTouchFingerState finger = info.Touchpads.FirstOrDefault(item => item.TouchpadIndex == diagnosticEvent.TouchpadIndex)?.Fingers.FirstOrDefault(item => item.FingerIndex == diagnosticEvent.FingerIndex);
                    if (finger != null)
                    {
                        finger.Active = diagnosticEvent.TouchActive;
                        finger.X = diagnosticEvent.X;
                        finger.Y = diagnosticEvent.Y;
                        finger.Pressure = diagnosticEvent.Pressure;
                        finger.LastChangedUtc = timestamp;
                    }
                    break;
                case SdlDiagnosticInputEventKind.SensorChanged:
                    SdlRawSensorState sensor = info.Sensors.FirstOrDefault(item => string.Equals(item.Name, diagnosticEvent.SensorName, StringComparison.OrdinalIgnoreCase));
                    if (sensor != null)
                    {
                        sensor.Values = diagnosticEvent.SensorValues?.ToArray() ?? Array.Empty<float>();
                        sensor.LastChangedUtc = timestamp;
                    }
                    break;
            }
        }

        private void OpenDevice(uint instanceId, string reason)
        {
            if (devices.TryGetValue(instanceId, out TrackedDevice existing) && existing.Snapshot.Connected)
            {
                AddEvent($"SDL duplicate add ignored for instance {instanceId}");
                return;
            }

            SdlGamepadHandle handle = api.OpenGamepad(instanceId, out string error);
            if (handle.IsNull)
            {
                RecordError($"Failed to open SDL instance {instanceId}: {error}");
                return;
            }

            SdlRawGamepadInfo info;
            try
            {
                info = api.QueryGamepadInfo(instanceId, handle);
            }
            catch (Exception ex)
            {
                api.CloseGamepad(handle);
                RecordError($"Failed to inspect SDL instance {instanceId}: {ex.Message}");
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            devices[instanceId] = new TrackedDevice
            {
                Handle = handle,
                Snapshot = new SdlDiagnosticDeviceSnapshot
                {
                    InstanceId = instanceId,
                    Connected = true,
                    FirstSeenUtc = existing?.Snapshot.FirstSeenUtc ?? now,
                    LastSeenUtc = now,
                    Info = info,
                },
            };
            AddEvent($"SDL device opened ({reason}): {devices[instanceId].Snapshot.DisplayName}");
            logger.Info($"SDL diagnostics opened instance {instanceId}: {info.Name}");
        }

        private void CloseDevice(TrackedDevice tracked, string reason)
        {
            try
            {
                api.CloseGamepad(tracked.Handle);
            }
            catch (Exception ex)
            {
                RecordError($"Failed to close SDL instance {tracked.Snapshot.InstanceId}: {ex.Message}");
            }

            tracked.Handle = new SdlGamepadHandle(IntPtr.Zero);
            tracked.Snapshot.Connected = false;
            tracked.Snapshot.LastSeenUtc = DateTimeOffset.UtcNow;
            ClearLiveState(tracked.Snapshot.Info);
            AddEvent($"SDL device closed ({reason}): {tracked.Snapshot.DisplayName}");
        }

        private static void ClearLiveState(SdlRawGamepadInfo info)
        {
            foreach (SdlRawButtonState button in info.Buttons) button.Pressed = false;
            foreach (SdlRawAxisState axis in info.Axes)
            {
                axis.RawValue = 0;
                axis.NormalizedValue = 0;
            }

            foreach (SdlRawTouchFingerState finger in info.Touchpads.SelectMany(item => item.Fingers))
            {
                finger.Active = false;
                finger.X = 0;
                finger.Y = 0;
                finger.Pressure = 0;
            }

            foreach (SdlRawSensorState sensor in info.Sensors)
            {
                sensor.Values = new float[3];
                sensor.Enabled = false;
            }
        }

        private void RecordError(string message)
        {
            errors.Add($"{DateTimeOffset.UtcNow:O} {message}");
            logger.Warn(message);
        }

        private void AddEvent(string message)
        {
            eventLog.Add($"{DateTimeOffset.UtcNow:O} {message}");
            if (eventLog.Count > 400) eventLog.RemoveAt(0);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SdlDiagnosticService));
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
        }
    }

    internal static class SdlDiagnosticValueNormalizer
    {
        public static double NormalizeAxis(short value) =>
            value < 0 ? value / 32768.0 : value / 32767.0;
    }
}
