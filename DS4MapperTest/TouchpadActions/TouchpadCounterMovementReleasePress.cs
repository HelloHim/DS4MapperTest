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
            CounterPressActive,
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

        private CounterMovementPressLengthPreset pressLengthPreset = CounterMovementPressLengthPreset.CS2;
        public CounterMovementPressLengthPreset PressLengthPreset
        {
            get => pressLengthPreset;
            set => pressLengthPreset = value;
        }

        // All press-length representation storage and computation (mode, Fixed, Percent,
        // Minimum, Maximum, the percentage/best-fit maths and the CS2 constants) lives in
        // this one shared object so it exists in exactly one place; CounterMovementReleasePressProcessor
        // composes the same type rather than duplicating any of it. See its class doc for
        // why synchronisation is never done inside a raw property setter.
        private readonly CounterPressLengthTiming pressLengthTiming = new CounterPressLengthTiming();

        public CounterPressLengthMode CounterPressLengthMode
        {
            get => pressLengthTiming.Mode;
            set => pressLengthTiming.Mode = value;
        }

        public int CounterPressLengthMs
        {
            get => pressLengthTiming.FixedMs;
            set => pressLengthTiming.FixedMs = value;
        }

        public int CounterPressLengthVariancePercent
        {
            get => pressLengthTiming.VariancePercent;
            set => pressLengthTiming.VariancePercent = value;
        }

        public int CounterPressLengthMinimumMs
        {
            get => pressLengthTiming.MinimumMs;
            set => pressLengthTiming.MinimumMs = value;
        }

        public int CounterPressLengthMaximumMs
        {
            get => pressLengthTiming.MaximumMs;
            set => pressLengthTiming.MaximumMs = value;
        }

        /// <summary>
        /// User-edit entry point for Fixed mode / Wait Variance Percentage mode. See
        /// CounterPressLengthTiming.ApplyFixedAndPercentage. Only ever called from a
        /// ViewModel edit, CS2 preset application or profile migration - never from the
        /// per-report runtime path.
        /// </summary>
        public void ApplyFixedAndPercentage(int fixedMs, int percent) => pressLengthTiming.ApplyFixedAndPercentage(fixedMs, percent);

        /// <summary>
        /// User-edit entry point for Minimum and Maximum mode. See
        /// CounterPressLengthTiming.ApplyMinimumAndMaximum. Only ever called from a
        /// ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyMinimumAndMaximum(int minimumMs, int maximumMs)
        {
            pressLengthTiming.ApplyMinimumAndMaximum(minimumMs, maximumMs);
            NormalizeRanges();
        }

        /// <summary>
        /// Returns the runtime effective Minimum/Maximum for the currently selected mode.
        /// This is the single, central place mode-aware timing logic lives: the state
        /// machine below must only ever consult this, never branch on the mode itself.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveCounterPressLengthRange() => pressLengthTiming.GetEffectiveRange();

        public int ReleasePressDurationMs
        {
            get => CounterPressLengthMaximumMs;
            set
            {
                ApplyMinimumAndMaximum(value, value);
                CounterPressStartDelayMinimumMs = 0;
                CounterPressStartDelayMaximumMs = 0;
                PressLengthPreset = CounterMovementPressLengthPreset.Custom;
                NormalizeRanges();
            }
        }

        // All start-delay representation storage and computation (mode, Fixed, Percent,
        // Minimum, Maximum, the percentage/best-fit maths) lives in this one shared object,
        // mirroring pressLengthTiming above; CounterMovementReleasePressProcessor composes the
        // same type rather than duplicating any of it.
        private readonly CounterPressStartDelayTiming startDelayTiming = new CounterPressStartDelayTiming();

        public CounterPressStartDelayMode CounterPressStartDelayMode
        {
            get => startDelayTiming.Mode;
            set => startDelayTiming.Mode = value;
        }

        public int CounterPressStartDelayMs
        {
            get => startDelayTiming.FixedMs;
            set => startDelayTiming.FixedMs = value;
        }

        public int CounterPressStartDelayVariancePercent
        {
            get => startDelayTiming.VariancePercent;
            set => startDelayTiming.VariancePercent = value;
        }

        public int CounterPressStartDelayMinimumMs
        {
            get => startDelayTiming.MinimumMs;
            set => startDelayTiming.MinimumMs = value;
        }

        public int CounterPressStartDelayMaximumMs
        {
            get => startDelayTiming.MaximumMs;
            set => startDelayTiming.MaximumMs = value;
        }

        /// <summary>
        /// User-edit entry point for Fixed mode / Wait Variance Percentage mode for the start
        /// delay. See CounterPressStartDelayTiming.ApplyFixedAndPercentage. Only ever called
        /// from a ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyStartDelayFixedAndPercentage(int fixedMs, int percent) => startDelayTiming.ApplyFixedAndPercentage(fixedMs, percent);

        /// <summary>
        /// User-edit entry point for Minimum and Maximum mode for the start delay. See
        /// CounterPressStartDelayTiming.ApplyMinimumAndMaximum. Only ever called from a
        /// ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyStartDelayMinimumAndMaximum(int minimumMs, int maximumMs)
        {
            startDelayTiming.ApplyMinimumAndMaximum(minimumMs, maximumMs);
            NormalizeRanges();
        }

        /// <summary>
        /// Returns the runtime effective Minimum/Maximum for the currently selected start
        /// delay mode. See GetEffectiveCounterPressLengthRange's class doc: the state machine
        /// below must only ever consult this, never branch on the mode itself.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveCounterPressStartDelayRange() => startDelayTiming.GetEffectiveRange();

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
        private int selectedTotalPressWindowMs;
        private int selectedStartDelayMs;
        private int actualOppositeHoldMs;
        private double releasePressElapsedSeconds;
        private long releasePressStartTimestamp;

        public PressState State => state;
        public bool HasActivePulse => pulseOwnedComponents != 0 || pendingOppositeComponents != 0;

        public bool MatchesCs2Values => pressLengthTiming.MatchesCs2Values;

        public CounterMovementPressLengthPreset EffectivePressLengthPreset =>
            MatchesCs2Values ? CounterMovementPressLengthPreset.CS2 : CounterMovementPressLengthPreset.Custom;

        public void ApplyCs2Preset()
        {
            pressLengthTiming.ApplyCs2Preset();
            pressLengthPreset = CounterMovementPressLengthPreset.CS2;
        }

        public void NormalizeRanges()
        {
            if (CounterPressLengthMinimumMs > CounterPressLengthMaximumMs)
            {
                CounterPressLengthMaximumMs = CounterPressLengthMinimumMs;
            }

            if (CounterPressStartDelayMinimumMs > CounterPressStartDelayMaximumMs)
            {
                CounterPressStartDelayMaximumMs = CounterPressStartDelayMinimumMs;
            }

            if (CounterPressStartDelayMaximumMs > CounterPressLengthMinimumMs)
            {
                CounterPressStartDelayMaximumMs = CounterPressLengthMinimumMs;
            }

            if (CounterPressStartDelayMinimumMs > CounterPressStartDelayMaximumMs)
            {
                CounterPressStartDelayMinimumMs = CounterPressStartDelayMaximumMs;
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
                    PressState.CounterPressActive : PressState.Tracking;
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
                state = PressState.CounterPressActive;
            }
            else if (state != PressState.CounterPressActive)
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
                BeginCounterPressOrSkip();

                if (pulseOwnedComponents != 0 && GetReleasePressElapsedMs() >= selectedTotalPressWindowMs)
                {
                    EndCounterPress();
                }
            }

            if (pulseOwnedComponents != 0 && elapsedMs >= selectedTotalPressWindowMs)
            {
                EndCounterPress();
            }

            if (pulseOwnedComponents != 0 || pendingOppositeComponents != 0)
            {
                state = PressState.CounterPressActive;
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
            if (CounterPressLengthMode == CounterPressLengthMode.Fixed)
            {
                // Fixed mode is deterministic: every qualifying activation uses exactly the
                // fixed duration, so the random provider is never consulted for it at all.
                selectedTotalPressWindowMs = CounterPressLengthMs;
            }
            else
            {
                (int effectiveMinimumMs, int effectiveMaximumMs) = GetEffectiveCounterPressLengthRange();
                selectedTotalPressWindowMs = randomProvider.NextInclusive(effectiveMinimumMs, effectiveMaximumMs);
            }

            if (CounterPressStartDelayMode == CounterPressStartDelayMode.Fixed)
            {
                // Fixed mode is deterministic: every qualifying activation uses exactly the
                // fixed delay, so the random provider is never consulted for it at all.
                selectedStartDelayMs = CounterPressStartDelayMs;
            }
            else
            {
                (int effectiveStartDelayMinimumMs, int effectiveStartDelayMaximumMs) = GetEffectiveCounterPressStartDelayRange();
                selectedStartDelayMs = randomProvider.NextInclusive(effectiveStartDelayMinimumMs, effectiveStartDelayMaximumMs);
            }
            actualOppositeHoldMs = Math.Max(0, selectedTotalPressWindowMs - selectedStartDelayMs);
            pendingOppositeComponents = DigitalReleasePressPulse.OppositeMask(eligible);
            releasePressElapsedSeconds = 0.0;
            releasePressStartTimestamp = Stopwatch.GetTimestamp();

            if (selectedStartDelayMs <= 0)
            {
                BeginCounterPressOrSkip();
            }
            else
            {
                state = PressState.CounterPressActive;
            }
        }

        private void BeginCounterPressOrSkip()
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
            state = PressState.CounterPressActive;
        }

        private void EndCounterPress()
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
            selectedTotalPressWindowMs = 0;
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
