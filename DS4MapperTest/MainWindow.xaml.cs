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
using NLog;
using DS4MapperTest.PhysicalMouse;

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
        private bool isClosingAfterDirtyPrompt;
        private bool isDirtyClosePromptActive;
        private DispatcherTimer saveStatusHideTimer;
        private static readonly Logger saveProfileLogger = LogManager.GetCurrentClassLogger();
        private readonly ObservableCollection<PhysicalMouseSettingsItem> physicalMouseItems =
            new ObservableCollection<PhysicalMouseSettingsItem>();
        private bool updatingPhysicalMouseSettings;
        private bool stagedPhysicalMouseForwardingEnabled;
        private string stagedPhysicalMouseId;
        private bool appliedPhysicalMouseForwardingEnabled;
        private string appliedPhysicalMouseId;

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
            public string ProfilePath => Entity.ProfilePath;

            public ProfileListEntry(ProfileEntity entity, bool isActive)
            {
                Entity = entity;
                IsActive = isActive;
            }
        }

        private class ProfilePreview
        {
            public string Name { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        public void PostInit(AppGlobalData appGlobal)
        {
            this.appGlobal = appGlobal;

            BackendManager manager = (App.Current as App).Manager;
            controlListVM = new ControllerListViewModel(manager);
            manager.ServiceStarted += BackendManager_ServiceStateChanged;
            manager.ServiceStopped += BackendManager_ServiceStateChanged;
            controlListVM.ReadProfileFailure += ControlListVM_ReadProfileFailure;
            controlListVM.ControllerList.CollectionChanged += ControllerList_CollectionChanged;
            deviceComboBox.ItemsSource = controlListVM.ControllerList;
            physicalMouseComboBox.ItemsSource = physicalMouseItems;
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
            if (compact == isNavCompact) return;
            isNavCompact = compact;

            if (compact)
            {
                navPopup.IsOpen = false;
                navSidebarBorder.Child = null;
                navPopupHost.Child = navStackPanel;
                navSidebarBorder.Visibility = Visibility.Collapsed;
                navColumn.Width = new GridLength(0);
                navHamburgerButton.Visibility = Visibility.Visible;
            }
            else
            {
                navPopup.IsOpen = false;
                navPopupHost.Child = null;
                navSidebarBorder.Child = navStackPanel;
                navSidebarBorder.Visibility = Visibility.Visible;
                navColumn.Width = new GridLength(240);
                navHamburgerButton.Visibility = Visibility.Collapsed;
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
            BackendManager manager = (Application.Current as App).Manager;
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
            reader?.RequestGyroCalibration();
            UpdateGyroCalibrationControls(manager);
        }

        private void GyroCalibrationStatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateGyroCalibrationControls((Application.Current as App).Manager);
        }

        private void UpdateGyroCalibrationControls(BackendManager manager)
        {
            if (gyroCalibrateButton == null || gyroCalibrationStatusText == null) return;

            DeviceReaderBase reader = manager?.GetDeviceReader(currentDeviceItem?.Device);
            Common.GyroCalibrationStatus status = reader?.GyroCalibrationStatus;
            bool active = status != null && (status.IsWaitingToStart || status.IsCalibrating);

            gyroCalibrateButton.IsEnabled = manager?.IsRunning == true && reader != null && !active;
            gyroCalibrationStatusText.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (active)
            {
                double seconds = status.RemainingMilliseconds / 1000.0;
                gyroCalibrationStatusText.Text = status.IsWaitingToStart
                    ? $"Gyro calibration starts in {seconds:F1}s"
                    : $"Keep controller still; gyro calibration: {seconds:F1}s";
            }
        }

        private void ControllerList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && editorTestVM == null)
            {
                DeviceListItem item = e.NewItems[0] as DeviceListItem;
                Dispatcher.BeginInvoke((Action)(() => LoadProfileForDevice(item)));
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                DeviceListItem removed = e.OldItems?[0] as DeviceListItem;
                if (removed != null && removed == currentDeviceItem)
                {
                    Dispatcher.BeginInvoke((Action)(() => HandleCurrentDeviceRemoved()));
                }
            }
        }

        private void HandleCurrentDeviceRemoved()
        {
            InlineBindingEditorService.CloseAny();
            ExitRenameSetMode();
            ExitRenameLayerMode();

            editorTestVM?.UnregisterEvents();
            editorTestVM = null;
            currentDeviceItem = null;
            DataContext = null;

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
                profileComboBox.ItemsSource = null;
                profileListBox.ItemsSource = null;
                actionSetComboBox.ItemsSource = null;
                actionLayerComboBox.ItemsSource = null;
            }
        }

        private bool LoadProfileForDevice(DeviceListItem item)
        {
            if (item == null || item.ProfileIndex < 0) return false;

            BackendManager manager = (App.Current as App).Manager;
            if (!manager.MapperDict.ContainsKey(item.Device.Index)) return false;

            Mapper mapper = manager.MapperDict[item.Device.Index];
            InputDeviceType devType = mapper.DeviceType;
            if (!manager.DeviceProfileListDict.ContainsKey(devType)) return false;

            var profileList = manager.DeviceProfileListDict[devType].ProfileListCol;
            if (item.ProfileIndex >= profileList.Count) return false;

            ProfileEntity profileEnt = profileList[item.ProfileIndex];

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

            // Only tear down and rebuild ItemsSource when the backing collection
            // actually changed (e.g. switching devices). Re-nulling and
            // immediately reassigning the SAME ObservableCollection (as happens
            // on every profile switch/delete/discard within one device) makes
            // WPF resolve an item's data template binding against its internal
            // unset-value marker (MS.Internal.NamedObject), which throws a
            // TargetException that crashes the app. Pump the dispatcher between
            // the clear and the reassignment on the rare path where the source
            // really does change, same workaround as LoadProfileForDevice.
            if (profileComboBox.ItemsSource != currentDeviceItem.DevProfileList)
            {
                profileComboBox.ItemsSource = null;
                Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                profileComboBox.ItemsSource = currentDeviceItem.DevProfileList;
            }

            profileComboBox.SelectedIndex = currentDeviceItem.ProfileIndex;
            suppressCombo = false;
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

            await Task.Run(() => editorTestVM.ActionResetEvent.Wait());

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

            await Task.Run(() => editorTestVM.ActionResetEvent.Wait());

            DataContext = null;
            editorTestVM.RefreshLayerBindings();
            DataContext = editorTestVM;

            RefreshActionLayerCombo();

            IsEnabled = true;
        }

        private void AddSetBtn_Click(object sender, RoutedEventArgs e)
        {
            editorTestVM?.AddSet();
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

        private void AddLayerBtn_Click(object sender, RoutedEventArgs e)
        {
            editorTestVM?.AddLayer();
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

            int newIndex = profileComboBox.SelectedIndex;
            if (newIndex < 0 || newIndex == currentDeviceItem.ProfileIndex) return;

            await SwitchProfileAsync(currentDeviceItem, newIndex);
        }

        private void RefreshProfileList()
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

            profileListBox.ItemsSource = entries;
            selectedListEntry = null;
            selectedProfilePanel.Visibility = Visibility.Collapsed;
        }

        private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedListEntry = profileListBox.SelectedItem as ProfileListEntry;
            if (selectedListEntry == null)
            {
                selectedProfilePanel.Visibility = Visibility.Collapsed;
                return;
            }

            profileRenameBox.Text = selectedListEntry.Name;
            selectedProfilePanel.Visibility = Visibility.Visible;
        }

        private async Task SwitchProfileAsync(DeviceListItem item, int newIndex)
        {
            if (!await ConfirmDiscardProfileChangesAsync())
            {
                RefreshProfileCombo();
                return;
            }

            IsEnabled = false;
            suppressCombo = true;
            await Task.Run(() => { item.ProfileIndex = newIndex; });
            LoadProfileForDevice(item);
            suppressCombo = false;
            IsEnabled = true;
        }

        private void ManageProfilesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null) return;
            RefreshProfileList();
            profilesOverlay.Visibility = Visibility.Visible;
        }

        private void CloseProfileOverlay_Click(object sender, RoutedEventArgs e)
        {
            HideNewProfilePanel();
            profilesOverlay.Visibility = Visibility.Collapsed;
        }

        private void ProfilesOverlayBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideNewProfilePanel();
            profilesOverlay.Visibility = Visibility.Collapsed;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && profilesOverlay.Visibility == Visibility.Visible)
            {
                HideNewProfilePanel();
                profilesOverlay.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void NewProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;

            BackendManager manager = (App.Current as App).Manager;
            Mapper mapper = editorTestVM.DeviceMapper;

            overlayNewProfileVM = new NewProfileCreateViewModel(mapper, manager);
            newProfilePanel.DataContext = overlayNewProfileVM;
            newProfilePanel.Visibility = Visibility.Visible;
        }

        private void CancelNewProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            HideNewProfilePanel();
        }

        private void CreateNewProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || overlayNewProfileVM == null) return;

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
                _ = SwitchProfileAsync(currentDeviceItem, newIndex);
            }
            else
            {
                RefreshProfileList();
            }
        }

        private void NewProfileBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (overlayNewProfileVM == null) return;

            SaveFileDialog fileDialog = new SaveFileDialog
            {
                InitialDirectory = overlayNewProfileVM.Mapper.AppGlobal.GetDeviceProfileFolderLocation(overlayNewProfileVM.Mapper.DeviceType),
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (fileDialog.ShowDialog() != true) return;

            string tempFile = fileDialog.FileName;
            string destDir = Path.GetDirectoryName(tempFile);
            if (!string.Equals(fileDialog.InitialDirectory, destDir, StringComparison.OrdinalIgnoreCase))
            {
                overlayNewProfileVM.ProfilePath = tempFile;
                overlayNewProfileVM.ValidateForm();
                return;
            }

            if (!tempFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                tempFile += ".json";
            }

            overlayNewProfileVM.ProfilePath = tempFile;
            overlayNewProfileVM.ClearOldErrors();
        }

        private void HideNewProfilePanel()
        {
            overlayNewProfileVM?.ClearOldErrors();
            overlayNewProfileVM = null;
            newProfilePanel.DataContext = null;
            newProfilePanel.Visibility = Visibility.Collapsed;
        }

        private void CopyActiveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;

            Mapper mapper = editorTestVM.DeviceMapper;
            string sourceFile = editorTestVM.ProfileEnt.ProfilePath;
            string profilesDir = appGlobal.GetDeviceProfileFolderLocation(mapper.DeviceType);

            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "Save Copy As",
                InitialDirectory = profilesDir,
                Filter = "JSON files (*.json)|*.json",
                FileName = Path.GetFileNameWithoutExtension(sourceFile) + "_copy"
            };

            if (dlg.ShowDialog() != true) return;

            string destFile = dlg.FileName;
            if (!destFile.EndsWith(".json")) destFile += ".json";

            if (File.Exists(destFile))
            {
                MessageBox.Show("A profile with that filename already exists.", "Cannot Overwrite",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                controlListVM.DuplicateProfile(currentDeviceItem, sourceFile, destFile);
                RefreshProfileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProfileFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;

            Mapper mapper = editorTestVM.DeviceMapper;
            string profilesDir = appGlobal.GetDeviceProfileFolderLocation(mapper.DeviceType);

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Load Profile from File",
                Filter = "JSON files (*.json)|*.json",
                InitialDirectory = profilesDir
            };

            if (dlg.ShowDialog() != true) return;

            string srcFile = dlg.FileName;
            string destFile = srcFile;

            if (!string.Equals(Path.GetDirectoryName(srcFile), profilesDir, StringComparison.OrdinalIgnoreCase))
            {
                destFile = Path.Combine(profilesDir, Path.GetFileName(srcFile));
                if (File.Exists(destFile))
                {
                    MessageBox.Show("A profile with that filename already exists in the profiles folder.", "Cannot Import",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                File.Copy(srcFile, destFile);
            }

            BackendManager manager = (App.Current as App).Manager;
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
            if (selectedListEntry == null || currentDeviceItem == null) return;

            var profileList = currentDeviceItem.DevProfileList;
            int newIndex = profileList.IndexOf(selectedListEntry.Entity);
            if (newIndex < 0 || newIndex == currentDeviceItem.ProfileIndex) return;

            await SwitchProfileAsync(currentDeviceItem, newIndex);
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

            ProfileEntity ent = selectedListEntry.Entity;

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

                ent.Name = newName;

                if (editorTestVM != null &&
                    string.Equals(ent.ProfilePath, editorTestVM.ProfileEnt?.ProfilePath, StringComparison.OrdinalIgnoreCase))
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

        private async void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedListEntry == null || currentDeviceItem == null) return;

            ProfileEntity ent = selectedListEntry.Entity;
            var profileList = currentDeviceItem.DevProfileList;
            ProfileEntity activeEnt = editorTestVM?.ProfileEnt;
            bool isActive = string.Equals(ent.ProfilePath, activeEnt?.ProfilePath, StringComparison.OrdinalIgnoreCase);

            int deleteIndex = profileList.IndexOf(ent);
            if (deleteIndex < 0) return;

            if (isActive && profileList.Count <= 1)
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
                File.Delete(ent.ProfilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete profile:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (isActive)
            {
                ProfileEntity replacement = deleteIndex > 0
                    ? profileList[deleteIndex - 1]
                    : profileList[deleteIndex + 1];

                IsEnabled = false;
                suppressCombo = true;
                profileList.Remove(ent);
                int newIndex = profileList.IndexOf(replacement);

                await Task.Run(() => currentDeviceItem.ResyncProfileIndex(newIndex, reloadProfile: true));

                suppressCombo = false;
                LoadProfileForDevice(currentDeviceItem);
                IsEnabled = true;
            }
            else
            {
                suppressCombo = true;
                profileList.Remove(ent);
                if (activeEnt != null)
                {
                    int activeIndex = profileList.IndexOf(activeEnt);
                    if (activeIndex >= 0)
                    {
                        currentDeviceItem.ResyncProfileIndex(activeIndex, reloadProfile: false);
                    }
                }
                suppressCombo = false;

                RefreshProfileCombo();
                RefreshProfileList();
            }
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
            bool liveReloaded = false;
            try
            {
                await Task.Run(() => activeVM.TestSave(activeVM.ProfileEnt, activeVM.DeviceMapper.ActionProfile));
                if (currentDeviceItem != null)
                {
                    int profileIndex = currentDeviceItem.ProfileIndex;
                    await Task.Run(() => currentDeviceItem.ResyncProfileIndex(profileIndex, reloadProfile: true));
                    liveReloaded = true;
                }
            }
            catch (Exception ex)
            {
                saveException = ex;
            }

            IsEnabled = true;
            saveProfileButton.IsEnabled = true;
            isSavingProfile = false;

            if (saveException == null)
            {
                if (liveReloaded && currentDeviceItem != null)
                {
                    LoadProfileForDevice(currentDeviceItem);
                }

                activeVM.MarkProfileClean();
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
                return false;
            }
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

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (isClosingAfterDirtyPrompt) return;
            if (isDirtyClosePromptActive)
            {
                e.Cancel = true;
                return;
            }

            if (editorTestVM?.IsProfileDirty != true) return;

            e.Cancel = true;
            isDirtyClosePromptActive = true;

            bool canClose;
            try
            {
                canClose = await ConfirmDiscardProfileChangesAsync();
            }
            finally
            {
                isDirtyClosePromptActive = false;
            }

            if (!canClose) return;

            isClosingAfterDirtyPrompt = true;
            Close();
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
                BackendManager manager = (Application.Current as App).Manager;
                if (manager.IsRunning)
                {
                    int type = wParam.ToInt32();
                    if (type == DBT_DEVICEARRIVAL || type == DBT_DEVICEREMOVECOMPLETE)
                    {
                        using (WriteLocker locker = new WriteLocker(hotplugCounterLock))
                        {
                            hotplugCounter++;
                        }

                        if (!inHotPlug)
                        {
                            inHotPlug = true;
                            Task.Run(() => InnerHotplug(manager));
                        }
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void InnerHotplug(BackendManager manager)
        {
            bool loop;
            using (WriteLocker locker = new WriteLocker(hotplugCounterLock))
            {
                loop = hotplugCounter > 0;
                hotplugCounter = 0;
            }

            while (loop)
            {
                Thread.Sleep(HOTPLUG_CHECK_DELAY);
                manager.EventDispatcher.Invoke((Action)(() => manager.Hotplug()));

                using (WriteLocker locker = new WriteLocker(hotplugCounterLock))
                {
                    loop = hotplugCounter > 0;
                    hotplugCounter = 0;
                }
            }

            inHotPlug = false;
        }

        public void DuplicateProfile(DeviceListItem item, string inputFile, string outputFile)
        {
            controlListVM.DuplicateProfile(item, inputFile, outputFile);
        }
    }
}
