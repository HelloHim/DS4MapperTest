using System;
using System.Collections.Generic;
using System.Reflection;
using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.StickActions;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickAnalogEmulationActionTests : BindingHelperBase
    {
        private const int FULL = 30000;
        private const double DT = 0.008; // ~125Hz report cadence, matches DirectionPulseTime/AnalogEmulationPulseTime defaults' worked examples

        private const uint VK_W = 0x57;
        private const uint VK_A = 0x41;
        private const uint VK_S = 0x53;
        private const uint VK_D = 0x44;

        private VirtualKBMMapping eventInputMapping;

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

        // --- Defaults ---------------------------------------------------------

        [TestMethod]
        public void Defaults_MatchSpec()
        {
            StickAnalogEmulationAction action = new StickAnalogEmulationAction();

            Assert.AreEqual(AnalogEmulationMath.ResolutionMode.Sixteen, action.DirectionMode);
            Assert.AreEqual(30, action.DirectionPulseTimeMs);
            Assert.IsFalse(action.SpeedEmulationEnabled);
            Assert.AreEqual(15, action.SpeedActivePercent);
            Assert.AreEqual(30, action.SpeedPulseTimeMs);
            Assert.AreEqual(80, action.FullSpeedThresholdPercent);
        }

        [TestMethod]
        public void Setters_ClampOutOfRangeValues()
        {
            StickAnalogEmulationAction action = new StickAnalogEmulationAction();

            action.DirectionPulseTimeMs = 5000;
            action.SpeedPulseTimeMs = -10;
            action.SpeedActivePercent = 250;
            action.FullSpeedThresholdPercent = 0;

            Assert.AreEqual(1000, action.DirectionPulseTimeMs);
            Assert.AreEqual(1, action.SpeedPulseTimeMs);
            Assert.AreEqual(100, action.SpeedActivePercent);
            Assert.AreEqual(1, action.FullSpeedThresholdPercent);
        }

        // --- Clone / duplicate --------------------------------------------------

        [TestMethod]
        public void DuplicateAction_PreservesAllSettings()
        {
            StickAnalogEmulationAction original = new StickAnalogEmulationAction();
            original.DirectionMode = AnalogEmulationMath.ResolutionMode.ThirtyTwo;
            original.DirectionPulseTimeMs = 40;
            original.SpeedEmulationEnabled = true;
            original.SpeedActivePercent = 20;
            original.SpeedPulseTimeMs = 50;
            original.FullSpeedThresholdPercent = 70;

            StickAnalogEmulationAction clone = (StickAnalogEmulationAction)original.DuplicateAction();

            Assert.AreEqual(original.DirectionMode, clone.DirectionMode);
            Assert.AreEqual(original.DirectionPulseTimeMs, clone.DirectionPulseTimeMs);
            Assert.AreEqual(original.SpeedEmulationEnabled, clone.SpeedEmulationEnabled);
            Assert.AreEqual(original.SpeedActivePercent, clone.SpeedActivePercent);
            Assert.AreEqual(original.SpeedPulseTimeMs, clone.SpeedPulseTimeMs);
            Assert.AreEqual(original.FullSpeedThresholdPercent, clone.FullSpeedThresholdPercent);
        }

        // --- Combined direction + speed behaviour, driven through TestMapper ----

        private string BuildProfileJson(string directionMode = "Sixteen", bool speedEnabled = false,
            int directionPulseMs = 30, int speedPulseMs = 30, int speedActivePercent = 15,
            int fullSpeedThresholdPercent = 80, bool omitSettings = false)
        {
            string settingsBlock = omitSettings
                ? @"""Settings"": {}"
                : $@"""Settings"": {{
                    ""DirectionMode"": ""{directionMode}"",
                    ""DirectionPulseTimeMs"": {directionPulseMs},
                    ""AnalogSpeedEmulationEnabled"": {speedEnabled.ToString().ToLowerInvariant()},
                    ""AnalogEmulationActivePercent"": {speedActivePercent},
                    ""AnalogEmulationPulseTimeMs"": {speedPulseMs},
                    ""FullSpeedThresholdPercent"": {fullSpeedThresholdPercent}
                }}";

            return @"{
  ""Name"": ""AnalogEmulationTest"",
  ""Description"": ""AnalogEmulationTest"",
  ""Creator"": ""test"",
  ""CreationDate"": ""2026-07-21T00:00:00+0000"",
  ""ActionSets"": [
    {
      ""Index"": 0,
      ""Name"": ""Set 1"",
      ""Description"": ""Only ActionSets"",
      ""ActionLayers"": [
        {
          ""Index"": 0,
          ""Name"": ""Default"",
          ""Description"": ""Only Action Layer"",
          ""MappedActions"": [
            {
              ""Id"": 0,
              ""Name"": ""StickAnalogEmu"",
              ""ActionMode"": ""StickAnalogEmulationAction"",
              ""Bindings"": {
                ""Up"": { ""Name"": ""Up"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""W"" } ] } ] },
                ""Down"": { ""Name"": ""Down"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""S"" } ] } ] },
                ""Left"": { ""Name"": ""Left"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""A"" } ] } ] },
                ""Right"": { ""Name"": ""Right"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""D"" } ] } ] }
              },
              __SETTINGS__
            }
          ]
        }
      ]
    }
  ],
  ""Mappings"": [
    {
      ""ActionSet"": 0,
      ""ActionLayer"": 0,
      ""InputMappings"": [
        { ""Input"": ""Stick"", ""Action"": 0 }
      ]
    }
  ]
}".Replace("__SETTINGS__", settingsBlock);
        }

        private (TestMapper mapper, StickAnalogEmulationAction action) LoadMapper(string directionMode = "Sixteen",
            bool speedEnabled = false, int directionPulseMs = 30, int speedPulseMs = 30,
            int speedActivePercent = 15, int fullSpeedThresholdPercent = 80, bool omitSettings = false)
        {
            eventInputMapping = new SendInputMapping();
            ProfileSerializer.EventInputMapper = eventInputMapping;

            Profile tempProfile = new Profile();
            mapper = new TestMapper(tempProfile);
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mapper, new NoOpVirtualKBM());
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(BuildProfileJson(directionMode, speedEnabled, directionPulseMs,
                speedPulseMs, speedActivePercent, fullSpeedThresholdPercent, omitSettings: omitSettings),
                profileSerializer);
            profileSerializer.PopulateProfile();
            tempProfile.ResetAliases();

            List<ProfileActionsMapping> tempMappings = profileSerializer.ActionMappings;
            FillMappingProfileInitialData(tempProfile, tempMappings);
            SyncActionData(tempProfile);

            mapper.EditActionSet = tempProfile.ActionSets[0];
            mapper.EditLayer = tempProfile.ActionSets[0].ActionLayers[0];

            StickAnalogEmulationAction action =
                tempProfile.ActionSets[0].ActionLayers[0].stickActionDict["Stick"] as StickAnalogEmulationAction;
            return ((TestMapper)mapper, action);
        }

        private static void Report(TestMapper mapper, int lx, int ly, double dt = DT)
        {
            SteamControllerState state = new SteamControllerState()
            {
                LX = (short)lx,
                LY = (short)ly,
                timeElapsed = dt,
            };
            mapper.Reader_Report(state, out IntermediateState _);
        }

        private static void Neutral(TestMapper mapper, double dt = DT)
        {
            Report(mapper, 0, 0, dt);
        }

        private static void HoldAngle(TestMapper mapper, double angleDeg, double magnitudeFraction = 1.0, double dt = DT)
        {
            double rad = angleDeg * Math.PI / 180.0;
            int x = (int)Math.Round(FULL * magnitudeFraction * Math.Sin(rad));
            int y = (int)Math.Round(FULL * magnitudeFraction * Math.Cos(rad));
            Report(mapper, x, y, dt);
        }

        private static bool KeyDown(uint vk) => TestMapper.KeyReferenceCountDict.ContainsKey(vk);

        [TestMethod]
        public void Sixteen_SpeedDisabled_EastHoldsRightOnlyContinuously()
        {
            var (mapper, action) = LoadMapper();

            Neutral(mapper);
            HoldAngle(mapper, 90.0); // East
            HoldAngle(mapper, 90.0);
            HoldAngle(mapper, 90.0);

            Assert.IsTrue(KeyDown(VK_D), "Right must be held at East.");
            Assert.IsFalse(KeyDown(VK_W));
            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsFalse(KeyDown(VK_S));
        }

        [TestMethod]
        public void Sixteen_SpeedDisabled_IntermediateAngle_PrimaryContinuousSecondaryPulses()
        {
            var (mapper, action) = LoadMapper(directionPulseMs: 30);

            Neutral(mapper);
            // NNE (22.5 deg): Up held continuously, Right pulsed at 50% of a 30ms cycle (15ms on/off).
            HoldAngle(mapper, 22.5); // phase = 8ms -> within the 15ms ON window
            Assert.IsTrue(KeyDown(VK_W), "Up must be held continuously through the intermediate sector.");
            Assert.IsTrue(KeyDown(VK_D), "Right should be pulsed ON during the first part of the cycle.");

            HoldAngle(mapper, 22.5); // phase = 16ms -> past the 15ms ON window
            Assert.IsTrue(KeyDown(VK_W), "Up must remain held.");
            Assert.IsFalse(KeyDown(VK_D), "Right should be pulsed OFF during the remainder of the cycle.");
        }

        [TestMethod]
        public void SpeedEnabled_BelowFullSpeedThreshold_GatesMovementOutput()
        {
            // Shallow deflection (well below the 80% full-speed threshold) with speed emulation on
            // must gate East's continuously-held Right key into an on/off duty cycle.
            var (mapper, action) = LoadMapper(speedEnabled: true, speedPulseMs: 30,
                speedActivePercent: 15, fullSpeedThresholdPercent: 80);

            Neutral(mapper);
            HoldAngle(mapper, 90.0, magnitudeFraction: 0.40); // r ~ 0.40/0.80 progress => speedActive = .15 + .85*.5 = .575
            Assert.IsTrue(KeyDown(VK_D), "Movement should be active at the start of the speed pulse cycle.");

            // Continue well past the ON window (duty ~57.5% of 30ms => ~17.25ms ON) to reach the OFF portion.
            for (int i = 0; i < 5; i++) HoldAngle(mapper, 90.0, magnitudeFraction: 0.40);
            Assert.IsFalse(KeyDown(VK_D), "Movement must gate off during the inactive portion of the speed cycle.");
        }

        [TestMethod]
        public void SpeedEnabled_AtOrAboveFullSpeedThreshold_MovementStaysContinuouslyActive()
        {
            var (mapper, action) = LoadMapper(speedEnabled: true, speedPulseMs: 30, fullSpeedThresholdPercent: 80);

            Neutral(mapper);
            for (int i = 0; i < 10; i++) HoldAngle(mapper, 90.0, magnitudeFraction: 1.0);

            Assert.IsTrue(KeyDown(VK_D), "At full deflection (>= threshold) the speed gate must stay continuously active.");
        }

        [TestMethod]
        public void SpeedDisabled_ContinuousMovementRegardlessOfRadius()
        {
            var (mapper, action) = LoadMapper(speedEnabled: false);

            Neutral(mapper);
            for (int i = 0; i < 10; i++) HoldAngle(mapper, 90.0, magnitudeFraction: 0.5);

            Assert.IsTrue(KeyDown(VK_D), "With speed emulation disabled, output must stay active regardless of radius.");
        }

        // --- Release behaviour --------------------------------------------------

        [TestMethod]
        public void CentringStick_ReleasesAllGeneratedOutputs()
        {
            var (mapper, action) = LoadMapper();

            Neutral(mapper);
            HoldAngle(mapper, 22.5);
            HoldAngle(mapper, 22.5);
            Assert.IsTrue(KeyDown(VK_W) || KeyDown(VK_D), "Precondition: some direction output should be active.");

            Neutral(mapper);

            Assert.IsFalse(KeyDown(VK_W));
            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsFalse(KeyDown(VK_S));
            Assert.IsFalse(KeyDown(VK_D));
        }

        [TestMethod]
        public void DirectRelease_ReleasesHeldOutputsAndResetsPhase()
        {
            var (mapper, action) = LoadMapper();

            Neutral(mapper);
            HoldAngle(mapper, 90.0);
            Assert.IsTrue(KeyDown(VK_D));

            action.Release(mapper, ignoreReleaseActions: true);
            mapper.SyncKeyboard();

            Assert.IsFalse(KeyDown(VK_D), "Release must drop any currently-held direction output.");
        }

        // --- Persistence ---------------------------------------------------------

        [TestMethod]
        public void OldProfileMissingNewFields_LoadsWithDefaults()
        {
            var (mapper, action) = LoadMapper(omitSettings: true);

            Assert.AreEqual(AnalogEmulationMath.ResolutionMode.Sixteen, action.DirectionMode);
            Assert.AreEqual(30, action.DirectionPulseTimeMs);
            Assert.IsFalse(action.SpeedEmulationEnabled);
            Assert.AreEqual(15, action.SpeedActivePercent);
            Assert.AreEqual(30, action.SpeedPulseTimeMs);
            Assert.AreEqual(80, action.FullSpeedThresholdPercent);
        }

        [TestMethod]
        public void ProfileValues_RoundTripThroughDeserialization()
        {
            var (mapper, action) = LoadMapper(directionMode: "ThirtyTwo", speedEnabled: true,
                directionPulseMs: 45, speedPulseMs: 60, speedActivePercent: 25, fullSpeedThresholdPercent: 90);

            Assert.AreEqual(AnalogEmulationMath.ResolutionMode.ThirtyTwo, action.DirectionMode);
            Assert.AreEqual(45, action.DirectionPulseTimeMs);
            Assert.IsTrue(action.SpeedEmulationEnabled);
            Assert.AreEqual(60, action.SpeedPulseTimeMs);
            Assert.AreEqual(25, action.SpeedActivePercent);
            Assert.AreEqual(90, action.FullSpeedThresholdPercent);
        }

        [TestMethod]
        public void ProfileValues_OutOfRangeAreClampedSafely()
        {
            string json = BuildProfileJson(directionPulseMs: 5000, speedPulseMs: -5,
                speedActivePercent: 500, fullSpeedThresholdPercent: 0);

            eventInputMapping = new SendInputMapping();
            ProfileSerializer.EventInputMapper = eventInputMapping;

            Profile tempProfile = new Profile();
            mapper = new TestMapper(tempProfile);
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mapper, new NoOpVirtualKBM());
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(json, profileSerializer);
            profileSerializer.PopulateProfile();
            tempProfile.ResetAliases();

            List<ProfileActionsMapping> tempMappings = profileSerializer.ActionMappings;
            FillMappingProfileInitialData(tempProfile, tempMappings);
            SyncActionData(tempProfile);

            StickAnalogEmulationAction action =
                tempProfile.ActionSets[0].ActionLayers[0].stickActionDict["Stick"] as StickAnalogEmulationAction;

            Assert.AreEqual(1000, action.DirectionPulseTimeMs);
            Assert.AreEqual(1, action.SpeedPulseTimeMs);
            Assert.AreEqual(100, action.SpeedActivePercent);
            Assert.AreEqual(1, action.FullSpeedThresholdPercent);
        }
    }
}
