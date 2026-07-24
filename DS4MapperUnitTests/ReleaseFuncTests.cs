using System.Linq;
using System.Reflection;
using DS4MapperTest;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    // Exercises the new regular Release Press implementation through the real production
    // path: ProfileSerializer -> Profile -> ButtonAction -> Mapper.Reader_Report /
    // Mapper.ProcessReleaseEvents. No dispatch logic is copied into TestMapper; only the
    // real Mapper.Reader_Report entry point is driven, exactly as MappingTests.cs does for
    // other features.
    [TestClass]
    public class ReleaseFuncTests : BindingHelperBase
    {
        private const double DT = 0.008;

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
            // Statics leak across tests otherwise (same pattern used throughout this suite).
            TestMapper.KeyReferenceCountDict.Clear();
            TestMapper.MouseButtonReferenceCountDict.Clear();
        }

        // Face button "A" -> Release Press (Keyboard Z, End Delay 0)
        // "RightPadClick" (this app's Steam Controller stand-in for R3 / right stick click)
        //     -> Release Press (Keyboard Z, End Delay 0)
        // "LSClick" (L3) -> Release Press (Keyboard X, End Delay 0)
        // "RShoulder" (shoulder button) -> Release Press (Keyboard C, End Delay 0)
        // "B" -> Release Press (Keyboard V, End Delay 100ms)
        // "X" -> Release Press (Keyboard T, Toggle on)
        // "Y" -> plain Normal Press (Keyboard N) - regression check for ordinary bindings
        private static string BuildProfileJson()
        {
            return @"{
  ""Name"": ""ReleaseFuncTest"",
  ""Description"": ""ReleaseFuncTest"",
  ""Creator"": ""test"",
  ""CreationDate"": ""2026-07-23T00:00:00+0000"",
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
              ""Name"": ""FaceButtonRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Empty"" } ] },
                { ""Type"": ""Release"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""Z"" } ], ""Settings"": { ""DelayDuration"": 0 } }
              ]
            },
            {
              ""Id"": 1,
              ""Name"": ""R3EquivalentRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Empty"" } ] },
                { ""Type"": ""Release"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""Z"" } ], ""Settings"": { ""DelayDuration"": 0 } }
              ]
            },
            {
              ""Id"": 2,
              ""Name"": ""L3EquivalentRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Empty"" } ] },
                { ""Type"": ""Release"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""X"" } ], ""Settings"": { ""DelayDuration"": 0 } }
              ]
            },
            {
              ""Id"": 3,
              ""Name"": ""ShoulderRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Empty"" } ] },
                { ""Type"": ""Release"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""C"" } ], ""Settings"": { ""DelayDuration"": 0 } }
              ]
            },
            {
              ""Id"": 4,
              ""Name"": ""PositiveDelayRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Empty"" } ] },
                { ""Type"": ""Release"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""V"" } ], ""Settings"": { ""DelayDuration"": 100 } }
              ]
            },
            {
              ""Id"": 5,
              ""Name"": ""ToggleRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Empty"" } ] },
                { ""Type"": ""Release"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""T"" } ], ""Settings"": { ""DelayDuration"": 0, ""Toggle"": true } }
              ]
            },
            {
              ""Id"": 6,
              ""Name"": ""PlainNormalPress"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""N"" } ] }
              ]
            },
            {
              ""Id"": 7,
              ""Name"": ""MaxHoldTimeRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                { ""Type"": ""NormalPress"", ""OutputActions"": [ { ""Type"": ""Empty"" } ] },
                { ""Type"": ""Release"", ""OutputActions"": [ { ""Type"": ""Keyboard"", ""Code"": ""M"" } ], ""Settings"": { ""DelayDuration"": 0, ""MaxHoldTimeEnabled"": true, ""MaxHoldTimeMs"": 250 } }
              ]
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
        { ""Input"": ""A"", ""Action"": 0 },
        { ""Input"": ""RightPadClick"", ""Action"": 1 },
        { ""Input"": ""LSClick"", ""Action"": 2 },
        { ""Input"": ""RShoulder"", ""Action"": 3 },
        { ""Input"": ""B"", ""Action"": 4 },
        { ""Input"": ""X"", ""Action"": 5 },
        { ""Input"": ""Y"", ""Action"": 6 },
        { ""Input"": ""Back"", ""Action"": 7 }
      ]
    }
  ]
}";
        }

        private TestMapper LoadMapper()
        {
            VirtualKBMMapping eventInputMapping = new SendInputMapping();
            ProfileSerializer.EventInputMapper = eventInputMapping;

            Profile tempProfile = new Profile();
            TestMapper testMapper = new TestMapper(tempProfile);
            mapper = testMapper;
            typeof(Mapper).GetField("eventInputHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(testMapper, new NoOpVirtualKBM());
            typeof(Mapper).GetField("eventInputMapping", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(testMapper, eventInputMapping);
            tempProfile.ActionSets.Clear();

            ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
            JsonConvert.PopulateObject(BuildProfileJson(), profileSerializer);
            profileSerializer.PopulateProfile();
            tempProfile.ResetAliases();

            FillMappingProfileInitialData(tempProfile, profileSerializer.ActionMappings);
            SyncActionData(tempProfile);

            testMapper.EditActionSet = tempProfile.ActionSets[0];
            testMapper.EditLayer = tempProfile.ActionSets[0].ActionLayers[0];

            return testMapper;
        }

        private static SteamControllerState NeutralState(double dt = DT) =>
            new SteamControllerState() { timeElapsed = dt };

        private static void Report(TestMapper mapper, SteamControllerState state)
        {
            mapper.Reader_Report(state, out IntermediateState _);
        }

        private static void SetA(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.A = pressed;
            Report(mapper, state);
        }

        private static void SetR3(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.RightPad = new SteamControllerState.TouchPadInfo { Click = pressed };
            Report(mapper, state);
        }

        private static void SetL3(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.LSClick = pressed;
            Report(mapper, state);
        }

        private static void SetShoulder(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.RB = pressed;
            Report(mapper, state);
        }

        private static void SetPositiveDelayButton(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.B = pressed;
            Report(mapper, state);
        }

        private static void SetToggleButton(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.X = pressed;
            Report(mapper, state);
        }

        private static void SetNormalPressButton(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.Y = pressed;
            Report(mapper, state);
        }

        private static void SetMaxHoldButton(TestMapper mapper, bool pressed, double dt = DT)
        {
            SteamControllerState state = NeutralState(dt);
            state.Back = pressed;
            Report(mapper, state);
        }

        private static bool KeyDown(uint code) => TestMapper.KeyReferenceCountDict.ContainsKey(code);

        private static void DrainPendingReleases(TestMapper mapper, int ticks = 20, double dt = DT)
        {
            for (int i = 0; i < ticks; i++)
            {
                SetA(mapper, false, dt);
            }
        }

        [TestMethod]
        public void BasicR3FallingEdge_NoOutputUntilRelease_ExactlyOneOnRelease()
        {
            TestMapper mapper = LoadMapper();

            SetR3(mapper, false);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z), "No output on initial neutral report.");

            SetR3(mapper, true);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z), "No output on press.");

            SetR3(mapper, true);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z), "No output while held.");

            SetR3(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.Z), "Output activates exactly on release.");
        }

        [TestMethod]
        public void QuickPress_FiresExactlyOnce()
        {
            TestMapper mapper = LoadMapper();

            SetR3(mapper, false);
            SetR3(mapper, true);
            SetR3(mapper, false);

            Assert.IsTrue(KeyDown((uint)VirtualKeys.Z));
            Assert.AreEqual(1, TestMapper.KeyReferenceCountDict[(uint)VirtualKeys.Z]);
        }

        [TestMethod]
        public void LongHold_NoOutputWhileHeld_ThenExactlyOneOnRelease()
        {
            TestMapper mapper = LoadMapper();

            SetR3(mapper, true);
            for (int i = 0; i < 50; i++)
            {
                SetR3(mapper, true);
                Assert.IsFalse(KeyDown((uint)VirtualKeys.Z), "Must not fire while held, regardless of hold duration.");
            }

            SetR3(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.Z));
        }

        [TestMethod]
        public void NoPrecedingPress_NoOutput()
        {
            TestMapper mapper = LoadMapper();

            SetR3(mapper, false);
            SetR3(mapper, false);
            SetR3(mapper, false);

            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z));
        }

        [TestMethod]
        public void NoDuplicate_ExactlyOneOutputActivationPerCycle()
        {
            TestMapper mapper = LoadMapper();

            SetR3(mapper, false);
            SetR3(mapper, true);
            SetR3(mapper, true);
            SetR3(mapper, false);

            // Checked immediately after the single falling edge - with End Delay 0, further
            // ticks would legitimately drain the pulse, so the "no duplicate" assertion must
            // be made before any additional reports are sent.
            Assert.IsTrue(KeyDown((uint)VirtualKeys.Z));
            Assert.AreEqual(1, TestMapper.KeyReferenceCountDict[(uint)VirtualKeys.Z]);
        }

        [TestMethod]
        public void EndDelayZero_PressAndReleaseDoNotHappenInSameSyncCycle()
        {
            TestMapper mapper = LoadMapper();

            SetA(mapper, true);
            SetA(mapper, false);

            // Immediately after the falling-edge report, the key must be down.
            Assert.IsTrue(KeyDown((uint)VirtualKeys.Z), "Output-down must occur on the falling edge.");

            // A configured End Delay of 0 still requires a later mapper tick before the
            // matching release is applied - it must never happen inside the same call.
            DrainPendingReleases(mapper, ticks: 1);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z), "Output-up must land on a later synchronization pass, not the press cycle itself.");
        }

        [TestMethod]
        public void PositiveEndDelay_StaysActiveForApproximatelyConfiguredDuration()
        {
            TestMapper mapper = LoadMapper();

            SetPositiveDelayButton(mapper, true);
            SetPositiveDelayButton(mapper, false);

            Assert.IsTrue(KeyDown((uint)VirtualKeys.V), "Output presses immediately on release.");

            // The End Delay timer is wall-clock based (matches this app's existing
            // Stopwatch-driven pulse idioms and JSM's own timed-instant-release model), so
            // exercising it needs real elapsed time, not simulated dt.
            SetPositiveDelayButton(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.V), "Output remains active well before the 100ms End Delay elapses.");

            System.Threading.Thread.Sleep(150);
            SetPositiveDelayButton(mapper, false);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.V), "Output releases once End Delay has elapsed.");
        }

        [TestMethod]
        public void ProfileStartup_NeutralInput_NoPhantomOutput()
        {
            TestMapper mapper = LoadMapper();

            // First report ever seen by the mapper is neutral, exactly like an app launch
            // with no buttons pressed.
            Report(mapper, NeutralState());

            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z));
            Assert.IsFalse(KeyDown((uint)VirtualKeys.X));
            Assert.IsFalse(KeyDown((uint)VirtualKeys.C));
            Assert.IsFalse(KeyDown((uint)VirtualKeys.V));
            Assert.IsFalse(KeyDown((uint)VirtualKeys.T));
        }

        [TestMethod]
        public void OtherDigitalButtons_L3AndShoulder_FireOnceOnRelease()
        {
            TestMapper mapper = LoadMapper();

            SetL3(mapper, true);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.X));
            SetL3(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.X));

            SetShoulder(mapper, true);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.C));
            SetShoulder(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.C));
        }

        [TestMethod]
        public void OtherDigitalButtons_FaceButton_FiresOnceOnRelease()
        {
            TestMapper mapper = LoadMapper();

            SetA(mapper, true);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z));
            SetA(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.Z));
        }

        [TestMethod]
        public void NormalBinding_PressAndReleaseUnaffectedByReleasePressChanges()
        {
            TestMapper mapper = LoadMapper();

            SetNormalPressButton(mapper, true);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.N), "Ordinary press binding still activates on press.");

            SetNormalPressButton(mapper, false);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.N), "Ordinary press binding still deactivates on release.");
        }

        [TestMethod]
        public void Toggle_ChangesExactlyOnceOnEachSourceRelease()
        {
            TestMapper mapper = LoadMapper();

            SetToggleButton(mapper, true);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.T), "Initial press must not toggle.");

            SetToggleButton(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.T), "First release toggles state on.");

            SetToggleButton(mapper, true);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.T), "Still on while pressed again.");

            SetToggleButton(mapper, false);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.T), "Second release toggles state back off.");
        }

        [TestMethod]
        public void ArmedState_DiscardedSafely_NoOutputWhenReleaseActionsIgnored()
        {
            TestMapper mapper = LoadMapper();

            SetA(mapper, true);

            ButtonAction faceAction = mapper.EditLayer.buttonActionDict["A"] as ButtonAction;
            Assert.IsNotNull(faceAction);

            // Simulates a binding edit / action-set jump that discards the action without a
            // genuine physical release (the same ignoreReleaseActions:true path the UI's
            // edit view models use).
            faceAction.Release(mapper, resetState: true, ignoreReleaseActions: true);

            DrainPendingReleases(mapper);
            Assert.IsFalse(KeyDown((uint)VirtualKeys.Z), "Discarding an armed action must never fire its output.");
        }

        [TestMethod]
        public void PendingRelease_ReleasedImmediately_WhenMapperShutsDownMidPulse()
        {
            TestMapper mapper = LoadMapper();

            SetPositiveDelayButton(mapper, true);
            SetPositiveDelayButton(mapper, false);
            Assert.IsTrue(KeyDown((uint)VirtualKeys.V), "Pulse is active and mid-flight (100ms End Delay, not yet elapsed).");
            Assert.AreEqual(1, mapper.PendingReleaseFuns.Count, "The in-flight pulse is queued on the mapper's pending list.");

            // Simulates shutdown/disconnect/cancellation via the real Mapper.Stop() path.
            // No further mapper ticks will occur to let ProcessReleaseEvents finish the
            // pulse naturally, so the output must not remain stuck down.
            mapper.Stop();

            Assert.IsFalse(KeyDown((uint)VirtualKeys.V), "A pending pulse must not be left stuck down when the mapper shuts down.");
            Assert.AreEqual(0, mapper.PendingReleaseFuns.Count);
        }

        [TestMethod]
        public void MaxHoldTime_HeldLongerThanMax_SuppressesFire()
        {
            TestMapper mapper = LoadMapper();

            SetMaxHoldButton(mapper, true);
            System.Threading.Thread.Sleep(300); // > configured 250ms max hold time
            SetMaxHoldButton(mapper, false);

            Assert.IsFalse(KeyDown((uint)VirtualKeys.M),
                "Held longer than the configured Max Hold Time - Release Press must not fire.");
        }

        [TestMethod]
        public void MaxHoldTime_HeldShorterThanMax_FiresNormally()
        {
            TestMapper mapper = LoadMapper();

            SetMaxHoldButton(mapper, true);
            SetMaxHoldButton(mapper, false);

            Assert.IsTrue(KeyDown((uint)VirtualKeys.M),
                "Held well under the configured Max Hold Time - Release Press must fire normally.");
        }

        [TestMethod]
        public void MaxHoldTime_Disabled_HoldDurationHasNoEffect()
        {
            TestMapper mapper = LoadMapper();

            // "A" is bound to a Release Press with MaxHoldTimeEnabled left at its default
            // (false). Even a long hold must still fire when the feature is off.
            SetA(mapper, true);
            System.Threading.Thread.Sleep(300);
            SetA(mapper, false);

            Assert.IsTrue(KeyDown((uint)VirtualKeys.Z),
                "Max Hold Time disabled - hold duration must have no effect on firing.");
        }

        [TestMethod]
        public void MaxHoldTime_DefaultsToOffWithTwoFiftyMsWhenNewlyCreated()
        {
            ReleaseFunc func = new ReleaseFunc();

            Assert.IsFalse(func.MaxHoldTimeEnabled, "Max Hold Time must default to Off.");
            Assert.AreEqual(250, func.MaxHoldTimeMs, "Enabled default value must be 250ms.");
        }

        [TestMethod]
        public void MaxHoldTime_ProfileSaveLoad_RoundTripsThroughRealSerializer()
        {
            ReleaseFunc source = new ReleaseFunc
            {
                MaxHoldTimeEnabled = true,
                MaxHoldTimeMs = 500,
            };
            source.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.K));

            ActionFuncSerializer serializer = ActionFuncSerializerFactory.CreateSerializer(source);
            string json = JsonConvert.SerializeObject(serializer);

            Assert.IsTrue(json.Contains("\"MaxHoldTimeEnabled\":true"));
            Assert.IsTrue(json.Contains("\"MaxHoldTimeMs\":500"));

            ActionFuncSerializer loaded = JsonConvert.DeserializeObject<ActionFuncSerializer>(json);
            loaded.PopulateFunc();

            Assert.IsInstanceOfType(loaded.ActionFunc, typeof(ReleaseFunc));
            ReleaseFunc loadedFunc = (ReleaseFunc)loaded.ActionFunc;
            Assert.IsTrue(loadedFunc.MaxHoldTimeEnabled);
            Assert.AreEqual(500, loadedFunc.MaxHoldTimeMs);
        }

        [TestMethod]
        public void MaxHoldTime_DefaultValue_IsNotWrittenToProfile()
        {
            ReleaseFunc source = new ReleaseFunc();
            source.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.K));

            ActionFuncSerializer serializer = ActionFuncSerializerFactory.CreateSerializer(source);
            string json = JsonConvert.SerializeObject(serializer);

            Assert.IsFalse(json.Contains("MaxHoldTime"),
                "Default (disabled, 250ms) Max Hold Time must be omitted from a saved profile.");
        }

        [TestMethod]
        public void MaxHoldTime_Clone_CopiesValueIndependently()
        {
            ReleaseFunc source = new ReleaseFunc
            {
                MaxHoldTimeEnabled = true,
                MaxHoldTimeMs = 400,
            };

            ActionFunc cloned = ActionFuncCopyFactory.CopyFunc(source);

            Assert.IsInstanceOfType(cloned, typeof(ReleaseFunc));
            ReleaseFunc clonedRelease = (ReleaseFunc)cloned;
            Assert.IsTrue(clonedRelease.MaxHoldTimeEnabled);
            Assert.AreEqual(400, clonedRelease.MaxHoldTimeMs);

            // Mutating the clone must not affect the source (independent copy, not a
            // reference share).
            clonedRelease.MaxHoldTimeMs = 999;
            Assert.AreEqual(400, source.MaxHoldTimeMs);
        }
    }
}
