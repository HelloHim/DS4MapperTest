using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.GyroActions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class DS4NativeGyroOutputTests
    {
        [TestMethod]
        public void DualSenseFrameConvertsToDualShock4NativeOutputAxes()
        {
            GyroMotionAxisAdapter.ToDualShock4OutputSpace(InputDeviceType.DualSense,
                frameGyroYaw: 11, frameGyroPitch: 22, frameGyroRoll: 33,
                frameAccelX: 44, frameAccelY: 55, frameAccelZ: 66,
                out short gyroX, out short gyroY, out short gyroZ,
                out short accelX, out short accelY, out short accelZ);

            Assert.AreEqual((short)-22, gyroX);
            Assert.AreEqual((short)-11, gyroY);
            Assert.AreEqual((short)-33, gyroZ);
            Assert.AreEqual((short)-44, accelX);
            Assert.AreEqual((short)-55, accelY);
            Assert.AreEqual((short)66, accelZ);
        }

        [TestMethod]
        public void SteamControllerFrameConvertsToDualShock4NativeOutputAxes()
        {
            GyroMotionAxisAdapter.ToDualShock4OutputSpace(InputDeviceType.SteamController,
                frameGyroYaw: 11, frameGyroPitch: 22, frameGyroRoll: 33,
                frameAccelX: 44, frameAccelY: 55, frameAccelZ: 66,
                out short gyroX, out short gyroY, out short gyroZ,
                out short accelX, out short accelY, out short accelZ);

            Assert.AreEqual((short)-22, gyroX);
            Assert.AreEqual((short)-11, gyroY);
            Assert.AreEqual((short)33, gyroZ);
            Assert.AreEqual((short)-44, accelX);
            Assert.AreEqual((short)66, accelY);
            Assert.AreEqual((short)-55, accelZ);
        }

        [TestMethod]
        public void PassthruAndUnboundDifferentiateNativeGyroOutput()
        {
            Assert.IsTrue(new GyroPassthruAction().OutputsNativeGyro);
            Assert.IsFalse(new GyroNoMapAction().OutputsNativeGyro);
            Assert.IsTrue(new GyroMouse().OutputsNativeGyro);
        }

        [TestMethod]
        public void NativeGyroOutputMarksVirtualPadStateDirty()
        {
            TestMapper mapper = new TestMapper();
            GyroEventFrame frame = new GyroEventFrame
            {
                GyroYaw = 11,
                GyroPitch = 22,
                GyroRoll = 33,
                AccelX = 44,
                AccelY = 55,
                AccelZ = 66,
                timeElapsed = 1.0 / 125.0,
            };

            Assert.IsFalse(mapper.IntermediateStateRef.Dirty,
                "A fresh intermediate state should start clean.");

            mapper.PopulateStateGyro(ref frame);

            // The virtual pad report is submitted only while the intermediate
            // state is dirty. A profile binding nothing but gyro to the pad has
            // no other way to raise that flag, so it must be raised here or the
            // virtual controller never reports at all.
            Assert.IsTrue(mapper.IntermediateStateRef.Dirty);
            Assert.AreNotEqual((short)0, mapper.IntermediateStateRef.GyroYaw);
        }

        [TestMethod]
        public void PassthruGyroActivationDefaultsToAlwaysOn()
        {
            GyroPassthruAction action = new GyroPassthruAction();
            GyroEventFrame frame = new GyroEventFrame { timeElapsed = 1.0 / 125.0 };

            action.Prepare(null, ref frame);

            Assert.IsTrue(action.active);
        }

        [TestMethod]
        public void PassthruGyroActivationCanDisableOutput()
        {
            GyroPassthruAction action = new GyroPassthruAction();
            action.passthruParams.gyroTriggerButtons = Array.Empty<JoypadActionCodes>();
            action.passthruParams.triggerActivates = true;
            GyroEventFrame frame = new GyroEventFrame { timeElapsed = 1.0 / 125.0 };

            action.Prepare(new TestMapper(), ref frame);

            Assert.IsFalse(action.active);
        }

        [TestMethod]
        public void PassthruGyroActivationSettingsRoundTrip()
        {
            GyroPassthruAction action = new GyroPassthruAction();
            action.passthruParams.gyroTriggerButtons = new[] { JoypadActionCodes.BtnSouth };
            action.passthruParams.triggerActivates = false;
            action.passthruParams.andCond = true;
            action.passthruParams.activationHoldMs = 150;
            action.ChangedProperties.Add(GyroPassthruAction.PropertyKeyStrings.TRIGGER_BUTTONS);
            action.ChangedProperties.Add(GyroPassthruAction.PropertyKeyStrings.TRIGGER_ACTIVATE);
            action.ChangedProperties.Add(GyroPassthruAction.PropertyKeyStrings.TRIGGER_EVAL_COND);
            action.ChangedProperties.Add(GyroPassthruAction.PropertyKeyStrings.ACTIVATION_HOLD_MS);

            string json = JsonConvert.SerializeObject(
                new GyroPassthruActionSerializer(null, action));
            GyroPassthruActionSerializer serializer = new GyroPassthruActionSerializer();
            JsonConvert.PopulateObject(json, serializer);
            GyroPassthruAction reloaded = (GyroPassthruAction)serializer.MapAction;

            CollectionAssert.AreEqual(
                new[] { JoypadActionCodes.BtnSouth },
                reloaded.passthruParams.gyroTriggerButtons);
            Assert.IsFalse(reloaded.passthruParams.triggerActivates);
            Assert.IsTrue(reloaded.passthruParams.andCond);
            Assert.AreEqual(150, reloaded.passthruParams.activationHoldMs);
        }
    }
}
