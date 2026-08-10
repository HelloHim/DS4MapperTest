using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DS4MapperTest;
using DS4MapperTest.ViewModels;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class MouseOutputRoutingUiTests
    {
        private sealed class FakeRoutingService : IMouseOutputRoutingService
        {
            private EventHandler stateChanged;
            private MouseOutputRoutingRuntimeSnapshot snapshot;

            public FakeRoutingService(MouseOutputRoutingRuntimeSnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public int SubscriptionCount { get; private set; }
            public int ApplyCount { get; private set; }
            public MouseOutputRoutingTable LastAppliedRouting { get; private set; }

            public event EventHandler StateChanged
            {
                add
                {
                    stateChanged += value;
                    SubscriptionCount++;
                }
                remove
                {
                    stateChanged -= value;
                    SubscriptionCount--;
                }
            }

            public MouseOutputRoutingRuntimeSnapshot GetSnapshot() => snapshot;

            public void ApplyRouting(MouseOutputRoutingTable routing)
            {
                ApplyCount++;
                LastAppliedRouting = MouseOutputRoutingRuntimeSnapshot.CloneRouting(routing);
                snapshot = new MouseOutputRoutingRuntimeSnapshot(LastAppliedRouting,
                    snapshot.Availability,
                    CreateConfiguredResolutions(LastAppliedRouting),
                    snapshot.ServiceRunning);
            }

            public void UpdateSnapshot(MouseOutputRoutingRuntimeSnapshot snapshot)
            {
                this.snapshot = snapshot;
                stateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class FakeRoutingRuntime : IMouseOutputRoutingRuntime
        {
            private EventHandler stateChanged;
            private MouseOutputRoutingAvailabilitySnapshot availability;
            private IReadOnlyList<MouseOutputRouteResolution> resolutions;

            public int RefreshCount { get; private set; }
            public bool? LastFlushSharedFakerInput { get; private set; }

            public event EventHandler StateChanged
            {
                add => stateChanged += value;
                remove => stateChanged -= value;
            }

            public MouseOutputRoutingAvailabilitySnapshot GetAvailabilitySnapshot() =>
                availability;

            public IReadOnlyList<MouseOutputRouteResolution> GetRouteResolutions() =>
                resolutions;

            public void RefreshRouting(bool flushSharedFakerInput)
            {
                RefreshCount++;
                LastFlushSharedFakerInput = flushSharedFakerInput;
            }

            public void SetSnapshot(MouseOutputRoutingAvailabilitySnapshot availability,
                IReadOnlyList<MouseOutputRouteResolution> resolutions)
            {
                this.availability = availability;
                this.resolutions = resolutions;
                stateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static readonly MouseOutputRoute[] RelativeRoutes =
        {
            MouseOutputRoute.Gyro,
            MouseOutputRoute.JoystickMouse,
            MouseOutputRoute.FlickStick,
            MouseOutputRoute.Trackpad,
            MouseOutputRoute.UnifiedVirtualMouse,
            MouseOutputRoute.TriggerMouse,
            MouseOutputRoute.Other,
        };

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "DS4MapperUnitTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static MouseOutputRoutingTable CreateConfiguredRouting() =>
            new MouseOutputRoutingTable
            {
                Gyro = MouseOutputDestination.FakerInputMouse,
                JoystickMouse = MouseOutputDestination.ViiperMouse1,
                FlickStick = MouseOutputDestination.ViiperMouse2,
                Trackpad = MouseOutputDestination.ViiperMouse3,
                UnifiedVirtualMouse = MouseOutputDestination.SendInput,
                TriggerMouse = MouseOutputDestination.SendInput,
                Other = MouseOutputDestination.FakerInputMouse,
                AbsoluteMouse = MouseOutputDestination.SendInput,
            };

        private static IReadOnlyList<MouseOutputRouteResolution> CreateConfiguredResolutions(
            MouseOutputRoutingTable configuredRouting)
        {
            return Enum.GetValues(typeof(MouseOutputRoute))
                .Cast<MouseOutputRoute>()
                .Select(route =>
                {
                    MouseOutputDestination destination =
                        configuredRouting.GetRouteDestination(route);
                    return new MouseOutputRouteResolution(route, destination, destination,
                        MouseOutputFallbackReason.None);
                })
                .ToArray();
        }

        private static MouseOutputRoutingRuntimeSnapshot CreateSnapshot(
            MouseOutputRoutingTable configuredRouting = null,
            MouseOutputRoutingAvailabilitySnapshot availability = null,
            IReadOnlyList<MouseOutputRouteResolution> resolutions = null,
            bool serviceRunning = true)
        {
            configuredRouting ??= CreateConfiguredRouting();
            availability ??= new MouseOutputRoutingAvailabilitySnapshot(
                sendInputAvailable: true,
                fakerInputMouseAvailable: true,
                viiperMouse1Available: true,
                viiperMouse2Available: true,
                viiperMouse3Available: true);
            resolutions ??= CreateConfiguredResolutions(configuredRouting);
            return new MouseOutputRoutingRuntimeSnapshot(configuredRouting, availability,
                resolutions, serviceRunning);
        }

        [TestMethod]
        public void PanelExposesAllRoutesAndEligibleDestinations()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            Assert.AreEqual(8, viewModel.RouteRows.Count);

            foreach (MouseOutputRoute route in RelativeRoutes)
            {
                MouseRoutingRouteRowViewModel row =
                    viewModel.RouteRows.Single(item => item.Route == route);
                CollectionAssert.AreEqual(new[]
                {
                    MouseOutputDestination.SendInput,
                    MouseOutputDestination.FakerInputMouse,
                    MouseOutputDestination.ViiperMouse1,
                    MouseOutputDestination.ViiperMouse2,
                    MouseOutputDestination.ViiperMouse3,
                }, row.DestinationOptions.Select(item => item.Destination).ToArray());
            }

            MouseRoutingRouteRowViewModel absoluteRow =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.AbsoluteMouse);
            CollectionAssert.AreEqual(new[]
            {
                MouseOutputDestination.SendInput,
                MouseOutputDestination.FakerInputMouse,
            }, absoluteRow.DestinationOptions.Select(item => item.Destination).ToArray());
        }

        [TestMethod]
        public void AbsoluteMouseRejectsViiperSelectionInUnderlyingUiModel()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel absoluteRow =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.AbsoluteMouse);
            absoluteRow.StagedDestination = MouseOutputDestination.ViiperMouse1;

            Assert.IsFalse(viewModel.Apply());
            StringAssert.Contains(viewModel.ValidationMessage, "Absolute Mouse");
            Assert.AreEqual(0, service.ApplyCount);
        }

        [TestMethod]
        public void OpeningPanelCopiesConfiguredValuesIntoStagedState()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            viewModel.PopupOpen = true;

            foreach (MouseRoutingRouteRowViewModel row in viewModel.RouteRows)
            {
                Assert.AreEqual(service.GetSnapshot().ConfiguredRouting.GetRouteDestination(row.Route),
                    row.StagedDestination, row.Route.ToString());
            }
        }

        [TestMethod]
        public void EditingStagedValueDoesNotMutateConfiguredStateOrPersistedApplyState()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel gyroRow =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.Gyro);
            gyroRow.StagedDestination = MouseOutputDestination.SendInput;

            Assert.AreEqual(MouseOutputDestination.FakerInputMouse,
                service.GetSnapshot().ConfiguredRouting.Gyro);
            Assert.AreEqual(0, service.ApplyCount);
            Assert.IsTrue(viewModel.HasStagedChanges);
        }

        [TestMethod]
        public void QuickSetAppliesSupportedDestinationToAllRoutes()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            viewModel.PopupOpen = true;
            viewModel.SelectedBulkApplyOption = viewModel.BulkApplyOptions.Single(option =>
                option.Destination == MouseOutputDestination.FakerInputMouse);

            viewModel.ApplySelectedDestinationToCompatibleRoutes();

            foreach (MouseRoutingRouteRowViewModel row in viewModel.RouteRows)
            {
                Assert.AreEqual(MouseOutputDestination.FakerInputMouse,
                    row.StagedDestination, row.Route.ToString());
            }

            Assert.AreEqual(0, service.ApplyCount);
            Assert.IsTrue(viewModel.HasStagedChanges);
        }

        [TestMethod]
        public void QuickSetLeavesAbsoluteMouseUnchangedForViiperDestinations()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            viewModel.PopupOpen = true;
            viewModel.SelectedBulkApplyOption = viewModel.BulkApplyOptions.Single(option =>
                option.Destination == MouseOutputDestination.ViiperMouse2);

            viewModel.ApplySelectedDestinationToCompatibleRoutes();

            foreach (MouseOutputRoute route in RelativeRoutes)
            {
                Assert.AreEqual(MouseOutputDestination.ViiperMouse2,
                    viewModel.RouteRows.Single(row => row.Route == route).StagedDestination,
                    route.ToString());
            }

            Assert.AreEqual(MouseOutputDestination.SendInput,
                viewModel.RouteRows.Single(row => row.Route == MouseOutputRoute.AbsoluteMouse)
                    .StagedDestination);
            Assert.AreEqual(0, service.ApplyCount);
            Assert.IsTrue(viewModel.HasStagedChanges);
        }

        [TestMethod]
        public void ApplyCommitsAllValidStagedValuesAndPreservesUnchangedRoutes()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            viewModel.PopupOpen = true;
            viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.Gyro)
                .StagedDestination = MouseOutputDestination.SendInput;
            viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.Other)
                .StagedDestination = MouseOutputDestination.ViiperMouse3;

            Assert.IsTrue(viewModel.Apply());
            Assert.AreEqual(1, service.ApplyCount);
            Assert.AreEqual(MouseOutputDestination.SendInput,
                service.LastAppliedRouting.Gyro);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse3,
                service.LastAppliedRouting.Other);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse1,
                service.LastAppliedRouting.JoystickMouse);
            Assert.IsFalse(viewModel.HasStagedChanges);
        }

        [TestMethod]
        public void DiscardRestoresConfiguredValuesWithoutApply()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel row =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.Trackpad);
            row.StagedDestination = MouseOutputDestination.SendInput;

            viewModel.DiscardStagedChanges();

            Assert.AreEqual(MouseOutputDestination.ViiperMouse3, row.StagedDestination);
            Assert.AreEqual(0, service.ApplyCount);
        }

        [TestMethod]
        public void ClosingWithoutApplyDiscardsStagedChanges()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            viewModel.PopupOpen = true;
            viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.TriggerMouse)
                .StagedDestination = MouseOutputDestination.FakerInputMouse;
            viewModel.PopupOpen = false;

            Assert.AreEqual(MouseOutputDestination.SendInput,
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.TriggerMouse)
                    .StagedDestination);
            Assert.AreEqual(0, service.ApplyCount);
        }

        [TestMethod]
        public void ConfiguredAndActiveDisplayMatchDuringNormalOperation()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel row =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.Gyro);
            Assert.AreEqual(row.ConfiguredDestinationText, row.ActiveDestinationText);
            Assert.AreEqual("Configured destination is active.", row.StatusText);
        }

        [TestMethod]
        public void ConfiguredAndActiveDisplaySeparatelyDuringFallback()
        {
            MouseOutputRoutingTable configuredRouting = CreateConfiguredRouting();
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot(
                configuredRouting,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: false,
                    viiperMouse2Available: true,
                    viiperMouse3Available: true),
                new[]
                {
                    new MouseOutputRouteResolution(MouseOutputRoute.Gyro,
                        MouseOutputDestination.FakerInputMouse,
                        MouseOutputDestination.FakerInputMouse,
                        MouseOutputFallbackReason.None),
                    new MouseOutputRouteResolution(MouseOutputRoute.JoystickMouse,
                        MouseOutputDestination.ViiperMouse1,
                        MouseOutputDestination.FakerInputMouse,
                        MouseOutputFallbackReason.ConfiguredDestinationUnavailable),
                    new MouseOutputRouteResolution(MouseOutputRoute.FlickStick,
                        MouseOutputDestination.ViiperMouse2,
                        MouseOutputDestination.ViiperMouse2,
                        MouseOutputFallbackReason.None),
                    new MouseOutputRouteResolution(MouseOutputRoute.Trackpad,
                        MouseOutputDestination.ViiperMouse3,
                        MouseOutputDestination.ViiperMouse3,
                        MouseOutputFallbackReason.None),
                    new MouseOutputRouteResolution(MouseOutputRoute.TriggerMouse,
                        MouseOutputDestination.SendInput,
                        MouseOutputDestination.SendInput,
                        MouseOutputFallbackReason.None),
                    new MouseOutputRouteResolution(MouseOutputRoute.Other,
                        MouseOutputDestination.FakerInputMouse,
                        MouseOutputDestination.FakerInputMouse,
                        MouseOutputFallbackReason.None),
                    new MouseOutputRouteResolution(MouseOutputRoute.AbsoluteMouse,
                        MouseOutputDestination.SendInput,
                        MouseOutputDestination.SendInput,
                        MouseOutputFallbackReason.None),
                }));
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel row =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.JoystickMouse);

            Assert.AreEqual("VIIPER Mouse 1", row.ConfiguredDestinationText);
            Assert.AreEqual("FakerInput", row.ActiveDestinationText);
            StringAssert.Contains(row.StatusText, "temporary fallback active");
            StringAssert.Contains(row.StatusText, "restore automatically");
        }

        [TestMethod]
        public void ViiperAvailabilityIsTrackedIndependentlyAndRecoveryUpdatesActiveDestination()
        {
            MouseOutputRoutingTable configuredRouting = CreateConfiguredRouting();
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot(
                configuredRouting,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: false,
                    viiperMouse2Available: true,
                    viiperMouse3Available: true),
                CreateConfiguredResolutions(configuredRouting).Select(item =>
                    item.Route == MouseOutputRoute.JoystickMouse
                        ? new MouseOutputRouteResolution(item.Route, item.ConfiguredDestination,
                            MouseOutputDestination.FakerInputMouse,
                            MouseOutputFallbackReason.ConfiguredDestinationUnavailable)
                        : item).ToArray()));
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel row =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.JoystickMouse);
            Assert.IsFalse(row.DestinationOptions.Single(item =>
                item.Destination == MouseOutputDestination.ViiperMouse1).Available);
            Assert.IsTrue(row.DestinationOptions.Single(item =>
                item.Destination == MouseOutputDestination.ViiperMouse2).Available);
            Assert.AreEqual("FakerInput", row.ActiveDestinationText);

            service.UpdateSnapshot(CreateSnapshot(configuredRouting,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: true,
                    viiperMouse2Available: true,
                    viiperMouse3Available: true),
                CreateConfiguredResolutions(configuredRouting)));

            Assert.AreEqual("VIIPER Mouse 1", row.ActiveDestinationText);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse1,
                service.GetSnapshot().ConfiguredRouting.JoystickMouse);
        }

        [TestMethod]
        public void UncreatedViiperMouseShowsAsAvailableWhenDriverIsPresent()
        {
            MouseOutputRoutingTable configuredRouting = CreateConfiguredRouting();
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot(
                configuredRouting,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: true,
                    viiperMouse2Available: false,
                    viiperMouse3Available: false,
                    viiperDriverAvailable: true)));
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel row =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.Gyro);

            MouseRoutingDestinationOptionViewModel uncreatedMouse = row.DestinationOptions.Single(
                item => item.Destination == MouseOutputDestination.ViiperMouse2);
            Assert.IsTrue(uncreatedMouse.Available);
            Assert.AreEqual("Available", uncreatedMouse.AvailabilityText);
            Assert.AreEqual("VIIPER Mouse 2", uncreatedMouse.DisplayName);
        }

        [TestMethod]
        public void ViiperMouseShowsAsUnavailableWhenDriverIsMissing()
        {
            MouseOutputRoutingTable configuredRouting = CreateConfiguredRouting();
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot(
                configuredRouting,
                new MouseOutputRoutingAvailabilitySnapshot(
                    sendInputAvailable: true,
                    fakerInputMouseAvailable: true,
                    viiperMouse1Available: false,
                    viiperMouse2Available: false,
                    viiperMouse3Available: false,
                    viiperDriverAvailable: false)));
            using MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            MouseRoutingRouteRowViewModel row =
                viewModel.RouteRows.Single(item => item.Route == MouseOutputRoute.Gyro);

            MouseRoutingDestinationOptionViewModel unavailableMouse = row.DestinationOptions.Single(
                item => item.Destination == MouseOutputDestination.ViiperMouse2);
            Assert.IsFalse(unavailableMouse.Available);
            Assert.AreEqual("Unavailable", unavailableMouse.AvailabilityText);
            Assert.AreEqual("VIIPER Mouse 2 (unavailable)", unavailableMouse.DisplayName);
        }

        [TestMethod]
        public void DisposingPanelViewModelUnsubscribesFromRoutingService()
        {
            FakeRoutingService service = new FakeRoutingService(CreateSnapshot());
            MouseRoutingPanelViewModel viewModel = new MouseRoutingPanelViewModel(service);

            Assert.AreEqual(1, service.SubscriptionCount);
            viewModel.Dispose();
            Assert.AreEqual(0, service.SubscriptionCount);
        }

        [TestMethod]
        public void ControllerApplyPersistsRoutingAndLeavesUnrelatedSettingsUnchanged()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                string configPath = Path.Combine(tempDir, "Settings.json");
                JObject originalJson = new JObject
                {
                    ["AppVersion"] = "0.0.34",
                    ["ConfigVersion"] = 0,
                    ["ThemeMode"] = "Dark",
                    ["PhysicalMouseForwardingEnabled"] = true,
                    ["SelectedPhysicalMouseId"] = "mouse-id",
                    ["GyroMouseDestination"] = "FakerInputMouse",
                    ["JoystickMouseDestination"] = "FakerInputMouse",
                    ["FlickStickMouseDestination"] = "FakerInputMouse",
                    ["TrackpadMouseDestination"] = "FakerInputMouse",
                    ["UnifiedVirtualMouseDestination"] = "FakerInputMouse",
                    ["TriggerMouseDestination"] = "FakerInputMouse",
                    ["OtherMouseDestination"] = "FakerInputMouse",
                    ["AbsoluteMouseDestination"] = "FakerInputMouse",
                };
                File.WriteAllText(configPath, originalJson.ToString());

                AppSettingsStore store = new AppSettingsStore(configPath);
                store.LoadConfig();
                AppGlobalData appGlobal = new AppGlobalData
                {
                    appSettings = store,
                    fakerInputInstalled = true,
                };
                FakeRoutingRuntime runtime = new FakeRoutingRuntime();
                runtime.SetSnapshot(
                    new MouseOutputRoutingAvailabilitySnapshot(
                        sendInputAvailable: true,
                        fakerInputMouseAvailable: true,
                        viiperMouse1Available: true,
                        viiperMouse2Available: true,
                        viiperMouse3Available: true),
                    CreateConfiguredResolutions(store.MouseOutputRouting));

                using MouseOutputRoutingController controller =
                    new MouseOutputRoutingController(appGlobal);
                controller.AttachRuntime(runtime, isServiceRunning: true);

                controller.ApplyRouting(new MouseOutputRoutingTable
                {
                    Gyro = MouseOutputDestination.SendInput,
                    JoystickMouse = MouseOutputDestination.ViiperMouse1,
                    FlickStick = MouseOutputDestination.ViiperMouse2,
                    Trackpad = MouseOutputDestination.ViiperMouse3,
                    UnifiedVirtualMouse = MouseOutputDestination.ViiperMouse1,
                    TriggerMouse = MouseOutputDestination.FakerInputMouse,
                    Other = MouseOutputDestination.SendInput,
                    AbsoluteMouse = MouseOutputDestination.SendInput,
                });

                AppSettingsStore loaded = new AppSettingsStore(configPath);
                loaded.LoadConfig();

                Assert.AreEqual("Dark", loaded.ThemeMode);
                Assert.IsTrue(loaded.PhysicalMouseForwardingEnabled);
                Assert.AreEqual("mouse-id", loaded.SelectedPhysicalMouseId);
                Assert.AreEqual(MouseOutputDestination.SendInput, loaded.GyroMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse1, loaded.JoystickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse2, loaded.FlickStickMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse3, loaded.TrackpadMouseDestination);
                Assert.AreEqual(MouseOutputDestination.ViiperMouse1, loaded.UnifiedVirtualMouseDestination);
                Assert.AreEqual(MouseOutputDestination.SendInput, loaded.OtherMouseDestination);
                Assert.AreEqual(MouseOutputDestination.SendInput, loaded.AbsoluteMouseDestination);
                Assert.AreEqual(1, runtime.RefreshCount);
                Assert.AreEqual(true, runtime.LastFlushSharedFakerInput);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void ControllerRejectsInvalidAbsoluteDestination()
        {
            AppGlobalData appGlobal = new AppGlobalData
            {
                appSettings = new AppSettingsStore(),
                fakerInputInstalled = true,
            };
            using MouseOutputRoutingController controller =
                new MouseOutputRoutingController(appGlobal);

            bool threw = false;
            try
            {
                controller.ApplyRouting(new MouseOutputRoutingTable
                {
                    AbsoluteMouse = MouseOutputDestination.ViiperMouse1,
                });
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw);
        }

        [TestMethod]
        public void ControllerSnapshotUsesConfiguredValuesWhenServiceStopped()
        {
            AppGlobalData appGlobal = new AppGlobalData
            {
                appSettings = new AppSettingsStore(),
                fakerInputInstalled = true,
            };
            appGlobal.appSettings.JoystickMouseDestination = MouseOutputDestination.ViiperMouse2;

            using MouseOutputRoutingController controller =
                new MouseOutputRoutingController(appGlobal);
            MouseOutputRoutingRuntimeSnapshot snapshot = controller.GetSnapshot();

            Assert.IsFalse(snapshot.ServiceRunning);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse2,
                snapshot.GetResolution(MouseOutputRoute.JoystickMouse).ActiveDestination);
            Assert.AreEqual(MouseOutputDestination.ViiperMouse2,
                snapshot.GetResolution(MouseOutputRoute.JoystickMouse).ConfiguredDestination);
        }
    }
}
