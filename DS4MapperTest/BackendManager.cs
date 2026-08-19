using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using Nefarius.ViGEm.Client;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using DS4MapperTest.PhysicalMouse;
using DS4MapperTest.SdlDiagnostics;
using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using NLog;

namespace DS4MapperTest
{
    public class DebugEventArgs : EventArgs
    {
        protected DateTime m_Time = DateTime.Now;
        protected string message = string.Empty;
        protected bool warning = false;
        //protected bool temporary = false;
        //public DebugEventArgs(string message, bool warn, bool temporary = false)
        public DebugEventArgs(string message, bool warn)
        {
            this.message = message;
            warning = warn;
            //this.temporary = temporary;
        }

        public DateTime Time => m_Time;
        public string Message => message;
        public bool Warning => warning;
        //public bool Temporary => temporary;
    }

    public class BackendManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public const int CONTROLLER_LIMIT = 8;
        private const string DEFAULT_VKBM_IDENTIFIER = SendInputHandler.IDENTIFIER;

        private Thread vbusThr;
        private bool isRunning;
        public bool IsRunning
        {
            get => isRunning;
        }

        private bool changingService;
        public bool ChangingService
        {
            get => changingService;
        }

        public event EventHandler ServiceStarted;
        public event EventHandler PreServiceStop;
        public event EventHandler ServiceStopped;
        public event EventHandler PhysicalMouseStatusChanged;
        //public event EventHandler HotplugFinished;

        private VirtualKBMBase virtualEventHandler;// = new FakerInputHandler();
        public VirtualKBMBase EventInputHandler => virtualEventHandler;

        private VirtualKBMMapping eventInputMapping;// = new FakerInputMapping();
        public VirtualKBMMapping EventInputMapping => eventInputMapping;
        private MouseOutputDispatcher mouseOutputDispatcher;
        private readonly MouseOutputRoutingController mouseOutputRoutingController;
        public MouseOutputRoutingController MouseOutputRoutingController =>
            mouseOutputRoutingController;
        private SteamControllerUniversalBackend steamControllerUniversalBackend;

        // Phase-2 physical-mouse forwarding. Owned here (not by the WPF UI)
        // so it starts/stops with the backend service regardless of which
        // window, if any, is open. See PhysicalMouseService for the actual
        // capture -> FakerInput wiring.
        private readonly PhysicalMouseService physicalMouseService = new PhysicalMouseService();
        public PhysicalMouseServiceStatus PhysicalMouseStatus => physicalMouseService.Status;
        private readonly HidHideVisibilityManager hidHideVisibilityManager;
        private UniversalMappingRuntime universalMappingRuntime;
        private Thread universalMappingThread;
        private Thread nativeSteamControllerDiscoveryThread;
        private volatile bool stopUniversalMappingThread;
        public UniversalMappingRuntime UniversalMappingRuntime => universalMappingRuntime;

        private Dictionary<int, Mapper> mapperDict;
        public Dictionary<int, Mapper> MapperDict
        {
            get => mapperDict;
        }
        private Dictionary<InputDeviceBase, DeviceReaderBase> deviceReadersMap;
        private Dictionary<InputDeviceBase, Mapper> deviceMapperMap =
            new Dictionary<InputDeviceBase, Mapper>();

        private InputDeviceBase[] controllerList =
            new InputDeviceBase[CONTROLLER_LIMIT];
        public InputDeviceBase[] ControllerList
        {
            get => controllerList;
        }

        public DeviceReaderBase GetDeviceReader(InputDeviceBase device)
        {
            if (device == null) return null;
            deviceReadersMap.TryGetValue(device, out DeviceReaderBase reader);
            return reader;
        }

        private Dictionary<InputDeviceType, ProfileList> deviceProfileListDict;
        public Dictionary<InputDeviceType, ProfileList> DeviceProfileListDict
        {
            get => deviceProfileListDict;
        }

        private Thread eventDispatchThread;
        private Dispatcher eventDispatcher;
        public Dispatcher EventDispatcher
        {
            get => eventDispatcher;
        }

