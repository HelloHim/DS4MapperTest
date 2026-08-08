using System;
using DS4MapperTest.ActionUtil;

namespace DS4MapperTest.StickActions
{
    /// <summary>
    /// Shared, mode-aware storage and computation for Counter Movement Release Press'
    /// Opposite Tap Length. Used by both CounterMovementReleasePressProcessor (stick D-Pad
    /// modes and Analog Emulation) and TouchpadReleaseBrake (touchpad D-Pad modes), so the
    /// percentage math, best-fit conversion search and CS2 constants exist in exactly one
    /// place rather than being duplicated across the two otherwise-independent state
    /// machines. Deliberately excludes the tap-length preset and the start-delay fields:
    /// those remain owned by each caller, since their surrounding preset/NormalizeRanges
    /// semantics differ slightly between the two.
    /// </summary>
    public sealed class OppositeTapLengthTiming
    {
        public const int CS2_FIXED_TAP_LENGTH_MS = 84;
        public const int CS2_WAIT_VARIANCE_PERCENT = 7;
        public const int CS2_TAP_LENGTH_MINIMUM_MS = 78;
        public const int CS2_TAP_LENGTH_MAXIMUM_MS = 90;

        public const int MIN_WAIT_VARIANCE_PERCENT = 0;
        public const int MAX_WAIT_VARIANCE_PERCENT = 100;

        // New actions default to Wait Variance Percentage, the mode that reproduces CS2's
        // 78-90ms range from an 84ms base and a 7% variance.
        private OppositeTapLengthMode mode = OppositeTapLengthMode.WaitVariancePercentage;
        public OppositeTapLengthMode Mode
        {
            get => mode;
            set => mode = value;
        }

        // All four numeric fields below (Fixed, Percent, Minimum, Maximum) are plain,
        // side-effect-free storage: clamp and store, nothing else. Keeping synchronisation
        // out of the property setters themselves means JSON deserialisation (which sets these
        // properties directly, in file order, one field at a time) can never trigger a stray
        // resync from a transient, partially-loaded state. Synchronisation is instead done
        // explicitly, and only from ApplyFixedAndPercentage/ApplyMinimumAndMaximum/
        // ApplyCs2Preset/migration, i.e. only on a real user edit, preset application or
        // profile load - never per controller report and never from a raw property set.
        private int fixedMs = CS2_FIXED_TAP_LENGTH_MS;
        public int FixedMs
        {
            get => fixedMs;
            set => fixedMs = DigitalReleaseBrakePulse.ClampBrakeDurationMs(value);
        }

        private int variancePercent = CS2_WAIT_VARIANCE_PERCENT;
        public int VariancePercent
        {
            get => variancePercent;
            set => variancePercent = Math.Clamp(value, MIN_WAIT_VARIANCE_PERCENT, MAX_WAIT_VARIANCE_PERCENT);
        }

        private int minimumMs = CS2_TAP_LENGTH_MINIMUM_MS;
        public int MinimumMs
        {
            get => minimumMs;
            set => minimumMs = DigitalReleaseBrakePulse.ClampBrakeDurationMs(value);
        }

        private int maximumMs = CS2_TAP_LENGTH_MAXIMUM_MS;
        public int MaximumMs
        {
            get => maximumMs;
            set => maximumMs = DigitalReleaseBrakePulse.ClampBrakeDurationMs(value);
        }

        /// <summary>
        /// User-edit entry point for Fixed mode / Wait Variance Percentage mode: stores the
        /// given fixed duration and percentage, then recomputes and stores the synchronised
        /// Minimum/Maximum from them. Only ever called from a ViewModel edit, CS2 preset
        /// application or profile migration - never from the per-report runtime path.
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
        /// duration and Wait Variance Percentage that reproduce it (see
        /// BestFitFixedAndPercentage). Only ever called from a ViewModel edit or profile
        /// migration - never from the per-report runtime path.
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
        /// Applies the CS2 preset (its 84ms/7% Fixed and Wait Variance Percentage values,
        /// and their equivalent 78-90ms Minimum/Maximum). Does not change the selected timing
        /// mode: which representation the preset actually drives at runtime depends on
        /// whatever mode is already selected.
        /// </summary>
        public void ApplyCs2Preset()
        {
            fixedMs = CS2_FIXED_TAP_LENGTH_MS;
            variancePercent = CS2_WAIT_VARIANCE_PERCENT;
            minimumMs = CS2_TAP_LENGTH_MINIMUM_MS;
            maximumMs = CS2_TAP_LENGTH_MAXIMUM_MS;
        }

