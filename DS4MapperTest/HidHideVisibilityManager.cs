using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.Universal;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DS4MapperTest
{
    public sealed class HidHideVisibilityManager
    {
        private const string HIDHIDE_INSTALL_DIR =
            @"C:\Program Files\Nefarius Software Solutions\HidHide\x64";
        private const string HIDHIDE_CLI_EXE = "HidHideCLI.exe";
        private const string SESSION_STATE_FILENAME = "HidHideSession.json";

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

        private string SessionStatePath => string.IsNullOrEmpty(appGlobal?.appdatapath)
            ? null
            : Path.Combine(appGlobal.appdatapath, SESSION_STATE_FILENAME);

        // Cloaking and the hidden device list are driver state, not app state:
        // they outlive the process that set them. Only ClearSessionOverrides
        // undid them, and that runs solely on the clean shutdown path, so being
        // killed from Task Manager or losing power left the controller hidden
        // from every other application until the user found HidHide and fixed
        // it by hand. Recording what was changed lets the next launch put it
        // back before anything else touches HidHide.
        public void RecoverOrphanedSession()
        {
            lock (syncRoot)
            {
                string statePath = SessionStatePath;
                if (string.IsNullOrEmpty(statePath) || !File.Exists(statePath)) return;

                try
                {
                    HidHideSessionState state =
                        JsonConvert.DeserializeObject<HidHideSessionState>(
                            File.ReadAllText(statePath));
                    if (state == null) return;

                    foreach (string hiddenPath in state.HiddenDevices ?? new List<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(hiddenPath))
                        {
                            sessionHiddenDevices.Add(hiddenPath);
                        }
                    }

                    sessionRegisteredApp = state.RegisteredApp;
                    sessionEnabledCloak = state.EnabledCloak;

                    if (sessionHiddenDevices.Count == 0 &&
                        !sessionRegisteredApp &&
                        !sessionEnabledCloak)
                    {
                        return;
                    }

                    if (!IsAvailable)
                    {
                        logger.Warn("A previous session left devices hidden but HidHide is " +
                            "no longer available to restore them.");
                        return;
                    }

                    logger.Info($"Restoring {sessionHiddenDevices.Count} device(s) hidden by a " +
                        "previous session that did not shut down cleanly.");
                    RestoreSessionOverrides();
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException ||
                    ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    logger.Warn(ex, "Could not restore HidHide state left by a previous session.");
                }
                finally
                {
                    sessionHiddenDevices.Clear();
                    sessionRegisteredApp = false;
                    sessionEnabledCloak = false;
                    DeleteSessionState();
                }
            }
        }

        // Always called with syncRoot held.
        private void PersistSessionState()
        {
            string statePath = SessionStatePath;
            if (string.IsNullOrEmpty(statePath)) return;

            try
            {
                if (sessionHiddenDevices.Count == 0 &&
                    !sessionRegisteredApp &&
                    !sessionEnabledCloak)
                {
                    DeleteSessionState();
                    return;
                }

                AtomicFileWriter.WriteText(statePath, JsonConvert.SerializeObject(
                    new HidHideSessionState
                    {
                        HiddenDevices = sessionHiddenDevices.ToList(),
                        RegisteredApp = sessionRegisteredApp,
                        EnabledCloak = sessionEnabledCloak,
                    }));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.Warn(ex, "Could not record HidHide session state.");
            }
        }

        private void DeleteSessionState()
        {
            string statePath = SessionStatePath;
            if (string.IsNullOrEmpty(statePath)) return;

            try
            {
                if (File.Exists(statePath)) File.Delete(statePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.Warn(ex, "Could not clear the HidHide session state file.");
            }
        }

        private sealed class HidHideSessionState
        {
            public List<string> HiddenDevices { get; set; }
            public bool RegisteredApp { get; set; }
            public bool EnabledCloak { get; set; }
        }

        public bool IsAvailable =>
            appGlobal?.hidHideInstalled == true &&
            !string.IsNullOrWhiteSpace(cliPath) &&
            File.Exists(cliPath);

        public void Reconcile(
            IEnumerable<InputDeviceBase> devices,
            IEnumerable<UniversalControllerVisibilityTarget> universalDevices = null)
        {
            lock (syncRoot)
            {
                if (!IsAvailable)
                {
                    return;
                }

                IEnumerable<InputDeviceBase> deviceEnumerable = devices ?? Enumerable.Empty<InputDeviceBase>();
                IEnumerable<UniversalControllerVisibilityTarget> universalEnumerable =
                    universalDevices ?? Enumerable.Empty<UniversalControllerVisibilityTarget>();
                bool anyWantsHiding = deviceEnumerable.Any(device =>
                    device?.Synced == true &&
                    device.DeviceOptions?.HidePhysicalController == true) ||
                    universalEnumerable.Any(device =>
                        device?.Synced == true &&
                        device.Options?.HidePhysicalController == true);

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
                        ResolveDesiredHiddenDevices(deviceEnumerable, universalEnumerable);
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

                        RunCli("--dev-hide", deviceInstancePath);
                        sessionHiddenDevices.Add(deviceInstancePath);
                        logger.Info($"HidHide hidden device '{deviceInstancePath}'");
                    }

                    string[] stalePaths = sessionHiddenDevices.Except(desiredHiddenDevices,
                        StringComparer.OrdinalIgnoreCase).ToArray();
                    foreach (string stalePath in stalePaths)
                    {
                        RunCli("--dev-unhide", stalePath);
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
                finally
                {
                    // Recorded even when the reconcile failed part way through:
                    // whatever was hidden before the failure still needs undoing
                    // if this process never gets to shut down cleanly.
                    PersistSessionState();
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
                    DeleteSessionState();
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
                finally
                {
                    // A cleanup that only got part way through leaves the rest
                    // recorded for the next launch to finish.
                    PersistSessionState();
                }
            }
        }

        private void RestoreSessionOverrides()
        {
            foreach (string hiddenPath in sessionHiddenDevices.ToArray())
            {
                RunCli("--dev-unhide", hiddenPath);
                logger.Info($"HidHide restored device '{hiddenPath}'");
            }
            sessionHiddenDevices.Clear();

            if (sessionRegisteredApp)
            {
                RunCli("--app-unreg", appPath);
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

        private HashSet<string> ResolveDesiredHiddenDevices(
            IEnumerable<InputDeviceBase> devices,
            IEnumerable<UniversalControllerVisibilityTarget> universalDevices)
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

            foreach (UniversalControllerVisibilityTarget device in universalDevices)
            {
                if (device?.Synced != true ||
                    device.Options == null ||
                    !device.Options.HidePhysicalController)
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

            foreach (string target in ExpandHideTarget(deviceInstancePath, inventory))
            {
                yield return target;
            }
        }

        private IEnumerable<string> ResolveHideTargets(
            UniversalControllerVisibilityTarget device,
            HidHideDeviceInventory inventory)
        {
            string deviceInstancePath = ResolveCurrentDeviceInstancePath(device.Identity);
            if (!string.IsNullOrEmpty(deviceInstancePath))
            {
                foreach (string target in ExpandHideTarget(deviceInstancePath, inventory))
                {
                    yield return target;
                }

                yield break;
            }

            foreach (HidHideCliDevice entry in inventory.FindByVendorProduct(
                device.Identity?.VendorId,
                device.Identity?.ProductId))
            {
                foreach (string target in ExpandHideTarget(entry.DeviceInstancePath, inventory))
                {
                    yield return target;
                }
            }
        }

        private IEnumerable<string> ExpandHideTarget(
            string deviceInstancePath,
            HidHideDeviceInventory inventory)
        {
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

        private static string ResolveCurrentDeviceInstancePath(InputDeviceBase device)
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

        internal static string ResolveCurrentDeviceInstancePath(UniversalDeviceIdentity identity)
        {
            string devicePath = identity?.DevicePath;
            if (string.IsNullOrWhiteSpace(devicePath) ||
                devicePath.StartsWith("xinput", StringComparison.OrdinalIgnoreCase))
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
                RunCli("--app-reg", appPath);
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

        private const int CLI_TIMEOUT_MS = 30000;

        private string RunCli(params string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = cliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cliPath),
            };

            // ArgumentList quotes each value the way the Windows command line
            // parser expects. Building one string by hand meant a device path
            // containing a quote produced a mangled argument.
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException(
                    $"Could not start HidHideCLI at '{cliPath}'.");

            // Both pipes are drained at once. Reading them one after the other
            // deadlocks as soon as the child fills the buffer of the stream not
            // being read, which for a redirected pipe is only a few kilobytes.
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(CLI_TIMEOUT_MS))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException ||
                    ex is System.ComponentModel.Win32Exception)
                {
                    // Already gone, or cannot be killed. The throw below is
                    // what matters either way.
                }

                throw new InvalidOperationException(
                    $"HidHideCLI did not finish within {CLI_TIMEOUT_MS} ms. " +
                    $"Args='{string.Join(" ", arguments)}'");
            }

            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"HidHideCLI failed with exit code {process.ExitCode}. " +
                    $"Args='{string.Join(" ", arguments)}' StdErr='{stderr}'");
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

            public IEnumerable<HidHideCliDevice> FindByVendorProduct(
                ushort? vendorId,
                ushort? productId)
            {
                if (!vendorId.HasValue || !productId.HasValue)
                {
                    return Enumerable.Empty<HidHideCliDevice>();
                }

                string vid = $"VID_{vendorId.Value:X4}";
                string pid = $"PID_{productId.Value:X4}";
                return byInstancePath.Values.Where(device =>
                    device.Present &&
                    device.DeviceInstancePath?.IndexOf(vid, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    device.DeviceInstancePath?.IndexOf(pid, StringComparison.OrdinalIgnoreCase) >= 0);
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
