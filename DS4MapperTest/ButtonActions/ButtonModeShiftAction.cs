using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS4MapperTest.ButtonActions
{
    public class ButtonModeShiftAction : ButtonMapAction
    {
        public ButtonMapAction primaryAction;
        public ButtonMapAction modeShiftAction;
        public ButtonMapAction currentAction;
        private ButtonMapAction previousAction;
        private JoypadActionCodes modeShiftTrigger;
        public JoypadActionCodes ModeShiftTrigger
        {
            get => modeShiftTrigger; set => modeShiftTrigger = value;
        }

        public override double ButtonDistance => currentAction.ButtonDistance;
        public override double AxisUnit => currentAction.AxisUnit;

        private bool state;

        public ButtonModeShiftAction()
        {
        }

        public override void Prepare(Mapper mapper, bool status, bool alterState = true)
        {
            state = status;
            bool shifted = false;
            if (state)
            {
                shifted = modeShiftTrigger != JoypadActionCodes.Empty &&
                    mapper.IsButtonActive(modeShiftTrigger);
            }

            if (!shifted)
            {
                currentAction = primaryAction;
            }
            else
            {
                currentAction = modeShiftAction;
            }

            if (alterState)
            {
                stateData.wasActive = state;
                stateData.state = state;
                stateData.axisNormValue = state ? 1.0 : 0.0;
            }
            
            currentAction.Prepare(mapper, status, alterState);
            active = state;
            activeEvent = true;
        }

        public override void PrepareAnalog(Mapper mapper, double axisValue, double axisUnit,
            bool alterState = true)
        {
        }

        public override void Event(Mapper mapper)
        {
            if (previousAction != currentAction && previousAction != null)
            {
                previousAction.Prepare(mapper, false);
                previousAction.Event(mapper);
            }

            currentAction.Event(mapper);

            previousAction = currentAction;
            active = state;
            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState=true, bool ignoreReleaseActions = false)
        {
            if (previousAction != currentAction && previousAction != null)
            {
                previousAction.Release(mapper, resetState, ignoreReleaseActions);
                previousAction = null;
            }

            currentAction?.Release(mapper, resetState, ignoreReleaseActions);
            if (resetState)
            {
                stateData.Reset(true);
            }
        }

        public override ButtonMapAction DuplicateAction()
        {
            ButtonModeShiftAction copy = new ButtonModeShiftAction();
            copy.CopyBaseProps(this);
            copy.modeShiftTrigger = modeShiftTrigger;
            copy.primaryAction = primaryAction?.DuplicateAction();
            copy.modeShiftAction = modeShiftAction?.DuplicateAction();

            // ButtonDistance and AxisUnit read straight off currentAction, so the
            // copy needs the same starting branch Prepare would pick unshifted
            // rather than a null it would be asked about before its first Prepare.
            copy.currentAction = copy.primaryAction;
            return copy;
        }
    }
}
