using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DS4MapperTest;

namespace DS4MapperUnitTests
{
    /// <summary>
    /// The profile browser gives every folder its own ListBox, so picking a profile
    /// has to clear the selection in the other folders' lists. Those lists raise
    /// SelectionChanged as they are cleared, which re-enters the same handler as if
    /// the user had deselected, and the handler used to read its shared
    /// selectedListEntry field back out afterwards. Selecting a profile in a
    /// different folder therefore tore down the panel the click had just set up and
    /// then threw, leaving no way to reach a profile that was alone in its folder.
    ///
    /// This drives the real MainWindow handler through a hidden HwndSource. The
    /// private list types are reached by reflection, so a rename in MainWindow will
    /// surface here as a failed setup assertion rather than a silent pass.
    /// </summary>
    [TestClass]
    public class ProfileListSelectionTests
    {
        [TestMethod]
        public void SelectingAProfileInAnotherFolderOpensTheProfilePanel()
        {
            SelectionProbeResult result = null;
            Exception failure = null;

            Thread staThread = new Thread(() =>
            {
                try
                {
                    result = ProbeCrossFolderSelection();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.IsBackground = true;
            staThread.Start();
            staThread.Join(TimeSpan.FromSeconds(60));

            Assert.IsNull(failure, $"Selecting across folders threw: {failure}");
            Assert.IsNotNull(result, "The selection probe did not finish.");
            Assert.AreEqual(2, result.FolderListCount, "Expected one list per folder.");
            Assert.AreEqual(Visibility.Visible, result.PanelVisibility,
                "The selected profile panel should open for a profile in any folder.");
            Assert.AreEqual("Solo", result.SelectedEntryName);
            Assert.AreEqual("Solo", result.RenameBoxText);
        }

        private sealed class SelectionProbeResult
        {
            public int FolderListCount { get; set; }
            public Visibility PanelVisibility { get; set; }
            public string SelectedEntryName { get; set; }
            public string RenameBoxText { get; set; }
        }

        private static SelectionProbeResult ProbeCrossFolderSelection()
        {
            // App.xaml merges two of its dictionaries by relative URI, which resolves
            // against Application.ResourceAssembly (the test host here), so merge the
            // same set by absolute pack URI instead of loading App.xaml.
            Application app = new Application();
            string[] dictionarySources =
            {
                "pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml",
                "pack://application:,,,/HandyControl;component/Themes/Theme.xaml",
                "pack://application:,,,/DS4MapperTest;component/Views/Styles/JsmccThemeDark.xaml",
                "pack://application:,,,/DS4MapperTest;component/Views/Styles/JsmccThemeBase.xaml",
            };
            foreach (string dictionarySource in dictionarySources)
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(dictionarySource, UriKind.Absolute),
                });
            }

            MainWindow window = new MainWindow();
            Type entryType = GetNestedType("ProfileListEntry");
            Type groupType = GetNestedType("ProfileFolderListGroup");

            object defaultEntry = CreateEntry(entryType, "Aim", "Default");
            object soloEntry = CreateEntry(entryType, "Solo", "Solo Folder");
            IList groups = CreateTypedList(groupType);
            groups.Add(CreateGroup(groupType, entryType, "Default", defaultEntry));
            groups.Add(CreateGroup(groupType, entryType, "Solo Folder", soloEntry));

            // A hidden HwndSource (no WS_VISIBLE) gives the tree a real presentation
            // source so the per-folder lists are generated, without showing a window.
            UIElement windowContent = (UIElement)window.Content;
            window.Content = null;
            HwndSource source = new HwndSource(new HwndSourceParameters("profileListSelectionTest")
            {
                Width = 1100,
                Height = 720,
                WindowStyle = 0x00800000,
            })
            {
                RootVisual = windowContent,
            };

            ((UIElement)GetField(window, "profilesOverlay")).Visibility = Visibility.Visible;
            ItemsControl profileListBox = (ItemsControl)GetField(window, "profileListBox");
            profileListBox.ItemsSource = groups;

            windowContent.Measure(new Size(1100, 720));
            windowContent.Arrange(new Rect(0, 0, 1100, 720));
            profileListBox.UpdateLayout();
            PumpDispatcher();

            List<ListBox> folderLists = new List<ListBox>();
            CollectListBoxes(profileListBox, folderLists);

            SelectionProbeResult result = new SelectionProbeResult { FolderListCount = folderLists.Count };
            if (folderLists.Count < 2)
            {
                source.Dispose();
                return result;
            }

            // Pick a profile in the first folder, then one in a different folder.
            folderLists[0].SelectedItem = defaultEntry;
            PumpDispatcher();
            folderLists[1].SelectedItem = soloEntry;
            PumpDispatcher();

            object selectedEntry = GetField(window, "selectedListEntry");
            result.PanelVisibility = ((UIElement)GetField(window, "selectedProfilePanel")).Visibility;
            result.SelectedEntryName = selectedEntry == null
                ? null
                : (string)entryType.GetProperty("Name").GetValue(selectedEntry);
            result.RenameBoxText = ((TextBox)GetField(window, "profileRenameBox")).Text;

            source.Dispose();
            return result;
        }

        private static Type GetNestedType(string name)
        {
            Type nestedType = typeof(MainWindow).GetNestedType(name, BindingFlags.NonPublic);
            Assert.IsNotNull(nestedType, $"MainWindow.{name} was not found; update this test.");
            return nestedType;
        }

        private static object CreateEntry(Type entryType, string name, string folderName)
        {
            ProfileEntity entity = new ProfileEntity($"{folderName}\\{name}.json", name,
                InputDeviceType.DS4, folderName);
            return Activator.CreateInstance(entryType,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new object[] { entity, false }, null);
        }

        private static object CreateGroup(Type groupType, Type entryType, string folderName, object entry)
        {
            object group = Activator.CreateInstance(groupType,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, null, null);
            groupType.GetProperty("FolderName").SetValue(group, folderName);
            groupType.GetProperty("IsExpanded").SetValue(group, true);

            IList entries = CreateTypedList(entryType);
            entries.Add(entry);
            groupType.GetProperty("Profiles").SetValue(group, entries);
            return group;
        }

        private static IList CreateTypedList(Type itemType)
        {
            return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, $"MainWindow.{name} was not found; update this test.");
            return field.GetValue(target);
        }

        private static void CollectListBoxes(DependencyObject root, List<ListBox> found)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, childIndex);
                if (child is ListBox listBox)
                {
                    found.Add(listBox);
                }

                CollectListBoxes(child, found);
            }
        }

        private static void PumpDispatcher()
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
    }
}
