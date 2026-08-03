using System.Collections.Generic;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ActionUtil
{
    public class SimPressFunc : ActionFunc
    {
        // JoyShockMapper's SIM_PRESS_WINDOW default
        public const int DEFAULT_SIM_PRESS_MS = 50;

        private bool status;
        private JoypadActionCodes triggerButton;

        public JoypadActionCodes TriggerButton
        {
            get => triggerButton;
            set => triggerButton = value;
        }

        private int simPressTimeMs = DEFAULT_SIM_PRESS_MS;
        public int SimPressTimeMs { get => simPressTimeMs; set => simPressTimeMs = value; }

        // When enabled, the regular press waits out the window in case the
        // trigger button still arrives, same contract as Hold/Double Press.
        private bool interruptRegularPress = true;
        public bool InterruptRegularPress
        {
            get => interruptRegularPress;
            set
            {
                interruptRegularPress = value;
                canPressInterrupt = value;
            }
        }

        // Whether the trigger button has already been resolved for this
        // press: either it matched (simMatched) or the window ran out.
        private bool waited;
        private bool simMatched;

        public SimPressFunc()
        {
            InterruptRegularPress = true;
        }

        public SimPressFunc(SimPressFunc srcFunc)
        {
            srcFunc.CopyTo(this);
            triggerButton = srcFunc.triggerButton;
            simPressTimeMs = srcFunc.simPressTimeMs;
            InterruptRegularPress = srcFunc.InterruptRegularPress;
        }

        public override void Prepare(Mapper mapper, bool state, ActionFuncStateData stateData)
        {
            if (status != state)
            {
                status = state;
                activeEvent = true;

                if (status)
                {
                    waited = false;
                    simMatched = false;
                    active = false;
                    outputActive = false;
                    finished = false;
                }
                else
                {
                    active = false;
                    outputActive = false;
                    finished = true;
                    waited = false;
                    simMatched = false;
                }
            }

            if (status && !waited)
            {
                if (mapper.IsButtonActive(triggerButton))
                {
                    waited = true;
                    simMatched = true;
                    active = true;
                    outputActive = true;
                }
                else if (stateData.elapsed.ElapsedMilliseconds >= simPressTimeMs)
                {
                    // Window expired with no matching press; let the regular
                    // press win.
                    waited = true;
                    simMatched = false;
                    active = false;
                    outputActive = false;
                    finished = true;
                }
            }
        }

        public override void Event(Mapper mapper, ActionFuncStateData stateData)
        {
            if (status && simMatched && !mapper.IsButtonActive(triggerButton))
            {
                // The trigger button let go first; the combo ends here and
                // does not re-arm until this button is released and pressed
                // again.
                active = false;
                outputActive = false;
                finished = true;
            }

            activeEvent = false;
        }

        public override void Release(Mapper mapper)
        {
            status = false;
            active = false;
            outputActive = false;
            activeEvent = false;
            finished = false;
            waited = false;
            simMatched = false;
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
                result = $"Sim({string.Join(", ", tempList)})";
            }

            return result;
        }

        public override string DescribeOutputActions(Mapper mapper)
        {
            string result = "";
            List<string> tempList = new List<string>();
            foreach (OutputActionData data in outputActions)
            {
                tempList.Add(data.Describe(mapper));
            }

            if (tempList.Count > 0)
            {
                result = $"{string.Join(", ", tempList)}";
            }
            else
            {
                result = "Unbound";
            }

            return result;
        }
    }
}
