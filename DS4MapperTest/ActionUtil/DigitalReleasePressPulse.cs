using System;
using System.Diagnostics;

namespace DS4MapperTest.ActionUtil
{
    public sealed class DigitalReleasePressPulse
    {
        public const int DEFAULT_RELEASE_PRESS_DURATION_MS = 100;
        public const int MIN_RELEASE_PRESS_DURATION_MS = 10;
        public const int MAX_RELEASE_PRESS_DURATION_MS = 150;
        public const int DEFAULT_MINIMUM_HOLD_MS = 0;
        public const int MIN_MINIMUM_HOLD_MS = 0;
        public const int MAX_MINIMUM_HOLD_MS = 300;

        public const uint UP = 1;
        public const uint RIGHT = 2;
        public const uint DOWN = 4;
        public const uint LEFT = 8;

        public static readonly uint[] CardinalComponents = new[] { UP, DOWN, LEFT, RIGHT };

        private uint pulseOwnedComponents;
        private uint explicitReleaseComponents;
        private double pulseElapsedSeconds;
        private long pulseStartTimestamp;

        public uint PulseOwnedComponents => pulseOwnedComponents;
        public uint ExplicitReleaseComponents => explicitReleaseComponents;
        public bool IsActive => pulseOwnedComponents != 0;

        public void Start(uint releasedComponents)
        {
            uint opposite = OppositeMask(releasedComponents);
            if (opposite == 0)
            {
                return;
            }

            explicitReleaseComponents |= pulseOwnedComponents;
            pulseOwnedComponents = opposite;
            pulseElapsedSeconds = 0.0;
            pulseStartTimestamp = Stopwatch.GetTimestamp();
        }

        public void Cancel()
        {
            explicitReleaseComponents |= pulseOwnedComponents;
            ClearPulseTimer();
        }

        public void TransferOrCancelForRealInput(uint realComponents)
        {
            if (pulseOwnedComponents == 0)
            {
                return;
            }

            uint canceled = pulseOwnedComponents & ~realComponents;
            explicitReleaseComponents |= canceled;
            pulseOwnedComponents = 0;

            if (pulseOwnedComponents == 0)
            {
                ClearPulseTimer();
            }
        }

        public bool Advance(double dtSeconds, int durationMs)
        {
            if (pulseOwnedComponents == 0)
            {
                return false;
            }

            if (dtSeconds > 0.0)
            {
                pulseElapsedSeconds += dtSeconds;
            }

            if (GetPulseElapsedSeconds() * 1000.0 < durationMs)
            {
                return false;
            }

            explicitReleaseComponents |= pulseOwnedComponents;
            ClearPulseTimer();
            return true;
        }

        public void FlushReleases<TAction>(TAction[] actionList, Action<TAction> releaseAction)
            where TAction : class
        {
            if (actionList == null || explicitReleaseComponents == 0)
            {
                return;
            }

            foreach (uint component in CardinalComponents)
            {
                if (!Has(explicitReleaseComponents, component))
                {
                    continue;
                }

                int index = (int)component;
                if (index >= 0 && index < actionList.Length)
                {
                    TAction action = actionList[index];
                    if (action != null)
                    {
                        releaseAction(action);
                    }
                }
            }

            explicitReleaseComponents = 0;
        }

        public void EmitPulse<TAction>(TAction[] actionList, Action<TAction> pressAction)
            where TAction : class
        {
            if (actionList == null || pulseOwnedComponents == 0)
            {
                return;
            }

            foreach (uint component in CardinalComponents)
            {
                if (!Has(pulseOwnedComponents, component))
                {
                    continue;
                }

                int index = (int)component;
                if (index >= 0 && index < actionList.Length)
                {
                    TAction action = actionList[index];
                    if (action != null)
                    {
                        pressAction(action);
                    }
                }
            }
        }

        public static int ClampReleasePressDurationMs(int value)
        {
            return Math.Clamp(value, MIN_RELEASE_PRESS_DURATION_MS, MAX_RELEASE_PRESS_DURATION_MS);
        }

        public static int ClampMinimumHoldMs(int value)
        {
            return Math.Clamp(value, MIN_MINIMUM_HOLD_MS, MAX_MINIMUM_HOLD_MS);
        }

        public static bool Has(uint mask, uint bit)
        {
            return (mask & bit) != 0;
        }

        public static uint OppositeMask(uint mask)
        {
            uint result = 0;
            foreach (uint component in CardinalComponents)
            {
                if (Has(mask, component))
                {
                    result |= ComponentOpposite(component);
                }
            }

            return result;
        }

        public static uint ComponentOpposite(uint component)
        {
            switch (component)
            {
                case UP: return DOWN;
                case DOWN: return UP;
                case LEFT: return RIGHT;
                case RIGHT: return LEFT;
                default: return 0;
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

        private void ClearPulseTimer()
        {
            pulseOwnedComponents = 0;
            pulseElapsedSeconds = 0.0;
            pulseStartTimestamp = 0;
        }
    }
}
