using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections;
using static DS4MapperTest.Mapper;

namespace DS4MapperTest.ViewModels
{
    public class NewProfileCreateViewModel : INotifyDataErrorInfo, INotifyPropertyChanged
    {
        private Mapper mapper;
        public Mapper Mapper => mapper;

        private BackendManager manager;

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class OutputContTypeAssoc
        {
            public string Name { get; set; }
            public OutputContType Type { get; set; }
        }

        private string profileName = string.Empty;
        public string ProfileName
        {
            get => profileName;
            set
            {
                if (profileName == value) return;
                profileName = value;
                RaisePropertyChanged(nameof(ProfileName));
                RaisePropertyChanged(nameof(ProfilePath));
                ValidateNameField();
            }
        }

        private string profileFolder;
        public string ProfileFolder
        {
            get => profileFolder;
            set
            {
                if (profileFolder == value) return;
                profileFolder = value;
                RaisePropertyChanged(nameof(ProfileFolder));
                RaisePropertyChanged(nameof(ProfilePath));
            }
        }

        public ObservableCollection<string> ProfileFolders =>
            manager.DeviceProfileListDict[mapper.DeviceType].ProfileFolderCol;

        private string selectedFolderName;
        public string SelectedFolderName
        {
            get => selectedFolderName;
            set
            {
                if (selectedFolderName == value) return;
                selectedFolderName = value;
                profileFolder = manager.DeviceProfileListDict[mapper.DeviceType].GetFolderPath(selectedFolderName);
                RaisePropertyChanged(nameof(SelectedFolderName));
                RaisePropertyChanged(nameof(ProfileFolder));
                RaisePropertyChanged(nameof(ProfilePath));
                ValidateNameField();
            }
        }

        // Full destination file path, derived from the folder and name fields.
        // Kept as a read-only property so callers that only care about the
        // eventual file location (e.g. matching up the newly created profile
        // in the profile list) don't need to know about the two-field split.
        public string ProfilePath =>
            string.IsNullOrEmpty(profileFolder) || string.IsNullOrEmpty(profileName)
                ? string.Empty
                : Path.Combine(profileFolder, profileName.Trim() + ".json");

        private bool profileCreated;
        public bool ProfileCreated
        {
            get => profileCreated;
            set
            {
                profileCreated = value;
            }
        }

        private int outputControllerTypeIdx = 0;
        public int OutputControllerTypeIdx
        {
            get => outputControllerTypeIdx;
            set
            {
                if (outputControllerTypeIdx == value) return;
                outputControllerTypeIdx = value;
                RaisePropertyChanged(nameof(OutputControllerTypeIdx));
            }
        }

        private List<OutputContTypeAssoc> outputContList = new List<OutputContTypeAssoc>()
        {
            new OutputContTypeAssoc() {Name="None", Type=OutputContType.None },
            new OutputContTypeAssoc() {Name="Xbox 360", Type=OutputContType.Xbox360 },
            new OutputContTypeAssoc() {Name="DualShock 4", Type=OutputContType.DualShock4 },
            new OutputContTypeAssoc() {Name="DualSense Edge", Type=OutputContType.DualSenseEdge },
            new OutputContTypeAssoc() {Name="Switch Pro Controller 2", Type=OutputContType.SwitchPro2 },
        };
        public List<OutputContTypeAssoc> OutputContList => outputContList;

        public string ProfileNameErrors
        {
            get
            {
                string result = string.Empty;
                if (errors.TryGetValue("ProfileName", out List<string> errorList))
                {
                    result = string.Join("\n", errorList);
                }

                return result;
            }
        }
        public bool HasProfileNameError
        {
            get => errors.ContainsKey("ProfileName");
        }

        public string ProfileFolderErrors
        {
            get
            {
                string result = string.Empty;
                if (errors.TryGetValue("ProfileFolder", out List<string> errorList))
                {
                    result = string.Join("\n", errorList);
                }

                return result;
            }
        }
        public bool HasProfileFolderError
        {
            get => errors.ContainsKey("ProfileFolder");
        }

        protected Dictionary<string, List<string>> errors =
            new Dictionary<string, List<string>>();

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
        public bool HasErrors => errors.Count > 0;

