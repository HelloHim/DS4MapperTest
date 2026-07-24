using System;
using DS4MapperTest.ActionUtil;

namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Shared, mode-aware storage and computation for Counter Movement Release Press'
    /// Opposite Tap Start Delay. Used by both CounterMovementReleasePressProcessor (stick
    /// D-Pad modes and Analog Emulation) and TouchpadReleaseBrake (touchpad D-Pad modes), so
    /// the percentage math and best-fit conversion search exist in exactly one place rather
    /// than being duplicated across the two otherwise-independent state machines. Mirrors
    /// OppositeTapLengthTiming's shape, but the start delay's own bounds start at 0 (a delay
    /// of zero is the common case) rather than the tap length's 10ms floor, and new actions
    /// default to Minimum/Maximum mode at 0/0ms - the setting's only representation before
    /// Fixed/Percentage modes existed - rather than a percentage-based preset.
    /// </summary>
    public sealed class OppositeTapStartDelayTiming
    {
        public const int MIN_START_DELAY_MS = 0;
        public const int MAX_START_DELAY_MS = DigitalReleaseBrakePulse.MAX_BRAKE_DURATION_MS;

        public const int MIN_START_DELAY_VARIANCE_PERCENT = 0;
        public const int MAX_START_DELAY_VARIANCE_PERCENT = 100;

        public const int DEFAULT_START_DELAY_FIXED_MS = 0;
        public const int DEFAULT_START_DELAY_VARIANCE_PERCENT = 0;
        public const int DEFAULT_START_DELAY_MINIMUM_MS = 0;
        public const int DEFAULT_START_DELAY_MAXIMUM_MS = 0;

        // New actions default to Fixed at 0ms, preserving immediate-start behaviour while
        // making the simplest representation the initially selected UI mode.
        private OppositeTapStartDelayMode mode = OppositeTapStartDelayMode.Fixed;
        public OppositeTapStartDelayMode Mode
        {
            get => mode;
            set => mode = value;
        }

        // All four numeric fields below (Fixed, Percent, Minimum, Maximum) are plain,
        // side-effect-free storage: clamp and store, nothing else. See
        // OppositeTapLengthTiming's class doc for why synchronisation is never done inside a
        // raw property setter - the same reasoning applies here.
        private int fixedMs = DEFAULT_START_DELAY_FIXED_MS;
        public int FixedMs
        {
            get => fixedMs;
            set => fixedMs = Math.Clamp(value, MIN_START_DELAY_MS, MAX_START_DELAY_MS);
        }

        private int variancePercent = DEFAULT_START_DELAY_VARIANCE_PERCENT;
        public int VariancePercent
        {
            get => variancePercent;
            set => variancePercent = Math.Clamp(value, MIN_START_DELAY_VARIANCE_PERCENT, MAX_START_DELAY_VARIANCE_PERCENT);
        }

        private int minimumMs = DEFAULT_START_DELAY_MINIMUM_MS;
        public int MinimumMs
        {
            get => minimumMs;
            set => minimumMs = Math.Clamp(value, MIN_START_DELAY_MS, MAX_START_DELAY_MS);
        }

        private int maximumMs = DEFAULT_START_DELAY_MAXIMUM_MS;
        public int MaximumMs
        {
            get => maximumMs;
            set => maximumMs = Math.Clamp(value, MIN_START_DELAY_MS, MAX_START_DELAY_MS);
        }

        /// <summary>
        /// User-edit entry point for Fixed mode / Wait Variance Percentage mode: stores the
        /// given fixed delay and percentage, then recomputes and stores the synchronised
        /// Minimum/Maximum from them. Only ever called from a ViewModel edit or profile
        /// migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyFixedAndPercentage(int fixedMsValue, int percent)
        {
            FixedMs = fixedMsValue;
            VariancePercent = percent;
            (minimumMs, maximumMs) = ComputePercentageRange(fixedMs, variancePercent);
        }

        /// <summary>
        /// User-edit entry point for Minimum and Maximum mode: stores the given range (after
        /// swapping it into order if needed), then derives and stores the best-fit Fixed
        /// delay and Wait Variance Percentage that reproduce it. Only ever called from a
        /// ViewModel edit or profile migration - never from the per-report runtime path.
        /// </summary>
        public void ApplyMinimumAndMaximum(int minimumMsValue, int maximumMsValue)
        {
            MinimumMs = minimumMsValue;
            MaximumMs = maximumMsValue;
            if (minimumMs > maximumMs)
            {
                maximumMs = minimumMs;
            }

            (fixedMs, variancePercent) = BestFitFixedAndPercentage(minimumMs, maximumMs);
        }

        /// <summary>
        /// Returns the runtime effective Minimum/Maximum for the currently selected mode. This
        /// is the single, central place mode-aware timing logic lives: callers' state machines
        /// must only ever consult this, never branch on the mode themselves.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveRange()
        {
            if (mode == OppositeTapStartDelayMode.Fixed)
            {
                return (fixedMs, fixedMs);
            }

            return (minimumMs, maximumMs);
        }

        /// <summary>
        /// Computes the inclusive percentage-variance range around a base delay, flooring each
        /// boundary to a whole millisecond using pure integer arithmetic (never floating
        /// point), so a mathematically exact boundary can never come out one millisecond off
        /// due to binary floating-point imprecision. Both operands are non-negative, so C#'s
        /// truncating integer division is exactly equivalent to a mathematical floor here.
        /// </summary>
        public static (int Minimum, int Maximum) ComputePercentageRange(int baseMs, int percent)
        {
            int minimum = (baseMs * (100 - percent)) / 100;
            int maximum = (baseMs * (100 + percent)) / 100;
            minimum = Math.Max(0, minimum);
            maximum = Math.Max(minimum, maximum);
            return (minimum, maximum);
        }

        /// <summary>
        /// Deterministic best-fit search for the whole-number Fixed delay and Wait Variance
        /// Percentage that best reproduce a requested Minimum/Maximum range, using the same
        /// priority order as OppositeTapLengthTiming.BestFitFixedAndPercentage: exact match,
        /// then smallest total boundary error, then smallest largest-boundary error, then
        /// Fixed closest to the exact midpoint, then Percentage closest to the approximate
        /// percentage, then a deterministic tie-break (ascending Fixed then ascending
        /// Percentage). Exhaustive but cheap (under 15000 integer candidates); only ever
        /// called from ApplyMinimumAndMaximum or profile migration, never from the per-report
        /// runtime path.
        /// </summary>
        public static (int FixedMs, int Percent) BestFitFixedAndPercentage(int requestedMinimum, int requestedMaximum)
        {
            if (requestedMinimum > requestedMaximum)
            {
                (requestedMinimum, requestedMaximum) = (requestedMaximum, requestedMinimum);
            }

            if (requestedMinimum == requestedMaximum)
            {
                return (Math.Clamp(requestedMinimum, MIN_START_DELAY_MS, MAX_START_DELAY_MS), 0);
            }

            double exactMidpoint = (requestedMinimum + requestedMaximum) / 2.0;
            int sum = requestedMinimum + requestedMaximum;
            double approxPercent = sum == 0 ? 0.0 :
                ((requestedMaximum - requestedMinimum) / (double)sum) * 100.0;

            int bestFixed = MIN_START_DELAY_MS;
            int bestPercent = MIN_START_DELAY_VARIANCE_PERCENT;
            long bestTotalError = 0;
            long bestMaxError = 0;
            double bestMidpointDelta = 0;
            double bestPercentDelta = 0;
            bool found = false;

            for (int candidateFixed = MIN_START_DELAY_MS; candidateFixed <= MAX_START_DELAY_MS; candidateFixed++)
            {
                for (int candidatePercent = MIN_START_DELAY_VARIANCE_PERCENT; candidatePercent <= MAX_START_DELAY_VARIANCE_PERCENT; candidatePercent++)
                {
                    (int candidateMin, int candidateMax) = ComputePercentageRange(candidateFixed, candidatePercent);

                    long totalError = Math.Abs(candidateMin - requestedMinimum) + Math.Abs(candidateMax - requestedMaximum);
                    long maxError = Math.Max(Math.Abs(candidateMin - requestedMinimum), Math.Abs(candidateMax - requestedMaximum));
                    double midpointDelta = Math.Abs(candidateFixed - exactMidpoint);
                    double percentDelta = Math.Abs(candidatePercent - approxPercent);

                    bool better;
                    if (!found)
                    {
                        better = true;
                    }
                    else if (totalError != bestTotalError)
                    {
                        better = totalError < bestTotalError;
                    }
                    else if (maxError != bestMaxError)
                    {
                        better = maxError < bestMaxError;
                    }
                    else if (midpointDelta != bestMidpointDelta)
                    {
                        better = midpointDelta < bestMidpointDelta;
                    }
                    else if (percentDelta != bestPercentDelta)
                    {
                        better = percentDelta < bestPercentDelta;
                    }
                    else
                    {
                        // Total tie: keep whichever was found first. Ascending iteration order
                        // means that is always the smallest Fixed, then the smallest
                        // Percentage - a stable, deterministic tie-break.
                        better = false;
                    }

                    if (better)
                    {
                        found = true;
                        bestFixed = candidateFixed;
                        bestPercent = candidatePercent;
                        bestTotalError = totalError;
                        bestMaxError = maxError;
                        bestMidpointDelta = midpointDelta;
                        bestPercentDelta = percentDelta;
                    }
                }
            }

            return (bestFixed, bestPercent);
        }
    }
}
