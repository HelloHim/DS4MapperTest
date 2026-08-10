using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace DS4MapperTest
{
    public class ProfileList
    {
        private object _proLockobj = new object();
        private ObservableCollection<ProfileEntity> profileListCol =
            new ObservableCollection<ProfileEntity>();

        public ObservableCollection<ProfileEntity> ProfileListCol { get => profileListCol; set => profileListCol = value; }

        private InputDeviceType inputDeviceType;

        public ProfileList(InputDeviceType inputDeviceType)
        {
            this.inputDeviceType = inputDeviceType;
            BindingOperations.EnableCollectionSynchronization(profileListCol, _proLockobj);
        }

        public void Refresh()
        {
            profileListCol.Clear();
            string tempDirPath = AppGlobalDataSingleton.Instance.GetDeviceProfileFolderLocation(inputDeviceType);
            if (Directory.Exists(tempDirPath))
            {
                string[] profiles = Directory.GetFiles(tempDirPath);
                foreach (string s in profiles)
                {
                    if (s.EndsWith(".json"))
                    {
                        string json = File.ReadAllText(s);

                        try
                        {
                            ProfilePreview tempPreview =
                                JsonConvert.DeserializeObject<ProfilePreview>(json);

                            if (tempPreview == null)
                            {
                                continue;
                            }

                            // The file name is a profile's real identity: it is
                            // what survives a manual rename in Explorer or a copy
                            // to a new file. Treat it as the source of truth and
                            // repair the stored "Name" field on disk whenever the
                            // two have drifted apart, rather than showing a name
                            // that no longer matches the file it lives in.
                            string expectedName = Path.GetFileNameWithoutExtension(s);
                            string entryName = tempPreview.Name;
                            if (!string.Equals(entryName, expectedName, StringComparison.Ordinal))
                            {
                                entryName = expectedName;
                                TrySyncStoredName(s, expectedName);
                            }

                            ProfileEntity item = new ProfileEntity(path: s, name: entryName, inputDeviceType);
                            profileListCol.Add(item);
                        }
                        catch (JsonReaderException)
                        {
                        }
                        catch (JsonSerializationException)
                        {
                        }
                    }
                }
            }
        }

        private static void TrySyncStoredName(string path, string expectedName)
        {
            try
            {
                string json = File.ReadAllText(path);
                JObject root = JObject.Parse(json);
                root["Name"] = expectedName;
                using (StreamWriter writer = new StreamWriter(path))
                using (JsonTextWriter jwriter = new JsonTextWriter(writer))
                {
                    jwriter.Formatting = Formatting.Indented;
                    jwriter.Indentation = 2;
                    root.WriteTo(jwriter);
                }
            }
            catch (Exception)
            {
                // Best-effort repair. If the write fails the in-memory name is
                // still correct for this session; the file gets another chance
                // to be fixed up on the next scan.
            }
        }

        public void CreateProfileItem(string profilePath, string profileName,
            InputDeviceType deviceType)
        {
            lock (_proLockobj)
            {
                ProfileEntity tempEntity =
                    new ProfileEntity(profilePath, profileName, deviceType);
                int insertIdx = profileListCol.TakeWhile((item) => string.Compare(item.Name, profileName) < 0).Count();
                if (insertIdx > 0 && insertIdx < profileListCol.Count-1)
                {
                    profileListCol.Insert(insertIdx, tempEntity);
                }
                else
                {
                    profileListCol.Add(tempEntity);
                }
            }
        }
    }

    public class ProfilePreview
    {
        private string name;
        public string Name
        {
            get => name;
            set => name = value;
        }

        private string controllerType;
        public string ControllerType
        {
            get => controllerType;
            set => controllerType = value;
        }
    }
}