        public NewProfileCreateViewModel(Mapper mapper, BackendManager manager)
        {
            this.mapper = mapper;
            this.manager = manager;

            // Profiles are stored per device type (DS4, DualSense, etc.), so the
            // folder for the currently active controller is always a sensible
            // default. The manage-profiles panel that hosts this view model can
            // only be opened while a controller is connected, so DeviceType is
            // guaranteed to be valid here.
            ProfileList profileList = manager.DeviceProfileListDict[mapper.DeviceType];
            selectedFolderName = profileList.FolderExists(ProfileList.VALORANT_PROFILE_FOLDER)
                ? ProfileList.VALORANT_PROFILE_FOLDER
                : profileList.ProfileFolderCol.FirstOrDefault() ?? ProfileList.VALORANT_PROFILE_FOLDER;
            profileFolder = profileList.GetFolderPath(selectedFolderName);
        }

        public bool CreateProfile()
        {
            Profile tempProfile = null;
            string fullPath = ProfilePath;
            string trimmedName = profileName.Trim();
            ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

            mapper.QueueEvent(() =>
            {
                mapper.UseBlankProfile();
                tempProfile = mapper.ActionProfile;
                tempProfile.Name = trimmedName;
                tempProfile.CreationDate = DateTime.UtcNow;
                tempProfile.Description = trimmedName;
                if (outputControllerTypeIdx >= 0)
                {
                    tempProfile.OutputGamepadSettings.OutputGamepad = OutputContList[outputControllerTypeIdx].Type;
                    if (tempProfile.OutputGamepadSettings.OutputGamepad != OutputContType.None)
                    {
                        tempProfile.OutputGamepadSettings.Enabled = true;
                    }
                    else
                    {
                        tempProfile.OutputGamepadSettings.Enabled = false;
                    }
                }
                else
                {
                    // Default output controller type to Xbox 360 in profile
                    // Use Enabled flag for specifying in a profile whether to output
                    // a virtual controller
                    tempProfile.OutputGamepadSettings.OutputGamepad = OutputContType.Xbox360;
                    tempProfile.OutputGamepadSettings.Enabled = false;
                }

                tempProfile.ActionSets[0].Name = "Main";
                tempProfile.ActionSets[0].ActionLayers[0].Name = "Default";

                mapper.AppGlobal.CreateBlankProfile(fullPath, tempProfile);

                resetEvent.Set();
            });

            resetEvent.Wait(AppGlobalData.RESET_WAIT_TIMEOUT);
            manager.DeviceProfileListDict[mapper.DeviceType].CreateProfileItem(fullPath,
                    trimmedName,
                    mapper.DeviceType);

            profileCreated = true;

            return profileCreated;
        }

        public bool ValidateForm()
        {
            ValidateNameField();
            ValidateFolderField();

            return errors.Count == 0;
        }

        // Runs on every keystroke (via the ProfileName setter) as well as on
        // form submission, so an invalid name is flagged immediately rather
        // than only once the user presses Create.
        private void ValidateNameField()
        {
            ClearFieldErrors("ProfileName");

            if (string.IsNullOrWhiteSpace(profileName))
            {
                AddError("ProfileName", "Profile name not provided");
            }
            else if (profileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                AddError("ProfileName", "Profile name contains invalid characters");
            }
            else if (File.Exists(ProfilePath))
            {
                AddError("ProfileName", "A profile with this name already exists");
            }

            RaiseErrorStatusEvents(new List<string> { "ProfileName" });
        }

        private void ValidateFolderField()
        {
            ClearFieldErrors("ProfileFolder");

            if (string.IsNullOrWhiteSpace(profileFolder))
            {
                AddError("ProfileFolder", "Profile folder not provided");
            }
            else if (!Directory.Exists(profileFolder))
            {
                AddError("ProfileFolder", "Profile folder does not exist");
            }

            RaiseErrorStatusEvents(new List<string> { "ProfileFolder" });
        }

        private void AddError(string propertyName, string message)
        {
            if (!errors.TryGetValue(propertyName, out List<string> tempList))
            {
                tempList = new List<string>();
                errors.Add(propertyName, tempList);
            }

            tempList.Add(message);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void ClearFieldErrors(string propertyName)
        {
            if (errors.Remove(propertyName))
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            errors.TryGetValue(propertyName, out List<string> errorsForName);
            return errorsForName;
        }

        public void ClearOldErrors()
        {
            List<string> keys = errors.Keys.ToList();
            errors.Clear();

            foreach(string key in keys)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(key));
            }

            RaiseErrorStatusEvents(keys);
        }

        private void RaiseErrorStatusEvents(List<string> keys)
        {
            foreach(string key in keys)
            {
                switch(key)
                {
                    case "ProfileName":
                        RaisePropertyChanged(nameof(ProfileNameErrors));
                        RaisePropertyChanged(nameof(HasProfileNameError));
                        break;
                    case "ProfileFolder":
                        RaisePropertyChanged(nameof(ProfileFolderErrors));
                        RaisePropertyChanged(nameof(HasProfileFolderError));
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
