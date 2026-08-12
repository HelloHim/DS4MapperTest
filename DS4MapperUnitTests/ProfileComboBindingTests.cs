using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Threading;
using DS4MapperTest;
using DS4MapperTest.Converters;

namespace DS4MapperUnitTests
{
    /// <summary>
    /// The profile ComboBox in MainWindow lists ProfileEntity objects and has its
    /// contents replaced whenever a profile is loaded, deleted, renamed or moved.
    /// Binding its item template to a property path (<c>{Binding Name}</c>) made
    /// WPF cache a reflection accessor for ProfileEntity.Name and then invoke it
    /// against the internal sentinel object it substitutes while the selection is
    /// being torn down, throwing "Object type DS4MapperTest.ProfileEntity does not
    /// match target type MS.Internal.NamedObject". That exception aborted the rest
    /// of whatever profile operation was running -- a deleted profile stayed on
    /// screen until a second delete attempt, and Reset Default Profiles reported a
    /// failure -- so the template goes through ProfileEntityNameConverter with no
    /// property path instead.
    /// </summary>
    [TestClass]
    public class ProfileComboBindingTests
    {
        private static ProfileEntity CreateProfile(string name, string folderName)
        {
            return new ProfileEntity($"{folderName}\\{name}.json", name, InputDeviceType.DS4, folderName);
        }

        [TestMethod]
        public void ConverterRendersTheProfileName()
        {
            ProfileEntityNameConverter converter = new ProfileEntityNameConverter();

            object converted = converter.Convert(CreateProfile("Aim", "Default"), typeof(string), null,
                CultureInfo.InvariantCulture);

            Assert.AreEqual("Aim", converted);
        }

        [TestMethod]
        public void ConverterRendersNonProfileValuesAsEmptyText()
        {
            ProfileEntityNameConverter converter = new ProfileEntityNameConverter();

            // DependencyProperty.UnsetValue is one of the MS.Internal.NamedObject
            // sentinels a ComboBox hands its selection box mid-refresh; reading a
            // ProfileEntity property off it is what used to throw.
            Assert.AreEqual(string.Empty, converter.Convert(DependencyProperty.UnsetValue, typeof(string), null,
                CultureInfo.InvariantCulture));
            Assert.AreEqual(string.Empty, converter.Convert(null, typeof(string), null,
                CultureInfo.InvariantCulture));
            Assert.AreEqual(string.Empty, converter.Convert("not a profile", typeof(string), null,
                CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void RepopulatingTheProfileComboDoesNotThrow()
        {
            Exception failure = RunOnStaThread(() =>
            {
                ObservableCollection<ProfileEntity> comboProfiles = new ObservableCollection<ProfileEntity>();
                ComboBox combo = BuildProfileCombo(comboProfiles);

                string[] folders = { "Default", "VALORANT", "Default" };
                for (int round = 0; round < 8; round++)
                {
                    List<ProfileEntity> updated = Enumerable.Range(0, 2 + (round % 3))
                        .Select(index => CreateProfile($"Profile {index}", folders[round % folders.Length]))
                        .ToList();

                    // What MainWindow.UpdateProfileComboItems does when the listed
                    // profiles actually changed.
                    combo.SelectedItem = null;
                    comboProfiles.Clear();
                    foreach (ProfileEntity profile in updated)
                    {
                        comboProfiles.Add(profile);
                    }

                    combo.SelectedItem = updated[0];
                    PumpDispatcher();

                    // A rename mutates an entity that is already in the collection.
                    updated[0].Name = $"Renamed {round}";
                    combo.UpdateLayout();
                    PumpDispatcher();
                }
            });

            Assert.IsNull(failure, $"Refreshing the profile combo threw: {failure}");
        }

        private static ComboBox BuildProfileCombo(ObservableCollection<ProfileEntity> comboProfiles)
        {
            FrameworkElementFactory itemText = new FrameworkElementFactory(typeof(TextBlock));
            itemText.SetBinding(TextBlock.TextProperty, new Binding
            {
                Converter = new ProfileEntityNameConverter(),
            });

            ComboBox combo = new ComboBox
            {
                Width = 180,
                IsSynchronizedWithCurrentItem = false,
                ItemTemplate = new DataTemplate { VisualTree = itemText },
                ItemsSource = comboProfiles,
            };

            // The ComboBox only activates its selection box's bindings once it has a
            // PresentationSource. A hidden HwndSource provides one without putting
            // anything on screen (no WS_VISIBLE).
            const int WS_BORDER = 0x00800000;
            HwndSource source = new HwndSource(new HwndSourceParameters("profileComboTest")
            {
                Width = 200,
                Height = 40,
                WindowStyle = WS_BORDER,
            });
            source.RootVisual = combo;

            combo.Measure(new Size(200, 40));
            combo.Arrange(new Rect(0, 0, 200, 40));
            combo.UpdateLayout();
            return combo;
        }

        private static void PumpDispatcher()
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }

        private static Exception RunOnStaThread(Action action)
        {
            Exception failure = null;
            Thread staThread = new Thread(() =>
            {
                try
                {
                    action();
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
            staThread.Join(TimeSpan.FromSeconds(30));
            return failure;
        }
    }
}