        private ReaderWriterLockSlim _hotplugLock = new ReaderWriterLockSlim();

        private AppGlobalData appGlobal;
        //private DS4Enumerator enumerator;
        private DeviceEnumerator testEnumerator;
        //private List<DeviceEnumeratorBase> enumeratorList;

        //private ViGEmClient vigemTestClient = null;
        private ArgumentParser _argParser;

        public delegate void HotplugControllerHandler(InputDeviceBase device, int ind);
        public event HotplugControllerHandler HotplugController;
        public event HotplugControllerHandler UnplugController;
        public event EventHandler<DebugEventArgs> Debug;

        public BackendManager(ArgumentParser argParse, AppGlobalData appGlobal)
        {
            _argParser = argParse;
            this.appGlobal = appGlobal;
            mouseOutputRoutingController = new MouseOutputRoutingController(appGlobal);
            _logCb = (level, message) =>
            {
                string text = $"VIIPER[{level}] {message}";
                if (level >= VIIPERLogLevel.Error)
                {
                    logger.Error(text);
                }
                else if (level >= VIIPERLogLevel.Warn)
                {
                    logger.Warn(text);
                }
                else
                {
                    logger.Info(text);
                }
            };
            physicalMouseService.StatusChanged += (_, _) => PhysicalMouseStatusChanged?.Invoke(this, EventArgs.Empty);
            hidHideVisibilityManager = new HidHideVisibilityManager(appGlobal);

            mapperDict = new Dictionary<int, Mapper>();
            deviceReadersMap = new Dictionary<InputDeviceBase, DeviceReaderBase>();
            deviceProfileListDict = new Dictionary<InputDeviceType, ProfileList>();
            ProfileList deviceProfileList = new ProfileList(InputDeviceType.DS4);
            deviceProfileList.Refresh();
            deviceProfileListDict.Add(InputDeviceType.DS4, deviceProfileList);

            ProfileList dsDeviceProfileList = new ProfileList(InputDeviceType.DualSense);
            dsDeviceProfileList.Refresh();
            deviceProfileListDict.Add(InputDeviceType.DualSense, dsDeviceProfileList);

            ProfileList switchDeviceProfileList = new ProfileList(InputDeviceType.SwitchPro);
            switchDeviceProfileList.Refresh();
            deviceProfileListDict.Add(InputDeviceType.SwitchPro, switchDeviceProfileList);

            ProfileList joyconDeviceProfileList = new ProfileList(InputDeviceType.JoyCon);
            joyconDeviceProfileList.Refresh();
            deviceProfileListDict.Add(InputDeviceType.JoyCon, joyconDeviceProfileList);

            ProfileList steamControllerDeviceProfileList = new ProfileList(InputDeviceType.SteamController);
            steamControllerDeviceProfileList.Refresh();
            deviceProfileListDict.Add(InputDeviceType.SteamController, steamControllerDeviceProfileList);

            ProfileList steamControllerTritonDeviceProfileList = new ProfileList(InputDeviceType.SteamControllerTriton);
            steamControllerTritonDeviceProfileList.Refresh();
            deviceProfileListDict.Add(InputDeviceType.SteamControllerTriton, steamControllerTritonDeviceProfileList);

            ProfileList ult2WirelessDeviceProfileList = new ProfileList(InputDeviceType.EightBitDoUltimate2Wireless);
            ult2WirelessDeviceProfileList.Refresh();
            deviceProfileListDict.Add(InputDeviceType.EightBitDoUltimate2Wireless, ult2WirelessDeviceProfileList);

            //enumeratorList = new List<DeviceEnumeratorBase>()
            //{
            //    new DS4Enumerator(),
            //};

            //enumerator = new DS4Enumerator();
            testEnumerator = new DeviceEnumerator();

            // Initialize Crc32 table for app
            Crc32Algorithm.InitializeTable(Crc32Algorithm.DefaultPolynomial);

            using ManualResetEventSlim dispatcherReady = new ManualResetEventSlim(false);
            eventDispatchThread = new Thread(() =>
            {
                Dispatcher currentDis = Dispatcher.CurrentDispatcher;
                eventDispatcher = currentDis;
                dispatcherReady.Set();
                Dispatcher.Run();
            });
            eventDispatchThread.IsBackground = true;
            eventDispatchThread.Priority = ThreadPriority.BelowNormal;
            eventDispatchThread.Name = "BackendManager Events";
            eventDispatchThread.Start();

            if (!dispatcherReady.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out initialising the backend event dispatcher.");
            }
        }

