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
        private readonly IUniversalLastProfileStore lastProfileStore;

        public UniversalProfileStoreSelector(
            UniversalProfileStore store,
            Guid? preferredProfileId = null,
            IUniversalLastProfileStore lastProfileStore = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.preferredProfileId = preferredProfileId;
            this.lastProfileStore = lastProfileStore;
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

            // What this controller was mapping with when it was last seen beats
            // the alphabetical fallback below. Without it every launch reset the
            // controller to whichever profile sorts first, discarding the user's
            // choice from the previous session.
            Guid? lastProfileId = lastProfileStore?.GetLastProfileId(controller);
            if (lastProfileId.HasValue)
            {
                UniversalProfileSummary last = profiles.FirstOrDefault(item =>
                    item.Loaded && item.ProfileId == lastProfileId.Value);
                UniversalProfile loadedLast = last != null ? LoadOrNull(last) : null;
                if (loadedLast != null) return loadedLast;

                // A deleted or unreadable profile is not an error worth blocking
                // the controller over, but it is worth saying why the profile
                // the user expected did not come back.
                logger.Info($"Last universal profile {lastProfileId.Value:D} is no longer available; falling back to the first readable profile.");
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
        private readonly IUniversalLastProfileStore lastProfileStore;
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

        // Serialises whole reconcile passes. Reconciliation is not driven by
        // the mapping loop alone: a backend raising ControllersChanged runs one
        // on whichever thread noticed the change, which for a hotplug is the
        // backend event dispatcher. Two passes running at once corrupted the
        // unresolved-controller dictionary below and could open two sessions
        // for one controller.
        private readonly object reconcileLock = new object();

        // Controllers whose profile could not be selected or compiled, keyed to
        // the time the next attempt is allowed. Only ever touched inside
        // ReconcileSessions, under reconcileLock.
        private readonly Dictionary<Guid, DateTimeOffset> unresolvedControllers =
            new Dictionary<Guid, DateTimeOffset>();
        private static readonly TimeSpan UnresolvedRetryInterval = TimeSpan.FromSeconds(5);

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
            IEnumerable<LegacyProfileMigrationSource> startupMigrationSources = null,
            IUniversalLastProfileStore lastProfileStore = null)
        {
            this.controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));
            this.profileSelector = profileSelector ?? throw new ArgumentNullException(nameof(profileSelector));
            this.lastProfileStore = lastProfileStore;
            this.outputHandler = outputHandler;
            this.outputMapping = outputMapping;
            this.mouseOutputDispatcher = mouseOutputDispatcher;
            this.viiperServerHandle = viiperServerHandle;
            this.profileMigrator = profileMigrator;
            this.startupMigrationSources = startupMigrationSources ?? Array.Empty<LegacyProfileMigrationSource>();
            this.controllerManager.ControllersChanged += ControllerManager_ControllersChanged;
        }

        // Rebuilt only when the session set actually changes. Refresh reads
        // this on every tick of the mapping loop, so returning a fresh array
        // and wrapper each time meant two allocations 125 times a second in
        // steady state.
        private IReadOnlyList<UniversalMapperSession> sessionsSnapshot =
            Array.Empty<UniversalMapperSession>();

        public IReadOnlyList<UniversalMapperSession> Sessions
        {
            get
            {
                lock (sessionsLock)
                {
                    return sessionsSnapshot;
                }
            }
        }

        // Always called with sessionsLock held.
        private void RebuildSessionsSnapshot()
        {
            sessionsSnapshot = new ReadOnlyCollection<UniversalMapperSession>(
                sessions.Values.ToArray());
        }

        public UniversalControllerManager ControllerManager => controllerManager;

        // Never below the historical fixed rate, so a device that cannot be
        // measured, or a slow one, polls exactly as it always did.
        public const double MinimumPollRateHz = 125.0;

        // A sanity bound rather than a judgement about hardware. A misread
        // rate must not be able to spin the mapping loop.
        public const double MaximumPollRateHz = 1000.0;

        // Polling at exactly the rate a controller reports sounds right and is
        // not: the two clocks drift against each other, so some passes see two
        // reports and some see none. Checking about twice as often removes the
        // beat without pretending to extract data that is not there.
        public const double PollRateOversampleFactor = 2.0;

        /// <summary>
        /// Ceiling applied to the measured rate, from the user's advanced
        /// setting. Defaults to the absolute maximum, meaning no extra limit.
        /// </summary>
        public double PollRateCapHz { get; set; } = MaximumPollRateHz;

        /// <summary>
        /// The rate the mapping loop should run at to keep up with the fastest
        /// connected controller, rather than assuming every device is 125 Hz.
        /// </summary>
        public double RecommendedPollRateHz => ResolvePollRateHz(out _);

        /// <summary>
        /// Same as <see cref="RecommendedPollRateHz"/>, additionally reporting
        /// whether the user's cap is what decided the answer, so the UI can say
        /// so rather than leaving the user to work it out from two numbers.
        /// </summary>
        public double ResolvePollRateHz(out bool limitedByCap)
        {
            double fastestDeviceHz = 0.0;
            foreach (UniversalMapperSession session in Sessions)
            {
                // The measured report rate is what the device actually does.
                // The declared motion rate is a fallback for a backend that
                // cannot count reports.
                double? rate = session.Controller.ReportRateHz ??
                    session.Controller.Capabilities?.MotionSampleRateHz;
                if (rate.HasValue && rate.Value > fastestDeviceHz)
                {
                    fastestDeviceHz = rate.Value;
                }
            }

            double desired = fastestDeviceHz * PollRateOversampleFactor;
            if (desired < MinimumPollRateHz) desired = MinimumPollRateHz;
            if (desired > MaximumPollRateHz) desired = MaximumPollRateHz;

            double cap = PollRateCapHz;
            if (cap < MinimumPollRateHz) cap = MinimumPollRateHz;
            if (cap > MaximumPollRateHz) cap = MaximumPollRateHz;

            limitedByCap = desired > cap;
            return limitedByCap ? cap : desired;
        }

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
                return;
            }

            RecordLastProfile(session.Controller, profile);
        }

        private void RecordLastProfile(IUniversalController controller, UniversalProfile profile)
        {
            if (lastProfileStore == null || profile == null || profile.ProfileId == Guid.Empty) return;

            try
            {
                lastProfileStore.SetLastProfileId(controller, profile.ProfileId);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Losing the record only costs the user the restore on the next
                // launch; it must never take the running mapper down with it.
                logger.Warn(ex, "Failed to record the last universal profile for a controller.");
            }
        }

        public void Stop()
        {
            UniversalMapperSession[] stopping;
            lock (sessionsLock)
            {
                stopping = sessions.Values.ToArray();
                sessions.Clear();
                RebuildSessionsSnapshot();
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
                        // SourceIdentity already starts with the family name.
                        logger.Warn($"Universal profile migration {report.Status}: {report.SourceIdentity}");
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
            bool changed;
            lock (reconcileLock)
            {
                changed = ReconcileSessionsLocked();
            }

            // Raised outside the lock. Listeners rebuild the controller list on
            // the UI thread, and holding a reconcile pass open while that
            // happens would let a UI callback block the mapping loop.
            if (changed)
            {
                SessionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool ReconcileSessionsLocked()
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
            HashSet<Guid> missing = new HashSet<Guid>();
            lock (sessionsLock)
            {
                foreach (Guid staleId in sessions.Keys.Where(item => !connectedIds.Contains(item)).ToArray())
                {
                    removed.Add(sessions[staleId]);
                    sessions.Remove(staleId);
                    changed = true;
                    logger.Info($"Disposed universal mapper session {staleId} after controller removal.");
                }

                foreach (Guid connectedId in connectedIds)
                {
                    if (!sessions.ContainsKey(connectedId)) missing.Add(connectedId);
                }

                if (changed) RebuildSessionsSnapshot();
            }

            // A controller that has gone away gets a fresh attempt if it comes
            // back, so its earlier failure must not be remembered.
            foreach (Guid staleId in unresolvedControllers.Keys
                .Where(item => !connectedIds.Contains(item)).ToArray())
            {
                unresolvedControllers.Remove(staleId);
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

                // Reconcile runs on every pass of the mapping loop, so a
                // controller that cannot be given a profile would otherwise be
                // retried 125 times a second. Each attempt walks the profile
                // directory and writes a log line, which turned an empty or
                // unreadable profile store into a permanent disk and log
                // hammer. Back off instead, slowly enough to cost nothing and
                // often enough that creating a profile still takes effect
                // without replugging the controller.
                if (IsBackingOff(logicalId)) continue;

                UniversalProfile profile = profileSelector.SelectProfile(controller);
                if (profile == null)
                {
                    BackOff(logicalId, "no universal profile available");
                    continue;
                }

                unresolvedControllers.Remove(logicalId);

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
                        if (added) RebuildSessionsSnapshot();
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
                    // A profile that will not compile now will not compile on
                    // the next pass either, so back off on the same terms as a
                    // missing one.
                    BackOff(logicalId, $"profile activation failed: {ex.Message}");
                    logger.Error(ex, $"Failed to activate universal profile for controller {logicalId} from {controller.Identity.BackendName}.");
                }
            }

            return changed;
        }

        private bool IsBackingOff(Guid logicalControllerId)
        {
            return unresolvedControllers.TryGetValue(logicalControllerId,
                    out DateTimeOffset retryAfter) &&
                UniversalMonotonicClock.UtcNow < retryAfter;
        }

        private void BackOff(Guid logicalControllerId, string reason)
        {
            bool firstFailure = !unresolvedControllers.ContainsKey(logicalControllerId);
            unresolvedControllers[logicalControllerId] =
                UniversalMonotonicClock.UtcNow + UnresolvedRetryInterval;

            // Only the first failure of a run is worth a line. Repeats are the
            // same message about the same controller and would fill the log.
            if (firstFailure)
            {
                logger.Warn($"Controller {logicalControllerId} left unmapped ({reason}). " +
                    $"Retrying every {UnresolvedRetryInterval.TotalSeconds:0} seconds.");
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
