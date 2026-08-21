using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HidLibrary;
using DS4MapperTest.Views;
using DS4MapperTest.ViewModels;
using DS4MapperTest.Behaviors;
using NLog;
using DS4MapperTest.PhysicalMouse;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;

namespace DS4MapperTest
{
    public partial class MainWindow : Window
    {
        private ControllerListViewModel controlListVM;
        private ProfileEditorTestViewModel editorTestVM;
        private AppGlobalData appGlobal;

        private DeviceListItem currentDeviceItem;
        private bool suppressCombo;
        private bool suppressDeviceCombo;
        private bool suppressActionSetCombo;
        private bool suppressActionLayerCombo;
        private ProfileListEntry selectedListEntry;
        private NewProfileCreateViewModel overlayNewProfileVM;
        private bool suppressSelectedProfileFolderCombo;
        private bool suppressProfileListSelection;
        private bool suppressFolderManageStatusHide;
        private readonly ObservableCollection<ProfileEntity> profileComboProfiles = new ObservableCollection<ProfileEntity>();
        // Names as they were last pushed into the combo. The items themselves are
        // reused across refreshes, so a rename mutates an entity that is already
        // in the collection and would otherwise leave stale text on screen.
        private readonly List<string> profileComboNames = new List<string>();

        private IntPtr regHandle = new IntPtr();
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int HOTPLUG_CHECK_DELAY = 2000;
        private bool inHotPlug;
        private int hotplugCounter;
        private readonly ReaderWriterLockSlim hotplugCounterLock = new ReaderWriterLockSlim();

        private bool isSavingProfile;
        private bool isTogglingService;
        private DispatcherTimer gyroCalibrationStatusTimer;
        private bool isDirtyClosePromptActive;
        private DispatcherTimer saveStatusHideTimer;
        private DispatcherTimer deleteActiveProfileWarningHideTimer;
        private DispatcherTimer resetDefaultProfilesStatusHideTimer;
        private static readonly Logger saveProfileLogger = LogManager.GetCurrentClassLogger();
        private readonly ObservableCollection<PhysicalMouseSettingsItem> physicalMouseItems =
            new ObservableCollection<PhysicalMouseSettingsItem>();
        private MouseRoutingPanelViewModel mouseRoutingPanelVM;
        private bool updatingPhysicalMouseSettings;
        private bool stagedPhysicalMouseForwardingEnabled;
        private string stagedPhysicalMouseId;
        private bool appliedPhysicalMouseForwardingEnabled;
        private string appliedPhysicalMouseId;
        private SdlDiagnosticsWindow sdlDiagnosticsWindow;
        private readonly UniversalProfileStore universalProfileStore = UniversalProfileStore.CreateDefault();

        private const double NavCompactWidthThreshold = 820;
        private bool isNavCompact;

        private enum DirtySwitchDecision
        {
            Save,
            Discard,
            Cancel,
        }

        private class ProfileListEntry
        {
            public ProfileEntity Entity { get; }
            public bool IsActive { get; set; }
            public string Name => Entity.Name;
            public string FolderName => Entity.FolderName;
            public string ProfilePath => Entity.ProfilePath;

            public ProfileListEntry(ProfileEntity entity, bool isActive)
            {
                Entity = entity;
                IsActive = isActive;
            }
        }

        private class ProfileFolderListGroup
        {
            public string FolderName { get; set; }
            public bool IsExpanded { get; set; }
            public List<ProfileListEntry> Profiles { get; set; }
            public bool IsEmpty => Profiles == null || Profiles.Count == 0;
        }

        private class ProfilePreview
        {
            public string Name { get; set; }
        }

        private sealed class UniversalProfileSaveUiUpdate
        {
            public string ProfilePath { get; set; }
            public string DisplayName { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        public void PostInit(AppGlobalData appGlobal)
        {
            this.appGlobal = appGlobal;
            UniversalLiveInputRoutingOptions.Apply(appGlobal?.appSettings);

            BackendManager manager = (App.Current as App).Manager;
            controlListVM = new ControllerListViewModel(manager);
            manager.ServiceStarted += BackendManager_ServiceStateChanged;
            manager.ServiceStopped += BackendManager_ServiceStateChanged;
            controlListVM.ReadProfileFailure += ControlListVM_ReadProfileFailure;
            controlListVM.ControllerList.CollectionChanged += ControllerList_CollectionChanged;
            deviceComboBox.ItemsSource = controlListVM.ControllerList;
            // Bound once for the window's lifetime. Reassigning a ComboBox's
            // ItemsSource while it holds a selection is what made refreshes throw;
            // RefreshProfileCombo updates this collection's contents instead.
            profileComboBox.ItemsSource = profileComboProfiles;
            physicalMouseComboBox.ItemsSource = physicalMouseItems;
            mouseRoutingPanelVM = new MouseRoutingPanelViewModel(
                manager.MouseOutputRoutingController,
                action => Dispatcher.BeginInvoke(action));
            mouseRoutingPanelRoot.DataContext = mouseRoutingPanelVM;
            manager.PhysicalMouseStatusChanged += BackendManager_PhysicalMouseStatusChanged;
            LoadPhysicalMouseSettings();
            _ = RefreshPhysicalMouseListAsync();
            noDeviceHint.Visibility = Visibility.Visible;
            actionContextRow.IsEnabled = false;
            gyroCalibrationStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            gyroCalibrationStatusTimer.Tick += GyroCalibrationStatusTimer_Tick;
            gyroCalibrationStatusTimer.Start();
            UpdateServiceControls(manager);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetNavCompactMode(ActualWidth < NavCompactWidthThreshold);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetNavCompactMode(ActualWidth < NavCompactWidthThreshold);
        }

        private void SetNavCompactMode(bool compact)
        {
            isNavCompact = compact;
            navPopup.IsOpen = false;
            navSidebarBorder.Child = null;
            navPopupHost.Child = null;

            if (compact)
            {
                navSidebarBorder.Visibility = Visibility.Collapsed;
                navColumn.Width = new GridLength(0);
                navHamburgerButton.Visibility = Visibility.Visible;
                navPopupHost.Child = navStackPanel;
            }
            else
            {
                navHamburgerButton.Visibility = Visibility.Collapsed;
                navSidebarBorder.Visibility = Visibility.Visible;
                navColumn.Width = new GridLength(220);
                navSidebarBorder.Child = navStackPanel;
            }
        }

        private void NavHamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            navPopup.IsOpen = !navPopup.IsOpen;
        }

        private void NavRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (navPopup != null)
            {
                navPopup.IsOpen = false;
            }

            mainContentScrollViewer?.ScrollToTop();
        }

        private async void RefreshPhysicalMiceButton_Click(object sender, RoutedEventArgs e) =>
            await RefreshPhysicalMouseListAsync();

        private async Task RefreshPhysicalMouseListAsync()
        {
            if (appGlobal == null) return;
            try
            {
                List<PhysicalMouseDevice> devices = await Task.Run(() => PhysicalMouseEnumerator.EnumerateMice());
                string selection = stagedPhysicalMouseId;
                List<PhysicalMouseSettingsItem> items = PhysicalMouseSettingsItems.Create(devices, selection);
                updatingPhysicalMouseSettings = true;
                try
                {
                    physicalMouseItems.Clear();
                    foreach (PhysicalMouseSettingsItem item in items) physicalMouseItems.Add(item);
                    physicalMouseComboBox.SelectedValue = selection;
                }
                finally
                {
                    updatingPhysicalMouseSettings = false;
                }
                UpdatePhysicalMouseSettingsButtons();
                UpdatePhysicalMouseStatus();
            }
            catch (Exception ex)
            {
                physicalMouseValidationText.Text = $"Unable to enumerate physical mice: {ex.Message}";
            }
        }

        private void LoadPhysicalMouseSettings()
        {
            appliedPhysicalMouseForwardingEnabled = appGlobal.appSettings.PhysicalMouseForwardingEnabled;
            appliedPhysicalMouseId = appGlobal.appSettings.SelectedPhysicalMouseId ?? string.Empty;
            stagedPhysicalMouseForwardingEnabled = appliedPhysicalMouseForwardingEnabled;
            stagedPhysicalMouseId = appliedPhysicalMouseId;

            updatingPhysicalMouseSettings = true;
            try
            {
                physicalMouseEnabledCheckBox.IsChecked = stagedPhysicalMouseForwardingEnabled;
                physicalMouseComboBox.SelectedValue = stagedPhysicalMouseId;
            }
            finally
            {
                updatingPhysicalMouseSettings = false;
            }
            UpdatePhysicalMouseSettingsButtons();
            UpdatePhysicalMouseStatus();
        }

        private void DiscardPhysicalMouseSettingsButton_Click(object sender, RoutedEventArgs e) => LoadPhysicalMouseSettings();

        private void ResetPhysicalMouseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            stagedPhysicalMouseForwardingEnabled = false;
            stagedPhysicalMouseId = string.Empty;
            updatingPhysicalMouseSettings = true;
            try
            {
                physicalMouseEnabledCheckBox.IsChecked = false;
                physicalMouseComboBox.SelectedValue = null;
            }
            finally
            {
                updatingPhysicalMouseSettings = false;
            }
            physicalMouseValidationText.Text = string.Empty;
            UpdatePhysicalMouseSettingsButtons();
        }

