using System;
using System.Collections.Generic;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ActionUtil
{
    public class DistanceFunc : ActionFunc
    {
        private bool inputStatus;
        private bool distanceOutputActive;

        public DistanceFunc()
        {
            onDistance = true;
        }

        public DistanceFunc(OutputActionData outputAction,
            double distance=0.0)
        {
            onDistance = true;

            outputActions.Add(outputAction);
            outputActionEnumerator = new OutputActionDataEnumerator(outputActions);

            this.distance = distance;
        }

        public DistanceFunc(IEnumerable<OutputActionData> outputActions,
            double distance=0.0)
        {
            onDistance = true;

            this.outputActions.AddRange(outputActions);
            outputActionEnumerator =
                new OutputActionDataEnumerator(this.outputActions);

            this.distance = distance;
        }

        public DistanceFunc(DistanceFunc secondFunc)
        {
            onDistance = true;

            secondFunc.CopyTo(this);
            distance = secondFunc.distance;
        }

        public override void Prepare(Mapper mapper, bool state,
            ActionFuncStateData stateData)
        {
            bool oldOutputActive = outputActive;
            bool stateChanged = inputStatus != state;
            inputStatus = state;

            UpdateDistanceState(stateData);
            activeEvent = stateChanged || oldOutputActive != outputActive;
        }

        public override void Event(Mapper mapper, ActionFuncStateData stateData)
        {
            UpdateDistanceState(stateData);
            activeEvent = false;
        }

        private void UpdateDistanceState(ActionFuncStateData stateData)
        {
            distanceOutputActive = inputStatus &&
                stateData.axisNormValue >= distance;
            active = distanceOutputActive;
            outputActive = active;
            finished = !active;
        }

        public override void Release(Mapper mapper)
        {
            inputStatus = false;
            active = false;
            outputActive = active;
            distanceOutputActive = active;
            activeEvent = false;
            finished = true;
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
                result = $"Dist({string.Join(", ", tempList)})";
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
