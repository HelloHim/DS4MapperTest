using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.StickActions;
using System;
using System.Diagnostics;

namespace DS4MapperTest.TouchpadActions
{
    public sealed class TouchpadCounterMovementReleasePress
    {
        public enum PressState
        {
            Idle,
            Tracking,
            CounterTapActive,
        }

        public const int CS2_TAP_LENGTH_MINIMUM_MS = CounterMovementReleasePressProcessor.CS2_TAP_LENGTH_MINIMUM_MS;
        public const int CS2_TAP_LENGTH_MAXIMUM_MS = CounterMovementReleasePressProcessor.CS2_TAP_LENGTH_MAXIMUM_MS;

        private readonly IRandomRangeProvider randomProvider;

        public TouchpadCounterMovementReleasePress() : this(RandomRangeProvider.Instance)
        {
        }

        public TouchpadCounterMovementReleasePress(IRandomRangeProvider randomProvider)
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

        private bool triggerOnDeadZoneReleaseEnabled = true;
        public bool TriggerOnDeadZoneReleaseEnabled
        {
            get => triggerOnDeadZoneReleaseEnabled;
            set => triggerOnDeadZoneReleaseEnabled = value;
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
        /// This is the single, central place mode-aware timing logic lives: the state
        /// machine below must only ever consult this, never branch on the mode itself.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveCounterTapLengthRange() => tapLengthTiming.GetEffectiveRange();

        public int ReleasePressDurationMs
        {
            get => CounterTapLengthMaximumMs;
            set
            {
                ApplyMinimumAndMaximum(value, value);
                CounterTapStartDelayMinimumMs = 0;
                CounterTapStartDelayMaximumMs = 0;
                TapLengthPreset = CounterMovementTapLengthPreset.Custom;
                NormalizeRanges();
            }
        }

        // All start-delay representation storage and computation (mode, Fixed, Percent,
        // Minimum, Maximum, the percentage/best-fit maths) lives in this one shared object,
        // mirroring tapLengthTiming above; CounterMovementReleasePressProcessor composes the
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

        private int minimumHoldMs = DigitalReleasePressPulse.DEFAULT_MINIMUM_HOLD_MS;
        public int MinimumHoldMs
        {
            get => minimumHoldMs;
            set => minimumHoldMs = DigitalReleasePressPulse.ClampMinimumHoldMs(value);
        }

        private PressState state = PressState.Idle;
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

        public PressState State => state;
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
            if (CounterTapLengthMinimumMs > CounterTapLengthMaximumMs)
            {
                CounterTapLengthMaximumMs = CounterTapLengthMinimumMs;
            }

            if (CounterTapStartDelayMinimumMs > CounterTapStartDelayMaximumMs)
            {
                CounterTapStartDelayMaximumMs = CounterTapStartDelayMinimumMs;
            }

            if (CounterTapStartDelayMaximumMs > CounterTapLengthMinimumMs)
            {
                CounterTapStartDelayMaximumMs = CounterTapLengthMinimumMs;
            }

            if (CounterTapStartDelayMinimumMs > CounterTapStartDelayMaximumMs)
            {
                CounterTapStartDelayMinimumMs = CounterTapStartDelayMaximumMs;
            }
        }

