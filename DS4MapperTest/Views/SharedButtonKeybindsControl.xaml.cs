using System.Collections;
using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    public partial class SharedButtonKeybindsControl : UserControl
    {
        public static readonly DependencyProperty SectionTitleProperty =
            DependencyProperty.Register(nameof(SectionTitle), typeof(string),
                typeof(SharedButtonKeybindsControl), new PropertyMetadata(""));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string),
                typeof(SharedButtonKeybindsControl), new PropertyMetadata(""));

        public static readonly DependencyProperty EmptyTextProperty =
            DependencyProperty.Register(nameof(EmptyText), typeof(string),
                typeof(SharedButtonKeybindsControl), new PropertyMetadata(""));

        public static readonly DependencyProperty CardSubtitleProperty =
            DependencyProperty.Register(nameof(CardSubtitle), typeof(string),
                typeof(SharedButtonKeybindsControl), new PropertyMetadata(""));

        public static readonly DependencyProperty UseClickTerminologyProperty =
            DependencyProperty.Register(nameof(UseClickTerminology), typeof(bool),
                typeof(SharedButtonKeybindsControl), new PropertyMetadata(false));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable),
                typeof(SharedButtonKeybindsControl), new PropertyMetadata(null));

        public string SectionTitle
        {
            get => (string)GetValue(SectionTitleProperty);
            set => SetValue(SectionTitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public string EmptyText
        {
            get => (string)GetValue(EmptyTextProperty);
            set => SetValue(EmptyTextProperty, value);
        }

        public string CardSubtitle
        {
            get => (string)GetValue(CardSubtitleProperty);
            set => SetValue(CardSubtitleProperty, value);
        }

        public bool UseClickTerminology
        {
            get => (bool)GetValue(UseClickTerminologyProperty);
            set => SetValue(UseClickTerminologyProperty, value);
        }

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public SharedButtonKeybindsControl()
        {
            InitializeComponent();
        }

        private void AddExtraBindingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void AddExtraBindingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem ||
                menuItem.Tag is not string tag ||
                menuItem.Parent is not ContextMenu menu ||
                menu.PlacementTarget is not FrameworkElement target ||
                target.DataContext is not FaceButtonBindingItem buttonItem)
            {
                return;
            }

            FaceBindingFuncKind? kind = tag switch
            {
                "Hold" => FaceBindingFuncKind.Hold,
                "Double" => FaceBindingFuncKind.Double,
                "Distance" => FaceBindingFuncKind.Distance,
                "Chorded" => FaceBindingFuncKind.Chorded,
                "SimPress" => FaceBindingFuncKind.SimPress,
                "Start" => FaceBindingFuncKind.Start,
                "Release" => FaceBindingFuncKind.Release,
                _ => null,
            };

            if (kind == null) return;

            buttonItem.AddExtraBinding(kind.Value);
        }

        private void RemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FaceButtonFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }

    }
}
