using Newtonsoft.Json;
using DS4MapperTest;
using DS4MapperTest.GyroActions;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class GyroOrientationTests
    {
        // --- GyroOrientationResolver -------------------------------------------------

        [TestMethod]
        public void ResolveYawPassesThroughUninverted()
        {
            GyroLocalAxisMapping mapping = GyroLocalAxisMapping.CreateDefault(GyroLocalAxisSource.Yaw);

            double result = GyroOrientationResolver.Resolve(mapping, yaw: 12.0, roll: 34.0, pitch: 56.0);

            Assert.AreEqual(12.0, result, 0.0000001);
        }

        [TestMethod]
        public void ResolveRollInvertedNegatesRoll()
        {
            GyroLocalAxisMapping mapping = GyroLocalAxisMapping.CreateDefault(GyroLocalAxisSource.Roll);
            mapping.invertSingle = true;

            double result = GyroOrientationResolver.Resolve(mapping, yaw: 12.0, roll: 34.0, pitch: 56.0);

            Assert.AreEqual(-34.0, result, 0.0000001);
        }

        [TestMethod]
        public void ResolvePitchPassesThroughUninverted()
        {
            GyroLocalAxisMapping mapping = GyroLocalAxisMapping.CreateDefault(GyroLocalAxisSource.Pitch);

            double result = GyroOrientationResolver.Resolve(mapping, yaw: 12.0, roll: 34.0, pitch: 56.0);

            Assert.AreEqual(56.0, result, 0.0000001);
        }

        [TestMethod]
        public void ResolveYawPlusRollFullContributionsSumsBoth()
        {
            GyroLocalAxisMapping mapping = new()
            {
                source = GyroLocalAxisSource.YawPlusRoll,
                yawContribution = 100.0,
                rollContribution = 100.0,
            };

            double result = GyroOrientationResolver.Resolve(mapping, yaw: 10.0, roll: 20.0, pitch: 999.0);

            Assert.AreEqual(30.0, result, 0.0000001);
        }

        [TestMethod]
        public void ResolveYawPlusRollHalfRollContributionScalesRoll()
        {
            GyroLocalAxisMapping mapping = new()
            {
                source = GyroLocalAxisSource.YawPlusRoll,
                yawContribution = 100.0,
                rollContribution = 50.0,
            };

            double result = GyroOrientationResolver.Resolve(mapping, yaw: 10.0, roll: 20.0, pitch: 999.0);

            Assert.AreEqual(20.0, result, 0.0000001); // 10 + (20 * 0.5)
        }

        [TestMethod]
        public void ResolveYawPlusRollNegativeYawContributionInvertsYaw()
        {
            GyroLocalAxisMapping mapping = new()
            {
                source = GyroLocalAxisSource.YawPlusRoll,
                yawContribution = -100.0,
                rollContribution = 50.0,
            };

            double result = GyroOrientationResolver.Resolve(mapping, yaw: 10.0, roll: 20.0, pitch: 999.0);

            Assert.AreEqual(0.0, result, 0.0000001); // -10 + 10
        }

        [TestMethod]
        public void ResolveYawPlusRollQuarterAndInvertedContributions()
        {
            GyroLocalAxisMapping mapping = new()
            {
                source = GyroLocalAxisSource.YawPlusRoll,
                yawContribution = 25.0,
                rollContribution = -70.0,
            };

            double result = GyroOrientationResolver.Resolve(mapping, yaw: 100.0, roll: 100.0, pitch: 999.0);

            Assert.AreEqual(-45.0, result, 0.0000001); // (100*0.25) + (100*-0.70)
        }

        // --- Defaults ------------------------------------------------------------------

        [TestMethod]
        public void OrientationSettingsDefaultMatchesLegacyMouseBehaviour()
        {
            GyroOrientationSettings orientation = GyroOrientationSettings.CreateDefault();

            Assert.AreEqual(GyroSpaceChoice.LocalSpace, orientation.gyroSpace);
            Assert.AreEqual(GyroLocalAxisSource.Yaw, orientation.horizontal.source);
            Assert.IsFalse(orientation.horizontal.invertSingle);
            Assert.AreEqual(GyroLocalAxisSource.Pitch, orientation.vertical.source);
            Assert.IsFalse(orientation.vertical.invertSingle);
        }

        [TestMethod]
        public void NewGyroMouseActionHasCorrectDefaultOrientation()
        {
            GyroMouse action = new();

            Assert.AreEqual(GyroLocalAxisSource.Yaw, action.mouseParams.orientation.horizontal.source);
            Assert.AreEqual(GyroLocalAxisSource.Pitch, action.mouseParams.orientation.vertical.source);
            Assert.AreEqual(100.0, action.mouseParams.orientation.horizontal.yawContribution);
            Assert.AreEqual(100.0, action.mouseParams.orientation.horizontal.rollContribution);
            Assert.AreEqual(100.0, action.mouseParams.orientation.vertical.yawContribution);
            Assert.AreEqual(100.0, action.mouseParams.orientation.vertical.rollContribution);
        }

        // --- Contribution/invert sign sync ----------------------------------------------

        [TestMethod]
        [DataRow(100.0, -100.0)]
        [DataRow(27.0, -27.0)]
        [DataRow(63.5, -63.5)]
        public void InvertOnNegatesPositiveContribution(double startValue, double expected)
        {
            double result = GyroContributionSync.ApplySignFromInvert(startValue, invert: true);
            Assert.AreEqual(expected, result, 0.0000001);
        }

        [TestMethod]
        [DataRow(-100.0, 100.0)]
        [DataRow(-27.0, 27.0)]
        [DataRow(-63.5, 63.5)]
        public void InvertOffRestoresPositiveContribution(double startValue, double expected)
        {
            double result = GyroContributionSync.ApplySignFromInvert(startValue, invert: false);
            Assert.AreEqual(expected, result, 0.0000001);
        }

        [TestMethod]
        public void ContributionSignDrivesInvertToggle()
        {
            Assert.IsFalse(GyroContributionSync.InvertFromContribution(50.0));
            Assert.IsTrue(GyroContributionSync.InvertFromContribution(-35.0));
            Assert.IsFalse(GyroContributionSync.InvertFromContribution(0.0));
        }

        [TestMethod]
        public void InvertToggleIsDisabledOnlyAtExactlyZeroContribution()
        {
            Assert.IsFalse(GyroContributionSync.CanToggleInvert(0.0));
            Assert.IsTrue(GyroContributionSync.CanToggleInvert(1.0));
            Assert.IsTrue(GyroContributionSync.CanToggleInvert(100.0));
            Assert.IsTrue(GyroContributionSync.CanToggleInvert(-1.0));
            Assert.IsTrue(GyroContributionSync.CanToggleInvert(-100.0));
        }

        // --- Legacy JSON migration -------------------------------------------------------

        private static GyroMouseSerializer DeserializeGyroMouse(string settingsJson)
        {
            string json = @"{
              ""Id"": 0,
              ""ActionMode"": ""GyroMouseAction"",
              ""Settings"": " + settingsJson + @"
            }";

            GyroMouseSerializer serializer = new();
            JsonConvert.PopulateObject(json, serializer);
            serializer.PopulateMap();
            return serializer;
        }

        [TestMethod]
        public void MigratesLegacyYawUninverted()
        {
            GyroMouseSerializer serializer = DeserializeGyroMouse(@"{
                ""InvertX"": false,
                ""InvertY"": false,
                ""UseForXAxis"": ""Yaw""
            }");

            var orientation = ((GyroMouse)serializer.MapAction).mouseParams.orientation;
            Assert.AreEqual(GyroLocalAxisSource.Yaw, orientation.horizontal.source);
            Assert.IsFalse(orientation.horizontal.invertSingle);
            Assert.AreEqual(GyroLocalAxisSource.Pitch, orientation.vertical.source);
            Assert.IsFalse(orientation.vertical.invertSingle);
        }

        [TestMethod]
        public void MigratesLegacyYawInverted()
        {
            GyroMouseSerializer serializer = DeserializeGyroMouse(@"{
                ""InvertX"": true,
                ""InvertY"": false,
                ""UseForXAxis"": ""Yaw""
            }");

            var orientation = ((GyroMouse)serializer.MapAction).mouseParams.orientation;
            Assert.AreEqual(GyroLocalAxisSource.Yaw, orientation.horizontal.source);
            Assert.IsTrue(orientation.horizontal.invertSingle);
        }

        [TestMethod]
        public void MigratesLegacyRollInverted()
        {
            GyroMouseSerializer serializer = DeserializeGyroMouse(@"{
                ""InvertX"": true,
                ""InvertY"": false,
                ""UseForXAxis"": ""Roll""
            }");

            var orientation = ((GyroMouse)serializer.MapAction).mouseParams.orientation;
            Assert.AreEqual(GyroLocalAxisSource.Roll, orientation.horizontal.source);
            Assert.IsTrue(orientation.horizontal.invertSingle);
        }

        [TestMethod]
        public void MigratesLegacyVerticalPitchInverted()
        {
            GyroMouseSerializer serializer = DeserializeGyroMouse(@"{
                ""InvertX"": false,
                ""InvertY"": true,
                ""UseForXAxis"": ""Yaw""
            }");

            var orientation = ((GyroMouse)serializer.MapAction).mouseParams.orientation;
            Assert.AreEqual(GyroLocalAxisSource.Pitch, orientation.vertical.source);
            Assert.IsTrue(orientation.vertical.invertSingle);
        }

        [TestMethod]
        public void ExplicitNewFormatIsNotOverwrittenByLegacyFields()
        {
            // A profile that already understands the new schema but (hypothetically, e.g.
            // hand-edited) also carries stale legacy fields disagreeing with it - the new
            // fields must win.
            GyroMouseSerializer serializer = DeserializeGyroMouse(@"{
                ""InvertX"": true,
                ""InvertY"": true,
                ""UseForXAxis"": ""Roll"",
                ""HorizontalControl"": ""Pitch"",
                ""VerticalControl"": ""Yaw"",
                ""HorizontalInvert"": false,
                ""VerticalInvert"": false
            }");

            var orientation = ((GyroMouse)serializer.MapAction).mouseParams.orientation;
            Assert.AreEqual(GyroLocalAxisSource.Pitch, orientation.horizontal.source);
            Assert.IsFalse(orientation.horizontal.invertSingle);
            Assert.AreEqual(GyroLocalAxisSource.Yaw, orientation.vertical.source);
            Assert.IsFalse(orientation.vertical.invertSingle);
        }

        // --- Contribution clamping ---------------------------------------------------

        [TestMethod]
        public void ContributionSetterClampsOutOfRangeValues()
        {
            GyroMouse action = new();
            GyroMouseSerializer.GyroMouseSettings settings = new(action);

            settings.HorizontalYawContribution = 250.0;
            Assert.AreEqual(100.0, action.mouseParams.orientation.horizontal.yawContribution);

            settings.HorizontalRollContribution = -250.0;
            Assert.AreEqual(-100.0, action.mouseParams.orientation.horizontal.rollContribution);
        }
    }
}
