using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DS4MapperUnitTests
{
    // Angle Calibration is one profile-wide setting (Profile.CalibMode/CalibRwc/
    // CalibInGameSens/CalibCounts/CalibPresetName) that every panel showing
    // CalibrationModeControl edits: Gyro, Stick/Trackpad Mouse, Stick/Touchpad Flick
    // Stick, Hybrid Aim and the Flick Turn output binding. Two ViewModels back that one
    // control, so the rules below have to hold in both or the panels disagree about a
    // value they all share.
    //
    // These are source-text checks in the style of MainWindowLegacySurfaceTests: both
    // ViewModels need a live WPF Application.Current.Dispatcher to construct, which is
    // not practical in this test host.
    [TestClass]
    public class AngleCalibrationSurfaceTests
    {
        // In RWC mode, Counts is derived. The ViewModel suppresses its own profile change
        // handler while writing, so nothing else refreshes its cached counts: without this
        // the panel showed the pre-edit Counts and switching to Counts mode re-derived RWC
        // from that stale number, reverting the edit.
        [TestMethod]
        public void EditingRwcRecalculatesCountsInRwcMode()
        {
            string source = ReadSourceFile("DS4MapperTest", "ViewModels", "GyroCalibrationViewModel.cs");
            string setter = ExtractBlock(source, "public double RealWorldCalibration");

            StringAssert.Contains(setter, "if (IsRwcMode) CalculateCountsFromRwc();",
                "Editing RWC in RWC mode must recompute the derived Counts value.");
        }

        // A preset names an RWC and nothing else. In-Game Sensitivity is the player's own
        // game setting and has to survive picking one, in either mode.
        [TestMethod]
        public void PresetSetsRwcAndLeavesInGameSensAlone()
        {
            string gyroVM = ReadSourceFile("DS4MapperTest", "ViewModels", "GyroCalibrationViewModel.cs");
            string presetSetter = ExtractBlock(gyroVM, "public GameCalibPreset SelectedPreset");

            StringAssert.Contains(presetSetter, "FullTurnCounts = next.RWC * 360.0 / InGameSens;",
                "From Counts mode a preset must move Counts, not In-Game Sensitivity.");
            StringAssert.Contains(presetSetter, "RealWorldCalibration = next.RWC;",
                "From RWC mode a preset must set RWC directly.");

            string buttonVM = ReadSourceFile("DS4MapperTest", "ViewModels", "ButtonActionEditViewModel.cs");
            string cameraTurnPresetSetter = ExtractBlock(buttonVM, "public GameCalibPreset SelectedCameraTurnPreset");

            StringAssert.Contains(cameraTurnPresetSetter,
                "CameraTurnCounts360 = next.RWC * 360.0 / CameraTurnInGameSens;",
                "The Flick Turn panel must apply presets the same way every other panel does.");
            StringAssert.Contains(cameraTurnPresetSetter, "CameraTurnRWC = next.RWC;",
                "From RWC mode a preset must set RWC directly.");
        }

        // Gyro Mouse, both Flick Sticks and the camera turn outputs all read this one
        // calibration, so every panel that writes it has to reach all of them.
        [TestMethod]
        public void BothCalibrationWritersUpdateEveryCalibratedAction()
        {
            string gyroSync = ExtractBlock(
                ReadSourceFile("DS4MapperTest", "ViewModels", "GyroCalibrationViewModel.cs"),
                "private void SyncCalibToProfile()");
            string cameraTurnSync = ExtractBlock(
                ReadSourceFile("DS4MapperTest", "ViewModels", "ButtonActionEditViewModel.cs"),
                "private void SyncCalibFromCameraTurnToProfile()");

            foreach (string sync in new[] { gyroSync, cameraTurnSync })
            {
                StringAssert.Contains(sync, "is GyroMouse gyroMouse");
                StringAssert.Contains(sync, "is StickFlickStick sfs");
                StringAssert.Contains(sync, "is TouchpadFlickStick tfs");
                StringAssert.Contains(sync, "OutputActionData.ActionType.CameraTurn");
            }
        }

        // A panel that stops listening to the profile shows, and then writes back, values
        // another panel has already replaced. The control owns that subscription so it
        // lasts exactly as long as the panel is on screen.
        [TestMethod]
        public void CalibrationPanelsFollowTheProfileWhileTheyAreOnScreen()
        {
            string control = ReadSourceFile("DS4MapperTest", "Views", "Shared", "CalibrationModeControl.xaml.cs");

            StringAssert.Contains(control, "Loaded += CalibrationModeControl_Loaded;");
            StringAssert.Contains(control, "Unloaded += CalibrationModeControl_Unloaded;");
            StringAssert.Contains(control, "DataContextChanged += CalibrationModeControl_DataContextChanged;");
            StringAssert.Contains(control, "calibVM.AttachProfileCalibEvents();");
            StringAssert.Contains(control, "calibVM.DetachProfileCalibEvents();");

            foreach (string viewModel in new[] { "GyroCalibrationViewModel.cs", "ButtonActionEditViewModel.cs" })
            {
                string source = ReadSourceFile("DS4MapperTest", "ViewModels", viewModel);
                StringAssert.Contains(source, "ICalibrationPanelViewModel",
                    $"{viewModel} backs CalibrationModeControl and must implement its lifecycle.");
                StringAssert.Contains(source, "public void AttachProfileCalibEvents()");
                StringAssert.Contains(source, "public void DetachProfileCalibEvents()");
                StringAssert.Contains(source, "public void BeginPanelInit()");
                StringAssert.Contains(source, "CalibRwcChanged -=",
                    $"{viewModel} must release its profile subscriptions; the profile outlives the panel.");
            }
        }

        // HandyControl's NumericUpDown fires ValueChanged(Minimum) while it initialises,
        // before the binding has handed it the real number. Every field bound into a
        // ViewModel has to ignore that write or it lands in the profile as a zeroed
        // calibration the moment a panel appears.
        [TestMethod]
        public void EditableFieldsIgnoreWritesUntilTheControlHasSettled()
        {
            string gyroVM = ReadSourceFile("DS4MapperTest", "ViewModels", "GyroCalibrationViewModel.cs");
            foreach (string property in new[] { "public double FullTurnCounts",
                "public double RealWorldCalibration", "public double InGameSens" })
            {
                StringAssert.Contains(ExtractBlock(gyroVM, property), "if (!_modelReady) return;",
                    $"{property} is bound to a NumericUpDown and must ignore its init write.");
            }

            string buttonVM = ReadSourceFile("DS4MapperTest", "ViewModels", "ButtonActionEditViewModel.cs");
            foreach (string property in new[] { "public double MasterCalibrationValue",
                "public double CameraTurnInGameSens" })
            {
                StringAssert.Contains(ExtractBlock(buttonVM, property), "if (!_cameraTurnReady) return;",
                    $"{property} is bound to a NumericUpDown and must ignore its init write.");
            }
        }

        // The mode's hidden field keeps a live TwoWay binding, and a NumericUpDown clamps
        // what it is handed to its own Maximum before writing it back, so a lower ceiling
        // on the off-screen field silently truncates the value the visible one holds.
        [TestMethod]
        public void CalibrationFieldsShareOneRange()
        {
            string xaml = ReadSourceFile("DS4MapperTest", "Views", "Shared", "CalibrationModeControl.xaml");
            string[] maximums = Regex.Matches(xaml, "Maximum=\"(?<value>[0-9.]+)\"")
                .Select(match => match.Groups["value"].Value)
                .ToArray();

            // Three calibration fields plus In-Game Sensitivity, which has its own range.
            Assert.AreEqual(4, maximums.Length, "Unexpected number of numeric fields in the panel.");
            Assert.AreEqual(3, maximums.Count(value => value == "99999999.9999"),
                "Counts, RWC and the derived field all bind the same values and must share one maximum.");
        }

        private static string ExtractBlock(string source, string declaration)
        {
            int start = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, $"Could not find '{declaration}' in the source.");

            int depth = 0;
            bool opened = false;
            for (int i = start; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (opened && depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail($"Could not find the end of the '{declaration}' block.");
            return null;
        }

        private static string ReadSourceFile(params string[] relativeSegments)
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            while (directory != null && !File.Exists(Path.Combine(directory, "DS4MapperTest.sln")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "Could not locate the repository root (DS4MapperTest.sln) from the test output directory.");

            string path = Path.Combine(new[] { directory }.Concat(relativeSegments).ToArray());
            Assert.IsTrue(File.Exists(path), $"Expected source file not found: {path}");
            return File.ReadAllText(path);
        }
    }
}
