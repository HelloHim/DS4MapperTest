using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS4MapperTest.ButtonActions
{
    public class AxisDirButton : ButtonAction
    {
        public enum AxisDirection : uint
        {
            None,
            XNeg,
            XPos,
            YNeg,
            YPos,
            XY,
        }

        private double currentValue;
        private AxisDirection direction;
        private double lastWheelDistance;
        private double axisUnit;

        public override double ButtonDistance => currentValue;
        public override double AxisUnit => axisUnit;

        public AxisDirection Direction
        {
            get => direction;
            set => direction = value;
        }

        public AxisDirButton(): base()
        {
            analog = true;
        }

        public AxisDirButton(ActionFunc actionFunc) :
            base(actionFunc)
        {
            analog = true;
        }

        public AxisDirButton(OutputActionData outputAction) :
            base(outputAction)
        {
            analog = true;
        }

        public AxisDirButton(IEnumerable<OutputActionData> outputActions) :
            base(outputActions)
        {
            analog = true;
        }

        public AxisDirButton(AxisDirButton parentAction) : base(parentAction)
        {
            analog = true;
        }

        public override void PrepareAnalog(Mapper mapper, double axisNorm, double axisUnit,
            bool alterState = true)
        {
            bool previousStatus = status;
            currentValue = axisNorm;
            active = true;
            activeEvent = true;
            status = currentValue != 0.0;
            this.axisUnit = axisUnit;
            if (alterState)
            {
                stateData.wasActive = stateData.state;
                stateData.state = status;
                stateData.axisNormValue = axisNorm;
            }

            if (previousStatus != status && status)
            {
                // AxisDirButton drives the soft/full stages of a dual-stage
                // trigger.  Delayed activators (Hold Press, Start Press and
                // Release Press) use this timer to measure the stage press,
                // just as they do for a regular ButtonAction.
                if (alterState)
                {
                    stateData.elapsed.Restart();
                }

                if (useParentActions)
                {
                    actionFuncCandidates.AddRange(parentButtonAct.ActionFuncs);
                }
                else
                {
                    actionFuncCandidates.AddRange(actionFuncs);
                }

                // Keep analogue directions and trigger stages on the same
                // interruptable regular-press timing path as digital buttons.
                ConfigureRegularPressInterruptDelay(actionFuncCandidates);
            }
        }

        public override void WrapTickProcess(OutputActionData action)
        {
            switch (action.OutputType)
            {
                case OutputActionData.ActionType.MouseWheel:
                {
                    double temp = Math.Abs(lastWheelDistance - currentValue);
                    if (temp >= 0.1)
                    {
                        lastWheelDistance = currentValue;
                        long currentElapsed = action.Elapsed.ElapsedMilliseconds;
                        long newTime = (long)(action.DurationMs / ButtonDistance);
                        long newElapsed;
                        if (currentElapsed > newTime)
                        {
                            newElapsed = newTime;
                        }
                        else
                        {
                            newElapsed = newTime - currentElapsed;
                        }
                        
                        action.EffectiveDurationMs = (int)newElapsed;
                    }
                    break;
                }
                default:
                    break;
            }
        }

        public override ButtonMapAction DuplicateAction()
        {
            Trace.WriteLine("IN AxisDirButton");
            return new AxisDirButton(this);
        }
    }
}
