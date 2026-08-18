using System;
using System.IO;

namespace DS4MapperTest.Universal
{
    /// <summary>
    /// Remembers which universal profile each controller was last mapping with
    /// so a connecting controller comes back on that profile instead of on
    /// whichever profile happens to sort first in the store.
    /// </summary>
    public interface IUniversalLastProfileStore
    {
        Guid? GetLastProfileId(IUniversalController controller);
        void SetLastProfileId(IUniversalController controller, Guid profileId);
    }

    public sealed class UniversalLastProfileStore : IUniversalLastProfileStore
    {
        public Guid? GetLastProfileId(IUniversalController controller)
        {
            string controllerKey = UniversalControllerDeviceOptionsStore.BuildControllerKey(controller);
            if (string.IsNullOrEmpty(controllerKey)) return null;

            string stored = AppGlobalDataSingleton.Instance.GetLastUniversalProfileId(controllerKey);
            return Guid.TryParse(stored, out Guid profileId) && profileId != Guid.Empty
                ? profileId
                : null;
        }

        public void SetLastProfileId(IUniversalController controller, Guid profileId)
        {
            if (profileId == Guid.Empty) return;

            string controllerKey = UniversalControllerDeviceOptionsStore.BuildControllerKey(controller);
            if (string.IsNullOrEmpty(controllerKey)) return;

            AppGlobalData appGlobal = AppGlobalDataSingleton.Instance;
            if (!Directory.Exists(appGlobal.appdatapath))
            {
                Directory.CreateDirectory(appGlobal.appdatapath);
            }

            appGlobal.SetLastUniversalProfileId(controllerKey, profileId.ToString("D"));
        }
    }
}
