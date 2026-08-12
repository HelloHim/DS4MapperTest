using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DS4MapperTest.SdlDiagnostics
{
    internal sealed class SdlDiagnosticReportWriter
    {
        public const string ReportFolderName = "SdlDiagnostics";
        private readonly string outputRoot;

        public SdlDiagnosticReportWriter(string logsRoot)
        {
            if (string.IsNullOrWhiteSpace(logsRoot))
            {
                throw new ArgumentException("A log root is required for SDL diagnostic reports.", nameof(logsRoot));
            }

            outputRoot = Path.Combine(logsRoot, ReportFolderName);
        }

        public string WriteReport(SdlDiagnosticSessionSnapshot snapshot, uint? selectedInstanceId = null)
        {
            Directory.CreateDirectory(outputRoot);
            SdlDiagnosticSessionSnapshot report = selectedInstanceId.HasValue
                ? new SdlDiagnosticSessionSnapshot
                {
                    TimestampUtc = snapshot.TimestampUtc,
                    Version = snapshot.Version,
                    Devices = snapshot.Devices.Where(item => item.InstanceId == selectedInstanceId.Value).ToList(),
                    Events = snapshot.Events.ToList(),
                    Errors = snapshot.Errors.ToList(),
                }
                : snapshot;

            string deviceName = report.Devices.FirstOrDefault()?.Info?.Name ?? "no-device";
            string path = GetUniquePath(Path.Combine(outputRoot, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{SanitizeFileName(deviceName)}.json"));
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
            return path;
        }

        public static string SanitizeFileName(string value)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                clean = clean.Replace(invalid, '-');
            }

            clean = new string(clean.Select(ch => char.IsControl(ch) ? '-' : ch).ToArray());
            while (clean.Contains("--"))
            {
                clean = clean.Replace("--", "-");
            }

            return clean.Length > 80 ? clean.Substring(0, 80) : clean;
        }

        private static string GetUniquePath(string desiredPath)
        {
            if (!File.Exists(desiredPath))
            {
                return desiredPath;
            }

            string directory = Path.GetDirectoryName(desiredPath);
            string name = Path.GetFileNameWithoutExtension(desiredPath);
            string extension = Path.GetExtension(desiredPath);
            for (int index = 1; ; index++)
            {
                string candidate = Path.Combine(directory, $"{name}-{index}{extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}
