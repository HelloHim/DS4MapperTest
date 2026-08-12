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
        private readonly UniversalProfileStore store;
        private readonly Guid? preferredProfileId;

        public UniversalProfileStoreSelector(UniversalProfileStore store, Guid? preferredProfileId = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.preferredProfileId = preferredProfileId;
        }

        public UniversalProfile SelectProfile(IUniversalController controller)
        {
            IReadOnlyList<UniversalProfileStoreEntry> profiles = store.EnumerateProfiles();
            if (preferredProfileId.HasValue)
            {
                UniversalProfileStoreEntry preferred = profiles.FirstOrDefault(item =>
                    item.Loaded && item.Profile.ProfileId == preferredProfileId.Value);
                if (preferred != null) return preferred.Profile.Clone();
            }

            return profiles
                .Where(item => item.Loaded)
                .Select(item => item.Profile)
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ProfileId)
                .FirstOrDefault()
                ?.Clone();
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

        public IReadOnlyList<UniversalMapperSession> Sessions =>
            new ReadOnlyCollection<UniversalMapperSession>(sessions.Values.ToArray());

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
            foreach (UniversalMapperSession session in sessions.Values.ToArray())
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
            if (sessions.TryGetValue(logicalControllerId, out UniversalMapperSession session))
            {
                session.SwitchProfile(profile);
            }
        }

        public void Stop()
        {
            foreach (UniversalMapperSession session in sessions.Values.ToArray())
            {
                session.Dispose();
            }

            sessions.Clear();
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

            foreach (Guid staleId in sessions.Keys.Where(item => !connectedIds.Contains(item)).ToArray())
            {
                sessions[staleId].Dispose();
                sessions.Remove(staleId);
                changed = true;
                logger.Info($"Disposed universal mapper session {staleId} after controller removal.");
            }

            foreach (IUniversalController controller in authoritative)
            {
                Guid logicalId = controller.Identity.LogicalControllerId;
                if (sessions.ContainsKey(logicalId)) continue;

                UniversalProfile profile = profileSelector.SelectProfile(controller);
                if (profile == null)
                {
                    logger.Warn($"No universal profile available for controller {logicalId} from {controller.Identity.BackendName}.");
                    continue;
                }

                try
                {
                    sessions.Add(logicalId, new UniversalMapperSession(
                        controller,
                        profile,
                        outputHandler,
                        outputMapping,
                        mouseOutputDispatcher,
                        viiperServerHandle));
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