        public TouchpadActionPad.DpadDirections Prepare(TouchEventFrame touchFrame,
            TouchpadActionPad.DpadDirections rawCurrentDir)
        {
            if (!enabled)
            {
                if (state != PressState.Idle || pulseOwnedComponents != 0)
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
                bool freshTouch = !controllingTouchActive;
                TransferPulseToRealInput(rawMask);

                // A pulse only ever starts from an actual lift (below), or, when Deadzone
                // Release Press is enabled, from crossing back into the dead zone without
                // lifting - never from sliding between two non-centre zones. If a genuinely
                // new touch begins (rather than the finger merely moving within one continuous
                // touch) and a pulse from a prior lift is still outstanding and does not match
                // this touch's own direction (TransferPulseToRealInput above already absorbed
                // the matching case), that stale pulse is abandoned rather than left to linger
                // into unrelated new input.
                if (freshTouch && (pulseOwnedComponents != 0 || pendingOppositeComponents != 0))
                {
                    explicitReleaseComponents |= pulseOwnedComponents;
                    pulseOwnedComponents = 0;
                    pendingOppositeComponents = 0;
                    releasePressStartTimestamp = 0;
                }

                // Deadzone Release Press: treat crossing back to centre while still touching
                // the same as a lift, using whatever zone was active immediately beforehand.
                // Must run before AccumulateHold/activeComponents below overwrite that state.
                if (!freshTouch && rawMask == 0 && triggerOnDeadZoneReleaseEnabled)
                {
                    TryStartPulse(activeComponents);
                }

                controllingTouchActive = true;
                AccumulateHold(rawMask, dt);
                activeComponents = rawMask;
                Advance(dt);
                state = pulseOwnedComponents != 0 || pendingOppositeComponents != 0 ?
                    PressState.CounterTapActive : PressState.Tracking;
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
                state = PressState.CounterTapActive;
            }
            else if (state != PressState.CounterTapActive)
            {
                state = PressState.Idle;
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

            foreach (uint component in DigitalReleasePressPulse.CardinalComponents)
            {
                if (!DigitalReleasePressPulse.Has(pulseOwnedComponents, component))
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
                BeginCounterTapOrSkip();

                if (pulseOwnedComponents != 0 && GetReleasePressElapsedMs() >= selectedTotalTapWindowMs)
                {
                    EndCounterTap();
                }
            }

            if (pulseOwnedComponents != 0 && elapsedMs >= selectedTotalTapWindowMs)
            {
                EndCounterTap();
            }

            if (pulseOwnedComponents != 0 || pendingOppositeComponents != 0)
            {
                state = PressState.CounterTapActive;
            }
            else
            {
                state = controllingTouchActive ? PressState.Tracking : PressState.Idle;
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

            foreach (uint component in DigitalReleasePressPulse.CardinalComponents)
            {
                if (!DigitalReleasePressPulse.Has(explicitReleaseComponents, component))
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
            holdUp = DigitalReleasePressPulse.Has(rawMask, DigitalReleasePressPulse.UP) ? holdUp + dt : 0.0;
            holdDown = DigitalReleasePressPulse.Has(rawMask, DigitalReleasePressPulse.DOWN) ? holdDown + dt : 0.0;
            holdLeft = DigitalReleasePressPulse.Has(rawMask, DigitalReleasePressPulse.LEFT) ? holdLeft + dt : 0.0;
            holdRight = DigitalReleasePressPulse.Has(rawMask, DigitalReleasePressPulse.RIGHT) ? holdRight + dt : 0.0;
        }

        private double GetHold(uint component)
        {
            if (component == DigitalReleasePressPulse.UP) return holdUp;
            if (component == DigitalReleasePressPulse.DOWN) return holdDown;
            if (component == DigitalReleasePressPulse.LEFT) return holdLeft;
            if (component == DigitalReleasePressPulse.RIGHT) return holdRight;
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
            foreach (uint component in DigitalReleasePressPulse.CardinalComponents)
            {
                if (DigitalReleasePressPulse.Has(releasedComponents, component) &&
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
            actualOppositeHoldMs = Math.Max(0, selectedTotalTapWindowMs - selectedStartDelayMs);
            pendingOppositeComponents = DigitalReleasePressPulse.OppositeMask(eligible);
            releasePressElapsedSeconds = 0.0;
            releasePressStartTimestamp = Stopwatch.GetTimestamp();

            if (selectedStartDelayMs <= 0)
            {
                BeginCounterTapOrSkip();
            }
            else
            {
                state = PressState.CounterTapActive;
            }
        }

        private void BeginCounterTapOrSkip()
        {
            if (actualOppositeHoldMs <= 0)
            {
                pendingOppositeComponents = 0;
                releasePressStartTimestamp = 0;
                state = controllingTouchActive ? PressState.Tracking : PressState.Idle;
                return;
            }

            pulseOwnedComponents = pendingOppositeComponents;
            pendingOppositeComponents = 0;
            state = PressState.CounterTapActive;
        }

        private void EndCounterTap()
        {
            explicitReleaseComponents |= pulseOwnedComponents;
            pulseOwnedComponents = 0;
            releasePressStartTimestamp = 0;
            state = controllingTouchActive ? PressState.Tracking : PressState.Idle;
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
            state = PressState.Idle;
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
