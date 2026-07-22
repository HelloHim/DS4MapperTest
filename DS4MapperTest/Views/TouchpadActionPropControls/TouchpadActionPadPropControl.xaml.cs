using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels.TouchpadActionPropViewModels;

namespace DS4MapperTest.Views.TouchpadActionPropControls
{
    /// <summary>
    /// Interaction logic for TouchpadActionPadPropControl.xaml
    /// </summary>
    public partial class TouchpadActionPadPropControl : UserControl, ISectionAwareTouchpadPropControl
    {
        public class DirButtonBindingArgs : EventArgs
        {
            private ButtonAction dirBtn;
            public ButtonAction DirBtn => dirBtn;

            private bool realAction = false;
            public bool RealAction => realAction;

            public delegate void UpdateActionHandler(ButtonAction oldAction, ButtonAction newAction);
            private UpdateActionHandler updateActHandler;
            public UpdateActionHandler UpdateActHandler => updateActHandler;

            public DirButtonBindingArgs(ButtonAction dirBtn, bool realAction = false, UpdateActionHandler updateActDel = null)
            {
                this.dirBtn = dirBtn;
                this.realAction = realAction;
                this.updateActHandler = updateActDel;
            }
        }

        private TouchpadActionPadPropViewModel touchActionPropVM;
        public TouchpadActionPadPropViewModel TouchActionPropVM => touchActionPropVM;

        public event EventHandler<DirButtonBindingArgs> RequestFuncEditor;

        public TouchpadActionPadPropControl()
        {
            InitializeComponent();
        }

        public void PostInit(Mapper mapper, TouchpadMapAction action)
        {
            touchActionPropVM = new TouchpadActionPadPropViewModel(mapper, action);

            DataContext = touchActionPropVM;
        }

        public void RefreshView()
        {
            // Force re-eval of bindings
            DataContext = null;
            DataContext = touchActionPropVM;
        }

        public void ApplySection(TouchpadSettingsSection section)
        {
            ExtraFieldsPanel.Visibility = TouchpadUiFeatureFlags.ShowActionNameField && section == TouchpadSettingsSection.Extra
                ? Visibility.Visible : Visibility.Collapsed;
            ModeFieldsPanel.Visibility = section == TouchpadSettingsSection.ModeSettings
                ? Visibility.Visible : Visibility.Collapsed;
            ZoneGeometryFieldsPanel.Visibility = section == TouchpadSettingsSection.ZonesGestures
                ? Visibility.Visible : Visibility.Collapsed;
            AdvancedFieldsPanel.Visibility = section == TouchpadSettingsSection.Advanced
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnUpEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Up],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Up],
                touchActionPropVM.UpdateUpDirAction));
        }

        private void btnDownEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Down],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Down],
                touchActionPropVM.UpdateDownDirAction));
        }

        private void btnLeftEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Left],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Left],
                touchActionPropVM.UpdateLeftDirAction));
        }

        private void btnRightEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.Right],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.Right],
                touchActionPropVM.UpdateRightAction));
        }

        private void btnUpLeftEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.UpLeft],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.UpLeft],
                touchActionPropVM.UpdateUpLeftAction));
        }

        private void btnUpRightEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.UpRight],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.UpRight],
                touchActionPropVM.UpdateUpRightAction));
        }

        private void btnDownLeftEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.DownLeft],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.DownLeft],
                touchActionPropVM.UpdateDownLeftAction));
        }

        private void btnDownRightEdit_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)TouchpadActionPad.DpadDirections.DownRight],
                !touchActionPropVM.Action.UseParentActionButton[(int)TouchpadActionPad.DpadDirections.DownRight],
                touchActionPropVM.UpdateDownRightAction));
        }

        private void DirectionAdvancedEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: TouchpadDirectionBindItem item })
            {
                return;
            }

            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.EventCodes4[(int)item.Direction],
                    !touchActionPropVM.Action.UseParentActionButton[(int)item.Direction],
                    GetDirectionUpdateHandler(item.Direction)));
        }

        private DirButtonBindingArgs.UpdateActionHandler GetDirectionUpdateHandler(
            TouchpadActionPad.DpadDirections direction)
        {
            return direction switch
            {
                TouchpadActionPad.DpadDirections.Up => touchActionPropVM.UpdateUpDirAction,
                TouchpadActionPad.DpadDirections.Down => touchActionPropVM.UpdateDownDirAction,
                TouchpadActionPad.DpadDirections.Left => touchActionPropVM.UpdateLeftDirAction,
                TouchpadActionPad.DpadDirections.Right => touchActionPropVM.UpdateRightAction,
                TouchpadActionPad.DpadDirections.UpLeft => touchActionPropVM.UpdateUpLeftAction,
                TouchpadActionPad.DpadDirections.UpRight => touchActionPropVM.UpdateUpRightAction,
                TouchpadActionPad.DpadDirections.DownLeft => touchActionPropVM.UpdateDownLeftAction,
                TouchpadActionPad.DpadDirections.DownRight => touchActionPropVM.UpdateDownRightAction,
                _ => null,
            };
        }

        private void btnEditTest_Click(object sender, RoutedEventArgs e)
        {
            RequestFuncEditor?.Invoke(this,
                new DirButtonBindingArgs(touchActionPropVM.Action.RingButton,
                !touchActionPropVM.Action.UseParentRingButton,
                touchActionPropVM.UpdateRingButton));
        }
    }
}
