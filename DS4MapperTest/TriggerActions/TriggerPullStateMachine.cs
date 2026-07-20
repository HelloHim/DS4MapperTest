using System;
using System.Diagnostics;

namespace DS4MapperTest.TriggerActions
{
    public enum TriggerStyle : ushort
    {
        SimpleThreshold,
        FullPullOnly,
        HipFire,
        HipFireExclusive,
    }

    public enum HipFirePreset : ushort
    {
        Fast,
        Balanced,
        Relaxed,
        Custom,
    }

    public sealed class TriggerPullStateMachine
    {
        public struct Result
        {
            public bool SoftAllowed;
            public bool FullAllowed;
            public bool PullInProgress;
            public bool SoftPending;
        }

        private enum PullOutcome
        {
            None,
            Soft,
            Full,
        }

        private readonly Func<long> timestampProvider;
        private double previousValue;
        private bool pullInProgress;
        private bool softPending;
        private long softPendingTimestamp;
        private bool softActivated;
        private bool fullActivated;
        private PullOutcome lockedOutcome;

        public TriggerPullStateMachine()
            : this(Stopwatch.GetTimestamp)
        {
        }

        public TriggerPullStateMachine(Func<long> timestampProvider)
        {
            this.timestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
        }

        public Result Update(TriggerStyle style, double value, double fullThreshold,
            int hipFireWindowMs)
        {
            value = Math.Clamp(value, 0.0, 1.0);
            fullThreshold = Math.Max(fullThreshold, 0.0);
            hipFireWindowMs = Math.Clamp(hipFireWindowMs, 0, 1000);

            bool softNow = value > 0.0;
            bool fullNow = value >= fullThreshold;
            bool crossedSoft = previousValue <= 0.0 && softNow;
            bool crossedFull = previousValue < fullThreshold && fullNow;

            if (!softNow)
            {
                ResetTransient();
                previousValue = value;
                return new Result();
            }

            if (crossedSoft)
            {
                pullInProgress = true;
            }

            Result result = style switch
            {
                TriggerStyle.FullPullOnly => UpdateFullPullOnly(fullNow),
                TriggerStyle.HipFire => UpdateHipFire(softNow, fullNow,
                    crossedSoft, crossedFull, hipFireWindowMs, false),
                TriggerStyle.HipFireExclusive => UpdateHipFire(softNow, fullNow,
                    crossedSoft, crossedFull, hipFireWindowMs, true),
                _ => UpdateSimpleThreshold(softNow, fullNow),
            };

            result.PullInProgress = pullInProgress;
            result.SoftPending = softPending;
            previousValue = value;
            return result;
        }

        public void Reset()
        {
            previousValue = 0.0;
            ResetTransient();
        }

        private Result UpdateSimpleThreshold(bool softNow, bool fullNow)
        {
            softActivated |= softNow;
            fullActivated |= fullNow;

            return new Result
            {
                SoftAllowed = softNow,
                FullAllowed = fullNow,
            };
        }

        private Result UpdateFullPullOnly(bool fullNow)
        {
            fullActivated |= fullNow;

            return new Result
            {
                SoftAllowed = false,
                FullAllowed = fullNow,
            };
        }

        private Result UpdateHipFire(bool softNow, bool fullNow,
            bool crossedSoft, bool crossedFull, int windowMs, bool exclusive)
        {
            if (crossedSoft && lockedOutcome == PullOutcome.None)
            {
                softPending = true;
                softPendingTimestamp = timestampProvider();
            }

            if (softPending && fullNow && IsInsideWindow(windowMs))
            {
                softPending = false;
                lockedOutcome = PullOutcome.Full;
                fullActivated = true;
            }
            else if (softPending && softNow && !IsInsideWindow(windowMs))
            {
                softPending = false;
                lockedOutcome = exclusive ? PullOutcome.Soft : PullOutcome.None;
                softActivated = true;
            }

            bool allowSoft = false;
            bool allowFull = false;

            if (lockedOutcome == PullOutcome.Full)
            {
                allowFull = fullNow;
            }
            else if (lockedOutcome == PullOutcome.Soft)
            {
                allowSoft = softNow;
            }
            else if (softActivated)
            {
                allowSoft = softNow;
                allowFull = fullNow;
            }
            else if (!softPending && crossedFull)
            {
                allowFull = fullNow;
            }

            if (exclusive && lockedOutcome == PullOutcome.Soft)
            {
                allowFull = false;
            }

            fullActivated |= allowFull;
            return new Result
            {
                SoftAllowed = allowSoft,
                FullAllowed = allowFull,
            };
        }

        private bool IsInsideWindow(int windowMs)
        {
            long elapsedTicks = timestampProvider() - softPendingTimestamp;
            long elapsedMs = elapsedTicks * 1000 / Stopwatch.Frequency;
            return elapsedMs <= windowMs;
        }

        private void ResetTransient()
        {
            pullInProgress = false;
            softPending = false;
            softPendingTimestamp = 0;
            softActivated = false;
            fullActivated = false;
            lockedOutcome = PullOutcome.None;
        }
    }
}