        nuint serverHandle = 0;
        private readonly VIIPERLogCallbackDelegate _logCb;
        private readonly Xbox360RumbleCallbackDelegate _rumbleCb;

        private void EnsureUsbipAvailable()
        {
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            List<string> candidateDirs = new List<string>()
            {
                AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "USBip"),
            };

            List<string> updatedDirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                updatedDirs.AddRange(currentPath.Split(Path.PathSeparator).
                    Where(item => !string.IsNullOrWhiteSpace(item)));
            }

            bool foundUsbip = false;
            foreach (string candidateDir in candidateDirs)
            {
                if (string.IsNullOrWhiteSpace(candidateDir) || !Directory.Exists(candidateDir))
                {
                    continue;
                }

                string usbipPath = Path.Combine(candidateDir, "usbip.exe");
                if (!File.Exists(usbipPath))
                {
                    continue;
                }

                foundUsbip = true;
                if (!updatedDirs.Any(item => string.Equals(item.TrimEnd(Path.DirectorySeparatorChar),
                    candidateDir, StringComparison.OrdinalIgnoreCase)))
                {
                    updatedDirs.Insert(0, candidateDir);
                }
            }

            if (foundUsbip)
            {
                string newPath = string.Join(Path.PathSeparator.ToString(), updatedDirs);
                Environment.SetEnvironmentVariable("PATH", newPath);
                logger.Info($"USBIP runtime available via PATH. Search roots={string.Join(";", candidateDirs.Where(Directory.Exists))}");
            }
            else
            {
                logger.Warn("USBIP runtime not found in app directory or Program Files\\USBip");
            }
        }

        public bool ApplyPhysicalMouseSettings(bool enabled, string stableDeviceId, out string validationMessage)
        {
            validationMessage = null;
            if (enabled && string.IsNullOrEmpty(stableDeviceId))
            {
                validationMessage = "Select a physical mouse before enabling forwarding.";
                return false;
            }

            bool isVirtual = false;
            try
            {
                isVirtual = enabled && Util.CheckIfVirtualDevice(stableDeviceId);
            }
            catch
            {
                // The service repeats this best-effort guard. A lookup
                // failure must not destabilise controller/gyro output.
            }
            if (isVirtual)
            {
                validationMessage = "The selected device is virtual and cannot be captured.";
                return false;
            }

            appGlobal.appSettings.PhysicalMouseForwardingEnabled = enabled;
            appGlobal.appSettings.SelectedPhysicalMouseId = stableDeviceId ?? string.Empty;
            appGlobal.SaveAppSettings();

            if (isRunning)
            {
                physicalMouseService.Reconfigure(enabled, stableDeviceId,
                    mouseOutputDispatcher);
            }
            return true;
        }

        public void Start()
        {
            if (isRunning || changingService) return;

            LogDebug("Starting service");
            changingService = true;

            InitOutputKBMHandler();
            EnsureUsbipAvailable();

            // Change thread affinity of bus object to not be tied
            // to GUI thread
            vbusThr = new Thread(() =>
            {
                //vigemTestClient = new ViGEmClient();

                if (serverHandle == 0)
                {
                    USBServerConfig conf = new() { addr = "localhost:3245", write_batch_flush_interval_ms = 4 };
                    if (!LibVIIPER.NewUSBServer(ref conf, out serverHandle, _logCb))
                    {
                        Trace.WriteLine("Fatal Error: Failed to start native libVIIPER server.");
                        return;
                    }
                }
            });

            vbusThr.Priority = ThreadPriority.Normal;
            vbusThr.IsBackground = true;
            vbusThr.Start();
            vbusThr.Join(); // Wait for bus object start

            if (serverHandle != 0)
            {
                LogDebug($"VIIPER connection established");
            }
            else
            {
                LogDebug("VIIPER server unavailable. Virtual gamepad and mouse " +
                    "output are disabled for this session.", true);
            }

            mouseOutputDispatcher = new MouseOutputDispatcher(appGlobal,
                virtualEventHandler, eventInputMapping, serverHandle);
            mouseOutputRoutingController.AttachRuntime(mouseOutputDispatcher,
                isServiceRunning: false);

            bool physicalMouseEnabled = appGlobal.appSettings?.PhysicalMouseForwardingEnabled ?? false;
            string selectedPhysicalMouseId = appGlobal.appSettings?.SelectedPhysicalMouseId;
            physicalMouseService.Start(physicalMouseEnabled, selectedPhysicalMouseId,
                mouseOutputDispatcher);
            LogDebug($"Physical mouse forwarding: {physicalMouseService.Status}");

            StartUniversalMappingRuntime();
            isRunning = true;
            StartNativeSteamControllerDiscovery();

            changingService = false;
            mouseOutputRoutingController.SetServiceRunning(true);
            ServiceStarted?.Invoke(this, EventArgs.Empty);
            LogDebug("Service started with universal controller runtime");
        }

        private void InitOutputKBMHandler()
        {
            string configuredHandlerIdentifier =
                DetermineConfiguredOutputHandlerIdentifier(_argParser, appGlobal);

            switch (configuredHandlerIdentifier)
            {
                case FakerInputHandler.IDENTIFIER:
                    virtualEventHandler = new FakerInputHandler();
                    virtualEventHandler.version = new Version(appGlobal.fakerInputVersion);
                    break;
                case SendInputHandler.IDENTIFIER:
                default:
                    virtualEventHandler = GetFallbackKBMHandler();
                    break;
            }

            bool checkConnect = virtualEventHandler.Connect();
            if (!checkConnect)
            {
                virtualEventHandler.Disconnect();
                // Use fallback handler
                virtualEventHandler = GetFallbackKBMHandler();
            }

            switch (virtualEventHandler.GetIdentifier())
            {
                case FakerInputHandler.IDENTIFIER:
                    eventInputMapping = new FakerInputMapping();
                    break;
                case SendInputHandler.IDENTIFIER:
                    eventInputMapping = new SendInputMapping();
                    break;
                default: break;
            }

            eventInputMapping.PopulateConstants();
            eventInputMapping.PopulateMappings();

            ProfileSerializer.EventInputMapper = eventInputMapping;

            LogDebug($"KBM Event Handler: {virtualEventHandler.GetFullDisplayName()}");
        }

        private void StartUniversalMappingRuntime()
        {
            UniversalProfileStore store = UniversalProfileStore.CreateDefault();
            PruneNonSteamControllerDevMigrations(store);
            LegacyProfileMigrator migrator = new LegacyProfileMigrator(store);
            IReadOnlyList<LegacyProfileMigrationSource> migrationSources =
                UniversalMappingRuntime.DiscoverLegacyProfileSources(deviceProfileListDict)
                    .Where(source => source.Family == InputDeviceType.SteamController)
                    .ToArray();

            steamControllerUniversalBackend =
                new SteamControllerUniversalBackend(CreateSteamControllerSources());

            List<IUniversalControllerBackend> backends = new List<IUniversalControllerBackend>
            {
                steamControllerUniversalBackend,
                new SdlUniversalControllerBackend(new Sdl3NativeDiagnosticApi()),
            };

            UniversalControllerManager controllerManager = new UniversalControllerManager(backends);
            UniversalLastProfileStore lastProfileStore = new UniversalLastProfileStore();
            universalMappingRuntime = new UniversalMappingRuntime(
                controllerManager,
                new UniversalProfileStoreSelector(store, lastProfileStore: lastProfileStore),
                virtualEventHandler,
                eventInputMapping,
                mouseOutputDispatcher,
                serverHandle,
                migrator,
                migrationSources,
                lastProfileStore);

            bool started = universalMappingRuntime.Start();
            foreach (string error in universalMappingRuntime.StartupErrors)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    logger.Warn($"Universal controller backend start issue: {error}");
                    LogDebug($"Universal controller backend start issue: {error}", warning: true);
                }
            }

            if (!started && universalMappingRuntime.Sessions.Count == 0)
            {
                LogDebug("Universal controller runtime started without active mapper sessions; controllers remain unmapped until a universal backend is available.", warning: true);
            }

            stopUniversalMappingThread = false;
            universalMappingThread = new Thread(UniversalMappingLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "Universal Mapper Runtime",
            };
            universalMappingThread.Start();
        }

        private void StartNativeSteamControllerDiscovery()
        {
            nativeSteamControllerDiscoveryThread = new Thread(() =>
            {
                try
                {
                    // This HID scan is only needed for the original 2015 Steam
                    // Controller native adapter. SDL3 owns all modern controllers,
                    // so keep SDL startup and the UI device list off this path.
                    testEnumerator.FindControllers();
                    if (!isRunning)
                    {
                        return;
                    }

                    steamControllerUniversalBackend?.RefreshSources(CreateSteamControllerSources());
                    universalMappingRuntime?.Refresh();
                    LogDebug($"Native HID discovery found {testEnumerator.GetKnownDevices().Count()} known controller(s).");
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Native Steam Controller discovery failed.");
                    LogDebug($"Native Steam Controller discovery failed: {ex.Message}", warning: true);
                }
                finally
                {
                    testEnumerator.ClearRemovedDevicesReferences();
                }
            });
            nativeSteamControllerDiscoveryThread.IsBackground = true;
            nativeSteamControllerDiscoveryThread.Priority = ThreadPriority.BelowNormal;
            nativeSteamControllerDiscoveryThread.Name = "Native Steam Controller HID Discovery";
            nativeSteamControllerDiscoveryThread.Start();
        }

        private static void PruneNonSteamControllerDevMigrations(UniversalProfileStore store)
        {
            if (ApplicationDataPathResolver.DefaultBuildFlavor != ApplicationDataBuildFlavor.Development)
            {
                return;
            }

            foreach (UniversalProfileSummary entry in store.EnumerateProfileSummaries())
            {
                if (!entry.Loaded || string.IsNullOrEmpty(entry.MigrationSourceFamily))
                {
                    continue;
                }

                if (string.Equals(entry.MigrationSourceFamily,
                    InputDeviceType.SteamController.ToString(), StringComparison.Ordinal))
                {
                    continue;
                }

                store.Delete(entry.Path);
            }
        }

        private IEnumerable<ISteamControllerNativeStateSource> CreateSteamControllerSources()
        {
            foreach (InputDeviceBase device in testEnumerator.GetKnownDevices())
            {
                if (device is SteamControllerDevice steamDevice &&
                    device.DeviceType == InputDeviceType.SteamController)
                {
                    yield return new SteamControllerReaderStateSource(steamDevice);
                }
            }
        }

        private void UniversalMappingLoop()
        {
            while (!stopUniversalMappingThread)
            {
                try
                {
                    universalMappingRuntime?.Refresh();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Universal mapping runtime refresh failed.");
                }

                Thread.Sleep(8);
            }
        }

        internal static string DetermineConfiguredOutputHandlerIdentifier(
            ArgumentParser argParser, AppGlobalData appGlobal)
        {
            if (!string.IsNullOrEmpty(argParser?.VirtualkbmHandler))
            {
                switch (argParser.VirtualkbmHandler)
                {
                    case "fakerinput":
                        return FakerInputHandler.IDENTIFIER;
                    case "sendinput":
                    default:
                        return SendInputHandler.IDENTIFIER;
                }
            }

            return appGlobal.fakerInputInstalled
                ? FakerInputHandler.IDENTIFIER
                : SendInputHandler.IDENTIFIER;
        }

        private VirtualKBMBase GetFallbackKBMHandler()
        {
            return new SendInputHandler();
        }

        public void Stop()
        {
            if (!isRunning || changingService) return;

            using WriteLocker locker = new WriteLocker(_hotplugLock);

            changingService = true;
            isRunning = false;

            // Stop physical-mouse capture/forwarding first: it must not
            // outlive or race the virtualEventHandler teardown below.
            physicalMouseService.Stop();

            PreServiceStop?.Invoke(this, EventArgs.Empty);

            if (nativeSteamControllerDiscoveryThread != null &&
                nativeSteamControllerDiscoveryThread.IsAlive &&
                Thread.CurrentThread != nativeSteamControllerDiscoveryThread)
            {
                nativeSteamControllerDiscoveryThread.Join(TimeSpan.FromSeconds(2));
            }

            nativeSteamControllerDiscoveryThread = null;

            stopUniversalMappingThread = true;
            if (universalMappingThread != null &&
                universalMappingThread.IsAlive &&
                Thread.CurrentThread != universalMappingThread)
            {
                universalMappingThread.Join();
            }

            universalMappingThread = null;
            universalMappingRuntime?.Stop();
            universalMappingRuntime?.Dispose();
            universalMappingRuntime = null;

            foreach (Mapper mapper in mapperDict.Values)
            {
                mapper.Stop();
            }

            foreach (DeviceReaderBase reader in deviceReadersMap.Values)
            {
                reader.StopUpdate();
            }

            // USBIP_WIN2 seems to be finicky when it comes to
            // when controllers are removed
            foreach (Mapper mapper in mapperDict.Values)
            {
                mapper.UnplugViiperVirtualControllers();
            }

            hidHideVisibilityManager.ClearSessionOverrides();

            Thread.Sleep(500);

            mapperDict.Clear();
            deviceReadersMap.Clear();
            deviceMapperMap.Clear();
            testEnumerator.StopControllers();
            //enumerator.StopControllers();
            Array.Clear(controllerList, 0, CONTROLLER_LIMIT);

            appGlobal.activeProfiles.Clear();

            //vigemTestClient?.Dispose();
            //vigemTestClient = null;

            mouseOutputDispatcher?.Dispose();
            mouseOutputDispatcher = null;
            mouseOutputRoutingController.DetachRuntime();
            mouseOutputRoutingController.SetServiceRunning(false);

            if (serverHandle != 0)
            {
                LogDebug($"Closing VIIPER connection");
                LibVIIPER.CloseUSBServer(serverHandle);
                serverHandle = 0;
            }

            virtualEventHandler.Sync();
            Thread.Sleep(100);
            try
            {
                virtualEventHandler.Disconnect();
            }
            catch (SEHException)
            {
                // Ignore
            }

            changingService = false;

            ServiceStopped?.Invoke(this, EventArgs.Empty);
        }

        public void PreAppStopDown()
        {
            PreServiceStop = null;
            ServiceStopped = null;
        }

        public void RefreshControllerVisibilityState()
        {
            IEnumerable<UniversalControllerVisibilityTarget> universalTargets =
                universalMappingRuntime?.Sessions
                    .Select(session => UniversalControllerDeviceOptionsStore.CreateVisibilityTarget(
                        session.Controller,
                        session.Mapper.DeviceType))
                    .Where(target => target != null) ??
                Enumerable.Empty<UniversalControllerVisibilityTarget>();

            hidHideVisibilityManager.Reconcile(
                controllerList.Where(device => device != null),
                universalTargets);
        }

        public void ShutDown()
        {
            mouseOutputRoutingController.Dispose();
            physicalMouseService.Dispose();
        }

        public void Hotplug()
        {
            if (isRunning)
            {
                using WriteLocker locker = new WriteLocker(_hotplugLock);
                testEnumerator.FindControllers();
                steamControllerUniversalBackend?.RefreshSources(CreateSteamControllerSources());
                LogDebug($"Native HID discovery found {testEnumerator.GetKnownDevices().Count()} known controller(s) after hotplug.");
                universalMappingRuntime?.Refresh();
            }
        }

        public void LogDebug(string message, bool warning = false)
        {
            DebugEventArgs args = new DebugEventArgs(message, warning);
            Debug?.Invoke(this, args);
        }
    }
}
