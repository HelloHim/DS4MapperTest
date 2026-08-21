using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DS4MapperTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private AppGlobalData appGlobal;
        private BackendManager manager;
        public BackendManager Manager { get => manager; }

        private Thread testThread;
        private Timer collectTimer;
        private ArgumentParser _parser;
        private LoggerHolder logHolder;

        // Global rather than session scoped so a second copy is caught even
        // when it is launched from another session, such as a scheduled task
        // or a fast user switch.
        private const string SINGLE_INSTANCE_MUTEX_NAME =
            @"Global\DS4MapperTest_SingleInstance";
        private Mutex singleInstanceMutex;
        private bool ownsSingleInstanceMutex;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Wired up before anything that can fail. These used to be attached
            // only after the config was loaded, so any startup fault landed in
            // the default WPF crash dialog with nothing written to the log.
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (!ClaimSingleInstance())
            {
                MessageBox.Show(
                    "DS4MapperTest is already running.\n\n" +
                    "Only one copy can map controllers at a time. Use the copy " +
                    "that is already open.",
                    "DS4MapperTest", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            _parser = new ArgumentParser();
            _parser.Parse(e.Args);

            try
            {
                RunStartup();
            }
            catch (Exception ex)
            {
                ReportFatalStartupFailure(ex);
            }
        }

        // Two copies of a remapper means every button press is emitted twice,
        // two USB/IP servers fight over the same port, two writers race on the
        // same profile files and two HidHide sessions contend for the cloak
        // list. None of that is recoverable once it has started, so the second
        // copy has to be stopped before it does any work at all.
        private bool ClaimSingleInstance()
        {
            try
            {
                singleInstanceMutex = new Mutex(true, SINGLE_INSTANCE_MUTEX_NAME,
                    out ownsSingleInstanceMutex);
            }
            catch (UnauthorizedAccessException)
            {
                // The mutex exists but this session cannot open it, which still
                // means another copy created it.
                return false;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }

            if (!ownsSingleInstanceMutex)
            {
                singleInstanceMutex.Dispose();
                singleInstanceMutex = null;
            }

            return ownsSingleInstanceMutex;
        }

        private void ReleaseSingleInstance()
        {
            if (singleInstanceMutex == null) return;

            try
            {
                if (ownsSingleInstanceMutex)
                {
                    singleInstanceMutex.ReleaseMutex();
                    ownsSingleInstanceMutex = false;
                }
            }
            catch (ApplicationException)
            {
                // Released from a thread that does not hold it, or already
                // abandoned by a crash. Disposing below is all that is left.
            }

            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
        }

        private void ReportFatalStartupFailure(Exception ex)
        {
            Trace.WriteLine($"Startup failed: {ex}");
            try
            {
                logHolder?.Logger?.Error(ex, "Startup failed");
            }
            catch
            {
                // Logging is itself part of startup and may not exist yet.
            }

            MessageBox.Show(
                $"DS4MapperTest could not start.\n\n{ex.Message}\n\n" +
                $"Configuration folder:\n{appGlobal?.appdatapath}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown(1);
        }

        private void RunStartup()
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass =
                    ProcessPriorityClass.High;
            }
            catch { } // Ignore problems raising the priority.

            // Force Normal IO Priority
            IntPtr ioPrio = new IntPtr(2);
            Util.NtSetInformationProcess(Process.GetCurrentProcess().Handle,
                Util.PROCESS_INFORMATION_CLASS.ProcessIoPriority, ref ioPrio, 4);

            // Force Normal Page Priority
            IntPtr pagePrio = new IntPtr(5);
            Util.NtSetInformationProcess(Process.GetCurrentProcess().Handle,
                Util.PROCESS_INFORMATION_CLASS.ProcessPagePriority, ref pagePrio, 4);

            // Allow sleep time durations less than 16 ms
            Util.timeBeginPeriod(1);

            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            appGlobal = AppGlobalDataSingleton.Instance;
            appGlobal.FindConfigLocation();
            bool createdSkel = false;
            if (!appGlobal.appSettingsDirFound)
            {
                createdSkel = appGlobal.CreateBaseConfigSkeleton();
                if (!createdSkel)
                {
                    MessageBox.Show($"Cannot create config folder structure in {appGlobal.appdatapath}. Exiting",
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown(1);
                    return;
                }

                // Only copy template profiles if app settings directory
                // was just created
                appGlobal.CheckAndCopyExampleProfiles();
            }
            else
            {
                // Create dirs for new devices if necessary
                appGlobal.CreateDeviceProfilesSkeleton();
                appGlobal.CheckAndCopyExampleProfiles();
            }

            appGlobal.RefreshBaseDriverInfo();
            appGlobal.StartupLoadAppSettings();
            if (!File.Exists(appGlobal.ControllerConfigsPath))
            {
                appGlobal.CreateControllerDeviceSettingsFile();
            }

            ThemeService.Initialize(appGlobal);

            // Use all display space
            appGlobal.PrepareAbsMonitorBounds(string.Empty);

            Exception managerFailure = null;
            testThread = new Thread(() =>
            {
                try
                {
                    manager = new BackendManager(_parser, appGlobal);
                }
                catch (Exception ex)
                {
                    // Nothing handles an exception thrown on a bare thread, so
                    // carry it back to the caller instead of letting it end the
                    // process with no window and no message.
                    managerFailure = ex;
                }
                //manager.RequestOSD += Manager_RequestOSD;
                //manager.Start();
                //mapper = new Mapper();
                //mapper.Start();
            });

            testThread.IsBackground = true;
            testThread.Start();
            testThread.Join();

            if (managerFailure != null)
            {
                throw new InvalidOperationException(
                    $"The controller backend could not be created. {managerFailure.Message}",
                    managerFailure);
            }

            logHolder = new LoggerHolder(manager, appGlobal);
            Logger logger = logHolder.Logger;
            logger.Info($"DS4MapperTest v. {AppGlobalData.exeversion}");
            logger.Info($"OS Version: {Environment.OSVersion}");
            logger.Info($"OS Product Name: {Util.GetOSProductName()}");
            logger.Info($"OS Release ID: {Util.GetOSReleaseId()}");

            if (!string.IsNullOrEmpty(appGlobal.QuarantinedSettingsPath))
            {
                logger.Warn($"Unreadable settings file moved to {appGlobal.QuarantinedSettingsPath}");
            }

            MainWindow window = new MainWindow();
            window.PostInit(appGlobal);
            window.Show();

            if (!string.IsNullOrEmpty(appGlobal.QuarantinedSettingsPath))
            {
                MessageBox.Show(
                    "The application settings file could not be read, so app " +
                    "settings have been reset to their defaults.\n\n" +
                    $"The previous file was kept at:\n{appGlobal.QuarantinedSettingsPath}\n\n" +
                    "Your profiles were not affected.",
                    "Settings Reset", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            window.StartCheckProcess();

            collectTimer = new Timer(GarbageTask, null, 30000, 30000);
        }

        private void GarbageTask(object state)
        {
            GC.Collect(0, GCCollectionMode.Forced, false);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger logger = logHolder?.Logger;
            if (logger != null)
            {
                logger.Error($"Thread Crashed with message {e.Exception.Message}");
                logger.Error(e.Exception.ToString());
            }
            else
            {
                Trace.WriteLine($"Unhandled dispatcher exception: {e.Exception}");
            }

            // Log and keep the app running. The app should only close when the
            // user closes it manually, not because a UI-thread exception occurred
            // (e.g. while handling a controller disconnect).
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exp = e.ExceptionObject as Exception;
            bool canAccessMain = Current.Dispatcher.CheckAccess();
            //Trace.WriteLine($"CRASHED {help}");
            Logger logger = logHolder?.Logger;
            if (e.IsTerminating)
            {
                if (logger != null && exp != null)
                {
                    logger.Error($"Thread Crashed with message {exp.Message}");
                    logger.Error(exp.ToString());
                }
                else
                {
                    Trace.WriteLine($"Unhandled domain exception: {exp}");
                }

                if (canAccessMain)
                {
                    CleanShutDown();
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        CleanShutDown();
                    });
                }
            }
        }
        
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            CleanShutDown();
        }

        private void CleanShutDown()
        {
            BackendManager currentManager = manager;
            if (currentManager != null)
            {
                currentManager.LogDebug($"Stopping manager");

                Task tempTask = Task.Run(() =>
                {
                    currentManager.PreAppStopDown();
                    currentManager.Stop();
                });
                tempTask.Wait();

                currentManager.ShutDown();

                currentManager.LogDebug($"Manager stopped");
            }

            currentManager?.LogDebug($"Stopping program");

            LogManager.Flush();
            LogManager.Shutdown();

            //osdTestWindow.Close();
            //osdTestWindow = null;

            // Reset timer
            Util.timeEndPeriod(1);

            ReleaseSingleInstance();
        }
    }
}
