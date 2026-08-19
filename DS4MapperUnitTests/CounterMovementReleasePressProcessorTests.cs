using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.StickActions;
using DS4MapperTest.ViewModels.StickActionPropViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class CounterMovementReleasePressProcessorTests : BindingHelperBase
    {
        // Matches TestMapper's "Stick" StickDefinition (min=-30000, max=30000, mid=0 on
        // both axes), so a circular/elliptical radial magnitude reduces to a single
        // shared scale and cardinal/diagonal full deflection both normalise to ~1.0.
        private const int FULL = 30000;
        private const int DIAG = 21213; // ~30000/sqrt(2)
        private const double DT = 0.008; // ~125Hz report cadence

        private const uint VK_W = 0x57;
        private const uint VK_A = 0x41;
        private const uint VK_S = 0x53;
        private const uint VK_D = 0x44;
        private const uint VK_LEFT = 0x25;
        private const uint VK_UP = 0x26;
        private const uint VK_RIGHT = 0x27;
        private const uint VK_DOWN = 0x28;

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
            // Mapper.keyReferenceCountDict/activeKeys/releasedKeys are static, so a key left
            // pressed by one test's TestMapper instance would otherwise leak into the next.
            TestMapper.KeyReferenceCountDict.Clear();
        }

        [TestMethod]
        public void CounterPressLengthMs_ClampsToOneHundredFiftyMilliseconds()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();

            releasePress.CounterPressLengthMinimumMs = 900;
            releasePress.CounterPressLengthMaximumMs = 900;

            Assert.AreEqual(150, releasePress.CounterPressLengthMinimumMs);
            Assert.AreEqual(150, releasePress.CounterPressLengthMaximumMs);
        }

        [TestMethod]
        public void ArmingThreshold_DefaultIsZero()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();

            Assert.AreEqual(0.0, releasePress.ArmingThreshold);
        }

        [TestMethod]
        public void CounterPressLengthMs_DefaultsToCs2Values()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();

            Assert.AreEqual(78, releasePress.CounterPressLengthMinimumMs);
            Assert.AreEqual(90, releasePress.CounterPressLengthMaximumMs);
        }

        [TestMethod]
        public void CounterPressStartDelayMs_DefaultsMatchSpec()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();

            Assert.AreEqual(0, releasePress.CounterPressStartDelayMinimumMs);
            Assert.AreEqual(0, releasePress.CounterPressStartDelayMaximumMs);
        }

        [TestMethod]
        public void PressLengthPreset_DefaultIsCs2()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();

            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, releasePress.PressLengthPreset);
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, releasePress.EffectivePressLengthPreset);
        }

        [TestMethod]
        public void EffectivePressLengthPreset_IsBidirectional()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, releasePress.EffectivePressLengthPreset);

            releasePress.CounterPressLengthMinimumMs = 60;
            Assert.AreEqual(CounterMovementPressLengthPreset.Custom, releasePress.EffectivePressLengthPreset,
                "Editing away from the CS2 values must switch the displayed preset to Custom.");

            releasePress.CounterPressLengthMinimumMs = 78;
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, releasePress.EffectivePressLengthPreset,
                "Editing back to exactly the CS2 values must switch the displayed preset back to CS2.");
        }

        [TestMethod]
        public void MinimumHoldMs_DefaultIsZero()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();

            Assert.AreEqual(0, releasePress.MinimumHoldMs);
        }

        private string BuildProfileJson(string padMode = "EightWay", double? requiredStickDeflectionThreshold = null)
        {
            string thresholdLine = requiredStickDeflectionThreshold.HasValue
                ? $@",
                ""RequiredStickDeflectionThreshold"": {requiredStickDeflectionThreshold.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
                : string.Empty;

            return @"{
  ""Name"": ""ReleasePressTest"",
  ""Description"": ""ReleasePressTest"",
  ""Creator"": ""test"",
  ""CreationDate"": ""2026-07-20T00:00:00+0000"",
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
              ""Name"": ""StickWASD"",
              ""ActionMode"": ""StickPadAction"",
              ""Bindings"": {
                ""Up"": { ""Name"": ""Up"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""W"" } ] } ] },
                ""Down"": { ""Name"": ""Down"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""S"" } ] } ] },
                ""Left"": { ""Name"": ""Left"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""A"" } ] } ] },
                ""Right"": { ""Name"": ""Right"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""D"" } ] } ] },
                ""UpLeft"": { ""Name"": ""UpLeft"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""W"" }, { ""Type"": ""Keyboard"", ""Code"": ""A"" } ] } ] },
                ""UpRight"": { ""Name"": ""UpRight"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""W"" }, { ""Type"": ""Keyboard"", ""Code"": ""D"" } ] } ] },
                ""DownLeft"": { ""Name"": ""DownLeft"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""S"" }, { ""Type"": ""Keyboard"", ""Code"": ""A"" } ] } ] },
                ""DownRight"": { ""Name"": ""DownRight"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""S"" }, { ""Type"": ""Keyboard"", ""Code"": ""D"" } ] } ] }
              },
              ""Settings"": {
                ""PadMode"": ""EightWay"",
                ""DeadZone"": 0.3,
                ""DiagonalRange"": 45,
                ""BrakeEnabled"": true,
                ""BrakeDurationMs"": 40,
                ""CounterMovementMinimumHoldMs"": 80__REQUIRED_STICK_DEFLECTION_THRESHOLD__
              }
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
}"
                .Replace("__REQUIRED_STICK_DEFLECTION_THRESHOLD__", thresholdLine)
                .Replace(@"""PadMode"": ""EightWay""", $@"""PadMode"": ""{padMode}""");
        }

        private (TestMapper mapper, StickPadAction padAction) LoadMapper(string padMode = "EightWay",
            double? requiredStickDeflectionThreshold = null)
        {
            eventInputMapping = new SendInputMapping();
            ProfileSerializer.EventInputMapper = eventInputMapping;

            Profile tempProfile = new Profile();
            mapper = new TestMapper(tempProfile);
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mapper, new NoOpVirtualKBM());
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(BuildProfileJson(padMode, requiredStickDeflectionThreshold), profileSerializer);
            profileSerializer.PopulateProfile();
            tempProfile.ResetAliases();

            List<ProfileActionsMapping> tempMappings = profileSerializer.ActionMappings;
            FillMappingProfileInitialData(tempProfile, tempMappings);
            SyncActionData(tempProfile);

            mapper.EditActionSet = tempProfile.ActionSets[0];
            mapper.EditLayer = tempProfile.ActionSets[0].ActionLayers[0];

            StickPadAction padAction = tempProfile.ActionSets[0].ActionLayers[0].stickActionDict["Stick"] as StickPadAction;
            return ((TestMapper)mapper, padAction);
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

        private static void HoldUp(TestMapper mapper, int ticks, double dt = DT)
        {
            for (int i = 0; i < ticks; i++) Report(mapper, 0, FULL, dt);
        }

        private static void HoldRight(TestMapper mapper, int ticks, double dt = DT)
        {
            for (int i = 0; i < ticks; i++) Report(mapper, FULL, 0, dt);
        }

        private static void HoldUpRight(TestMapper mapper, int ticks, double dt = DT)
        {
            for (int i = 0; i < ticks; i++) Report(mapper, DIAG, DIAG, dt);
        }

        private static void HoldShallowUp(TestMapper mapper, double fraction, int ticks, double dt = DT)
        {
            for (int i = 0; i < ticks; i++) Report(mapper, 0, (int)(FULL * fraction), dt);
        }

        private static bool KeyDown(uint vk) => TestMapper.KeyReferenceCountDict.ContainsKey(vk);

        [TestMethod]
        [DataRow("Standard")]
        [DataRow("EightWay")]
        [DataRow("FourWayCardinal")]
        [DataRow("FourWayDiagonal")]
        public void CounterMovementReleasePressSettings_AreVisibleForEveryDPadMode(string padMode)
        {
            var (mapper, padAction) = LoadMapper(padMode);

            StickPadActionPropViewModel vm = new StickPadActionPropViewModel(mapper, padAction);

            Assert.IsTrue(vm.ShowCounterMovementReleasePressSection, $"Counter Movement Release Press controls must be visible in {padMode} mode.");
        }

        [TestMethod]
        public void EnablingForTheFirstTime_AlwaysLandsOnWaitVariancePercentageAndCs2()
        {
            var (mapper, padAction) = LoadMapper();
            StickPadActionPropViewModel vm = new StickPadActionPropViewModel(mapper, padAction);

            // LoadMapper's fixture profile is legacy-format (BrakeEnabled/BrakeDurationMs), so
            // it loads already enabled; disable it first so the assertion below exercises a
            // genuine off-to-on transition rather than a same-value no-op.
            vm.CounterMovementReleasePressEnabled = false;

            // Perturb the mode and values away from the CS2/Wait Variance Percentage
            // combination before enabling, to prove enabling forces both back regardless of
            // whatever was previously configured.
            vm.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;
            vm.CounterPressLengthMinimumMs = 40;
            vm.CounterPressLengthMaximumMs = 60;

            vm.CounterMovementReleasePressEnabled = true;

            Assert.AreEqual(CounterPressLengthMode.WaitVariancePercentage, vm.CounterPressLengthMode,
                "Enabling for the first time must always land on Wait Variance Percentage mode.");
            Assert.AreEqual(CounterMovementPressLengthPreset.CS2, vm.PressLengthPreset);
            Assert.AreEqual(84, vm.CounterPressLengthMs);
            Assert.AreEqual(7, vm.CounterPressLengthVariancePercent);
            Assert.AreEqual(78, vm.CounterPressLengthMinimumMs);
            Assert.AreEqual(90, vm.CounterPressLengthMaximumMs);
        }

        [TestMethod]
        [DataRow("Standard")]
        [DataRow("EightWay")]
        [DataRow("FourWayCardinal")]
        [DataRow("FourWayDiagonal")]
        public void FastCardinalRelease_FiresOppositeInEveryDPadMode(string padMode)
        {
            var (mapper, padAction) = LoadMapper(padMode);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State,
                $"Expected counter press active in {padMode} mode.");
            Assert.IsFalse(KeyDown(VK_W), $"Original Up key must be released in {padMode} mode.");
            Assert.IsTrue(KeyDown(VK_S), $"Opposite Down key must be pressed in {padMode} mode.");
        }

        [TestMethod]
        public void MovementBelowConfiguredArmingThreshold_DoesNotArm()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.ArmingThreshold = 0.75;

            Neutral(mapper);
            HoldShallowUp(mapper, 0.50, 20);

            Assert.IsTrue(KeyDown(VK_W), "The digital direction should be active through normal D-Pad logic.");
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Idle, padAction.CounterMovementReleasePress.State);

            Report(mapper, 0, 0);

            Assert.AreNotEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_S), "A below-threshold movement must not arm and release press on release.");
        }

        [TestMethod]
        public void MovementReachingConfiguredArmingThreshold_Arms()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.ArmingThreshold = 0.50;

            Neutral(mapper);
            HoldShallowUp(mapper, 0.60, 20);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed, padAction.CounterMovementReleasePress.State);
        }

        [TestMethod]
        public void ZeroArmingThreshold_ValidDigitalDirectionArmsWithShallowDeflection()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.ArmingThreshold = 0.0;

            Neutral(mapper);
            HoldShallowUp(mapper, 0.35, 1);

            Assert.IsTrue(KeyDown(VK_W), "Normal D-Pad logic must have activated Up.");
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed, padAction.CounterMovementReleasePress.State);
        }

        [TestMethod]
        public void ZeroArmingThreshold_CentreJitterWithoutDigitalDirectionDoesNotArm()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.ArmingThreshold = 0.0;

            Neutral(mapper);
            Report(mapper, 100, -100);
            Report(mapper, -120, 80);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Idle, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_W));
            Assert.IsFalse(KeyDown(VK_A));
            Assert.IsFalse(KeyDown(VK_S));
            Assert.IsFalse(KeyDown(VK_D));
        }

        [TestMethod]
        public void MinimumHoldTime_StillAppliesAfterZeroThresholdArming()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.ArmingThreshold = 0.0;

            Neutral(mapper);
            HoldShallowUp(mapper, 0.35, 4);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed, padAction.CounterMovementReleasePress.State);

            Report(mapper, 0, 0);

            Assert.IsFalse(KeyDown(VK_S), "The release press must still wait for MinimumHoldMs after arming.");
        }

        [TestMethod]
        public void ChangingArmingThreshold_DoesNotChangePressLengthRange()
        {
            CounterMovementReleasePressProcessor releasePress = new CounterMovementReleasePressProcessor();
            releasePress.CounterPressLengthMinimumMs = 60;
            releasePress.CounterPressLengthMaximumMs = 123;

            releasePress.ArmingThreshold = 0.25;

            Assert.AreEqual(60, releasePress.CounterPressLengthMinimumMs);
            Assert.AreEqual(123, releasePress.CounterPressLengthMaximumMs);
        }

        [TestMethod]
        [DataRow("Standard")]
        [DataRow("EightWay")]
        [DataRow("FourWayCardinal")]
        [DataRow("FourWayDiagonal")]
        public void AllDPadModes_UseConfiguredZeroArmingThreshold(string padMode)
        {
            var (mapper, padAction) = LoadMapper(padMode);
            padAction.CounterMovementReleasePress.ArmingThreshold = 0.0;

            Neutral(mapper);
            HoldShallowUp(mapper, 0.35, 1);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed, padAction.CounterMovementReleasePress.State,
                $"{padMode} should use the configured arming threshold.");
        }

        [TestMethod]
        [DataRow("Standard")]
        [DataRow("EightWay")]
        [DataRow("FourWayDiagonal")]
        public void FastDiagonalRelease_FiresBothOppositeComponentsInDiagonalModes(string padMode)
        {
            var (mapper, padAction) = LoadMapper(padMode);

            Neutral(mapper);
            HoldUpRight(mapper, 20);
            Report(mapper, 0, 0);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State,
                $"Expected counter press active in {padMode} mode.");
            Assert.IsTrue(KeyDown(VK_S), $"Opposite of Up must be Down (S) in {padMode} mode.");
            Assert.IsTrue(KeyDown(VK_A), $"Opposite of Right must be Left (A) in {padMode} mode.");
        }

        [TestMethod]
        public void FastCardinalRelease_FiresOnceAndSuppressesOldDirection()
        {
            var (mapper, padAction) = LoadMapper();

            Neutral(mapper);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Idle, padAction.CounterMovementReleasePress.State);

            // Arm and hold Up (W) well past the arm-settle time and MinimumHoldMs (80ms).
            HoldUp(mapper, 20);
            Assert.IsTrue(KeyDown(VK_W));
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed, padAction.CounterMovementReleasePress.State);

            // Fast full release.
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_W), "Original direction must be released immediately.");
            Assert.IsTrue(KeyDown(VK_S), "Opposite direction (S) must be pressed by the release press.");

            // Stick stays at rest (spring already settled in this synthetic trace); pulse
            // must still expire after legacy BrakeDurationMs and S must be released while W stays
            // suppressed until neutral.
            for (int i = 0; i < 10 && padAction.CounterMovementReleasePress.State != CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed; i++)
            {
                Report(mapper, 0, 0);
            }
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_S), "Release press pulse must release S after legacy BrakeDurationMs.");
            Assert.IsFalse(KeyDown(VK_W), "Old direction must remain suppressed until neutral.");
        }

        [TestMethod]
        public void FastDiagonalRelease_EmitsBothOppositeComponents()
        {
            var (mapper, padAction) = LoadMapper();

            Neutral(mapper);
            HoldUpRight(mapper, 20);
            Assert.IsTrue(KeyDown(VK_W) && KeyDown(VK_D));

            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S), "Opposite of Up is Down (S).");
            Assert.IsTrue(KeyDown(VK_A), "Opposite of Right is Left (A).");
        }

        [TestMethod]
        public void ArrowKeyMode_UsesFixedOppositeArrowsWithoutChangingMovementBinds()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = true;

            Neutral(mapper);
            HoldUp(mapper, 20);
            Assert.IsTrue(KeyDown(VK_W), "Normal Up movement must keep using W.");
            Report(mapper, 0, 0);

            Assert.IsTrue(KeyDown(VK_DOWN));
            Assert.IsFalse(KeyDown(VK_S));
        }

        [TestMethod]
        public void ArrowKeyMode_DiagonalReleaseOwnsBothArrowComponents()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = true;

            Neutral(mapper);
            HoldUpRight(mapper, 20);
            Report(mapper, 0, 0);

            Assert.IsTrue(KeyDown(VK_DOWN));
            Assert.IsTrue(KeyDown(VK_LEFT));
            Assert.IsFalse(KeyDown(VK_S));
            Assert.IsFalse(KeyDown(VK_A));
        }

        [TestMethod]
        public void ArrowKeyMode_ChangeDuringPulseCancelsOwnedArrowOutput()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = true;

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.IsTrue(KeyDown(VK_DOWN));

            padAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = false;
            Report(mapper, 0, 0);

            Assert.IsFalse(KeyDown(VK_DOWN));
            Assert.AreNotEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive,
                padAction.CounterMovementReleasePress.State);
        }

        [TestMethod]
        public void AllEightZones_MapToCorrectOpposite()
        {
            (int lx, int ly, uint origKey1, uint origKey2, uint oppKey1, uint oppKey2)[] cases = new[]
            {
                (0, FULL, VK_W, (uint)0, VK_S, (uint)0),
                (0, -FULL, VK_S, (uint)0, VK_W, (uint)0),
                (-FULL, 0, VK_A, (uint)0, VK_D, (uint)0),
                (FULL, 0, VK_D, (uint)0, VK_A, (uint)0),
                (DIAG, DIAG, VK_W, VK_D, VK_S, VK_A),
                (DIAG, -DIAG, VK_S, VK_D, VK_W, VK_A),
                (-DIAG, DIAG, VK_W, VK_A, VK_S, VK_D),
                (-DIAG, -DIAG, VK_S, VK_A, VK_W, VK_D),
            };

            foreach (var c in cases)
            {
                var (mapper, padAction) = LoadMapper();
                Neutral(mapper);
                for (int i = 0; i < 20; i++) Report(mapper, c.lx, c.ly);
                Report(mapper, 0, 0);

                Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State,
                    $"Expected counter press active for ({c.lx},{c.ly})");
                Assert.IsTrue(KeyDown(c.oppKey1), $"Missing opposite key for ({c.lx},{c.ly})");
                if (c.oppKey2 != 0)
                {
                    Assert.IsTrue(KeyDown(c.oppKey2), $"Missing second opposite key for ({c.lx},{c.ly})");
                }
            }
        }

        [TestMethod]
        public void SlowEasedRelease_TriggersViaFallback()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);

            // Ease back to centre gradually (well under the derivative threshold) over ~50 ticks.
            for (int i = 50; i >= 0 && padAction.CounterMovementReleasePress.State == CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed; i--)
            {
                int ly = (int)(FULL * (i / 50.0));
                Report(mapper, 0, ly);
            }

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State,
                "Slow release must still trigger via the neutral-crossing fallback.");
            Assert.IsTrue(KeyDown(VK_S));
        }

        [TestMethod]
        public void ThumbRelaxation_DoesNotTrigger()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);

            // Gradually settle 1.00 -> 0.90 over 160ms while still clearly holding Up.
            for (int i = 0; i < 20; i++)
            {
                double frac = 1.0 - (0.10 * (i / 19.0));
                Report(mapper, 0, (int)(FULL * frac));
            }

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed, padAction.CounterMovementReleasePress.State,
                "A small sustained relaxation must not be treated as a release.");
            Assert.IsTrue(KeyDown(VK_W));
        }

        [TestMethod]
        public void RimArc_DoesNotTrigger()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldRight(mapper, 20);

            // Arc from Right (D) through UpRight (W+D) to Up (W) at constant radius.
            for (int step = 0; step <= 18; step++)
            {
                double angleDeg = 90.0 - (90.0 * step / 18.0);
                double rad = angleDeg * Math.PI / 180.0;
                int x = (int)(FULL * Math.Sin(rad));
                int y = (int)(FULL * Math.Cos(rad));
                Report(mapper, x, y);

                Assert.AreNotEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State,
                    $"Rim arc must not trigger a release press at step {step}");
            }
        }

        [TestMethod]
        public void IdleJitter_DoesNotArm()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);

            Random rnd = new Random(42);
            for (int i = 0; i < 30; i++)
            {
                Report(mapper, rnd.Next(-500, 500), rnd.Next(-500, 500));
                Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Idle, padAction.CounterMovementReleasePress.State);
            }
        }

        [TestMethod]
        public void OnePulsePerRelease_ContinuousReturnFiresOnce()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);

            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);

            int releasePressObservations = 1;
            for (int i = 0; i < 40; i++)
            {
                Report(mapper, 0, 0);
                if (padAction.CounterMovementReleasePress.State == CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive)
                {
                    releasePressObservations++;
                }
            }

            // Only the single contiguous CounterPressActive run from the one release should ever occur;
            // once Suppressed/Idle is reached it must not re-enter CounterPressActive without a fresh push.
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Idle, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(releasePressObservations < 40, "Release press re-armed and fired more than once for a single release.");
        }

        [TestMethod]
        public void OldDirectionSuppression_HoldsAcrossReports()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);

            for (int i = 0; i < 5; i++)
            {
                // Physical stick still reads a lingering Up value while springing back.
                Report(mapper, 0, FULL / 4);
                Assert.IsFalse(KeyDown(VK_W), "Old direction must stay suppressed during spring return.");
            }
        }

        [TestMethod]
        public void RepeekOriginalDirection_CancelsReleasePressAndRestoresCleanly()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldRight(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_A));

            // Deliberately push D again.
            for (int i = 0; i < 6; i++)
            {
                Report(mapper, FULL, 0);
            }

            Assert.IsTrue(KeyDown(VK_D), "Renewed D push must be restored.");
            Assert.IsFalse(KeyDown(VK_A), "Cancelled release press pulse must not leave A stuck.");
        }

        [TestMethod]
        public void ReverseIntoReleasePressDirection_TransfersOwnershipWithoutGap()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldRight(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_A));

            // Deliberately push A (left) — the same key the release press is already holding.
            for (int i = 0; i < 6; i++)
            {
                Report(mapper, -FULL, 0);
                Assert.IsTrue(KeyDown(VK_A), "A must remain continuously held through ownership handover.");
            }
        }

        [TestMethod]
        public void ShortCardinalTap_DoesNotReleasePress()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);

            // Arm, then release almost immediately (well under MinimumHoldMs=80ms).
            HoldRight(mapper, 4);
            Report(mapper, 0, 0);

            Assert.IsFalse(KeyDown(VK_A), "Short tap under MinimumHoldMs must not release press.");
        }

        [TestMethod]
        public void MixedDurationDiagonal_OnlyEligibleComponentReleasePresses()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);

            // Hold D (Right) well past MinimumHoldMs, then add W (Up) for only ~16ms.
            HoldRight(mapper, 20);
            HoldUpRight(mapper, 2);
            Report(mapper, 0, 0);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_A), "Right was held long enough; A (opposite) must fire.");
            Assert.IsFalse(KeyDown(VK_S), "Up was only added briefly; S (opposite) must not fire.");
        }

        [TestMethod]
        public void InvalidDtSample_DoesNotTriggerOrCorruptState()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);

            // A hitched/duplicate report with zero dt, same physical position, must be
            // ignored, not crash, and not release press.
            Report(mapper, 0, FULL, 0.0);
            Assert.AreNotEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);

            // A dropped-report style huge dt (still no real release) must also be rejected.
            Report(mapper, 0, FULL, 5.0);
            Assert.AreNotEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);

            // Normal operation must still work afterwards.
            HoldUp(mapper, 10);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
        }

        [TestMethod]
        public void EnableWhileHeld_StaysUnprimedUntilNeutral()
        {
            var (mapper, padAction) = LoadMapper();

            // First-ever report already holds the stick — no neutral warm-up.
            HoldUp(mapper, 30);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Unprimed, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_W), "Normal output must continue while Unprimed.");

            for (int i = 0; i < 5; i++) Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Idle, padAction.CounterMovementReleasePress.State);

            // Now a genuine push-and-release cycle must release press normally.
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
        }

        [TestMethod]
        public void DisableMidPulse_ReleasesPulseOwnedKeysAndClearsSuppression()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S));

            padAction.CounterMovementReleasePress.Enabled = false;
            Report(mapper, 0, 0);

            Assert.IsFalse(KeyDown(VK_S), "Disabling mid-pulse must release the release-press-owned key.");
            Assert.IsFalse(KeyDown(VK_W), "No key should be left stuck.");
        }

        [TestMethod]
        public void ReleaseDuringPulse_LeavesNoStuckKeys()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S));

            // Simulate controller disconnect / profile unload: the action gets released
            // directly (outside the normal per-report cycle), then synced like Mapper.Stop()
            // and Mapper.ChangeProfile() do.
            padAction.Release(mapper);
            mapper.SyncKeyboard();

            Assert.IsFalse(KeyDown(VK_S), "Release() must flush the pulse-owned key.");
            Assert.IsFalse(KeyDown(VK_W));
        }

        [TestMethod]
        public void InvalidDtDuringPulse_StillExpiresByWallClock()
        {
            var (mapper, padAction) = LoadMapper();
            // LoadMapper's fixture carries a legacy BrakeDurationMs field, which migrates the
            // action to Fixed mode; Minimum/Maximum are only consulted outside Fixed mode, so
            // the mode must be switched explicitly for the 10ms window below to take effect.
            padAction.CounterMovementReleasePress.CounterPressLengthMode = CounterPressLengthMode.MinimumAndMaximum;
            padAction.CounterMovementReleasePress.CounterPressLengthMinimumMs = 10;
            padAction.CounterMovementReleasePress.CounterPressLengthMaximumMs = 10;

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.CounterPressActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S));

            Thread.Sleep(25);
            Report(mapper, 0, 0, 0.0);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_S), "Invalid report dt must not keep the release-press-owned key held indefinitely.");
        }

        [TestMethod]
        public void ArmingThreshold_ProfileSaveLoadInheritanceAndReset()
        {
            var (_, loadedAction) = LoadMapper(requiredStickDeflectionThreshold: 0.25);
            Assert.AreEqual(0.25, loadedAction.CounterMovementReleasePress.ArmingThreshold);
            Assert.IsTrue(loadedAction.ChangedProperties.Contains(
                StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD));

            StickPadAction actionToSave = new StickPadAction();
            actionToSave.Id = 7;
            actionToSave.CounterMovementReleasePress.ArmingThreshold = 0.35;
            actionToSave.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
            string json = JsonConvert.SerializeObject(new StickPadActionSerializer(null, actionToSave));
            JObject parsed = JObject.Parse(json);
            Assert.AreEqual(0.35, parsed["Settings"]?["RequiredStickDeflectionThreshold"]?.Value<double>());

            StickPadAction parent = new StickPadAction();
            parent.CounterMovementReleasePress.ArmingThreshold = 0.25;
            StickPadAction child = new StickPadAction();
            child.SoftCopyFromParent(parent);
            Assert.AreEqual(0.25, child.CounterMovementReleasePress.ArmingThreshold);

            parent.CounterMovementReleasePress.ArmingThreshold = 0.45;
            parent.RaiseNotifyPropertyChange(null, StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
            Assert.AreEqual(0.45, child.CounterMovementReleasePress.ArmingThreshold);

            child.CounterMovementReleasePress.ArmingThreshold = 0.20;
            child.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
            parent.CounterMovementReleasePress.ArmingThreshold = 0.60;
            parent.RaiseNotifyPropertyChange(null, StickPadAction.PropertyKeyStrings.REQUIRED_STICK_DEFLECTION_THRESHOLD);
            Assert.AreEqual(0.20, child.CounterMovementReleasePress.ArmingThreshold);

            child.CounterMovementReleasePress.ArmingThreshold = CounterMovementReleasePressProcessor.DEFAULT_ARMING_THRESHOLD;
            Assert.AreEqual(CounterMovementReleasePressProcessor.DEFAULT_ARMING_THRESHOLD, child.CounterMovementReleasePress.ArmingThreshold);
        }

        [TestMethod]
        public void ArrowKeyMode_DefaultSerialisationAndInheritanceArePreserved()
        {
            CounterMovementReleasePressProcessor defaults = new CounterMovementReleasePressProcessor();
            Assert.IsFalse(defaults.UseArrowKeysForCounterMovementPresses);

            StickPadAction action = new StickPadAction();
            action.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = true;
            action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
            string json = JsonConvert.SerializeObject(new StickPadActionSerializer(null, action));
            Assert.IsTrue(JObject.Parse(json)["Settings"]?["UseArrowKeysForCounterMovementPresses"]?.Value<bool>());

            StickPadAction parent = new StickPadAction();
            parent.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = true;
            StickPadAction child = new StickPadAction();
            child.SoftCopyFromParent(parent);
            Assert.IsTrue(child.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses);

            parent.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = false;
            parent.RaiseNotifyPropertyChange(null, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
            Assert.IsFalse(child.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses);
        }

        [TestMethod]
        public void ProfileWithoutArmingThreshold_UsesCurrentDefaultBehaviour()
        {
            var (mapper, padAction) = LoadMapper();

            Assert.AreEqual(CounterMovementReleasePressProcessor.DEFAULT_ARMING_THRESHOLD, padAction.CounterMovementReleasePress.ArmingThreshold);

            Neutral(mapper);
            HoldShallowUp(mapper, 0.50, 20);
            // With a 0% default arming threshold, any digital direction activation arms immediately.
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Armed, padAction.CounterMovementReleasePress.State);
        }

        [TestMethod]
        public void ArrowKeyMode_ReverseIntoReleasePressDirection_ReleasesTheOwnedArrow()
        {
            var (mapper, padAction) = LoadMapper();
            padAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = true;
            Neutral(mapper);

            // Push Up and release: the release press owns the Down arrow.
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.IsTrue(KeyDown(VK_DOWN));

            // Reverse into the release press's own direction while it is still pulsing. With the
            // normal (movement bind) output the release press silently hands the key over to the
            // masked normal path; with arrow output the normal path presses S instead, so
            // the arrow the release press owns has to be released rather than just disowned.
            for (int i = 0; i < 40; i++) Report(mapper, 0, -FULL);
            Assert.IsTrue(KeyDown(VK_S), "Reversing into the release press direction must press the normal Down bind.");
            Assert.IsFalse(KeyDown(VK_DOWN), "The handed-off arrow must not stay held once the normal bind takes over.");

            for (int i = 0; i < 40; i++) Report(mapper, 0, 0);
            Assert.IsFalse(KeyDown(VK_DOWN), "The Down arrow must not be left stuck after the stick recentres.");
        }
    }
}
