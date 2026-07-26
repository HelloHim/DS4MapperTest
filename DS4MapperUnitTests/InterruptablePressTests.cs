using System.Threading;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class InterruptablePressTests
    {
        [TestMethod]
        public void ShortPress_FiresRegularTapAfterDoubleWindow()
        {
            OutputActionData output = new OutputActionData(OutputActionData.ActionType.Keyboard, 0x5A);
            ButtonAction action = new ButtonAction(new NormalPressFunc(output));
            action.ActionFuncs.Add(new DoublePressFunc { DurationMs = 25 });
            TestMapper mapper = new TestMapper();

            action.Prepare(mapper, true);
            action.Event(mapper);
            action.Prepare(mapper, false);
            action.Event(mapper);
            Assert.IsTrue(action.active, "The button must remain active while its regular tap is deferred.");
            Thread.Sleep(35);
            action.Event(mapper);

            Assert.IsTrue(output.activatedEvent, "The delayed regular press should be down after the double window.");
        }
    }
}
