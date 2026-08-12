using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace DS4MapperUnitTests
{
    // Step 9 restores the classic MainWindow editor surface and removes the
    // temporary Step 8 Universal Profile Editor preview entry point. This is a
    // structural source-text check rather than a live WPF test, since
    // instantiating a Window in this test host is not practical.
    [TestClass]
    public class MainWindowLegacySurfaceTests
    {
        [TestMethod]
        public void ClassicEditorRegionsAreReachableAndPreviewEntryIsGone()
        {
            string xaml = ReadSourceFile("DS4MapperTest", "MainWindow.xaml");

            Assert.IsFalse(xaml.Contains("x:Name=\"primaryEditorBanner\""),
                "The temporary Step 8 preview banner must be removed.");
            Assert.IsFalse(xaml.Contains("Universal Profiles (Preview)"),
                "The temporary Step 8 launch button must be removed.");

            AssertElementOpeningTagDoesNotContain(xaml, "actionContextRow", "Visibility=\"Collapsed\"");
            AssertElementOpeningTagDoesNotContain(xaml, "actionContextRow", "IsEnabled=\"False\"");
            AssertElementOpeningTagDoesNotContain(xaml, "mainContentScrollViewer", "Visibility=\"Collapsed\"");
            StringAssert.Contains(xaml, "x:Name=\"deviceComboBox\"");
            StringAssert.Contains(xaml, "DisplayMemberPath=\"DisplayNameWithBattery\"");
            StringAssert.Contains(xaml, "x:Name=\"profileComboBox\"");
            StringAssert.Contains(xaml, "x:Name=\"nintendoFaceSwapCheckBox\"");
        }

        [TestMethod]
        public void ExtraKeybindsTabShowsOnlyMiscBindings()
        {
            string xaml = ReadSourceFile("DS4MapperTest", "Views", "ExtraKeybindsControl.xaml");

            StringAssert.Contains(xaml, "ItemsSource=\"{Binding ExtraButtonBindings}\"");
            StringAssert.Contains(xaml,
                "Extra controller buttons. Each MISC slot corresponds to a different physical button depending on your controller.");
            Assert.IsFalse(xaml.Contains("ItemsSource=\"{Binding TouchpadButtonBindings}\""),
                "Touchpad press/touch bindings belong in the Touchpad tab, not the Extra keybinds tab.");
        }

        [TestMethod]
        public void TouchpadCentreSettingsTabStaysSelectableWhenUnavailable()
        {
            string codeBehind = ReadSourceFile("DS4MapperTest", "Views", "TouchpadControl.xaml.cs");

            StringAssert.Contains(codeBehind, "CreateUnavailableCenterTouchpadContent");
            StringAssert.Contains(codeBehind, "tab.Opacity = 0.48");
            Assert.IsFalse(codeBehind.Contains("tab.IsEnabled = false"),
                "The centre touchpad tab should look unavailable but remain selectable.");
        }

        [TestMethod]
        public void StickTouchTabUsesCapacitiveTouchAvailability()
        {
            string xaml = ReadSourceFile("DS4MapperTest", "Views", "StickSideTouchBindingsControl.xaml");

            StringAssert.Contains(xaml, "HasTouchBindings");
            StringAssert.Contains(xaml, "This stick does not report a capacitive touch sensor.");
        }

        [TestMethod]
        public void MainWindowCodeBehindCanShowTheClassicNavRail()
        {
            string codeBehind = ReadSourceFile("DS4MapperTest", "MainWindow.xaml.cs");

            int methodStart = codeBehind.IndexOf("private void SetNavCompactMode", StringComparison.Ordinal);
            Assert.IsTrue(methodStart >= 0, "SetNavCompactMode should still exist (handles hamburger/compact layout).");

            int methodEnd = codeBehind.IndexOf("\n        }", methodStart, StringComparison.Ordinal);
            Assert.IsTrue(methodEnd > methodStart);
            string methodBody = codeBehind.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains(methodBody, "navSidebarBorder.Visibility = Visibility.Visible",
                "The classic nav rail must be reachable again in the non-compact layout.");
        }

        private static void AssertElementOpeningTagDoesNotContain(string xaml, string elementName, string prohibited)
        {
            int nameIndex = xaml.IndexOf($"x:Name=\"{elementName}\"", StringComparison.Ordinal);
            Assert.IsTrue(nameIndex >= 0, $"Expected to find x:Name=\"{elementName}\" in MainWindow.xaml.");

            // The element's opening tag runs from its own '<' back-search to the
            // next '>' after the name attribute; Visibility="Collapsed" must
            // appear somewhere within that same opening tag.
            int tagEnd = xaml.IndexOf('>', nameIndex);
            Assert.IsTrue(tagEnd > nameIndex);
            int tagStart = xaml.LastIndexOf('<', nameIndex);
            Assert.IsTrue(tagStart >= 0 && tagStart < nameIndex);

            string openingTag = xaml.Substring(tagStart, tagEnd - tagStart);
            Assert.IsFalse(openingTag.Contains(prohibited),
                $"{elementName} must not contain {prohibited} in its opening tag.");
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
