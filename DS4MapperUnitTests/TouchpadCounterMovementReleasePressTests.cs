using System.Collections.Generic;
using System.Reflection;
using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels.TouchpadActionPropViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class TouchpadCounterMovementReleasePressTests : BindingHelperBase
    {
        private const short FULL = 32767;
        private const short DIAG = 23170;
        private const double DT = 0.008;

        private const uint VK_W = 0x57;
        private const uint VK_A = 0x41;
        private const uint VK_S = 0x53;
        private const uint VK_D = 0x44;

        private sealed class NoOpVirtualKBM : VirtualKBMBase
        {
            public override bool Connect() => true;
            public override bool Disconnect() => true;
            public override void MoveRelativeMouse(int x, int y) { }
            public override void MoveAbsoluteMouse(double x, double y) { }
            public override void PerformMouseWheelEvent(int vertical, int horizontal) { }
            public override void PerformMouseButtonEvent(uint mouseButton) { }
            public override void PerformMouseButtonPress(uint mouseButton) { }
            public override void PerformMouseButtonRelease(uint mouseButton) { }
            public override void PerformKeyPress(uint key) { }
            public override void PerformKeyPressAlt(uint key) { }
            public override void PerformKeyRelease(uint key) { }
            public override void PerformKeyReleaseAlt(uint key) { }
            public override string GetDisplayName() => "NoOp";
            public override string GetIdentifier() => "noop";
            public override string GetFullDisplayName() => "NoOp";
        }

        [TestInitialize]
        public void TestInitialize()
        {
            TestMapper.KeyReferenceCountDict.Clear();
        }

        private string BuildProfileJson(bool releasePressEnabled = true, int minHoldMs = 80, int durationMs = 40)
        {
            return $@"{{
  ""Name"": ""TouchpadCounterMovementReleasePressTest"",
  ""Description"": ""TouchpadCounterMovementReleasePressTest"",
  ""Creator"": ""test"",
  ""CreationDate"": ""2026-07-21T00:00:00+0000"",
  ""ActionSets"": [
    {{
      ""Index"": 0,
      ""Name"": ""Set 1"",
      ""Description"": ""Only ActionSets"",
      ""ActionLayers"": [
        {{
          ""Index"": 0,
          ""Name"": ""Default"",
          ""Description"": ""Only Action Layer"",
          ""MappedActions"": [
            {{
              ""Id"": 0,
              ""Name"": ""TouchpadWASD"",
              ""ActionMode"": ""TouchActionPadAction"",
              ""Bindings"": {{
                ""Up"": {{ ""Name"": ""Up"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""W"" }} ] }} ] }},
                ""Down"": {{ ""Name"": ""Down"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""S"" }} ] }} ] }},
                ""Left"": {{ ""Name"": ""Left"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""A"" }} ] }} ] }},
                ""Right"": {{ ""Name"": ""Right"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""D"" }} ] }} ] }},
                ""UpLeft"": {{ ""Name"": ""UpLeft"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""W"" }}, {{ ""Type"": ""Keyboard"", ""Code"": ""A"" }} ] }} ] }},
                ""UpRight"": {{ ""Name"": ""UpRight"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""W"" }}, {{ ""Type"": ""Keyboard"", ""Code"": ""D"" }} ] }} ] }},
                ""DownLeft"": {{ ""Name"": ""DownLeft"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""S"" }}, {{ ""Type"": ""Keyboard"", ""Code"": ""A"" }} ] }} ] }},
                ""DownRight"": {{ ""Name"": ""DownRight"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""S"" }}, {{ ""Type"": ""Keyboard"", ""Code"": ""D"" }} ] }} ] }}
              }},
              ""Settings"": {{
                ""PadMode"": ""Standard"",
                ""DeadZone"": 0.0,
                ""DiagonalRange"": 45,
                ""BrakeEnabled"": {releasePressEnabled.ToString().ToLowerInvariant()},
                ""BrakeDurationMs"": {durationMs},
                ""CounterMovementMinimumHoldMs"": {minHoldMs}
              }}
            }}
          ]
        }}
      ]
    }}
  ],
  ""Mappings"": [
    {{
      ""ActionSet"": 0,
      ""ActionLayer"": 0,
      ""InputMappings"": [
        {{ ""Input"": ""LeftTouchpad"", ""Action"": 0 }}
      ]
    }}
  ]
}}";
        }

        private (TestMapper mapper, TouchpadActionPad padAction) LoadMapper(
            bool releasePressEnabled = true, int minHoldMs = 80, int durationMs = 40)
        {
            ProfileSerializer.EventInputMapper = new SendInputMapping();

            Profile tempProfile = new Profile();
            mapper = new TestMapper(tempProfile);
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mapper, new NoOpVirtualKBM());
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(BuildProfileJson(releasePressEnabled, minHoldMs, durationMs), profileSerializer);
            profileSerializer.PopulateProfile();
            tempProfile.ResetAliases();

            List<ProfileActionsMapping> tempMappings = profileSerializer.ActionMappings;
            FillMappingProfileInitialData(tempProfile, tempMappings);
            SyncActionData(tempProfile);

            mapper.EditActionSet = tempProfile.ActionSets[0];
            mapper.EditLayer = tempProfile.ActionSets[0].ActionLayers[0];

            TouchpadActionPad padAction =
                tempProfile.ActionSets[0].ActionLayers[0].touchpadActionDict["LeftTouchpad"] as TouchpadActionPad;
            return ((TestMapper)mapper, padAction);
        }

        private static void Touch(TestMapper mapper, short x, short y, bool active, double dt = DT)
        {
            SteamControllerState state = new SteamControllerState
            {
                LeftPad = new SteamControllerState.TouchPadInfo
                {
                    X = x,
                    Y = y,
                    Touch = active,
                },
                timeElapsed = dt,
            };

            mapper.Reader_Report(state, out IntermediateState _);
        }

        private static void HoldRight(TestMapper mapper, int ticks)
        {
            for (int i = 0; i < ticks; i++) Touch(mapper, FULL, 0, true);
        }

        private static void HoldUp(TestMapper mapper, int ticks)
        {
            for (int i = 0; i < ticks; i++) Touch(mapper, 0, FULL, true);
        }

        private static void HoldUpRight(TestMapper mapper, int ticks)
        {
            for (int i = 0; i < ticks; i++) Touch(mapper, DIAG, DIAG, true);
        }

        private static void Lift(TestMapper mapper)
        {
            Touch(mapper, 0, 0, false);
        }

        private static bool KeyDown(uint vk) => TestMapper.KeyReferenceCountDict.ContainsKey(vk);

        [TestMethod]
        public void TouchpadCounterMovementReleasePressSettings_AreVisibleForDPadZones()
        {
            var (mapper, padAction) = LoadMapper();

            TouchpadActionPadPropViewModel vm = new TouchpadActionPadPropViewModel(mapper, padAction);

            Assert.IsTrue(vm.ShowCounterMovementReleasePressSection);
        }

        [TestMethod]
        public void CardinalRelease_FiresOppositeAndExpires()
        {
            var (mapper, padAction) = LoadMapper(durationMs: 16);

            HoldRight(mapper, 20);
            Assert.IsTrue(KeyDown(VK_D));

            Lift(mapper);
            Assert.AreEqual(TouchpadCounterMovementReleasePress.PressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_D));
            Assert.IsTrue(KeyDown(VK_A));

            Lift(mapper);
            Lift(mapper);
            Assert.IsFalse(KeyDown(VK_A));
        }

        [TestMethod]
        public void BelowMinimumHold_DoesNotReleasePress()
        {
            var (mapper, _) = LoadMapper(minHoldMs: 80);

            HoldRight(mapper, 4);
            Lift(mapper);

            Assert.IsFalse(KeyDown(VK_A));
        }

        [TestMethod]
        public void ExactMinimumHoldBoundary_ReleasePresses()
        {
            var (mapper, padAction) = LoadMapper(minHoldMs: 80);

            HoldRight(mapper, 10);
            Lift(mapper);

            Assert.AreEqual(TouchpadCounterMovementReleasePress.PressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_A));
        }

        [TestMethod]
        public void DiagonalRelease_FiresBothOppositeComponentsTogether()
        {
            var (mapper, padAction) = LoadMapper(durationMs: 16);

            HoldUpRight(mapper, 20);
            Lift(mapper);

            Assert.AreEqual(TouchpadCounterMovementReleasePress.PressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S));
            Assert.IsTrue(KeyDown(VK_A));

            Lift(mapper);
            Lift(mapper);
            Assert.IsFalse(KeyDown(VK_S));
            Assert.IsFalse(KeyDown(VK_A));
        }

        [TestMethod]
        public void ZoneSliding_DoesNotReleasePressUntilLift()
        {
            var (mapper, _) = LoadMapper();

            HoldRight(mapper, 20);
            HoldUpRight(mapper, 2);
            HoldUp(mapper, 2);

            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsFalse(KeyDown(VK_S));
        }

        [TestMethod]
        public void FinalDirectionHoldTiming_ResetsOnDirectionChange()
        {
            var (mapper, _) = LoadMapper(minHoldMs: 80);

            HoldRight(mapper, 20);
            HoldUp(mapper, 2);
            Lift(mapper);

            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsFalse(KeyDown(VK_S));
        }

        [TestMethod]
        public void FinalDirectionSelection_UsesDirectionHeldAtLift()
        {
            var (mapper, _) = LoadMapper(minHoldMs: 80);

            HoldRight(mapper, 20);
            HoldUp(mapper, 20);
            Lift(mapper);

            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsTrue(KeyDown(VK_S));
        }

        [TestMethod]
        public void NeutralEntryClearsEligibleDirection()
        {
            var (mapper, padAction) = LoadMapper(minHoldMs: 80);
            padAction.CounterMovementReleasePress.TriggerOnDeadZoneReleaseEnabled = false;

            HoldRight(mapper, 20);
            Touch(mapper, 0, 0, true);
            Lift(mapper);

            Assert.IsFalse(KeyDown(VK_A));
        }

        [TestMethod]
        public void DeadZoneReleaseEnabled_FiresOnCentreWithoutLifting()
        {
            var (mapper, padAction) = LoadMapper(minHoldMs: 80, durationMs: 40);
            padAction.CounterMovementReleasePress.TriggerOnDeadZoneReleaseEnabled = true;

            HoldRight(mapper, 20);
            Touch(mapper, 0, 0, true);

            Assert.IsTrue(KeyDown(VK_A));
        }

        [TestMethod]
        public void DeadZoneReleaseEnabled_StillIgnoresZoneToZoneSliding()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.TriggerOnDeadZoneReleaseEnabled = true;

            HoldRight(mapper, 20);
            HoldUpRight(mapper, 2);
            HoldUp(mapper, 2);

            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsFalse(KeyDown(VK_S));
        }

        [TestMethod]
        public void NewRealTouchCancelsPulse()
        {
            var (mapper, _) = LoadMapper(durationMs: 40);

            HoldRight(mapper, 20);
            Lift(mapper);
            Assert.IsTrue(KeyDown(VK_A));

            Touch(mapper, FULL, 0, true);

            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsTrue(KeyDown(VK_D));
        }

        [TestMethod]
        public void SameOutputTouchTransfersWithoutPulseExpiryRelease()
        {
            var (mapper, _) = LoadMapper(durationMs: 16);

            HoldRight(mapper, 20);
            Lift(mapper);
            Assert.IsTrue(KeyDown(VK_A));

            Touch(mapper, -FULL, 0, true);
            Assert.IsTrue(KeyDown(VK_A));

            Touch(mapper, -FULL, 0, true);
            Touch(mapper, -FULL, 0, true);
            Assert.IsTrue(KeyDown(VK_A));
        }

        [TestMethod]
        public void FeatureDisabled_DoesNotReleasePress()
        {
            var (mapper, _) = LoadMapper(releasePressEnabled: false);

            HoldRight(mapper, 20);
            Lift(mapper);

            Assert.IsFalse(KeyDown(VK_A));
        }

        [TestMethod]
        public void Persistence_SaveLoadCloneAndIndependentFromStickSettings()
        {
            var (_, padAction) = LoadMapper(minHoldMs: 123, durationMs: 77);

            Assert.IsTrue(padAction.CounterMovementReleasePress.Enabled);
            Assert.AreEqual(77, padAction.CounterMovementReleasePress.ReleasePressDurationMs);
            Assert.AreEqual(123, padAction.CounterMovementReleasePress.MinimumHoldMs);

            string json = JsonConvert.SerializeObject(new TouchpadActionPadSerializer(null, padAction));
            JObject parsed = JObject.Parse(json);
            Assert.AreEqual(true, parsed["Settings"]?["CounterMovementReleasePressEnabled"]?.Value<bool>());
            Assert.AreEqual(77, parsed["Settings"]?["CounterPressLengthMinimumMs"]?.Value<int>());
            Assert.AreEqual(77, parsed["Settings"]?["CounterPressLengthMaximumMs"]?.Value<int>());
            Assert.AreEqual(123, parsed["Settings"]?["CounterMovementMinimumHoldMs"]?.Value<int>());

            TouchpadActionPad parent = new TouchpadActionPad();
            parent.CounterMovementReleasePress.Enabled = true;
            parent.CounterMovementReleasePress.ReleasePressDurationMs = 90;
            parent.CounterMovementReleasePress.MinimumHoldMs = 150;
            parent.TouchDefinition = padAction.TouchDefinition;
            TouchpadActionPad child = new TouchpadActionPad();
            child.SoftCopyFromParent(parent);
            Assert.IsTrue(child.CounterMovementReleasePress.Enabled);
            Assert.AreEqual(90, child.CounterMovementReleasePress.ReleasePressDurationMs);
            Assert.AreEqual(150, child.CounterMovementReleasePress.MinimumHoldMs);
        }
    }
}
