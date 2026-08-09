using DS4MapperTest.StickActions;
using DS4MapperTest.SwitchProLibrary;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class SwitchProMapperTests
    {
        [TestMethod]
        public void NormalizeStickYAxis_MapsPhysicalDownToLogicalDown()
        {
            StickDefinition.StickAxisData axisData = new StickDefinition.StickAxisData
            {
                min = 548,
                mid = 2048,
                max = 3548,
            };
            axisData.PostInit();

            short normalizedUp = SwitchProMapper.NormalizeStickYAxis(axisData.min, axisData);
            short normalizedNeutral = SwitchProMapper.NormalizeStickYAxis(axisData.mid, axisData);
            short normalizedDown = SwitchProMapper.NormalizeStickYAxis(axisData.max, axisData);

            Assert.AreEqual(axisData.max, normalizedUp);
            Assert.AreEqual(axisData.mid, normalizedNeutral);
            Assert.AreEqual(axisData.min, normalizedDown);
        }
    }
}
