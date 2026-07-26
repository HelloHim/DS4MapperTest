using System.Collections.Generic;
using System.Diagnostics;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ActionUtil
{
    public class DoublePressFunc : ActionFunc
    {
        public const int DEFAULT_TAP_WINDOW_MS = 150;

        private enum TapStatus : uint
        {
            Inactive,
            FirstPress,
            WaitingForSecondPress,
            SecondPress,
        }

        private bool status;
        private int durationMs = DEFAULT_TAP_WINDOW_MS;
        public int DurationMs { get => durationMs; set => durationMs = value; }

        // The window begins on the first release, so a longer first tap does
        // not steal time from the configured second-tap window.
        private readonly Stopwatch elapsed = new Stopwatch();
        private TapStatus currentTapStatus;

        public DoublePressFunc()
        {
        }

        public DoublePressFunc(DoublePressFunc srcFunc)
        {
            srcFunc.CopyTo(this);
            durationMs = srcFunc.durationMs;
            toggleEnabled = srcFunc.toggleEnabled;
        }

        public override void PrepareState(Mapper mapper, ActionFunc secondFunc)
        {
            base.PrepareState(mapper, secondFunc);
            if (secondFunc is DoublePressFunc tempFunc)
            {
                currentTapStatus = tempFunc.currentTapStatus;
            }
        }

        public override void Prepare(Mapper mapper, bool state, ActionFuncStateData stateData)
        {
            if (status == state) return;

            status = state;
            activeEvent = true;
            if (status) HandlePress();
            else HandleRelease();
        }

        private void HandlePress()
        {
            if (toggleEnabled && active)
            {
                active = outputActive = false;
                finished = true;
                elapsed.Reset();
                currentTapStatus = TapStatus.Inactive;
            }
            else if (currentTapStatus == TapStatus.WaitingForSecondPress &&
                elapsed.ElapsedMilliseconds <= durationMs)
            {
                currentTapStatus = TapStatus.SecondPress;
                elapsed.Reset();
                active = outputActive = true;
                finished = false;
            }
            else
            {
                // This is a first press, or the old window expired and this
                // press must become the new first tap.
                currentTapStatus = TapStatus.FirstPress;
                elapsed.Reset();
                active = outputActive = false;
                finished = false;
            }
        }

        private void HandleRelease()
        {
            switch (currentTapStatus)
            {
                case TapStatus.FirstPress:
                    currentTapStatus = TapStatus.WaitingForSecondPress;
                    elapsed.Restart();
                    active = outputActive = false;
                    finished = false;
                    break;
                case TapStatus.SecondPress:
                    elapsed.Reset();
                    currentTapStatus = TapStatus.Inactive;
                    if (!toggleEnabled)
                    {
                        active = outputActive = false;
                        finished = true;
                    }
                    break;
                default:
                    active = outputActive = false;
                    break;
            }
        }

        public override void Event(Mapper mapper, ActionFuncStateData stateData)
        {
        }

        public override void Release(Mapper mapper)
        {
            status = false;
            active = outputActive = false;
            activeEvent = false;
            finished = false;
            elapsed.Reset();
            currentTapStatus = TapStatus.Inactive;
        }

        public void Reset()
        {
            currentTapStatus = TapStatus.Inactive;
            elapsed.Reset();
        }

        public override string Describe(Mapper mapper)
        {
            List<string> descriptions = new List<string>();
            foreach (OutputActionData data in outputActions)
            {
                descriptions.Add(data.Describe(mapper));
            }

            return descriptions.Count > 0 ? $"DP({string.Join(", ", descriptions)})" : "";
        }

        public override string DescribeOutputActions(Mapper mapper)
        {
            List<string> descriptions = new List<string>();
            foreach (OutputActionData data in outputActions)
            {
                descriptions.Add(data.Describe(mapper));
            }

            return descriptions.Count > 0 ? string.Join(", ", descriptions) : "Unbound";
        }
    }
}
