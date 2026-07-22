using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.StickActions;
using System;
using System.Diagnostics;

namespace DS4MapperTest.TouchpadActions
{
    public sealed class TouchpadReleaseBrake
    {
        public enum BrakeState
        {
            Idle,
            Tracking,
            Braking,
        }

        private const int MIN_START_DELAY_MS = 0;
        private const int MAX_START_DELAY_MS = DigitalReleaseBrakePulse.MAX_BRAKE_DURATION_MS;

        public const int DEFAULT_START_DELAY_MINIMUM_MS = 0;
        public const int DEFAULT_START_DELAY_MAXIMUM_MS = 0;

        public const int CS2_TAP_LENGTH_MINIMUM_MS = CounterMovementReleasePressProcessor.CS2_TAP_LENGTH_MINIMUM_MS;
        public const int CS2_TAP_LENGTH_MAXIMUM_MS = CounterMovementReleasePressProcessor.CS2_TAP_LENGTH_MAXIMUM_MS;

        private readonly IRandomRangeProvider randomProvider;

        public TouchpadReleaseBrake() : this(RandomRangeProvider.Instance)
        {
        }

        public TouchpadReleaseBrake(IRandomRangeProvider randomProvider)
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

        private CounterMovementTapLengthPreset tapLengthPreset = CounterMovementTapLengthPreset.CS2;
        public CounterMovementTapLengthPreset TapLengthPreset
        {
            get => tapLengthPreset;
            set => tapLengthPreset = value;
        }

        // All tap-length representation storage and computation (mode, Fixed, Percent,
        // Minimum, Maximum, the percentage/best-fit maths and the CS2 constants) lives in
        // this one shared object so it exists in exactly one place; CounterMovementReleasePressProcessor
        // composes the same type rather than duplicating any of it. See its class doc for
        // why synchronisation is never done inside a raw property setter.
        private readonly OppositeTapLengthTiming tapLengthTiming = new OppositeTapLengthTiming();

        public OppositeTapLengthMode OppositeTapLengthMode
        {
            get => tapLengthTiming.Mode;
            set => tapLengthTiming.Mode = value;
        }

        public int OppositeTapLengthMs
        {
            get => tapLengthTiming.FixedMs;
            set => tapLengthTiming.FixedMs = value;
        }

        public int OppositeTapLengthVariancePercent
        {
            get => tapLengthTiming.VariancePercent;
            set => tapLengthTiming.VariancePercent = value;
        }

        public int OppositeTapLengthMinimumMs
        {
            get => tapLengthTiming.MinimumMs;
            set => tapLengthTiming.MinimumMs = value;
        }

        public int OppositeTapLengthMaximumMs
        {
            get => tapLengthTiming.MaximumMs;
            set => tapLengthTiming.MaximumMs = value;
        }

        /// <summary>
        /// User-edit entry point for Fixed mode / Wait Variance Percentage mode. See
        /// OppositeTapLengthTiming.ApplyFixedAndPercentage. Only ever called from a
        /// ViewModel edit, CS2 preset application or profile migration - never from the
        /// per-report runtime path.
        /// </summary>
        public void ApplyFixedAndPercentage(int fixedMs, int percent) => tapLengthTiming.ApplyFixedAndPercentage(fixedMs, percent);

        /// <summary>
        /// User-edit entry point for Minimum and Maximum mode. See
        /// OppositeTapLengthTiming.ApplyMinimumAndMaximum. Only ever called from a
        /// ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyMinimumAndMaximum(int minimumMs, int maximumMs)
        {
            tapLengthTiming.ApplyMinimumAndMaximum(minimumMs, maximumMs);
            NormalizeRanges();
        }

        /// <summary>
        /// Returns the runtime effective Minimum/Maximum for the currently selected mode.
        /// This is the single, central place mode-aware timing logic lives: the state
        /// machine below must only ever consult this, never branch on the mode itself.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveOppositeTapLengthRange() => tapLengthTiming.GetEffectiveRange();