        private void ApplyPhysicalMouseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            BackendManager manager = (App.Current as App).Manager;
            bool enabled = stagedPhysicalMouseForwardingEnabled;
            string selectedId = stagedPhysicalMouseId;
            if (!manager.ApplyPhysicalMouseSettings(enabled, selectedId, out string validation))
            {
                physicalMouseValidationText.Text = validation;
                return;
            }
            appliedPhysicalMouseForwardingEnabled = enabled;
            appliedPhysicalMouseId = selectedId ?? string.Empty;
            physicalMouseValidationText.Text = string.Empty;
            UpdatePhysicalMouseSettingsButtons();
            UpdatePhysicalMouseStatus();
        }

        private async void ApplyOutputControllerButton_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null) return;

            IsEnabled = false;
            try
            {
                ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
                Exception applyException = null;

                await Task.Run(() =>
                {
                    editorTestVM.DeviceMapper.ProcessMappingChangeAction(() =>
                    {
                        try
                        {
                            editorTestVM.DeviceMapper.ApplyOutputSettings();
                        }
                        catch (Exception ex)
                        {
                            applyException = ex;
                        }
                        finally
                        {
                            resetEvent.Set();
                        }
                    });

                    if (!resetEvent.Wait(TimeSpan.FromSeconds(5)))
                    {
                        throw new TimeoutException("Timed out waiting to apply output controller changes.");
                    }
                });

                if (applyException != null)
                {
                    throw applyException;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply output controller changes:\n{ex.Message}",
                    "Apply Output Controller", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void PhysicalMouseEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (updatingPhysicalMouseSettings) return;
            stagedPhysicalMouseForwardingEnabled = physicalMouseEnabledCheckBox.IsChecked == true;
            physicalMouseValidationText.Text = string.Empty;
            UpdatePhysicalMouseSettingsButtons();
        }

        private void PhysicalMouseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingPhysicalMouseSettings) return;
            stagedPhysicalMouseId = physicalMouseComboBox.SelectedValue as string ?? string.Empty;
            if (!string.IsNullOrEmpty(stagedPhysicalMouseId) && !stagedPhysicalMouseForwardingEnabled)
            {
                stagedPhysicalMouseForwardingEnabled = true;
                physicalMouseEnabledCheckBox.IsChecked = true;
            }
            physicalMouseValidationText.Text = string.Empty;
            UpdatePhysicalMouseSettingsButtons();
        }

        private void UpdatePhysicalMouseSettingsButtons()
        {
            bool settingsChanged = stagedPhysicalMouseForwardingEnabled != appliedPhysicalMouseForwardingEnabled ||
                !string.Equals(stagedPhysicalMouseId ?? string.Empty, appliedPhysicalMouseId ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            applyPhysicalMouseSettingsButton.IsEnabled = settingsChanged;
            discardPhysicalMouseSettingsButton.IsEnabled = settingsChanged;
        }

        private void BackendManager_PhysicalMouseStatusChanged(object sender, EventArgs e) =>
            Dispatcher.BeginInvoke((Action)UpdatePhysicalMouseStatus);

        private void MouseRoutingButton_Click(object sender, RoutedEventArgs e)
        {
            if (mouseRoutingPanelVM == null)
            {
                return;
            }

            mouseRoutingPanelVM.PopupOpen = !mouseRoutingPopup.IsOpen;
            mouseRoutingPopup.IsOpen = mouseRoutingPanelVM.PopupOpen;
        }

        private void MouseRoutingPopup_Closed(object sender, EventArgs e)
        {
            if (mouseRoutingPanelVM != null)
            {
                mouseRoutingPanelVM.PopupOpen = false;
            }
        }

        private void SdlDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sdlDiagnosticsWindow != null)
            {
                sdlDiagnosticsWindow.Activate();
                return;
            }

            sdlDiagnosticsWindow = new SdlDiagnosticsWindow(appGlobal)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            sdlDiagnosticsWindow.Closed += (_, _) => sdlDiagnosticsWindow = null;
            sdlDiagnosticsWindow.Show();
        }

        private void NintendoFaceSwapCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = editorTestVM?.ShowFaceButtonSwapToggle == true &&
                nintendoFaceSwapCheckBox?.IsChecked == true;
            UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled = enabled;
            if (appGlobal?.appSettings != null)
            {
                appGlobal.appSettings.NintendoFaceButtonSwapEnabled = enabled;
                appGlobal.SaveAppSettings();
            }

            if (editorTestVM == null) return;

            DataContext = null;
            editorTestVM.RefreshLayerBindings();
            DataContext = editorTestVM;
        }

        private void RaiseUniversalControllerStateProperties()
        {
            bool showSwap = editorTestVM?.ShowFaceButtonSwapToggle == true;
            UniversalLiveInputRoutingOptions.Apply(appGlobal?.appSettings);
            if (!showSwap)
            {
                UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled = false;
            }

            if (nintendoFaceSwapCheckBox != null)
            {
                nintendoFaceSwapCheckBox.Visibility = showSwap ? Visibility.Visible : Visibility.Collapsed;
                nintendoFaceSwapCheckBox.IsChecked = showSwap && UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled;
            }

            if (navTrackpad != null &&
                editorTestVM?.HasSupportedTouchpadHardware != true &&
                navTrackpad.IsChecked == true)
            {
                navKeybinds.IsChecked = true;
            }

            if (navGyroSensitivity != null &&
                editorTestVM?.HasSupportedGyroHardware != true &&
                navGyroSensitivity.IsChecked == true)
            {
                navKeybinds.IsChecked = true;
            }
        }

        private void RefreshUniversalProfileLists()
        {
            controlListVM?.RefreshUniversalProfileLists();
        }

        private void ApplyMouseRoutingButton_Click(object sender, RoutedEventArgs e)
        {
            if (mouseRoutingPanelVM?.Apply() == true)
            {
                mouseRoutingPopup.IsOpen = false;
            }
        }

        private void DiscardMouseRoutingButton_Click(object sender, RoutedEventArgs e)
        {
            mouseRoutingPanelVM?.DiscardStagedChanges();
            mouseRoutingPopup.IsOpen = false;
        }

        private void ApplyMouseRoutingQuickSetButton_Click(object sender, RoutedEventArgs e)
        {
            mouseRoutingPanelVM?.ApplySelectedDestinationToCompatibleRoutes();
        }

        private void UpdatePhysicalMouseStatus()
        {
            BackendManager manager = (App.Current as App).Manager;
            string status = manager?.PhysicalMouseStatus switch
            {
                PhysicalMouseServiceStatus.Capturing => "Status: Active",
                PhysicalMouseServiceStatus.WaitingForSelectedDevice => "Status: Waiting for selected mouse",
                PhysicalMouseServiceStatus.NoDeviceSelected => "Status: No mouse selected",
                PhysicalMouseServiceStatus.SelectedDeviceVirtual => "Status: Selected device is virtual or invalid",
                PhysicalMouseServiceStatus.RegistrationFailed => "Status: Unable to start Raw Input capture",
                _ => manager?.IsRunning == true ? "Status: Disabled" : "Status: Capture stopped",
            };
            physicalMouseStatusText.Text = status;
        }

        public async void StartCheckProcess()
        {
            await SetMappingServiceRunningAsync(true);
        }

        private async void ServiceToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Manager is null when the backend failed to construct at startup.
            // SetMappingServiceRunningAsync already handles that; reading
            // IsRunning off it first did not.
            BackendManager manager = (Application.Current as App)?.Manager;
            if (manager == null) return;

            await SetMappingServiceRunningAsync(!manager.IsRunning);
        }

        private async Task SetMappingServiceRunningAsync(bool shouldRun)
        {
            BackendManager manager = (Application.Current as App).Manager;
            if (manager == null || isTogglingService || manager.ChangingService) return;
            if (shouldRun == manager.IsRunning)
            {
                UpdateServiceControls(manager);
                return;
            }

            isTogglingService = true;
            UpdateServiceControls(manager);

            Exception serviceException = null;
            try
            {
                await Task.Run(async () =>
                {
                    if (shouldRun)
                    {
                        manager.Start();
                        await Task.Delay(1000);
                    }
                    else
                    {
                        manager.Stop();
                    }
                });
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            isTogglingService = false;
            UpdateServiceControls(manager);

            if (serviceException != null)
            {
                MessageBox.Show(
                    $"Failed to {(shouldRun ? "start" : "stop")} mapping service:\n{serviceException.Message}",
                    "Mapping Service",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BackendManager_ServiceStateChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => UpdateServiceControls(sender as BackendManager)));
        }

        private void UpdateServiceControls(BackendManager manager)
        {
            if (serviceToggleButton == null || serviceStatusText == null) return;

            bool running = manager?.IsRunning == true;
            bool changing = isTogglingService || manager?.ChangingService == true;
            serviceToggleButton.Content = running ? "Stop" : "Start";
            serviceToggleButton.IsEnabled = !changing;
            serviceStatusText.Text = changing
                ? (running ? "Stopping..." : "Starting...")
                : (running ? "Running" : "Stopped");
            UpdateGyroCalibrationControls(manager);
        }

        private void GyroCalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            BackendManager manager = (Application.Current as App).Manager;
            DeviceReaderBase reader = manager?.GetDeviceReader(currentDeviceItem?.Device);
            if (reader != null)
            {
                reader.RequestGyroCalibration();
            }
            else
            {
                currentDeviceItem?.UniversalSession?.Mapper.RequestGyroCalibration();
            }

            UpdateGyroCalibrationControls(manager);
        }

        private void GyroCalibrationStatusTimer_Tick(object sender, EventArgs e)
        {
            currentDeviceItem?.RefreshUniversalState();
            UpdateGyroCalibrationControls((Application.Current as App).Manager);
        }

        private void UpdateGyroCalibrationControls(BackendManager manager)
        {
            if (gyroCalibrateButton == null || gyroCalibrationStatusText == null) return;

            DeviceReaderBase reader = manager?.GetDeviceReader(currentDeviceItem?.Device);
            Common.GyroCalibrationStatus status =
                reader?.GyroCalibrationStatus ??
                currentDeviceItem?.UniversalSession?.Mapper.GyroCalibrationStatus;
            bool active = status != null && (status.IsWaitingToStart || status.IsCalibrating);
            bool canCalibrate =
                reader != null ||
                currentDeviceItem?.UniversalSession?.Controller.Capabilities.Supports(UniversalInputId.Gyroscope) == true;

            gyroCalibrateButton.IsEnabled = manager?.IsRunning == true && canCalibrate && !active;
            gyroCalibrationStatusText.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (active)
            {
                double seconds = status.RemainingMilliseconds / 1000.0;
                gyroCalibrationStatusText.Text = status.IsWaitingToStart
                    ? $"Gyro calibration starts in {seconds:F1}s"
                    : $"Keep controller on a flat surface. Calibrating gyro: {seconds:F1}s";
            }
        }

        private void ControllerList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)ReconcileDeviceSelectionAfterListChange);
        }

        private void ReconcileDeviceSelectionAfterListChange()
        {
            if (currentDeviceItem != null &&
                !controlListVM.ControllerList.Contains(currentDeviceItem))
            {
                HandleCurrentDeviceRemoved();
            }

            // Any controller arriving while nothing is loaded should be picked up.
            // Requiring the list to hold exactly one item meant a second device
            // still in the list - the app's own virtual pad, or a controller whose
            // profile had failed to load - left a freshly reconnected controller
            // sitting there unrecognised until the app was restarted.
            if (currentDeviceItem == null)
            {
                foreach (DeviceListItem candidate in controlListVM.ControllerList)
                {
                    if (LoadProfileForDevice(candidate))
                    {
                        break;
                    }
                }
            }
        }

        private void HandleCurrentDeviceRemoved()
        {
            if (profilesOverlay.Visibility == Visibility.Visible)
            {
                // A profile dialog (e.g. the folder browse picker) may be open
                // and modal on this thread right now. Close it before tearing
                // down the overlay so a disconnect mid-workflow can't leave a
                // dangling picker pointed at a profile folder for a controller
                // that is no longer connected.
                Util.CloseOwnedDialogs(new WindowInteropHelper(this).Handle);
                HideNewProfilePanel();
                profilesOverlay.Visibility = Visibility.Collapsed;
            }

            InlineBindingEditorService.CloseAny();
            ExitRenameSetMode();
            ExitRenameLayerMode();

            editorTestVM?.UnregisterEvents();
            editorTestVM = null;
            currentDeviceItem = null;
            DataContext = null;
            RaiseUniversalControllerStateProperties();

            suppressDeviceCombo = true;
            deviceComboBox.SelectedItem = null;
            suppressDeviceCombo = false;

            bool loaded = false;
            foreach (DeviceListItem candidate in controlListVM.ControllerList)
            {
                if (LoadProfileForDevice(candidate))
                {
                    loaded = true;
                    break;
                }
            }

            if (!loaded)
            {
                noDeviceHint.Visibility = Visibility.Visible;
                actionContextRow.IsEnabled = false;
                ClearProfileComboItems();
                profileListBox.ItemsSource = null;
                actionSetComboBox.ItemsSource = null;
                actionLayerComboBox.ItemsSource = null;
            }
        }

        private bool LoadProfileForDevice(DeviceListItem item)
        {
            if (item == null || item.ProfileIndex < 0) return false;

            BackendManager manager = (App.Current as App).Manager;
            Mapper mapper;
            var profileList = item.DevProfileList;
            if (item.IsUniversal)
            {
                mapper = item.UniversalSession.Mapper;
            }
            else
            {
                if (!manager.MapperDict.ContainsKey(item.Device.Index)) return false;

                mapper = manager.MapperDict[item.Device.Index];
                InputDeviceType devType = mapper.DeviceType;
                if (!manager.DeviceProfileListDict.ContainsKey(devType)) return false;

                profileList = manager.DeviceProfileListDict[devType].ProfileListCol;
            }

            if (item.ProfileIndex >= profileList.Count) return false;

            ProfileEntity profileEnt = profileList[item.ProfileIndex];
            if (item.IsUniversal)
            {
                UniversalProfile selectedProfile = universalProfileStore.LoadFromPath(profileEnt.ProfilePath);
                if (item.UniversalSession.ActiveProfile == null ||
                    item.UniversalSession.ActiveProfile.ProfileId != selectedProfile.ProfileId)
                {
                    manager.UniversalMappingRuntime.SwitchProfile(item.UniversalSession.LogicalControllerId, selectedProfile);
                }
            }

            InlineBindingEditorService.CloseAny();
            ExitRenameSetMode();
            ExitRenameLayerMode();

            // Clear inherited bindings before replacing the editor.  Some child
            // controls keep profile-list item contexts, so swapping directly can
            // make WPF resolve a binding against its internal unset-value marker.
            // Unlike SwitchActionSetAsync/SwitchActionLayerAsync (which await a real
            // yield point between the clear and the reassignment), this method is
            // fully synchronous, so the DataContext = null below never actually
            // reaches the dispatcher before being overwritten a few lines later.
            // Pump the dispatcher so WPF processes the clear first.
            DataContext = null;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            editorTestVM?.UnregisterEvents();
            editorTestVM = new ProfileEditorTestViewModel(mapper, profileEnt, mapper.ActionProfile);
            DataContext = editorTestVM;
            editorTestVM.Test();
            RaiseUniversalControllerStateProperties();

            currentDeviceItem = item;
            noDeviceHint.Visibility = Visibility.Collapsed;
            actionContextRow.IsEnabled = true;

            RefreshDeviceCombo();
            RefreshProfileCombo();
            RefreshProfileList();
            RefreshActionSetCombo();
            RefreshActionLayerCombo();

            return true;
        }

        // Saving a universal profile (and renaming the one being edited) hands the stored
        // profile back to UniversalMappingRuntime.SwitchProfile, which recompiles it into
        // the live mapper. That builds a brand new Profile object graph and clears
        // Mapper.EditActionSet/EditLayer, but the editor view model went on holding the
        // pre-save graph, so from the first save onwards the editor and the running mapper
        // were two different profiles: panels showed every later edit while the controller
        // ignored it, the next save serialised the mapper's own untouched copy back over
        // those edits, and any panel that rebuilt a prop view model threw on the cleared
        // edit layer - an exception WPF swallows as a failed binding update, leaving a mode
        // selector showing a mode whose settings were never loaded. Rebind the editor onto
        // whichever profile the mapper is actually running.
        private void RebindEditorToLiveProfile()
        {
            ProfileEditorTestViewModel staleVM = editorTestVM;
            Mapper mapper = staleVM?.DeviceMapper;
            if (mapper == null || ReferenceEquals(mapper.ActionProfile, staleVM.CurrentProfile)) return;

            InlineBindingEditorService.CloseAny();
            ExitRenameSetMode();
            ExitRenameLayerMode();

            // Same reason as LoadProfileForDevice: clear the inherited bindings and let WPF
            // process the clear before the replacement view model is attached.
            DataContext = null;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            staleVM.UnregisterEvents();
            editorTestVM = new ProfileEditorTestViewModel(mapper, staleVM.ProfileEnt, mapper.ActionProfile);
            DataContext = editorTestVM;
            editorTestVM.Test();
            RaiseUniversalControllerStateProperties();

            RefreshActionSetCombo();
            RefreshActionLayerCombo();
        }

        private void RefreshDeviceCombo()
        {
            if (currentDeviceItem == null) return;

            suppressDeviceCombo = true;
            deviceComboBox.SelectedItem = currentDeviceItem;
            suppressDeviceCombo = false;
        }

        private async void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressDeviceCombo) return;

            DeviceListItem newItem = deviceComboBox.SelectedItem as DeviceListItem;
            if (newItem == null || newItem == currentDeviceItem) return;

            if (!await ConfirmDiscardProfileChangesAsync())
            {
                RefreshDeviceCombo();
                return;
            }

            if (!LoadProfileForDevice(newItem))
            {
                RefreshDeviceCombo();
            }
        }

        private void RefreshProfileCombo()
        {
            if (currentDeviceItem == null) return;

            suppressCombo = true;
            try
            {
                ResyncCurrentDeviceProfileIndexToActiveProfile();

                ProfileEntity activeProfile = currentDeviceItem.ProfileIndex >= 0 &&
                    currentDeviceItem.ProfileIndex < currentDeviceItem.DevProfileList.Count
                    ? currentDeviceItem.DevProfileList[currentDeviceItem.ProfileIndex]
                    : null;
                string activeFolderName = activeProfile?.FolderName ?? string.Empty;

                List<ProfileEntity> updatedProfiles = currentDeviceItem.DevProfileList
                    .Where(profile => string.Equals(profile.FolderName, activeFolderName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                UpdateProfileComboItems(updatedProfiles, activeProfile);

                profileFolderText.Text = activeFolderName;
                profileFolderText.Visibility = string.IsNullOrEmpty(activeFolderName)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            catch (Exception ex)
            {
                // Every profile operation (load, delete, rename, move, reset
                // defaults) refreshes this combo and then refreshes the profile
                // browser. Letting a refresh failure escape aborted the rest of
                // the operation's UI update -- that is what made a deleted
                // profile linger until a second delete attempt -- and surfaced a
                // raw WPF message in the caller's error dialog. The model is
                // already up to date by this point, so log and carry on.
                saveProfileLogger.Error(ex, "Failed to refresh the profile combo box");
            }
            finally
            {
                suppressCombo = false;
            }
        }

        /// <summary>
        /// Brings the ComboBox's items in line with <paramref name="updatedProfiles"/>,
        /// touching the bound collection only when its contents actually changed.
        /// </summary>
        private void UpdateProfileComboItems(List<ProfileEntity> updatedProfiles, ProfileEntity activeProfile)
        {
            bool sameItems = profileComboProfiles.SequenceEqual(updatedProfiles) &&
                profileComboNames.SequenceEqual(updatedProfiles.Select(profile => profile.Name ?? string.Empty),
                    StringComparer.Ordinal);

            // Most refreshes (loading a profile, saving, switching folders back and
            // forth) leave the listed profiles untouched and only move the
            // selection, so skipping the rebuild keeps the ComboBox out of the
            // clear/repopulate window entirely.
            if (sameItems)
            {
                if (!ReferenceEquals(profileComboBox.SelectedItem, activeProfile))
                {
                    profileComboBox.SelectedItem = activeProfile;
                }

                return;
            }

            // Drop the selection first: removing the selected item from the bound
            // collection makes WPF hand the ComboBox's selection box one of its
            // internal sentinel objects, and anything that then reads a property
            // off that sentinel throws.
            profileComboBox.SelectedItem = null;

            profileComboProfiles.Clear();
            profileComboNames.Clear();
            foreach (ProfileEntity profile in updatedProfiles)
            {
                profileComboProfiles.Add(profile);
                profileComboNames.Add(profile.Name ?? string.Empty);
            }

            profileComboBox.SelectedItem = activeProfile;
        }

        private void ClearProfileComboItems()
        {
            profileComboBox.SelectedItem = null;
            profileComboProfiles.Clear();
            profileComboNames.Clear();
            profileFolderText.Text = string.Empty;
            profileFolderText.Visibility = Visibility.Collapsed;
        }

        private void ResyncCurrentDeviceProfileIndexToActiveProfile()
        {
            if (currentDeviceItem == null || editorTestVM?.ProfileEnt == null) return;

            string activePath = editorTestVM.ProfileEnt.ProfilePath;
            var profileList = currentDeviceItem.DevProfileList;
            int activeIndex = profileList
                .Select((profile, index) => new { profile, index })
                .Where(item => string.Equals(item.profile.ProfilePath, activePath, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1)
                .First();

            if (activeIndex >= 0 && activeIndex != currentDeviceItem.ProfileIndex)
            {
                currentDeviceItem.ResyncProfileIndex(activeIndex, reloadProfile: false);
            }

            ResyncOtherDeviceProfileIndexes();
        }

        // Every other connected device that shares this profile list (same
        // InputDeviceType) keeps its own cached ProfileIndex - a numeric
        // position into ProfileListCol. Deleting, renaming or moving a
        // profile/folder mutates that shared list for everyone, but only
        // currentDeviceItem's index gets corrected above. Left alone, a
        // sibling device's stale index silently ends up pointing at whatever
        // profile now occupies that position - often another profile in the
        // same folder, since entries are sorted by folder then name - the
        // next time that device becomes current.
        private void ResyncOtherDeviceProfileIndexes()
        {
            if (currentDeviceItem == null) return;

            BackendManager manager = (App.Current as App).Manager;
            ProfileList sharedList = currentDeviceItem.ProfileListHolder;

            foreach (DeviceListItem other in controlListVM.ControllerList)
            {
                if (other.IsUniversal ||
                    other == currentDeviceItem ||
                    !ReferenceEquals(other.ProfileListHolder, sharedList))
                {
                    continue;
                }

                if (!manager.MapperDict.TryGetValue(other.Device.Index, out Mapper otherMapper))
                {
                    continue;
                }

                string otherActivePath = otherMapper.ProfileFile;
                if (string.IsNullOrEmpty(otherActivePath))
                {
                    continue;
                }

                var profileList = other.DevProfileList;
                int activeIndex = profileList
                    .Select((profile, index) => new { profile, index })
                    .Where(item => string.Equals(item.profile.ProfilePath, otherActivePath, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.index)
                    .DefaultIfEmpty(-1)
                    .First();

                if (activeIndex >= 0 && activeIndex != other.ProfileIndex)
                {
                    other.ResyncProfileIndex(activeIndex, reloadProfile: false);
                }
            }
        }

        private void RefreshActionSetCombo()
        {
            if (editorTestVM == null)
            {
                actionSetComboBox.ItemsSource = null;
                return;
            }

            suppressActionSetCombo = true;
            actionSetComboBox.ItemsSource = editorTestVM.ActionSetItems;
            actionSetComboBox.SelectedIndex = editorTestVM.SelectedActionSetIndex;
            suppressActionSetCombo = false;

            removeSetButton.IsEnabled = editorTestVM.SelectedActionSetIndex > 0;
            removeSetButton.ToolTip = removeSetButton.IsEnabled
                ? "Remove Action Set"
                : "The default Action Set cannot be removed.";
        }

        private void RefreshActionLayerCombo()
        {
            if (editorTestVM == null)
            {
                actionLayerComboBox.ItemsSource = null;
                return;
            }

            suppressActionLayerCombo = true;
            actionLayerComboBox.ItemsSource = editorTestVM.LayerItems;
            actionLayerComboBox.SelectedIndex = editorTestVM.SelectedActionLayerIndex;
            suppressActionLayerCombo = false;

            removeLayerButton.IsEnabled = editorTestVM.SelectedActionLayerIndex > 0;
            removeLayerButton.ToolTip = removeLayerButton.IsEnabled
                ? "Remove Action Layer"
                : "The default Action Layer cannot be removed.";
        }

        private async void ActionSetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressActionSetCombo || editorTestVM == null) return;

            int newIndex = actionSetComboBox.SelectedIndex;
            if (newIndex < 0 || newIndex == editorTestVM.SelectedActionSetIndex) return;

            await SwitchActionSetAsync(newIndex);
        }

        private async Task SwitchActionSetAsync(int newIndex)
        {
            IsEnabled = false;
            InlineBindingEditorService.CloseAny();
            ExitRenameSetMode();
            ExitRenameLayerMode();

            editorTestVM.SwitchActionSets(newIndex);

            // See TestSave/TestFakeSave: ProcessMappingChangeAction only tries once, for up
            // to 500ms, to halt the input reading thread before giving up and never running
            // the queued action (and its ActionResetEvent.Set()) at all. Without a bounded
            // wait here, a missed halt window hung this method, and the whole window with it
            // since IsEnabled stays false, forever.
            if (!await Task.Run(() => editorTestVM.ActionResetEvent.Wait(TimeSpan.FromSeconds(5))))
            {
                MessageBox.Show("Timed out waiting for the mapper thread to become available for switching Action Sets.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                IsEnabled = true;
                return;
            }

            DataContext = null;
            editorTestVM.RefreshSetBindings();
            DataContext = editorTestVM;

            RefreshActionSetCombo();
            RefreshActionLayerCombo();

            IsEnabled = true;
        }

        private async void ActionLayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressActionLayerCombo || editorTestVM == null) return;

            int newIndex = actionLayerComboBox.SelectedIndex;
            if (newIndex < 0 || newIndex == editorTestVM.SelectedActionLayerIndex) return;

            await SwitchActionLayerAsync(newIndex);
        }

        private async Task SwitchActionLayerAsync(int newIndex)
        {
            IsEnabled = false;
            InlineBindingEditorService.CloseAny();
            ExitRenameLayerMode();

            editorTestVM.SwitchActionLayer(newIndex);

            // See TestSave/TestFakeSave: ProcessMappingChangeAction only tries once, for up
            // to 500ms, to halt the input reading thread before giving up and never running
            // the queued action (and its ActionResetEvent.Set()) at all. Without a bounded
            // wait here, a missed halt window hung this method, and the whole window with it
            // since IsEnabled stays false, forever.
            if (!await Task.Run(() => editorTestVM.ActionResetEvent.Wait(TimeSpan.FromSeconds(5))))
            {
                MessageBox.Show("Timed out waiting for the mapper thread to become available for switching Action Layers.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                IsEnabled = true;
                return;
            }

            DataContext = null;
            editorTestVM.RefreshLayerBindings();
            DataContext = editorTestVM;

            RefreshActionLayerCombo();

            IsEnabled = true;
        }

        private async void AddSetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null) return;

            try
            {
                int newIndex = editorTestVM.AddSet();
                RefreshActionSetCombo();
                if (newIndex >= 0)
                {
                    await SwitchActionSetAsync(newIndex);
                }
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show(ex.Message, "Add Action Set",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RemoveSetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null || editorTestVM.SelectedActionSetIndex <= 0) return;

            string setName = editorTestVM.ActionSetItems[editorTestVM.SelectedActionSetIndex].DisplayName;
            var confirm = MessageBox.Show(
                $"Remove action set \"{setName}\"?\n\nThis cannot be undone.",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            editorTestVM.RemoveSet();
            await SwitchActionSetAsync(editorTestVM.SelectedActionSetIndex);
        }

        private async void AddLayerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null) return;

            try
            {
                int newIndex = editorTestVM.AddLayer();
                RefreshActionLayerCombo();
                if (newIndex >= 0)
                {
                    await SwitchActionLayerAsync(newIndex);
                }
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show(ex.Message, "Add Action Layer",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RemoveLayerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null || editorTestVM.SelectedActionLayerIndex <= 0) return;

            string layerName = editorTestVM.LayerItems[editorTestVM.SelectedActionLayerIndex].DisplayName;
            var confirm = MessageBox.Show(
                $"Remove action layer \"{layerName}\"?\n\nThis cannot be undone.",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            editorTestVM.RemoveLayer();
            await SwitchActionLayerAsync(editorTestVM.SelectedActionLayerIndex);
        }

        private void RenameSetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null) return;

            renameSetTextBox.Text = editorTestVM.CurrentSetName;
            actionSetComboBox.Visibility = Visibility.Collapsed;
            addSetButton.Visibility = Visibility.Collapsed;
            renameSetButton.Visibility = Visibility.Collapsed;
            removeSetButton.Visibility = Visibility.Collapsed;
            renameSetTextBox.Visibility = Visibility.Visible;
            confirmRenameSetButton.Visibility = Visibility.Visible;
            cancelRenameSetButton.Visibility = Visibility.Visible;
            renameSetTextBox.Focus();
            renameSetTextBox.SelectAll();
        }

        private void ConfirmRenameSetBtn_Click(object sender, RoutedEventArgs e)
        {
            CommitRenameSet();
        }

        private void CancelRenameSetBtn_Click(object sender, RoutedEventArgs e)
        {
            ExitRenameSetMode();
        }

        private void RenameSetTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitRenameSet();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ExitRenameSetMode();
                e.Handled = true;
            }
        }

        private void CommitRenameSet()
        {
            if (editorTestVM == null) return;

            string newName = renameSetTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Action set name cannot be empty.", "Rename",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            editorTestVM.CurrentSetName = newName;
            RefreshActionSetCombo();
            ExitRenameSetMode();
        }

        private void ExitRenameSetMode()
        {
            renameSetTextBox.Visibility = Visibility.Collapsed;
            confirmRenameSetButton.Visibility = Visibility.Collapsed;
            cancelRenameSetButton.Visibility = Visibility.Collapsed;
            actionSetComboBox.Visibility = Visibility.Visible;
            addSetButton.Visibility = Visibility.Visible;
            renameSetButton.Visibility = Visibility.Visible;
            removeSetButton.Visibility = Visibility.Visible;
        }

        private void RenameLayerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null) return;

            renameLayerTextBox.Text = editorTestVM.CurrentLayerName;
            actionLayerComboBox.Visibility = Visibility.Collapsed;
            addLayerButton.Visibility = Visibility.Collapsed;
            renameLayerButton.Visibility = Visibility.Collapsed;
            removeLayerButton.Visibility = Visibility.Collapsed;
            renameLayerTextBox.Visibility = Visibility.Visible;
            confirmRenameLayerButton.Visibility = Visibility.Visible;
            cancelRenameLayerButton.Visibility = Visibility.Visible;
            renameLayerTextBox.Focus();
            renameLayerTextBox.SelectAll();
        }

        private void ConfirmRenameLayerBtn_Click(object sender, RoutedEventArgs e)
        {
            CommitRenameLayer();
        }

        private void CancelRenameLayerBtn_Click(object sender, RoutedEventArgs e)
        {
            ExitRenameLayerMode();
        }

        private void RenameLayerTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitRenameLayer();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ExitRenameLayerMode();
                e.Handled = true;
            }
        }

        private void CommitRenameLayer()
        {
            if (editorTestVM == null) return;

            string newName = renameLayerTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Action layer name cannot be empty.", "Rename",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            editorTestVM.CurrentLayerName = newName;
            RefreshActionLayerCombo();
            ExitRenameLayerMode();
        }

        private void ExitRenameLayerMode()
        {
            renameLayerTextBox.Visibility = Visibility.Collapsed;
            confirmRenameLayerButton.Visibility = Visibility.Collapsed;
            cancelRenameLayerButton.Visibility = Visibility.Collapsed;
            actionLayerComboBox.Visibility = Visibility.Visible;
            addLayerButton.Visibility = Visibility.Visible;
            renameLayerButton.Visibility = Visibility.Visible;
            removeLayerButton.Visibility = Visibility.Visible;
        }

        private async void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressCombo || currentDeviceItem == null) return;

            ProfileEntity selectedProfile = profileComboBox.SelectedItem as ProfileEntity;
            if (selectedProfile == null) return;

            int newIndex = currentDeviceItem.DevProfileList.IndexOf(selectedProfile);
            if (newIndex < 0 || newIndex == currentDeviceItem.ProfileIndex) return;

            await SwitchProfileAsync(currentDeviceItem, newIndex);
        }

        private void RefreshProfileList(string selectedFolderName = null, string selectedProfilePath = null)
        {
            if (currentDeviceItem == null)
            {
                profileListBox.ItemsSource = null;
                return;
            }

            string activePath = editorTestVM?.ProfileEnt?.ProfilePath ?? string.Empty;
            var entries = currentDeviceItem.DevProfileList
                .Select(p => new ProfileListEntry(p, string.Equals(p.ProfilePath, activePath, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var entriesByFolder = entries
                .GroupBy(entry => entry.FolderName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            // Drive the browser off the folder list rather than off the profiles,
            // so a folder that has just been created (or emptied) still shows up,
            // collapsed, instead of only appearing once something is moved into it.
            // ProfileFolderCol is already in display order (Default, VALORANT, then
            // alphabetical); anything a profile claims but the folder list has not
            // caught up with yet is appended so no profile can go missing.
            List<string> folderNames = GetProfileFolderSnapshot();
            foreach (string folderName in entriesByFolder.Keys)
            {
                if (!folderNames.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                {
                    folderNames.Add(folderName);
                }
            }

            // Rebuilding the browser used to collapse every folder the user had
            // opened, so an action taken inside a folder (a delete especially)
            // looked like it had done nothing at all. Carry the open folders over.
            HashSet<string> expandedFolders = GetExpandedProfileFolders();

            var groups = folderNames
                .Select(folderName =>
                {
                    entriesByFolder.TryGetValue(folderName, out List<ProfileListEntry> folderEntries);
                    folderEntries ??= new List<ProfileListEntry>();
                    return new ProfileFolderListGroup
                    {
                        FolderName = folderName,
                        IsExpanded = expandedFolders.Contains(folderName) ||
                            folderEntries.Any(entry => entry.IsActive) ||
                            (!string.IsNullOrWhiteSpace(selectedProfilePath) &&
                                folderEntries.Any(entry => string.Equals(entry.ProfilePath, selectedProfilePath, StringComparison.OrdinalIgnoreCase))),
                        Profiles = folderEntries,
                    };
                })
                .ToList();

            profileListBox.ItemsSource = groups;
            RefreshFolderManagementControls(selectedFolderName);

            selectedListEntry = !string.IsNullOrWhiteSpace(selectedProfilePath)
                ? entries.FirstOrDefault(entry => string.Equals(entry.Entity.ProfilePath, selectedProfilePath, StringComparison.OrdinalIgnoreCase))
                : null;

            if (selectedListEntry != null)
            {
                profileRenameBox.Text = selectedListEntry.Name;
                suppressSelectedProfileFolderCombo = true;
                selectedProfileFolderComboBox.ItemsSource = GetProfileFolderSnapshot();
                selectedProfileFolderComboBox.SelectedItem = selectedListEntry.FolderName;
                suppressSelectedProfileFolderCombo = false;
                ShowSelectedProfileControls(true);
                Dispatcher.BeginInvoke(new Action(() => SelectProfileListEntry(selectedListEntry)),
                    DispatcherPriority.Loaded);
            }
            else
            {
                ShowSelectedProfileControls(false);
            }

            HideDeleteActiveProfileWarning();
        }

        // Copy Selected acts on the browser's selection rather than on the
        // active profile, so it has nothing to work with until a row is picked.
        private void ShowSelectedProfileControls(bool visible)
        {
            selectedProfilePanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            copySelectedProfileBtn.IsEnabled = visible;
        }

        private HashSet<string> GetExpandedProfileFolders()
        {
            HashSet<string> expandedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (profileListBox.ItemsSource is not IEnumerable<ProfileFolderListGroup> currentGroups)
            {
                return expandedFolders;
            }

            foreach (ProfileFolderListGroup group in currentGroups)
            {
                // IsExpanded is two-way bound to each folder's Expander, so the
                // group objects still on screen carry the user's current state.
                if (group.IsExpanded && !string.IsNullOrWhiteSpace(group.FolderName))
                {
                    expandedFolders.Add(group.FolderName);
                }
            }

            return expandedFolders;
        }

        private void ProfileListBox_Loaded(object sender, RoutedEventArgs e)
        {
            // The profile browser's own ScrollViewer lives inside the overlay,
            // so attach wheel bubbling directly. Scrolling at the top or bottom
            // should hand off to the outer manage-profiles scroll viewer.
            if (ScrollViewerBehavior.FindVisualChild<ScrollViewer>(profileListBox) is ScrollViewer innerScrollViewer)
            {
                ScrollViewerBehavior.SetBubbleWheelToParent(innerScrollViewer, true);
            }
        }

        private void ProfileGroupListBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Each folder list has its own ScrollViewer in the default control
            // template, so the app-wide implicit ScrollViewer style (which
            // wires up ScrollViewerBehavior.BubbleWheelToParent) is not
            // guaranteed to reach it.
            if (sender is ListBox listBox &&
                ScrollViewerBehavior.FindVisualChild<ScrollViewer>(listBox) is ScrollViewer innerScrollViewer)
            {
                ScrollViewerBehavior.SetBubbleWheelToParent(innerScrollViewer, true);
            }
        }

        private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressProfileListSelection) return;
            if (sender is not ListBox selectedListBox) return;

            ProfileListEntry clickedEntry = selectedListBox.SelectedItem as ProfileListEntry;
            ApplyProfileListSelection(selectedListBox, clickedEntry);
        }

        private async void ProfileListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBoxItem item ||
                item.DataContext is not ProfileListEntry clickedEntry)
            {
                return;
            }

            ListBox selectedListBox = FindVisualAncestor<ListBox>(item);
            if (selectedListBox == null)
            {
                return;
            }

            bool wasSuppressed = suppressProfileListSelection;
            suppressProfileListSelection = true;
            try
            {
                selectedListBox.SelectedItem = clickedEntry;
            }
            finally
            {
                suppressProfileListSelection = wasSuppressed;
            }

            ApplyProfileListSelection(selectedListBox, clickedEntry);

            // Handling the click here stops ListBoxItem's own mouse-down
            // handling, which is what normally moves keyboard focus into the
            // row. Without focus the list counts as inactive and WPF paints the
            // muted grey unfocused-selection brush instead of the accent blue,
            // so the picked profile stops looking picked. Take focus explicitly.
            item.Focus();
            e.Handled = true;

            // Handling the click here also suppresses ListBoxItem's own double
            // click event, so the second click is recognised from the click
            // count instead. The row has just been selected above either way,
            // so a double click loads it whether or not it was already the
            // selected profile.
            if (e.ClickCount >= 2)
            {
                await LoadSelectedProfileFromListAsync();
            }
        }

        private void ApplyProfileListSelection(ListBox selectedListBox, ProfileListEntry clickedEntry)
        {
            // Hold the entry locally. Anything below that clears another folder's
            // list re-enters selection plumbing, so the field must be set from
            // this click's row, not from a later deselection event.
            selectedListEntry = clickedEntry;
            HideDeleteActiveProfileWarning();
            if (clickedEntry == null)
            {
                ShowSelectedProfileControls(false);
                return;
            }

            ClearOtherProfileListSelections(profileListBox, selectedListBox);
            profileRenameBox.Text = clickedEntry.Name;
            suppressSelectedProfileFolderCombo = true;
            selectedProfileFolderComboBox.ItemsSource = GetProfileFolderSnapshot();
            selectedProfileFolderComboBox.SelectedItem = clickedEntry.FolderName;
            suppressSelectedProfileFolderCombo = false;
            ShowSelectedProfileControls(true);

            // Scroll just far enough that Name/Folder/Delete/Load This Profile are
            // fully in view once a profile is picked, rather than always jumping to
            // the very bottom of the overlay (which now has the collapsible Folders
            // panel below this one). BringIntoView is a no-op when the panel is
            // already fully visible. Deferred to Loaded priority so layout has
            // already accounted for selectedProfilePanel becoming visible first.
            Dispatcher.BeginInvoke(new Action(() => selectedProfilePanel.BringIntoView()),
                DispatcherPriority.Loaded);
        }

        private static T FindVisualAncestor<T>(DependencyObject start)
            where T : DependencyObject
        {
            DependencyObject current = start;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void ClearOtherProfileListSelections(DependencyObject root, ListBox selectedListBox)
        {
            // Each folder in the browser has its own list, so picking a profile in
            // one folder has to drop the selection in the others. Those lists raise
            // SelectionChanged as they are cleared, which reads as the user
            // deselecting and would tear down the panel this click is setting up.
            // The list being clicked is never touched here, so suppressing the
            // handler for the duration only silences the clearing.
            bool wasSuppressed = suppressProfileListSelection;
            suppressProfileListSelection = true;
            try
            {
                ClearOtherProfileListSelectionsCore(root, selectedListBox);
            }
            finally
            {
                suppressProfileListSelection = wasSuppressed;
            }
        }

        private void ClearOtherProfileListSelectionsCore(DependencyObject root, ListBox selectedListBox)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, childIndex);
                if (child is ListBox listBox && !ReferenceEquals(listBox, selectedListBox))
                {
                    listBox.SelectedItem = null;
                }

                ClearOtherProfileListSelectionsCore(child, selectedListBox);
            }
        }

        private void SelectProfileListEntry(ProfileListEntry entry)
        {
            if (entry == null) return;

            SelectProfileListEntry(profileListBox, entry);
        }

        private bool SelectProfileListEntry(DependencyObject root, ProfileListEntry entry)
        {
            if (root == null) return false;

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, childIndex);
                if (child is ListBox listBox && listBox.Items.Contains(entry))
                {
                    listBox.SelectedItem = entry;
                    listBox.ScrollIntoView(entry);
                    ClearOtherProfileListSelections(profileListBox, listBox);
                    return true;
                }

                if (SelectProfileListEntry(child, entry))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> SwitchProfileAsync(DeviceListItem item, int newIndex)
        {
            if (!await ConfirmDiscardProfileChangesAsync())
            {
                RefreshProfileCombo();
                return false;
            }

            IsEnabled = false;
            suppressCombo = true;

            bool loaded = false;
            Exception switchException = null;
            try
            {
                await Task.Run(() => { item.ProfileIndex = newIndex; });
                loaded = LoadProfileForDevice(item);
            }
            catch (Exception ex)
            {
                switchException = ex;
            }
            finally
            {
                suppressCombo = false;
                IsEnabled = true;
            }

            if (switchException != null)
            {
                saveProfileLogger.Error(switchException, "Failed to switch profile");
                MessageBox.Show(
                    $"Failed to load the selected profile:\n{switchException.Message}",
                    "Load Profile",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                RefreshProfileCombo();
                return false;
            }

            return loaded;
        }

        private void ManageProfilesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null) return;
            RefreshProfileList();
            HideManageFoldersPanel();
            profilesOverlay.Visibility = Visibility.Visible;
        }

        private void CloseProfileOverlay_Click(object sender, RoutedEventArgs e)
        {
            HideNewProfilePanel();
            HideManageFoldersPanel();
            profilesOverlay.Visibility = Visibility.Collapsed;
        }

        private void ProfilesOverlayBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideNewProfilePanel();
            HideManageFoldersPanel();
            profilesOverlay.Visibility = Visibility.Collapsed;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && profilesOverlay.Visibility == Visibility.Visible)
            {
                HideNewProfilePanel();
                HideManageFoldersPanel();
                profilesOverlay.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void ManageFoldersBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null) return;

            if (manageFoldersPanel.Visibility == Visibility.Visible)
            {
                HideManageFoldersPanel();
                return;
            }

            RefreshFolderManagementControls();
            manageFoldersPanel.Visibility = Visibility.Visible;
        }

        private void CloseManageFoldersBtn_Click(object sender, RoutedEventArgs e)
        {
            HideManageFoldersPanel();
        }

        private void HideManageFoldersPanel()
        {
            manageFoldersPanel.Visibility = Visibility.Collapsed;
        }

        private void NewProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;

            if (newProfilePanel.Visibility == Visibility.Visible)
            {
                HideNewProfilePanel();
                return;
            }

            BackendManager manager = (App.Current as App).Manager;
            Mapper mapper = editorTestVM.DeviceMapper;

            overlayNewProfileVM = currentDeviceItem.IsUniversal
                ? new NewProfileCreateViewModel(
                    mapper,
                    manager,
                    currentDeviceItem.ProfileFolders,
                    universalProfileStore.GetFolderPath)
                : new NewProfileCreateViewModel(mapper, manager);
            newProfilePanel.DataContext = overlayNewProfileVM;
            newProfilePanel.Visibility = Visibility.Visible;
        }

        private void CancelNewProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            HideNewProfilePanel();
        }

        private async void CreateNewProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || overlayNewProfileVM == null) return;

            if (currentDeviceItem.IsUniversal)
            {
                await CreateUniversalProfileFromClassicDialogAsync();
                return;
            }

            bool validForm = overlayNewProfileVM.ValidateForm();
            if (!validForm) return;

            overlayNewProfileVM.CreateProfile();

            NewProfileCreateViewModel newProfVM = overlayNewProfileVM;
            HideNewProfilePanel();

            BackendManager manager = (App.Current as App).Manager;
            Mapper mapper = newProfVM.Mapper;
            if (newProfVM == null || !newProfVM.ProfileCreated) return;

            var profileList = manager.DeviceProfileListDict[mapper.DeviceType].ProfileListCol;
            var newEnt = profileList.FirstOrDefault(p => string.Equals(p.ProfilePath, newProfVM.ProfilePath, StringComparison.OrdinalIgnoreCase));
            if (newEnt != null)
            {
                int newIndex = profileList.IndexOf(newEnt);
                // Await the switch (rather than fire-and-forget it) so the
                // browser refresh below runs after the new profile has
                // actually landed in the list, instead of racing it and
                // redrawing the overlay from a stale, pre-creation snapshot
                // that then never gets rebuilt until the overlay is reopened.
                await SwitchProfileAsync(currentDeviceItem, newIndex);
            }

            RefreshProfileList();
        }

        private async Task CreateUniversalProfileFromClassicDialogAsync()
        {
            string profileName = overlayNewProfileVM.ProfileName?.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Profile name cannot be empty.", "Create Profile",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                UniversalProfile profile = CreateBlankUniversalProfile(profileName);
                UniversalProfileEditorSaveCoordinator coordinator = new UniversalProfileEditorSaveCoordinator(
                    universalProfileStore,
                    (logicalControllerId, savedProfile) =>
                    {
                        BackendManager manager = (App.Current as App).Manager;
                        manager.UniversalMappingRuntime?.SwitchProfile(logicalControllerId, savedProfile);
                    });

                Guid? activeControllerId = currentDeviceItem.UniversalSession?.LogicalControllerId;
                UniversalProfileEditorSaveResult result = coordinator.SaveProfile(
                    profile,
                    activeControllerId,
                    universalProfileStore.GetNamedProfilePath(profile.DisplayName, overlayNewProfileVM.SelectedFolderName));
                if (!result.Success)
                {
                    MessageBox.Show(string.Join("\n", result.Issues.Select(issue => issue.Message)),
                        "Create Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                HideNewProfilePanel();
                RefreshUniversalProfileLists();

                string newPath = universalProfileStore.FindProfilePath(profile.ProfileId);
                ProfileEntity newEnt = currentDeviceItem.DevProfileList.FirstOrDefault(p =>
                    string.Equals(p.ProfilePath, newPath, StringComparison.OrdinalIgnoreCase));
                if (newEnt != null)
                {
                    int newIndex = currentDeviceItem.DevProfileList.IndexOf(newEnt);
                    // Await the switch (rather than fire-and-forget it) so the
                    // browser refresh below runs after the new profile has
                    // actually landed in the list, instead of racing it and
                    // redrawing the overlay from a stale, pre-creation snapshot
                    // that then never gets rebuilt until the overlay is reopened.
                    await SwitchProfileAsync(currentDeviceItem, newIndex);
                }

                RefreshProfileCombo();
                RefreshProfileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create profile:\n{ex.Message}", "Create Profile",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static UniversalProfile CreateBlankUniversalProfile(string profileName)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = profileName,
                Description = profileName,
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Main" };
            set.Layers.Add(new UniversalProfileActionLayer { Index = 0, Name = "Default" });
            profile.ActionSets.Add(set);
            return profile;
        }

        private void NewProfileBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (overlayNewProfileVM == null) return;

            OpenFolderDialog folderDialog = new OpenFolderDialog
            {
                InitialDirectory = overlayNewProfileVM.ProfileFolder
            };

            if (folderDialog.ShowDialog() != true) return;

            overlayNewProfileVM.ProfileFolder = folderDialog.FolderName;
            overlayNewProfileVM.ClearOldErrors();
        }

        private void CreateFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null) return;

            string folderName = newFolderNameBox.Text?.Trim();
            if (!ValidateFolderName(folderName, "Create Folder")) return;

            if (CurrentProfileFolderExists(folderName))
            {
                MessageBox.Show("A folder with this name already exists.", "Create Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentDeviceItem.IsUniversal)
            {
                currentDeviceItem.UniversalProfileListHolder?.CreateFolder(folderName);
            }
            else
            {
                currentDeviceItem.ProfileListHolder.CreateFolder(folderName);
            }

            newFolderNameBox.Text = string.Empty;
            if (currentDeviceItem.IsUniversal)
            {
                RefreshUniversalProfileLists();
            }

            RefreshProfileCombo();
            RefreshProfileList();
        }

        private void RenameFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || folderManageComboBox.SelectedItem is not string oldFolderName) return;

            string newFolderName = folderRenameBox.Text?.Trim();
            if (!ValidateFolderName(newFolderName, "Rename Folder")) return;

            if (string.Equals(oldFolderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The Default folder cannot be renamed.", "Rename Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CurrentProfileFolderExists(newFolderName))
            {
                MessageBox.Show("A folder with this name already exists.", "Rename Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (currentDeviceItem.IsUniversal)
                {
                    currentDeviceItem.UniversalProfileListHolder?.RenameFolder(oldFolderName, newFolderName);
                    RefreshUniversalProfileLists();
                }
                else
                {
                    currentDeviceItem.ProfileListHolder.RenameFolder(oldFolderName, newFolderName);
                }

                RefreshProfileCombo();
                RefreshProfileList(newFolderName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename folder:\n{ex.Message}", "Rename Folder",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || folderManageComboBox.SelectedItem is not string folderName) return;

            if (string.Equals(folderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The Default folder cannot be deleted.", "Delete Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentDeviceItem.DevProfileList.Any(p => string.Equals(p.FolderName, folderName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Move or delete the profiles in this folder first.", "Delete Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Delete folder \"{folderName}\"?", "Delete Folder",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                bool deleted = currentDeviceItem.IsUniversal
                    ? currentDeviceItem.UniversalProfileListHolder?.DeleteFolder(folderName) == true
                    : currentDeviceItem.ProfileListHolder.DeleteFolder(folderName);

                if (currentDeviceItem.IsUniversal)
                {
                    RefreshUniversalProfileLists();
                }

                RefreshProfileCombo();
                RefreshProfileList();

                if (!deleted)
                {
                    MessageBox.Show("This folder still holds profiles. Move or delete them first.",
                        "Delete Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete folder:\n{ex.Message}", "Delete Folder",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CurrentProfileFolderExists(string folderName)
        {
            if (currentDeviceItem == null) return false;

            return currentDeviceItem.IsUniversal
                ? currentDeviceItem.UniversalProfileListHolder?.FolderExists(folderName) == true
                : currentDeviceItem.ProfileListHolder.FolderExists(folderName);
        }

        private void FolderManageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            folderRenameBox.Text = folderManageComboBox.SelectedItem as string ?? string.Empty;
            if (!suppressFolderManageStatusHide)
            {
                HideResetDefaultProfilesStatus();
            }
            RefreshDefaultProfileResetVisibility();
        }

        private async void ResetDefaultProfilesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;
            if (currentDeviceItem.IsUniversal) return;

            string selectedFolder = folderManageComboBox.SelectedItem as string;
            if (!string.Equals(selectedFolder, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (MessageBox.Show(
                    "Reset the bundled default profiles for this controller?\n\nThis will overwrite changes made to profiles in the Default folder.",
                    "Reset Default Profiles",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            string activeProfilePath = editorTestVM.ProfileEnt?.ProfilePath;
            string selectedProfilePath = selectedListEntry?.Entity?.ProfilePath ?? activeProfilePath;
            string activeProfileName = editorTestVM.ProfileEnt?.Name;
            string selectedProfileName = selectedListEntry?.Name ?? activeProfileName;
            bool activeDefaultProfile = currentDeviceItem.DevProfileList.Any(profile =>
                string.Equals(profile.ProfilePath, activeProfilePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.FolderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase));

            if (activeDefaultProfile && editorTestVM.IsProfileDirty)
            {
                DirtySwitchDecision decision = ShowDirtySwitchDialog(
                    allowSave: false,
                    title: "Reset Default Profiles",
                    messageText: "Resetting defaults will discard unsaved changes to the current profile.");
                if (decision != DirtySwitchDecision.Discard)
                {
                    return;
                }
            }

            IsEnabled = false;
            Exception resetException = null;
            int resetCount = 0;
            try
            {
                InputDeviceType deviceType = editorTestVM.DeviceMapper.DeviceType;
                resetCount = await Task.Run(() => appGlobal.ResetBundledDefaultProfiles(deviceType));
                currentDeviceItem.ProfileListHolder.Refresh();

                if (activeDefaultProfile)
                {
                    ProfileEntity restoredActiveProfile = FindRestoredDefaultProfile(activeProfilePath, activeProfileName) ??
                        currentDeviceItem.DevProfileList.FirstOrDefault(profile =>
                            string.Equals(profile.FolderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase));

                    if (restoredActiveProfile != null)
                    {
                        int activeIndex = currentDeviceItem.DevProfileList.IndexOf(restoredActiveProfile);
                        await Task.Run(() => currentDeviceItem.ResyncProfileIndex(activeIndex, reloadProfile: true));
                        appGlobal.activeProfiles[currentDeviceItem.Device.Index] = restoredActiveProfile.ProfilePath;
                        selectedProfilePath = restoredActiveProfile.ProfilePath;
                        LoadProfileForDevice(currentDeviceItem);
                    }
                }
            }
            catch (Exception ex)
            {
                resetException = ex;
            }
            finally
            {
                IsEnabled = true;
            }

            if (resetException != null)
            {
                saveProfileLogger.Error(resetException, "Failed to reset default profiles");
                MessageBox.Show($"Failed to reset default profiles:\n{resetException.Message}", "Reset Default Profiles",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (resetCount == 0)
            {
                MessageBox.Show("No bundled default profiles were found for this controller.", "Reset Default Profiles",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            RefreshProfileCombo();
            selectedProfilePath = FindRestoredDefaultProfile(selectedProfilePath, selectedProfileName)?.ProfilePath ?? selectedProfilePath;
            RefreshProfileList(ProfileList.DEFAULT_PROFILE_FOLDER, selectedProfilePath);
            if (resetCount > 0)
            {
                ShowResetDefaultProfilesStatus();
            }

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                SelectManagedProfileFolder(ProfileList.DEFAULT_PROFILE_FOLDER, preserveResetStatus: true);
                if (resetCount > 0)
                {
                    ShowResetDefaultProfilesStatus();
                }
            }), DispatcherPriority.ContextIdle);
        }

        private ProfileEntity FindRestoredDefaultProfile(string profilePath, string profileName)
        {
            if (currentDeviceItem == null) return null;

            return currentDeviceItem.DevProfileList.FirstOrDefault(profile =>
                    string.Equals(profile.FolderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(profile.ProfilePath, profilePath, StringComparison.OrdinalIgnoreCase)) ??
                currentDeviceItem.DevProfileList.FirstOrDefault(profile =>
                    string.Equals(profile.FolderName, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
        }

        private void SelectedProfileFolderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSelectedProfileFolderCombo || currentDeviceItem == null || selectedListEntry == null) return;

            string folderName = selectedProfileFolderComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(folderName) ||
                string.Equals(folderName, selectedListEntry.FolderName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Captured before the move: the entity's path is rewritten in place
            // by MoveProfile, and it is the pre-move path that tells us whether
            // the profile being moved is the one the editor has open.
            string previousProfilePath = selectedListEntry.ProfilePath;

            try
            {
                bool moved = currentDeviceItem.IsUniversal
                    ? currentDeviceItem.UniversalProfileListHolder?.MoveProfile(selectedListEntry.Entity, folderName) == true
                    : currentDeviceItem.ProfileListHolder.MoveProfile(selectedListEntry.Entity, folderName);
                if (!moved)
                {
                    MessageBox.Show("A profile with this filename already exists in that folder.", "Move Profile",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    selectedProfileFolderComboBox.SelectedItem = selectedListEntry.FolderName;
                    return;
                }

                ProfileEntity activeEntity = editorTestVM?.ProfileEnt;
                if (activeEntity != null &&
                    string.Equals(activeEntity.ProfilePath, previousProfilePath, StringComparison.OrdinalIgnoreCase))
                {
                    string newProfilePath = selectedListEntry.Entity.ProfilePath;
                    // The editor may be holding a different entity object for
                    // the same profile, and it is the one a later save reads
                    // the path from, so it has to follow the file too.
                    activeEntity.UpdatePath(newProfilePath);
                    activeEntity.FolderName = folderName;

                    Mapper mapper = editorTestVM.DeviceMapper;
                    mapper.ProfileFile = newProfilePath;
                    appGlobal.activeProfiles[currentDeviceItem.Device.Index] = newProfilePath;
                }

                if (currentDeviceItem.IsUniversal)
                {
                    RefreshUniversalProfileLists();
                }

                RefreshProfileCombo();
                RefreshProfileList(folderName, selectedListEntry.ProfilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to move profile:\n{ex.Message}", "Move Profile",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateFolderName(string folderName, string title)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                MessageBox.Show("Folder name cannot be empty.", title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                folderName.Contains(Path.DirectorySeparatorChar) ||
                folderName.Contains(Path.AltDirectorySeparatorChar) ||
                folderName.Trim() == "." ||
                folderName.Trim() == "..")
            {
                MessageBox.Show("Folder name contains invalid characters.", title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void RefreshFolderManagementControls(string selectedFolderName = null)
        {
            if (currentDeviceItem == null) return;

            string folderToSelect = selectedFolderName ?? folderManageComboBox.SelectedItem as string;
            List<string> folderItems = GetProfileFolderSnapshot();
            folderManageComboBox.ItemsSource = folderItems;
            selectedProfileFolderComboBox.ItemsSource = folderItems;
            if (!SelectManagedProfileFolder(folderToSelect))
            {
                SelectManagedProfileFolder(folderItems.FirstOrDefault());
            }

            RefreshDefaultProfileResetVisibility();
        }

        private List<string> GetProfileFolderSnapshot()
        {
            return currentDeviceItem?.ProfileFolders?.ToList() ?? new List<string>();
        }

        private bool SelectManagedProfileFolder(string folderName, bool preserveResetStatus = false)
        {
            if (folderManageComboBox == null || currentDeviceItem == null || string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            List<string> folderItems = folderManageComboBox.Items.Cast<string>().ToList();
            int folderIndex = folderItems
                .Select((folder, index) => new { folder, index })
                .Where(item => string.Equals(item.folder, folderName, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1)
                .First();

            if (folderIndex < 0) return false;

            suppressFolderManageStatusHide = preserveResetStatus;
            try
            {
                folderManageComboBox.SelectedIndex = folderIndex;
                folderRenameBox.Text = folderItems[folderIndex];
            }
            finally
            {
                suppressFolderManageStatusHide = false;
            }

            RefreshDefaultProfileResetVisibility();
            return true;
        }

        private void RefreshDefaultProfileResetVisibility()
        {
            if (resetDefaultProfilesPanel == null || folderManageComboBox == null) return;

            // Resetting bundled defaults rewrites the legacy per-device profile
            // files, which a universal controller does not use, so the button
            // did nothing at all when it was shown for one. Keep it hidden
            // rather than offering an action that cannot happen.
            string selectedFolder = folderManageComboBox.SelectedItem as string;
            resetDefaultProfilesPanel.Visibility =
                currentDeviceItem?.IsUniversal != true &&
                string.Equals(selectedFolder, ProfileList.DEFAULT_PROFILE_FOLDER, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void HideResetDefaultProfilesStatus()
        {
            if (resetDefaultProfilesStatusText == null) return;

            resetDefaultProfilesStatusHideTimer?.Stop();
            resetDefaultProfilesStatusText.Visibility = Visibility.Collapsed;
        }

        private void ShowResetDefaultProfilesStatus()
        {
            if (resetDefaultProfilesStatusText == null) return;

            resetDefaultProfilesStatusHideTimer?.Stop();
            resetDefaultProfilesStatusText.Visibility = Visibility.Visible;

            resetDefaultProfilesStatusHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            resetDefaultProfilesStatusHideTimer.Tick += (s, e) =>
            {
                resetDefaultProfilesStatusHideTimer.Stop();
                resetDefaultProfilesStatusText.Visibility = Visibility.Collapsed;
            };
            resetDefaultProfilesStatusHideTimer.Start();
        }

        private void HideNewProfilePanel()
        {
            overlayNewProfileVM?.ClearOldErrors();
            overlayNewProfileVM = null;
            newProfilePanel.DataContext = null;
            newProfilePanel.Visibility = Visibility.Collapsed;
        }

        private void CopySelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;

            string sourceFile = selectedListEntry?.Entity?.ProfilePath;
            if (string.IsNullOrWhiteSpace(sourceFile)) return;

            if (currentDeviceItem.IsUniversal)
            {
                CopySelectedUniversalProfile(sourceFile);
                return;
            }

            try
            {
                string destFile = BuildLegacyProfileCopyPath(sourceFile);
                controlListVM.DuplicateProfile(currentDeviceItem, sourceFile, destFile);
                RefreshProfileList(selectedListEntry.FolderName, destFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // A copy belongs beside the profile it came from, so it keeps the
        // source's folder and only the filename is worked out here. Legacy
        // profile names are filenames, so uniqueness is decided against disk
        // rather than against the display names the universal store tracks.
        private static string BuildLegacyProfileCopyPath(string sourceFile)
        {
            string folder = Path.GetDirectoryName(sourceFile);
            string baseName = Path.GetFileNameWithoutExtension(sourceFile) +
                UniversalProfileDuplicator.CopyNameSuffix;

            string candidate = Path.Combine(folder, baseName + ".json");
            for (int suffix = 2; File.Exists(candidate) && suffix < 1000; suffix++)
            {
                candidate = Path.Combine(folder, $"{baseName} ({suffix}).json");
            }

            return candidate;
        }

        // Universal profiles are one shared, controller-independent set whose
        // filenames are owned by the store, so a copy is named and placed by
        // the store rather than through a Save As picker.
        private void CopySelectedUniversalProfile(string sourcePath)
        {
            try
            {
                string folderName = universalProfileStore.GetFolderName(sourcePath);
                UniversalProfile copy = UniversalProfileDuplicator.PrepareCopy(
                    universalProfileStore.LoadFromPath(sourcePath),
                    universalProfileStore.EnumerateProfileSummaries());

                universalProfileStore.SaveNamed(copy,
                    universalProfileStore.GetNamedProfilePath(copy.DisplayName, folderName));

                RefreshUniversalProfileLists();
                RefreshProfileCombo();
                RefreshProfileList(folderName, universalProfileStore.FindProfilePath(copy.ProfileId));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportUniversalProfileFromFile()
        {
            string profilesDir = universalProfileStore.RootPath;
            Directory.CreateDirectory(profilesDir);

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Load Profile from File",
                Filter = $"Universal profiles (*{UniversalProfileStore.ProfileFileExtension})|*{UniversalProfileStore.ProfileFileExtension}|JSON files (*.json)|*.json",
                InitialDirectory = profilesDir,
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                string sourcePath = Path.GetFullPath(dlg.FileName);
                ProfileEntity alreadyListed = currentDeviceItem.DevProfileList.FirstOrDefault(profile =>
                    string.Equals(profile.ProfilePath, sourcePath, StringComparison.OrdinalIgnoreCase));
                if (alreadyListed != null)
                {
                    RefreshProfileList(alreadyListed.FolderName, alreadyListed.ProfilePath);
                    return;
                }

                UniversalProfile imported = UniversalProfileDuplicator.PrepareImport(
                    UniversalProfileSerializer.Deserialize(File.ReadAllText(sourcePath)),
                    universalProfileStore.EnumerateProfileSummaries());

                string folderName = ProfileList.DEFAULT_PROFILE_FOLDER;
                universalProfileStore.SaveNamed(imported,
                    universalProfileStore.GetNamedProfilePath(imported.DisplayName, folderName));

                RefreshUniversalProfileLists();
                RefreshProfileCombo();
                RefreshProfileList(folderName, universalProfileStore.FindProfilePath(imported.ProfileId));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProfileFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;

            if (currentDeviceItem.IsUniversal)
            {
                ImportUniversalProfileFromFile();
                return;
            }

            Mapper mapper = editorTestVM.DeviceMapper;
            BackendManager manager = (App.Current as App).Manager;
            ProfileList profileListHolder = manager.DeviceProfileListDict[mapper.DeviceType];
            string profilesDir = profileListHolder.GetDeviceProfileRoot();
            string importDir = profileListHolder.GetFolderPath(ProfileList.VALORANT_PROFILE_FOLDER);
            Directory.CreateDirectory(importDir);

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Load Profile from File",
                Filter = "JSON files (*.json)|*.json",
                InitialDirectory = profilesDir
            };

            if (dlg.ShowDialog() != true) return;

            string srcFile = dlg.FileName;
            string destFile = srcFile;

            bool sourceInsideProfileRoot = Path.GetFullPath(srcFile)
                .StartsWith(Path.GetFullPath(profilesDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
            if (!sourceInsideProfileRoot)
            {
                destFile = Path.Combine(importDir, Path.GetFileName(srcFile));
                if (File.Exists(destFile))
                {
                    MessageBox.Show("A profile with that filename already exists in the VALORANT folder.", "Cannot Import",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                File.Copy(srcFile, destFile);
            }

            var profileList = manager.DeviceProfileListDict[mapper.DeviceType].ProfileListCol;

            if (profileList.Any(p => string.Equals(p.ProfilePath, destFile, StringComparison.OrdinalIgnoreCase)))
            {
                RefreshProfileList();
                return;
            }

            try
            {
                string json = File.ReadAllText(destFile);
                ProfilePreview preview = JsonConvert.DeserializeObject<ProfilePreview>(json);
                string profileName = preview?.Name ?? Path.GetFileNameWithoutExtension(destFile);
                manager.DeviceProfileListDict[mapper.DeviceType].CreateProfileItem(destFile, profileName, mapper.DeviceType);
                RefreshProfileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadProfileFromListBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadSelectedProfileFromListAsync();
        }

        private async Task LoadSelectedProfileFromListAsync()
        {
            if (selectedListEntry == null || currentDeviceItem == null) return;

            var profileList = currentDeviceItem.DevProfileList;
            string selectedPath = selectedListEntry.Entity?.ProfilePath;
            string activePath = editorTestVM?.ProfileEnt?.ProfilePath;
            if (string.IsNullOrWhiteSpace(selectedPath) ||
                string.Equals(selectedPath, activePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int newIndex = profileList
                .Select((profile, index) => new { profile, index })
                .Where(item => string.Equals(item.profile.ProfilePath, selectedPath, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1)
                .First();
            if (newIndex < 0) return;

            // Loading redraws the browser from scratch, which used to drop the
            // selection and collapse the Name/Folder/Delete panel with it, so the
            // profile the user had just loaded stopped being the selected one.
            // Re-select it afterwards: loading a profile is usually the start of
            // editing it, not the end of working with the browser.
            string selectedFolderName = selectedListEntry.FolderName;
            if (await SwitchProfileAsync(currentDeviceItem, newIndex))
            {
                HideNewProfilePanel();
                RefreshProfileList(selectedFolderName, selectedPath);
            }
        }

        private void RenameProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedListEntry == null) return;

            string newName = profileRenameBox.Text?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Profile name cannot be empty.", "Rename",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentDeviceItem?.IsUniversal == true)
            {
                RenameUniversalProfile(selectedListEntry.Entity, newName);
                return;
            }

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("Profile name contains invalid characters.", "Rename",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProfileEntity ent = selectedListEntry.Entity;
            string newPath = Path.Combine(Path.GetDirectoryName(ent.ProfilePath), newName + ".json");
            bool pathChanging = !string.Equals(ent.ProfilePath, newPath, StringComparison.OrdinalIgnoreCase);

            if (pathChanging && File.Exists(newPath))
            {
                MessageBox.Show("A profile with this name already exists.", "Rename",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string json = File.ReadAllText(ent.ProfilePath);
                JObject root = JObject.Parse(json);
                root["Name"] = newName;
                using (StreamWriter writer = new StreamWriter(ent.ProfilePath))
                using (JsonTextWriter jwriter = new JsonTextWriter(writer))
                {
                    jwriter.Formatting = Formatting.Indented;
                    jwriter.Indentation = 2;
                    JObject.Parse(root.ToString()).WriteTo(jwriter);
                }

                // The displayed name and the file it lives in must always match,
                // otherwise the profile shown as "Foo" keeps living in a file
                // still called "Bar.json" on disk. Move the file to match the
                // new name rather than only updating the JSON content.
                if (pathChanging)
                {
                    File.Move(ent.ProfilePath, newPath);
                    ent.UpdatePath(newPath);
                }

                ent.Name = newName;

                if (editorTestVM != null && ent == editorTestVM.ProfileEnt)
                {
                    editorTestVM.SetProfileNameWithoutDirty(newName);
                }

                RefreshProfileCombo();
                RefreshProfileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenameUniversalProfile(ProfileEntity ent, string newName)
        {
            try
            {
                UniversalProfile profile = universalProfileStore.LoadFromPath(ent.ProfilePath);
                string oldPath = ent.ProfilePath;
                profile.DisplayName = newName;
                UniversalProfileEditorSaveCoordinator coordinator = new UniversalProfileEditorSaveCoordinator(
                    universalProfileStore,
                    (logicalControllerId, savedProfile) =>
                    {
                        BackendManager manager = (App.Current as App).Manager;
                        manager.UniversalMappingRuntime?.SwitchProfile(logicalControllerId, savedProfile);
                    });

                Guid? reloadId = editorTestVM?.ProfileEnt == ent
                    ? currentDeviceItem?.UniversalSession?.LogicalControllerId
                    : null;
                UniversalProfileEditorSaveResult result = coordinator.SaveProfile(profile, reloadId, oldPath);
                if (!result.Success)
                {
                    MessageBox.Show(string.Join("\n", result.Issues.Select(issue => issue.Message)),
                        "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newPath = universalProfileStore.FindProfilePath(profile.ProfileId) ??
                    universalProfileStore.GetNamedProfilePath(profile.DisplayName);
                ent.UpdatePath(newPath);
                ent.Name = profile.DisplayName;

                if (editorTestVM != null && ent == editorTestVM.ProfileEnt)
                {
                    editorTestVM.SetProfileNameWithoutDirty(profile.DisplayName);
                }

                RefreshUniversalProfileLists();
                RefreshProfileCombo();
                RefreshProfileList();
                RebindEditorToLiveProfile();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedListEntry == null || currentDeviceItem == null) return;

            ProfileEntity ent = selectedListEntry.Entity;
            var profileList = currentDeviceItem.DevProfileList;
            ProfileEntity activeEnt = editorTestVM?.ProfileEnt;
            bool isActive = string.Equals(ent.ProfilePath, activeEnt?.ProfilePath, StringComparison.OrdinalIgnoreCase);

            // Deleting the active profile used to remove it and reload a replacement
            // in its place, but that reload raced ChangeProfile against the mapper's
            // own input-thread halt window and could leave the window disabled with
            // no way to recover. Rather than chase that race further, require the
            // user to switch away first: a profile can't be pulled out from under
            // itself while it's the one actually loaded.
            if (isActive)
            {
                ShowDeleteActiveProfileWarning();
                return;
            }

            if (profileList.Count <= 1)
            {
                MessageBox.Show("Cannot delete the only remaining profile.", "Delete",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete \"{ent.Name}\"?\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (currentDeviceItem.IsUniversal)
                {
                    universalProfileStore.Delete(ent.ProfilePath);
                }
                else
                {
                    File.Delete(ent.ProfilePath);
                }
            }
            catch (Exception ex)
            {
                saveProfileLogger.Error(ex, "Failed to delete profile");
                MessageBox.Show($"Failed to delete profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            suppressCombo = true;
            if (currentDeviceItem.IsUniversal)
            {
                RefreshUniversalProfileLists();
            }
            else
            {
                profileList.Remove(ent);
            }
            selectedListEntry = null;
            if (activeEnt != null)
            {
                int activeIndex = profileList.IndexOf(activeEnt);
                if (activeIndex >= 0)
                {
                    currentDeviceItem.ResyncProfileIndex(activeIndex, reloadProfile: false);
                }
            }
            suppressCombo = false;

            // The browser is what the user is looking at when they hit Delete, so
            // refresh it first: the deleted profile has to disappear on this
            // attempt even if redrawing the top-bar combo hits trouble.
            RefreshProfileList();
            RefreshProfileCombo();
        }

        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveCurrentProfileAsync();
        }

        private async void DiscardProfileChangesButton_Click(object sender, RoutedEventArgs e)
        {
            await DiscardCurrentProfileChangesAsync();
        }

        private async Task<bool> SaveCurrentProfileAsync()
        {
            if (editorTestVM == null || isSavingProfile) return false;

            isSavingProfile = true;
            saveStatusHideTimer?.Stop();
            HideSaveStatusPill(animate: false);

            ProfileEditorTestViewModel activeVM = editorTestVM;
            saveProfileButton.Content = "Saving...";
            saveProfileButton.IsEnabled = false;
            IsEnabled = false;

            Exception saveException = null;
            UniversalProfileSaveUiUpdate universalSaveUpdate = null;
            try
            {
                if (activeVM.DeviceMapper is UniversalMapper universalMapper)
                {
                    universalSaveUpdate = await Task.Run(() => SaveUniversalProfileFromClassicEditor(activeVM, universalMapper));
                }
                else
                {
                    await Task.Run(() => activeVM.TestSave(activeVM.ProfileEnt, activeVM.DeviceMapper.ActionProfile));
                }
            }
            catch (Exception ex)
            {
                saveException = ex;
            }
            finally
            {
                IsEnabled = true;
                saveProfileButton.IsEnabled = true;
                isSavingProfile = false;
            }

            if (saveException == null)
            {
                if (universalSaveUpdate != null)
                {
                    activeVM.ProfileEnt.UpdatePath(universalSaveUpdate.ProfilePath);
                    activeVM.ProfileEnt.Name = universalSaveUpdate.DisplayName;
                    RefreshUniversalProfileLists();
                    RefreshProfileCombo();
                    RefreshProfileList();

                    // The save handed the stored profile back to the live mapper, which
                    // recompiled it and left activeVM editing the graph it replaced.
                    if (ReferenceEquals(editorTestVM, activeVM))
                    {
                        RebindEditorToLiveProfile();
                    }
                }

                (editorTestVM ?? activeVM).MarkProfileClean();
                saveProfileButton.Content = "Saved ✓";
                ShowSaveStatusPill(success: true);
                StartSaveStatusHideTimer(TimeSpan.FromSeconds(2.5), revertButton: true);
                return true;
            }
            else
            {
                saveProfileLogger.Error(saveException, "Failed to save profile");
                saveProfileButton.Content = "Save Profile";
                ShowSaveStatusPill(success: false);
                StartSaveStatusHideTimer(TimeSpan.FromSeconds(6), revertButton: false);

                // A "Save failed" pill on its own leaves the user with edits that
                // silently never reached disk and no way to tell why. Every other
                // profile operation reports its reason, so this one must too.
                MessageBox.Show(
                    $"Failed to save the profile:\n{saveException.Message}",
                    "Save Profile",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private UniversalProfileSaveUiUpdate SaveUniversalProfileFromClassicEditor(ProfileEditorTestViewModel activeVM, UniversalMapper universalMapper)
        {
            UniversalProfile latest = universalProfileStore.LoadFromPath(activeVM.ProfileEnt.ProfilePath);

            // UniversalMapper.BaseReader is always null, so Mapper.ProcessMappingChangeAction
            // (the halt used by the classic TestSave path) never actually pauses anything here.
            // The real input loop for a live session runs under UniversalMapperSession.syncRoot
            // (see ProcessCurrentState), so the projector's read of the still-live ActionProfile
            // has to take that same lock or it races the controller's own polling thread.
            UniversalProfile updated = null;
            UniversalMapperSession session = currentDeviceItem?.UniversalSession;
            if (session != null)
            {
                session.RunExclusive(() =>
                {
                    updated = UniversalClassicProfileProjector.BuildUpdatedProfile(
                        universalMapper,
                        activeVM.DeviceMapper.ActionProfile,
                        latest);
                });
            }
            else
            {
                updated = UniversalClassicProfileProjector.BuildUpdatedProfile(
                    universalMapper,
                    activeVM.DeviceMapper.ActionProfile,
                    latest);
            }

            string oldPath = activeVM.ProfileEnt.ProfilePath;
            UniversalProfileEditorSaveCoordinator coordinator = new UniversalProfileEditorSaveCoordinator(
                universalProfileStore,
                (logicalControllerId, savedProfile) =>
                {
                    BackendManager manager = (App.Current as App).Manager;
                    manager.UniversalMappingRuntime?.SwitchProfile(logicalControllerId, savedProfile);
                });

            UniversalProfileEditorSaveResult result = coordinator.SaveProfile(
                updated,
                currentDeviceItem?.UniversalSession?.LogicalControllerId,
                oldPath);
            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine,
                    result.Issues.Select(issue => $"{issue.Location}: {issue.Message}")));
            }

            string newPath = universalProfileStore.FindProfilePath(updated.ProfileId) ??
                universalProfileStore.GetNamedProfilePath(updated.DisplayName);
            return new UniversalProfileSaveUiUpdate
            {
                ProfilePath = newPath,
                DisplayName = updated.DisplayName,
            };
        }

        private async Task<bool> DiscardCurrentProfileChangesAsync()
        {
            if (editorTestVM?.IsProfileDirty != true || currentDeviceItem == null) return true;

            DirtySwitchDecision decision = ShowDirtySwitchDialog(
                allowSave: false,
                title: "Discard Changes",
                messageText: "Discard all unsaved changes to the current profile?");
            if (decision != DirtySwitchDecision.Discard) return false;

            IsEnabled = false;
            saveStatusHideTimer?.Stop();
            HideSaveStatusPill(animate: false);
            InlineBindingEditorService.CloseAny();
            ExitRenameSetMode();
            ExitRenameLayerMode();

            bool discarded = false;
            Exception discardException = null;
            try
            {
                int profileIndex = currentDeviceItem.ProfileIndex;
                await Task.Run(() => currentDeviceItem.ResyncProfileIndex(profileIndex, reloadProfile: true));
                discarded = LoadProfileForDevice(currentDeviceItem);
            }
            catch (Exception ex)
            {
                discardException = ex;
            }

            saveProfileButton.Content = "Save Profile";
            IsEnabled = true;

            if (!discarded)
            {
                MessageBox.Show(
                    discardException == null
                        ? "Failed to reload the current profile."
                        : $"Failed to reload the current profile:\n{discardException.Message}",
                    "Discard Changes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return discarded;
        }

        private async Task<bool> ConfirmDiscardProfileChangesAsync()
        {
            if (editorTestVM?.IsProfileDirty != true) return true;

            DirtySwitchDecision decision = ShowDirtySwitchDialog();
            switch (decision)
            {
                case DirtySwitchDecision.Save:
                    return await SaveCurrentProfileAsync();
                case DirtySwitchDecision.Discard:
                    return true;
                default:
                    return false;
            }
        }

        private DirtySwitchDecision ShowDirtySwitchDialog(
            bool allowSave = true,
            string title = "Unsaved Changes",
            string messageText = "The current profile has unsaved changes.")
        {
            DirtySwitchDecision decision = DirtySwitchDecision.Cancel;
            Window dialog = new Window
            {
                Owner = this,
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("JsmccBg1Brush"),
            };

            StackPanel root = new StackPanel { Width = 360 };
            TextBlock message = new TextBlock
            {
                Text = messageText,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
            };
            message.Style = (Style)FindResource("JsmccBodyText");
            root.Children.Add(message);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            Button saveButton = allowSave ? CreateDirtyDialogButton("Save", "JsmccPrimaryButtonStyle") : null;
            Button discardButton = CreateDirtyDialogButton("Discard", "JsmccDangerButtonStyle");
            Button cancelButton = CreateDirtyDialogButton("Cancel", "JsmccButtonStyle");

            if (saveButton != null)
            {
                saveButton.Click += (_, _) =>
                {
                    decision = DirtySwitchDecision.Save;
                    dialog.DialogResult = true;
                };
            }
            discardButton.Click += (_, _) =>
            {
                decision = DirtySwitchDecision.Discard;
                dialog.DialogResult = true;
            };
            cancelButton.Click += (_, _) =>
            {
                decision = DirtySwitchDecision.Cancel;
                dialog.DialogResult = false;
            };

            if (saveButton != null)
            {
                buttons.Children.Add(saveButton);
            }
            buttons.Children.Add(discardButton);
            buttons.Children.Add(cancelButton);
            root.Children.Add(buttons);

            dialog.Content = new Border
            {
                Padding = new Thickness(18),
                Child = root,
            };
            dialog.ShowDialog();

            return decision;
        }

        private Button CreateDirtyDialogButton(string content, string styleKey)
        {
            return new Button
            {
                Content = content,
                Style = (Style)FindResource(styleKey),
                MinWidth = 82,
                Margin = new Thickness(8, 0, 0, 0),
            };
        }

        private void ShowDeleteActiveProfileWarning()
        {
            deleteActiveProfileWarningHideTimer?.Stop();
            deleteActiveProfileWarningText.Visibility = Visibility.Visible;

            deleteActiveProfileWarningHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            deleteActiveProfileWarningHideTimer.Tick += (s, e) =>
            {
                deleteActiveProfileWarningHideTimer.Stop();
                deleteActiveProfileWarningText.Visibility = Visibility.Collapsed;
            };
            deleteActiveProfileWarningHideTimer.Start();
        }

        private void HideDeleteActiveProfileWarning()
        {
            deleteActiveProfileWarningHideTimer?.Stop();
            deleteActiveProfileWarningText.Visibility = Visibility.Collapsed;
        }

        private void StartSaveStatusHideTimer(TimeSpan delay, bool revertButton)
        {
            saveStatusHideTimer = new DispatcherTimer { Interval = delay };
            saveStatusHideTimer.Tick += (s, e) =>
            {
                saveStatusHideTimer.Stop();
                HideSaveStatusPill(animate: true);
                if (revertButton)
                {
                    saveProfileButton.Content = "Save Profile";
                }
            };
            saveStatusHideTimer.Start();
        }

        private void ShowSaveStatusPill(bool success)
        {
            saveStatusPill.Style = (Style)FindResource(success ? "SaveStatusPillSuccessStyle" : "SaveStatusPillErrorStyle");
            saveStatusPillText.Style = (Style)FindResource(success ? "SaveStatusPillTextSuccessStyle" : "SaveStatusPillTextErrorStyle");
            saveStatusPillText.Text = success ? "Saved ✓" : "Save failed";
            saveStatusPill.ToolTip = success
                ? $"Saved at {DateTime.Now:HH:mm:ss}"
                : "Check the log for details.";

            saveStatusPill.Visibility = Visibility.Visible;
            saveStatusPill.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        private void HideSaveStatusPill(bool animate)
        {
            if (saveStatusPill.Visibility != Visibility.Visible) return;

            if (!animate)
            {
                saveStatusPill.BeginAnimation(OpacityProperty, null);
                saveStatusPill.Visibility = Visibility.Collapsed;
                return;
            }

            DoubleAnimation fadeOut = new DoubleAnimation(saveStatusPill.Opacity, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) =>
            {
                saveStatusPill.Visibility = Visibility.Collapsed;
            };
            saveStatusPill.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void LightbarPreset_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null || sender is not Button button || button.Tag is not string hexColor) return;
            editorTestVM.LightbarHexColor = hexColor;
        }

        private void LightbarPulsePreset_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null || sender is not Button button || button.Tag is not string hexColor) return;
            editorTestVM.LightbarPulseHexColor = hexColor;
        }

        private void LightbarBatteryPreset_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null || sender is not Button button || button.Tag is not string hexColor) return;
            editorTestVM.LightbarBatteryHexColor = hexColor;
        }


        private void ControlListVM_ReadProfileFailure(object sender, ReadProfileFailException e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                MessageBox.Show($"{e.ExtraMessage}\n\n{e.InnerJsonException.Message}",
                    "Profile read failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (isDirtyClosePromptActive)
            {
                e.Cancel = true;
                return;
            }

            if (editorTestVM?.IsProfileDirty != true) return;

            // Resolve e.Cancel synchronously, in this same dispatch, rather than
            // cancelling and calling Close() again afterwards. A second Close() call
            // (whether direct or queued via the dispatcher) depends on WPF/dispatcher
            // reentrancy behaviour that proved unreliable after repeated cancelled
            // close attempts on the same window. Since the dialog and the save below
            // are both synchronous, there is nothing to await here.
            isDirtyClosePromptActive = true;
            try
            {
                DirtySwitchDecision decision = ShowDirtySwitchDialog();
                switch (decision)
                {
                    case DirtySwitchDecision.Discard:
                        e.Cancel = false;
                        break;
                    case DirtySwitchDecision.Save:
                        e.Cancel = !SaveCurrentProfileForClose();
                        break;
                    default:
                        e.Cancel = true;
                        break;
                }
            }
            finally
            {
                isDirtyClosePromptActive = false;
            }
        }

        private bool SaveCurrentProfileForClose()
        {
            if (editorTestVM == null) return false;

            ProfileEditorTestViewModel activeVM = editorTestVM;
            try
            {
                // Same split as SaveCurrentProfileAsync. A universal profile has to be
                // projected back into its own stored format and written through the
                // store, so sending one down the classic TestSave path writes the wrong
                // file and loses the edits the user just chose to keep.
                if (activeVM.DeviceMapper is UniversalMapper universalMapper)
                {
                    UniversalProfileSaveUiUpdate universalSaveUpdate =
                        SaveUniversalProfileFromClassicEditor(activeVM, universalMapper);
                    activeVM.ProfileEnt.UpdatePath(universalSaveUpdate.ProfilePath);
                    activeVM.ProfileEnt.Name = universalSaveUpdate.DisplayName;
                }
                else
                {
                    activeVM.TestSave(activeVM.ProfileEnt, activeVM.DeviceMapper.ActionProfile);
                }

                activeVM.MarkProfileClean();
                return true;
            }
            catch (Exception ex)
            {
                saveProfileLogger.Error(ex, "Failed to save profile while closing");
                MessageBox.Show(
                    $"Failed to save the current profile:\n{ex.Message}",
                    "Save Profile",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            BackendManager manager = (App.Current as App).Manager;
            if (manager != null)
            {
                manager.ServiceStarted -= BackendManager_ServiceStateChanged;
                manager.ServiceStopped -= BackendManager_ServiceStateChanged;
                manager.PhysicalMouseStatusChanged -= BackendManager_PhysicalMouseStatusChanged;
            }

            DataContext = null;
            editorTestVM?.UnregisterEvents();
            mouseRoutingPanelVM?.Dispose();
            sdlDiagnosticsWindow?.Close();
            sdlDiagnosticsWindow = null;

            Util.UnregisterNotify(regHandle);
            Application.Current.Shutdown(0);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            HookWindowMessages(source);
            source.AddHook(WndProc);
        }

        private void HookWindowMessages(HwndSource source)
        {
            Guid hidGuid = new Guid();
            NativeMethods.HidD_GetHidGuid(ref hidGuid);
            if (!Util.RegisterNotify(source.Handle, hidGuid, ref regHandle))
            {
                App.Current.Shutdown();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Util.WM_DEVICECHANGE)
            {
                BackendManager manager = (Application.Current as App)?.Manager;
                if (manager?.IsRunning == true)
                {
                    int type = wParam.ToInt32();
                    if (type == DBT_DEVICEARRIVAL || type == DBT_DEVICEREMOVECOMPLETE)
                    {
                        bool startPass;
                        using (WriteLocker locker = new WriteLocker(hotplugCounterLock))
                        {
                            hotplugCounter++;
                            startPass = !inHotPlug;
                            if (startPass) inHotPlug = true;
                        }

                        if (startPass)
                        {
                            Task.Run(() => InnerHotplug(manager));
                        }
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void InnerHotplug(BackendManager manager)
        {
            while (true)
            {
                // Taking the count and clearing the in-progress flag under the
                // same lock is what closes the race. Clearing the flag after
                // the loop had already ended left a window in which a device
                // arriving right then bumped the counter, saw the flag still
                // set, started no new pass, and was never acted on.
                bool loop;
                using (WriteLocker locker = new WriteLocker(hotplugCounterLock))
                {
                    loop = hotplugCounter > 0;
                    hotplugCounter = 0;
                    if (!loop) inHotPlug = false;
                }

                if (!loop) return;

                Thread.Sleep(HOTPLUG_CHECK_DELAY);
                manager.EventDispatcher.Invoke((Action)(() => manager.Hotplug()));
            }
        }

        public void DuplicateProfile(DeviceListItem item, string inputFile, string outputFile)
        {
            controlListVM.DuplicateProfile(item, inputFile, outputFile);
        }
    }
}
