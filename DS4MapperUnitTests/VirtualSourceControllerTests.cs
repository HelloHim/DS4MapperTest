using DS4MapperTest;
using DS4MapperTest.SdlDiagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    // The mapper ignores controllers hanging off a root-enumerated bus so it
    // never consumes the pad it just emitted. Other applications emit their
    // virtual pads the same way, though, and those are real inputs the user
    // wants to map, so the bus has to decide it rather than the topology alone.
    [TestClass]
    public class VirtualSourceControllerTests
    {
        [TestMethod]
        public void BusesThatEmitMappableSourcesAreRecognised()
        {
            // reWASD emits through its own root bus, seen as ROOT\SYSTEM\nnnn with
            // this hardware id and driver provider "R Team".
            Assert.IsTrue(Util.IsSourceVirtualBusHardwareId(@"root\wjl19drv"));
            Assert.IsTrue(Util.IsSourceVirtualBusHardwareId(@"ROOT\HIDGAMEMAP"));
            Assert.IsTrue(Util.IsSourceVirtualBusHardwareId(@"Nefarius\ViGEmBus\Gen1"));
            Assert.IsTrue(Util.IsSourceVirtualBusHardwareId(@"ROOT\VHUSB3HC"));
        }

        [TestMethod]
        public void UnrelatedBusesAreStillTreatedAsOutput()
        {
            Assert.IsFalse(Util.IsSourceVirtualBusHardwareId(@"root\FakerInput"));
            Assert.IsFalse(Util.IsSourceVirtualBusHardwareId(@"root\HidHide"));
            Assert.IsFalse(Util.IsSourceVirtualBusHardwareId(@"USB\VID_054C&PID_05C4"));
            Assert.IsFalse(Util.IsSourceVirtualBusHardwareId(string.Empty));
            Assert.IsFalse(Util.IsSourceVirtualBusHardwareId(null));
        }

        [TestMethod]
        public void AnotherApplicationsVirtualPadIsNotMistakenForOwnOutput()
        {
            // A reWASD virtual DS4 reports Sony's ids and carries no marker naming
            // a virtual output stack, so nothing but the bus behind it can classify
            // it. The bus itself is covered above; this pins that none of the name
            // and id rules claim the device on their own. The path is deliberately
            // one no machine can resolve, so the result cannot depend on what
            // happens to be plugged into the machine running the test.
            SdlRawGamepadInfo info = new SdlRawGamepadInfo
            {
                InstanceId = 1,
                Name = "PS4 Controller",
                MappingName = "030000004c050000cc09000000000000,PS4 Controller,a:b1,b:b2",
                Guid = "030000004c050000cc09000000000000",
                VendorId = 0x054C,
                ProductId = 0x05C4,
                DevicePath = @"\\?\HID#VID_054C&PID_05C4#0&unresolvable&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
                IsMappedGamepad = true,
            };

            Assert.IsFalse(SdlUniversalStateTranslator.IsKnownVirtualOutputController(info));
        }

        [TestMethod]
        public void OwnViiperOutputIsStillIgnored()
        {
            SdlRawGamepadInfo info = new SdlRawGamepadInfo
            {
                InstanceId = 2,
                Name = "PS4 Controller",
                MappingName = "usbip virtual device",
                VendorId = 0,
                ProductId = 0,
                DevicePath = @"\\?\USB#VID_054C&PID_05C4#usbip",
                IsMappedGamepad = true,
            };

            Assert.IsTrue(SdlUniversalStateTranslator.IsKnownVirtualOutputController(info));
        }
    }
}
