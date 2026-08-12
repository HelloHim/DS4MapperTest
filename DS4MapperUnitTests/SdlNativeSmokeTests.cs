using DS4MapperTest.SdlDiagnostics;
using System.Runtime.InteropServices;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class SdlNativeSmokeTests
    {
        [TestMethod]
        public void DeployedSdl3BinaryLoadsAndInitialises()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("SDL3 native smoke test is Windows-only.");
            }

            string expectedPath = SdlNativeLibraryLocator.GetExpectedOutputPath(AppContext.BaseDirectory);
            Assert.IsTrue(File.Exists(expectedPath), $"Expected SDL3.dll in test output: {expectedPath}");

            Sdl3NativeDiagnosticApi api = new Sdl3NativeDiagnosticApi();
            Assert.IsTrue(api.Initialise(out string error), error);
            try
            {
                SdlDiagnosticVersionInfo version = api.VersionInfo;
                Assert.IsFalse(string.IsNullOrWhiteSpace(version.NativeVersion), "SDL native version should be available.");
            }
            finally
            {
                api.Shutdown();
            }
        }
    }
}
