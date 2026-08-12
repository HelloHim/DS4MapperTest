using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DS4MapperTest.SdlDiagnostics;

namespace DS4MapperTest.ViewModels
{
    internal sealed class SdlDiagnosticsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SdlDiagnosticService service;
        private readonly SdlDiagnosticReportWriter reportWriter;
        private SdlDiagnosticDeviceSnapshot selectedDevice;
        private string statusText = "Stopped";
        private string lastCapturePath = string.Empty;

        public ObservableCollection<SdlDiagnosticDeviceSnapshot> Devices { get; } =
            new ObservableCollection<SdlDiagnosticDeviceSnapshot>();

        public ObservableCollection<string> Events { get; } =
            new ObservableCollection<string>();

        public ObservableCollection<string> Errors { get; } =
            new ObservableCollection<string>();

        public SdlDiagnosticDeviceSnapshot SelectedDevice
        {
            get => selectedDevice;
            set
            {
                selectedDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedDevice));
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

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            service.Dispose();
        }
    }
}
