using DS4MapperTest;
using DS4MapperTest.SdlDiagnostics;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class SdlDiagnosticReportWriterTests
    {
        [TestMethod]
        public void ReportWriterSanitisesFileNamesAndAvoidsOverwrite()
        {
            string root = CreateTempDirectory();
            try
            {
                SdlDiagnosticReportWriter writer = new SdlDiagnosticReportWriter(Path.Combine(root, "Logs"));
                SdlDiagnosticSessionSnapshot snapshot = CreateSnapshot("Pad:One");

                string first = writer.WriteReport(snapshot, selectedInstanceId: 1);
                string second = writer.WriteReport(snapshot, selectedInstanceId: 1);

                Assert.IsTrue(File.Exists(first));
                Assert.IsTrue(File.Exists(second));
                Assert.AreNotEqual(first, second);
                Assert.IsFalse(Path.GetFileName(first).Contains(":"));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void ReportWriterStoresOutputUnderIsolatedDevelopmentLogRoot()
        {
            string root = CreateTempDirectory();
            try
            {
                ApplicationDataPathSet paths = ApplicationDataPathResolver.Resolve(ApplicationDataBuildFlavor.Development, root);
                SdlDiagnosticReportWriter writer = new SdlDiagnosticReportWriter(paths.LogsPath);

                string reportPath = writer.WriteReport(CreateSnapshot("Controller"), selectedInstanceId: 1);
                string json = File.ReadAllText(reportPath);

                Assert.IsTrue(IsUnderRoot(reportPath, paths.LogsPath));
                Assert.IsTrue(IsUnderRoot(reportPath, paths.RootPath));
                Assert.IsFalse(json.Contains(root, StringComparison.OrdinalIgnoreCase));
                Assert.AreEqual("Controller", JObject.Parse(json).SelectToken("$.Devices[0].Info.Name")?.Value<string>());
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static SdlDiagnosticSessionSnapshot CreateSnapshot(string name)
        {
            return new SdlDiagnosticSessionSnapshot
            {
                Version = new SdlDiagnosticVersionInfo { NativeVersion = "3.4.14" },
                Devices = new List<SdlDiagnosticDeviceSnapshot>
                {
                    new SdlDiagnosticDeviceSnapshot
                    {
                        InstanceId = 1,
                        Connected = true,
                        Info = new SdlRawGamepadInfo
                        {
                            InstanceId = 1,
                            Name = name,
                            Guid = "guid",
                            VendorId = 1,
                            ProductId = 2,
                            BestEffortPersistentKey = "guid-guid|vid-0001|pid-0002|serial-unknown",
                            Buttons = new List<SdlRawButtonState> { new SdlRawButtonState { Index = 0, Name = "South", Supported = true, Pressed = true } },
                            Axes = new List<SdlRawAxisState> { new SdlRawAxisState { Index = 0, Name = "LeftX", Supported = true, RawValue = 100 } },
                        },
                    },
                },
            };
        }

        private static bool IsUnderRoot(string childPath, string rootPath)
        {
            string relative = Path.GetRelativePath(rootPath, childPath);
            return relative != "." && !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }

        private static string CreateTempDirectory()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DS4MapperTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }
    }
}