        public int BrakeDurationMs
        {
            get => OppositeTapLengthMaximumMs;
            set
            {
                ApplyMinimumAndMaximum(value, value);
                OppositeTapStartDelayMinimumMs = 0;
                OppositeTapStartDelayMaximumMs = 0;
                TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                NormalizeRanges();
            }
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

        private int minimumHoldMs = DigitalReleaseBrakePulse.DEFAULT_MINIMUM_HOLD_MS;
        public int MinimumHoldMs
        {
            get => minimumHoldMs;
            set => minimumHoldMs = DigitalReleaseBrakePulse.ClampMinimumHoldMs(value);
        }

        private BrakeState state = BrakeState.Idle;
        private bool controllingTouchActive;
        private uint activeComponents;
        private uint pulseOwnedComponents;
        private uint pendingOppositeComponents;
        private uint explicitReleaseComponents;
        private double holdUp, holdDown, holdLeft, holdRight;
        private int selectedTotalTapWindowMs;
        private int selectedStartDelayMs;
        private int actualOppositeHoldMs;
        private double releasePressElapsedSeconds;
        private long releasePressStartTimestamp;

        public BrakeState State => state;
        public bool HasActivePulse => pulseOwnedComponents != 0 || pendingOppositeComponents != 0;

        public bool MatchesCs2Values => tapLengthTiming.MatchesCs2Values;

        public CounterMovementTapLengthPreset EffectiveTapLengthPreset =>
            MatchesCs2Values ? CounterMovementTapLengthPreset.CS2 : CounterMovementTapLengthPreset.Custom;

        public void ApplyCs2Preset()
        {
            tapLengthTiming.ApplyCs2Preset();
            tapLengthPreset = CounterMovementTapLengthPreset.CS2;
        }

        public void NormalizeRanges()
        {
            if (OppositeTapLengthMinimumMs > OppositeTapLengthMaximumMs)
            {
                OppositeTapLengthMaximumMs = OppositeTapLengthMinimumMs;
            }

            if (oppositeTapStartDelayMinimumMs > oppositeTapStartDelayMaximumMs)
            {
                oppositeTapStartDelayMaximumMs = oppositeTapStartDelayMinimumMs;
            }

            if (oppositeTapStartDelayMaximumMs > OppositeTapLengthMinimumMs)
            {
                oppositeTapStartDelayMaximumMs = OppositeTapLengthMinimumMs;
            }

            if (oppositeTapStartDelayMinimumMs > oppositeTapStartDelayMaximumMs)
            {
                oppositeTapStartDelayMinimumMs = oppositeTapStartDelayMaximumMs;
            }
        }

        public TouchpadActionPad.DpadDirections Prepare(TouchEventFrame touchFrame,
            TouchpadActionPad.DpadDirections rawCurrentDir)
        {
            if (!enabled)
            {
                if (state != BrakeState.Idle || pulseOwnedComponents != 0)
                {
                    ForceReleaseAndReset();
                }
                return rawCurrentDir;
            }

            double dt = touchFrame.timeElapsed > 0.0 ? touchFrame.timeElapsed : 0.0;
            bool touchActive = touchFrame.Touch;

            if (touchActive)
            {
                uint rawMask = ToMask(rawCurrentDir);
                TransferPulseToRealInput(rawMask);

                uint releasedComponents = controllingTouchActive ? activeComponents & ~rawMask : 0;
                controllingTouchActive = true;
                TryStartPulse(releasedComponents);
                AccumulateHold(rawMask, dt);
                activeComponents = rawMask;
                Advance(dt);
                state = pulseOwnedComponents != 0 || pendingOppositeComponents != 0 ?
                    BrakeState.Braking : BrakeState.Tracking;
                return rawCurrentDir;
            }

            if (controllingTouchActive)
            {
                TryStartPulse(activeComponents);
                controllingTouchActive = false;
                activeComponents = 0;
                holdUp = holdDown = holdLeft = holdRight = 0.0;
            }
            else
            {
                Advance(dt);
            }

            if (pulseOwnedComponents != 0 || pendingOppositeComponents != 0)
            {
                state = BrakeState.Braking;
            }
            else if (state != BrakeState.Braking)
            {
                state = BrakeState.Idle;
            }

            return TouchpadActionPad.DpadDirections.Centered;
        }

        public void Event(Mapper mapper, ButtonAction[] usedFuncList)
        {
            FlushReleases(mapper, usedFuncList);

            EmitPulse(mapper, usedFuncList);
        }

        public void FlushPendingReleases(Mapper mapper, ButtonAction[] usedFuncList)
        {
            FlushReleases(mapper, usedFuncList);
        }

        public void EmitPulse(Mapper mapper, ButtonAction[] usedFuncList)
        {
            if (usedFuncList == null || pulseOwnedComponents == 0)
            {
                return;
            }

            foreach (uint component in DigitalReleaseBrakePulse.CardinalComponents)
            {
                if (!DigitalReleaseBrakePulse.Has(pulseOwnedComponents, component))
                {
                    continue;
                }

                int index = (int)component;
                if (index >= 0 && index < usedFuncList.Length)
                {
                    ButtonAction data = usedFuncList[index];
                    if (data != null)
                    {
                        data.Prepare(mapper, true);
                        data.Event(mapper);
                    }
                }
            }
        }

        public void Advance(double dtSeconds)
        {
            if (pulseOwnedComponents == 0 && pendingOppositeComponents == 0)
            {
                return;
            }

            if (dtSeconds > 0.0)
            {
                releasePressElapsedSeconds += dtSeconds;
            }

            double elapsedMs = GetReleasePressElapsedMs();
            if (pendingOppositeComponents != 0 && elapsedMs >= selectedStartDelayMs)
            {
                BeginOppositeTapOrSkip();

                if (pulseOwnedComponents != 0 && GetReleasePressElapsedMs() >= selectedTotalTapWindowMs)
                {
                    EndOppositeTap();
                }
            }

            if (pulseOwnedComponents != 0 && elapsedMs >= selectedTotalTapWindowMs)
            {
                EndOppositeTap();
            }

            if (pulseOwnedComponents != 0 || pendingOppositeComponents != 0)
            {
                state = BrakeState.Braking;
            }
            else
            {
                state = controllingTouchActive ? BrakeState.Tracking : BrakeState.Idle;
            }
        }

        public void Cleanup(Mapper mapper, ButtonAction[] usedFuncList)
        {
            ForceReleaseAndReset();
            FlushReleases(mapper, usedFuncList);
        }

        private void FlushReleases(Mapper mapper, ButtonAction[] usedFuncList)
        {
            if (usedFuncList == null || explicitReleaseComponents == 0)
            {
                return;
            }

            foreach (uint component in DigitalReleaseBrakePulse.CardinalComponents)
            {
                if (!DigitalReleaseBrakePulse.Has(explicitReleaseComponents, component))
                {
                    continue;
                }

                int index = (int)component;
                if (index >= 0 && index < usedFuncList.Length)
                {
                    ButtonAction data = usedFuncList[index];
                    if (data != null)
                    {
                        data.Prepare(mapper, false);
                        data.Event(mapper);
                        data.Release(mapper, ignoreReleaseActions: true);
                    }
                }
            }

            explicitReleaseComponents = 0;
        }

        private void AccumulateHold(uint rawMask, double dt)
        {
            holdUp = DigitalReleaseBrakePulse.Has(rawMask, DigitalReleaseBrakePulse.UP) ? holdUp + dt : 0.0;
            holdDown = DigitalReleaseBrakePulse.Has(rawMask, DigitalReleaseBrakePulse.DOWN) ? holdDown + dt : 0.0;
            holdLeft = DigitalReleaseBrakePulse.Has(rawMask, DigitalReleaseBrakePulse.LEFT) ? holdLeft + dt : 0.0;
            holdRight = DigitalReleaseBrakePulse.Has(rawMask, DigitalReleaseBrakePulse.RIGHT) ? holdRight + dt : 0.0;
        }

        private double GetHold(uint component)
        {
            if (component == DigitalReleaseBrakePulse.UP) return holdUp;
            if (component == DigitalReleaseBrakePulse.DOWN) return holdDown;
            if (component == DigitalReleaseBrakePulse.LEFT) return holdLeft;
            if (component == DigitalReleaseBrakePulse.RIGHT) return holdRight;
            return 0.0;
        }

        private void TryStartPulse(uint releasedComponents)
        {
            if (releasedComponents == 0)
            {
                return;
            }

            double minHoldSeconds = minimumHoldMs / 1000.0;
            uint eligible = 0;
            foreach (uint component in DigitalReleaseBrakePulse.CardinalComponents)
            {
                if (DigitalReleaseBrakePulse.Has(releasedComponents, component) &&
                    GetHold(component) + double.Epsilon >= minHoldSeconds)
                {
                    eligible |= component;
                }
            }

            if (eligible == 0)
            {
                return;
            }

            explicitReleaseComponents |= pulseOwnedComponents;
            pulseOwnedComponents = 0;

            NormalizeRanges();
            if (OppositeTapLengthMode == OppositeTapLengthMode.Fixed)
            {
                // Fixed mode is deterministic: every qualifying activation uses exactly the
                // fixed duration, so the random provider is never consulted for it at all.
                selectedTotalTapWindowMs = OppositeTapLengthMs;
            }
            else
            {
                (int effectiveMinimumMs, int effectiveMaximumMs) = GetEffectiveOppositeTapLengthRange();
                selectedTotalTapWindowMs = randomProvider.NextInclusive(effectiveMinimumMs, effectiveMaximumMs);
            }

            selectedStartDelayMs = randomProvider.NextInclusive(
                oppositeTapStartDelayMinimumMs, oppositeTapStartDelayMaximumMs);
            actualOppositeHoldMs = Math.Max(0, selectedTotalTapWindowMs - selectedStartDelayMs);
            pendingOppositeComponents = DigitalReleaseBrakePulse.OppositeMask(eligible);
            releasePressElapsedSeconds = 0.0;
            releasePressStartTimestamp = Stopwatch.GetTimestamp();

            if (selectedStartDelayMs <= 0)
            {
                BeginOppositeTapOrSkip();
            }
            else
            {
                state = BrakeState.Braking;
            }
        }

        private void BeginOppositeTapOrSkip()
        {
            if (actualOppositeHoldMs <= 0)
            {
                pendingOppositeComponents = 0;
                releasePressStartTimestamp = 0;
                state = controllingTouchActive ? BrakeState.Tracking : BrakeState.Idle;
                return;
            }

            pulseOwnedComponents = pendingOppositeComponents;
            pendingOppositeComponents = 0;
            state = BrakeState.Braking;
        }

        private void EndOppositeTap()
        {
            explicitReleaseComponents |= pulseOwnedComponents;
            pulseOwnedComponents = 0;
            releasePressStartTimestamp = 0;
            state = controllingTouchActive ? BrakeState.Tracking : BrakeState.Idle;
        }

        private void TransferPulseToRealInput(uint rawMask)
        {
            uint transferred = pulseOwnedComponents & rawMask;
            pulseOwnedComponents &= ~transferred;
            pendingOppositeComponents &= ~transferred;
            if (pulseOwnedComponents == 0 && pendingOppositeComponents == 0)
            {
                releasePressStartTimestamp = 0;
            }
        }

        private void ForceReleaseAndReset()
        {
            explicitReleaseComponents |= pulseOwnedComponents;
            pulseOwnedComponents = 0;
            pendingOppositeComponents = 0;
            controllingTouchActive = false;
            activeComponents = 0;
            holdUp = holdDown = holdLeft = holdRight = 0.0;
            selectedTotalTapWindowMs = 0;
            selectedStartDelayMs = 0;
            actualOppositeHoldMs = 0;
            releasePressElapsedSeconds = 0.0;
            releasePressStartTimestamp = 0;
            state = BrakeState.Idle;
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

        private static uint ToMask(TouchpadActionPad.DpadDirections directions)
        {
            return (uint)directions;
        }
    }
}
