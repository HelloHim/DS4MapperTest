using DS4MapperTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Reflection;

namespace DS4MapperUnitTests
{
    // Step 7 cutover: proves the legacy per-device reader/mapper types and
    // the BackendManager methods that used to construct and register them
    // are actually gone from the compiled assembly, not merely unreferenced.
    // The SDL3 universal backend and the native Steam Controller adapter
    // (behind UniversalControllerManager) are the only paths left that can
    // own a physical controller.
    [TestClass]
    public class LegacyCutoverTests
    {
        [TestMethod]
        public void LegacyControllerFamilyTypesNoLongerExist()
        {
            string[] removedTypeNames =
            {
                "DS4Mapper", "DS4Reader", "DS4Device", "DS4Enumerator",
                "DualSenseMapper", "DualSenseReader", "DualSenseDevice",
                "JoyConMapper", "JoyConReader", "JoyConDevice",
                "SwitchProMapper", "SwitchProReader", "SwitchProDevice",
                "SteamControllerMapper", "SteamControllerEnumerator",
                "SteamControllerTritonMapper", "SteamControllerTritonDevice", "SteamControllerTritionReader",
                "Ultimate2WirelessMapper", "Ultimate2WirelessReader", "Ultimate2WirelessDevice",
                "ControllerConfigWin", "ControllerConfigViewModel",
            };

            Type[] allTypes = typeof(BackendManager).Assembly.GetTypes();
            foreach (string removedTypeName in removedTypeNames)
            {
                bool stillExists = allTypes.Any(type => type.Name == removedTypeName);
                Assert.IsFalse(stillExists, $"{removedTypeName} should have been removed in the SDL3 cutover.");
            }
        }

        [TestMethod]
        public void BackendManagerHasNoLegacyPerDeviceMappingPath()
        {
            string[] removedMemberNames =
            {
                "PrepareAddInputDevice",
                "PrepareAddInputDeviceMini",
                "JoyConMapperCheck",
                "Device_SyncedChanged",
                "Device_Removal",
                "PrepareSyncedInputDevice",
            };

            MemberInfo[] members = typeof(BackendManager).GetMembers(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            foreach (string removedMemberName in removedMemberNames)
            {
                bool stillExists = members.Any(member => member.Name == removedMemberName);
                Assert.IsFalse(stillExists,
                    $"BackendManager.{removedMemberName} should have been removed with the dead legacy mapper loop.");
            }
        }

        [TestMethod]
        public void DeviceEnumeratorOnlyIdentifiesSteamController()
        {
            // Modern controllers are owned entirely by the SDL3 universal
            // backend, which enumerates devices itself; DeviceEnumerator
            // now exists solely to identify the original Steam Controller
            // for the retained native adapter.
            MethodInfo prepareDeviceMapper = typeof(DeviceEnumerator).GetMethod(
                "PrepareDeviceMapper", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(prepareDeviceMapper,
                "DeviceEnumerator.PrepareDeviceMapper should have been removed with the dead legacy mapper loop.");
        }
    }
}
