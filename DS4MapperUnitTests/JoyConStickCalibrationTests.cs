using DS4MapperTest.JoyConLibrary;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class JoyConStickCalibrationTests
    {
        [TestMethod]
        public void AxisAtAssumedMidpoint_NoCalibrationChangeApplied()
        {
            var axisData = new JoyConDevice.StickAxisData { min = 548, mid = 2048, max = 3548 };

            bool calibUpdated = JoyConReader.AdjustStickAxisCalibration(ref axisData, 2048);

            Assert.IsFalse(calibUpdated);
            Assert.AreEqual((ushort)548, axisData.min);
            Assert.AreEqual((ushort)2048, axisData.mid);
            Assert.AreEqual((ushort)3548, axisData.max);
        }

        [TestMethod]
        public void AxisAboveAssumedMidpoint_ShiftsCentreAndScalesRangeSymmetrically()
        {
            var axisData = new JoyConDevice.StickAxisData { min = 548, mid = 2048, max = 3548 };

            bool calibUpdated = JoyConReader.AdjustStickAxisCalibration(ref axisData, 2148);

            Assert.IsTrue(calibUpdated);
            // The true rest position is read directly, rather than assuming the
            // theoretical midpoint is still correct.
            Assert.AreEqual((ushort)2148, axisData.mid);
            Assert.AreEqual((ushort)698, axisData.min);
            Assert.AreEqual((ushort)3598, axisData.max);
            // Range stays proportional around the corrected centre in both directions.
            Assert.AreEqual(axisData.mid - axisData.min, axisData.max - axisData.mid);
        }

        [TestMethod]
        public void AxisBelowAssumedMidpoint_ShiftsCentreAndScalesRangeSymmetrically()
        {
            var axisData = new JoyConDevice.StickAxisData { min = 548, mid = 2048, max = 3548 };

            bool calibUpdated = JoyConReader.AdjustStickAxisCalibration(ref axisData, 1948);

            Assert.IsTrue(calibUpdated);
            Assert.AreEqual((ushort)1948, axisData.mid);
            Assert.AreEqual((ushort)498, axisData.min);
            Assert.AreEqual((ushort)3398, axisData.max);
            Assert.AreEqual(axisData.mid - axisData.min, axisData.max - axisData.mid);
        }

        [TestMethod]
        public void AsymmetricStartingRange_PreservesRelativeAsymmetryOnUpwardShift()
        {
            // Further travel available below centre than above it, e.g. a stick
            // whose physical maximum sits closer to rest than its minimum.
            var axisData = new JoyConDevice.StickAxisData { min = 0, mid = 2048, max = 3600 };
            int belowGap = axisData.mid - axisData.min;
            int aboveGap = axisData.max - axisData.mid;

            bool calibUpdated = JoyConReader.AdjustStickAxisCalibration(ref axisData, 2148);

            Assert.IsTrue(calibUpdated);
            int newBelowGap = axisData.mid - axisData.min;
            int newAboveGap = axisData.max - axisData.mid;
            // Both sides shrink by the same amount, so the original asymmetry
            // between the two sides is preserved rather than clamped to symmetric.
            Assert.AreEqual(belowGap - 50, newBelowGap);
            Assert.AreEqual(aboveGap - 50, newAboveGap);
            Assert.AreEqual(belowGap - aboveGap, newBelowGap - newAboveGap);
        }

        [TestMethod]
        public void AsymmetricStartingRange_PreservesRelativeAsymmetryOnDownwardShift()
        {
            var axisData = new JoyConDevice.StickAxisData { min = 400, mid = 2048, max = 4096 };
            int belowGap = axisData.mid - axisData.min;
            int aboveGap = axisData.max - axisData.mid;

            bool calibUpdated = JoyConReader.AdjustStickAxisCalibration(ref axisData, 1948);

            Assert.IsTrue(calibUpdated);
            int newBelowGap = axisData.mid - axisData.min;
            int newAboveGap = axisData.max - axisData.mid;
            Assert.AreEqual(belowGap - 50, newBelowGap);
            Assert.AreEqual(aboveGap - 50, newAboveGap);
            Assert.AreEqual(belowGap - aboveGap, newBelowGap - newAboveGap);
        }
    }
}