        /// <summary>
        /// True whenever the current numeric tap-length values happen to equal the CS2
        /// preset's values, regardless of how they got there (the preset dropdown, a direct
        /// edit, migration, or a loaded profile).
        /// </summary>
        public bool MatchesCs2Values =>
            minimumMs == CS2_TAP_LENGTH_MINIMUM_MS && maximumMs == CS2_TAP_LENGTH_MAXIMUM_MS;

        /// <summary>
        /// Returns the runtime effective Minimum/Maximum for the currently selected mode. This
        /// is the single, central place mode-aware timing logic lives: callers' state machines
        /// must only ever consult this, never branch on the mode themselves. O(1): Minimum/
        /// Maximum are already kept synchronised with Fixed/Percent by ApplyFixedAndPercentage,
        /// so Wait Variance Percentage mode can simply reuse the already-computed range rather
        /// than recomputing it on the per-report/per-activation runtime path.
        /// </summary>
        public (int Minimum, int Maximum) GetEffectiveRange()
        {
            if (mode == OppositeTapLengthMode.Fixed)
            {
                return (fixedMs, fixedMs);
            }

            return (minimumMs, maximumMs);
        }

        /// <summary>
        /// Computes the inclusive percentage-variance range around a base duration, flooring
        /// each boundary to a whole millisecond using pure integer arithmetic (never floating
        /// point), so a mathematically exact boundary like 120 can never come out as 119 due
        /// to binary floating-point imprecision. Both operands are non-negative, so C#'s
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
        /// Deterministic best-fit search for the whole-number Fixed duration and Wait
        /// Variance Percentage that best reproduce a requested Minimum/Maximum range, per the
        /// documented priority order: exact match, then smallest total boundary error, then
        /// smallest largest-boundary error, then Fixed closest to the exact midpoint, then
        /// Percentage closest to the approximate percentage, then a deterministic tie-break
        /// (ascending Fixed then ascending Percentage, which the iteration order already
        /// provides for free since only strictly-better candidates ever replace the current
        /// best). Exhaustive but cheap (under 15000 integer candidates); only ever called from
        /// ApplyMinimumAndMaximum or profile migration, never from the per-report runtime path.
        /// </summary>
        public static (int FixedMs, int Percent) BestFitFixedAndPercentage(int requestedMinimum, int requestedMaximum)
        {
            if (requestedMinimum > requestedMaximum)
            {
                (requestedMinimum, requestedMaximum) = (requestedMaximum, requestedMinimum);
            }

            if (requestedMinimum == requestedMaximum)
            {
                return (DigitalReleaseBrakePulse.ClampBrakeDurationMs(requestedMinimum), 0);
            }

            double exactMidpoint = (requestedMinimum + requestedMaximum) / 2.0;
            int sum = requestedMinimum + requestedMaximum;
            double approxPercent = sum == 0 ? 0.0 :
                ((requestedMaximum - requestedMinimum) / (double)sum) * 100.0;

            int bestFixed = DigitalReleaseBrakePulse.MIN_BRAKE_DURATION_MS;
            int bestPercent = MIN_WAIT_VARIANCE_PERCENT;
            long bestTotalError = 0;
            long bestMaxError = 0;
            double bestMidpointDelta = 0;
            double bestPercentDelta = 0;
            bool found = false;

            for (int candidateFixed = DigitalReleaseBrakePulse.MIN_BRAKE_DURATION_MS;
                candidateFixed <= DigitalReleaseBrakePulse.MAX_BRAKE_DURATION_MS; candidateFixed++)
            {
                for (int candidatePercent = MIN_WAIT_VARIANCE_PERCENT; candidatePercent <= MAX_WAIT_VARIANCE_PERCENT; candidatePercent++)
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
