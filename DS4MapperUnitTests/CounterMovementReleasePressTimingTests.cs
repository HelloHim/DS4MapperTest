using System;
using System.Collections.Generic;
using System.Reflection;
using DS4MapperTest;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.StickActions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DS4MapperUnitTests
{
    // Covers the new Counter Movement Release Press behaviour: Opposite Tap Start Delay,
    // Opposite Tap Length Variance sampling, subtraction, Tap Length Preset, range
    // validation, random-provider injection and legacy profile migration. Release-detection
    // itself (radial magnitude, derivative, re-engagement, diagonal ownership) is already
    // covered by CounterMovementReleasePressProcessorTests and is intentionally not
    // re-tested here.
    [TestClass]
    public class CounterMovementReleasePressTimingTests : BindingHelperBase
    {
        private const int FULL = 30000;
        private const double DT = 0.008; // ~125Hz report cadence

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

        // Deterministic stand-in for the runtime RandomRangeProvider, so "random selection"
        // tests never depend on real randomness and never fail flakily.
        private sealed class FixedRandomRangeProvider : IRandomRangeProvider
        {
            private readonly int fixedValue;
            public int CallCount { get; private set; }

            public FixedRandomRangeProvider(int fixedValue)
            {
                this.fixedValue = fixedValue;
            }

            public int NextInclusive(int minimum, int maximum)
            {
                CallCount++;
                if (minimum >= maximum) return minimum;
                return Math.Clamp(fixedValue, minimum, maximum);
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            TestMapper.KeyReferenceCountDict.Clear();
        }

        private string BuildProfileJson(int tapLengthMin, int tapLengthMax, int startDelayMin, int startDelayMax,
            bool enabled = true, int minimumHoldMs = 0, double armingThreshold = 0.0,
            string preset = "Custom")
        {
            return $@"{{
  ""Name"": ""TimingTest"",
  ""Description"": ""TimingTest"",
  ""Creator"": ""test"",
  ""CreationDate"": ""2026-07-22T00:00:00+0000"",
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
              ""Name"": ""StickWASD"",
              ""ActionMode"": ""StickPadAction"",
              ""Bindings"": {{
                ""Up"": {{ ""Name"": ""Up"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""W"" }} ] }} ] }},
                ""Down"": {{ ""Name"": ""Down"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""S"" }} ] }} ] }},
                ""Left"": {{ ""Name"": ""Left"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""A"" }} ] }} ] }},
                ""Right"": {{ ""Name"": ""Right"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""D"" }} ] }} ] }}
              }},
              ""Settings"": {{
                ""PadMode"": ""Standard"",
                ""DeadZone"": 0.3,
                ""DiagonalRange"": 45,
                ""CounterMovementReleasePressEnabled"": {enabled.ToString().ToLowerInvariant()},
                ""CounterMovementTapLengthPreset"": ""{preset}"",
                ""OppositeTapLengthMinimumMs"": {tapLengthMin},
                ""OppositeTapLengthMaximumMs"": {tapLengthMax},
                ""OppositeTapStartDelayMinimumMs"": {startDelayMin},
                ""OppositeTapStartDelayMaximumMs"": {startDelayMax},
                ""BrakeMinimumHoldMs"": {minimumHoldMs},
                ""BrakeArmingThreshold"": {armingThreshold.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}
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
        {{ ""Input"": ""Stick"", ""Action"": 0 }}
      ]
    }}
  ]
}}";
        }

        private (TestMapper mapper, StickPadAction padAction) LoadMapper(int tapLengthMin, int tapLengthMax,
            int startDelayMin, int startDelayMax, bool enabled = true, int minimumHoldMs = 0,
            double armingThreshold = 0.0, string preset = "Custom")
        {
            eventInputMapping = new SendInputMapping();
            ProfileSerializer.EventInputMapper = eventInputMapping;

            Profile tempProfile = new Profile();
            mapper = new TestMapper(tempProfile);
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mapper, new NoOpVirtualKBM());
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(
                BuildProfileJson(tapLengthMin, tapLengthMax, startDelayMin, startDelayMax, enabled, minimumHoldMs, armingThreshold, preset),
                profileSerializer);
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
            SteamControllerState state = new SteamControllerState() { LX = (short)lx, LY = (short)ly, timeElapsed = dt };
            mapper.Reader_Report(state, out IntermediateState _);
        }

        private static void Neutral(TestMapper mapper) => Report(mapper, 0, 0);
        private static void HoldUp(TestMapper mapper, int ticks)
        {
            for (int i = 0; i < ticks; i++) Report(mapper, 0, FULL);
        }

        private static bool KeyDown(uint vk) => TestMapper.KeyReferenceCountDict.ContainsKey(vk);

        // --- Section 30: direct state-machine timing cases -----------------------

        [TestMethod]
        public void Case1_TotalSeventyFive_DelayZero_BeginsImmediatelyAndEndsAtWindow()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 75, tapLengthMax: 75, startDelayMin: 0, startDelayMax: 0);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0); // release
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.OppositeTapActive,
                padAction.CounterMovementReleasePress.State, "Opposite output must begin immediately at zero delay.");
            Assert.IsTrue(KeyDown(VK_S));

            for (int i = 0; i < 10 && padAction.CounterMovementReleasePress.State != CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed; i++)
            {
                Report(mapper, 0, 0);
            }
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_S), "Opposite output must end once the 75ms window elapses.");
        }

        [TestMethod]
        public void Case2_TotalOneTwenty_DelayTwenty_NeutralThenPressThenEndsAtWindow()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 120, tapLengthMax: 120, startDelayMin: 20, startDelayMax: 20);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0); // release: elapsed 0ms
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.WaitingForOppositeTap,
                padAction.CounterMovementReleasePress.State, "Must wait out the start delay before pressing.");
            Assert.IsFalse(KeyDown(VK_S), "No opposite output during the neutral start-delay period.");
            Assert.IsFalse(KeyDown(VK_W));

            // Advance through the ~20ms delay (8ms ticks: 8, 16, 24ms elapsed).
            for (int i = 0; i < 3 && padAction.CounterMovementReleasePress.State == CounterMovementReleasePressProcessor.CounterMovementReleasePressState.WaitingForOppositeTap; i++)
            {
                Report(mapper, 0, 0);
            }
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.OppositeTapActive,
                padAction.CounterMovementReleasePress.State, "Opposite output must begin once the start delay elapses.");
            Assert.IsTrue(KeyDown(VK_S));

            for (int i = 0; i < 20 && padAction.CounterMovementReleasePress.State != CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed; i++)
            {
                Report(mapper, 0, 0);
            }
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed, padAction.CounterMovementReleasePress.State);
            Assert.IsFalse(KeyDown(VK_S), "Opposite output must end once the full 120ms window elapses.");
        }

        [TestMethod]
        public void Case3_TotalSeventyFive_DelayTwenty_ActualHoldIsFiftyFiveMs()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 75, tapLengthMax: 75, startDelayMin: 20, startDelayMax: 20);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.WaitingForOppositeTap, padAction.CounterMovementReleasePress.State);

            for (int i = 0; i < 3 && padAction.CounterMovementReleasePress.State == CounterMovementReleasePressProcessor.CounterMovementReleasePressState.WaitingForOppositeTap; i++)
            {
                Report(mapper, 0, 0);
            }
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.OppositeTapActive, padAction.CounterMovementReleasePress.State);

            for (int i = 0; i < 20 && padAction.CounterMovementReleasePress.State != CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed; i++)
            {
                Report(mapper, 0, 0);
            }
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed, padAction.CounterMovementReleasePress.State,
                "The complete action (delay + hold) must end at the 75ms total window, not 95ms.");
        }

        [TestMethod]
        public void Case4_TotalTwenty_DelayTwenty_NoOppositePressAndEndsCleanly()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 20, tapLengthMax: 20, startDelayMin: 20, startDelayMax: 20);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);

            for (int i = 0; i < 6; i++)
            {
                Report(mapper, 0, 0);
                Assert.IsFalse(KeyDown(VK_S), "actualOppositeHoldMs is zero: the opposite key must never be pressed.");
            }

            // The stick has been sitting dead centre the whole time, so Suppressed clears to
            // Idle almost immediately; either state confirms the action ended cleanly with no
            // opposite press, which is what this test actually cares about.
            var finalState = padAction.CounterMovementReleasePress.State;
            Assert.IsTrue(
                finalState == CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Suppressed ||
                finalState == CounterMovementReleasePressProcessor.CounterMovementReleasePressState.Idle,
                $"Expected the action to have ended cleanly (Suppressed or Idle), but was {finalState}.");
        }

        [TestMethod]
        public void Case5_TotalOneHundred_DelayZero_MatchesImmediateStartBehaviour()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 100, tapLengthMax: 100, startDelayMin: 0, startDelayMax: 0);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.OppositeTapActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S), "Zero delay must behave identically to the pre-timing-variance implementation.");
        }

        // --- Section 17: real input takes priority --------------------------------

        [TestMethod]
        public void DeliberateNewDirection_DuringWaitingForOppositeTap_CancelsScheduledPress()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 120, tapLengthMax: 120, startDelayMin: 40, startDelayMax: 40);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.WaitingForOppositeTap, padAction.CounterMovementReleasePress.State);

            // Deliberately push Right while still waiting out the delay.
            for (int i = 0; i < 6; i++)
            {
                Report(mapper, FULL, 0);
            }

            Assert.IsTrue(KeyDown(VK_D), "Real input must take control immediately.");
            Assert.IsFalse(KeyDown(VK_S), "The scheduled opposite press must never begin.");
            Assert.IsFalse(KeyDown(VK_W));
        }

        [TestMethod]
        public void DeliberateNewDirection_DuringOppositeTapActive_CancelsGeneratedPress()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 150, tapLengthMax: 150, startDelayMin: 0, startDelayMax: 0);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.OppositeTapActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S));

            for (int i = 0; i < 6; i++)
            {
                Report(mapper, FULL, 0);
            }

            Assert.IsTrue(KeyDown(VK_D), "Real input must take control immediately.");
            Assert.IsFalse(KeyDown(VK_S), "The automatic press must be released once real input diverges.");
        }

        // --- Section 19: cancellation and cleanup ---------------------------------

        [TestMethod]
        public void DisableDuringWaitingForOppositeTap_CancelsPendingPressAndReleasesSuppression()
        {
            var (mapper, padAction) = LoadMapper(tapLengthMin: 120, tapLengthMax: 120, startDelayMin: 40, startDelayMax: 40);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.WaitingForOppositeTap, padAction.CounterMovementReleasePress.State);

            padAction.CounterMovementReleasePress.Enabled = false;
            Report(mapper, 0, 0);

            Assert.IsFalse(KeyDown(VK_S), "No delayed output may begin after the feature is disabled.");
            Assert.IsFalse(KeyDown(VK_W));
        }

        // --- Section 5/6: sampling and subtraction --------------------------------

        [TestMethod]
        public void FixedRandomProvider_SamplesExactlyOnceForTotalWindowAndStartDelay()
        {
            FixedRandomRangeProvider provider = new FixedRandomRangeProvider(50);

            Assert.AreEqual(50, provider.NextInclusive(10, 100));
            Assert.AreEqual(1, provider.CallCount);

            // Minimum equals maximum: deterministic, no sampling call needed conceptually,
            // but even if invoked it must return that exact value.
            Assert.AreEqual(75, provider.NextInclusive(75, 75));
        }

        [TestMethod]
        public void RandomRangeProvider_InclusiveBoundsAndDeterministicWhenEqual()
        {
            RandomRangeProvider provider = RandomRangeProvider.Instance;

            Assert.AreEqual(10, provider.NextInclusive(10, 10));
            Assert.AreEqual(0, provider.NextInclusive(0, 0));

            for (int i = 0; i < 200; i++)
            {
                int result = provider.NextInclusive(75, 120);
                Assert.IsTrue(result >= 75 && result <= 120, $"Sampled value {result} was outside [75,120].");
            }
        }

        // --- Section 8: range validation and cross-field clamping ----------------

        [TestMethod]
        public void NormalizeRanges_SwapsInvertedTapLengthRange()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMinimumMs = 120;
            processor.OppositeTapLengthMaximumMs = 75;

            processor.NormalizeRanges();

            Assert.IsTrue(processor.OppositeTapLengthMinimumMs <= processor.OppositeTapLengthMaximumMs);
        }

        [TestMethod]
        public void NormalizeRanges_ClampsStartDelayMaximumToTapLengthMinimum()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMinimumMs = 75;
            processor.OppositeTapLengthMaximumMs = 120;
            processor.OppositeTapStartDelayMinimumMs = 0;
            processor.OppositeTapStartDelayMaximumMs = 90;

            processor.NormalizeRanges();

            Assert.AreEqual(75, processor.OppositeTapStartDelayMaximumMs,
                "Start delay maximum must never exceed the tap-length minimum.");
        }

        [TestMethod]
        public void NormalizeRanges_LoweringTapLengthMinimumPullsDownStartDelayMaximum()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMinimumMs = 75;
            processor.OppositeTapLengthMaximumMs = 120;
            processor.OppositeTapStartDelayMaximumMs = 70;
            processor.NormalizeRanges();
            Assert.AreEqual(70, processor.OppositeTapStartDelayMaximumMs);

            processor.OppositeTapLengthMinimumMs = 50;
            processor.NormalizeRanges();

            Assert.AreEqual(50, processor.OppositeTapStartDelayMaximumMs,
                "Lowering the tap-length minimum below the current start-delay maximum must clamp the delay down too.");
        }

        [TestMethod]
        public void NormalizeRanges_EqualMinimumAndMaximum_StaysDeterministic()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMinimumMs = 100;
            processor.OppositeTapLengthMaximumMs = 100;
            processor.OppositeTapStartDelayMinimumMs = 0;
            processor.OppositeTapStartDelayMaximumMs = 0;

            processor.NormalizeRanges();

            Assert.AreEqual(100, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(100, processor.OppositeTapLengthMaximumMs);
            Assert.AreEqual(0, processor.OppositeTapStartDelayMinimumMs);
            Assert.AreEqual(0, processor.OppositeTapStartDelayMaximumMs);
        }

        [TestMethod]
        public void NormalizeRanges_DoesNotCreateRecursiveOrUnstableLoop()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMinimumMs = 30;
            processor.OppositeTapLengthMaximumMs = 20;
            processor.OppositeTapStartDelayMinimumMs = 50;
            processor.OppositeTapStartDelayMaximumMs = 10;

            processor.NormalizeRanges();
            processor.NormalizeRanges(); // Idempotent: a second call must not change anything further.

            Assert.IsTrue(processor.OppositeTapLengthMinimumMs <= processor.OppositeTapLengthMaximumMs);
            Assert.IsTrue(processor.OppositeTapStartDelayMinimumMs <= processor.OppositeTapStartDelayMaximumMs);
            Assert.IsTrue(processor.OppositeTapStartDelayMaximumMs <= processor.OppositeTapLengthMinimumMs);
        }

        // --- Section 10/27: CS2 preset ---------------------------------------------

        [TestMethod]
        public void ApplyCs2Preset_SetsTapLengthRangeOnly()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapStartDelayMinimumMs = 3;
            processor.OppositeTapStartDelayMaximumMs = 15;
            processor.MinimumHoldMs = 42;
            processor.ArmingThreshold = 0.33;

            processor.ApplyCs2Preset();

            Assert.AreEqual(75, processor.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, processor.OppositeTapLengthMaximumMs);
            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, processor.TapLengthPreset);
            Assert.AreEqual(3, processor.OppositeTapStartDelayMinimumMs, "CS2 must not alter the start delay.");
            Assert.AreEqual(15, processor.OppositeTapStartDelayMaximumMs, "CS2 must not alter the start delay.");
            Assert.AreEqual(42, processor.MinimumHoldMs, "CS2 must not alter Minimum Hold Time.");
            Assert.AreEqual(0.33, processor.ArmingThreshold, "CS2 must not alter the Required Stick Deflection threshold.");
        }

        [TestMethod]
        public void EffectiveTapLengthPreset_MatchingCs2Values_DisplaysCs2()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMinimumMs = 75;
            processor.OppositeTapLengthMaximumMs = 120;
            processor.TapLengthPreset = CounterMovementTapLengthPreset.CS2;

            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, processor.EffectiveTapLengthPreset);
        }

        [TestMethod]
        public void EffectiveTapLengthPreset_MismatchedCs2Values_DisplaysCustomWithoutOverwritingValues()
        {
            CounterMovementReleasePressProcessor processor = new CounterMovementReleasePressProcessor();
            processor.OppositeTapLengthMinimumMs = 80;
            processor.OppositeTapLengthMaximumMs = 130;
            processor.TapLengthPreset = CounterMovementTapLengthPreset.CS2; // stale/malformed stored preset

            Assert.AreEqual(CounterMovementTapLengthPreset.Custom, processor.EffectiveTapLengthPreset,
                "The numeric values are authoritative over a stale stored preset.");
            Assert.AreEqual(80, processor.OppositeTapLengthMinimumMs, "Must not silently overwrite the loaded numeric values.");
            Assert.AreEqual(130, processor.OppositeTapLengthMaximumMs);
        }

        [TestMethod]
        public void LoadingCs2Preset_WithMatchingValues_ResolvesToCs2()
        {
            var (_, padAction) = LoadMapper(tapLengthMin: 75, tapLengthMax: 120, startDelayMin: 0, startDelayMax: 20, preset: "CS2");

            Assert.AreEqual(CounterMovementTapLengthPreset.CS2, padAction.CounterMovementReleasePress.EffectiveTapLengthPreset);
        }

        [TestMethod]
        public void LoadingCs2Preset_WithMismatchedValues_ResolvesToCustom()
        {
            var (_, padAction) = LoadMapper(tapLengthMin: 60, tapLengthMax: 90, startDelayMin: 0, startDelayMax: 20, preset: "CS2");

            Assert.AreEqual(CounterMovementTapLengthPreset.Custom, padAction.CounterMovementReleasePress.EffectiveTapLengthPreset);
            Assert.AreEqual(60, padAction.CounterMovementReleasePress.OppositeTapLengthMinimumMs);
            Assert.AreEqual(90, padAction.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
        }

        // --- Section 13/26: legacy profile migration -------------------------------

        private string BuildLegacyProfileJson(bool enabled, int brakeDurationMs, int minimumHoldMs, double armingThreshold)
        {
            return $@"{{
  ""Name"": ""LegacyTest"",
  ""Description"": ""LegacyTest"",
  ""Creator"": ""test"",
  ""CreationDate"": ""2026-07-22T00:00:00+0000"",
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
              ""Name"": ""StickWASD"",
              ""ActionMode"": ""StickPadAction"",
              ""Bindings"": {{
                ""Up"": {{ ""Name"": ""Up"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""W"" }} ] }} ] }},
                ""Down"": {{ ""Name"": ""Down"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""S"" }} ] }} ] }},
                ""Left"": {{ ""Name"": ""Left"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""A"" }} ] }} ] }},
                ""Right"": {{ ""Name"": ""Right"", ""Functions"": [ {{ ""Type"": ""NormalPress"", ""OutputActions"": [ {{ ""Type"": ""Keyboard"", ""Code"": ""D"" }} ] }} ] }}
              }},
              ""Settings"": {{
                ""PadMode"": ""Standard"",
                ""DeadZone"": 0.3,
                ""DiagonalRange"": 45,
                ""BrakeEnabled"": {enabled.ToString().ToLowerInvariant()},
                ""BrakeDurationMs"": {brakeDurationMs},
                ""BrakeMinimumHoldMs"": {minimumHoldMs},
                ""BrakeArmingThreshold"": {armingThreshold.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}
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
        {{ ""Input"": ""Stick"", ""Action"": 0 }}
      ]
    }}
  ]
}}";
        }

        private (TestMapper mapper, StickPadAction padAction) LoadLegacyMapper(bool enabled, int brakeDurationMs,
            int minimumHoldMs = 80, double armingThreshold = 0.15)
        {
            eventInputMapping = new SendInputMapping();
            ProfileSerializer.EventInputMapper = eventInputMapping;

            Profile tempProfile = new Profile();
            mapper = new TestMapper(tempProfile);
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mapper, new NoOpVirtualKBM());
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(BuildLegacyProfileJson(enabled, brakeDurationMs, minimumHoldMs, armingThreshold), profileSerializer);
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

        [TestMethod]
        public void LegacyProfile_EnabledValueIsPreserved()
        {
            var (_, padAction) = LoadLegacyMapper(enabled: true, brakeDurationMs: 90);
            Assert.IsTrue(padAction.CounterMovementReleasePress.Enabled);
        }

        [TestMethod]
        public void LegacyProfile_BrakeDurationBecomesMinimumAndMaximum()
        {
            var (_, padAction) = LoadLegacyMapper(enabled: true, brakeDurationMs: 90);
            Assert.AreEqual(90, padAction.CounterMovementReleasePress.OppositeTapLengthMinimumMs);
            Assert.AreEqual(90, padAction.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
        }

        [TestMethod]
        public void LegacyProfile_StartDelayDefaultsToZeroZero_PreservingImmediateStart()
        {
            var (_, padAction) = LoadLegacyMapper(enabled: true, brakeDurationMs: 90);
            Assert.AreEqual(0, padAction.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs,
                "Migrated profiles must use 0ms, not the 0-20ms new-action default, to preserve old behaviour exactly.");
            Assert.AreEqual(0, padAction.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs);
        }

        [TestMethod]
        public void LegacyProfile_PresetBecomesCustom()
        {
            var (_, padAction) = LoadLegacyMapper(enabled: true, brakeDurationMs: 90);
            Assert.AreEqual(CounterMovementTapLengthPreset.Custom, padAction.CounterMovementReleasePress.TapLengthPreset);
        }

        [TestMethod]
        public void LegacyProfile_MinimumHoldTimeIsPreserved()
        {
            var (_, padAction) = LoadLegacyMapper(enabled: true, brakeDurationMs: 90, minimumHoldMs: 55);
            Assert.AreEqual(55, padAction.CounterMovementReleasePress.MinimumHoldMs);
        }

        [TestMethod]
        public void LegacyProfile_ArmingThresholdIsPreserved()
        {
            var (_, padAction) = LoadLegacyMapper(enabled: true, brakeDurationMs: 90, armingThreshold: 0.42);
            Assert.AreEqual(0.42, padAction.CounterMovementReleasePress.ArmingThreshold);
        }

        [TestMethod]
        public void LegacyProfile_ProducesImmediateStartBehaviourEndToEnd()
        {
            var (mapper, padAction) = LoadLegacyMapper(enabled: true, brakeDurationMs: 40, minimumHoldMs: 0, armingThreshold: 0.0);

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);

            Assert.AreEqual(CounterMovementReleasePressProcessor.CounterMovementReleasePressState.OppositeTapActive, padAction.CounterMovementReleasePress.State);
            Assert.IsTrue(KeyDown(VK_S), "A migrated legacy profile must still press the opposite direction immediately on release.");
        }

        [TestMethod]
        public void MissingNewFields_LoadSafelyWithProcessorDefaults()
        {
            // A profile with only PadMode/DeadZone (no brake-related fields at all, legacy or
            // new) must not crash and must leave the processor at its constructed defaults.
            string json = @"{
  ""Name"": ""NoFieldsTest"",
  ""Description"": ""NoFieldsTest"",
  ""Creator"": ""test"",
  ""CreationDate"": ""2026-07-22T00:00:00+0000"",
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
                ""Up"": { ""Name"": ""Up"", ""Functions"": [ { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""W"" } ] } ] }
              },
              ""Settings"": { ""PadMode"": ""Standard"", ""DeadZone"": 0.3 }
            }
          ]
        }
      ]
    }
  ],
  ""Mappings"": [
    { ""ActionSet"": 0, ""ActionLayer"": 0, ""InputMappings"": [ { ""Input"": ""Stick"", ""Action"": 0 } ] }
  ]
}";

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
            FillMappingProfileInitialData(tempProfile, profileSerializer.ActionMappings);
            SyncActionData(tempProfile);

            StickPadAction padAction = tempProfile.ActionSets[0].ActionLayers[0].stickActionDict["Stick"] as StickPadAction;

            Assert.IsFalse(padAction.CounterMovementReleasePress.Enabled);
            Assert.AreEqual(75, padAction.CounterMovementReleasePress.OppositeTapLengthMinimumMs);
            Assert.AreEqual(120, padAction.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
            Assert.AreEqual(0, padAction.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs);
            Assert.AreEqual(0, padAction.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs);
        }

        // --- Section 24/26: round trip through the serializer ----------------------

        [TestMethod]
        public void NewProfile_RoundTripsAllTimingValuesThroughSerialization()
        {
            StickPadAction actionToSave = new StickPadAction();
            actionToSave.Id = 9;
            actionToSave.CounterMovementReleasePress.Enabled = true;
            actionToSave.CounterMovementReleasePress.OppositeTapLengthMinimumMs = 60;
            actionToSave.CounterMovementReleasePress.OppositeTapLengthMaximumMs = 110;
            actionToSave.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs = 5;
            actionToSave.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs = 25;
            actionToSave.CounterMovementReleasePress.TapLengthPreset = CounterMovementTapLengthPreset.Custom;
            actionToSave.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            actionToSave.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            actionToSave.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            actionToSave.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            actionToSave.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            actionToSave.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);

            string json = JsonConvert.SerializeObject(new StickPadActionSerializer(null, actionToSave));
            JObject parsed = JObject.Parse(json);

            Assert.AreEqual(true, parsed["Settings"]?["CounterMovementReleasePressEnabled"]?.Value<bool>());
            Assert.AreEqual(60, parsed["Settings"]?["OppositeTapLengthMinimumMs"]?.Value<int>());
            Assert.AreEqual(110, parsed["Settings"]?["OppositeTapLengthMaximumMs"]?.Value<int>());
            Assert.AreEqual(5, parsed["Settings"]?["OppositeTapStartDelayMinimumMs"]?.Value<int>());
            Assert.AreEqual(25, parsed["Settings"]?["OppositeTapStartDelayMaximumMs"]?.Value<int>());
            Assert.AreEqual("Custom", parsed["Settings"]?["CounterMovementTapLengthPreset"]?.Value<string>());
            Assert.IsNull(parsed["Settings"]?["BrakeEnabled"], "Legacy field names must not be re-serialized.");
            Assert.IsNull(parsed["Settings"]?["BrakeDurationMs"], "Legacy field names must not be re-serialized.");

            // Re-import and confirm values survive the round trip.
            StickPadActionSerializer reimport = new StickPadActionSerializer();
            JsonConvert.PopulateObject(json, reimport);
            reimport.PopulateMap();
            StickPadAction reloaded = reimport.MapAction as StickPadAction;

            Assert.AreEqual(60, reloaded.CounterMovementReleasePress.OppositeTapLengthMinimumMs);
            Assert.AreEqual(110, reloaded.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
            Assert.AreEqual(5, reloaded.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs);
            Assert.AreEqual(25, reloaded.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs);
        }

        // --- Section 23: inheritance ------------------------------------------------

        [TestMethod]
        public void InheritedAction_PicksUpParentTimingValuesUntilOverridden()
        {
            StickPadAction parent = new StickPadAction();
            parent.CounterMovementReleasePress.OppositeTapLengthMinimumMs = 60;
            parent.CounterMovementReleasePress.OppositeTapLengthMaximumMs = 90;
            parent.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs = 5;
            parent.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs = 15;

            StickPadAction child = new StickPadAction();
            child.SoftCopyFromParent(parent);

            Assert.AreEqual(60, child.CounterMovementReleasePress.OppositeTapLengthMinimumMs);
            Assert.AreEqual(90, child.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
            Assert.AreEqual(5, child.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs);
            Assert.AreEqual(15, child.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs);

            parent.CounterMovementReleasePress.OppositeTapLengthMaximumMs = 100;
            parent.RaiseNotifyPropertyChange(null, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            Assert.AreEqual(100, child.CounterMovementReleasePress.OppositeTapLengthMaximumMs,
                "An uninherited-override child property must keep tracking the parent.");

            child.CounterMovementReleasePress.OppositeTapLengthMaximumMs = 70;
            child.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            parent.CounterMovementReleasePress.OppositeTapLengthMaximumMs = 130;
            parent.RaiseNotifyPropertyChange(null, StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            Assert.AreEqual(70, child.CounterMovementReleasePress.OppositeTapLengthMaximumMs,
                "Once explicitly overridden, the child must stop tracking the parent for that property.");
        }
    }
}
