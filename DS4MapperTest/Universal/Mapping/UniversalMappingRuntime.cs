using DS4MapperTest.Universal.Profiles;
using NLog;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DS4MapperTest.Universal.Mapping
{
    public interface IUniversalProfileSelector
    {
        UniversalProfile SelectProfile(IUniversalController controller);
    }

    public sealed class UniversalProfileStoreSelector : IUniversalProfileSelector
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly UniversalProfileStore store;
        private readonly Guid? preferredProfileId;

        public UniversalProfileStoreSelector(UniversalProfileStore store, Guid? preferredProfileId = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.preferredProfileId = preferredProfileId;
        }

        public UniversalProfile SelectProfile(IUniversalController controller)
        {
            // Choosing from summaries keeps a controller connecting from
            // parsing every profile in the store; only the winner is loaded.
            IReadOnlyList<UniversalProfileSummary> profiles = store.EnumerateProfileSummaries();
            if (preferredProfileId.HasValue)
            {
                UniversalProfileSummary preferred = profiles.FirstOrDefault(item =>
                    item.Loaded && item.ProfileId == preferredProfileId.Value);
                if (preferred != null) return LoadOrNull(preferred);
            }

            foreach (UniversalProfileSummary candidate in profiles
                .Where(item => item.Loaded)
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ProfileId))
            {
                UniversalProfile loaded = LoadOrNull(candidate);
                if (loaded != null) return loaded;
            }

            return null;
        }

        private UniversalProfile LoadOrNull(UniversalProfileSummary summary)
        {
            try
            {
                return store.LoadFromPath(summary.Path);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Skipping unreadable universal profile {summary.Path}.");
                return null;
            }
        }
    }

    public sealed class UniversalMappingRuntime : IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly UniversalControllerManager controllerManager;
        private readonly IUniversalProfileSelector profileSelector;
        private readonly LegacyProfileMigrator profileMigrator;
        private readonly IEnumerable<LegacyProfileMigrationSource> startupMigrationSources;
        private readonly VirtualKBMBase outputHandler;
        private readonly VirtualKBMMapping outputMapping;
        private readonly MouseOutputDispatcher mouseOutputDispatcher;
        private readonly nuint viiperServerHandle;
        // Reconciliation runs on the mapping thread while the editor switches
        // profiles and reads the session list from the UI thread, so every
        // touch of this dictionary is serialised. Session work itself happens
        // outside this lock: a session takes its own lock, and taking them in
        // the other order would be a deadlock waiting to happen.
        private readonly object sessionsLock = new object();
        private readonly Dictionary<Guid, UniversalMapperSession> sessions =
            new Dictionary<Guid, UniversalMapperSession>();
        private bool started;
        private bool disposed;

        public UniversalMappingRuntime(
            UniversalControllerManager controllerManager,
            IUniversalProfileSelector profileSelector,
            VirtualKBMBase outputHandler,
            VirtualKBMMapping outputMapping,
            MouseOutputDispatcher mouseOutputDispatcher = null,
            nuint viiperServerHandle = 0,
            LegacyProfileMigrator profileMigrator = null,
            IEnumerable<LegacyProfileMigrationSource> startupMigrationSources = null)
        {
            this.controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));
            this.profileSelector = profileSelector ?? throw new ArgumentNullException(nameof(profileSelector));
            this.outputHandler = outputHandler;
            this.outputMapping = outputMapping;
            this.mouseOutputDispatcher = mouseOutputDispatcher;
            this.viiperServerHandle = viiperServerHandle;
            this.profileMigrator = profileMigrator;
            this.startupMigrationSources = startupMigrationSources ?? Array.Empty<LegacyProfileMigrationSource>();
            this.controllerManager.ControllersChanged += ControllerManager_ControllersChanged;
        }

        public IReadOnlyList<UniversalMapperSession> Sessions
        {
            get
            {
                lock (sessionsLock)
                {
                    return new ReadOnlyCollection<UniversalMapperSession>(sessions.Values.ToArray());
                }
            }
        }

        public UniversalControllerManager ControllerManager => controllerManager;

        public event EventHandler SessionsChanged;

        public IReadOnlyList<string> StartupErrors { get; private set; } =
            Array.Empty<string>();
        public IReadOnlyList<ProfileMigrationReport> StartupMigrationReports { get; private set; } =
            Array.Empty<ProfileMigrationReport>();

        public bool Start()
        {
            ThrowIfDisposed();
            if (started) return true;

            RunStartupMigration();
            bool success = controllerManager.Start(out IReadOnlyList<string> errors);
            StartupErrors = errors;
            started = true;
            ReconcileSessions();
            return success;
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            if (!started) return;

            controllerManager.Refresh();
            ReconcileSessions();
            foreach (UniversalMapperSession session in Sessions)
            {
                try
                {
                    session.ProcessCurrentState();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Universal mapper session failed. LogicalController={session.LogicalControllerId} Backend={session.BackendName}");
                }
            }
        }

        public void SwitchProfile(Guid logicalControllerId, UniversalProfile profile)
        {
            ThrowIfDisposed();
            UniversalMapperSession session;
            lock (sessionsLock)
            {
                if (!sessions.TryGetValue(logicalControllerId, out session)) return;
            }

            try
            {
                session.SwitchProfile(profile);
            }
            catch (ObjectDisposedException)
            {
                // The controller disconnected between the lookup and the
                // switch. There is nothing left to switch a profile on.
            }
        }

        public void Stop()
        {
            UniversalMapperSession[] stopping;
            lock (sessionsLock)
            {
                stopping = sessions.Values.ToArray();
                sessions.Clear();
            }

            foreach (UniversalMapperSession session in stopping)
            {
                session.Dispose();
            }

            controllerManager.Stop();
            started = false;
        }

        public static IReadOnlyList<LegacyProfileMigrationSource> DiscoverLegacyProfileSources(
            IDictionary<InputDeviceType, ProfileList> profileLists)
        {
            if (profileLists == null) return Array.Empty<LegacyProfileMigrationSource>();

            List<LegacyProfileMigrationSource> result = new List<LegacyProfileMigrationSource>();
            foreach (KeyValuePair<InputDeviceType, ProfileList> item in profileLists.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            {
                string root = item.Value.GetDeviceProfileRoot();
                foreach (ProfileEntity profile in item.Value.ProfileListCol.OrderBy(profile => profile.ProfilePath, StringComparer.OrdinalIgnoreCase))
                {
                    if (!File.Exists(profile.ProfilePath)) continue;

                    string relativeIdentity = BuildRelativeSourceIdentity(item.Key, root, profile.ProfilePath);
                    result.Add(new LegacyProfileMigrationSource(
                        item.Key,
                        relativeIdentity,
                        File.ReadAllText(profile.ProfilePath)));
                }
            }

            return result;
        }

        private void RunStartupMigration()
        {
            if (profileMigrator == null)
            {
                StartupMigrationReports = Array.Empty<ProfileMigrationReport>();
                return;
            }

            try
            {
                StartupMigrationReports = profileMigrator.MigrateBatch(startupMigrationSources, preview: false);
                foreach (ProfileMigrationReport report in StartupMigrationReports)
                {
                    if (report.Status == ProfileMigrationStatus.Failed ||
                        report.Status == ProfileMigrationStatus.Conflict)
                    {
                        logger.Warn($"Universal profile migration {report.Status}: {report.SourceFamily}/{report.SourceIdentity}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Universal profile startup migration failed before controller activation.");
                StartupMigrationReports = Array.Empty<ProfileMigrationReport>();
            }
        }

        private void ControllerManager_ControllersChanged(object sender, EventArgs e)
        {
            if (started) ReconcileSessions();
        }

        private void ReconcileSessions()
        {
            bool changed = false;
            IReadOnlyList<IUniversalController> authoritative = controllerManager.Controllers
                .Where(item => item.ConnectionState == UniversalControllerConnectionState.Connected)
                .Where(item => item.Identity.BackendName != UniversalControllerBackendIds.DiagnosticObserver)
                .ToArray();

            HashSet<Guid> connectedIds = authoritative
                .Select(item => item.Identity.LogicalControllerId)
                .ToHashSet();

            List<UniversalMapperSession> removed = new List<UniversalMapperSession>();
            List<Guid> missing = new List<Guid>();
            lock (sessionsLock)
            {
                foreach (Guid staleId in sessions.Keys.Where(item => !connectedIds.Contains(item)).ToArray())
                {
                    removed.Add(sessions[staleId]);
                    sessions.Remove(staleId);
                    changed = true;
                    logger.Info($"Disposed universal mapper session {staleId} after controller removal.");
                }

                missing.AddRange(connectedIds.Where(item => !sessions.ContainsKey(item)));
            }

            // Disposing takes a session's own lock, so do it outside this
            // runtime's lock to keep the two always acquired in one order.
            foreach (UniversalMapperSession session in removed)
            {
                session.Dispose();
            }

            foreach (IUniversalController controller in authoritative)
            {
                Guid logicalId = controller.Identity.LogicalControllerId;
                if (!missing.Contains(logicalId)) continue;

                UniversalProfile profile = profileSelector.SelectProfile(controller);
                if (profile == null)
                {
                    logger.Warn($"No universal profile available for controller {logicalId} from {controller.Identity.BackendName}.");
                    continue;
                }

                try
                {
                    UniversalMapperSession session = new UniversalMapperSession(
                        controller,
                        profile,
                        outputHandler,
                        outputMapping,
                        mouseOutputDispatcher,
                        viiperServerHandle);

                    bool added;
                    lock (sessionsLock)
                    {
                        added = sessions.TryAdd(logicalId, session);
                    }

                    if (!added)
                    {
                        // Another reconcile beat this one to the controller.
                        session.Dispose();
                        continue;
                    }

                    changed = true;
                    logger.Info($"Created universal mapper session {logicalId} from backend {controller.Identity.BackendName}.");
                }
                catch (Exception ex) when (ex is JsonException || ex is UniversalProfileCompilationException || ex is UniversalProfileValidationException)
                {
                    logger.Error(ex, $"Failed to activate universal profile for controller {logicalId} from {controller.Identity.BackendName}.");
                }
            }

            if (changed)
            {
                SessionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UniversalMappingRuntime));
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            controllerManager.ControllersChanged -= ControllerManager_ControllersChanged;
            controllerManager.Dispose();
            disposed = true;
        }

        private static string BuildRelativeSourceIdentity(
            InputDeviceType family,
            string root,
            string profilePath)
        {
            string relative = Path.GetRelativePath(root, profilePath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return $"{family}/{relative}";
        }
    }
}
