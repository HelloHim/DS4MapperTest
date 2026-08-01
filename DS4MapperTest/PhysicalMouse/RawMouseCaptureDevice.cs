using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DS4MapperTest.PhysicalMouse
{
    /// <summary>
    /// Captures Raw Input mouse reports for a single selected physical mouse,
    /// identified by <see cref="PhysicalMouseDevice.StableId"/>.
    ///
    /// Runs its own background thread with its own message-only window and
    /// its own <see cref="Dispatcher"/> - it never touches the WPF UI
    /// dispatcher/main window. RIDEV_INPUTSINK means capture works
    /// regardless of which window has focus.
    ///
    /// Phase-1 scope: this class only raises events. Nothing here is wired
    /// to FakerInputHandler, VirtualKBMBase, BackendManager or
    /// SendInputHandler - that connection, and physical-mouse suppression,
    /// are explicitly out of scope until the capture lifecycle is wired into
    /// BackendManager.
    ///
    /// Feedback-loop note: filtering is done purely by comparing each
    /// report's RAWINPUTHEADER.hDevice against the handle resolved for the
    /// selected device's stable path (see <see cref="ResolveSelectedDevice"/>).
    /// It never inspects cursor position. A future FakerInput virtual mouse
    /// enumerates as its own Raw Input device with its own device path/handle
    /// (and PhysicalMouseEnumerator flags it via Util.CheckIfVirtualDevice),
    /// so its WM_INPUT reports carry a different hDevice than the selected
    /// physical mouse and are dropped by this same comparison - no special
    /// FakerInput-specific check is required as long as the caller never
    /// selects the virtual device in the first place.
    /// </summary>
    public sealed class RawMouseCaptureDevice : IDisposable
    {
        public event EventHandler<RawMouseMoveEventArgs> MouseMove;
        public event EventHandler<RawMouseButtonEventArgs> MouseButton;
        public event EventHandler<RawMouseWheelEventArgs> MouseWheel;

        /// <summary>Raised when the selected device is (re)resolved to a live Raw Input handle.</summary>
        public event EventHandler SelectedDeviceArrived;
        /// <summary>Raised when the selected device's handle is invalidated (unplugged).</summary>
        public event EventHandler SelectedDeviceRemoved;

        /// <summary>
        /// Off by default. When enabled, lifecycle and per-report details are
        /// written via System.Diagnostics.Debug - a sink kept deliberately
        /// separate from the application's normal log so this never floods
        /// it. Intended for debug/testing use only.
        /// </summary>
        public bool DiagnosticLoggingEnabled { get; set; }

        public bool IsCapturing => running;
        public string SelectedDevicePath => selectedDevicePath;
        public bool IsSelectedDeviceAvailable => selectedHDevice != IntPtr.Zero;

        private readonly object lifecycleLock = new object();

        private Thread captureThread;
        private Dispatcher dispatcher;
        private HwndSource hwndSource;
        private HwndSourceHook wndProcHook;

        private IntPtr rawInputBuffer = IntPtr.Zero;
        private int rawInputBufferSize;
        private static readonly uint RawInputHeaderSize = (uint)Marshal.SizeOf<RawInputNativeMethods.RAWINPUTHEADER>();

        private volatile bool running;
        private bool lastStartRegistrationSucceeded;

        // Only ever touched from the capture thread (Start()'s initial
        // resolution runs there too, gated by readySignal before Start()
        // returns), so no synchronisation is needed for these two fields.
        private string selectedDevicePath;
        private IntPtr selectedHDevice = IntPtr.Zero;

        /// <summary>
        /// Starts the capture thread and registers for Raw Input mouse
        /// reports. <paramref name="stableDeviceId"/> may be null/empty to
        /// start with no device selected (capture runs, nothing is ever
        /// reported until <see cref="SelectDevice"/> is called).
        /// Returns false if RegisterRawInputDevices failed or the capture
        /// window could not be created; the thread is guaranteed to have
        /// exited by the time this returns false.
        /// </summary>
        public bool Start(string stableDeviceId)
        {
            lock (lifecycleLock)
            {
                if (running)
                {
                    return true;
                }

                selectedDevicePath = stableDeviceId;
                selectedHDevice = IntPtr.Zero;
                lastStartRegistrationSucceeded = false;

                using ManualResetEventSlim readySignal = new ManualResetEventSlim(false);

                captureThread = new Thread(() => CaptureThreadMain(readySignal))
                {
                    IsBackground = true,
                    Name = "PhysicalMouse Raw Input Capture",
                };
                captureThread.SetApartmentState(ApartmentState.STA);
                captureThread.Start();

                readySignal.Wait();

                running = lastStartRegistrationSucceeded;
                if (!running)
                {
                    captureThread.Join(2000);
                    captureThread = null;
                }

                return running;
            }
        }

        /// <summary>
        /// Changes which device's reports are forwarded. Safe to call while
        /// running or stopped; safe to pass null/empty to select nothing.
        /// </summary>
        public void SelectDevice(string stableDeviceId)
        {
            selectedDevicePath = stableDeviceId;

            Dispatcher targetDispatcher = dispatcher;
            if (running && targetDispatcher != null)
            {
                targetDispatcher.BeginInvoke(new Action(ResolveSelectedDevice));
            }
        }

        public void Stop()
        {
            lock (lifecycleLock)
            {
                if (!running)
                {
                    return;
                }

                running = false;

                Dispatcher targetDispatcher = dispatcher;
                targetDispatcher?.InvokeShutdown();
                captureThread?.Join(2000);

                captureThread = null;
                dispatcher = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void CaptureThreadMain(ManualResetEventSlim readySignal)
        {
            bool registrationOk = false;

            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;

                HwndSourceParameters parameters = new HwndSourceParameters("DS4MapperTest Raw Input Capture")
                {
                    WindowStyle = 0,
                    ExtendedWindowStyle = 0,
                    ParentWindow = RawInputNativeMethods.HWND_MESSAGE,
                };
                hwndSource = new HwndSource(parameters);
                wndProcHook = WndProc;
                hwndSource.AddHook(wndProcHook);

                rawInputBufferSize = Marshal.SizeOf<RawInputNativeMethods.RAWINPUT>();
                rawInputBuffer = Marshal.AllocHGlobal(rawInputBufferSize);

                registrationOk = RegisterForRawInput(hwndSource.Handle);
                if (registrationOk)
                {
                    ResolveSelectedDevice();
                }
                else
                {
                    LogDiagnostic("RegisterRawInputDevices failed");
                }
            }
            catch (Exception ex)
            {
                registrationOk = false;
                LogDiagnostic($"capture thread init failed: {ex.Message}");
            }
            finally
            {
                lastStartRegistrationSucceeded = registrationOk;
                readySignal.Set();
            }

            if (registrationOk)
            {
                try
                {
                    Dispatcher.Run();
                }
                finally
                {
                    CleanupThreadResources();
                }
            }
            else
            {
                CleanupThreadResources();
            }
        }

        private void CleanupThreadResources()
        {
            try
            {
                if (hwndSource != null)
                {
                    UnregisterRawInput();
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"UnregisterRawInput failed: {ex.Message}");
            }

            if (hwndSource != null)
            {
                if (wndProcHook != null)
                {
                    hwndSource.RemoveHook(wndProcHook);
                }
                hwndSource.Dispose();
                hwndSource = null;
            }

            if (rawInputBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rawInputBuffer);
                rawInputBuffer = IntPtr.Zero;
            }

            selectedHDevice = IntPtr.Zero;
        }

        private bool RegisterForRawInput(IntPtr hwnd)
        {
            RawInputNativeMethods.RAWINPUTDEVICE[] devices = new RawInputNativeMethods.RAWINPUTDEVICE[]
            {
                new RawInputNativeMethods.RAWINPUTDEVICE
                {
                    usUsagePage = 0x01, // Generic Desktop Controls
                    usUsage = 0x02,     // Mouse
                    dwFlags = RawInputNativeMethods.RIDEV_INPUTSINK | RawInputNativeMethods.RIDEV_DEVNOTIFY,
                    hwndTarget = hwnd,
                },
            };

            return RawInputNativeMethods.RegisterRawInputDevices(devices, (uint)devices.Length,
                (uint)Marshal.SizeOf<RawInputNativeMethods.RAWINPUTDEVICE>());
        }

        private void UnregisterRawInput()
        {
            RawInputNativeMethods.RAWINPUTDEVICE[] devices = new RawInputNativeMethods.RAWINPUTDEVICE[]
            {
                new RawInputNativeMethods.RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,
                    usUsage = 0x02,
                    dwFlags = RawInputNativeMethods.RIDEV_REMOVE,
                    hwndTarget = IntPtr.Zero,
                },
            };

            RawInputNativeMethods.RegisterRawInputDevices(devices, (uint)devices.Length,
                (uint)Marshal.SizeOf<RawInputNativeMethods.RAWINPUTDEVICE>());
        }

        // Re-resolves the selected stable device path to a live Raw Input
        // handle. hDevice values are only valid until the next
        // reconnect/reboot (per Microsoft's own documented caveat), so this
        // is called on Start() and again on every WM_INPUT_DEVICE_CHANGE
        // arrival rather than caching the handle across a reconnect.
        private void ResolveSelectedDevice()
        {
            string target = selectedDevicePath;
            if (string.IsNullOrEmpty(target))
            {
                selectedHDevice = IntPtr.Zero;
                return;
            }

            foreach (RawInputNativeMethods.RAWINPUTDEVICELIST entry in PhysicalMouseEnumerator.EnumerateRawInputDeviceList())
            {
                if (entry.dwType != RawInputNativeMethods.RIM_TYPEMOUSE)
                {
                    continue;
                }

                string path = PhysicalMouseEnumerator.GetDeviceName(entry.hDevice);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                path = PhysicalMouseEnumerator.NormalizeDevicePath(path);
                if (!string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool wasUnresolved = selectedHDevice == IntPtr.Zero;
                selectedHDevice = entry.hDevice;
                LogDiagnostic($"selected device resolved: {path} -> {entry.hDevice}");
                if (wasUnresolved)
                {
                    SelectedDeviceArrived?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            bool wasResolved = selectedHDevice != IntPtr.Zero;
            selectedHDevice = IntPtr.Zero;
            if (wasResolved)
            {
                LogDiagnostic("selected device no longer present");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case RawInputNativeMethods.WM_INPUT:
                    ProcessRawInput(lParam);
                    break;
                case RawInputNativeMethods.WM_INPUT_DEVICE_CHANGE:
                    ProcessDeviceChange(wParam, lParam);
                    break;
            }

            return IntPtr.Zero;
        }

        private void ProcessRawInput(IntPtr hRawInput)
        {
            // Cheapest possible reject: nothing selected/resolved yet.
            if (selectedHDevice == IntPtr.Zero || rawInputBuffer == IntPtr.Zero)
            {
                return;
            }

            uint size = (uint)rawInputBufferSize;
            uint written = RawInputNativeMethods.GetRawInputData(hRawInput, RawInputNativeMethods.RID_INPUT,
                rawInputBuffer, ref size, RawInputHeaderSize);

            // Malformed/oversized/unavailable report: drop it rather than
            // risk interpreting a partially-populated buffer.
            if (written == unchecked((uint)-1) || written < RawInputHeaderSize)
            {
                return;
            }

            RawInputNativeMethods.RAWINPUT raw = Marshal.PtrToStructure<RawInputNativeMethods.RAWINPUT>(rawInputBuffer);
            if (raw.header.dwType != RawInputNativeMethods.RIM_TYPEMOUSE)
            {
                return;
            }

            if (raw.header.hDevice != selectedHDevice)
            {
                return;
            }

            DispatchMouseReport(ref raw.mouse);
        }

        private void DispatchMouseReport(ref RawInputNativeMethods.RAWMOUSE mouse)
        {
            // Only relative reports are in scope for this capture path;
            // absolute-positioned reports (rare - e.g. some RDP/virtual
            // sources) are intentionally not translated or forwarded here.
            if ((mouse.usFlags & RawInputNativeMethods.MOUSE_MOVE_ABSOLUTE) == 0)
            {
                if (mouse.lLastX != 0 || mouse.lLastY != 0)
                {
                    LogDiagnostic($"move dx={mouse.lLastX} dy={mouse.lLastY}");
                    MouseMove?.Invoke(this, new RawMouseMoveEventArgs(mouse.lLastX, mouse.lLastY));
                }
            }

            ushort flags = mouse.usButtonFlags;
            if (flags == 0)
            {
                return;
            }

            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN, RawMouseButton.Left, true);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_LEFT_BUTTON_UP, RawMouseButton.Left, false);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN, RawMouseButton.Right, true);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_RIGHT_BUTTON_UP, RawMouseButton.Right, false);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_MIDDLE_BUTTON_DOWN, RawMouseButton.Middle, true);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_MIDDLE_BUTTON_UP, RawMouseButton.Middle, false);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_BUTTON_4_DOWN, RawMouseButton.Button4, true);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_BUTTON_4_UP, RawMouseButton.Button4, false);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_BUTTON_5_DOWN, RawMouseButton.Button5, true);
            RaiseButtonIfSet(flags, RawInputNativeMethods.RI_MOUSE_BUTTON_5_UP, RawMouseButton.Button5, false);

            if ((flags & RawInputNativeMethods.RI_MOUSE_WHEEL) != 0)
            {
                short delta = unchecked((short)mouse.usButtonData);
                LogDiagnostic($"wheel delta={delta}");
                MouseWheel?.Invoke(this, new RawMouseWheelEventArgs(delta, false));
            }

            if ((flags & RawInputNativeMethods.RI_MOUSE_HWHEEL) != 0)
            {
                short delta = unchecked((short)mouse.usButtonData);
                LogDiagnostic($"hwheel delta={delta}");
                MouseWheel?.Invoke(this, new RawMouseWheelEventArgs(delta, true));
            }
        }

        private void RaiseButtonIfSet(ushort flags, ushort mask, RawMouseButton button, bool isDown)
        {
            if ((flags & mask) != 0)
            {
                LogDiagnostic($"button {button} {(isDown ? "down" : "up")}");
                MouseButton?.Invoke(this, new RawMouseButtonEventArgs(button, isDown));
            }
        }

        private void ProcessDeviceChange(IntPtr wParam, IntPtr lParam)
        {
            long code = wParam.ToInt64();
            IntPtr changedDevice = lParam;

            if (code == RawInputNativeMethods.GIDC_REMOVAL)
            {
                if (changedDevice == selectedHDevice && selectedHDevice != IntPtr.Zero)
                {
                    selectedHDevice = IntPtr.Zero;
                    LogDiagnostic("selected device removed");
                    SelectedDeviceRemoved?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (code == RawInputNativeMethods.GIDC_ARRIVAL)
            {
                // A device (re)connected somewhere; hDevice assignments are
                // not stable across reconnects, so re-resolve from the
                // current device list rather than assuming this arrival is
                // (or isn't) the selected device.
                ResolveSelectedDevice();
            }
        }

        private void LogDiagnostic(string message)
        {
            if (!DiagnosticLoggingEnabled)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RawMouseCapture] {message}");
        }
    }
}
