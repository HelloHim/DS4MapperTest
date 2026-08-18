using DS4MapperTest.SteamControllerLibrary;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DS4MapperTest
{
    public sealed class HidHideVisibilityManager
    {
        private const string HIDHIDE_INSTALL_DIR =
            @"C:\Program Files\Nefarius Software Solutions\HidHide\x64";
        private const string HIDHIDE_CLI_EXE = "HidHideCLI.exe";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly AppGlobalData appGlobal;
        private readonly string appPath;
        private readonly string cliPath;
        private readonly object syncRoot = new object();
        private readonly HashSet<string> sessionHiddenDevices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool sessionRegisteredApp;
        private bool sessionEnabledCloak;

        public HidHideVisibilityManager(AppGlobalData appGlobal)
        {
            this.appGlobal = appGlobal;
            appPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            cliPath = ResolveCliPath();
        }

        public bool IsAvailable =>
            appGlobal?.hidHideInstalled == true &&
            !string.IsNullOrWhiteSpace(cliPath) &&
            File.Exists(cliPath);

        public void Reconcile(IEnumerable<InputDeviceBase> devices)
        {
            lock (syncRoot)
            {
                if (!IsAvailable)
                {
                    return;
                }

                IEnumerable<InputDeviceBase> deviceEnumerable = devices ?? Enumerable.Empty<InputDeviceBase>();
                bool anyWantsHiding = deviceEnumerable.Any(device =>
                    device?.Synced == true &&
                    device.DeviceOptions?.HidePhysicalController == true);

                if (!anyWantsHiding && sessionHiddenDevices.Count == 0)
                {
                    // Nothing wants to be hidden and nothing is hidden from an earlier
                    // call this session, so there is nothing to reconcile. Skip out before
                    // spawning HidHideCLI.exe (each call below shells out to it), since this
                    // runs once per controller on every startup/hotplug.
                    return;
                }

                try
                {
                    HashSet<string> desiredHiddenDevices =
                        ResolveDesiredHiddenDevices(deviceEnumerable);
                    HashSet<string> currentHiddenDevices = GetHiddenDevices();

                    if (desiredHiddenDevices.Count > 0)
                    {
                        EnsureAppRegistered();
                        EnsureCloakEnabled();
                    }

                    foreach (string deviceInstancePath in desiredHiddenDevices)
                    {
                        if (currentHiddenDevices.Contains(deviceInstancePath))
                        {
                            continue;
                        }

                        RunCli($"--dev-hide \"{EscapeCliArg(deviceInstancePath)}\"");
                        sessionHiddenDevices.Add(deviceInstancePath);
                        logger.Info($"HidHide hidden device '{deviceInstancePath}'");
                    }

                    string[] stalePaths = sessionHiddenDevices.Except(desiredHiddenDevices,
                        StringComparer.OrdinalIgnoreCase).ToArray();
                    foreach (string stalePath in stalePaths)
                    {
                        RunCli($"--dev-unhide \"{EscapeCliArg(stalePath)}\"");
                        sessionHiddenDevices.Remove(stalePath);
                        logger.Info($"HidHide restored device '{stalePath}'");
                    }

                    if (desiredHiddenDevices.Count == 0)
                    {
                        RestoreSessionOverrides();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    logger.Warn(ex, "HidHide reconcile failed; controller visibility left unchanged. " +
                        "HidHideCLI usually requires DS4MapperTest to run as Administrator.");
                }
            }
        }

        public void ClearSessionOverrides()
        {
            lock (syncRoot)
            {
                if (!IsAvailable)
                {
                    sessionHiddenDevices.Clear();
                    sessionRegisteredApp = false;
                    sessionEnabledCloak = false;
                    return;
                }

                try
                {
                    RestoreSessionOverrides();
                }
                catch (InvalidOperationException ex)
                {
                    logger.Warn(ex, "HidHide session cleanup failed; some devices or the app " +
                        "registration may remain hidden/registered until HidHideCLI is run elevated.");
                }
            }
        }

        private void RestoreSessionOverrides()
        {
            foreach (string hiddenPath in sessionHiddenDevices.ToArray())
            {
                RunCli($"--dev-unhide \"{EscapeCliArg(hiddenPath)}\"");
                logger.Info($"HidHide restored device '{hiddenPath}'");
            }
            sessionHiddenDevices.Clear();

            if (sessionRegisteredApp)
            {
                RunCli($"--app-unreg \"{EscapeCliArg(appPath)}\"");
                sessionRegisteredApp = false;
                logger.Info($"HidHide unregistered app '{appPath}'");
            }

            if (sessionEnabledCloak)
            {
                RunCli("--cloak-off");
                sessionEnabledCloak = false;
                logger.Info("HidHide cloaking disabled");
            }
        }

        private HashSet<string> ResolveDesiredHiddenDevices(IEnumerable<InputDeviceBase> devices)
        {
            HashSet<string> desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HidHideDeviceInventory inventory = GetDeviceInventory();

            foreach (InputDeviceBase device in devices)
            {
                if (device?.Synced != true ||
                    device.DeviceOptions == null ||
                    !device.DeviceOptions.HidePhysicalController)
                {
                    continue;
                }

                foreach (string devicePath in ResolveHideTargets(device, inventory))
                {
                    desired.Add(devicePath);
                }
            }

            return desired;
        }

        private IEnumerable<string> ResolveHideTargets(InputDeviceBase device,
            HidHideDeviceInventory inventory)
        {
            string deviceInstancePath = ResolveCurrentDeviceInstancePath(device);
            if (string.IsNullOrEmpty(deviceInstancePath))
            {
                yield break;
            }

            HidHideCliDevice entry = inventory.Find(deviceInstancePath);
            if (entry == null)
            {
                yield return deviceInstancePath;
                yield break;
            }

            if (!string.IsNullOrEmpty(entry.BaseContainerDeviceInstancePath))
            {
                foreach (HidHideCliDevice sibling in inventory.FindByBaseContainer(
                    entry.BaseContainerDeviceInstancePath))
                {
                    if (sibling.Present)
                    {
                        yield return sibling.DeviceInstancePath;
                    }
                }

                yield break;
            }

            yield return entry.DeviceInstancePath;
        }

        private string ResolveCurrentDeviceInstancePath(InputDeviceBase device)
        {
            string devicePath = device switch
            {
                SteamControllerDevice steamController => steamController.HidDevice?.DevicePath,
                _ => string.Empty,
            };

            if (string.IsNullOrEmpty(devicePath))
            {
                return string.Empty;
            }

            return Util.GetInstanceIdFromDevicePath(devicePath);
        }

        private void EnsureAppRegistered()
        {
            if (sessionRegisteredApp)
            {
                return;
            }

            HashSet<string> registeredApps = GetRegisteredApps();
            if (!registeredApps.Contains(appPath))
            {
                RunCli($"--app-reg \"{EscapeCliArg(appPath)}\"");
                sessionRegisteredApp = true;
                logger.Info($"HidHide registered app '{appPath}'");
            }
        }

        private void EnsureCloakEnabled()
        {
            bool cloakEnabled = GetCloakEnabled();
            if (!cloakEnabled)
            {
                RunCli("--cloak-on");
                sessionEnabledCloak = true;
                logger.Info("HidHide cloaking enabled");
            }
        }

        private HashSet<string> GetRegisteredApps()
        {
            string output = RunCli("--app-list");
            return ParseSimpleArgList(output, "--app-reg");
        }

        private HashSet<string> GetHiddenDevices()
        {
            string output = RunCli("--dev-list");
            return ParseSimpleArgList(output, "--dev-hide");
        }

        private bool GetCloakEnabled()
        {
            string output = RunCli("--cloak-state");
            string[] lines = SplitOutputLines(output);
            return lines.Any(line => line.Equals("--cloak-on",
                StringComparison.OrdinalIgnoreCase));
        }

        private HidHideDeviceInventory GetDeviceInventory()
        {
            string output = RunCli("--dev-all");
            List<HidHideCliGroup> groups =
                JsonConvert.DeserializeObject<List<HidHideCliGroup>>(output) ??
                new List<HidHideCliGroup>();
            return new HidHideDeviceInventory(groups.SelectMany(group =>
                (IEnumerable<HidHideCliDevice>)(group.Devices ?? new List<HidHideCliDevice>())));
        }

        private HashSet<string> ParseSimpleArgList(string output, string prefix)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in SplitOutputLines(output))
            {
                if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int firstQuote = line.IndexOf('"');
                int lastQuote = line.LastIndexOf('"');
                if (firstQuote >= 0 && lastQuote > firstQuote)
                {
                    values.Add(line.Substring(firstQuote + 1, lastQuote - firstQuote - 1));
                }
            }

            return values;
        }

        private string[] SplitOutputLines(string output)
        {
            return output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        private string RunCli(string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cliPath),
            };

            using Process process = Process.Start(startInfo);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"HidHideCLI failed with exit code {process.ExitCode}. Args='{arguments}' StdErr='{stderr}'");
            }

            return stdout;
        }

        private string ResolveCliPath()
        {
            string[] candidates =
            {
                Path.Combine(HIDHIDE_INSTALL_DIR, HIDHIDE_CLI_EXE),
                Path.Combine(AppContext.BaseDirectory, HIDHIDE_CLI_EXE),
            };

            return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        }

        private static string EscapeCliArg(string value)
        {
            return value?.Replace("\"", "\\\"") ?? string.Empty;
        }

        private sealed class HidHideDeviceInventory
        {
            private readonly Dictionary<string, HidHideCliDevice> byInstancePath;
            private readonly ILookup<string, HidHideCliDevice> byBaseContainer;

            public HidHideDeviceInventory(IEnumerable<HidHideCliDevice> devices)
            {
                HidHideCliDevice[] deviceArray = devices
                    .Where(device => !string.IsNullOrEmpty(device?.DeviceInstancePath))
                    .ToArray();
                byInstancePath = deviceArray.ToDictionary(device => device.DeviceInstancePath,
                    StringComparer.OrdinalIgnoreCase);
                byBaseContainer = deviceArray.ToLookup(
                    device => device.BaseContainerDeviceInstancePath ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
            }

            public HidHideCliDevice Find(string deviceInstancePath)
            {
                byInstancePath.TryGetValue(deviceInstancePath, out HidHideCliDevice result);
                return result;
            }

            public IEnumerable<HidHideCliDevice> FindByBaseContainer(string baseContainerPath)
            {
                return byBaseContainer[baseContainerPath ?? string.Empty];
            }
        }

        private sealed class HidHideCliGroup
        {
            [JsonProperty("devices")]
            public List<HidHideCliDevice> Devices { get; set; }
        }

        private sealed class HidHideCliDevice
        {
            [JsonProperty("present")]
            public bool Present { get; set; }

            [JsonProperty("deviceInstancePath")]
            public string DeviceInstancePath { get; set; }

            [JsonProperty("baseContainerDeviceInstancePath")]
            public string BaseContainerDeviceInstancePath { get; set; }
        }
    }
}
