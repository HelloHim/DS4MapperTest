using System;
using System.Collections.Generic;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.PhysicalMouse
{
    /// <summary>
    /// Wires <see cref="RawMouseCaptureDevice"/>'s events to the shared
    /// virtual mouse/keyboard output (<see cref="BackendManager.EventInputHandler"/>).
    /// Runs entirely on the capture thread that raises those events - never
    /// touches the WPF dispatcher, cursor position, or any per-report
    /// sensitivity/acceleration/smoothing.
    ///
    /// Movement passes straight through to <see cref="VirtualKBMBase.MoveRelativeMouse"/>.
    /// FakerInputHandler's own accumulator (see FakerInputHandler.cs) already
    /// safely combines it with any concurrent gyro/controller movement rather
    /// than one source overwriting the other, so this class does nothing
    /// extra to protect that.
    ///
    /// Buttons route through <see cref="Mapper.AcquireSharedMouseButton"/>/
    /// <see cref="Mapper.ReleaseSharedMouseButton"/> - the same cross-source,
    /// reference-counted ownership controller bindings already use - so a
    /// controller hold and a physical-mouse press/release on the same button
    /// can never desync each other.
    ///
    /// Mouse buttons 4/5 are intentionally not forwarded: FakerInput's
    /// underlying vmulti driver has no X-button support (see
    /// FakerInputMapping.PopulateConstants(), which hard-wires
    /// MOUSEEVENTF_XBUTTONDOWN/UP to 0) and Mapper.MouseButtonCodes has no
    /// side-button entries for controller bindings either, so there is
    /// nowhere to send them today.
    ///
    /// Wheel deltas are converted from Raw Input's WHEEL_DELTA-scaled units
    /// into the "notch count" currency PerformMouseWheelEvent expects (see
    /// VirtualKBMMapping.WHEEL_TICK_BASE), carrying any fractional notch
    /// remainder forward instead of rounding it away.
    /// </summary>
    public sealed class PhysicalMouseForwarder : IDisposable
    {
        private const double WHEEL_DELTA = 120.0;

        private readonly RawMouseCaptureDevice capture;

        private volatile VirtualKBMBase eventInputHandler;
        private volatile VirtualKBMMapping eventInputMapping;

        private readonly object heldButtonsLock = new object();
        private readonly HashSet<RawMouseButton> heldButtons = new HashSet<RawMouseButton>();

        private readonly object wheelLock = new object();
        private double verticalWheelRemainder;
        private double horizontalWheelRemainder;

        public PhysicalMouseForwarder(RawMouseCaptureDevice capture)
        {
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
            capture.MouseMove += OnCaptureMouseMove;
            capture.MouseButton += OnCaptureMouseButton;
            capture.MouseWheel += OnCaptureMouseWheel;
            capture.SelectedDeviceRemoved += OnCaptureSelectedDeviceRemoved;
        }

        /// <summary>
        /// Points this forwarder at the live output handler/mapping. Must be
        /// called before the capture device is started.
        /// </summary>
        public void AttachOutput(VirtualKBMBase handler, VirtualKBMMapping mapping)
        {
            eventInputHandler = handler;
            eventInputMapping = mapping;
        }

        /// <summary>
        /// Releases any buttons this source still holds (using the still-live
        /// handler) then detaches. Safe to call repeatedly.
        /// </summary>
        public void DetachOutput()
        {
            HandleDeviceRemoved();
            eventInputHandler = null;
            eventInputMapping = null;
        }

        private void OnCaptureMouseMove(object sender, RawMouseMoveEventArgs e) => HandleMouseMove(e.DeltaX, e.DeltaY);
        private void OnCaptureMouseButton(object sender, RawMouseButtonEventArgs e) => HandleMouseButton(e.Button, e.IsPressed);
        private void OnCaptureMouseWheel(object sender, RawMouseWheelEventArgs e) => HandleMouseWheel(e.Delta, e.Horizontal);
        private void OnCaptureSelectedDeviceRemoved(object sender, EventArgs e) => HandleDeviceRemoved();

        // Public (rather than the event-handler signatures directly) so
        // forwarding logic is exercisable from tests without needing a live
        // Raw Input device to raise the real events.

        public void HandleMouseMove(int deltaX, int deltaY)
        {
            VirtualKBMBase handler = eventInputHandler;
            if (handler == null || (deltaX == 0 && deltaY == 0))
            {
                return;
            }

            // Exact counts, no scaling. Promptly flushed rather than waiting
            // for the next controller poll tick's own Sync() call.
            handler.MoveRelativeMouse(deltaX, deltaY);
            handler.Sync();
        }

        public void HandleMouseButton(RawMouseButton button, bool isPressed)
        {
            VirtualKBMBase handler = eventInputHandler;
            VirtualKBMMapping mapping = eventInputMapping;
            if (handler == null || mapping == null)
            {
                return;
            }

            int mouseCode = ToMouseCode(button);
            if (mouseCode == 0)
            {
                // Button4/Button5 - unsupported by the FakerInput backend, see class remarks.
                return;
            }

            bool transition;
            lock (heldButtonsLock)
            {
                transition = isPressed ? heldButtons.Add(button) : heldButtons.Remove(button);
            }

            // Raw Input only reports edges, so this should never happen in
            // practice; guards against a duplicate down inflating the shared
            // refcount, or a stray up we never actually held decrementing it.
            if (!transition)
            {
                return;
            }

            if (isPressed)
            {
                Mapper.AcquireSharedMouseButton(handler, mapping, mouseCode);
            }
            else
            {
                Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode);
            }

            handler.Sync();
        }

        public void HandleMouseWheel(int delta, bool horizontal)
        {
            VirtualKBMBase handler = eventInputHandler;
            VirtualKBMMapping mapping = eventInputMapping;
            if (handler == null || mapping == null || delta == 0)
            {
                return;
            }

            int notches;
            lock (wheelLock)
            {
                if (horizontal)
                {
                    horizontalWheelRemainder += delta / WHEEL_DELTA;
                    notches = (int)horizontalWheelRemainder;
                    horizontalWheelRemainder -= notches;
                }
                else
                {
                    verticalWheelRemainder += delta / WHEEL_DELTA;
                    notches = (int)verticalWheelRemainder;
                    verticalWheelRemainder -= notches;
                }
            }

            if (notches == 0)
            {
                // Sub-notch delta from a high-resolution wheel; carried in
                // the remainder above rather than lost.
                return;
            }

            int scaled = notches * mapping.WHEEL_TICK_BASE;
            handler.PerformMouseWheelEvent(
                vertical: horizontal ? 0 : scaled,
                horizontal: horizontal ? scaled : 0);
            handler.Sync();
        }

        public void HandleDeviceRemoved()
        {
            List<RawMouseButton> toRelease;
            lock (heldButtonsLock)
            {
                if (heldButtons.Count == 0)
                {
                    return;
                }
                toRelease = new List<RawMouseButton>(heldButtons);
                heldButtons.Clear();
            }

            VirtualKBMBase handler = eventInputHandler;
            VirtualKBMMapping mapping = eventInputMapping;
            if (handler == null || mapping == null)
            {
                return;
            }

            foreach (RawMouseButton button in toRelease)
            {
                int mouseCode = ToMouseCode(button);
                if (mouseCode != 0)
                {
                    Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode);
                }
            }

            handler.Sync();
        }

        private static int ToMouseCode(RawMouseButton button)
        {
            switch (button)
            {
                case RawMouseButton.Left: return MouseButtonCodes.MOUSE_LEFT_BUTTON;
                case RawMouseButton.Right: return MouseButtonCodes.MOUSE_RIGHT_BUTTON;
                case RawMouseButton.Middle: return MouseButtonCodes.MOUSE_MIDDLE_BUTTON;
                default: return 0;
            }
        }

        public void Dispose()
        {
            capture.MouseMove -= OnCaptureMouseMove;
            capture.MouseButton -= OnCaptureMouseButton;
            capture.MouseWheel -= OnCaptureMouseWheel;
            capture.SelectedDeviceRemoved -= OnCaptureSelectedDeviceRemoved;
            DetachOutput();
        }
    }
}
