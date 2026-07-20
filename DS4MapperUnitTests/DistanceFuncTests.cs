using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class DistanceFuncTests
    {
        [TestMethod]
        public void Event_ActivatesWhenHeldInputReachesDistance()
        {
            DistanceFunc func = new DistanceFunc
            {
                distance = 0.9,
            };

            ActionFuncStateData stateData = new ActionFuncStateData
            {
                axisNormValue = 0.5,
            };

            func.Prepare(null, true, stateData);

            Assert.IsFalse(func.active);
            Assert.IsFalse(func.outputActive);

            stateData.axisNormValue = 1.0;
            func.Event(null, stateData);

            Assert.IsTrue(func.active);
            Assert.IsTrue(func.outputActive);
            Assert.IsFalse(func.finished);
        }

        [TestMethod]
        public void Event_DeactivatesWhenHeldInputDropsBelowDistance()
        {
            DistanceFunc func = new DistanceFunc
            {
                distance = 0.9,
            };

            ActionFuncStateData stateData = new ActionFuncStateData
            {
                axisNormValue = 1.0,
            };

            func.Prepare(null, true, stateData);

            Assert.IsTrue(func.active);
            Assert.IsTrue(func.outputActive);

            stateData.axisNormValue = 0.8;
            func.Event(null, stateData);

            Assert.IsFalse(func.active);
            Assert.IsFalse(func.outputActive);
            Assert.IsTrue(func.finished);
        }

        [TestMethod]
        public void Prepare_ReevaluatesHeldInputDistanceChanges()
        {
            DistanceFunc func = new DistanceFunc
            {
                distance = 0.9,
            };

            ActionFuncStateData stateData = new ActionFuncStateData
            {
                axisNormValue = 0.5,
            };

            func.Prepare(null, true, stateData);

            Assert.IsFalse(func.active);
            Assert.IsFalse(func.outputActive);

            stateData.axisNormValue = 1.0;
            func.Prepare(null, true, stateData);

            Assert.IsTrue(func.active);
            Assert.IsTrue(func.outputActive);
            Assert.IsFalse(func.finished);

            stateData.axisNormValue = 0.8;
            func.Prepare(null, true, stateData);

            Assert.IsFalse(func.active);
            Assert.IsFalse(func.outputActive);
            Assert.IsTrue(func.finished);
        }

        [TestMethod]
        public void OutputActionConstructorsKeepDistanceFlag()
        {
            OutputActionData outputAction =
                new OutputActionData(OutputActionData.ActionType.Empty, 0);
            DistanceFunc fromEnumerable = new DistanceFunc([outputAction]);
            DistanceFunc fromCopy = new DistanceFunc(fromEnumerable);

            Assert.IsTrue(fromEnumerable.onDistance);
            Assert.IsTrue(fromCopy.onDistance);
        }
    }
}
