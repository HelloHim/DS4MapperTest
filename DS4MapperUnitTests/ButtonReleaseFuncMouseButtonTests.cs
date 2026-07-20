using System.Reflection;
using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    // Covers a binding shape found in the wild: a button whose NormalPress does
    // nothing but whose Release fires a MouseButton output (no matching press
    // elsewhere). ButtonAction.Event's release-funcs branch sends the deferred
    // press then immediately calls action.Release(), which clears
    // OutputActionData.activatedEvent before ReleaseFunc's own deferred
    // ReleaseEvents() checks it - so the queued mouse-up is silently skipped and
    // the virtual button stays held down at the OS level indefinitely.
    [TestClass]
    public class ButtonReleaseFuncMouseButtonTests : BindingHelperBase
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
            // Statics leak across tests otherwise (same pattern as StickReleaseBrakeTests).
            TestMapper.KeyReferenceCountDict.Clear();
            TestMapper.MouseButtonReferenceCountDict.Clear();
        }

        private static string BuildProfileJson()
        {
            return @"{
  ""Name"": ""ReleaseFuncMouseButtonTest"",
  ""Description"": ""ReleaseFuncMouseButtonTest"",
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
              ""Name"": ""MiddleOnRelease"",
              ""ActionMode"": ""ButtonAction"",
              ""Functions"": [
                {
                  ""Type"": ""NormalPress"",
                  ""OutputActions"": [ { ""Type"": ""Empty"" } ]
                },
                {
                  ""Type"": ""Release"",
                  ""OutputActions"": [ { ""Type"": ""MouseButton"", ""Code"": ""MiddleButton"" } ],
                  ""Settings"": { ""DelayDuration"": 0 }
                }
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
        { ""Input"": ""A"", ""Action"": 0 }
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

        private static void PressA(TestMapper mapper, double dt = DT)
        {
            SteamControllerState state = new SteamControllerState() { A = true, timeElapsed = dt };
            mapper.Reader_Report(state, out IntermediateState _);
        }

        private static void ReleaseA(TestMapper mapper, double dt = DT)
        {
            SteamControllerState state = new SteamControllerState() { A = false, timeElapsed = dt };
            mapper.Reader_Report(state, out IntermediateState _);
        }

        [TestMethod]
        public void ReleaseFuncMouseButton_IsReleasedAfterPhysicalRelease()
        {
            TestMapper mapper = LoadMapper();

            PressA(mapper);
            ReleaseA(mapper);

            // Let the deferred ReleaseFunc/ProcessReleaseEvents pipeline run to completion.
            for (int i = 0; i < 10; i++) ReleaseA(mapper);

            Assert.IsFalse(
                TestMapper.MouseButtonReferenceCountDict.ContainsKey(MouseButtonCodes.MOUSE_MIDDLE_BUTTON),
                "Middle mouse button must not remain held down after a Release-function binding fires.");
        }
    }
}
