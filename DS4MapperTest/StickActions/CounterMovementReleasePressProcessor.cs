using System;
using System.Diagnostics;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;

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
            WaitingForOppositeTap,
            OppositeTapActive,
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

        // Start delay is clamped to the same absolute ceiling as tap length; the tighter,
        // "must not exceed the selected tap-length minimum" constraint is enforced by
        // NormalizeRanges rather than by this per-field clamp.
        private const int MIN_START_DELAY_MS = 0;
        private const int MAX_START_DELAY_MS = DigitalReleaseBrakePulse.MAX_BRAKE_DURATION_MS;

        public static readonly int DEFAULT_TAP_LENGTH_MS = DigitalReleaseBrakePulse.DEFAULT_BRAKE_DURATION_MS;
        public const int DEFAULT_START_DELAY_MINIMUM_MS = 0;
        public const int DEFAULT_START_DELAY_MAXIMUM_MS = 20;

        public const int CS2_TAP_LENGTH_MINIMUM_MS = 75;
        public const int CS2_TAP_LENGTH_MAXIMUM_MS = 120;

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

        private int minimumHoldMs = 0;
        public int MinimumHoldMs
        {
            get => minimumHoldMs;
            set => minimumHoldMs = DigitalReleaseBrakePulse.ClampMinimumHoldMs(value);
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

        private int oppositeTapLengthMinimumMs = CS2_TAP_LENGTH_MINIMUM_MS;
        public int OppositeTapLengthMinimumMs
        {
            get => oppositeTapLengthMinimumMs;
            set => oppositeTapLengthMinimumMs = DigitalReleaseBrakePulse.ClampBrakeDurationMs(value);
        }

        private int oppositeTapLengthMaximumMs = CS2_TAP_LENGTH_MAXIMUM_MS;
        public int OppositeTapLengthMaximumMs
        {
            get => oppositeTapLengthMaximumMs;
            set => oppositeTapLengthMaximumMs = DigitalReleaseBrakePulse.ClampBrakeDurationMs(value);
        }

        private int oppositeTapStartDelayMinimumMs = DEFAULT_START_DELAY_MINIMUM_MS;
        public int OppositeTapStartDelayMinimumMs
        {
            get => oppositeTapStartDelayMinimumMs;
            set => oppositeTapStartDelayMinimumMs = Math.Clamp(value, MIN_START_DELAY_MS, MAX_START_DELAY_MS);
        }

        private int oppositeTapStartDelayMaximumMs = DEFAULT_START_DELAY_MAXIMUM_MS;
        public int OppositeTapStartDelayMaximumMs
        {
            get => oppositeTapStartDelayMaximumMs;
            set => oppositeTapStartDelayMaximumMs = Math.Clamp(value, MIN_START_DELAY_MS, MAX_START_DELAY_MS);
        }

        /// <summary>
        /// Corrects malformed range combinations (min greater than max, or a start delay
        /// maximum that could exceed the sampled tap-length window) in place. Cheap enough
        /// to call on every activation and after every load, so it is never relied on to
        /// run per mapper tick.
        /// </summary>
        public void NormalizeRanges()
        {
            if (oppositeTapLengthMinimumMs > oppositeTapLengthMaximumMs)
            {
                oppositeTapLengthMaximumMs = oppositeTapLengthMinimumMs;
            }

            if (oppositeTapStartDelayMinimumMs > oppositeTapStartDelayMaximumMs)
            {
                oppositeTapStartDelayMaximumMs = oppositeTapStartDelayMinimumMs;
            }

            // The start delay must never be able to sample longer than the tap-length window
            // can sample short, otherwise actualOppositeHoldMs could go negative.
            if (oppositeTapStartDelayMaximumMs > oppositeTapLengthMinimumMs)
            {
                oppositeTapStartDelayMaximumMs = oppositeTapLengthMinimumMs;
            }

            if (oppositeTapStartDelayMinimumMs > oppositeTapStartDelayMaximumMs)
            {
                oppositeTapStartDelayMinimumMs = oppositeTapStartDelayMaximumMs;
            }
        }

        /// <summary>
        /// Applies the CS2 preset (75-120ms tap length only; start delay and every other
        /// setting are left untouched) and marks the preset as CS2.
        /// </summary>
        public void ApplyCs2Preset()
        {
            oppositeTapLengthMinimumMs = CS2_TAP_LENGTH_MINIMUM_MS;
            oppositeTapLengthMaximumMs = CS2_TAP_LENGTH_MAXIMUM_MS;
            tapLengthPreset = CounterMovementTapLengthPreset.CS2;
        }

        /// <summary>
        /// True whenever the current numeric tap-length values happen to equal the CS2
        /// preset's values, regardless of how they got there (the preset dropdown, a direct
        /// edit, migration, or a loaded profile).
        /// </summary>
        public bool MatchesCs2Values =>
            oppositeTapLengthMinimumMs == CS2_TAP_LENGTH_MINIMUM_MS &&
            oppositeTapLengthMaximumMs == CS2_TAP_LENGTH_MAXIMUM_MS;

        /// <summary>
        /// The preset that should actually be displayed, derived purely from the current
        /// numeric values rather than the stored preset field: this makes the relationship
        /// bidirectional. Editing away from 75/120 shows Custom; editing back to exactly
        /// 75/120 (by hand or via the dropdown) shows CS2 again. There are only two presets
        /// and CS2 is entirely defined by its values, so there is never a meaningful
        /// distinction between "custom values that happen to equal 75/120" and "CS2".
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
        private StickPadAction.DpadDirections explicitReleaseComponents;

        // Sampled once per activation in EnterReleasePress, then held for the lifetime of
        // that activation. Never resampled while WaitingForOppositeTap/OppositeTapActive or
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

                case CounterMovementReleasePressState.WaitingForOppositeTap:
                    effectiveDir = ApplySuppression(rawCurrentDir);
                    CheckReengagement(rawCurrentDir, dr, dtValid, ref effectiveDir);
                    if (state == CounterMovementReleasePressState.WaitingForOppositeTap)
                    {
                        if (dtValid) releasePressElapsedSeconds += dt;
                        AdvanceWaitingForOppositeTap();
                    }
                    break;

                case CounterMovementReleasePressState.OppositeTapActive:
                    effectiveDir = ApplySuppression(rawCurrentDir);
                    CheckReengagement(rawCurrentDir, dr, dtValid, ref effectiveDir);
                    if (state == CounterMovementReleasePressState.OppositeTapActive)
                    {
                        if (dtValid) releasePressElapsedSeconds += dt;
                        if (GetReleasePressElapsedMs() >= selectedTotalTapWindowMs)
                        {
                            EndOppositeTap();
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
                    AxisDirButton data = usedFuncList[(int)c];
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
            if (usedFuncList == null || explicitReleaseComponents == StickPadAction.DpadDirections.Centered)
            {
                return;
            }

            for (int i = 0; i < CardinalComponents.Length; i++)
            {
                StickPadAction.DpadDirections c = CardinalComponents[i];
                if (Has(explicitReleaseComponents, c))
                {
                    AxisDirButton data = usedFuncList[(int)c];
                    if (data != null)
                    {
                        data.PrepareAnalog(mapper, 0.0, 0.0);
                        data.Event(mapper);
                    }
                }
            }

            explicitReleaseComponents = StickPadAction.DpadDirections.Centered;
        }

        /// <summary>
        /// Entered once per qualifying release. Samples the total tap window and start
        /// delay exactly once here (never resampled for the lifetime of this activation),
        /// subtracts the delay from the window per the class doc, and either begins the
        /// opposite press immediately (delay 0, matching the pre-timing-variance behaviour)
        /// or moves to WaitingForOppositeTap to wait out the delay first.
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
            selectedTotalTapWindowMs = randomProvider.NextInclusive(oppositeTapLengthMinimumMs, oppositeTapLengthMaximumMs);
            selectedStartDelayMs = randomProvider.NextInclusive(oppositeTapStartDelayMinimumMs, oppositeTapStartDelayMaximumMs);
            // The start delay is included inside the selected tap-length window, not added
            // on top of it: the delay is subtracted from the total window to get the actual
            // opposite-direction hold duration, clamped at zero so a delay sampled equal to
            // (or, defensively, slightly above) the window can never go negative.
            actualOppositeHoldMs = Math.Max(0, selectedTotalTapWindowMs - selectedStartDelayMs);

            pendingOppositeComponents = opposite;
            releasePressElapsedSeconds = 0.0;
            releasePressStartTimestamp = Stopwatch.GetTimestamp();
            postTriggerMinR = rFiltered;
            risingTickCount = 0;

            if (selectedStartDelayMs <= 0)
            {
                BeginOppositeTapOrSkip();
            }
            else
            {
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                state = CounterMovementReleasePressState.WaitingForOppositeTap;
            }
        }

        private void AdvanceWaitingForOppositeTap()
        {
            if (GetReleasePressElapsedMs() >= selectedStartDelayMs)
            {
                BeginOppositeTapOrSkip();

                // A single huge dt jump (e.g. a resumed-from-background report) could
                // satisfy both thresholds in the same tick; handle that immediately rather
                // than waiting for a further tick that may never suppress correctly.
                if (state == CounterMovementReleasePressState.OppositeTapActive &&
                    GetReleasePressElapsedMs() >= selectedTotalTapWindowMs)
                {
                    EndOppositeTap();
                }
            }
        }

        /// <summary>
        /// Begins the generated opposite press, unless the computed hold duration is zero
        /// (total window entirely consumed by the delay), in which case no key is pressed
        /// at all and the action ends cleanly by moving straight to Suppressed.
        /// </summary>
        private void BeginOppositeTapOrSkip()
        {
            if (actualOppositeHoldMs <= 0)
            {
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                releasePressStartTimestamp = 0;
                state = CounterMovementReleasePressState.Suppressed;
                return;
            }

            pulseOwnedComponents = pendingOppositeComponents;
            state = CounterMovementReleasePressState.OppositeTapActive;
        }

        private void EndOppositeTap()
        {
            explicitReleaseComponents |= pulseOwnedComponents;
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
                            explicitReleaseComponents |= opp;
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
                // Reverse into brake direction: hand the key over to the normal masked path
                // without an intervening release, so it stays continuously held.
                for (int i = 0; i < CardinalComponents.Length; i++)
                {
                    StickPadAction.DpadDirections c = CardinalComponents[i];
                    if (Has(rawCurrentDir, c) && Has(pulseOwnedComponents, c))
                    {
                        pulseOwnedComponents &= ~c;
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
                // Genuinely different direction (this also covers WaitingForOppositeTap,
                // where pulseOwnedComponents is still empty so touchesPulse can never be
                // true): abandon this activation entirely, release anything owned so far,
                // and start fresh tracking of the new push.
                explicitReleaseComponents |= pulseOwnedComponents;
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
            explicitReleaseComponents |= pulseOwnedComponents;
            pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
            pendingOppositeComponents = StickPadAction.DpadDirections.Centered;
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
