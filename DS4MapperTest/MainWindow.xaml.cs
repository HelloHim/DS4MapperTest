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
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HidLibrary;
using DS4MapperTest.Views;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest
{
    public partial class MainWindow : Window
    {
        private ControllerListViewModel controlListVM;
        private ProfileEditorTestViewModel editorTestVM;
        private AppGlobalData appGlobal;

        private DeviceListItem currentDeviceItem;
        private bool suppressCombo;
        private ProfileListEntry selectedListEntry;

        private IntPtr regHandle = new IntPtr();
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int HOTPLUG_CHECK_DELAY = 2000;
        private bool inHotPlug;
        private int hotplugCounter;
        private readonly ReaderWriterLockSlim hotplugCounterLock = new ReaderWriterLockSlim();

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
            noDeviceHint.Visibility = Visibility.Visible;
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
        }

        private void LoadProfileForDevice(DeviceListItem item)
        {
            if (item == null || item.ProfileIndex < 0) return;

            BackendManager manager = (App.Current as App).Manager;
            if (!manager.MapperDict.ContainsKey(item.Device.Index)) return;

            Mapper mapper = manager.MapperDict[item.Device.Index];
            InputDeviceType devType = mapper.DeviceType;
            if (!manager.DeviceProfileListDict.ContainsKey(devType)) return;

            var profileList = manager.DeviceProfileListDict[devType].ProfileListCol;
            if (item.ProfileIndex >= profileList.Count) return;

            ProfileEntity profileEnt = profileList[item.ProfileIndex];

            editorTestVM?.UnregisterEvents();
            editorTestVM = new ProfileEditorTestViewModel(mapper, profileEnt, mapper.ActionProfile);
            DataContext = editorTestVM;
            editorTestVM.Test();

            currentDeviceItem = item;
            noDeviceHint.Visibility = Visibility.Collapsed;

            RefreshProfileCombo();
            RefreshProfileList();
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
            if (editorTestVM == null) return;

            IsEnabled = false;
            editorTestVM.TestSave(editorTestVM.ProfileEnt, editorTestVM.DeviceMapper.ActionProfile);
            await Task.Run(() => editorTestVM.ActionResetEvent.Wait());
            IsEnabled = true;
        }

        private void ColorPicker_SelectedColorChanged(object sender, HandyControl.Data.FunctionEventArgs<Color> e)
        {
            editorTestVM?.UpdateSelectedSolidColor(ColorPicker.SelectedBrush.Color.R,
                ColorPicker.SelectedBrush.Color.G, ColorPicker.SelectedBrush.Color.B);
        }

        private void ColorPickerBattery_SelectedColorChanged(object sender, HandyControl.Data.FunctionEventArgs<Color> e)
        {
            editorTestVM?.UpdateSelectedBatteryColor(ColorPickerBattery.SelectedBrush.Color.R,
                ColorPickerBattery.SelectedBrush.Color.G, ColorPickerBattery.SelectedBrush.Color.B);
        }

        private void ColorPickerPulse_SelectedColorChanged(object sender, HandyControl.Data.FunctionEventArgs<Color> e)
        {
            editorTestVM?.UpdateSelectedPulseColor(ColorPickerPulse.SelectedBrush.Color.R,
                ColorPickerPulse.SelectedBrush.Color.G, ColorPickerPulse.SelectedBrush.Color.B);
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
            ColorPicker.SelectedColorChanged -= ColorPicker_SelectedColorChanged;
            ColorPickerBattery.SelectedColorChanged -= ColorPickerBattery_SelectedColorChanged;
            ColorPickerPulse.SelectedColorChanged -= ColorPickerPulse_SelectedColorChanged;

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
