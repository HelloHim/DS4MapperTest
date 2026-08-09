using System;
using System.Collections.Generic;

namespace DS4MapperTest
{
    public enum MouseOutputRoute
    {
        Gyro = 0,
        JoystickMouse = 1,
        FlickStick = 2,
        Trackpad = 3,
        TriggerMouse = 4,
        Other = 5,
        AbsoluteMouse = 6,
        UnifiedVirtualMouse = 7,
    }

    public enum MouseOutputDestination
    {
        SendInput = 0,
        FakerInputMouse = 1,
        ViiperMouse1 = 2,
        ViiperMouse2 = 3,
        ViiperMouse3 = 4,
    }

    public enum MouseOutputFallbackReason
    {
        None = 0,
        ConfiguredDestinationUnavailable = 1,
    }

    public sealed class MouseOutputRoutingTable
    {
        public MouseOutputDestination Gyro { get; set; } = MouseOutputDestination.FakerInputMouse;
        public MouseOutputDestination JoystickMouse { get; set; } = MouseOutputDestination.FakerInputMouse;
        public MouseOutputDestination FlickStick { get; set; } = MouseOutputDestination.FakerInputMouse;
        public MouseOutputDestination Trackpad { get; set; } = MouseOutputDestination.FakerInputMouse;
        public MouseOutputDestination TriggerMouse { get; set; } = MouseOutputDestination.FakerInputMouse;
        public MouseOutputDestination Other { get; set; } = MouseOutputDestination.FakerInputMouse;
        public MouseOutputDestination AbsoluteMouse { get; set; } = MouseOutputDestination.FakerInputMouse;
        public MouseOutputDestination UnifiedVirtualMouse { get; set; } = MouseOutputDestination.FakerInputMouse;

        public MouseOutputDestination GetRouteDestination(MouseOutputRoute route)
        {
            return route switch
            {
                MouseOutputRoute.Gyro => Gyro,
                MouseOutputRoute.JoystickMouse => JoystickMouse,
                MouseOutputRoute.FlickStick => FlickStick,
                MouseOutputRoute.Trackpad => Trackpad,
                MouseOutputRoute.TriggerMouse => TriggerMouse,
                MouseOutputRoute.Other => Other,
                MouseOutputRoute.AbsoluteMouse => AbsoluteMouse,
                MouseOutputRoute.UnifiedVirtualMouse => UnifiedVirtualMouse,
                _ => MouseOutputDestination.FakerInputMouse,
            };
        }

        public void SetRouteDestination(MouseOutputRoute route, MouseOutputDestination destination)
        {
            switch (route)
            {
                case MouseOutputRoute.Gyro:
                    Gyro = destination;
                    break;
                case MouseOutputRoute.JoystickMouse:
                    JoystickMouse = destination;
                    break;
                case MouseOutputRoute.FlickStick:
                    FlickStick = destination;
                    break;
                case MouseOutputRoute.Trackpad:
                    Trackpad = destination;
                    break;
                case MouseOutputRoute.TriggerMouse:
                    TriggerMouse = destination;
                    break;
                case MouseOutputRoute.Other:
                    Other = destination;
                    break;
                case MouseOutputRoute.AbsoluteMouse:
                    AbsoluteMouse = destination;
                    break;
                case MouseOutputRoute.UnifiedVirtualMouse:
                    UnifiedVirtualMouse = destination;
                    break;
            }
        }
    }

    public sealed class MouseOutputRoutingAvailabilitySnapshot
    {
        public MouseOutputRoutingAvailabilitySnapshot(
            bool sendInputAvailable = true,
            bool fakerInputMouseAvailable = false,
            bool viiperMouse1Available = false,
            bool viiperMouse2Available = false,
            bool viiperMouse3Available = false,
            bool viiperAbsoluteMouseSupported = false)
        {
            SendInputAvailable = sendInputAvailable;
            FakerInputMouseAvailable = fakerInputMouseAvailable;
            ViiperMouse1Available = viiperMouse1Available;
            ViiperMouse2Available = viiperMouse2Available;
            ViiperMouse3Available = viiperMouse3Available;
            ViiperAbsoluteMouseSupported = viiperAbsoluteMouseSupported;
        }

        public bool SendInputAvailable { get; }
        public bool FakerInputMouseAvailable { get; }
        public bool ViiperMouse1Available { get; }
        public bool ViiperMouse2Available { get; }
        public bool ViiperMouse3Available { get; }
        public bool ViiperAbsoluteMouseSupported { get; }

        public bool IsDestinationAvailable(MouseOutputDestination destination)
        {
            return destination switch
            {
                MouseOutputDestination.SendInput => SendInputAvailable,
                MouseOutputDestination.FakerInputMouse => FakerInputMouseAvailable,
                MouseOutputDestination.ViiperMouse1 => ViiperMouse1Available,
                MouseOutputDestination.ViiperMouse2 => ViiperMouse2Available,
                MouseOutputDestination.ViiperMouse3 => ViiperMouse3Available,
                _ => false,
            };
        }
    }

    public sealed class MouseOutputRouteResolution
    {
        public MouseOutputRouteResolution(MouseOutputRoute route,
            MouseOutputDestination configuredDestination,
            MouseOutputDestination activeDestination,
            MouseOutputFallbackReason fallbackReason)
        {
            Route = route;
            ConfiguredDestination = configuredDestination;
            ActiveDestination = activeDestination;
            FallbackReason = fallbackReason;
        }

