using DS4MapperTest;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Mapping;
using DS4MapperTest.Universal.Profiles;
using DS4MapperTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalMappingRuntimeTests
    {
        [TestInitialize]
        public void Setup()
        {
            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            mapping.PopulateMappings();
            ProfileSerializer.EventInputMapper = mapping;
            TestMapper.KeyReferenceCountDict.Clear();
            TestMapper.MouseButtonReferenceCountDict.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            TestMapper.KeyReferenceCountDict.Clear();
            TestMapper.MouseButtonReferenceCountDict.Clear();
        }

        [TestMethod]
        public void CompilerKeepsUnsupportedBindingsStoredButInactive()
        {
            UniversalProfile profile = CreateProfile(
                "unsupported",
                Binding(UniversalInputId.FaceButtonSouth, 1),
                Binding(UniversalInputId.Mute, 1));
            ControllerCapabilities capabilities = Capabilities(UniversalInputId.FaceButtonSouth);

            UniversalCompiledProfile compiled =
                UniversalProfileRuntimeCompiler.Compile(profile, capabilities);

            Assert.IsTrue(compiled.ActiveBindingIds.ContainsKey(UniversalInputId.FaceButtonSouth));
            Assert.IsFalse(compiled.ActiveBindingIds.ContainsKey(UniversalInputId.Mute));
            Assert.AreEqual(1, compiled.UnsupportedBindings.Count);
            Assert.AreEqual(UniversalInputId.Mute, compiled.UnsupportedBindings[0].Input);
            Assert.AreEqual(2, profile.Bindings.Count);
        }

        [TestMethod]
        public void CompilerMapsRuntimeShapesWithoutControllerSpecificTokens()
        {
            UniversalProfile profile = CreateProfile(
                "shapes",
                Binding(UniversalInputId.LeftTrigger, 1),
                Binding(UniversalInputId.LeftStick, 1),
                Binding(UniversalInputId.PrimaryTouchSurface, 1),
                Binding(UniversalInputId.Gyroscope, 1),
                Binding(UniversalInputId.MiscButton16, 1));

            UniversalCompiledProfile compiled = UniversalProfileRuntimeCompiler.Compile(
                profile,
                Capabilities(
                    UniversalInputId.LeftTrigger,
                    UniversalInputId.LeftStick,
                    UniversalInputId.PrimaryTouchSurface,
                    UniversalInputId.Gyroscope,
                    UniversalInputId.MiscButton16));

            StringAssert.Contains(compiled.LegacyJson, "\"LeftTrigger\"");
            StringAssert.Contains(compiled.LegacyJson, "\"LeftStick\"");
            StringAssert.Contains(compiled.LegacyJson, "\"PrimaryTouchSurface\"");
            StringAssert.Contains(compiled.LegacyJson, "\"Gyroscope\"");
            StringAssert.Contains(compiled.LegacyJson, "\"MiscButton16\"");
            Assert.IsFalse(compiled.LegacyJson.Contains("Cross"));
            Assert.IsFalse(compiled.LegacyJson.Contains("SDL"));
        }

        [TestMethod]
        public void UniversalDPadBindingsCompileToClassicDPadControl()
        {
            InputBindingMeta dpadBinding = UniversalLegacyBindingMap.CreateBindingList()
                .Single(item => item.id == "DPad");

            Assert.AreEqual(InputBindingMeta.InputControlType.DPad, dpadBinding.controlType);

            UniversalProfile profile = CreateProfile(
                "dpad",
                Binding(UniversalInputId.DPadUp, 1),
                Binding(UniversalInputId.DPadDown, 1),
                Binding(UniversalInputId.DPadLeft, 1),
                Binding(UniversalInputId.DPadRight, 1));

            UniversalCompiledProfile compiled = UniversalProfileRuntimeCompiler.Compile(
                profile,
                Capabilities(
                    UniversalInputId.DPadUp,
                    UniversalInputId.DPadDown,
                    UniversalInputId.DPadLeft,
                    UniversalInputId.DPadRight));

            StringAssert.Contains(compiled.LegacyJson, "\"Input\":\"DPad\"");
            Assert.AreEqual("DPad", compiled.ActiveBindingIds[UniversalInputId.DPadUp]);
            Assert.AreEqual("DPad", compiled.ActiveBindingIds[UniversalInputId.DPadDown]);
            Assert.AreEqual("DPad", compiled.ActiveBindingIds[UniversalInputId.DPadLeft]);
            Assert.AreEqual("DPad", compiled.ActiveBindingIds[UniversalInputId.DPadRight]);
        }

        [TestMethod]
        public void MapperProcessesPressHoldReleaseOnce()
        {
            UniversalController controller = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "sdl-1",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));
            RecordingVirtualKeyboard output = new RecordingVirtualKeyboard();
            UniversalMapper mapper = new UniversalMapper(controller, CreateProfile(
                "button",
                Binding(UniversalInputId.FaceButtonSouth, 1)));
            mapper.Start(output, CreateMapping());

            mapper.ProcessSnapshot(State(1, UniversalInputId.FaceButtonSouth, UniversalInputValue.DigitalButton(false)));
            mapper.ProcessSnapshot(State(2, UniversalInputId.FaceButtonSouth, UniversalInputValue.DigitalButton(true)));
            Assert.AreEqual(1, TestMapper.KeyReferenceCountDict.Count);
            mapper.ProcessSnapshot(State(3, UniversalInputId.FaceButtonSouth, UniversalInputValue.DigitalButton(true)));
            Assert.AreEqual(1, TestMapper.KeyReferenceCountDict.Count);
            mapper.ProcessSnapshot(State(4, UniversalInputId.FaceButtonSouth, UniversalInputValue.DigitalButton(false)));

            Assert.AreEqual(0, TestMapper.KeyReferenceCountDict.Count);
            mapper.Stop(true);
        }

        [TestMethod]
        public void MapperReleaseRunsOnDisconnect()
        {
            UniversalController controller = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "sdl-2",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));
            RecordingVirtualKeyboard output = new RecordingVirtualKeyboard();
            UniversalMapper mapper = new UniversalMapper(controller, CreateProfile(
                "disconnect",
                Binding(UniversalInputId.FaceButtonSouth, 1)));
            mapper.Start(output, CreateMapping());

            mapper.ProcessSnapshot(State(1, UniversalInputId.FaceButtonSouth, UniversalInputValue.DigitalButton(true)));
            Assert.AreEqual(1, TestMapper.KeyReferenceCountDict.Count);
            mapper.ProcessSnapshot(UniversalControllerStateSnapshot.Disconnected(2));

            Assert.AreEqual(0, TestMapper.KeyReferenceCountDict.Count);
            mapper.Stop(true);
        }

        [TestMethod]
        public void RuntimeCreatesOneSessionPerAuthoritativeController()
        {
            UniversalController first = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "same",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));
            UniversalController duplicate = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "same",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));
            FakeUniversalBackend backend = new FakeUniversalBackend(first, duplicate);
            using UniversalMappingRuntime runtime = CreateRuntime(backend);

            Assert.IsTrue(runtime.Start());

            Assert.AreEqual(1, runtime.Sessions.Count);
            Assert.AreEqual(first.Identity.LogicalControllerId, runtime.Sessions[0].LogicalControllerId);
        }

        [TestMethod]
        public void RuntimeLeavesDiagnosticControllersNonAuthoritative()
        {
            FakeUniversalBackend backend = new FakeUniversalBackend(CreateController(
                UniversalControllerBackendIds.DiagnosticObserver,
                "diag-1",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth)));
            using UniversalMappingRuntime runtime = CreateRuntime(backend);

            Assert.IsTrue(runtime.Start());

            Assert.AreEqual(0, runtime.Sessions.Count);
        }

        [TestMethod]
        public void RuntimeKeepsIdenticalModelsSeparateBySession()
        {
            FakeUniversalBackend backend = new FakeUniversalBackend(
                CreateController(UniversalControllerBackendIds.Sdl3, "1", false, Capabilities(UniversalInputId.FaceButtonSouth), 0x045E, 0x02EA),
                CreateController(UniversalControllerBackendIds.Sdl3, "2", false, Capabilities(UniversalInputId.FaceButtonSouth), 0x045E, 0x02EA));
            using UniversalMappingRuntime runtime = CreateRuntime(backend);

            Assert.IsTrue(runtime.Start());

            Assert.AreEqual(2, runtime.Sessions.Count);
        }

        [TestMethod]
        public void RuntimeGivesNativeSteamControllerPriorityOverSdl()
        {
            UniversalController native = CreateController(
                UniversalControllerBackendIds.SteamControllerNative,
                "native",
                true,
                Capabilities(UniversalInputId.FaceButtonSouth));
            UniversalController sdl = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "sdl",
                true,
                Capabilities(UniversalInputId.FaceButtonSouth));
            FakeUniversalBackend backend = new FakeUniversalBackend(native, sdl);
            using UniversalMappingRuntime runtime = CreateRuntime(backend);

            Assert.IsTrue(runtime.Start());

            Assert.AreEqual(1, runtime.Sessions.Count);
            Assert.AreEqual(UniversalControllerBackendIds.SteamControllerNative, runtime.Sessions[0].BackendName);
        }

        [TestMethod]
        public void RemovalDisposesOnlyTheRemovedSession()
        {
            UniversalController first = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "1",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));
            UniversalController second = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "2",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));
            FakeUniversalBackend backend = new FakeUniversalBackend(first, second);
            using UniversalMappingRuntime runtime = CreateRuntime(backend);
            runtime.Start();

            backend.Remove(first);
            runtime.Refresh();

            Assert.AreEqual(1, runtime.Sessions.Count);
            Assert.AreEqual(second.Identity.LogicalControllerId, runtime.Sessions[0].LogicalControllerId);
        }

        [TestMethod]
        public void FailedProfileActivationDoesNotCreatePartialSession()
        {
            UniversalController controller = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "bad-profile",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));
            FakeProfileSelector selector = new FakeProfileSelector(CreateProfile(
                "bad",
                new UniversalProfileBinding
                {
                    ActionSet = 0,
                    ActionLayer = 0,
                    Input = UniversalInputId.FaceButtonSouth,
                    ValueKind = UniversalInputValueKind.Stick2D,
                    Action = 1,
                }));
            using UniversalMappingRuntime runtime = new UniversalMappingRuntime(
                new UniversalControllerManager(new[] { new FakeUniversalBackend(controller) }),
                selector,
                new RecordingVirtualKeyboard(),
                CreateMapping());

            Assert.IsTrue(runtime.Start());

            Assert.AreEqual(0, runtime.Sessions.Count);
        }

        [TestMethod]
        public void ProfileSwitchReleasesOldActionBeforeNewProfileActivates()
        {
            UniversalController controller = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "switch",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth, UniversalInputId.FaceButtonEast));
            RecordingVirtualKeyboard output = new RecordingVirtualKeyboard();
            using UniversalMapperSession session = new UniversalMapperSession(
                controller,
                CreateProfile("old", Binding(UniversalInputId.FaceButtonSouth, 1)),
                output,
                CreateMapping());
            controller.PublishState(State(1, UniversalInputId.FaceButtonSouth, UniversalInputValue.DigitalButton(true)));
            session.ProcessCurrentState();
            Assert.AreEqual(1, TestMapper.KeyReferenceCountDict.Count);

            session.SwitchProfile(CreateProfile("new", Binding(UniversalInputId.FaceButtonEast, 1)));

            Assert.AreEqual(0, TestMapper.KeyReferenceCountDict.Count);
        }

        [TestMethod]
        public void DeviceListKeepsItsItemsWhenAnotherControllerConnects()
        {
            // The editor works from the DeviceListItem it was handed. Replacing
            // that item reads to the window as the current device disconnecting,
            // which tears the editor down and drops unsaved edits, so an
            // unrelated hotplug must leave the existing items alone.
            using UniversalMapperSession first = CreateSession("first");
            using UniversalMapperSession second = CreateSession("second");
            ObservableCollection<DeviceListItem> list = new ObservableCollection<DeviceListItem>();

            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { first }, CreateDeviceListItem);
            DeviceListItem firstItem = list.Single();

            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { first, second }, CreateDeviceListItem);

            Assert.AreEqual(2, list.Count);
            Assert.AreSame(firstItem, list[0]);
            Assert.AreEqual(second.LogicalControllerId, list[1].UniversalSession.LogicalControllerId);
        }

        [TestMethod]
        public void DeviceListDropsItemsForSessionsThatWentAway()
        {
            using UniversalMapperSession first = CreateSession("first");
            using UniversalMapperSession second = CreateSession("second");
            ObservableCollection<DeviceListItem> list = new ObservableCollection<DeviceListItem>();

            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { first, second }, CreateDeviceListItem);
            DeviceListItem secondItem = list[1];

            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { second }, CreateDeviceListItem);

            Assert.AreSame(secondItem, list.Single());
        }

        [TestMethod]
        public void DeviceListGivesAReplacementItemAFreeIndex()
        {
            using UniversalMapperSession first = CreateSession("first");
            using UniversalMapperSession second = CreateSession("second");
            using UniversalMapperSession third = CreateSession("third");
            ObservableCollection<DeviceListItem> list = new ObservableCollection<DeviceListItem>();

            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { first, second }, CreateDeviceListItem);
            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { second }, CreateDeviceListItem);
            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { second, third }, CreateDeviceListItem);

            CollectionAssert.AreEquivalent(
                new[] { 1, 0 },
                list.Select(item => item.ItemIndex).ToArray());
            Assert.AreEqual(2, list.Select(item => item.ItemIndex).Distinct().Count());
        }

        [TestMethod]
        public void DeviceListIgnoresDisposedSessions()
        {
            UniversalMapperSession session = CreateSession("gone");
            ObservableCollection<DeviceListItem> list = new ObservableCollection<DeviceListItem>();

            session.Dispose();
            ControllerListViewModel.ReconcileUniversalDeviceList(
                list, new[] { session }, CreateDeviceListItem);

            Assert.AreEqual(0, list.Count);
        }

        private static DeviceListItem CreateDeviceListItem(UniversalMapperSession session, int itemIndex)
        {
            return new DeviceListItem(session, itemIndex, null);
        }

        private static UniversalMapperSession CreateSession(string backendSessionId)
        {
            UniversalController controller = CreateController(
                UniversalControllerBackendIds.Sdl3,
                backendSessionId,
                false,
                Capabilities(UniversalInputId.FaceButtonSouth));

            return new UniversalMapperSession(
                controller,
                CreateProfile(backendSessionId, Binding(UniversalInputId.FaceButtonSouth, 1)),
                new RecordingVirtualKeyboard(),
                CreateMapping());
        }

        [TestMethod]
        public void SessionActiveProfileReflectsWhatTheSelectorChoseAndAnySwitch()
        {
            // Backs profile-selection UI: the editor reads Session.ActiveProfile to tell
            // the user which profile is actually live for a connected controller right now.
            UniversalController controller = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "active-profile",
                false,
                Capabilities(UniversalInputId.FaceButtonSouth, UniversalInputId.FaceButtonEast));
            RecordingVirtualKeyboard output = new RecordingVirtualKeyboard();
            UniversalProfile oldProfile = CreateProfile("old", Binding(UniversalInputId.FaceButtonSouth, 1));
            using UniversalMapperSession session = new UniversalMapperSession(
                controller, oldProfile, output, CreateMapping());

            Assert.AreEqual(oldProfile.ProfileId, session.ActiveProfile.ProfileId);
            Assert.AreEqual("old", session.ActiveProfile.DisplayName);

            UniversalProfile newProfile = CreateProfile("new", Binding(UniversalInputId.FaceButtonEast, 1));
            session.SwitchProfile(newProfile);

            Assert.AreEqual(newProfile.ProfileId, session.ActiveProfile.ProfileId);
            Assert.AreEqual("new", session.ActiveProfile.DisplayName);
        }

        [TestMethod]
        public void UniversalSnapshotsFeedAnalogueStickTouchAndMotionShapes()
        {
            UniversalController controller = CreateController(
                UniversalControllerBackendIds.Sdl3,
                "shapes-runtime",
                false,
                Capabilities(
                    UniversalInputId.LeftTrigger,
                    UniversalInputId.LeftTriggerFullPull,
                    UniversalInputId.LeftStick,
                    UniversalInputId.DPadUp,
                    UniversalInputId.PrimaryTouchSurface,
                    UniversalInputId.PrimaryTouchSurfaceClick,
                    UniversalInputId.Gyroscope,
                    UniversalInputId.Accelerometer));
            UniversalMapper mapper = new UniversalMapper(controller, CreateProfile(
                "runtime shapes",
                Binding(UniversalInputId.LeftTrigger, 1),
                Binding(UniversalInputId.LeftStick, 1),
                Binding(UniversalInputId.DPadUp, 1),
                Binding(UniversalInputId.PrimaryTouchSurface, 1),
                Binding(UniversalInputId.Gyroscope, 1)));
            mapper.Start(new RecordingVirtualKeyboard(), CreateMapping());

            mapper.ProcessSnapshot(new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow,
                1,
                true,
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [UniversalInputId.LeftTrigger] = UniversalInputValue.AnalogAxis(0.5),
                    [UniversalInputId.LeftTriggerFullPull] = UniversalInputValue.DigitalButton(true),
                    [UniversalInputId.LeftStick] = UniversalInputValue.Stick(0.25, 0.75),
                    [UniversalInputId.DPadUp] = UniversalInputValue.DigitalButton(true),
                    [UniversalInputId.PrimaryTouchSurface] = UniversalInputValue.TouchSurface(new[]
                    {
                        new UniversalTouchContact(1, true, 0.2, 0.8, 0.5),
                        new UniversalTouchContact(2, true, 0.4, 0.6, null),
                    }, clickPressed: true),
                    [UniversalInputId.PrimaryTouchSurfaceClick] = UniversalInputValue.DigitalButton(true),
                    [UniversalInputId.Gyroscope] = UniversalInputValue.Gyroscope(1, 2, 3),
                    [UniversalInputId.Accelerometer] = UniversalInputValue.Accelerometer(4, 5, 6),
                }));

            Assert.AreEqual(0, mapper.CompiledProfile.UnsupportedBindings.Count);
            mapper.Stop(true);
        }

        private static UniversalMappingRuntime CreateRuntime(FakeUniversalBackend backend)
        {
            return new UniversalMappingRuntime(
                new UniversalControllerManager(new[] { backend }),
                new FakeProfileSelector(CreateProfile("default", Binding(UniversalInputId.FaceButtonSouth, 1))),
                new RecordingVirtualKeyboard(),
                CreateMapping());
        }

        private static UniversalProfile CreateProfile(string name, params UniversalProfileBinding[] bindings)
        {
            UniversalProfile profile = new UniversalProfile
            {
                DisplayName = name,
                CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                ProfileSettings = new JObject
                {
                    ["OutputGamepadSettings"] = new JObject { ["Enabled"] = false },
                },
            };

            UniversalProfileActionSet set = new UniversalProfileActionSet { Index = 0, Name = "Set 1" };
            UniversalProfileActionLayer layer = new UniversalProfileActionLayer { Index = 0, Name = "Default" };
            layer.Actions.Add(ButtonAction(1, "Space"));
            set.Layers.Add(layer);
            profile.ActionSets.Add(set);
            profile.Bindings.AddRange(bindings);
            return profile;
        }

        private static JObject ButtonAction(int id, string key)
        {
            return new JObject
            {
                ["id"] = id,
                ["type"] = "ButtonAction",
                ["payload"] = new JObject
                {
                    ["Id"] = id,
                    ["ActionMode"] = "ButtonAction",
                    ["Functions"] = new JArray(new JObject
                    {
                        ["Type"] = "NormalPress",
                        ["OutputActions"] = new JArray(new JObject
                        {
                            ["Type"] = "Keyboard",
                            ["Code"] = key,
                        }),
                    }),
                },
            };
        }

        private static UniversalProfileBinding Binding(UniversalInputId input, int action)
        {
            return new UniversalProfileBinding
            {
                ActionSet = 0,
                ActionLayer = 0,
                Input = input,
                ValueKind = UniversalInputCatalog.GetMetadata(input).ValueKind,
                Action = action,
            };
        }

        private static ControllerCapabilities Capabilities(params UniversalInputId[] inputs)
        {
            return new ControllerCapabilities(
                new ControllerDisplayInfo("Synthetic Controller"),
                inputs.Select(input => new ControllerInputDescriptor(
                    input,
                    UniversalInputCatalog.GetMetadata(input).ValueKind,
                    true,
                    input.ToString(),
                    string.Empty,
                    new ControllerInputSource("test", input.ToString(), input.ToString()))));
        }

        private static UniversalController CreateController(
            string backend,
            string session,
            bool originalSteam,
            ControllerCapabilities capabilities,
            ushort? vendorId = null,
            ushort? productId = null)
        {
            UniversalDeviceIdentity deviceIdentity = new UniversalDeviceIdentity(
                backend,
                session,
                vendorId.HasValue && productId.HasValue
                    ? $"vid-{vendorId.Value:X4}|pid-{productId.Value:X4}"
                    : string.Empty,
                vendorId,
                productId,
                string.Empty,
                string.Empty,
                string.Empty,
                originalSteam);

            return new UniversalController(
                new UniversalControllerIdentity(
                    Guid.NewGuid(),
                    backend,
                    session,
                    deviceIdentity,
                    DateTimeOffset.UtcNow),
                capabilities,
                State(0, UniversalInputId.FaceButtonSouth, UniversalInputValue.DigitalButton(false)));
        }

        private static UniversalControllerStateSnapshot State(
            long sequence,
            UniversalInputId input,
            UniversalInputValue value)
        {
            return new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow.AddMilliseconds(sequence),
                sequence,
                true,
                new Dictionary<UniversalInputId, UniversalInputValue>
                {
                    [input] = value,
                });
        }

        private static FakerInputMapping CreateMapping()
        {
            FakerInputMapping mapping = new FakerInputMapping();
            mapping.PopulateConstants();
            mapping.PopulateMappings();
            return mapping;
        }

        private sealed class RecordingVirtualKeyboard : VirtualKBMBase
        {
            public List<uint> PressedKeys { get; } = new List<uint>();
            public List<uint> ReleasedKeys { get; } = new List<uint>();

            public override bool Connect() => true;
            public override bool Disconnect() => true;
            public override void MoveRelativeMouse(int x, int y) { }
            public override void MoveAbsoluteMouse(double x, double y) { }
            public override void PerformMouseWheelEvent(int vertical, int horizontal) { }
            public override void PerformMouseButtonEvent(uint mouseButton) { }
            public override void PerformMouseButtonPress(uint mouseButton) { }
            public override void PerformMouseButtonRelease(uint mouseButton) { }
            public override void PerformKeyPress(uint key) => PressedKeys.Add(key);
            public override void PerformKeyPressAlt(uint key) => PressedKeys.Add(key);
            public override void PerformKeyRelease(uint key) => ReleasedKeys.Add(key);
            public override void PerformKeyReleaseAlt(uint key) => ReleasedKeys.Add(key);
            public override string GetDisplayName() => "recording";
            public override string GetIdentifier() => "recording";
            public override string GetFullDisplayName() => "recording";
        }

        private sealed class FakeProfileSelector : IUniversalProfileSelector
        {
            private readonly UniversalProfile profile;

            public FakeProfileSelector(UniversalProfile profile)
            {
                this.profile = profile;
            }

            public UniversalProfile SelectProfile(IUniversalController controller)
            {
                return profile?.Clone();
            }
        }

        private sealed class FakeUniversalBackend : IUniversalControllerBackend
        {
            private readonly List<IUniversalController> controllers;

            public string BackendName => "fake";
            public IReadOnlyList<IUniversalController> Controllers => controllers.ToArray();
            public event EventHandler ControllersChanged;

            public FakeUniversalBackend(params IUniversalController[] controllers)
            {
                this.controllers = controllers.ToList();
            }

            public void Remove(IUniversalController controller)
            {
                controllers.Remove(controller);
                if (controller is UniversalController mutable)
                {
                    mutable.MarkDisconnected();
                }

                ControllersChanged?.Invoke(this, EventArgs.Empty);
            }

            public bool Start(out string error)
            {
                error = string.Empty;
                ControllersChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }

            public void Refresh()
            {
            }

            public void Stop()
            {
                foreach (IUniversalController controller in controllers)
                {
                    if (controller is UniversalController mutable)
                    {
                        mutable.MarkDisconnected();
                    }
                }

                ControllersChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Dispose()
            {
                foreach (IUniversalController controller in controllers)
                {
                    controller.Dispose();
                }
            }
        }
    }
}
