using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace DS4MapperTest.ViewModels
{
    public sealed class MouseRoutingDestinationOptionViewModel : INotifyPropertyChanged
    {
        private bool available;

        public MouseRoutingDestinationOptionViewModel(MouseOutputDestination destination)
        {
            Destination = destination;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MouseOutputDestination Destination { get; }

        public bool Available
        {
            get => available;
            set
            {
                if (available == value) return;
                available = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Available)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailabilityText)));
            }
        }

        public string BaseDisplayName => GetDestinationDisplayName(Destination);
        public string AvailabilityText => Available ? "Available" : "Unavailable";
        public string DisplayName => Available
            ? BaseDisplayName
            : $"{BaseDisplayName} (unavailable)";

        public static string GetDestinationDisplayName(MouseOutputDestination destination)
        {
            return destination switch
            {
                MouseOutputDestination.FakerInputMouse => "FakerInput",
                MouseOutputDestination.ViiperMouse1 => "VIIPER Mouse 1",
                MouseOutputDestination.ViiperMouse2 => "VIIPER Mouse 2",
                MouseOutputDestination.ViiperMouse3 => "VIIPER Mouse 3",
                _ => "SendInput",
            };
        }
    }

    public sealed class MouseRoutingRouteRowViewModel : INotifyPropertyChanged
    {
        private readonly Action stagedChangedAction;
        private MouseOutputDestination stagedDestination;
        private MouseOutputDestination configuredDestination;
        private MouseOutputDestination activeDestination;
        private MouseOutputFallbackReason fallbackReason;
        private bool serviceRunning;

        public MouseRoutingRouteRowViewModel(MouseOutputRoute route,
            IEnumerable<MouseOutputDestination> destinations, Action stagedChanged)
        {
            Route = route;
            stagedChangedAction = stagedChanged;
            DestinationOptions = new ObservableCollection<MouseRoutingDestinationOptionViewModel>(
                destinations.Select(destination =>
                    new MouseRoutingDestinationOptionViewModel(destination)));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MouseOutputRoute Route { get; }
        public string RouteLabel => GetRouteDisplayName(Route);
        public bool IsGyroRoute => Route == MouseOutputRoute.Gyro;
        public bool IsAbsoluteMouseRoute => Route == MouseOutputRoute.AbsoluteMouse;
        public bool IsOtherRoute => Route == MouseOutputRoute.Other;
        public ObservableCollection<MouseRoutingDestinationOptionViewModel> DestinationOptions { get; }

        public MouseOutputDestination StagedDestination
        {
            get => stagedDestination;
            set
            {
                if (stagedDestination == value) return;
                stagedDestination = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StagedDestination)));
                stagedChangedAction?.Invoke();
            }
        }

        public MouseOutputDestination ConfiguredDestination
        {
            get => configuredDestination;
            private set
            {
                if (configuredDestination == value) return;
                configuredDestination = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConfiguredDestination)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConfiguredDestinationText)));
            }
        }

        public MouseOutputDestination ActiveDestination
        {
            get => activeDestination;
            private set
            {
                if (activeDestination == value) return;
                activeDestination = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveDestination)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveDestinationText)));
            }
        }

        public MouseOutputFallbackReason FallbackReason
        {
            get => fallbackReason;
            private set
            {
                if (fallbackReason == value) return;
                fallbackReason = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FallbackReason)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFallbackActive)));
            }
        }

        public bool ServiceRunning
        {
            get => serviceRunning;
            private set
            {
                if (serviceRunning == value) return;
                serviceRunning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ServiceRunning)));
            }
        }

        public bool IsFallbackActive => FallbackReason != MouseOutputFallbackReason.None;
        public string ConfiguredDestinationText =>
            MouseRoutingDestinationOptionViewModel.GetDestinationDisplayName(ConfiguredDestination);
        public string ActiveDestinationText =>
            MouseRoutingDestinationOptionViewModel.GetDestinationDisplayName(ActiveDestination);

        public string StatusText
        {
            get
            {
                if (!ServiceRunning)
                {
                    return "Mapping service stopped; the configured route will activate when the service is running.";
                }

                if (!IsFallbackActive)
                {
                    return "Configured destination is active.";
                }

                return $"{ConfiguredDestinationText} unavailable; temporary fallback active. The configured route will restore automatically after recovery.";
            }
        }

        public void UpdateRuntime(MouseOutputRouteResolution resolution,
            MouseOutputRoutingAvailabilitySnapshot availability, bool isServiceRunning)
        {
            ServiceRunning = isServiceRunning;
            ConfiguredDestination = resolution.ConfiguredDestination;
            ActiveDestination = resolution.ActiveDestination;
            FallbackReason = resolution.FallbackReason;

            foreach (MouseRoutingDestinationOptionViewModel option in DestinationOptions)
            {
                option.Available = availability.IsDestinationUsable(option.Destination);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }

        private static string GetRouteDisplayName(MouseOutputRoute route)
        {
            return route switch
            {
                MouseOutputRoute.Gyro => "Gyro Mouse",
                MouseOutputRoute.JoystickMouse => "Joystick Mouse",
                MouseOutputRoute.FlickStick => "Flick Stick",
                MouseOutputRoute.Trackpad => "Trackpad Relative Mouse",
                MouseOutputRoute.UnifiedVirtualMouse => "Physical Mouse Forwarding",
                MouseOutputRoute.TriggerMouse => "Trigger Mouse",
                MouseOutputRoute.Other => "Flick Turn Binds",
                MouseOutputRoute.AbsoluteMouse => "Absolute Mouse",
                _ => route.ToString(),
            };
        }
    }

    public sealed class MouseRoutingBulkApplyOptionViewModel
    {
        public MouseRoutingBulkApplyOptionViewModel(MouseOutputDestination destination,
            int compatibleRouteCount)
        {
            Destination = destination;
            CompatibleRouteCount = compatibleRouteCount;
        }

        public MouseOutputDestination Destination { get; }
        public int CompatibleRouteCount { get; }
        public string DisplayName =>
            MouseRoutingDestinationOptionViewModel.GetDestinationDisplayName(Destination);
        public string ScopeText => CompatibleRouteCount == 7
            ? "All routes"
            : "7 compatible routes";
    }

    public sealed class MouseRoutingPanelViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly MouseOutputRoute[] RouteDisplayOrder =
        {
            MouseOutputRoute.Gyro,
            MouseOutputRoute.JoystickMouse,
            MouseOutputRoute.FlickStick,
            MouseOutputRoute.Trackpad,
            MouseOutputRoute.AbsoluteMouse,
            MouseOutputRoute.Other,
            MouseOutputRoute.TriggerMouse,
            MouseOutputRoute.UnifiedVirtualMouse,
        };

        private readonly IMouseOutputRoutingService routingService;
        private readonly Action<Action> uiInvoker;
        private bool popupOpen;
        private bool hasStagedChanges;
        private bool applyingChanges;
        private string validationMessage = string.Empty;
        private MouseRoutingBulkApplyOptionViewModel selectedBulkApplyOption;
        private bool disposed;

        public MouseRoutingPanelViewModel(IMouseOutputRoutingService routingService,
            Action<Action> uiInvoker = null)
        {
            this.routingService = routingService ??
                throw new ArgumentNullException(nameof(routingService));
            this.uiInvoker = uiInvoker ?? (action => action());
            RouteRows = new ObservableCollection<MouseRoutingRouteRowViewModel>(
                RouteDisplayOrder.Select(CreateRouteRow));
            BulkApplyOptions = new ObservableCollection<MouseRoutingBulkApplyOptionViewModel>(
                Enum.GetValues(typeof(MouseOutputDestination))
                    .Cast<MouseOutputDestination>()
                    .Select(destination => new MouseRoutingBulkApplyOptionViewModel(destination,
                        CountCompatibleRoutes(destination))));

            routingService.StateChanged += RoutingService_StateChanged;
            RefreshRuntimeState();
            LoadStagedSelectionsFromConfigured();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<MouseRoutingRouteRowViewModel> RouteRows { get; }
        public ObservableCollection<MouseRoutingBulkApplyOptionViewModel> BulkApplyOptions { get; }

        public bool PopupOpen
        {
            get => popupOpen;
            set
            {
                if (popupOpen == value) return;
                popupOpen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PopupOpen)));
                if (popupOpen)
                {
                    BeginEditSession();
                }
                else if (!applyingChanges)
                {
                    DiscardStagedChanges();
                }
            }
        }

        public bool HasStagedChanges
        {
            get => hasStagedChanges;
            private set
            {
                if (hasStagedChanges == value) return;
                hasStagedChanges = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStagedChanges)));
            }
        }

        public string ValidationMessage
        {
            get => validationMessage;
            private set
            {
                if (validationMessage == value) return;
                validationMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationMessage)));
            }
        }

        public MouseRoutingBulkApplyOptionViewModel SelectedBulkApplyOption
        {
            get => selectedBulkApplyOption;
            set
            {
                if (ReferenceEquals(selectedBulkApplyOption, value))
                {
                    return;
                }

                selectedBulkApplyOption = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(SelectedBulkApplyOption)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(HasSelectedBulkApplyOption)));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(BulkApplyHelperText)));
            }
        }

        public bool HasSelectedBulkApplyOption => SelectedBulkApplyOption != null;
        public string BulkApplyHelperText => SelectedBulkApplyOption == null
            ? "Choose a destination to update every compatible staged route at once."
            : SelectedBulkApplyOption.CompatibleRouteCount == RouteRows.Count
                ? "Apply to all eight staged routes in one action."
                : "Apply to seven staged routes in one action. VIIPER does not work for Absolute Mouse.";

        public void BeginEditSession()
        {
            LoadStagedSelectionsFromConfigured();
            SelectedBulkApplyOption = null;
            ValidationMessage = string.Empty;
        }

        public bool Apply()
        {
            MouseOutputRoutingTable stagedRouting = BuildStagedRouting();
            foreach (MouseOutputRoute route in Enum.GetValues(typeof(MouseOutputRoute)))
            {
                MouseOutputDestination destination =
                    stagedRouting.GetRouteDestination(route);
                if (!MouseOutputRoutingPolicy.IsDestinationEligible(route, destination,
                    viiperAbsoluteMouseSupported: false))
                {
                    ValidationMessage =
                        $"{GetRouteDisplayName(route)} cannot use {GetDestinationDisplayName(destination)}.";
                    return false;
                }
            }

            applyingChanges = true;
            try
            {
                routingService.ApplyRouting(stagedRouting);
                RefreshRuntimeState();
                LoadStagedSelectionsFromConfigured();
                ValidationMessage = string.Empty;
                PopupOpen = false;
                return true;
            }
            finally
            {
                applyingChanges = false;
            }
        }

        public void DiscardStagedChanges()
        {
            LoadStagedSelectionsFromConfigured();
            SelectedBulkApplyOption = null;
            ValidationMessage = string.Empty;
        }

        public void ApplySelectedDestinationToCompatibleRoutes()
        {
            if (SelectedBulkApplyOption == null)
            {
                return;
            }

            MouseOutputDestination destination = SelectedBulkApplyOption.Destination;
            foreach (MouseRoutingRouteRowViewModel row in RouteRows)
            {
                if (row.DestinationOptions.Any(option => option.Destination == destination))
                {
                    row.StagedDestination = destination;
                }
            }
        }

        public void RefreshRuntimeState()
        {
            MouseOutputRoutingRuntimeSnapshot snapshot = routingService.GetSnapshot();
            foreach (MouseRoutingRouteRowViewModel row in RouteRows)
            {
                row.UpdateRuntime(snapshot.GetResolution(row.Route), snapshot.Availability,
                    snapshot.ServiceRunning);
            }

            UpdateHasStagedChanges(snapshot.ConfiguredRouting);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            routingService.StateChanged -= RoutingService_StateChanged;
        }

        private MouseRoutingRouteRowViewModel CreateRouteRow(MouseOutputRoute route)
        {
            IReadOnlyList<MouseOutputDestination> destinations =
                MouseOutputRoutingPolicy.GetEligibleDestinations(route,
                    viiperAbsoluteMouseSupported: false);
            return new MouseRoutingRouteRowViewModel(route, destinations, () =>
            {
                ValidationMessage = string.Empty;
                UpdateHasStagedChanges(routingService.GetSnapshot().ConfiguredRouting);
            });
        }

        private void LoadStagedSelectionsFromConfigured()
        {
            MouseOutputRoutingTable configuredRouting = routingService.GetSnapshot().ConfiguredRouting;
            foreach (MouseRoutingRouteRowViewModel row in RouteRows)
            {
                row.StagedDestination = configuredRouting.GetRouteDestination(row.Route);
            }

            UpdateHasStagedChanges(configuredRouting);
        }

        private MouseOutputRoutingTable BuildStagedRouting()
        {
            MouseOutputRoutingTable table = new MouseOutputRoutingTable();
            foreach (MouseRoutingRouteRowViewModel row in RouteRows)
            {
                table.SetRouteDestination(row.Route, row.StagedDestination);
            }

            return table;
        }

        private void UpdateHasStagedChanges(MouseOutputRoutingTable configuredRouting)
        {
            HasStagedChanges = RouteRows.Any(row =>
                row.StagedDestination != configuredRouting.GetRouteDestination(row.Route));
        }

        private void RoutingService_StateChanged(object sender, EventArgs e) =>
            uiInvoker(RefreshRuntimeState);

        private int CountCompatibleRoutes(MouseOutputDestination destination)
        {
            return RouteRows.Count(row =>
                row.DestinationOptions.Any(option => option.Destination == destination));
        }

        private static string GetRouteDisplayName(MouseOutputRoute route)
        {
            return route switch
            {
                MouseOutputRoute.Gyro => "Gyro Mouse",
                MouseOutputRoute.JoystickMouse => "Joystick Mouse",
                MouseOutputRoute.FlickStick => "Flick Stick",
                MouseOutputRoute.Trackpad => "Trackpad Relative Mouse",
                MouseOutputRoute.UnifiedVirtualMouse => "Physical Mouse Forwarding",
                MouseOutputRoute.TriggerMouse => "Trigger Mouse",
                MouseOutputRoute.Other => "Flick Turn Binds",
                MouseOutputRoute.AbsoluteMouse => "Absolute Mouse",
                _ => route.ToString(),
            };
        }

        private static string GetDestinationDisplayName(MouseOutputDestination destination)
        {
            return destination switch
            {
                MouseOutputDestination.FakerInputMouse => "FakerInput",
                MouseOutputDestination.ViiperMouse1 => "VIIPER Mouse 1",
                MouseOutputDestination.ViiperMouse2 => "VIIPER Mouse 2",
                MouseOutputDestination.ViiperMouse3 => "VIIPER Mouse 3",
                _ => "SendInput",
            };
        }
    }
}
