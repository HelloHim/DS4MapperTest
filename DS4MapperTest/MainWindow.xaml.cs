using System;
using System.Collections.Specialized;
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

        private IntPtr regHandle = new IntPtr();
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int HOTPLUG_CHECK_DELAY = 2000;
        private bool inHotPlug;
        private int hotplugCounter;
        private readonly ReaderWriterLockSlim hotplugCounterLock = new ReaderWriterLockSlim();

        private bool isSavingProfile;
        private DispatcherTimer saveStatusHideTimer;
        private static readonly Logger saveProfileLogger = LogManager.GetCurrentClassLogger();

        private const double NavCompactWidthThreshold = 820;
        private bool isNavCompact;

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
            controlListVM.ReadProfileFailure += ControlListVM_ReadProfileFailure;
            controlListVM.ControllerList.CollectionChanged += ControllerList_CollectionChanged;
            deviceComboBox.ItemsSource = controlListVM.ControllerList;
            noDeviceHint.Visibility = Visibility.Visible;
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
        }

        public async void StartCheckProcess()
        {
            await Task.Run(async () =>
            {
                (Application.Current as App).Manager.Start();
                await Task.Delay(1000);
            });
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
                actionContextRow.Visibility = Visibility.Collapsed;
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

            editorTestVM?.UnregisterEvents();
            editorTestVM = new ProfileEditorTestViewModel(mapper, profileEnt, mapper.ActionProfile);
            DataContext = editorTestVM;
            editorTestVM.Test();

            currentDeviceItem = item;
            noDeviceHint.Visibility = Visibility.Collapsed;
            actionContextRow.Visibility = Visibility.Visible;

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

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressDeviceCombo) return;

            DeviceListItem newItem = deviceComboBox.SelectedItem as DeviceListItem;
            if (newItem == null || newItem == currentDeviceItem) return;

            if (editorTestVM?.CurrentProfile != null && editorTestVM.CurrentProfile.Dirty)
            {
                var confirm = MessageBox.Show(
                    "The current profile has unsaved changes. Switch devices anyway?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    RefreshDeviceCombo();
                    return;
                }
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
            profileComboBox.ItemsSource = null;
            profileComboBox.ItemsSource = currentDeviceItem.DevProfileList;
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
            profilesOverlay.Visibility = Visibility.Collapsed;
        }

        private void ProfilesOverlayBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            profilesOverlay.Visibility = Visibility.Collapsed;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && profilesOverlay.Visibility == Visibility.Visible)
            {
                profilesOverlay.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void NewProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentDeviceItem == null || editorTestVM == null) return;

            BackendManager manager = (App.Current as App).Manager;
            Mapper mapper = editorTestVM.DeviceMapper;

            NewProfileCreateWindow newProfWin = new NewProfileCreateWindow();
            newProfWin.PostInit(mapper, manager);
            newProfWin.Owner = this;
            newProfWin.ShowDialog();

            NewProfileCreateViewModel newProfVM = newProfWin.NewProfCreateVM;
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
                    editorTestVM.ProfileName = newName;
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

        private void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedListEntry == null || currentDeviceItem == null) return;

            ProfileEntity ent = selectedListEntry.Entity;
            bool isActive = string.Equals(ent.ProfilePath, editorTestVM?.ProfileEnt?.ProfilePath, StringComparison.OrdinalIgnoreCase);

            if (isActive && currentDeviceItem.DevProfileList.Count <= 1)
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

            if (isActive)
            {
                int nextIndex = currentDeviceItem.ProfileIndex > 0 ? currentDeviceItem.ProfileIndex - 1 : 1;
                string pathToDelete = ent.ProfilePath;
                _ = Task.Run(() => { currentDeviceItem.ProfileIndex = nextIndex; })
                    .ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try { File.Delete(pathToDelete); } catch { }
                            currentDeviceItem.DevProfileList.Remove(ent);
                            LoadProfileForDevice(currentDeviceItem);
                        });
                    });
            }
            else
            {
                try
                {
                    File.Delete(ent.ProfilePath);
                    currentDeviceItem.DevProfileList.Remove(ent);
                    selectedListEntry = null;
                    selectedProfilePanel.Visibility = Visibility.Collapsed;
                    RefreshProfileList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete profile:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (editorTestVM == null || isSavingProfile) return;

            isSavingProfile = true;
            saveStatusHideTimer?.Stop();
            HideSaveStatusPill(animate: false);

            ProfileEditorTestViewModel activeVM = editorTestVM;
            saveProfileButton.Content = "Saving...";
            saveProfileButton.IsEnabled = false;
            IsEnabled = false;

            Exception saveException = null;
            try
            {
                await Task.Run(() => activeVM.TestSave(activeVM.ProfileEnt, activeVM.DeviceMapper.ActionProfile));
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
                saveProfileButton.Content = "Saved ✓";
                ShowSaveStatusPill(success: true);
                StartSaveStatusHideTimer(TimeSpan.FromSeconds(2.5), revertButton: true);
            }
            else
            {
                saveProfileLogger.Error(saveException, "Failed to save profile");
                saveProfileButton.Content = "Save Profile";
                ShowSaveStatusPill(success: false);
                StartSaveStatusHideTimer(TimeSpan.FromSeconds(6), revertButton: false);
            }
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


        private void ControlListVM_ReadProfileFailure(object sender, ReadProfileFailException e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                MessageBox.Show($"{e.ExtraMessage}\n\n{e.InnerJsonException.Message}",
                    "Profile read failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }

        private void Window_Closed(object sender, EventArgs e)
        {
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
