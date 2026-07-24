using System;
using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    /// <summary>
    /// Interaction logic for ReleaseFuncPropControl.xaml
    /// </summary>
    public partial class ReleaseFuncPropControl : UserControl
    {
        private ReleaseFuncPropViewModel releaseFuncVM;
        public ReleaseFuncPropViewModel ReleaseFuncVM => releaseFuncVM;

        public event EventHandler RequestBindingEditor;
        public event EventHandler<int> RequestChangeFuncType;

        public ReleaseFuncPropControl()
        {
            InitializeComponent();
        }

        public void PostInit(Mapper mapper, ButtonAction action, ReleaseFunc func)
        {
            releaseFuncVM = new ReleaseFuncPropViewModel(mapper, action, func);
            DataContext = releaseFuncVM;

            funcTypeControl.PostInit(func);
            funcTypeControl.FuncTypeSelectVM.SelectedIndexChanged += FuncTypeSelectVM_SelectedIndexChanged;
        }

        private void FuncTypeSelectVM_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selectedIndex = funcTypeControl.FuncTypeSelectVM.SelectedIndex;
            RequestChangeFuncType?.Invoke(this, selectedIndex);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            RequestBindingEditor?.Invoke(this, EventArgs.Empty);
        }
    }
}
