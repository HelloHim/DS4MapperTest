using System;

namespace DS4MapperTest.PhysicalMouse
{
    public enum PhysicalMouseServiceStatus
    {
        /// <summary>Forwarding turned off (the default).</summary>
        Disabled,
        /// <summary>Enabled, but no device identifier is configured yet.</summary>
        NoDeviceSelected,
        /// <summary>
        /// The configured identifier resolves to a known virtual output
        /// device (see Util.CheckIfVirtualDevice) - refused to start capture
        /// to avoid a FakerInput-into-FakerInput feedback loop.
        /// </summary>
        SelectedDeviceVirtual,
        /// <summary>Capture thread/window failed to initialise.</summary>
        RegistrationFailed,
        /// <summary>Capture is running, but the saved mouse is unplugged.</summary>
        WaitingForSelectedDevice,
        /// <summary>Running normally. The configured device may or may not currently be plugged in.</summary>
        Capturing,
    }

    /// <summary>
    /// Owns the phase-2 physical-mouse capture + forwarding lifecycle:
    ///
    /// RawMouseCaptureDevice (Raw Input) -&gt; PhysicalMouseForwarder (button
    /// ownership + wheel scaling) -&gt; BackendManager.EventInputHandler.
    ///
    /// Deliberately owned by BackendManager, not the WPF UI: it must start
    /// with the backend service and stop/dispose with it regardless of
    /// which window (if any) is open. See BackendManager.Start()/Stop()/
    /// ShutDown().
    /// </summary>
    public sealed class PhysicalMouseService : IDisposable
    {
        public PhysicalMouseServiceStatus Status { get; private set; } = PhysicalMouseServiceStatus.Disabled;
        public event EventHandler StatusChanged;

        private readonly RawMouseCaptureDevice capture = new RawMouseCaptureDevice();
        private readonly PhysicalMouseForwarder forwarder;

        public PhysicalMouseService()
        {
            forwarder = new PhysicalMouseForwarder(capture);
            capture.SelectedDeviceArrived += Capture_SelectedDeviceArrived;
            capture.SelectedDeviceRemoved += Capture_SelectedDeviceRemoved;
        }

        public bool DiagnosticLoggingEnabled
        {
            get => capture.DiagnosticLoggingEnabled;
            set => capture.DiagnosticLoggingEnabled = value;
        }

        /// <summary>
        /// Starts (or restarts) forwarding for the given persisted stable
        /// device id against the given output handler/mapping. Always safe
        /// to call - never throws, and always leaves any previous
        /// capture/forwarding state fully torn down first, so repeated
        /// calls (e.g. backend restarts) can't leak threads, windows or
        /// event subscriptions.
        /// </summary>
        public void Start(bool enabled, string stableDeviceId, VirtualKBMBase handler, VirtualKBMMapping mapping)
        {
            Stop();

            if (!enabled)
            {
                SetStatus(PhysicalMouseServiceStatus.Disabled);
                return;
            }

            if (string.IsNullOrEmpty(stableDeviceId))
            {
                SetStatus(PhysicalMouseServiceStatus.NoDeviceSelected);
                return;
            }

            if (LooksLikeVirtualDevice(stableDeviceId))
            {
                SetStatus(PhysicalMouseServiceStatus.SelectedDeviceVirtual);
                System.Diagnostics.Debug.WriteLine(
                    $"[PhysicalMouseService] refusing to capture '{stableDeviceId}': " +
                    "resolves to a known virtual output device");
                return;
            }

            // Attach before starting capture so the very first Raw Input
            // report the capture thread could possibly see already has a
            // live output to forward to.
            forwarder.AttachOutput(handler, mapping);

            bool started = capture.Start(stableDeviceId);
            SetStatus(!started ? PhysicalMouseServiceStatus.RegistrationFailed
                : capture.IsSelectedDeviceAvailable ? PhysicalMouseServiceStatus.Capturing
                : PhysicalMouseServiceStatus.WaitingForSelectedDevice);

            if (!started)
            {
                forwarder.DetachOutput();
            }
        }

        /// <summary>
        /// Stops capture and detaches the output, releasing any buttons the
        /// physical mouse still held. Safe to call even if never started.
        /// </summary>
        public void Stop()
        {
            capture.Stop();
            forwarder.DetachOutput();
            SetStatus(PhysicalMouseServiceStatus.Disabled);
        }

        public void Dispose()
        {
            Stop();
            forwarder.Dispose();
            capture.Dispose();
        }

        public void Reconfigure(bool enabled, string stableDeviceId,
            VirtualKBMBase handler, VirtualKBMMapping mapping) =>
            Start(enabled, stableDeviceId, handler, mapping);

        private void Capture_SelectedDeviceArrived(object sender, EventArgs e) =>
            SetStatus(PhysicalMouseServiceStatus.Capturing);

        private void Capture_SelectedDeviceRemoved(object sender, EventArgs e) =>
            SetStatus(PhysicalMouseServiceStatus.WaitingForSelectedDevice);

        private void SetStatus(PhysicalMouseServiceStatus status)
        {
            if (Status == status) return;
            Status = status;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private static bool LooksLikeVirtualDevice(string stableDeviceId)
        {
            try
            {
                return Util.CheckIfVirtualDevice(stableDeviceId);
            }
            catch
            {
                // Best-effort heuristic; a lookup failure shouldn't itself
                // block starting capture for what may be a perfectly real
                // mouse.
                return false;
            }
        }
    }
}
