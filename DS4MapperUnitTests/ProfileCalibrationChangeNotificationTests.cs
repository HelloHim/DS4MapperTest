using DS4MapperTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4MapperUnitTests
{
    // Gyro, Stick Flick Stick and Touchpad Flick Stick calibration panels each
    // subscribe to these events to refresh their own displayed In-Game Sens/RWC/
    // Counts when another panel changes the shared profile-level value. Before this
    // fix, Profile.CalibRwc/CalibInGameSens/CalibCounts were plain fields with no
    // notification, so a panel already open would silently show a stale value.
    [TestClass]
    public class ProfileCalibrationChangeNotificationTests
    {
        [TestMethod]
        public void CalibInGameSens_RaisesChangedEvent_WhenValueActuallyChanges()
        {
            Profile profile = new Profile { CalibInGameSens = 1.0 };
            int raiseCount = 0;
            profile.CalibInGameSensChanged += (s, e) => raiseCount++;

            profile.CalibInGameSens = 0.05;

            Assert.AreEqual(0.05, profile.CalibInGameSens);
            Assert.AreEqual(1, raiseCount);
        }

        [TestMethod]
        public void CalibInGameSens_DoesNotRaiseChangedEvent_WhenValueIsUnchanged()
        {
            Profile profile = new Profile { CalibInGameSens = 1.0 };
            int raiseCount = 0;
            profile.CalibInGameSensChanged += (s, e) => raiseCount++;

            profile.CalibInGameSens = 1.0;

            Assert.AreEqual(0, raiseCount);
        }

        [TestMethod]
        public void CalibRwc_RaisesChangedEvent_WhenValueActuallyChanges()
        {
            Profile profile = new Profile { CalibRwc = 5.0 };
            int raiseCount = 0;
            profile.CalibRwcChanged += (s, e) => raiseCount++;

            profile.CalibRwc = 12.5;

            Assert.AreEqual(12.5, profile.CalibRwc);
            Assert.AreEqual(1, raiseCount);
        }

        [TestMethod]
        public void CalibCounts_RaisesChangedEvent_WhenValueActuallyChanges()
        {
            Profile profile = new Profile { CalibCounts = 1800.0 };
            int raiseCount = 0;
            profile.CalibCountsChanged += (s, e) => raiseCount++;

            profile.CalibCounts = 1350.0;

            Assert.AreEqual(1350.0, profile.CalibCounts);
            Assert.AreEqual(1, raiseCount);
        }

        [TestMethod]
        public void CalibValueChanges_DoNotCrossFireEachOthersEvent()
        {
            // Each calibration field should only raise its own event, so a
            // listener that only cares about e.g. InGameSens doesn't get
            // spuriously woken by an RWC-only edit.
            Profile profile = new Profile { CalibRwc = 5.0, CalibInGameSens = 1.0, CalibCounts = 1800.0 };
            int rwcRaises = 0, inGameSensRaises = 0, countsRaises = 0;
            profile.CalibRwcChanged += (s, e) => rwcRaises++;
            profile.CalibInGameSensChanged += (s, e) => inGameSensRaises++;
            profile.CalibCountsChanged += (s, e) => countsRaises++;

            profile.CalibRwc = 8.25;

            Assert.AreEqual(1, rwcRaises);
            Assert.AreEqual(0, inGameSensRaises);
            Assert.AreEqual(0, countsRaises);
        }
    }
}
