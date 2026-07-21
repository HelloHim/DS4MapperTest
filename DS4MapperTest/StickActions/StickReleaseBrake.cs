using System;
using System.Diagnostics;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;

namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Optional per-action "Digital Release Brake" for StickPadAction D-Pad modes.
    /// When a spring-loaded stick is released, the mechanical spring can take tens of
    /// milliseconds to return to centre. During that window this detector suppresses the
    /// stick's own returning digital direction and briefly pulses the logically opposite
    /// direction, using whichever AxisDirButton bindings the action already owns.
    /// Detection is derived solely from analogue stick X/Y position; no other sensing is used.
    /// </summary>
    public class StickReleaseBrake
    {
        public enum BrakeState
        {
            Unprimed,
            Idle,
            Armed,
            Braking,
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

        private int brakeDurationMs = 100;
        public int BrakeDurationMs
        {
            get => brakeDurationMs;
            set => brakeDurationMs = DigitalReleaseBrakePulse.ClampBrakeDurationMs(value);
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

        private BrakeState state = BrakeState.Unprimed;
        public BrakeState State => state;

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
        private StickPadAction.DpadDirections explicitReleaseComponents;
        // Accumulated from validated report dt (genuine elapsed time, never a poll count),
        // so pulse duration tracks real elapsed time regardless of report cadence and is
        // immune to hitches (only validated samples advance it). A monotonic wall-clock
        // fallback bounds the pulse if the reader reports invalid dt for a few frames.
        private double pulseElapsedSeconds;
        private long pulseStartTimestamp;

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
                if (state != BrakeState.Unprimed || suppressedComponents != StickPadAction.DpadDirections.Centered ||
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
                case BrakeState.Unprimed:
                    if (rFiltered <= RESET_THRESHOLD)
                    {
                        state = BrakeState.Idle;
                    }
                    break;

                case BrakeState.Idle:
                    if (rawCurrentDir != StickPadAction.DpadDirections.Centered &&
                        (armingThreshold <= 0.0 || rFiltered >= armingThreshold))
                    {
                        state = BrakeState.Armed;
                        peakRadius = rFiltered;
                        latchedZone = rawCurrentDir;
                        hasFiredThisCycle = false;
                        holdUp = holdDown = holdLeft = holdRight = 0.0;
                        AccumulateHold(rawCurrentDir, dtValid ? dt : 0.0);
                    }
                    break;

                case BrakeState.Armed:
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
                        EnterBraking(ReleaseTriggerReason.Derivative);
                    }
                    else if (slowFallback)
                    {
                        EnterBraking(ReleaseTriggerReason.SlowReleaseFallback);
                    }
                    break;

                case BrakeState.Braking:
                    effectiveDir = ApplySuppression(rawCurrentDir);
                    CheckReengagement(rawCurrentDir, dr, dtValid, ref effectiveDir);
                    if (state == BrakeState.Braking)
                    {
                        if (dtValid) pulseElapsedSeconds += dt;
                        if (GetPulseElapsedSeconds() * 1000.0 >= brakeDurationMs)
                        {
                            explicitReleaseComponents |= pulseOwnedComponents;
                            pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                            pulseStartTimestamp = 0;
                            state = BrakeState.Suppressed;
                        }
                    }
                    break;

                case BrakeState.Suppressed:
                    effectiveDir = ApplySuppression(rawCurrentDir);
                    CheckReengagement(rawCurrentDir, dr, dtValid, ref effectiveDir);
                    if (state == BrakeState.Suppressed && rFiltered <= RESET_THRESHOLD)
                    {
                        suppressedComponents = StickPadAction.DpadDirections.Centered;
                        latchedZone = StickPadAction.DpadDirections.Centered;
                        holdUp = holdDown = holdLeft = holdRight = 0.0;
                        state = BrakeState.Idle;
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
        /// Called from StickPadAction.Release/SoftRelease. Releases only brake-owned output,
        /// clears all state, and returns to Unprimed so a subsequent controller connect/profile
        /// load/layer switch cannot cause a spurious brake.
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

        private void EnterBraking(ReleaseTriggerReason reason)
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
                // Nothing qualified for a pulse. Still consume the single trigger for this
                // movement cycle and wait out neutral before arming again.
                suppressedComponents = StickPadAction.DpadDirections.Centered;
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                state = BrakeState.Suppressed;
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

            pulseOwnedComponents = opposite;
            pulseElapsedSeconds = 0.0;
            pulseStartTimestamp = Stopwatch.GetTimestamp();
            postTriggerMinR = rFiltered;
            risingTickCount = 0;
            state = BrakeState.Braking;
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
                                pulseStartTimestamp = 0;
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
                    pulseStartTimestamp = 0;
                }
                lastTriggerReason = ReleaseTriggerReason.NewInputCancellation;
                effectiveDir = ApplySuppression(rawCurrentDir);
            }
            else
            {
                // Genuinely different direction. Abandon this brake cycle entirely and
                // start fresh tracking of the new push.
                explicitReleaseComponents |= pulseOwnedComponents;
                pulseOwnedComponents = StickPadAction.DpadDirections.Centered;
                pulseStartTimestamp = 0;
                suppressedComponents = StickPadAction.DpadDirections.Centered;
                lastTriggerReason = ReleaseTriggerReason.NewInputCancellation;
                effectiveDir = rawCurrentDir;
                ResumeArmedTracking(rawCurrentDir);
            }
        }

        private void ResumeArmedTracking(StickPadAction.DpadDirections rawCurrentDir)
        {
            state = BrakeState.Armed;
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

        private double GetPulseElapsedSeconds()
        {
            if (pulseStartTimestamp == 0)
            {
                return pulseElapsedSeconds;
            }

            double wallElapsedSeconds = (Stopwatch.GetTimestamp() - pulseStartTimestamp) /
                (double)Stopwatch.Frequency;
            return Math.Max(pulseElapsedSeconds, wallElapsedSeconds);
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
            suppressedComponents = StickPadAction.DpadDirections.Centered;
            latchedZone = StickPadAction.DpadDirections.Centered;
            holdUp = holdDown = holdLeft = holdRight = 0.0;
            hasFiredThisCycle = false;
            pulseElapsedSeconds = 0.0;
            pulseStartTimestamp = 0;
            state = BrakeState.Unprimed;
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
                "[ReleaseBrake] t={0} dt={1:F5} valid={2} r={3:F3} rF={4:F3} dr={5:F2} state={6} " +
                "raw={7} eff={8} latched={9} peak={10:F3} suppressed={11} pulseOwned={12} reason={13}",
                Environment.TickCount64, dt, dtValid, rPhysical, rFiltered, dr, state,
                rawDir, effectiveDir, latchedZone, peakRadius, suppressedComponents, pulseOwnedComponents,
                lastTriggerReason));
        }
    }
}
