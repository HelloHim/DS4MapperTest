using System;
using System.Diagnostics;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Optional per-action "Counter Movement Release Press" for StickPadAction D-Pad modes
    /// (and, sharing the same instance, Analog Emulation). When a spring-loaded stick is
    /// released, the mechanical spring can take tens of milliseconds to return to centre.
    /// During that window this detector suppresses the stick's own returning digital
    /// direction and generates a short, optionally delayed press of the logically opposite
    /// direction, using whichever AxisDirButton bindings the action already owns.
    /// Detection is derived solely from analogue stick X/Y position; no other sensing is used.
    /// This is an offline accessibility and controller-customisation tool: it contains no
    /// anti-cheat detection or bypass logic, no server or game-process detection, and no
    /// behaviour that changes based on online state. Timing is entirely driven by the
    /// settings below.
    /// </summary>
    public class CounterMovementReleasePressProcessor
    {
        public enum CounterMovementReleasePressState
        {
            Unprimed,
            Idle,
            Armed,
            WaitingForCounterTap,
            CounterTapActive,
            Suppressed,
        }

        public enum ReleaseTriggerReason
        {
            None,
            Derivative,
            SlowReleaseFallback,
            NewInputCancellation,
            NeutralReset,
        }

        // Internal tunables. Kept out of the UI per spec until real trace tuning
        // demonstrates a need to expose them.
        private const double SMOOTHING_TAU_SECONDS = 0.010;
        public const double DEFAULT_ARMING_THRESHOLD = 0.0;
        private const double RESET_THRESHOLD = 0.20;
        private const double MINIMUM_RADIAL_DROP = 0.08;
        // Starting point derived from the spec's own worked example (a ~60-100ms spring
        // return implies the filtered radius collapsing at several units/second); this
        // has not been validated against real controller traces and should be retuned
        // once real release traces are available.
        private const double RELEASE_RATE_THRESHOLD = 4.0;
        private const double REENGAGE_RISE_THRESHOLD = 0.05;
        private const int REENGAGE_CONFIRM_TICKS = 3;
        private const double DT_HITCH_MULTIPLIER = 6.0;
        private const double DT_ABS_MAX_SECONDS = 0.5;
        private const double DT_AVG_TAU_SECONDS = 0.2;

        public static readonly int DEFAULT_TAP_LENGTH_MS = DigitalReleasePressPulse.DEFAULT_RELEASE_PRESS_DURATION_MS;

        public const int CS2_TAP_LENGTH_MINIMUM_MS = 78;
        public const int CS2_TAP_LENGTH_MAXIMUM_MS = 90;
        public const int CS2_FIXED_TAP_LENGTH_MS = 84;
        public const int CS2_WAIT_VARIANCE_PERCENT = 7;

        public const int MIN_WAIT_VARIANCE_PERCENT = 0;
        public const int MAX_WAIT_VARIANCE_PERCENT = 100;

        private static readonly StickPadAction.DpadDirections[] CardinalComponents = new[]
        {
            StickPadAction.DpadDirections.Up,
            StickPadAction.DpadDirections.Down,
            StickPadAction.DpadDirections.Left,
            StickPadAction.DpadDirections.Right,
        };

        /// <summary>
        /// Gate for diagnostic trace logging. Off by default to avoid unconditional
        /// production logging; flip on to capture a real controller-trace analysis.
        /// </summary>
        public bool DiagnosticsEnabled = false;

        private readonly IRandomRangeProvider randomProvider;

        public CounterMovementReleasePressProcessor() : this(RandomRangeProvider.Instance)
        {
        }

        /// <summary>
        /// Test/DI entry point: substitute a deterministic IRandomRangeProvider so timing
        /// tests never depend on real randomness.
        /// </summary>
        public CounterMovementReleasePressProcessor(IRandomRangeProvider randomProvider)
        {
            this.randomProvider = randomProvider ?? RandomRangeProvider.Instance;
        }

        private bool enabled;
        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                ForceReleaseAndReset();
            }
        }

        private bool useArrowKeysForCounterMovementPresses;
        public bool UseArrowKeysForCounterMovementPresses
        {
            get => useArrowKeysForCounterMovementPresses;
            set
            {
                if (useArrowKeysForCounterMovementPresses == value) return;
                useArrowKeysForCounterMovementPresses = value;
                ForceReleaseAndReset();
            }
        }

        private int minimumHoldMs = 0;
        public int MinimumHoldMs
        {
            get => minimumHoldMs;
            set => minimumHoldMs = DigitalReleasePressPulse.ClampMinimumHoldMs(value);
        }

        private double armingThreshold = DEFAULT_ARMING_THRESHOLD;
        public double ArmingThreshold
        {
            get => armingThreshold;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(armingThreshold - clamped) < double.Epsilon) return;
                armingThreshold = clamped;
                ForceReleaseAndReset();
            }
        }

        // New actions default to the CS2 preset (the only tuned/named preset available).
        private CounterMovementTapLengthPreset tapLengthPreset = CounterMovementTapLengthPreset.CS2;
        public CounterMovementTapLengthPreset TapLengthPreset
        {
            get => tapLengthPreset;
            set => tapLengthPreset = value;
        }

        // All tap-length representation storage and computation (mode, Fixed, Percent,
        // Minimum, Maximum, the percentage/best-fit maths and the CS2 constants) lives in
        // this one shared object so it exists in exactly one place;
        // TouchpadCounterMovementReleasePress composes the same type rather than duplicating
        // any of it. See its class doc for why synchronisation is never done inside a raw
        // property setter.
        private readonly CounterTapLengthTiming tapLengthTiming = new CounterTapLengthTiming();

        public CounterTapLengthMode CounterTapLengthMode
        {
            get => tapLengthTiming.Mode;
            set => tapLengthTiming.Mode = value;
        }

        public int CounterTapLengthMs
        {
            get => tapLengthTiming.FixedMs;
            set => tapLengthTiming.FixedMs = value;
        }

        public int CounterTapLengthVariancePercent
        {
            get => tapLengthTiming.VariancePercent;
            set => tapLengthTiming.VariancePercent = value;
        }

        public int CounterTapLengthMinimumMs
        {
            get => tapLengthTiming.MinimumMs;
            set => tapLengthTiming.MinimumMs = value;
        }

        public int CounterTapLengthMaximumMs
        {
            get => tapLengthTiming.MaximumMs;
            set => tapLengthTiming.MaximumMs = value;
        }

        /// <summary>
        /// User-edit entry point for Fixed mode / Wait Variance Percentage mode. See
        /// CounterTapLengthTiming.ApplyFixedAndPercentage. Only ever called from a
        /// ViewModel edit, CS2 preset application or profile migration - never from the
        /// per-report runtime path.
        /// </summary>
        public void ApplyFixedAndPercentage(int fixedMs, int percent) => tapLengthTiming.ApplyFixedAndPercentage(fixedMs, percent);

        /// <summary>
        /// User-edit entry point for Minimum and Maximum mode. See
        /// CounterTapLengthTiming.ApplyMinimumAndMaximum. Only ever called from a
        /// ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyMinimumAndMaximum(int minimumMs, int maximumMs)
        {
            tapLengthTiming.ApplyMinimumAndMaximum(minimumMs, maximumMs);
            NormalizeRanges();
        }

        /// <summary>
        /// Returns the runtime effective Minimum/Maximum for the currently selected mode.
        /// This is the single, central place mode-aware timing logic lives (per class doc):
        /// the state machine below must only ever consult this, never branch on the mode
        /// itself.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveCounterTapLengthRange() => tapLengthTiming.GetEffectiveRange();

        // All start-delay representation storage and computation (mode, Fixed, Percent,
        // Minimum, Maximum, the percentage/best-fit maths) lives in this one shared object,
        // mirroring tapLengthTiming above; TouchpadCounterMovementReleasePress composes the
        // same type rather than duplicating any of it.
        private readonly CounterTapStartDelayTiming startDelayTiming = new CounterTapStartDelayTiming();

        public CounterTapStartDelayMode CounterTapStartDelayMode
        {
            get => startDelayTiming.Mode;
            set => startDelayTiming.Mode = value;
        }

        public int CounterTapStartDelayMs
        {
            get => startDelayTiming.FixedMs;
            set => startDelayTiming.FixedMs = value;
        }

        public int CounterTapStartDelayVariancePercent
        {
            get => startDelayTiming.VariancePercent;
            set => startDelayTiming.VariancePercent = value;
        }

        public int CounterTapStartDelayMinimumMs
        {
            get => startDelayTiming.MinimumMs;
            set => startDelayTiming.MinimumMs = value;
        }

        public int CounterTapStartDelayMaximumMs
        {
            get => startDelayTiming.MaximumMs;
            set => startDelayTiming.MaximumMs = value;
        }

        /// <summary>
        /// User-edit entry point for Fixed mode / Wait Variance Percentage mode for the start
        /// delay. See CounterTapStartDelayTiming.ApplyFixedAndPercentage. Only ever called
        /// from a ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyStartDelayFixedAndPercentage(int fixedMs, int percent) => startDelayTiming.ApplyFixedAndPercentage(fixedMs, percent);

        /// <summary>
        /// User-edit entry point for Minimum and Maximum mode for the start delay. See
        /// CounterTapStartDelayTiming.ApplyMinimumAndMaximum. Only ever called from a
        /// ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyStartDelayMinimumAndMaximum(int minimumMs, int maximumMs)
        {
            startDelayTiming.ApplyMinimumAndMaximum(minimumMs, maximumMs);
            NormalizeRanges();
        }

        /// <summary>
        /// Returns the runtime effective Minimum/Maximum for the currently selected start
        /// delay mode. See GetEffectiveCounterTapLengthRange's class doc: the state machine
        /// below must only ever consult this, never branch on the mode itself.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveCounterTapStartDelayRange() => startDelayTiming.GetEffectiveRange();

        /// <summary>
        /// Corrects malformed range combinations (min greater than max, or a start delay
        /// maximum that could exceed the sampled tap-length window) in place. Cheap enough
        /// to call on every activation and after every load, so it is never relied on to
        /// run per mapper tick.
        /// </summary>
        public void NormalizeRanges()
        {
            if (CounterTapLengthMinimumMs > CounterTapLengthMaximumMs)
            {
                CounterTapLengthMaximumMs = CounterTapLengthMinimumMs;
            }

            if (CounterTapStartDelayMinimumMs > CounterTapStartDelayMaximumMs)
            {
                CounterTapStartDelayMaximumMs = CounterTapStartDelayMinimumMs;
            }

            // The start delay's Minimum/Maximum representation must never be able to sample
            // longer than the tap-length window can sample short, otherwise
            // actualOppositeHoldMs could go negative. This only constrains the stored
            // Minimum/Maximum representation (also used by Wait Variance Percentage mode,
            // which keeps Minimum/Maximum synchronised); Fixed mode's selected delay is
            // unaffected here and instead relies on actualOppositeHoldMs's own Math.Max(0, ...)
            // clamp below.
            if (CounterTapStartDelayMaximumMs > CounterTapLengthMinimumMs)
            {
                CounterTapStartDelayMaximumMs = CounterTapLengthMinimumMs;
            }

            if (CounterTapStartDelayMinimumMs > CounterTapStartDelayMaximumMs)
            {
                CounterTapStartDelayMinimumMs = CounterTapStartDelayMaximumMs;
            }
        }

        /// <summary>
        /// Applies the CS2 preset (its 84ms/7% Fixed and Wait Variance Percentage values,
        /// and their equivalent 78-90ms Minimum/Maximum; start delay and every other setting
        /// are left untouched) and marks the preset as CS2. Does not change the selected
        /// timing mode: which representation the preset actually drives at runtime depends on
        /// whatever mode is already selected.
        /// </summary>
        public void ApplyCs2Preset()
        {
            tapLengthTiming.ApplyCs2Preset();
            tapLengthPreset = CounterMovementTapLengthPreset.CS2;
        }

        /// <summary>
        /// True whenever the current numeric tap-length values happen to equal the CS2
        /// preset's values, regardless of how they got there (the preset dropdown, a direct
        /// edit, migration, or a loaded profile).
        /// </summary>
        public bool MatchesCs2Values => tapLengthTiming.MatchesCs2Values;

        /// <summary>
        /// The preset that should actually be displayed, derived purely from the current
        /// numeric values rather than the stored preset field: this makes the relationship
        /// bidirectional. Editing away from 78/90 shows Custom; editing back to exactly
        /// 78/90 (by hand or via the dropdown) shows CS2 again. There are only two presets
        /// and CS2 is entirely defined by its values, so there is never a meaningful
        /// distinction between "custom values that happen to equal 78/90" and "CS2".
        /// </summary>
        public CounterMovementTapLengthPreset EffectiveTapLengthPreset =>
            MatchesCs2Values ? CounterMovementTapLengthPreset.CS2 : CounterMovementTapLengthPreset.Custom;

        private CounterMovementReleasePressState state = CounterMovementReleasePressState.Unprimed;
        public CounterMovementReleasePressState State => state;

        private bool filterSeeded;
        private double rFiltered;
        private double prevRFiltered;
        private double peakRadius;

        private double avgDt;
        private bool avgDtInitialized;

        private StickPadAction.DpadDirections latchedZone;
        private double holdUp, holdDown, holdLeft, holdRight;

        private StickPadAction.DpadDirections suppressedComponents;
        private StickPadAction.DpadDirections pulseOwnedComponents;
        private StickPadAction.DpadDirections pendingOppositeComponents;
        private StickPadAction.DpadDirections normalReleaseComponents;
        private StickPadAction.DpadDirections arrowReleaseComponents;
        private bool pendingUsesArrowKeys;
        private bool pulseUsesArrowKeys;
        private readonly AxisDirButton[] arrowOutputs = new AxisDirButton[13];

        // Sampled once per activation in EnterReleasePress, then held for the lifetime of
        // that activation. Never resampled while WaitingForCounterTap/CounterTapActive or
        // by mapper updates; only a fresh qualifying release samples new values.
        private int selectedTotalTapWindowMs;
        private int selectedStartDelayMs;
        private int actualOppositeHoldMs;

        // Single monotonic reference point for the whole release-to-end window (see class
        // doc / EnterReleasePress): both the "begin opposite press" and "end the action"
        // checks compare elapsed time from this one timestamp, so mapper update rounding
        // can never extend the action past the requested window. Accumulated from
        // validated report dt (genuine elapsed time, never a poll count), so it tracks real
        // elapsed time regardless of report cadence and is immune to hitches (only
        // validated samples advance it). A monotonic wall-clock fallback bounds the action
        // if the reader reports invalid dt for a few frames.
        private double releasePressElapsedSeconds;
        private long releasePressStartTimestamp;

        private bool hasFiredThisCycle;
        private double postTriggerMinR;
        private int risingTickCount;

        private ReleaseTriggerReason lastTriggerReason = ReleaseTriggerReason.None;

        /// <summary>
        /// One state-machine tick. Must be called from StickPadAction.Prepare, immediately
        /// after DetermineDirection() computes the raw (unmasked) 8-way zone. Returns the
        /// direction StickPadAction should actually use for the rest of its own press/release
        /// bookkeeping this tick (equal to rawCurrentDir unless a component is suppressed).
        /// </summary>
        public StickPadAction.DpadDirections Prepare(Mapper mapper, int axisXDir, int axisYDir,
            int maxDirX, int maxDirY, StickPadAction.DpadDirections rawCurrentDir)
        {
            if (!enabled)
            {
                if (state != CounterMovementReleasePressState.Unprimed ||
                    suppressedComponents != StickPadAction.DpadDirections.Centered ||
                    pulseOwnedComponents != StickPadAction.DpadDirections.Centered)
                {
                    ForceReleaseAndReset();
                }
                return rawCurrentDir;
            }

            double rPhysical = ComputeRadialMagnitude(axisXDir, axisYDir, maxDirX, maxDirY);
            double dt = mapper.CurrentLatency;

            bool dtValidForAvg = dt > 0.0 && dt <= DT_ABS_MAX_SECONDS;
            bool dtValid = dtValidForAvg && (!avgDtInitialized || dt <= avgDt * DT_HITCH_MULTIPLIER);
            if (dtValidForAvg)
            {
                UpdateAvgDt(dt);
            }

            if (!filterSeeded)
            {
                rFiltered = rPhysical;
                prevRFiltered = rFiltered;
                filterSeeded = true;
                dtValid = false;
            }
            else if (!dtValid)
            {
                prevRFiltered = rFiltered;
                rFiltered = rPhysical;
            }
            else
            {
                double alpha = dt / (SMOOTHING_TAU_SECONDS + dt);
                prevRFiltered = rFiltered;
                rFiltered += alpha * (rPhysical - rFiltered);
            }

            double dr = dtValid ? (rFiltered - prevRFiltered) / dt : 0.0;

            StickPadAction.DpadDirections effectiveDir = rawCurrentDir;

            switch (state)
            {
                case CounterMovementReleasePressState.Unprimed:
                    if (rFiltered <= RESET_THRESHOLD)
                    {
                        state = CounterMovementReleasePressState.Idle;
                    }
                    break;

                case CounterMovementReleasePressState.Idle:
                    if (rawCurrentDir != StickPadAction.DpadDirections.Centered &&
                        (armingThreshold <= 0.0 || rFiltered >= armingThreshold))
                    {
                        state = CounterMovementReleasePressState.Armed;
                        peakRadius = rFiltered;
                        latchedZone = rawCurrentDir;
                        hasFiredThisCycle = false;
                        holdUp = holdDown = holdLeft = holdRight = 0.0;
                        AccumulateHold(rawCurrentDir, dtValid ? dt : 0.0);
                    }
                    break;

                case CounterMovementReleasePressState.Armed:
                    peakRadius = Math.Max(peakRadius, rFiltered);
                    if (rawCurrentDir != StickPadAction.DpadDirections.Centered)
                    {
                        latchedZone = rawCurrentDir;
                        AccumulateHold(rawCurrentDir, dtValid ? dt : 0.0);
                    }

                    bool fastRelease = !hasFiredThisCycle && dtValid &&
                        dr <= -RELEASE_RATE_THRESHOLD &&
                        (peakRadius - rFiltered) >= MINIMUM_RADIAL_DROP;

                    bool slowFallback = !hasFiredThisCycle && rawCurrentDir == StickPadAction.DpadDirections.Centered;

                    if (fastRelease)
                    {
                        EnterReleasePress(ReleaseTriggerReason.Derivative);
                    }
                    else if (slowFallback)
                    {
                        EnterReleasePress(ReleaseTriggerReason.SlowReleaseFallback);
                    }
                    break;

                case CounterMovementReleasePressState.WaitingForCounterTap:
                    effectiveDir = ApplySuppression(rawCurrentDir);
                    CheckReengagement(rawCurrentDir, dr, dtValid, ref effectiveDir);
                    if (state == CounterMovementReleasePressState.WaitingForCounterTap)
                    {
                        if (dtValid) releasePressElapsedSeconds += dt;
                        AdvanceWaitingForCounterTap();
                    }
                    break;

                case CounterMovementReleasePressState.CounterTapActive:
                    effectiveDir = ApplySuppression(rawCurrentDir);
                    CheckReengagement(rawCurrentDir, dr, dtValid, ref effectiveDir);
                    if (state == CounterMovementReleasePressState.CounterTapActive)
                    {
                        if (dtValid) releasePressElapsedSeconds += dt;
                        if (GetReleasePressElapsedMs() >= selectedTotalTapWindowMs)
                        {
                            EndCounterTap();
                        }
                    }
                    break;

                case CounterMovementReleasePressState.Suppressed:
                    effectiveDir = ApplySuppression(rawCurrentDir);
                    CheckReengagement(rawCurrentDir, dr, dtValid, ref effectiveDir);
                    if (state == CounterMovementReleasePressState.Suppressed && rFiltered <= RESET_THRESHOLD)
                    {
                        suppressedComponents = StickPadAction.DpadDirections.Centered;
                        latchedZone = StickPadAction.DpadDirections.Centered;
                        holdUp = holdDown = holdLeft = holdRight = 0.0;
                        state = CounterMovementReleasePressState.Idle;
                        lastTriggerReason = ReleaseTriggerReason.NeutralReset;
                    }
                    break;
            }

            LogDiagnostic(rPhysical, dt, dr, dtValid, rawCurrentDir, effectiveDir);

            return effectiveDir;
        }

        /// <summary>
        /// Must be called from StickPadAction.Event, after its own normal press/release logic
        /// has run against the (already masked) direction returned by Prepare.
        /// </summary>
        public void Event(Mapper mapper, AxisDirButton[] usedFuncList)
        {
            FlushReleases(mapper, usedFuncList);

            if (usedFuncList == null) return;

            for (int i = 0; i < CardinalComponents.Length; i++)
            {
                StickPadAction.DpadDirections c = CardinalComponents[i];
                if (Has(pulseOwnedComponents, c))
                {
                    AxisDirButton data = GetCounterMovementOutput(mapper, usedFuncList, c,
                        pulseUsesArrowKeys);
                    if (data != null)
                    {
                        data.PrepareAnalog(mapper, 1.0, 1.0);
                        data.Event(mapper);
                    }
                }
            }
        }

        /// <summary>
        /// Called from StickPadAction.Release/SoftRelease. Releases only owned output,
        /// clears all state, and returns to Unprimed so a subsequent controller connect/profile
        /// load/layer switch cannot cause a spurious activation.
        /// </summary>
        public void Cleanup(Mapper mapper, AxisDirButton[] usedFuncList)
        {
            ForceReleaseAndReset();
            FlushReleases(mapper, usedFuncList);
        }

        private void FlushReleases(Mapper mapper, AxisDirButton[] usedFuncList)
        {
            if (usedFuncList == null)
            {
                return;
            }

            for (int i = 0; i < CardinalComponents.Length; i++)
            {
                StickPadAction.DpadDirections c = CardinalComponents[i];
                if (Has(normalReleaseComponents, c))
                {
                    AxisDirButton data = usedFuncList[(int)c];
                    if (data != null)
                    {
                        data.PrepareAnalog(mapper, 0.0, 0.0);
                        data.Event(mapper);
                    }
                }

                if (Has(arrowReleaseComponents, c))
                {
                    AxisDirButton data = GetCounterMovementOutput(mapper, usedFuncList, c, true);
                    if (data != null)
                    {
                        data.PrepareAnalog(mapper, 0.0, 0.0);
                        data.Event(mapper);
                    }
                }
            }

            normalReleaseComponents = StickPadAction.DpadDirections.Centered;
            arrowReleaseComponents = StickPadAction.DpadDirections.Centered;
        }

        /// <summary>
        /// Entered once per qualifying release. Samples the total tap window and start
        /// delay exactly once here (never resampled for the lifetime of this activation),
        /// subtracts the delay from the window per the class doc, and either begins the
        /// opposite press immediately (delay 0, matching the pre-timing-variance behaviour)
        /// or moves to WaitingForCounterTap to wait out the delay first.
        /// </summary>
        private void EnterReleasePress(ReleaseTriggerReason reason)
        {
            hasFiredThisCycle = true;
            lastTriggerReason = reason;

            double minHoldSeconds = minimumHoldMs / 1000.0;
            StickPadAction.DpadDirections eligible = StickPadAction.DpadDirections.Centered;
            for (int i = 0; i < CardinalComponents.Length; i++)
            {
                StickPadAction.DpadDirections c = CardinalComponents[i];
                if (Has(latchedZone, c) && GetHold(c) >= minHoldSeconds)
                {
                    eligible |= c;
                }
            }

            if (eligible == StickPadAction.DpadDirections.Centered)
            {
                // Nothing qualified for a press. Still consume the single trigger for this
                // movement cycle and wait out neutral before arming again.
                suppressedComponents = StickPadAction.DpadDirections.Centered;
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                state = CounterMovementReleasePressState.Suppressed;
                return;
            }

            suppressedComponents = eligible;
            StickPadAction.DpadDirections opposite = StickPadAction.DpadDirections.Centered;
            for (int i = 0; i < CardinalComponents.Length; i++)
            {
                StickPadAction.DpadDirections c = CardinalComponents[i];
                if (Has(eligible, c))
                {
                    opposite |= ComponentOpposite(c);
                }
            }

            NormalizeRanges();
            if (CounterTapLengthMode == CounterTapLengthMode.Fixed)
            {
                // Fixed mode is deterministic: every qualifying activation uses exactly the
                // fixed duration, so the random provider is never consulted for it at all.
                selectedTotalTapWindowMs = CounterTapLengthMs;
            }
            else
            {
                (int effectiveMinimumMs, int effectiveMaximumMs) = GetEffectiveCounterTapLengthRange();
                selectedTotalTapWindowMs = randomProvider.NextInclusive(effectiveMinimumMs, effectiveMaximumMs);
            }

            if (CounterTapStartDelayMode == CounterTapStartDelayMode.Fixed)
            {
                // Fixed mode is deterministic: every qualifying activation uses exactly the
                // fixed delay, so the random provider is never consulted for it at all.
                selectedStartDelayMs = CounterTapStartDelayMs;
            }
            else
            {
                (int effectiveStartDelayMinimumMs, int effectiveStartDelayMaximumMs) = GetEffectiveCounterTapStartDelayRange();
                selectedStartDelayMs = randomProvider.NextInclusive(effectiveStartDelayMinimumMs, effectiveStartDelayMaximumMs);
            }
            // The start delay is included inside the selected tap-length window, not added
            // on top of it: the delay is subtracted from the total window to get the actual
            // opposite-direction hold duration, clamped at zero so a delay sampled equal to
            // (or, defensively, slightly above) the window can never go negative.
            actualOppositeHoldMs = Math.Max(0, selectedTotalTapWindowMs - selectedStartDelayMs);

            pendingOppositeComponents = opposite;
            pendingUsesArrowKeys = useArrowKeysForCounterMovementPresses;
            releasePressElapsedSeconds = 0.0;
            releasePressStartTimestamp = Stopwatch.GetTimestamp();
            postTriggerMinR = rFiltered;
            risingTickCount = 0;

            if (selectedStartDelayMs <= 0)
            {
                BeginCounterTapOrSkip();
            }
            else
            {
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                state = CounterMovementReleasePressState.WaitingForCounterTap;
            }
        }

        private void AdvanceWaitingForCounterTap()
        {
            if (GetReleasePressElapsedMs() >= selectedStartDelayMs)
            {
                BeginCounterTapOrSkip();

                // A single huge dt jump (e.g. a resumed-from-background report) could
                // satisfy both thresholds in the same tick; handle that immediately rather
                // than waiting for a further tick that may never suppress correctly.
                if (state == CounterMovementReleasePressState.CounterTapActive &&
                    GetReleasePressElapsedMs() >= selectedTotalTapWindowMs)
                {
                    EndCounterTap();
                }
            }
        }

        /// <summary>
        /// Begins the generated opposite press, unless the computed hold duration is zero
        /// (total window entirely consumed by the delay), in which case no key is pressed
        /// at all and the action ends cleanly by moving straight to Suppressed.
        /// </summary>
        private void BeginCounterTapOrSkip()
        {
            if (actualOppositeHoldMs <= 0)
            {
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                releasePressStartTimestamp = 0;
                state = CounterMovementReleasePressState.Suppressed;
                return;
            }

            pulseOwnedComponents = pendingOppositeComponents;
            pulseUsesArrowKeys = pendingUsesArrowKeys;
            state = CounterMovementReleasePressState.CounterTapActive;
        }

        private void EndCounterTap()
        {
            MarkPulseForRelease(pulseOwnedComponents);
            pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
            releasePressStartTimestamp = 0;
            state = CounterMovementReleasePressState.Suppressed;
        }

        private void CheckReengagement(StickPadAction.DpadDirections rawCurrentDir, double dr, bool dtValid,
            ref StickPadAction.DpadDirections effectiveDir)
        {
            double rNow = rFiltered;
            if (rNow < postTriggerMinR)
            {
                postTriggerMinR = rNow;
                risingTickCount = 0;
            }
            else if (dtValid && dr > 0.0)
            {
                risingTickCount++;
            }
            else if (dtValid)
            {
                risingTickCount = 0;
            }

            bool confirmedRise = risingTickCount >= REENGAGE_CONFIRM_TICKS &&
                (rNow - postTriggerMinR) >= REENGAGE_RISE_THRESHOLD;

            bool genuineNewInput = confirmedRise && rawCurrentDir != StickPadAction.DpadDirections.Centered;
            if (!genuineNewInput)
            {
                return;
            }

            bool touchesOriginal = false;
            bool touchesPulse = false;
            for (int i = 0; i < CardinalComponents.Length; i++)
            {
                StickPadAction.DpadDirections c = CardinalComponents[i];
                if (!Has(rawCurrentDir, c)) continue;
                if (Has(suppressedComponents, c)) touchesOriginal = true;
                if (Has(pulseOwnedComponents, c)) touchesPulse = true;
            }

            if (touchesOriginal)
            {
                // Re-peek: user pushed the original direction again. Cancel the matching
                // opposite pulse component and stop suppressing the original.
                for (int i = 0; i < CardinalComponents.Length; i++)
                {
                    StickPadAction.DpadDirections c = CardinalComponents[i];
                    if (Has(rawCurrentDir, c) && Has(suppressedComponents, c))
                    {
                        suppressedComponents &= ~c;
                        StickPadAction.DpadDirections opp = ComponentOpposite(c);
                        if (Has(pulseOwnedComponents, opp))
                        {
                            pulseOwnedComponents &= ~opp;
                            if (pulseOwnedComponents == StickPadAction.DpadDirections.Centered)
                            {
                                releasePressStartTimestamp = 0;
                            }
                            MarkPulseForRelease(opp);
                        }
                    }
                }
                lastTriggerReason = ReleaseTriggerReason.NewInputCancellation;
                effectiveDir = ApplySuppression(rawCurrentDir);

                if (suppressedComponents == StickPadAction.DpadDirections.Centered &&
                    pulseOwnedComponents == StickPadAction.DpadDirections.Centered)
                {
                    ResumeArmedTracking(rawCurrentDir);
                }
            }
            else if (touchesPulse)
            {
                // Reverse into the release press direction: hand the key over to the normal masked path
                // without an intervening release, so it stays continuously held. That silent
                // hand-off is only correct when the pulse was driving the action's own
                // direction binding, i.e. the very same AxisDirButton the normal path is
                // about to press. With arrow-key output the pulse owns a different key from
                // the movement bind, so nothing downstream ever takes ownership of it and it
                // must be released explicitly or it stays held indefinitely.
                for (int i = 0; i < CardinalComponents.Length; i++)
                {
                    StickPadAction.DpadDirections c = CardinalComponents[i];
                    if (Has(rawCurrentDir, c) && Has(pulseOwnedComponents, c))
                    {
                        pulseOwnedComponents &= ~c;
                        if (pulseUsesArrowKeys)
                        {
                            MarkPulseForRelease(c);
                        }
                    }
                }
                if (pulseOwnedComponents == StickPadAction.DpadDirections.Centered)
                {
                    releasePressStartTimestamp = 0;
                }
                lastTriggerReason = ReleaseTriggerReason.NewInputCancellation;
                effectiveDir = ApplySuppression(rawCurrentDir);
            }
            else
            {
                // Genuinely different direction (this also covers WaitingForCounterTap,
                // where pulseOwnedComponents is still empty so touchesPulse can never be
                // true): abandon this activation entirely, release anything owned so far,
                // and start fresh tracking of the new push.
                MarkPulseForRelease(pulseOwnedComponents);
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                releasePressStartTimestamp = 0;
                suppressedComponents = StickPadAction.DpadDirections.Centered;
                lastTriggerReason = ReleaseTriggerReason.NewInputCancellation;
                effectiveDir = rawCurrentDir;
                ResumeArmedTracking(rawCurrentDir);
            }
        }

        private void ResumeArmedTracking(StickPadAction.DpadDirections rawCurrentDir)
        {
            state = CounterMovementReleasePressState.Armed;
            hasFiredThisCycle = false;
            latchedZone = rawCurrentDir;
            peakRadius = rFiltered;
            holdUp = holdDown = holdLeft = holdRight = 0.0;
            AccumulateHold(rawCurrentDir, 0.0);
        }

        private StickPadAction.DpadDirections ApplySuppression(StickPadAction.DpadDirections raw)
        {
            return raw & ~suppressedComponents;
        }

        private void AccumulateHold(StickPadAction.DpadDirections zone, double dt)
        {
            holdUp = Has(zone, StickPadAction.DpadDirections.Up) ? holdUp + dt : 0.0;
            holdDown = Has(zone, StickPadAction.DpadDirections.Down) ? holdDown + dt : 0.0;
            holdLeft = Has(zone, StickPadAction.DpadDirections.Left) ? holdLeft + dt : 0.0;
            holdRight = Has(zone, StickPadAction.DpadDirections.Right) ? holdRight + dt : 0.0;
        }

        private double GetHold(StickPadAction.DpadDirections component)
        {
            if (component == StickPadAction.DpadDirections.Up) return holdUp;
            if (component == StickPadAction.DpadDirections.Down) return holdDown;
            if (component == StickPadAction.DpadDirections.Left) return holdLeft;
            if (component == StickPadAction.DpadDirections.Right) return holdRight;
            return 0.0;
        }

        private void UpdateAvgDt(double dt)
        {
            if (!avgDtInitialized)
            {
                avgDt = dt;
                avgDtInitialized = true;
            }
            else
            {
                double alpha = dt / (DT_AVG_TAU_SECONDS + dt);
                avgDt += alpha * (dt - avgDt);
            }
        }

        private double GetReleasePressElapsedMs()
        {
            double accumulated = releasePressElapsedSeconds;
            if (releasePressStartTimestamp != 0)
            {
                double wallElapsedSeconds = (Stopwatch.GetTimestamp() - releasePressStartTimestamp) /
                    (double)Stopwatch.Frequency;
                accumulated = Math.Max(accumulated, wallElapsedSeconds);
            }

            return accumulated * 1000.0;
        }

        private static bool Has(StickPadAction.DpadDirections mask, StickPadAction.DpadDirections bit)
        {
            return (mask & bit) != 0;
        }

        private static StickPadAction.DpadDirections ComponentOpposite(StickPadAction.DpadDirections component)
        {
            switch (component)
            {
                case StickPadAction.DpadDirections.Up: return StickPadAction.DpadDirections.Down;
                case StickPadAction.DpadDirections.Down: return StickPadAction.DpadDirections.Up;
                case StickPadAction.DpadDirections.Left: return StickPadAction.DpadDirections.Right;
                case StickPadAction.DpadDirections.Right: return StickPadAction.DpadDirections.Left;
                default: return StickPadAction.DpadDirections.Centered;
            }
        }

        private AxisDirButton GetCounterMovementOutput(Mapper mapper, AxisDirButton[] normalOutputs,
            StickPadAction.DpadDirections direction, bool useArrowKeys)
        {
            if (!useArrowKeys)
            {
                return normalOutputs[(int)direction];
            }

            int index = (int)direction;
            if (arrowOutputs[index] == null)
            {
                if (direction != StickPadAction.DpadDirections.Up &&
                    direction != StickPadAction.DpadDirections.Down &&
                    direction != StickPadAction.DpadDirections.Left &&
                    direction != StickPadAction.DpadDirections.Right)
                {
                    return null;
                }

                VirtualKeys key = direction switch
                {
                    StickPadAction.DpadDirections.Up => VirtualKeys.Up,
                    StickPadAction.DpadDirections.Down => VirtualKeys.Down,
                    StickPadAction.DpadDirections.Left => VirtualKeys.Left,
                    _ => VirtualKeys.Right,
                };

                VirtualKBMMapping keyboardMapping = mapper.EventInputMapping ??
                    ProfileSerializer.EventInputMapper;
                uint realKey = keyboardMapping != null ? keyboardMapping.GetRealEventKey((uint)key) :
                    (uint)key;
                OutputActionData output = new OutputActionData(OutputActionData.ActionType.Keyboard,
                    (int)key, (int)realKey);
                output.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[key];
                arrowOutputs[index] = new AxisDirButton(output);
            }

            return arrowOutputs[index];
        }

        private void MarkPulseForRelease(StickPadAction.DpadDirections components)
        {
            if (pulseUsesArrowKeys)
            {
                arrowReleaseComponents |= components;
            }
            else
            {
                normalReleaseComponents |= components;
            }
        }

        private static double ComputeRadialMagnitude(int axisXDir, int axisYDir, int maxDirX, int maxDirY)
        {
            double angle = Math.Atan2(-axisYDir, axisXDir);
            double angCos = Math.Abs(Math.Cos(angle));
            double angSin = Math.Abs(Math.Sin(angle));
            double maxRadiusAtAngle = Math.Sqrt(Math.Pow(maxDirX * angCos, 2) + Math.Pow(maxDirY * angSin, 2));
            if (maxRadiusAtAngle <= 0.0) return 0.0;

            double raw = Math.Sqrt(((double)axisXDir * axisXDir) + ((double)axisYDir * axisYDir));
            return raw / maxRadiusAtAngle;
        }

        /// <summary>
        /// Full reset used on enable/disable and Cleanup. Marks any currently pulse-owned
        /// components for release on the next flush; caller is responsible for actually
        /// flushing (Cleanup does so immediately, the Enabled setter relies on the next
        /// natural Event tick).
        /// </summary>
        private void ForceReleaseAndReset()
        {
            MarkPulseForRelease(pulseOwnedComponents);
            pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
            pendingOppositeComponents = StickPadAction.DpadDirections.Centered;
            pendingUsesArrowKeys = false;
            pulseUsesArrowKeys = false;
            suppressedComponents = StickPadAction.DpadDirections.Centered;
            latchedZone = StickPadAction.DpadDirections.Centered;
            holdUp = holdDown = holdLeft = holdRight = 0.0;
            hasFiredThisCycle = false;
            selectedTotalTapWindowMs = 0;
            selectedStartDelayMs = 0;
            actualOppositeHoldMs = 0;
            releasePressElapsedSeconds = 0.0;
            releasePressStartTimestamp = 0;
            state = CounterMovementReleasePressState.Unprimed;
            filterSeeded = false;
            rFiltered = prevRFiltered = 0.0;
            peakRadius = 0.0;
            avgDtInitialized = false;
            postTriggerMinR = 0.0;
            risingTickCount = 0;
            lastTriggerReason = ReleaseTriggerReason.None;
        }

        private void LogDiagnostic(double rPhysical, double dt, double dr, bool dtValid,
            StickPadAction.DpadDirections rawDir, StickPadAction.DpadDirections effectiveDir)
        {
            if (!DiagnosticsEnabled) return;

            Trace.WriteLine(string.Format(
                "[CounterMovementReleasePress] t={0} dt={1:F5} valid={2} r={3:F3} rF={4:F3} dr={5:F2} state={6} " +
                "raw={7} eff={8} latched={9} peak={10:F3} suppressed={11} pulseOwned={12} reason={13} " +
                "totalWindowMs={14} startDelayMs={15} actualHoldMs={16}",
                Environment.TickCount64, dt, dtValid, rPhysical, rFiltered, dr, state,
                rawDir, effectiveDir, latchedZone, peakRadius, suppressedComponents, pulseOwnedComponents,
                lastTriggerReason, selectedTotalTapWindowMs, selectedStartDelayMs, actualOppositeHoldMs));
        }
    }
}
