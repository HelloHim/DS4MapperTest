using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest
{
    public interface IMouseOutputRoutingService
    {
        event EventHandler StateChanged;
        MouseOutputRoutingRuntimeSnapshot GetSnapshot();
        void ApplyRouting(MouseOutputRoutingTable routing);
    }

    public interface IMouseOutputRoutingRuntime
    {
        event EventHandler StateChanged;
        MouseOutputRoutingAvailabilitySnapshot GetAvailabilitySnapshot();
        IReadOnlyList<MouseOutputRouteResolution> GetRouteResolutions();
        void RefreshRouting(bool flushSharedFakerInput);
    }

    public sealed class MouseOutputRoutingRuntimeSnapshot
    {
        private readonly IReadOnlyDictionary<MouseOutputRoute, MouseOutputRouteResolution> resolutions;

        public MouseOutputRoutingRuntimeSnapshot(
            MouseOutputRoutingTable configuredRouting,
            MouseOutputRoutingAvailabilitySnapshot availability,
            IEnumerable<MouseOutputRouteResolution> resolutions,
            bool serviceRunning)
        {
            ConfiguredRouting = CloneRouting(configuredRouting ?? new MouseOutputRoutingTable());
            Availability = availability ?? new MouseOutputRoutingAvailabilitySnapshot();
            this.resolutions = (resolutions ?? Array.Empty<MouseOutputRouteResolution>())
                .ToDictionary(item => item.Route);
            ServiceRunning = serviceRunning;
        }

        public MouseOutputRoutingTable ConfiguredRouting { get; }
        public MouseOutputRoutingAvailabilitySnapshot Availability { get; }
        public bool ServiceRunning { get; }

        public MouseOutputRouteResolution GetResolution(MouseOutputRoute route)
        {
            if (resolutions.TryGetValue(route, out MouseOutputRouteResolution resolution))
            {
                return resolution;
            }

            MouseOutputDestination configuredDestination = ConfiguredRouting.GetRouteDestination(route);
            return new MouseOutputRouteResolution(route, configuredDestination,
                configuredDestination, MouseOutputFallbackReason.None);
        }

        public static MouseOutputRoutingTable CloneRouting(MouseOutputRoutingTable routing)
        {
            routing ??= new MouseOutputRoutingTable();
            return new MouseOutputRoutingTable
            {
                Gyro = routing.Gyro,
                JoystickMouse = routing.JoystickMouse,
                FlickStick = routing.FlickStick,
                Trackpad = routing.Trackpad,
                TriggerMouse = routing.TriggerMouse,
                Other = routing.Other,
                AbsoluteMouse = routing.AbsoluteMouse,
            };
        }
    }

    public sealed class MouseOutputRoutingController : IMouseOutputRoutingService, IDisposable
    {
        private IMouseOutputRoutingRuntime runtime;
        private bool serviceRunning;
        private bool disposed;

        public MouseOutputRoutingController(AppGlobalData appGlobal)
        {
            AppGlobal = appGlobal ?? throw new ArgumentNullException(nameof(appGlobal));
        }

        public event EventHandler StateChanged;

        public AppGlobalData AppGlobal { get; }

        public void AttachRuntime(IMouseOutputRoutingRuntime runtime, bool isServiceRunning)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MouseOutputRoutingController));

            if (ReferenceEquals(this.runtime, runtime) && serviceRunning == isServiceRunning)
            {
                return;
            }

            DetachRuntime();
            this.runtime = runtime;
            serviceRunning = isServiceRunning;
            if (this.runtime != null)
            {
                this.runtime.StateChanged += Runtime_StateChanged;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DetachRuntime()
        {
            if (runtime != null)
            {
                runtime.StateChanged -= Runtime_StateChanged;
                runtime = null;
            }
        }

        public void SetServiceRunning(bool isServiceRunning)
        {
            if (serviceRunning == isServiceRunning)
            {
                return;
            }

            serviceRunning = isServiceRunning;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public MouseOutputRoutingRuntimeSnapshot GetSnapshot()
        {
            MouseOutputRoutingTable configuredRouting =
                MouseOutputRoutingRuntimeSnapshot.CloneRouting(AppGlobal.appSettings?.MouseOutputRouting);

            if (runtime != null && serviceRunning)
            {
                return new MouseOutputRoutingRuntimeSnapshot(configuredRouting,
                    runtime.GetAvailabilitySnapshot(), runtime.GetRouteResolutions(),
                    serviceRunning: true);
            }

            MouseOutputRouteResolution[] configuredResolutions =
                Enum.GetValues(typeof(MouseOutputRoute))
                .Cast<MouseOutputRoute>()
                .Select(route =>
                {
                    MouseOutputDestination destination =
                        configuredRouting.GetRouteDestination(route);
                    return new MouseOutputRouteResolution(route, destination,
                        destination, MouseOutputFallbackReason.None);
                })
                .ToArray();

            return new MouseOutputRoutingRuntimeSnapshot(configuredRouting,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: AppGlobal.fakerInputInstalled),
                configuredResolutions, serviceRunning: false);
        }

        public void ApplyRouting(MouseOutputRoutingTable routing)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MouseOutputRoutingController));
            if (AppGlobal.appSettings == null) throw new InvalidOperationException("App settings are not loaded.");

            MouseOutputRoutingTable candidate =
                MouseOutputRoutingRuntimeSnapshot.CloneRouting(routing);
            ValidateRouting(candidate);

            AppGlobal.appSettings.MouseOutputRouting = candidate;
            AppGlobal.SaveAppSettings();
            runtime?.RefreshRouting(flushSharedFakerInput: false);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DetachRuntime();
        }

        private static void ValidateRouting(MouseOutputRoutingTable routing)
        {
            foreach (MouseOutputRoute route in Enum.GetValues(typeof(MouseOutputRoute)))
            {
                MouseOutputDestination destination = routing.GetRouteDestination(route);
                if (!MouseOutputRoutingPolicy.IsDestinationEligible(route, destination,
                    viiperAbsoluteMouseSupported: false))
                {
                    throw new InvalidOperationException(
                        $"Destination {destination} is not valid for route {route}.");
                }
            }
        }

        private void Runtime_StateChanged(object sender, EventArgs e) =>
            StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
