using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using DS4MapperTest.StickActions;
using DS4MapperTest.ViewModels.StickActionPropViewModels;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class StickReleaseBrakeTests : BindingHelperBase
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
        public void BrakeDurationMs_ClampsToOneHundredFiftyMilliseconds()
        {
            StickReleaseBrake brake = new StickReleaseBrake();

            brake.BrakeDurationMs = 900;

            Assert.AreEqual(150, brake.BrakeDurationMs);
        }

        private string BuildProfileJson(string padMode = "EightWay")
        {
            return @"{
  ""Name"": ""ReleaseBrakeTest"",
  ""Description"": ""ReleaseBrakeTest"",
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
                ""BrakeMinimumHoldMs"": 80
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
}".Replace(@"""PadMode"": ""EightWay""", $@"""PadMode"": ""{padMode}""");
        }

        private (TestMapper mapper, StickPadAction padAction) LoadMapper(string padMode = "EightWay")
        {
            eventInputMapping = new SendInputMapping();
            ProfileSerializer.EventInputMapper = eventInputMapping;

            Profile tempProfile = new Profile();
            mapper = new TestMapper(tempProfile);
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mapper, new NoOpVirtualKBM());
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(BuildProfileJson(padMode), profileSerializer);
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

        private static bool KeyDown(uint vk) => TestMapper.KeyReferenceCountDict.ContainsKey(vk);

        [TestMethod]
        [DataRow("Standard")]
        [DataRow("EightWay")]
        [DataRow("FourWayCardinal")]
        [DataRow("FourWayDiagonal")]
        public void ReleaseBrakeSettings_AreVisibleForEveryDPadMode(string padMode)
        {
            var (mapper, padAction) = LoadMapper(padMode);

            StickPadActionPropViewModel vm = new StickPadActionPropViewModel(mapper, padAction);

            Assert.IsTrue(vm.ShowReleaseBrakeSection, $"Release brake controls must be visible in {padMode} mode.");
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

            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State,
                $"Expected Braking in {padMode} mode.");
            Assert.IsFalse(KeyDown(VK_W), $"Original Up key must be released in {padMode} mode.");
            Assert.IsTrue(KeyDown(VK_S), $"Opposite Down key must be pressed in {padMode} mode.");
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

            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State,
                $"Expected Braking in {padMode} mode.");
            Assert.IsTrue(KeyDown(VK_S), $"Opposite of Up must be Down (S) in {padMode} mode.");
            Assert.IsTrue(KeyDown(VK_A), $"Opposite of Right must be Left (A) in {padMode} mode.");
        }

        [TestMethod]
        public void FastCardinalRelease_FiresOnceAndSuppressesOldDirection()
        {
            var (mapper, padAction) = LoadMapper();

            Neutral(mapper);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Idle, padAction.ReleaseBrake.State);

            // Arm and hold Up (W) well past the arm-settle time and MinimumHoldMs (80ms).
            HoldUp(mapper, 20);
            Assert.IsTrue(KeyDown(VK_W));
            Assert.AreEqual(StickReleaseBrake.BrakeState.Armed, padAction.ReleaseBrake.State);

            // Fast full release.
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
            Assert.IsFalse(KeyDown(VK_W), "Original direction must be released immediately.");
            Assert.IsTrue(KeyDown(VK_S), "Opposite direction (S) must be pressed by the brake.");

            // Stick stays at rest (spring already settled in this synthetic trace); pulse
            // must still expire after BrakeDurationMs and S must be released while W stays
            // suppressed until neutral.
            for (int i = 0; i < 10 && padAction.ReleaseBrake.State != StickReleaseBrake.BrakeState.Suppressed; i++)
            {
                Report(mapper, 0, 0);
            }
            Assert.AreEqual(StickReleaseBrake.BrakeState.Suppressed, padAction.ReleaseBrake.State);
            Assert.IsFalse(KeyDown(VK_S), "Brake pulse must release S after BrakeDurationMs.");
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
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
            Assert.IsTrue(KeyDown(VK_S), "Opposite of Up is Down (S).");
            Assert.IsTrue(KeyDown(VK_A), "Opposite of Right is Left (A).");
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

                Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State,
                    $"Expected Braking for ({c.lx},{c.ly})");
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
            for (int i = 50; i >= 0 && padAction.ReleaseBrake.State == StickReleaseBrake.BrakeState.Armed; i--)
            {
                int ly = (int)(FULL * (i / 50.0));
                Report(mapper, 0, ly);
            }

            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State,
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

            Assert.AreEqual(StickReleaseBrake.BrakeState.Armed, padAction.ReleaseBrake.State,
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

                Assert.AreNotEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State,
                    $"Rim arc must not trigger a brake at step {step}");
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
                Assert.AreEqual(StickReleaseBrake.BrakeState.Idle, padAction.ReleaseBrake.State);
            }
        }

        [TestMethod]
        public void OnePulsePerRelease_ContinuousReturnFiresOnce()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);

            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);

            int brakingObservations = 1;
            for (int i = 0; i < 40; i++)
            {
                Report(mapper, 0, 0);
                if (padAction.ReleaseBrake.State == StickReleaseBrake.BrakeState.Braking)
                {
                    brakingObservations++;
                }
            }

            // Only the single contiguous Braking run from the one release should ever occur;
            // once Suppressed/Idle is reached it must not re-enter Braking without a fresh push.
            Assert.AreEqual(StickReleaseBrake.BrakeState.Idle, padAction.ReleaseBrake.State);
            Assert.IsTrue(brakingObservations < 40, "Brake re-armed and fired more than once for a single release.");
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
        public void RepeekOriginalDirection_CancelsBrakeAndRestoresCleanly()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldRight(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
            Assert.IsTrue(KeyDown(VK_A));

            // Deliberately push D again.
            for (int i = 0; i < 6; i++)
            {
                Report(mapper, FULL, 0);
            }

            Assert.IsTrue(KeyDown(VK_D), "Renewed D push must be restored.");
            Assert.IsFalse(KeyDown(VK_A), "Cancelled brake pulse must not leave A stuck.");
        }

        [TestMethod]
        public void ReverseIntoBrakeDirection_TransfersOwnershipWithoutGap()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldRight(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
            Assert.IsTrue(KeyDown(VK_A));

            // Deliberately push A (left) — the same key the brake is already holding.
            for (int i = 0; i < 6; i++)
            {
                Report(mapper, -FULL, 0);
                Assert.IsTrue(KeyDown(VK_A), "A must remain continuously held through ownership handover.");
            }
        }

        [TestMethod]
        public void ShortCardinalTap_DoesNotBrake()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);

            // Arm, then release almost immediately (well under MinimumHoldMs=80ms).
            HoldRight(mapper, 4);
            Report(mapper, 0, 0);

            Assert.IsFalse(KeyDown(VK_A), "Short tap under MinimumHoldMs must not brake.");
        }

        [TestMethod]
        public void MixedDurationDiagonal_OnlyEligibleComponentBrakes()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);

            // Hold D (Right) well past MinimumHoldMs, then add W (Up) for only ~16ms.
            HoldRight(mapper, 20);
            HoldUpRight(mapper, 2);
            Report(mapper, 0, 0);

            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
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
            // ignored, not crash, and not brake.
            Report(mapper, 0, FULL, 0.0);
            Assert.AreNotEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);

            // A dropped-report style huge dt (still no real release) must also be rejected.
            Report(mapper, 0, FULL, 5.0);
            Assert.AreNotEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);

            // Normal operation must still work afterwards.
            HoldUp(mapper, 10);
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
        }

        [TestMethod]
        public void EnableWhileHeld_StaysUnprimedUntilNeutral()
        {
            var (mapper, padAction) = LoadMapper();

            // First-ever report already holds the stick — no neutral warm-up.
            HoldUp(mapper, 30);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Unprimed, padAction.ReleaseBrake.State);
            Assert.IsTrue(KeyDown(VK_W), "Normal output must continue while Unprimed.");

            for (int i = 0; i < 5; i++) Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Idle, padAction.ReleaseBrake.State);

            // Now a genuine push-and-release cycle must brake normally.
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
        }

        [TestMethod]
        public void DisableMidPulse_ReleasesPulseOwnedKeysAndClearsSuppression()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
            Assert.IsTrue(KeyDown(VK_S));

            padAction.ReleaseBrake.Enabled = false;
            Report(mapper, 0, 0);

            Assert.IsFalse(KeyDown(VK_S), "Disabling mid-pulse must release the brake-owned key.");
            Assert.IsFalse(KeyDown(VK_W), "No key should be left stuck.");
        }

        [TestMethod]
        public void ReleaseDuringPulse_LeavesNoStuckKeys()
        {
            var (mapper, padAction) = LoadMapper();
            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
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
            padAction.ReleaseBrake.BrakeDurationMs = 10;

            Neutral(mapper);
            HoldUp(mapper, 20);
            Report(mapper, 0, 0);
            Assert.AreEqual(StickReleaseBrake.BrakeState.Braking, padAction.ReleaseBrake.State);
            Assert.IsTrue(KeyDown(VK_S));

            Thread.Sleep(25);
            Report(mapper, 0, 0, 0.0);

            Assert.AreEqual(StickReleaseBrake.BrakeState.Suppressed, padAction.ReleaseBrake.State);
            Assert.IsFalse(KeyDown(VK_S), "Invalid report dt must not keep the brake-owned key held indefinitely.");
        }
    }
}
