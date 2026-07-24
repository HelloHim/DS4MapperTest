using System.Collections.Generic;
using System.Diagnostics;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ActionUtil
{
    // Fires its output once when the source input transitions from pressed to released
    // (the digital falling edge). Arms on press, stays armed through the whole hold
    // regardless of duration, and fires exactly once on release - never during the press,
    // never repeatedly while held, never on startup/neutral input.
    //
    // Behavioural reference: JoyShockMapper's release-press + instant action modifiers
    // (EventModifier::ReleasePress + ActionModifier::Instant, e.g. "!X/"), which press the
    // output on release and release it again automatically a short time later
    // (JoyShockMapper's MAGIC_INSTANT_DURATION, 40ms). See src/DigitalButton.cpp and
    // src/Mapping.cpp in https://github.com/Electronicks/JoyShockMapper (MIT licence).
    // No JSM source was copied; this is an independent C# implementation based on the
    // observed behaviour, built on this app's own ActionFunc/Mapper architecture.
    public class ReleaseFunc : ActionFunc
    {
        public const int DELAY_DURATION_DEFAULT = 100;

        private bool armed;
        private bool inToggleState;

        private int delayDurationMs = DELAY_DURATION_DEFAULT;
        public int DelayDurationMs { get => delayDurationMs; set => delayDurationMs = value; }

        private Stopwatch delayTimer = new Stopwatch();

        public ReleaseFunc()
        {
            onRelease = true;
        }

        public ReleaseFunc(ReleaseFunc srcFunc)
        {
            srcFunc.CopyTo(this);
            onRelease = true;
            delayDurationMs = srcFunc.delayDurationMs;
            toggleEnabled = srcFunc.toggleEnabled;
        }

        public override void Prepare(Mapper mapper, bool state, ActionFuncStateData stateData)
        {
            if (state)
            {
                // Held (or just pressed). Arm once; stay armed for the whole hold,
                // regardless of duration. Never fires while held.
                armed = true;
                active = false;
            }
            else
            {
                // Source released. Fire exactly once if a genuine earlier press armed us;
                // otherwise this is a neutral/startup report or an already-consumed cycle.
                if (armed)
                {
                    armed = false;

                    if (toggleEnabled)
                    {
                        ApplyToggleFiring();
                    }
                    else
                    {
                        active = true;
                        outputActive = true;
                        finished = false;
                        delayTimer.Restart();
                    }
                }
                else
                {
                    active = false;
                }
            }
        }

        private void ApplyToggleFiring()
        {
            inToggleState = !inToggleState;
            active = true;
            outputActive = inToggleState;
            // A toggle firing is fully resolved the moment it is applied - it either
            // presses (and stays pressed) or releases (a previous press). It never needs
            // the delayed-release pulse used by the non-toggle case.
            finished = true;
        }

        // Called by Mapper.ProcessReleaseEvents() on a later mapper tick than the one that
        // fired the press, once per tick, until it reports finished. Never invoked for a
        // toggle firing (those resolve immediately in Prepare and are never queued).
        public override void Event(Mapper mapper, ActionFuncStateData stateData)
        {
            if (active && !finished && delayTimer.ElapsedMilliseconds >= delayDurationMs)
            {
                ReleaseEvents(mapper);
                active = false;
                outputActive = false;
                finished = true;
            }
        }

        protected override void ReleaseEvents(Mapper mapper)
        {
            foreach (OutputActionData action in outputActions)
            {
                mapper.RunEventFromButton(action, false);
            }
        }

        // Called when this action is being discarded (profile unload, layer/action-set
        // change, controller disconnect, binding removal, mapper shutdown) rather than
        // through a genuine source release. Must never fire the configured output; only
        // clean up so nothing is left stuck down.
        public override void Release(Mapper mapper)
        {
            bool wasPendingRelease = active && !finished;
            armed = false;
            inToggleState = false;
            active = false;
            outputActive = false;
            finished = true;
            delayTimer.Reset();

            if (wasPendingRelease)
            {
                ReleaseEvents(mapper);
            }
        }

        public override string Describe(Mapper mapper)
        {
            string result = "";
            List<string> tempList = new List<string>();
            foreach (OutputActionData data in outputActions)
            {
                tempList.Add(data.Describe(mapper));
            }

            if (tempList.Count > 0)
            {
                result = string.Join(", ", tempList);
            }
            else
            {
                result = "Unbound";
            }

            return result;
        }

        public override string DescribeOutputActions(Mapper mapper)
        {
            return Describe(mapper);
        }
    }
}
