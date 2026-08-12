using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DS4MapperTest.SdlDiagnostics;
using DS4MapperTest.Universal;

namespace DS4MapperTest.ViewModels
{
    internal sealed class SdlDiagnosticsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SdlDiagnosticService service;
        private readonly SdlDiagnosticReportWriter reportWriter;
        private readonly SdlUniversalStateTranslator universalTranslator = new SdlUniversalStateTranslator();
        private SdlDiagnosticDeviceSnapshot selectedDevice;
        private string statusText = "Stopped";
        private string lastCapturePath = string.Empty;
        private string universalStatusText = string.Empty;

        public ObservableCollection<SdlDiagnosticDeviceSnapshot> Devices { get; } =
            new ObservableCollection<SdlDiagnosticDeviceSnapshot>();

        public ObservableCollection<string> Events { get; } =
            new ObservableCollection<string>();

        public ObservableCollection<string> Errors { get; } =
            new ObservableCollection<string>();

        public ObservableCollection<UniversalDiagnosticInputRow> UniversalInputs { get; } =
            new ObservableCollection<UniversalDiagnosticInputRow>();

        public SdlDiagnosticDeviceSnapshot SelectedDevice
        {
            get => selectedDevice;
            set
            {
                selectedDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedDevice));
                RefreshUniversalInputs();
            }
        }

        public bool HasSelectedDevice => SelectedDevice != null;

        public string StatusText
        {
            get => statusText;
            private set
            {
                statusText = value;
                OnPropertyChanged();
            }
        }

        public string LastCapturePath
        {
            get => lastCapturePath;
            private set
            {
                lastCapturePath = value;
                OnPropertyChanged();
            }
        }

        public string UniversalStatusText
        {
            get => universalStatusText;
            private set
            {
                universalStatusText = value;
                OnPropertyChanged();
            }
        }

        public SdlDiagnosticsViewModel(AppGlobalData appGlobal)
            : this(new SdlDiagnosticService(new Sdl3NativeDiagnosticApi()),
                  new SdlDiagnosticReportWriter(appGlobal.LogsPath))
        {
        }

        internal SdlDiagnosticsViewModel(SdlDiagnosticService service, SdlDiagnosticReportWriter reportWriter)
        {
            this.service = service;
            this.reportWriter = reportWriter;
        }

        public bool Start(out string error)
        {
            bool started = service.Start(out error);
            StatusText = started
                ? "Running. SDL input is diagnostic only and does not drive mappings."
                : $"SDL diagnostics unavailable: {error}";
            RefreshFromService();
            return started;
        }

        public void Refresh()
        {
            service.Refresh();
            RefreshFromService();
        }

        public string CaptureSelectedDevice()
        {
            uint? selectedInstanceId = SelectedDevice?.InstanceId;
            string path = reportWriter.WriteReport(service.CreateSnapshot(), selectedInstanceId);
            LastCapturePath = path;
            return path;
        }

        private void RefreshFromService()
        {
            SdlDiagnosticSessionSnapshot snapshot = service.CreateSnapshot();
            uint? selectedId = SelectedDevice?.InstanceId;

            Devices.Clear();
            foreach (SdlDiagnosticDeviceSnapshot device in snapshot.Devices)
            {
                Devices.Add(device);
            }

            SelectedDevice = selectedId.HasValue
                ? Devices.FirstOrDefault(item => item.InstanceId == selectedId.Value) ?? Devices.FirstOrDefault()
                : Devices.FirstOrDefault();

            Events.Clear();
            foreach (string item in snapshot.Events.AsEnumerable().Reverse().Take(100))
            {
                Events.Add(item);
            }

            Errors.Clear();
            foreach (string item in snapshot.Errors.AsEnumerable().Reverse().Take(100))
            {
                Errors.Add(item);
            }

            if (service.Started)
            {
                StatusText = $"Running. Devices: {Devices.Count}. SDL input is diagnostic only.";
            }
        }

        private void RefreshUniversalInputs()
        {
            UniversalInputs.Clear();
            if (SelectedDevice?.Info == null)
            {
                UniversalStatusText = string.Empty;
                return;
            }

            SdlRawGamepadInfo info = SelectedDevice.Info;
            if (universalTranslator.ShouldSuppressForNativeSteamController(info))
            {
                UniversalStatusText = "Authoritative universal owner: native Steam Controller backend. SDL remains diagnostic only.";
                return;
            }

            ControllerCapabilities capabilities = universalTranslator.CreateCapabilities(info);
            UniversalControllerStateSnapshot state = universalTranslator.CreateState(
                info,
                capabilities,
                SelectedDevice.Connected,
                0,
                DateTimeOffset.UtcNow);

            UniversalStatusText = "Translated from the SDL diagnostic snapshot. This does not drive mappings or output.";
            foreach (ControllerInputDescriptor descriptor in capabilities.Descriptors.OrderBy(item => item.InputId))
            {
                state.TryGetValue(descriptor.InputId, out UniversalInputValue value);
                UniversalInputs.Add(new UniversalDiagnosticInputRow(
                    descriptor.InputId.ToString(),
                    descriptor.ValueKind.ToString(),
                    descriptor.NativeDisplayLabel,
                    descriptor.Source.NativeElement,
                    FormatValue(value)));
            }
        }

        private static string FormatValue(UniversalInputValue value)
        {
            if (value == null) return "unsupported";
            if (value.Status == UniversalInputValueStatus.TemporarilyUnavailable) return "temporarily unavailable";

            return value.Kind switch
            {
                UniversalInputValueKind.DigitalButton => value.Pressed ? "pressed" : "released",
                UniversalInputValueKind.AnalogAxis1D => value.AxisValue.ToString("0.0000"),
                UniversalInputValueKind.Stick2D => $"{value.Vector2.X:0.0000}, {value.Vector2.Y:0.0000}",
                UniversalInputValueKind.TouchSurface => $"contacts {value.Contacts.Count(item => item.Active)}, click {value.TouchClickPressed}",
                UniversalInputValueKind.Gyroscope => $"{value.Vector3.X:0.0000}, {value.Vector3.Y:0.0000}, {value.Vector3.Z:0.0000}",
                UniversalInputValueKind.Accelerometer => $"{value.Vector3.X:0.0000}, {value.Vector3.Y:0.0000}, {value.Vector3.Z:0.0000}",
                _ => string.Empty,
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            service.Dispose();
        }
    }

    internal sealed class UniversalDiagnosticInputRow
    {
        public string InputId { get; }
        public string ValueKind { get; }
        public string NativeLabel { get; }
        public string Source { get; }
        public string Value { get; }

        public UniversalDiagnosticInputRow(
            string inputId,
            string valueKind,
            string nativeLabel,
            string source,
            string value)
        {
            InputId = inputId;
            ValueKind = valueKind;
            NativeLabel = nativeLabel;
            Source = source;
            Value = value;
        }
    }
}
