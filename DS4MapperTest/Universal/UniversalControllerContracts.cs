using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DS4MapperTest.Universal
{
    public sealed class UniversalDeviceIdentity : IEquatable<UniversalDeviceIdentity>
    {
        public string BackendName { get; }
        public string BackendSessionId { get; }
        public string BestEffortPersistentKey { get; }
        public ushort? VendorId { get; }
        public ushort? ProductId { get; }
        public string SerialNumber { get; }
        public string DevicePath { get; }
        public string Guid { get; }
        public bool IsOriginalSteamController2015 { get; }
        public string IdentityNotes { get; }

        public UniversalDeviceIdentity(
            string backendName,
            string backendSessionId,
            string bestEffortPersistentKey = "",
            ushort? vendorId = null,
            ushort? productId = null,
            string serialNumber = "",
            string devicePath = "",
            string guid = "",
            bool isOriginalSteamController2015 = false,
            string identityNotes = "")
        {
            BackendName = backendName ?? string.Empty;
            BackendSessionId = backendSessionId ?? string.Empty;
            BestEffortPersistentKey = bestEffortPersistentKey ?? string.Empty;
            VendorId = vendorId;
            ProductId = productId;
            SerialNumber = serialNumber ?? string.Empty;
            DevicePath = devicePath ?? string.Empty;
            Guid = guid ?? string.Empty;
            IsOriginalSteamController2015 = isOriginalSteamController2015;
            IdentityNotes = identityNotes ?? string.Empty;
        }

        public bool Equals(UniversalDeviceIdentity other)
        {
            if (other == null) return false;

            return string.Equals(BackendName, other.BackendName, StringComparison.Ordinal) &&
                string.Equals(BackendSessionId, other.BackendSessionId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as UniversalDeviceIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((BackendName?.GetHashCode() ?? 0) * 397) ^
                    (BackendSessionId?.GetHashCode() ?? 0);
            }
        }

        public string StrongPhysicalKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DevicePath))
                {
                    return $"path:{DevicePath}";
                }

                if (!string.IsNullOrWhiteSpace(SerialNumber) && VendorId.HasValue && ProductId.HasValue)
                {
                    return $"vid:{VendorId.Value:X4}|pid:{ProductId.Value:X4}|serial:{SerialNumber}";
                }

                return string.Empty;
            }
        }
    }

    public sealed class UniversalControllerIdentity
    {
        public Guid LogicalControllerId { get; }
        public string BackendName { get; }
        public string BackendSessionId { get; }
        public UniversalDeviceIdentity DeviceIdentity { get; }
        public DateTimeOffset ConnectedAtUtc { get; }

        public UniversalControllerIdentity(
            Guid logicalControllerId,
            string backendName,
            string backendSessionId,
            UniversalDeviceIdentity deviceIdentity,
            DateTimeOffset connectedAtUtc)
        {
            LogicalControllerId = logicalControllerId;
            BackendName = backendName ?? string.Empty;
            BackendSessionId = backendSessionId ?? string.Empty;
            DeviceIdentity = deviceIdentity ?? new UniversalDeviceIdentity(BackendName, BackendSessionId);
            ConnectedAtUtc = connectedAtUtc;
        }
    }

    public interface IUniversalController : IDisposable
    {
        UniversalControllerIdentity Identity { get; }
        UniversalControllerConnectionState ConnectionState { get; }
        ControllerCapabilities Capabilities { get; }
        ControllerDisplayInfo DisplayInfo { get; }
        UniversalControllerStateSnapshot State { get; }
        int? BatteryPercent { get; }
    }

    public interface IUniversalControllerBackend : IDisposable
    {
        string BackendName { get; }
        IReadOnlyList<IUniversalController> Controllers { get; }
        event EventHandler ControllersChanged;
        bool Start(out string error);
        void Refresh();
        void Stop();
    }

    public sealed class UniversalController : IUniversalController
    {
        private readonly object syncRoot = new object();
        private UniversalControllerStateSnapshot state;

        public UniversalControllerIdentity Identity { get; }
        public UniversalControllerConnectionState ConnectionState { get; private set; }
        public ControllerCapabilities Capabilities { get; private set; }
        public ControllerDisplayInfo DisplayInfo => Capabilities.DisplayInfo;
        public int? BatteryPercent { get; private set; }

        public UniversalControllerStateSnapshot State
        {
            get
            {
                lock (syncRoot) return state;
            }
        }

        public UniversalController(
            UniversalControllerIdentity identity,
            ControllerCapabilities capabilities,
            UniversalControllerStateSnapshot state)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            this.state = state ?? UniversalControllerStateSnapshot.Disconnected();
            ConnectionState = this.state.IsConnected
                ? UniversalControllerConnectionState.Connected
                : UniversalControllerConnectionState.Disconnected;
        }

        public void PublishState(UniversalControllerStateSnapshot snapshot)
        {
            lock (syncRoot)
            {
                state = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
                ConnectionState = snapshot.IsConnected
                    ? UniversalControllerConnectionState.Connected
                    : UniversalControllerConnectionState.Disconnected;
            }
        }

        public void PublishCapabilities(ControllerCapabilities capabilities)
        {
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        }

        public void PublishBatteryPercent(int? batteryPercent)
        {
            BatteryPercent = batteryPercent >= 0 && batteryPercent <= 100
                ? batteryPercent
                : null;
        }

        public void MarkSuppressed()
        {
            lock (syncRoot)
            {
                state = UniversalControllerStateSnapshot.Disconnected(state.Sequence + 1);
                ConnectionState = UniversalControllerConnectionState.Suppressed;
            }
        }

        public void MarkDisconnected()
        {
            ConnectionState = UniversalControllerConnectionState.Disconnected;
            PublishState(UniversalControllerStateSnapshot.Disconnected(State.Sequence + 1));
        }

        public void Dispose()
        {
            MarkDisconnected();
        }
    }

    public sealed class UniversalControllerManager : IDisposable
    {
        private readonly IReadOnlyList<IUniversalControllerBackend> backends;
        private readonly object syncRoot = new object();
        private IReadOnlyList<IUniversalController> controllers =
            new ReadOnlyCollection<IUniversalController>(Array.Empty<IUniversalController>());
        private bool disposed;

        public IReadOnlyList<IUniversalController> Controllers
        {
            get
            {
                lock (syncRoot) return controllers;
            }
        }

        public event EventHandler ControllersChanged;

        public UniversalControllerManager(IEnumerable<IUniversalControllerBackend> backends)
        {
            this.backends = new ReadOnlyCollection<IUniversalControllerBackend>(
                (backends ?? Enumerable.Empty<IUniversalControllerBackend>()).ToArray());

            foreach (IUniversalControllerBackend backend in this.backends)
            {
                backend.ControllersChanged += Backend_ControllersChanged;
            }
        }

        public bool Start(out IReadOnlyList<string> errors)
        {
            ThrowIfDisposed();
            List<string> tempErrors = new List<string>();
            foreach (IUniversalControllerBackend backend in backends)
            {
                if (!backend.Start(out string error))
                {
                    tempErrors.Add($"{backend.BackendName}: {error}");
                }
            }

            RefreshControllerList();
            errors = tempErrors;
            return tempErrors.Count == 0;
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            foreach (IUniversalControllerBackend backend in backends)
            {
                backend.Refresh();
            }

            RefreshControllerList();
        }

        public void Stop()
        {
            foreach (IUniversalControllerBackend backend in backends)
            {
                backend.Stop();
            }

            RefreshControllerList();
        }

        private void Backend_ControllersChanged(object sender, EventArgs e)
        {
            RefreshControllerList();
        }

        private void RefreshControllerList()
        {
            bool changed;
            lock (syncRoot)
            {
                IUniversalController[] candidates = backends
                    .SelectMany(backend => backend.Controllers)
                    .Where(controller => controller.ConnectionState == UniversalControllerConnectionState.Connected)
                    .ToArray();

                IEnumerable<IUniversalController> unique = candidates
                    .GroupBy(controller => $"{controller.Identity.BackendName}|{controller.Identity.BackendSessionId}")
                    .Select(group => group.First());

                IUniversalController[] next =
                    UniversalBackendArbitrator.SelectAuthoritativeControllers(unique).ToArray();
                changed = !next.Select(item => item.Identity.LogicalControllerId)
                    .SequenceEqual(controllers.Select(item => item.Identity.LogicalControllerId));
                controllers = new ReadOnlyCollection<IUniversalController>(next);
            }

            // This runs on every mapping tick. Announcing a change that did not
            // happen made every listener redo its reconciliation work hundreds
            // of times a second for nothing.
            if (changed)
            {
                ControllersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UniversalControllerManager));
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (IUniversalControllerBackend backend in backends)
            {
                backend.ControllersChanged -= Backend_ControllersChanged;
                backend.Dispose();
            }

            disposed = true;
        }
    }

    public static class UniversalBackendArbitrator
    {
        public static IReadOnlyList<IUniversalController> SelectAuthoritativeControllers(
            IEnumerable<IUniversalController> candidates)
        {
            IUniversalController[] input = (candidates ?? Enumerable.Empty<IUniversalController>()).ToArray();
            List<IUniversalController> result = new List<IUniversalController>();
            bool hasNativeSteam = input.Any(IsNativeOriginalSteamController);

            foreach (IUniversalController controller in input)
            {
                if (controller.Identity.DeviceIdentity.IsOriginalSteamController2015 &&
                    controller.Identity.BackendName != UniversalControllerBackendIds.SteamControllerNative)
                {
                    continue;
                }

                if (hasNativeSteam &&
                    controller.Identity.DeviceIdentity.IsOriginalSteamController2015 &&
                    controller.Identity.BackendName != UniversalControllerBackendIds.SteamControllerNative)
                {
                    continue;
                }

                result.Add(controller);
            }

            return result;
        }

        public static bool IsNativeOriginalSteamController(IUniversalController controller)
        {
            return controller != null &&
                controller.Identity.BackendName == UniversalControllerBackendIds.SteamControllerNative &&
                controller.Identity.DeviceIdentity.IsOriginalSteamController2015;
        }
    }
}
