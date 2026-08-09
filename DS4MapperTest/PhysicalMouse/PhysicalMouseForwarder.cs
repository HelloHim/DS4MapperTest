using System;
using System.Collections.Generic;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.PhysicalMouse
{
    public interface IPhysicalMouseOutputRouter
    {
        MouseOutputProducerId RegisterProducer();
        void UnregisterProducer(MouseOutputProducerId producerId);
        void QueueRelative(MouseOutputProducerId producerId, MouseOutputRoute route, int x, int y);
        void QueueWheel(MouseOutputProducerId producerId, MouseOutputRoute route, int vertical, int horizontal);
        void SetButton(MouseOutputProducerId producerId, MouseOutputRoute route, int mouseCode, bool pressed);
        void FlushProducer(MouseOutputProducerId producerId, bool flushSharedFakerInput);
    }

    /// <summary>
    /// Wires <see cref="RawMouseCaptureDevice"/>'s events to the central
    /// mouse-output dispatcher using the dedicated
    /// <see cref="MouseOutputRoute.UnifiedVirtualMouse"/> route. Runs
    /// entirely on the capture thread that raises those events - never
    /// touches the WPF dispatcher, cursor position, or any per-report
    /// sensitivity/acceleration/smoothing.
    ///
    /// Wheel deltas are converted from Raw Input's WHEEL_DELTA-scaled units
    /// into the same 120-count "notch" currency the mouse dispatcher uses,
    /// carrying any fractional notch remainder forward instead of rounding
    /// it away.
    /// </summary>
    public sealed class PhysicalMouseForwarder : IDisposable
    {
        private const double WHEEL_DELTA = 120.0;

        private readonly RawMouseCaptureDevice capture;

        private volatile IPhysicalMouseOutputRouter outputRouter;
        private MouseOutputProducerId? producerId;

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
        /// Points this forwarder at the live mouse-output router. Must be
        /// called before the capture device is started.
        /// </summary>
        public void AttachOutput(IPhysicalMouseOutputRouter router)
        {
            DetachOutput();
            if (router == null)
            {
                return;
            }

            outputRouter = router;
            producerId = router.RegisterProducer();
        }

        /// <summary>
        /// Releases any buttons this source still holds, unregisters its
        /// producer from the dispatcher, then detaches. Safe to call
        /// repeatedly.
        /// </summary>
        public void DetachOutput()
        {
            HandleDeviceRemoved();
            IPhysicalMouseOutputRouter router = outputRouter;
            MouseOutputProducerId? currentProducerId = producerId;
            outputRouter = null;
            producerId = null;
            if (router != null && currentProducerId.HasValue)
            {
                router.UnregisterProducer(currentProducerId.Value);
            }
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
            IPhysicalMouseOutputRouter router = outputRouter;
            MouseOutputProducerId? currentProducerId = producerId;
            if (router == null || !currentProducerId.HasValue ||
                (deltaX == 0 && deltaY == 0))
            {
                return;
            }

            router.QueueRelative(currentProducerId.Value,
                MouseOutputRoute.UnifiedVirtualMouse, deltaX, deltaY);
            router.FlushProducer(currentProducerId.Value, flushSharedFakerInput: true);
        }

        public void HandleMouseButton(RawMouseButton button, bool isPressed)
        {
            IPhysicalMouseOutputRouter router = outputRouter;
            MouseOutputProducerId? currentProducerId = producerId;
            if (router == null || !currentProducerId.HasValue)
            {
                return;
            }

            int mouseCode = ToMouseCode(button);
            if (mouseCode == 0)
            {
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

            router.SetButton(currentProducerId.Value,
                MouseOutputRoute.UnifiedVirtualMouse, mouseCode, isPressed);
            router.FlushProducer(currentProducerId.Value, flushSharedFakerInput: true);
        }

        public void HandleMouseWheel(int delta, bool horizontal)
        {
            IPhysicalMouseOutputRouter router = outputRouter;
            MouseOutputProducerId? currentProducerId = producerId;
            if (router == null || !currentProducerId.HasValue || delta == 0)
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

            int scaled = notches * 120;
            router.QueueWheel(currentProducerId.Value,
                MouseOutputRoute.UnifiedVirtualMouse,
                vertical: horizontal ? 0 : scaled,
                horizontal: horizontal ? scaled : 0);
            router.FlushProducer(currentProducerId.Value, flushSharedFakerInput: true);
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

            IPhysicalMouseOutputRouter router = outputRouter;
            MouseOutputProducerId? currentProducerId = producerId;
            if (router == null || !currentProducerId.HasValue)
            {
                return;
            }

            foreach (RawMouseButton button in toRelease)
            {
                int mouseCode = ToMouseCode(button);
                if (mouseCode != 0)
                {
                    router.SetButton(currentProducerId.Value,
                        MouseOutputRoute.UnifiedVirtualMouse, mouseCode, false);
                }
            }

            router.FlushProducer(currentProducerId.Value, flushSharedFakerInput: true);
        }

        private static int ToMouseCode(RawMouseButton button)
        {
            switch (button)
            {
                case RawMouseButton.Left: return MouseButtonCodes.MOUSE_LEFT_BUTTON;
                case RawMouseButton.Right: return MouseButtonCodes.MOUSE_RIGHT_BUTTON;
                case RawMouseButton.Middle: return MouseButtonCodes.MOUSE_MIDDLE_BUTTON;
                case RawMouseButton.Button4: return MouseButtonCodes.MOUSE_XBUTTON1;
                case RawMouseButton.Button5: return MouseButtonCodes.MOUSE_XBUTTON2;
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
