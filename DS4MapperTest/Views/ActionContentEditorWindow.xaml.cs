using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.ViewModels;
using System;
using System.Linq;
using System.Windows;

namespace DS4MapperTest.Views
{
    // Hosts the existing legacy action-content editing controls (the same
    // Gyro/Keybinds/Touchpad/Sticks panels MainWindow used to show) against a
    // UniversalActionContentEditorSession's offline Mapper. The session's
    // offline mapper is never Started, so it has no BaseReader and no
    // background mapping thread - switching to the target set/layer below is
    // done directly against the legacy Profile/ActionSet model rather than
    // through ProfileEditorTestViewModel.SwitchActionSets/SwitchActionLayer,
    // which route through Mapper.ProcessMappingChangeAction and would throw
    // on a null BaseReader.
    public partial class ActionContentEditorWindow : Window
    {
        private readonly UniversalActionContentEditorSession session;
        private readonly ProfileEditorTestViewModel editorVM;

        public ActionContentEditorWindow(UniversalActionContentEditorSession session, string categoryHint = null)
        {
            InitializeComponent();

            this.session = session ?? throw new ArgumentNullException(nameof(session));

            Profile actionProfile = session.Mapper.ActionProfile;
            ActionSet targetSet = actionProfile.ActionSets.First(set => set.Index == session.ActionSetIndex);
            int setPosition = actionProfile.ActionSets.IndexOf(targetSet);
            actionProfile.SwitchSets(setPosition, session.Mapper);
            actionProfile.CurrentActionSet.RecompileCompositeLayer(session.Mapper);

            ActionLayer targetLayer = targetSet.ActionLayers.First(layer => layer.Index == session.ActionLayerIndex);
            int layerPosition = targetSet.ActionLayers.IndexOf(targetLayer);
            actionProfile.CurrentActionSet.SwitchActionLayer(session.Mapper, layerPosition);

            ProfileEntity stubEntity = new ProfileEntity(string.Empty, "Action Content Editor", InputDeviceType.None);
            editorVM = new ProfileEditorTestViewModel(session.Mapper, stubEntity, actionProfile);
            editorVM.Test();
            DataContext = editorVM;

            headerSubtitleText.Text = $"Editing \"{targetSet.Name}\" / \"{targetLayer.Name}\".";
            SelectInitialTab(categoryHint);
        }

        private void SelectInitialTab(string categoryHint)
        {
            if (string.IsNullOrEmpty(categoryHint)) return;

            object targetTab = categoryHint switch
            {
                nameof(UniversalInputCategory.MotionSensor) => gyroTab,
                nameof(UniversalInputCategory.Stick) => sticksTab,
                nameof(UniversalInputCategory.StickClick) => sticksTab,
                nameof(UniversalInputCategory.StickTouch) => sticksTab,
                nameof(UniversalInputCategory.TouchSurface) => touchpadTab,
                nameof(UniversalInputCategory.TouchSurfaceClick) => touchpadTab,
                _ => keybindsTab,
            };

            contentTabControl.SelectedItem = targetTab;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
