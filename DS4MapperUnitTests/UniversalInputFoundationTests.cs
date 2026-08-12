using DS4MapperTest.Universal;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class UniversalInputFoundationTests
    {
        [TestMethod]
        public void EveryDeclaredUniversalInputHasValidMetadata()
        {
            foreach (UniversalInputId id in Enum.GetValues(typeof(UniversalInputId)))
            {
                UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(id);

                Assert.AreEqual(id, metadata.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.DisplayName), id.ToString());
                Assert.IsTrue(Enum.IsDefined(typeof(UniversalInputValueKind), metadata.ValueKind), id.ToString());
                Assert.IsTrue(Enum.IsDefined(typeof(UniversalInputCategory), metadata.Category), id.ToString());
            }
        }

        [TestMethod]
        public void UniversalInputIdsAreUnique()
        {
            ushort[] values = Enum.GetValues(typeof(UniversalInputId))
                .Cast<UniversalInputId>()
                .Select(id => (ushort)id)
                .ToArray();

            Assert.AreEqual(values.Length, values.Distinct().Count());
            Assert.AreEqual(values.Length, UniversalInputCatalog.All.Select(item => item.Id).Distinct().Count());
        }

        [TestMethod]
        public void UniversalInputCategoriesRepresentExpectedControllerInputs()
        {
            UniversalInputCategory[] expected =
            {
                UniversalInputCategory.FaceButton,
                UniversalInputCategory.DPad,
                UniversalInputCategory.Shoulder,
                UniversalInputCategory.Trigger,
                UniversalInputCategory.Stick,
                UniversalInputCategory.StickClick,
                UniversalInputCategory.StickTouch,
                UniversalInputCategory.Menu,
                UniversalInputCategory.System,
                UniversalInputCategory.Capture,
                UniversalInputCategory.Mute,
                UniversalInputCategory.QuickAccess,
                UniversalInputCategory.RearControl,
                UniversalInputCategory.SideControl,
                UniversalInputCategory.TouchSurface,
                UniversalInputCategory.TouchSurfaceClick,
                UniversalInputCategory.MotionSensor,
                UniversalInputCategory.Miscellaneous,
            };

            HashSet<UniversalInputCategory> actual = UniversalInputCatalog.All
                .Select(item => item.Category)
                .ToHashSet();

            foreach (UniversalInputCategory category in expected)
            {
                Assert.IsTrue(actual.Contains(category), category.ToString());
            }
        }

        [TestMethod]
        public void UniversalInputIdsDoNotUseControllerLocalNames()
        {
            string[] prohibited =
            {
                "Cross",
                "Circle",
                "Square",
                "Triangle",
                "ZL",
                "ZR",
                "QAM",
                "L4",
                "R4",
                "L5",
                "R5",
            };

            foreach (UniversalInputId id in Enum.GetValues(typeof(UniversalInputId)))
            {
                string name = id.ToString();
                foreach (string prohibitedName in prohibited)
                {
                    Assert.IsFalse(name.Contains(prohibitedName, StringComparison.Ordinal), name);
                }
            }
        }

        [TestMethod]
        public void ButtonAxisStickTouchAndSensorInputsKeepDistinctValueKinds()
        {
            AssertKind(UniversalInputId.FaceButtonSouth, UniversalInputValueKind.DigitalButton);
            AssertKind(UniversalInputId.LeftTrigger, UniversalInputValueKind.AnalogAxis1D);
            AssertKind(UniversalInputId.LeftStick, UniversalInputValueKind.Stick2D);
            AssertKind(UniversalInputId.PrimaryTouchSurface, UniversalInputValueKind.TouchSurface);
            AssertKind(UniversalInputId.Gyroscope, UniversalInputValueKind.Gyroscope);
            AssertKind(UniversalInputId.Accelerometer, UniversalInputValueKind.Accelerometer);
        }

        [TestMethod]
        public void ControllerCapabilitiesReportSupportedInputs()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Synthetic Pad"),
                new[]
                {
                    Descriptor(UniversalInputId.FaceButtonSouth),
                    Descriptor(UniversalInputId.LeftTrigger),
                    Descriptor(UniversalInputId.LeftStick),
                });

            Assert.IsTrue(capabilities.Supports(UniversalInputId.FaceButtonSouth));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftTrigger));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftStick));
        }

        [TestMethod]
        public void UnsupportedInputsAreNotReportedAsSupported()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("No Motion Pad"),
                new[]
                {
                    Descriptor(UniversalInputId.FaceButtonSouth),
                    Descriptor(UniversalInputId.Gyroscope, isSupported: false),
                });

            Assert.IsFalse(capabilities.Supports(UniversalInputId.RightTrigger));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.Gyroscope));
            Assert.IsTrue(capabilities.TryGetDescriptor(UniversalInputId.Gyroscope, out ControllerInputDescriptor descriptor));
            Assert.IsFalse(descriptor.IsSupported);
        }

        [TestMethod]
        public void DuplicateCapabilityDescriptorsAreRejected()
        {
            try
            {
                new ControllerCapabilities(
                    new ControllerDisplayInfo("Duplicate Pad"),
                    new[]
                    {
                        Descriptor(UniversalInputId.FaceButtonSouth),
                        Descriptor(UniversalInputId.FaceButtonSouth),
                    });

                Assert.Fail("Duplicate descriptors should be rejected.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void ControllerCanBeRepresentedWithoutTouchOrMotionSupport()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Basic Pad"),
                new[]
                {
                    Descriptor(UniversalInputId.FaceButtonSouth),
                    Descriptor(UniversalInputId.LeftStick),
                    Descriptor(UniversalInputId.RightStick),
                });

            Assert.IsFalse(capabilities.Supports(UniversalInputId.PrimaryTouchSurface));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.Gyroscope));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.Accelerometer));
        }

        [TestMethod]
        public void ControllerCanExposeOneTouchSurface()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Single Touch Pad"),
                new[]
                {
                    Descriptor(UniversalInputId.PrimaryTouchSurface),
                    Descriptor(UniversalInputId.PrimaryTouchSurfaceClick),
                });

            Assert.IsTrue(capabilities.Supports(UniversalInputId.PrimaryTouchSurface));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.LeftTouchSurface));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.RightTouchSurface));
        }

        [TestMethod]
        public void ControllerCanExposeTwoIndependentTouchSurfaces()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Dual Touch Pad"),
                new[]
                {
                    Descriptor(UniversalInputId.LeftTouchSurface),
                    Descriptor(UniversalInputId.RightTouchSurface),
                    Descriptor(UniversalInputId.LeftTouchSurfaceClick),
                    Descriptor(UniversalInputId.RightTouchSurfaceClick),
                });

            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftTouchSurface));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightTouchSurface));
            Assert.IsFalse(capabilities.Supports(UniversalInputId.PrimaryTouchSurface));
        }

        [TestMethod]
        public void ControllerCanExposePaddlesStickTouchAndMiscellaneousInputs()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Extended Pad"),
                new[]
                {
                    Descriptor(UniversalInputId.LeftRearPrimary),
                    Descriptor(UniversalInputId.RightRearSecondary),
                    Descriptor(UniversalInputId.LeftStickTouch),
                    Descriptor(UniversalInputId.RightStickTouch),
                    Descriptor(UniversalInputId.MiscButton1),
                    Descriptor(UniversalInputId.MiscAxis1),
                });

            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftRearPrimary));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.RightRearSecondary));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.LeftStickTouch));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.MiscButton1));
            Assert.IsTrue(capabilities.Supports(UniversalInputId.MiscAxis1));
        }

        [TestMethod]
        public void DisplayInformationFallsBackWhenNativeLabelOrGlyphIsUnavailable()
        {
            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("", glyphFamily: ""),
                new[]
                {
                    new ControllerInputDescriptor(
                        UniversalInputId.FaceButtonSouth,
                        UniversalInputValueKind.DigitalButton),
                    new ControllerInputDescriptor(
                        UniversalInputId.Capture,
                        UniversalInputValueKind.DigitalButton,
                        nativeDisplayLabel: "Create",
                        glyphKey: "dualsense/create"),
                });

            Assert.AreEqual(ControllerDisplayInfo.UnknownControllerName, capabilities.DisplayInfo.DisplayName);
            Assert.AreEqual("Face Button South", capabilities.GetDisplayLabel(UniversalInputId.FaceButtonSouth));
            Assert.AreEqual("Create", capabilities.GetDisplayLabel(UniversalInputId.Capture));
            Assert.AreEqual("generic:FaceButtonSouth", capabilities.GetGlyphKey(UniversalInputId.FaceButtonSouth));
            Assert.AreEqual("dualsense/create", capabilities.GetGlyphKey(UniversalInputId.Capture));
        }

        [TestMethod]
        public void StoredBindingIdentityIsIndependentOfCurrentControllerSupport()
        {
            UniversalInputBindingIdentity storedBinding =
                new UniversalInputBindingIdentity(UniversalInputId.Mute);

            ControllerCapabilities capabilities = new ControllerCapabilities(
                new ControllerDisplayInfo("Controller Without Mute"),
                new[] { Descriptor(UniversalInputId.FaceButtonSouth) });

            UniversalInputStateSnapshot activeInput = new UniversalInputStateSnapshot(
                UniversalInputId.FaceButtonSouth,
                UniversalInputValueKind.DigitalButton,
                isActive: true);

            UniversalInputEditorPresentation hiddenBinding = new UniversalInputEditorPresentation(
                UniversalInputId.Mute,
                UniversalInputEditorVisibility.Hidden);

            Assert.AreEqual(UniversalInputId.Mute, storedBinding.InputId);
            Assert.IsFalse(capabilities.Supports(storedBinding.InputId));
            Assert.IsTrue(activeInput.IsActive);
            Assert.AreEqual(UniversalInputId.FaceButtonSouth, activeInput.InputId);
            Assert.AreEqual(UniversalInputEditorVisibility.Hidden, hiddenBinding.Visibility);
            Assert.AreEqual(storedBinding.InputId, hiddenBinding.InputId);
        }

        private static void AssertKind(UniversalInputId id, UniversalInputValueKind expected)
        {
            Assert.AreEqual(expected, UniversalInputCatalog.GetMetadata(id).ValueKind);
        }

        private static ControllerInputDescriptor Descriptor(
            UniversalInputId id,
            bool isSupported = true)
        {
            return new ControllerInputDescriptor(
                id,
                UniversalInputCatalog.GetMetadata(id).ValueKind,
                isSupported);
        }
    }
}
