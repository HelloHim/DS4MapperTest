using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest.SdlDiagnostics
{
    internal enum SdlDiagnosticInputEventKind
    {
        DeviceAdded,
        DeviceRemoved,
        DeviceRemapped,
        ButtonChanged,
        AxisChanged,
        TouchpadChanged,
        SensorChanged,
    }

    internal sealed class SdlDiagnosticVersionInfo
    {
        public string BindingName { get; set; } = "SDL3-CS";
        public string BindingVersion { get; set; } = "3.4.14.1";
        public string NativeVersion { get; set; } = string.Empty;
        public string NativeRevision { get; set; } = string.Empty;
    }

    internal sealed class SdlDiagnosticEvent
    {
        public SdlDiagnosticInputEventKind Kind { get; set; }
        public uint InstanceId { get; set; }
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
        public int ControlIndex { get; set; } = -1;
        public string ControlName { get; set; } = string.Empty;
        public bool ButtonPressed { get; set; }
        public short AxisValue { get; set; }
        public int TouchpadIndex { get; set; } = -1;
        public int FingerIndex { get; set; } = -1;
        public bool TouchActive { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Pressure { get; set; }
        public string SensorName { get; set; } = string.Empty;
        public float[] SensorValues { get; set; } = Array.Empty<float>();
        public string Message { get; set; } = string.Empty;
    }

    internal sealed class SdlRawButtonState
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Supported { get; set; }
        public bool Pressed { get; set; }
        public DateTimeOffset LastChangedUtc { get; set; }
    }

    internal sealed class SdlRawAxisState
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Supported { get; set; }
        public short RawValue { get; set; }
        public double NormalizedValue { get; set; }
        public DateTimeOffset LastChangedUtc { get; set; }
    }

    internal sealed class SdlRawTouchFingerState
    {
        public int FingerIndex { get; set; }
        public bool Active { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Pressure { get; set; }
        public DateTimeOffset LastChangedUtc { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    internal sealed class SdlRawTouchpadState
    {
        public int TouchpadIndex { get; set; }
        public int FingerCapacity { get; set; }
        public List<SdlRawTouchFingerState> Fingers { get; set; } = new List<SdlRawTouchFingerState>();
    }

    internal sealed class SdlRawSensorState
    {
        public string Name { get; set; } = string.Empty;
        public bool Supported { get; set; }
        public bool Enabled { get; set; }
        public bool EnableAttempted { get; set; }
        public bool EnableSucceeded { get; set; }
        public float DataRateHz { get; set; }
        public string Units { get; set; } = string.Empty;
        public float[] Values { get; set; } = new float[3];
        public string ValuesText => Values == null ? string.Empty : string.Join(", ", Values.Select(item => item.ToString("0.####")));
        public DateTimeOffset LastChangedUtc { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    internal sealed class SdlRawGamepadInfo
    {
        public uint InstanceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MappingName { get; set; } = string.Empty;
        public string Guid { get; set; } = string.Empty;
        public ushort? VendorId { get; set; }
        public ushort? ProductId { get; set; }
        public ushort? ProductVersion { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string DevicePath { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = string.Empty;
        public int? PlayerIndex { get; set; }
        public int? BatteryPercent { get; set; }
        public string BatteryState { get; set; } = string.Empty;
        public string BatteryDisplay => BatteryPercent.HasValue ? $"{BatteryPercent.Value}%" : "Unknown";
        public bool IsMappedGamepad { get; set; }
        public string BestEffortPersistentKey { get; set; } = string.Empty;
        public string IdentityNotes { get; set; } = string.Empty;
        public List<SdlRawButtonState> Buttons { get; set; } = new List<SdlRawButtonState>();
        public List<SdlRawAxisState> Axes { get; set; } = new List<SdlRawAxisState>();
        public List<SdlRawTouchpadState> Touchpads { get; set; } = new List<SdlRawTouchpadState>();
        public List<SdlRawSensorState> Sensors { get; set; } = new List<SdlRawSensorState>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    internal sealed class SdlDiagnosticDeviceSnapshot
    {
        public uint InstanceId { get; set; }
        public bool Connected { get; set; }
        public DateTimeOffset FirstSeenUtc { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; }
        public SdlRawGamepadInfo Info { get; set; } = new SdlRawGamepadInfo();

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Info?.Name) ? $"SDL gamepad {InstanceId}" : $"{Info.Name} ({InstanceId})";
    }

    internal sealed class SdlDiagnosticSessionSnapshot
    {
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
        public SdlDiagnosticVersionInfo Version { get; set; } = new SdlDiagnosticVersionInfo();
        public List<SdlDiagnosticDeviceSnapshot> Devices { get; set; } = new List<SdlDiagnosticDeviceSnapshot>();
        public List<string> Events { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    internal static class SdlDiagnosticModelExtensions
    {
        public static SdlDiagnosticDeviceSnapshot Clone(this SdlDiagnosticDeviceSnapshot source)
        {
            return new SdlDiagnosticDeviceSnapshot
            {
                InstanceId = source.InstanceId,
                Connected = source.Connected,
                FirstSeenUtc = source.FirstSeenUtc,
                LastSeenUtc = source.LastSeenUtc,
                Info = source.Info.Clone(),
            };
        }

        public static SdlRawGamepadInfo Clone(this SdlRawGamepadInfo source)
        {
            return new SdlRawGamepadInfo
            {
                InstanceId = source.InstanceId,
                Name = source.Name,
                MappingName = source.MappingName,
                Guid = source.Guid,
                VendorId = source.VendorId,
                ProductId = source.ProductId,
                ProductVersion = source.ProductVersion,
                SerialNumber = source.SerialNumber,
                DevicePath = source.DevicePath,
                ConnectionType = source.ConnectionType,
                PlayerIndex = source.PlayerIndex,
                BatteryPercent = source.BatteryPercent,
                BatteryState = source.BatteryState,
                IsMappedGamepad = source.IsMappedGamepad,
                BestEffortPersistentKey = source.BestEffortPersistentKey,
                IdentityNotes = source.IdentityNotes,
                Buttons = source.Buttons.Select(item => new SdlRawButtonState { Index = item.Index, Name = item.Name, Supported = item.Supported, Pressed = item.Pressed, LastChangedUtc = item.LastChangedUtc }).ToList(),
                Axes = source.Axes.Select(item => new SdlRawAxisState { Index = item.Index, Name = item.Name, Supported = item.Supported, RawValue = item.RawValue, NormalizedValue = item.NormalizedValue, LastChangedUtc = item.LastChangedUtc }).ToList(),
                Touchpads = source.Touchpads.Select(item => new SdlRawTouchpadState
                {
                    TouchpadIndex = item.TouchpadIndex,
                    FingerCapacity = item.FingerCapacity,
                    Fingers = item.Fingers.Select(finger => new SdlRawTouchFingerState { FingerIndex = finger.FingerIndex, Active = finger.Active, X = finger.X, Y = finger.Y, Pressure = finger.Pressure, LastChangedUtc = finger.LastChangedUtc, LastError = finger.LastError }).ToList(),
                }).ToList(),
                Sensors = source.Sensors.Select(item => new SdlRawSensorState { Name = item.Name, Supported = item.Supported, Enabled = item.Enabled, EnableAttempted = item.EnableAttempted, EnableSucceeded = item.EnableSucceeded, DataRateHz = item.DataRateHz, Units = item.Units, Values = item.Values?.ToArray() ?? Array.Empty<float>(), LastChangedUtc = item.LastChangedUtc, LastError = item.LastError }).ToList(),
                Errors = source.Errors.ToList(),
            };
        }
    }
}
