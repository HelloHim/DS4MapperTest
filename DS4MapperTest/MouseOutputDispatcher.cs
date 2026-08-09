using System;
using System.Collections.Generic;
using System.Linq;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.PhysicalMouse;

namespace DS4MapperTest
{
    public readonly struct MouseOutputProducerId : IEquatable<MouseOutputProducerId>
    {
        public MouseOutputProducerId(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool Equals(MouseOutputProducerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is MouseOutputProducerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    internal enum MouseOutputButton
    {
        Left,
        Right,
        Middle,
        Button4,
        Button5,
    }

    internal readonly struct MouseOutputBackendIdentity
    {
        public MouseOutputBackendIdentity(MouseOutputDestination destination, ushort vendorId = 0,
            ushort productId = 0, uint busId = 0, uint deviceId = 0, nuint handle = 0,
            bool supportsAbsolute = false)
        {
            Destination = destination;
            VendorId = vendorId;
            ProductId = productId;
            BusId = busId;
            DeviceId = deviceId;
            Handle = handle;
            SupportsAbsolute = supportsAbsolute;
        }

        public MouseOutputDestination Destination { get; }
        public ushort VendorId { get; }
        public ushort ProductId { get; }
        public uint BusId { get; }
        public uint DeviceId { get; }
        public nuint Handle { get; }
        public bool SupportsAbsolute { get; }
    }

    internal readonly struct MouseOutputBackendSubmission
    {
        public MouseOutputBackendSubmission(byte previousButtons, byte buttons, long relativeX,
            long relativeY, long wheel, long pan, bool hasAbsolute, double absoluteX,
            double absoluteY, bool flush)
        {
            PreviousButtons = previousButtons;
            Buttons = buttons;
            RelativeX = relativeX;
            RelativeY = relativeY;
            Wheel = wheel;
            Pan = pan;
            HasAbsolute = hasAbsolute;
            AbsoluteX = absoluteX;
            AbsoluteY = absoluteY;
            Flush = flush;
        }

        public byte PreviousButtons { get; }
        public byte Buttons { get; }
        public long RelativeX { get; }
        public long RelativeY { get; }
        public long Wheel { get; }
        public long Pan { get; }
        public bool HasAbsolute { get; }
        public double AbsoluteX { get; }
        public double AbsoluteY { get; }
        public bool Flush { get; }
    }

    internal interface IMouseOutputBackend : IDisposable
    {
        MouseOutputDestination Destination { get; }
        bool IsAvailable { get; }
        MouseOutputBackendIdentity Identity { get; }
        bool TrySubmit(MouseOutputBackendSubmission submission);
    }

    internal interface IMouseOutputNativeViiperApi
    {
        bool CreateUSBBus(nuint serverHandle, ref uint busId);
        bool CreateMouseDevice(nuint serverHandle, out nuint handle, uint busId,
            bool autoAttachLocalhost, ushort vendorId, ushort productId);
        bool SetMouseDeviceState(nuint deviceHandle, MouseDeviceState state);
        bool RemoveMouseDevice(nuint deviceHandle);
    }

    internal sealed class LibViiperMouseApi : IMouseOutputNativeViiperApi
    {
        public bool CreateUSBBus(nuint serverHandle, ref uint busId) =>
            LibVIIPER.CreateUSBBus(serverHandle, ref busId);

        public bool CreateMouseDevice(nuint serverHandle, out nuint handle, uint busId,
            bool autoAttachLocalhost, ushort vendorId, ushort productId) =>
            LibVIIPER.CreateMouseDevice(serverHandle, out handle, busId, autoAttachLocalhost,
                vendorId, productId);

        public bool SetMouseDeviceState(nuint deviceHandle, MouseDeviceState state) =>
            LibVIIPER.SetMouseDeviceState(deviceHandle, state);

        public bool RemoveMouseDevice(nuint deviceHandle) =>
            LibVIIPER.RemoveMouseDevice(deviceHandle);
    }

    internal static class MouseOutputButtonMapper
    {
        public static IEnumerable<MouseOutputButton> AllButtons =>
            Enum.GetValues(typeof(MouseOutputButton)).Cast<MouseOutputButton>();

        public static bool TryFromMouseCode(int mouseCode, out MouseOutputButton button)
        {
            switch (mouseCode)
            {
                case MouseButtonCodes.MOUSE_LEFT_BUTTON:
                    button = MouseOutputButton.Left;
                    return true;
                case MouseButtonCodes.MOUSE_RIGHT_BUTTON:
                    button = MouseOutputButton.Right;
                    return true;
                case MouseButtonCodes.MOUSE_MIDDLE_BUTTON:
                    button = MouseOutputButton.Middle;
                    return true;
                case MouseButtonCodes.MOUSE_XBUTTON1:
                    button = MouseOutputButton.Button4;
                    return true;
                case MouseButtonCodes.MOUSE_XBUTTON2:
                    button = MouseOutputButton.Button5;
                    return true;
                default:
                    button = default;
                    return false;
            }
        }

        public static bool TryToMouseCode(MouseOutputButton button, out int mouseCode)
        {
            switch (button)
            {
                case MouseOutputButton.Left:
                    mouseCode = MouseButtonCodes.MOUSE_LEFT_BUTTON;
                    return true;
                case MouseOutputButton.Right:
                    mouseCode = MouseButtonCodes.MOUSE_RIGHT_BUTTON;
                    return true;
                case MouseOutputButton.Middle:
                    mouseCode = MouseButtonCodes.MOUSE_MIDDLE_BUTTON;
                    return true;
                case MouseOutputButton.Button4:
                    mouseCode = MouseButtonCodes.MOUSE_XBUTTON1;
                    return true;
                case MouseOutputButton.Button5:
                    mouseCode = MouseButtonCodes.MOUSE_XBUTTON2;
                    return true;
                default:
                    mouseCode = 0;
                    return false;
            }
        }

        public static uint GetMouseButtonDownFlag(VirtualKBMMapping mapping, MouseOutputButton button)
        {
            switch (button)
            {
                case MouseOutputButton.Left:
                    return mapping.MOUSEEVENTF_LEFTDOWN;
                case MouseOutputButton.Right:
                    return mapping.MOUSEEVENTF_RIGHTDOWN;
                case MouseOutputButton.Middle:
                    return mapping.MOUSEEVENTF_MIDDLEDOWN;
                case MouseOutputButton.Button4:
                    return mapping.MOUSEEVENTF_XBUTTON1DOWN;
                case MouseOutputButton.Button5:
                    return mapping.MOUSEEVENTF_XBUTTON2DOWN;
                default:
                    return 0;
            }
        }

        public static uint GetMouseButtonUpFlag(VirtualKBMMapping mapping, MouseOutputButton button)
        {
            switch (button)
            {
                case MouseOutputButton.Left:
                    return mapping.MOUSEEVENTF_LEFTUP;
                case MouseOutputButton.Right:
                    return mapping.MOUSEEVENTF_RIGHTUP;
                case MouseOutputButton.Middle:
                    return mapping.MOUSEEVENTF_MIDDLEUP;
                case MouseOutputButton.Button4:
                    return mapping.MOUSEEVENTF_XBUTTON1UP;
                case MouseOutputButton.Button5:
                    return mapping.MOUSEEVENTF_XBUTTON2UP;
                default:
                    return 0;
            }
        }

        public static int GetXButtonData(VirtualKBMMapping mapping, MouseOutputButton button)
        {
            switch (button)
            {
                case MouseOutputButton.Button4:
                    return mapping.MOUSEEVENTF_XBUTTON1DATA;
                case MouseOutputButton.Button5:
                    return mapping.MOUSEEVENTF_XBUTTON2DATA;
                default:
                    return 0;
            }
        }

        public static byte ToViiperMask(MouseOutputButton button)
        {
            switch (button)
            {
                case MouseOutputButton.Left:
                    return VIIPERMouseButton.Left;
                case MouseOutputButton.Right:
                    return VIIPERMouseButton.Right;
                case MouseOutputButton.Middle:
                    return VIIPERMouseButton.Middle;
                case MouseOutputButton.Button4:
                    return VIIPERMouseButton.Button4;
                case MouseOutputButton.Button5:
                    return VIIPERMouseButton.Button5;
                default:
                    return 0;
            }
        }
    }

    internal sealed class VirtualKbmMouseOutputBackend : IMouseOutputBackend
    {
        private readonly VirtualKBMBase handler;
        private readonly VirtualKBMMapping mapping;
        private readonly Func<bool> availabilityProvider;
        private readonly bool useSharedFakerInputButtons;

        public VirtualKbmMouseOutputBackend(MouseOutputDestination destination,
            VirtualKBMBase handler, VirtualKBMMapping mapping, Func<bool> availabilityProvider,
            bool useSharedFakerInputButtons, bool supportsAbsolute)
        {
            Destination = destination;
            this.handler = handler;
            this.mapping = mapping;
            this.availabilityProvider = availabilityProvider;
            this.useSharedFakerInputButtons = useSharedFakerInputButtons;
            Identity = new MouseOutputBackendIdentity(destination, supportsAbsolute: supportsAbsolute);
        }

        public MouseOutputDestination Destination { get; }
        public bool IsAvailable => availabilityProvider();
        public MouseOutputBackendIdentity Identity { get; }

        public bool TrySubmit(MouseOutputBackendSubmission submission)
        {
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                if (submission.HasAbsolute)
                {
                    handler.MoveAbsoluteMouse(submission.AbsoluteX, submission.AbsoluteY);
                }

                EmitButtons(submission.PreviousButtons, submission.Buttons);
                SubmitLongRelative(submission.RelativeX, submission.RelativeY,
                    (x, y) => handler.MoveRelativeMouse((int)x, (int)y));
                SubmitLongRelative(submission.Wheel, submission.Pan,
                    (vertical, horizontal) => handler.PerformMouseWheelEvent((int)vertical, (int)horizontal));

                if (submission.Flush)
                {
                    handler.Sync();
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private void EmitButtons(byte previousButtons, byte buttons)
        {
            foreach (MouseOutputButton button in MouseOutputButtonMapper.AllButtons)
            {
                byte mask = MouseOutputButtonMapper.ToViiperMask(button);
                bool wasHeld = (previousButtons & mask) != 0;
                bool isHeld = (buttons & mask) != 0;
                if (wasHeld == isHeld)
                {
                    continue;
                }

                if (useSharedFakerInputButtons)
                {
                    if (!MouseOutputButtonMapper.TryToMouseCode(button, out int mouseCode))
                    {
                        continue;
                    }

                    if (isHeld)
                    {
                        Mapper.AcquireSharedMouseButton(handler, mapping, mouseCode);
                    }
                    else
                    {
                        Mapper.ReleaseSharedMouseButton(handler, mapping, mouseCode);
                    }

                    continue;
                }

                uint flag = isHeld
                    ? MouseOutputButtonMapper.GetMouseButtonDownFlag(mapping, button)
                    : MouseOutputButtonMapper.GetMouseButtonUpFlag(mapping, button);
                int xButtonData = MouseOutputButtonMapper.GetXButtonData(mapping, button);
                if (xButtonData != 0)
                {
                    if (isHeld)
                    {
                        handler.PerformMouseButtonPressAlt(flag, xButtonData);
                    }
                    else
                    {
                        handler.PerformMouseButtonReleaseAlt(flag, xButtonData);
                    }
                }
                else
                {
                    if (isHeld)
                    {
                        handler.PerformMouseButtonPress(flag);
                    }
                    else
                    {
                        handler.PerformMouseButtonRelease(flag);
                    }
                }
            }
        }

        private static void SubmitLongRelative(long first, long second, Action<long, long> submit)
        {
            while (first != 0 || second != 0)
            {
                long x = ClampToInt(ref first);
                long y = ClampToInt(ref second);
                submit(x, y);
            }
        }

        private static long ClampToInt(ref long pending)
        {
            long value = pending < int.MinValue ? int.MinValue :
                pending > int.MaxValue ? int.MaxValue : pending;
            pending -= value;
            return value;
        }

        public void Dispose()
        {
        }
    }

    internal sealed class MouseOutputViiperMouseDevice : IDisposable
    {
        private const long MinRelative = short.MinValue;
        private const long MaxRelative = short.MaxValue;
        private readonly IMouseOutputNativeViiperApi nativeApi;

        public MouseOutputViiperMouseDevice(MouseOutputDestination destination, ushort vendorId,
            ushort productId, IMouseOutputNativeViiperApi nativeApi)
        {
            Destination = destination;
            VendorId = vendorId;
            ProductId = productId;
            this.nativeApi = nativeApi;
        }

        public MouseOutputDestination Destination { get; }
        public ushort VendorId { get; }
        public ushort ProductId { get; }
        public nuint Handle { get; private set; }
        public uint BusId { get; private set; }
        public uint DeviceId { get; private set; }
        public bool IsAvailable { get; private set; }

        public MouseOutputBackendIdentity Identity =>
            new MouseOutputBackendIdentity(Destination, VendorId, ProductId, BusId, DeviceId,
                Handle, supportsAbsolute: false);

        public bool TryCreate(nuint serverHandle)
        {
            Dispose();
            if (serverHandle == 0)
            {
                return false;
            }

            uint busId = 0;
            if (!nativeApi.CreateUSBBus(serverHandle, ref busId))
            {
                return false;
            }

            if (!nativeApi.CreateMouseDevice(serverHandle, out nuint handle, busId, true,
                VendorId, ProductId))
            {
                return false;
            }

            Handle = handle;
            BusId = busId;
            DeviceId = 0;
            IsAvailable = true;
            return true;
        }

        public bool TrySubmitState(byte buttons, long relativeX, long relativeY, long wheel, long pan)
        {
            if (!IsAvailable)
            {
                return false;
            }

            bool sentAny = false;
            long pendingX = relativeX;
            long pendingY = relativeY;
            long pendingWheel = wheel;
            long pendingPan = pan;
            while (pendingX != 0 || pendingY != 0 || pendingWheel != 0 || pendingPan != 0 || !sentAny)
            {
                MouseDeviceState state = new MouseDeviceState
                {
                    Buttons = buttons,
                    DX = ClampAndConsume(ref pendingX),
                    DY = ClampAndConsume(ref pendingY),
                    Wheel = ClampAndConsume(ref pendingWheel),
                    Pan = ClampAndConsume(ref pendingPan),
                };

                if (!nativeApi.SetMouseDeviceState(Handle, state))
                {
                    IsAvailable = false;
                    return false;
                }

                sentAny = true;
            }

            return true;
        }

        private static short ClampAndConsume(ref long pending)
        {
            long value = pending < MinRelative ? MinRelative :
                pending > MaxRelative ? MaxRelative : pending;
            pending -= value;
            return (short)value;
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                try
                {
                    nativeApi.RemoveMouseDevice(Handle);
                }
                catch
                {
                }
            }

            Handle = 0;
            BusId = 0;
            DeviceId = 0;
            IsAvailable = false;
        }
    }

    internal sealed class MouseOutputViiperBackend : IMouseOutputBackend
    {
        private readonly MouseOutputViiperMouseDevice device;

        public MouseOutputViiperBackend(MouseOutputViiperMouseDevice device)
        {
            this.device = device;
        }

        public MouseOutputDestination Destination => device.Destination;
        public bool IsAvailable => device.IsAvailable;
        public MouseOutputBackendIdentity Identity => device.Identity;

        public bool TrySubmit(MouseOutputBackendSubmission submission)
        {
            return device.TrySubmitState(submission.Buttons, submission.RelativeX,
                submission.RelativeY, submission.Wheel, submission.Pan);
        }

        public void Dispose()
        {
            device.Dispose();
        }
    }

    internal sealed class MouseOutputViiperManager : IDisposable
    {
        private readonly Dictionary<MouseOutputDestination, MouseOutputViiperMouseDevice> devices;
        private nuint serverHandle;

        public MouseOutputViiperManager()
            : this(new LibViiperMouseApi())
        {
        }

        public MouseOutputViiperManager(IMouseOutputNativeViiperApi nativeApi)
        {
            devices = new Dictionary<MouseOutputDestination, MouseOutputViiperMouseDevice>
            {
                [MouseOutputDestination.ViiperMouse1] =
                    new MouseOutputViiperMouseDevice(MouseOutputDestination.ViiperMouse1, 0x2E8A, 0x1011, nativeApi),
                [MouseOutputDestination.ViiperMouse2] =
                    new MouseOutputViiperMouseDevice(MouseOutputDestination.ViiperMouse2, 0x2E8A, 0x1012, nativeApi),
                [MouseOutputDestination.ViiperMouse3] =
                    new MouseOutputViiperMouseDevice(MouseOutputDestination.ViiperMouse3, 0x2E8A, 0x1013, nativeApi),
            };
        }

        public void Initialise(nuint serverHandle)
        {
            this.serverHandle = serverHandle;
            foreach (MouseOutputViiperMouseDevice device in devices.Values)
            {
                device.TryCreate(serverHandle);
            }
        }

        public void RefreshAvailability()
        {
            if (serverHandle == 0)
            {
                return;
            }

            foreach (MouseOutputViiperMouseDevice device in devices.Values)
            {
                if (!device.IsAvailable)
                {
                    device.TryCreate(serverHandle);
                }
            }
        }

        public MouseOutputViiperMouseDevice GetDevice(MouseOutputDestination destination)
        {
            devices.TryGetValue(destination, out MouseOutputViiperMouseDevice device);
            return device;
        }

        public IEnumerable<MouseOutputViiperBackend> CreateBackends()
        {
            return devices.Values.Select(device => new MouseOutputViiperBackend(device));
        }

        public void Dispose()
        {
            foreach (MouseOutputViiperMouseDevice device in devices.Values)
            {
                device.Dispose();
            }

            serverHandle = 0;
        }
    }

    public sealed class MouseOutputDispatcher :
        IMouseOutputRoutingRuntime, IPhysicalMouseOutputRouter, IDisposable
    {
        private sealed class ProducerRouteState
        {
            public MouseOutputDestination? ActiveDestination;
            public readonly HashSet<MouseOutputButton> HeldButtons = new HashSet<MouseOutputButton>();
            public long PendingRelativeX;
            public long PendingRelativeY;
            public long PendingWheel;
            public long PendingPan;
            public bool HasAbsolute;
            public double AbsoluteX;
            public double AbsoluteY;
        }

        private sealed class ProducerState
        {
            public readonly Dictionary<MouseOutputRoute, ProducerRouteState> Routes =
                new Dictionary<MouseOutputRoute, ProducerRouteState>();
        }

        private sealed class DestinationState
        {
            public byte CurrentButtons;
            public long PendingRelativeX;
            public long PendingRelativeY;
            public long PendingWheel;
            public long PendingPan;
            public bool HasAbsolute;
            public double AbsoluteX;
            public double AbsoluteY;
            public readonly Dictionary<MouseOutputButton, HashSet<string>> ButtonOwners =
                new Dictionary<MouseOutputButton, HashSet<string>>();
        }

        private long nextProducerId = 1;
        private readonly object syncRoot = new object();
        private readonly AppGlobalData appGlobal;
        private readonly Dictionary<MouseOutputProducerId, ProducerState> producers =
            new Dictionary<MouseOutputProducerId, ProducerState>();
        private readonly Dictionary<MouseOutputDestination, DestinationState> destinationStates =
            Enum.GetValues(typeof(MouseOutputDestination))
            .Cast<MouseOutputDestination>()
            .ToDictionary(destination => destination, _ => new DestinationState());
        private readonly Dictionary<MouseOutputDestination, IMouseOutputBackend> backends;
        private readonly MouseOutputRoutingResolver resolver = new MouseOutputRoutingResolver();
        private readonly MouseOutputViiperManager viiperManager;
        public event EventHandler StateChanged;

        public MouseOutputDispatcher(AppGlobalData appGlobal, VirtualKBMBase fakerInputHandler,
            VirtualKBMMapping fakerInputMapping, nuint viiperServerHandle)
            : this(appGlobal, CreateProductionBackends(appGlobal, fakerInputHandler,
                fakerInputMapping, viiperServerHandle, out MouseOutputViiperManager viiperManager),
                  viiperManager)
        {
        }

        internal MouseOutputDispatcher(AppGlobalData appGlobal,
            IEnumerable<IMouseOutputBackend> backends)
            : this(appGlobal, backends, null)
        {
        }

        private MouseOutputDispatcher(AppGlobalData appGlobal,
            IEnumerable<IMouseOutputBackend> backends, MouseOutputViiperManager viiperManager)
        {
            this.appGlobal = appGlobal;
            this.backends = backends.ToDictionary(backend => backend.Destination);
            this.viiperManager = viiperManager;
        }

        public MouseOutputProducerId RegisterProducer()
        {
            lock (syncRoot)
            {
                MouseOutputProducerId producerId = new MouseOutputProducerId(nextProducerId++);
                producers[producerId] = new ProducerState();
                return producerId;
            }
        }

        public void UnregisterProducer(MouseOutputProducerId producerId)
        {
            lock (syncRoot)
            {
                if (!producers.TryGetValue(producerId, out ProducerState producer))
                {
                    return;
                }

                foreach ((MouseOutputRoute route, ProducerRouteState routeState) in producer.Routes)
                {
                    ReleaseRouteButtons(producerId, route, routeState);
                    routeState.PendingRelativeX = 0;
                    routeState.PendingRelativeY = 0;
                    routeState.PendingWheel = 0;
                    routeState.PendingPan = 0;
                    routeState.HasAbsolute = false;
                }

                producers.Remove(producerId);
                FlushAllDestinations(flushSharedFakerInput: false);
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void QueueRelative(MouseOutputProducerId producerId, MouseOutputRoute route, int x, int y)
        {
            lock (syncRoot)
            {
                ProducerRouteState state = GetRouteState(producerId, route);
                state.PendingRelativeX += x;
                state.PendingRelativeY += y;
            }
        }

        public void QueueWheel(MouseOutputProducerId producerId, MouseOutputRoute route,
            int vertical, int horizontal)
        {
            lock (syncRoot)
            {
                ProducerRouteState state = GetRouteState(producerId, route);
                state.PendingWheel += vertical;
                state.PendingPan += horizontal;
            }
        }

        public void QueueAbsolute(MouseOutputProducerId producerId, double x, double y)
        {
            lock (syncRoot)
            {
                ProducerRouteState state = GetRouteState(producerId, MouseOutputRoute.AbsoluteMouse);
                state.AbsoluteX = x;
                state.AbsoluteY = y;
                state.HasAbsolute = true;
            }
        }

        public void SetButton(MouseOutputProducerId producerId, MouseOutputRoute route,
            int mouseCode, bool pressed)
        {
            if (!MouseOutputButtonMapper.TryFromMouseCode(mouseCode, out MouseOutputButton button))
            {
                return;
            }

            lock (syncRoot)
            {
                ProducerRouteState state = GetRouteState(producerId, route);
                if (pressed)
                {
                    state.HeldButtons.Add(button);
                    if (state.ActiveDestination.HasValue)
                    {
                        AddButtonOwner(state.ActiveDestination.Value, button,
                            CreateButtonOwnerKey(producerId, route));
                    }
                }
                else
                {
                    state.HeldButtons.Remove(button);
                    if (state.ActiveDestination.HasValue &&
                        destinationStates[state.ActiveDestination.Value].ButtonOwners
                            .TryGetValue(button, out HashSet<string> owners))
                    {
                        owners.Remove(CreateButtonOwnerKey(producerId, route));
                    }
                }
            }
        }

        public void FlushProducer(MouseOutputProducerId producerId, bool flushSharedFakerInput)
        {
            lock (syncRoot)
            {
                if (!producers.TryGetValue(producerId, out ProducerState producer))
                {
                    return;
                }

                MouseOutputRoutingAvailabilitySnapshot availability = CreateAvailabilitySnapshot();
                MouseOutputRoutingTable routing = appGlobal.appSettings.MouseOutputRouting;
                FlushProducerState(producerId, producer, routing, availability);

                FlushAllDestinations(flushSharedFakerInput);
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        internal MouseOutputBackendIdentity GetBackendIdentity(MouseOutputDestination destination) =>
            backends.TryGetValue(destination, out IMouseOutputBackend backend)
                ? backend.Identity
                : default;

        internal bool IsDestinationAvailable(MouseOutputDestination destination) =>
            backends.TryGetValue(destination, out IMouseOutputBackend backend) && backend.IsAvailable;

        public MouseOutputRoutingAvailabilitySnapshot GetAvailabilitySnapshot()
        {
            lock (syncRoot)
            {
                return CreateAvailabilitySnapshot();
            }
        }

        public IReadOnlyList<MouseOutputRouteResolution> GetRouteResolutions()
        {
            lock (syncRoot)
            {
                MouseOutputRoutingAvailabilitySnapshot availability = CreateAvailabilitySnapshot();
                MouseOutputRoutingTable routing = appGlobal.appSettings.MouseOutputRouting;
                return Enum.GetValues(typeof(MouseOutputRoute))
                    .Cast<MouseOutputRoute>()
                    .Select(route => resolver.Resolve(route,
                        routing.GetRouteDestination(route), availability))
                    .ToArray();
            }
        }

        public void RefreshRouting(bool flushSharedFakerInput)
        {
            lock (syncRoot)
            {
                MouseOutputRoutingAvailabilitySnapshot availability = CreateAvailabilitySnapshot();
                MouseOutputRoutingTable routing = appGlobal.appSettings.MouseOutputRouting;
                foreach ((MouseOutputProducerId producerId, ProducerState producerState) in producers)
                {
                    FlushProducerState(producerId, producerState, routing, availability);
                }

                FlushAllDestinations(flushSharedFakerInput);
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void FlushAllDestinations(bool flushSharedFakerInput)
        {
            FlushDestination(MouseOutputDestination.SendInput, flushSharedFakerInput);
            FlushDestination(MouseOutputDestination.FakerInputMouse, flushSharedFakerInput);
            FlushDestination(MouseOutputDestination.ViiperMouse1, flushSharedFakerInput);
            FlushDestination(MouseOutputDestination.ViiperMouse2, flushSharedFakerInput);
            FlushDestination(MouseOutputDestination.ViiperMouse3, flushSharedFakerInput);
        }

        private void FlushDestination(MouseOutputDestination destination, bool flushSharedFakerInput)
        {
            if (!backends.TryGetValue(destination, out IMouseOutputBackend backend))
            {
                return;
            }

            DestinationState state = destinationStates[destination];
            byte nextButtons = CalculateButtonMask(state);
            MouseOutputBackendSubmission submission = new MouseOutputBackendSubmission(
                state.CurrentButtons,
                nextButtons,
                state.PendingRelativeX,
                state.PendingRelativeY,
                state.PendingWheel,
                state.PendingPan,
                state.HasAbsolute,
                state.AbsoluteX,
                state.AbsoluteY,
                flushSharedFakerInput && destination == MouseOutputDestination.FakerInputMouse);

            bool success;
            try
            {
                success = backend.TrySubmit(submission);
            }
            catch
            {
                success = false;
            }

            if (!success)
            {
                state.CurrentButtons = 0;
                state.PendingRelativeX = 0;
                state.PendingRelativeY = 0;
                state.PendingWheel = 0;
                state.PendingPan = 0;
                state.HasAbsolute = false;
                return;
            }

            state.CurrentButtons = nextButtons;
            state.PendingRelativeX = 0;
            state.PendingRelativeY = 0;
            state.PendingWheel = 0;
            state.PendingPan = 0;
            state.HasAbsolute = false;
        }

        private MouseOutputRoutingAvailabilitySnapshot CreateAvailabilitySnapshot()
        {
            viiperManager?.RefreshAvailability();
            return new MouseOutputRoutingAvailabilitySnapshot(
                sendInputAvailable: IsDestinationAvailable(MouseOutputDestination.SendInput),
                fakerInputMouseAvailable: IsDestinationAvailable(MouseOutputDestination.FakerInputMouse),
                viiperMouse1Available: IsDestinationAvailable(MouseOutputDestination.ViiperMouse1),
                viiperMouse2Available: IsDestinationAvailable(MouseOutputDestination.ViiperMouse2),
                viiperMouse3Available: IsDestinationAvailable(MouseOutputDestination.ViiperMouse3),
                viiperAbsoluteMouseSupported: false);
        }

        private void ReleaseRouteButtons(MouseOutputProducerId producerId, MouseOutputRoute route,
            ProducerRouteState routeState)
        {
            if (!routeState.ActiveDestination.HasValue)
            {
                return;
            }

            DestinationState destinationState = destinationStates[routeState.ActiveDestination.Value];
            foreach (MouseOutputButton button in routeState.HeldButtons)
            {
                if (!destinationState.ButtonOwners.TryGetValue(button, out HashSet<string> owners))
                {
                    continue;
                }

                owners.Remove(CreateButtonOwnerKey(producerId, route));
            }
        }

        private void ApplyRouteButtons(MouseOutputProducerId producerId, MouseOutputRoute route,
            ProducerRouteState routeState)
        {
            if (!routeState.ActiveDestination.HasValue)
            {
                return;
            }

            string ownerKey = CreateButtonOwnerKey(producerId, route);
            foreach (MouseOutputButton button in routeState.HeldButtons)
            {
                AddButtonOwner(routeState.ActiveDestination.Value, button, ownerKey);
            }
        }

        private void AddButtonOwner(MouseOutputDestination destination, MouseOutputButton button,
            string ownerKey)
        {
            DestinationState destinationState = destinationStates[destination];
            if (!destinationState.ButtonOwners.TryGetValue(button, out HashSet<string> owners))
            {
                owners = new HashSet<string>();
                destinationState.ButtonOwners[button] = owners;
            }

            owners.Add(ownerKey);
        }

        private ProducerRouteState GetRouteState(MouseOutputProducerId producerId, MouseOutputRoute route)
        {
            ProducerState producer = producers[producerId];
            if (!producer.Routes.TryGetValue(route, out ProducerRouteState routeState))
            {
                routeState = new ProducerRouteState();
                producer.Routes[route] = routeState;
            }

            return routeState;
        }

        private static string CreateButtonOwnerKey(MouseOutputProducerId producerId,
            MouseOutputRoute route) => $"{producerId.Value}:{route}";

        private void FlushProducerState(MouseOutputProducerId producerId, ProducerState producer,
            MouseOutputRoutingTable routing, MouseOutputRoutingAvailabilitySnapshot availability)
        {
            foreach ((MouseOutputRoute route, ProducerRouteState routeState) in producer.Routes)
            {
                MouseOutputDestination configured = routing.GetRouteDestination(route);
                MouseOutputRouteResolution resolution = resolver.Resolve(route, configured, availability);
                if (routeState.ActiveDestination != resolution.ActiveDestination)
                {
                    ReleaseRouteButtons(producerId, route, routeState);
                    if (routeState.ActiveDestination.HasValue)
                    {
                        routeState.PendingRelativeX = 0;
                        routeState.PendingRelativeY = 0;
                        routeState.PendingWheel = 0;
                        routeState.PendingPan = 0;
                        routeState.HasAbsolute = false;
                    }

                    routeState.ActiveDestination = resolution.ActiveDestination;
                    ApplyRouteButtons(producerId, route, routeState);
                }

                DestinationState destinationState = destinationStates[resolution.ActiveDestination];
                destinationState.PendingRelativeX += routeState.PendingRelativeX;
                destinationState.PendingRelativeY += routeState.PendingRelativeY;
                destinationState.PendingWheel += routeState.PendingWheel;
                destinationState.PendingPan += routeState.PendingPan;
                routeState.PendingRelativeX = 0;
                routeState.PendingRelativeY = 0;
                routeState.PendingWheel = 0;
                routeState.PendingPan = 0;

                if (routeState.HasAbsolute)
                {
                    destinationState.AbsoluteX = routeState.AbsoluteX;
                    destinationState.AbsoluteY = routeState.AbsoluteY;
                    destinationState.HasAbsolute = true;
                    routeState.HasAbsolute = false;
                }
            }
        }

        private static byte CalculateButtonMask(DestinationState state)
        {
            byte mask = 0;
            foreach ((MouseOutputButton button, HashSet<string> owners) in state.ButtonOwners)
            {
                if (owners.Count > 0)
                {
                    mask |= MouseOutputButtonMapper.ToViiperMask(button);
                }
            }

            return mask;
        }

        private static IEnumerable<IMouseOutputBackend> CreateProductionBackends(
            AppGlobalData appGlobal, VirtualKBMBase fakerInputHandler,
            VirtualKBMMapping fakerInputMapping, nuint viiperServerHandle,
            out MouseOutputViiperManager viiperManager)
        {
            SendInputHandler sendInputHandler = new SendInputHandler();
            sendInputHandler.Connect();
            SendInputMapping sendInputMapping = new SendInputMapping();
            sendInputMapping.PopulateConstants();
            sendInputMapping.PopulateMappings();

            viiperManager = new MouseOutputViiperManager();
            viiperManager.Initialise(viiperServerHandle);

            List<IMouseOutputBackend> result = new List<IMouseOutputBackend>
            {
                new VirtualKbmMouseOutputBackend(
                    MouseOutputDestination.SendInput,
                    sendInputHandler,
                    sendInputMapping,
                    availabilityProvider: () => true,
                    useSharedFakerInputButtons: false,
                    supportsAbsolute: true),
                new VirtualKbmMouseOutputBackend(
                    MouseOutputDestination.FakerInputMouse,
                    fakerInputHandler,
                    fakerInputMapping,
                    availabilityProvider: () => appGlobal.fakerInputInstalled,
                    useSharedFakerInputButtons: true,
                    supportsAbsolute: true),
            };

            result.AddRange(viiperManager.CreateBackends());
            return result;
        }

        public void Dispose()
        {
            foreach (IMouseOutputBackend backend in backends.Values)
            {
                backend.Dispose();
            }
        }
    }
}
