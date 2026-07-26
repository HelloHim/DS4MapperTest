using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.TriggerActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class TriggerDualStageHapticsTests
    {
        private sealed class FeedbackRecordingMapper : TestMapper
        {
            public List<double> FeedbackRatios { get; } = new List<double>();

            public override void SetFeedback(string mappingId, double ratio,
                MapAction.HapticsSide side = MapAction.HapticsSide.Default)
            {
                FeedbackRatios.Add(ratio);
            }
        }

        [TestMethod]
        public void SoftPullHaptics_PulsesAtTheConfiguredIntensity()
        {
            FeedbackRecordingMapper mapper = new FeedbackRecordingMapper();
            TriggerDualStageAction action = new TriggerDualStageAction
            {
                MappingId = "LT",
                TriggerDef = mapper.KnownTriggerDefinitions["LT"],
                TriggerStateMode = TriggerDualStageAction.DualStageMode.Threshold,
                SoftPullActionHapticsIntensity = MapAction.HapticsIntensity.Heavy,
            };
            TriggerEventFrame eventFrame = new TriggerEventFrame
            {
                axisValue = 16000,
            };

            action.Prepare(mapper, ref eventFrame);
            action.Event(mapper);
            action.Prepare(mapper, ref eventFrame);
            action.Event(mapper);

            CollectionAssert.AreEqual(new List<double> { 0.8, 0.0 },
                mapper.FeedbackRatios);
        }

        [TestMethod]
        public void SoftPullHaptics_InheritsUnlessOverridden()
        {
            TestMapper mapper = new TestMapper();
            TriggerDualStageAction parent = new TriggerDualStageAction
            {
                MappingId = "LT",
                TriggerDef = mapper.KnownTriggerDefinitions["LT"],
                SoftPullActionHapticsIntensity = MapAction.HapticsIntensity.Medium,
            };
            TriggerDualStageAction child = new TriggerDualStageAction();

            child.SoftCopyFromParent(parent);

            Assert.AreEqual(MapAction.HapticsIntensity.Medium,
                child.SoftPullActionHapticsIntensity);
        }
    }
}
