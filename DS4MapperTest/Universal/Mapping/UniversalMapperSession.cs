using DS4MapperTest.Universal.Profiles;
using System;

namespace DS4MapperTest.Universal.Mapping
{
    public sealed class UniversalMapperSession : IDisposable
    {
        private readonly object syncRoot = new object();
        private bool disposed;

        public UniversalMapperSession(
            IUniversalController controller,
            UniversalProfile profile,
            VirtualKBMBase outputHandler,
            VirtualKBMMapping outputMapping,
            MouseOutputDispatcher mouseOutputDispatcher = null,
            nuint viiperServerHandle = 0)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            Mapper = new UniversalMapper(controller, profile ?? throw new ArgumentNullException(nameof(profile)));
            Mapper.PassMouseOutputDispatcher(mouseOutputDispatcher);
            Mapper.PassVIIPERConnection(viiperServerHandle);
            Mapper.Start(outputHandler, outputMapping);
        }

        public IUniversalController Controller { get; }
        public UniversalMapper Mapper { get; }
        public Guid LogicalControllerId => Controller.Identity.LogicalControllerId;
        public string BackendName => Controller.Identity.BackendName;
        public string BackendSessionId => Controller.Identity.BackendSessionId;
        public bool IsDisposed => disposed;

        // The profile actually compiled and running for this controller right
        // now, as selected by IUniversalProfileSelector at connect/switch time.
        public UniversalProfile ActiveProfile => Mapper.CompiledProfile?.SourceProfile;

        public void ProcessCurrentState()
        {
            lock (syncRoot)
            {
                if (disposed) return;
                if (Controller.ConnectionState != UniversalControllerConnectionState.Connected)
                {
                    Mapper.Stop(true);
                    return;
                }

                Mapper.ProcessSnapshot(Controller.State);
            }
        }

        public void SwitchProfile(UniversalProfile profile)
        {
            lock (syncRoot)
            {
                if (disposed) throw new ObjectDisposedException(nameof(UniversalMapperSession));
                Mapper.ActivateProfile(profile);
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                Mapper.Stop(true);
            }
        }
    }
}
