//using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.Common;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace DS4MapperTest.ViewModels
{
    public class ReadProfileFailException : Exception
    {
        private JsonException innerJsonException;
        public JsonException InnerJsonException => innerJsonException;

        private string extraMessage;
        public string ExtraMessage => extraMessage;

        public ReadProfileFailException(JsonException e, string extraMessage)
        {
            innerJsonException = e;
            this.extraMessage = extraMessage;
        }
    }

    public class ControllerListViewModel
    {
        private ReaderWriterLockSlim _colListLocker = new ReaderWriterLockSlim();
        private ObservableCollection<DeviceListItem> controllerList =
            new ObservableCollection<DeviceListItem>();
        public ObservableCollection<DeviceListItem> ControllerList
        {
            get => controllerList;
        }

        private Dictionary<int, DeviceListItem> controllerDict =
            new Dictionary<int, DeviceListItem>();
        public DeviceListItem CurrentItem
        {
            get
            {
                if (selectedIndex == -1) return null;
                controllerDict.TryGetValue(selectedIndex, out DeviceListItem item);
                return item;
            }
        }

        public Dictionary<int, DeviceListItem> ControllerDict { get => controllerDict; set => controllerDict = value; }

        private BackendManager backendManager;
        private UniversalClassicProfileList universalProfiles;
        private UniversalProfileStore universalStore;
        private int selectedIndex = -1;

        //public ProfileList DeviceProfileList
        //{
        //    get => backendManager.DeviceProfileList;
        //}

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (value == selectedIndex) return;
                selectedIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedIndexChanged;
        public event EventHandler<ReadProfileFailException> ReadProfileFailure;
        public event EventHandler<DeviceListItem> EditProfileRequested;

        public ControllerListViewModel(BackendManager manager)
        {
            backendManager = manager;

            backendManager.ServiceStarted += BackendManager_ServiceStarted;
            backendManager.ServiceStopped += BackendManager_ServiceStopped;
            backendManager.HotplugController += BackendManager_HotplugController;
            backendManager.UnplugController += BackendManager_UnplugController;

            BindingOperations.EnableCollectionSynchronization(controllerList, _colListLocker,
                            ColLockCallback);
        }

        private void BackendManager_UnplugController(InputDeviceBase device, int ind)
        {
            using (WriteLocker locker = new WriteLocker(_colListLocker))
            {
                //int ind = controllerList.Where((item) => item.ItemIndex == device.Index)
                //    .Select((item) => item.ItemIndex).DefaultIfEmpty(-1).First();
                if (ind >= 0)
                {
                    controllerDict.Remove(ind);
                    controllerList.RemoveAt(ind);
                }
            }
        }

        private void BackendManager_ServiceStopped(object sender, EventArgs e)
        {
            if (backendManager.UniversalMappingRuntime != null)
            {
                backendManager.UniversalMappingRuntime.SessionsChanged -= UniversalMappingRuntime_SessionsChanged;
            }

            using (WriteLocker locker = new WriteLocker(_colListLocker))
            {
                controllerList.Clear();
                controllerDict.Clear();
            }
        }

        private void BackendManager_ServiceStarted(object sender, EventArgs e)
        {
            if (backendManager.UniversalMappingRuntime != null)
            {
                backendManager.UniversalMappingRuntime.SessionsChanged -= UniversalMappingRuntime_SessionsChanged;
                backendManager.UniversalMappingRuntime.SessionsChanged += UniversalMappingRuntime_SessionsChanged;
                RefreshUniversalControllers();
                return;
            }

            using (WriteLocker locker = new WriteLocker(_colListLocker))
            {
                int i = 0;
                foreach (InputDeviceBase device in backendManager.ControllerList)
                {
                    if (device != null)
                    {
                        DeviceListItem devItem = new DeviceListItem(device, i,
                            backendManager.DeviceProfileListDict[device.DeviceType]);

                        if (backendManager.MapperDict.ContainsKey(device.Index))
                        {
                            Mapper map = backendManager.MapperDict[device.Index];
                            if (map.ProfileFile != string.Empty)
                            {
                                devItem.PostInit(map.ProfileFile);
                            }

                            devItem.ProfileIndexChanged += DevItem_ProfileIndexChanged;
                            devItem.EditProfileRequested += DevItem_EditProfileRequested;
                        }

                        //if (!string.IsNullOrWhiteSpace(backendManager.ProfileFile))
                        //{
                        //    devItem.PostInit(backendManager.ProfileFile);
                        //}
                        device.Removal += Device_Removal;
                        controllerList.Add(devItem);
                        controllerDict[i] = devItem;

                        i++;
                    }
                }
            }
        }

        private void UniversalMappingRuntime_SessionsChanged(object sender, EventArgs e)
        {
            RefreshUniversalControllers();
        }

        private void RefreshUniversalControllers()
        {
            UniversalMappingRuntime runtime = backendManager.UniversalMappingRuntime;
            if (runtime == null) return;

            universalStore ??= UniversalProfileStore.CreateDefault();
            universalProfiles ??= new UniversalClassicProfileList(universalStore);
            universalProfiles.Refresh();

            using (WriteLocker locker = new WriteLocker(_colListLocker))
            {
                controllerList.Clear();
                controllerDict.Clear();
                int i = 0;
                foreach (UniversalMapperSession session in runtime.Sessions)
                {
                    DeviceListItem devItem = new DeviceListItem(session, i, universalProfiles);
                    string activePath = session.ActiveProfile != null
                        ? universalStore.FindProfilePath(session.ActiveProfile.ProfileId)
                        : string.Empty;
                    devItem.PostInit(activePath);
                    devItem.ProfileIndexChanged += DevItem_ProfileIndexChanged;
                    devItem.EditProfileRequested += DevItem_EditProfileRequested;
                    controllerList.Add(devItem);
                    controllerDict[i] = devItem;
                    i++;
                }
            }
        }

        public void RefreshUniversalProfileLists()
        {
            universalProfiles?.Refresh();
        }

        private void DevItem_EditProfileRequested(object sender, EventArgs e)
        {
            EditProfileRequested?.Invoke(this, sender as DeviceListItem);
        }

        private void Device_Removal(object sender, EventArgs e)
        {
            InputDeviceBase device = sender as InputDeviceBase;
            using (WriteLocker locker = new WriteLocker(_colListLocker))
            {
                int ind = -1;
                int findInd = 0;
                foreach(DeviceListItem devItem in controllerList)
                {
                    if (devItem.ItemIndex == device.Index)
                    {
                        ind = findInd;
                        break;
                    }

                    findInd++;
                }
                //int ind = controllerList.Where((item) => item.ItemIndex == device.Index)
                //    .Select((item) => item.ItemIndex).DefaultIfEmpty(-1).First();
                if (device.Synced && ind >= 0)
                {
                    controllerList.RemoveAt(ind);
                }
            }
        }

        private void BackendManager_HotplugController(InputDeviceBase device, int ind)
        {
            // Engage write lock pre-maturely
            using (WriteLocker readLock = new WriteLocker(_colListLocker))
            {
                DeviceListItem devItem = new DeviceListItem(device, ind,
                    backendManager.DeviceProfileListDict[device.DeviceType]);
                Mapper map = backendManager.MapperDict[device.Index];
                if (!string.IsNullOrWhiteSpace(map.ProfileFile))
                {
                    devItem.PostInit(map.ProfileFile);
                }

                devItem.ProfileIndexChanged += DevItem_ProfileIndexChanged;
                devItem.EditProfileRequested += DevItem_EditProfileRequested;
                device.Removal += Device_Removal;
                controllerList.Add(devItem);
                controllerDict[ind] = devItem;
            }
        }

        private void DevItem_ProfileIndexChanged(object sender, EventArgs e)
        {
            DeviceListItem item = sender as DeviceListItem;
            if (item?.IsUniversal == true)
            {
                if (item.ProfileIndex < 0 || item.ProfileIndex >= item.DevProfileList.Count) return;
                UniversalProfile profile = universalStore.LoadFromPath(item.DevProfileList[item.ProfileIndex].ProfilePath);
                backendManager.UniversalMappingRuntime.SwitchProfile(item.UniversalSession.LogicalControllerId, profile);
                return;
            }

            Mapper map = backendManager.MapperDict[item.Device.Index];
            string profilePath = backendManager.DeviceProfileListDict[item.Device.DeviceType].ProfileListCol[item.ProfileIndex].ProfilePath;

            ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
            map.QueueEvent(() =>
            {
                {
                    //map.UseBlankProfile();
                    //ReadProfileFailure?.Invoke(this, new ReadProfileFailException(new JsonException(), $"Failed to read profile {profilePath}"));
                    try
                    {
                        map.ChangeProfile(profilePath);
                    }
                    catch (JsonException e)
                    {
                        ReadProfileFailure?.Invoke(this, new ReadProfileFailException(e, $"Failed to read profile {profilePath}"));
                    }
                    //backendManager.ProfileFile = DeviceProfileList.ProfileListCol[item.ProfileIndex].ProfilePath;
                }

                resetEvent.Set();
            });

            resetEvent.Wait(AppGlobalData.RESET_WAIT_TIMEOUT);
        }

        private void ColLockCallback(IEnumerable collection, object context,
            Action accessMethod, bool writeAccess)
        {
            if (writeAccess)
            {
                using (WriteLocker locker = new WriteLocker(_colListLocker))
                {
                    accessMethod?.Invoke();
                }
            }
            else
            {
                using (ReadLocker locker = new ReadLocker(_colListLocker))
                {
                    accessMethod?.Invoke();
                }
            }
        }

        public void WaitMapperEvent(DeviceListItem item)
        {
            Mapper map = backendManager.MapperDict[item.Device.Index];
            map.ProcessMappingChangeAction(() =>
            {
            });
        }

        public void DuplicateProfile(DeviceListItem item, string inputFile, string outputFile)
        {
            if (item?.IsUniversal == true)
            {
                File.Copy(inputFile, outputFile);
                universalProfiles.Refresh();
                return;
            }

            // Copy file as is
            File.Copy(inputFile, outputFile);

            string tempOutJson = string.Empty;
            string profileName = string.Empty;
            // Read output file as a JSON object
            using (StreamReader sreader = new StreamReader(outputFile))
            using (JsonTextReader jReader = new JsonTextReader(sreader))
            {
                JObject root = (JObject)JToken.ReadFrom(jReader);

                // Edit JSON and output to string
                profileName = Path.GetFileNameWithoutExtension(outputFile);
                root["Name"] = profileName;
                root["Description"] = profileName;
                root["CreationDate"] = DateTime.UtcNow;
                tempOutJson = root.ToString();
            }

            // Write update JSON string back to output file
            if (!string.IsNullOrEmpty(tempOutJson))
            {
                AtomicFileWriter.WriteJson(outputFile, JObject.Parse(tempOutJson));

                // Update profile list
                Mapper mapper = backendManager.MapperDict[item.Device.Index];
                backendManager.DeviceProfileListDict[mapper.DeviceType].CreateProfileItem(outputFile,
                    profileName,
                    mapper.DeviceType);
            }
        }
    }

    public class DeviceListItem : INotifyPropertyChanged
    {
        private int itemIndex;
        private InputDeviceBase device;
        private ProfileList profileListHolder;
        private UniversalClassicProfileList universalProfileListHolder;
        private UniversalMapperSession universalSession;
        private int profileIndex = -1;
        private bool batteryKnown;

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public InputDeviceBase Device
        {
            get => device;
        }

        public bool IsUniversal => universalSession != null;
        public UniversalMapperSession UniversalSession => universalSession;

        public string DisplayName
        {
            get => device.DevTypeStr;
        }

        public string DisplayNameWithBattery => $"{DisplayName}  {Battery}";

        public int DisplayIndex
        {
            get => device.Index + 1;
        }

        public int ItemIndex
        {
            get => itemIndex;
        }

        public string Battery
        {
            get
            {
                if (universalSession?.Controller.BatteryPercent is int universalBattery)
                {
                    return $"{universalBattery}%";
                }

                uint batteryValue = device.Battery;
                return batteryKnown && batteryValue <= 100
                    ? $"{batteryValue}%"
                    : "Battery unknown";
            }
        }
        public event EventHandler BatteryChanged;

        public int ProfileIndex
        {
            get => profileIndex;
            set
            {
                if (value == profileIndex) return;
                profileIndex = value;
                ProfileIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ProfileIndexChanged;

        // Realign profileIndex after DevProfileList has been mutated. The
        // ProfileIndex setter skips equal values, so it cannot force a reload
        // when a removal leaves the numeric index unchanged
        public void ResyncProfileIndex(int value, bool reloadProfile)
        {
            profileIndex = value;
            if (reloadProfile)
            {
                ProfileIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ObservableCollection<ProfileEntity> DevProfileList
        {
            get => universalProfileListHolder?.Profiles ?? profileListHolder.ProfileListCol;
        }

        public ObservableCollection<string> ProfileFolders
        {
            get => universalProfileListHolder?.Folders ?? profileListHolder.ProfileFolderCol;
        }

        public ProfileList ProfileListHolder
        {
            get => profileListHolder;
        }

        public UniversalClassicProfileList UniversalProfileListHolder
        {
            get => universalProfileListHolder;
        }

        //private EditProfileCommand editProfCommand;
        //public EditProfileCommand EditProfCommand => editProfCommand;

        private BasicActionCommand editProfCommand;
        public BasicActionCommand EditProfCommand => editProfCommand;

        public event EventHandler EditProfileRequested;

        public bool PrimaryDevice
        {
            get => device.PrimaryDevice;
        }

        public DeviceListItem(InputDeviceBase device, int itemIndex, ProfileList profileListHolder)
        {
            this.device = device;
            this.itemIndex = itemIndex;
            this.profileListHolder = profileListHolder;
            batteryKnown = device.Battery > 0 && device.Battery <= 100;
            device.BatteryChanged += Device_BatteryChanged;

            editProfCommand = new BasicActionCommand((parameter) =>
            {
                EditProfileRequested?.Invoke(this, EventArgs.Empty);
            });
        }

        public DeviceListItem(UniversalMapperSession session, int itemIndex, UniversalClassicProfileList profileListHolder)
            : this(new UniversalClassicInputDevice(session, itemIndex), itemIndex, null)
        {
            universalSession = session;
            universalProfileListHolder = profileListHolder;
            batteryKnown = session.Controller.BatteryPercent.HasValue;
        }

        private void Device_BatteryChanged(object sender, EventArgs e)
        {
            batteryKnown = device.Battery <= 100;
            RaisePropertyChanged(nameof(Battery));
            RaisePropertyChanged(nameof(DisplayNameWithBattery));
            BatteryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshUniversalState()
        {
            if (universalSession == null)
            {
                return;
            }

            int? percent = universalSession.Controller.BatteryPercent;
            batteryKnown = percent.HasValue;
            uint nextBattery = percent.HasValue ? (uint)percent.Value : uint.MaxValue;
            if (device.Battery != nextBattery)
            {
                device.Battery = nextBattery;
            }
            else
            {
                RaisePropertyChanged(nameof(Battery));
                RaisePropertyChanged(nameof(DisplayNameWithBattery));
                BatteryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void PostInit(string profilePath)
        {
            ProfileEntity temp = DevProfileList.SingleOrDefault((item) =>
                string.Equals(item.ProfilePath, profilePath, StringComparison.OrdinalIgnoreCase));
            if (temp != null)
            {
                int ind = DevProfileList.IndexOf(temp);
                ProfileIndex = ind;
            }
            else
            {
                ProfileIndex = DevProfileList.Count > 0 ? 0 : -1;
            }
        }

        public override string ToString()
        {
            return DisplayNameWithBattery;
        }
    }
}
