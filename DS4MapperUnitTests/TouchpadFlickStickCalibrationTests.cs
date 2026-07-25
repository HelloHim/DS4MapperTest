using DS4MapperTest;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class TouchpadFlickStickCalibrationTests
    {
        [TestMethod]
        public void PrepareNewAction_FlickStick_UsesSharedProfileCalibration()
        {
            Profile profile = new Profile
            {
                CalibRwc = 7.5,
                CalibInGameSens = 2.0,
                CalibCounts = 1350.0,
            };
            TestMapper mapper = new TestMapper(profile);
            TouchpadBindEditViewModel editViewModel =
                new TouchpadBindEditViewModel(mapper, new TouchpadNoAction());

            TouchpadFlickStick action = editViewModel.PrepareNewAction(9) as TouchpadFlickStick;

            Assert.IsNotNull(action);
            Assert.AreEqual(profile.CalibRwc, action.RealWorldCalibration);
            Assert.AreEqual(profile.CalibInGameSens, action.InGameSens);
            Assert.AreEqual(profile.CalibCounts,
                action.RealWorldCalibration * 360.0 / action.InGameSens, 1e-10);
        }
    }
}
