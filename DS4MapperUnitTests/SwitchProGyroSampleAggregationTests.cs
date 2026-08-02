using DS4MapperTest.SwitchProLibrary;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class SwitchProGyroSampleAggregationTests
    {
        [TestMethod]
        public void SingleMeaningfulSample_ProducesSameMagnitudeAsThatSample()
        {
            // Only the first of the three per-report samples carries a real
            // reading; the other two are zero. With zero bias/calibration
            // offset, the combined output should equal that one sample,
            // confirming no unrelated scaling was introduced.
            short[] gyroOut = new short[9];
            gyroOut[0 * 3 + 1] = 500;

            short combined = SwitchProReader.CombineGyroAxisSamples(gyroOut, 1,
                bias: 0, calibOffset: 0, negate: false, calibOffsetSign: -1);

            Assert.AreEqual((short)500, combined);
        }

        [TestMethod]
        public void AllThreeSamples_AreSummedNotJustMostRecent()
        {
            // Three distinct per-report samples must all contribute to the
            // combined output (matching the actual elapsed report window),
            // rather than only the most recent sample being used.
            short[] gyroOut = new short[9];
            gyroOut[0 * 3 + 1] = 100;
            gyroOut[1 * 3 + 1] = -50;
            gyroOut[2 * 3 + 1] = 25;

            short combined = SwitchProReader.CombineGyroAxisSamples(gyroOut, 1,
                bias: 0, calibOffset: 0, negate: false, calibOffsetSign: -1);

            Assert.AreEqual((short)75, combined);
            Assert.AreNotEqual((short)25, combined);
        }

        [TestMethod]
        public void StationaryReport_NearRestValues_ProduceNearZeroOutput()
        {
            // All three samples matching the calibrated bias represents a
            // controller at rest; the combined output must stay at zero,
            // confirming no new resting drift was introduced.
            short[] gyroOut = new short[9];
            gyroOut[0 * 3 + 1] = 200;
            gyroOut[1 * 3 + 1] = 200;
            gyroOut[2 * 3 + 1] = 200;

            short combined = SwitchProReader.CombineGyroAxisSamples(gyroOut, 1,
                bias: 200, calibOffset: 0, negate: false, calibOffsetSign: -1);

            Assert.AreEqual((short)0, combined);
        }

        [TestMethod]
        public void YawAxis_AppliesNegationAndCalibOffsetSignAcrossAllSamples()
        {
            // Yaw negates the whole per-sample term and adds the calibration
            // offset before negation, unlike pitch/roll. A constant angular
            // rate across the report's three samples confirms this sign
            // convention is applied consistently to every sample summed.
            short[] gyroOut = new short[9];
            gyroOut[0 * 3 + 0] = 1000;
            gyroOut[1 * 3 + 0] = 1000;
            gyroOut[2 * 3 + 0] = 1000;

            short combinedYaw = SwitchProReader.CombineGyroAxisSamples(gyroOut, 0,
                bias: 50, calibOffset: 20, negate: true, calibOffsetSign: 1);
            short combinedPitch = SwitchProReader.CombineGyroAxisSamples(gyroOut, 0,
                bias: 50, calibOffset: 20, negate: false, calibOffsetSign: -1);

            Assert.AreEqual((short)-2910, combinedYaw);
            Assert.AreEqual((short)2790, combinedPitch);
        }

        [TestMethod]
        public void RollAxis_IsIndependentOfYawAndPitchOffsets()
        {
            short[] gyroOut = new short[9];
            gyroOut[0 * 3 + 2] = 10;
            gyroOut[1 * 3 + 2] = 20;
            gyroOut[2 * 3 + 2] = 30;

            short combinedRoll = SwitchProReader.CombineGyroAxisSamples(gyroOut, 2,
                bias: 5, calibOffset: 2, negate: false, calibOffsetSign: -1);

            Assert.AreEqual((short)(3 + 13 + 23), combinedRoll);
        }

        [TestMethod]
        public void UsbConnection_ResolvesToUsbElapsedReference()
        {
            // Previously the USB branch incorrectly reused the Bluetooth
            // reference duration, understating the true USB report interval.
            double reference = SwitchProDevice.ResolveBaseElapsedReference(
                SwitchProDevice.ConnectionType.USB);

            Assert.AreEqual(133.6, reference, 0.0001);
        }

        [TestMethod]
        public void BluetoothConnection_ResolvesToBluetoothElapsedReference()
        {
            double reference = SwitchProDevice.ResolveBaseElapsedReference(
                SwitchProDevice.ConnectionType.Bluetooth);

            Assert.AreEqual(66.7, reference, 0.0001);
        }
    }
}
