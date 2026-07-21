using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;

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

        private int brakeDurationMs = DigitalReleaseBrakePulse.DEFAULT_BRAKE_DURATION_MS;
        public int BrakeDurationMs
        {
            get => brakeDurationMs;
            set => brakeDurationMs = DigitalReleaseBrakePulse.ClampBrakeDurationMs(value);
        }

        private int minimumHoldMs = DigitalReleaseBrakePulse.DEFAULT_MINIMUM_HOLD_MS;
        public int MinimumHoldMs
        {
            get => minimumHoldMs;
            set => minimumHoldMs = DigitalReleaseBrakePulse.ClampMinimumHoldMs(value);
        }

        private readonly DigitalReleaseBrakePulse pulse = new DigitalReleaseBrakePulse();
        private BrakeState state = BrakeState.Idle;
        private bool controllingTouchActive;
        private TouchpadActionPad.DpadDirections activeDirection;
        private double activeDirectionSeconds;

        public BrakeState State => state;
        public bool HasActivePulse => pulse.IsActive;

        public TouchpadActionPad.DpadDirections Prepare(TouchEventFrame touchFrame,
            TouchpadActionPad.DpadDirections rawCurrentDir)
        {
            if (!enabled)
            {
                if (state != BrakeState.Idle || pulse.IsActive)
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
                if (!controllingTouchActive)
                {
                    pulse.TransferOrCancelForRealInput(rawMask);
                }

                controllingTouchActive = true;
                TrackActiveDirection(rawCurrentDir, dt);
                state = pulse.IsActive ? BrakeState.Braking : BrakeState.Tracking;
                return rawCurrentDir;
            }

            if (controllingTouchActive)
            {
                TryStartPulseFromLift();
                controllingTouchActive = false;
                activeDirection = TouchpadActionPad.DpadDirections.Centered;
                activeDirectionSeconds = 0.0;
            }
            else
            {
                pulse.Advance(dt, brakeDurationMs);
            }

            if (pulse.IsActive)
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
            pulse.EmitPulse(usedFuncList, data =>
            {
                data.Prepare(mapper, true);
                data.Event(mapper);
            });
        }

        public void Advance(double dtSeconds)
        {
            if (pulse.Advance(dtSeconds > 0.0 ? dtSeconds : 0.0, brakeDurationMs))
            {
                state = controllingTouchActive ? BrakeState.Tracking : BrakeState.Idle;
            }
            else if (pulse.IsActive)
            {
                state = BrakeState.Braking;
            }
        }

        public void Cleanup(Mapper mapper, ButtonAction[] usedFuncList)
        {
            ForceReleaseAndReset();
            FlushReleases(mapper, usedFuncList);
        }

        private void FlushReleases(Mapper mapper, ButtonAction[] usedFuncList)
        {
            pulse.FlushReleases(usedFuncList, data =>
            {
                data.Prepare(mapper, false);
                data.Event(mapper);
                data.Release(mapper, ignoreReleaseActions: true);
            });
        }

        private void TrackActiveDirection(TouchpadActionPad.DpadDirections rawCurrentDir, double dt)
        {
            if (rawCurrentDir == TouchpadActionPad.DpadDirections.Centered)
            {
                activeDirection = TouchpadActionPad.DpadDirections.Centered;
                activeDirectionSeconds = 0.0;
                return;
            }

            if (rawCurrentDir != activeDirection)
            {
                activeDirection = rawCurrentDir;
                activeDirectionSeconds = 0.0;
            }

            activeDirectionSeconds += dt;
        }

        private void TryStartPulseFromLift()
        {
            if (activeDirection == TouchpadActionPad.DpadDirections.Centered)
            {
                state = BrakeState.Idle;
                return;
            }

            double minHoldSeconds = minimumHoldMs / 1000.0;
            if (activeDirectionSeconds + double.Epsilon < minHoldSeconds)
            {
                state = BrakeState.Idle;
                return;
            }

            pulse.Start(ToMask(activeDirection));
            state = pulse.IsActive ? BrakeState.Braking : BrakeState.Idle;
        }

        private void ForceReleaseAndReset()
        {
            pulse.Cancel();
            controllingTouchActive = false;
            activeDirection = TouchpadActionPad.DpadDirections.Centered;
            activeDirectionSeconds = 0.0;
            state = BrakeState.Idle;
        }

        private static uint ToMask(TouchpadActionPad.DpadDirections directions)
        {
            return (uint)directions;
        }
    }
}
