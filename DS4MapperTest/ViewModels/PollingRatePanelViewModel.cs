using DS4MapperTest.Universal.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace DS4MapperTest.ViewModels
{
    public sealed class PollRateCapOptionViewModel
    {
        public PollRateCapOptionViewModel(int rateHz)
        {
            RateHz = rateHz;
        }

        public int RateHz { get; }

        // Both numbers matter to different people: the rate is what a polling
        // rate checker shows, the period is what JoyShockMapper and similar
        // tools ask for.
        public string DisplayName => $"{RateHz} Hz ({1000.0 / RateHz:0.#} ms)";
    }

    public sealed class ConnectedControllerRateViewModel
    {
        public ConnectedControllerRateViewModel(string displayName, double? reportRateHz)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Controller" : displayName;
            ReportRateHz = reportRateHz;
        }

        public string DisplayName { get; }
        public double? ReportRateHz { get; }

        public string ReportRateText => ReportRateHz.HasValue
            ? $"sends {ReportRateHz.Value:0} Hz"
            : "rate not measured yet";
    }

    /// <summary>
    /// Backs the Controller Polling panel: a live read-out of what each
    /// connected controller actually sends, and the advanced ceiling.
    /// </summary>
    public sealed class PollingRatePanelViewModel : INotifyPropertyChanged
    {
        private readonly BackendManager backendManager;
        private readonly AppGlobalData appGlobal;
        private bool suppressPersist;

        public PollingRatePanelViewModel(BackendManager backendManager,
            AppGlobalData appGlobal)
        {
            this.backendManager = backendManager;
            this.appGlobal = appGlobal;

            CapOptions = new ReadOnlyCollection<PollRateCapOptionViewModel>(
                new[] { 1000, 500, 333, 250, 125 }
                    .Select(rate => new PollRateCapOptionViewModel(rate)).ToArray());

            Refresh();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IReadOnlyList<PollRateCapOptionViewModel> CapOptions { get; }

        public ObservableCollection<ConnectedControllerRateViewModel> Controllers { get; } =
            new ObservableCollection<ConnectedControllerRateViewModel>();

        public bool OverrideEnabled
        {
            get => appGlobal.appSettings?.PollRateOverrideEnabled ?? false;
            set
            {
                if (appGlobal.appSettings == null ||
                    appGlobal.appSettings.PollRateOverrideEnabled == value)
                {
                    return;
                }

                appGlobal.appSettings.PollRateOverrideEnabled = value;
                PersistAndApply();
                RaiseChanged(nameof(OverrideEnabled));
                RaiseChanged(nameof(SelectedCapOption));
            }
        }

        public PollRateCapOptionViewModel SelectedCapOption
        {
            get
            {
                int capHz = appGlobal.appSettings?.PollRateCapHz ??
                    AppSettingsStore.DEFAULT_POLL_RATE_CAP_HZ;
                return CapOptions.FirstOrDefault(option => option.RateHz == capHz) ??
                    CapOptions[0];
            }
            set
            {
                if (value == null || appGlobal.appSettings == null ||
                    appGlobal.appSettings.PollRateCapHz == value.RateHz)
                {
                    return;
                }

                appGlobal.appSettings.PollRateCapHz = value.RateHz;
                PersistAndApply();
                RaiseChanged(nameof(SelectedCapOption));
            }
        }

        public string CurrentRateText
        {
            get
            {
                UniversalMappingRuntime runtime = backendManager?.UniversalMappingRuntime;
                if (runtime == null)
                {
                    return "Start the mapping service to see the polling rate.";
                }

                double rateHz = runtime.ResolvePollRateHz(out bool limitedByCap);
                string suffix = limitedByCap
                    ? " - held at your configured maximum"
                    : string.Empty;
                return $"Polling at {rateHz:0} Hz ({1000.0 / rateHz:0.#} ms){suffix}";
            }
        }

        public string ExplanationText =>
            "The polling rate follows whatever the connected controllers actually " +
            "send, measured live, and runs at twice that so no report is missed. " +
            "The maximum below only puts a ceiling on it.";

        /// <summary>
        /// Re-reads the live figures. Called while the panel is open.
        /// </summary>
        public void Refresh()
        {
            Controllers.Clear();
            UniversalMappingRuntime runtime = backendManager?.UniversalMappingRuntime;
            if (runtime != null)
            {
                foreach (UniversalMapperSession session in runtime.Sessions)
                {
                    Controllers.Add(new ConnectedControllerRateViewModel(
                        session.Controller.DisplayInfo?.DisplayName,
                        session.Controller.ReportRateHz));
                }
            }

            RaiseChanged(nameof(CurrentRateText));
            RaiseChanged(nameof(HasControllers));
        }

        public bool HasControllers => Controllers.Count > 0;

        private void PersistAndApply()
        {
            if (suppressPersist) return;

            appGlobal.SaveAppSettings();
            backendManager?.ApplyPollRateSettings();
            RaiseChanged(nameof(CurrentRateText));
        }

        private void RaiseChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