        public MouseOutputRoute Route { get; }
        public MouseOutputDestination ConfiguredDestination { get; }
        public MouseOutputDestination ActiveDestination { get; }
        public MouseOutputFallbackReason FallbackReason { get; }
        public bool IsFallbackActive => FallbackReason != MouseOutputFallbackReason.None;
    }

    public static class MouseOutputRoutingPolicy
    {
        private static readonly MouseOutputDestination[] RelativeEligibleDestinations =
        {
            MouseOutputDestination.SendInput,
            MouseOutputDestination.FakerInputMouse,
            MouseOutputDestination.ViiperMouse1,
            MouseOutputDestination.ViiperMouse2,
            MouseOutputDestination.ViiperMouse3,
        };

        private static readonly MouseOutputDestination[] AbsoluteEligibleDestinations =
        {
            MouseOutputDestination.SendInput,
            MouseOutputDestination.FakerInputMouse,
        };

        public static IReadOnlyList<MouseOutputDestination> GetEligibleDestinations(
            MouseOutputRoute route, bool viiperAbsoluteMouseSupported)
        {
            if (route == MouseOutputRoute.AbsoluteMouse && !viiperAbsoluteMouseSupported)
            {
                return AbsoluteEligibleDestinations;
            }

            return RelativeEligibleDestinations;
        }

        public static bool IsDestinationEligible(MouseOutputRoute route,
            MouseOutputDestination destination, bool viiperAbsoluteMouseSupported)
        {
            IReadOnlyList<MouseOutputDestination> eligibleDestinations =
                GetEligibleDestinations(route, viiperAbsoluteMouseSupported);
            foreach (MouseOutputDestination eligibleDestination in eligibleDestinations)
            {
                if (eligibleDestination == destination)
                {
                    return true;
                }
            }

            return false;
        }

        public static MouseOutputDestination SanitizeConfiguredDestination(
            MouseOutputRoute route, MouseOutputDestination destination,
            bool viiperAbsoluteMouseSupported)
        {
            return IsDestinationEligible(route, destination, viiperAbsoluteMouseSupported)
                ? destination
                : MouseOutputDestination.FakerInputMouse;
        }

        public static string SerializeDestination(MouseOutputDestination destination)
        {
            return destination switch
            {
                MouseOutputDestination.SendInput => "SendInput",
                MouseOutputDestination.FakerInputMouse => "FakerInputMouse",
                MouseOutputDestination.ViiperMouse1 => "VIIPERMouse1",
                MouseOutputDestination.ViiperMouse2 => "VIIPERMouse2",
                MouseOutputDestination.ViiperMouse3 => "VIIPERMouse3",
                _ => "FakerInputMouse",
            };
        }

        public static bool TryParseSerializedDestination(string value,
            out MouseOutputDestination destination)
        {
            switch (value)
            {
                case "SendInput":
                    destination = MouseOutputDestination.SendInput;
                    return true;
                case "FakerInputMouse":
                    destination = MouseOutputDestination.FakerInputMouse;
                    return true;
                case "VIIPERMouse1":
                    destination = MouseOutputDestination.ViiperMouse1;
                    return true;
                case "VIIPERMouse2":
                    destination = MouseOutputDestination.ViiperMouse2;
                    return true;
                case "VIIPERMouse3":
                    destination = MouseOutputDestination.ViiperMouse3;
                    return true;
                default:
                    destination = MouseOutputDestination.FakerInputMouse;
                    return false;
            }
        }
    }

    public sealed class MouseOutputRoutingResolver
    {
        public MouseOutputRouteResolution Resolve(MouseOutputRoute route,
            MouseOutputDestination configuredDestination,
            MouseOutputRoutingAvailabilitySnapshot availability)
        {
            MouseOutputDestination sanitizedConfiguredDestination =
                MouseOutputRoutingPolicy.SanitizeConfiguredDestination(route,
                    configuredDestination, availability.ViiperAbsoluteMouseSupported);

            if (availability.IsDestinationAvailable(sanitizedConfiguredDestination))
            {
                return new MouseOutputRouteResolution(route,
                    sanitizedConfiguredDestination, sanitizedConfiguredDestination,
                    MouseOutputFallbackReason.None);
            }

            MouseOutputDestination activeDestination =
                ResolveFallback(route, sanitizedConfiguredDestination, availability);
            return new MouseOutputRouteResolution(route,
                sanitizedConfiguredDestination, activeDestination,
                MouseOutputFallbackReason.ConfiguredDestinationUnavailable);
        }

        private static MouseOutputDestination ResolveFallback(MouseOutputRoute route,
            MouseOutputDestination configuredDestination,
            MouseOutputRoutingAvailabilitySnapshot availability)
        {
            if (route == MouseOutputRoute.AbsoluteMouse &&
                !availability.ViiperAbsoluteMouseSupported)
            {
                return MouseOutputDestination.SendInput;
            }

            if (configuredDestination != MouseOutputDestination.SendInput &&
                availability.FakerInputMouseAvailable)
            {
                return MouseOutputDestination.FakerInputMouse;
            }

            MouseOutputDestination[] viiperOrder =
            {
                MouseOutputDestination.ViiperMouse1,
                MouseOutputDestination.ViiperMouse2,
                MouseOutputDestination.ViiperMouse3,
            };

            foreach (MouseOutputDestination destination in viiperOrder)
            {
                if (destination == configuredDestination)
                {
                    continue;
                }

                if (MouseOutputRoutingPolicy.IsDestinationEligible(route, destination,
                    availability.ViiperAbsoluteMouseSupported) &&
                    availability.IsDestinationAvailable(destination))
                {
                    return destination;
                }
            }

            return MouseOutputDestination.SendInput;
        }
    }
}
