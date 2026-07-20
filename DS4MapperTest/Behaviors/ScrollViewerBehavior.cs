using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace DS4MapperTest.Behaviors
{
    /// <summary>
    /// Forwards mouse wheel input from a ScrollViewer to its nearest scrollable
    /// ancestor once the ScrollViewer itself has nothing left to scroll, instead
    /// of letting WPF's default handling swallow the event at the first
    /// ScrollViewer it hits.
    /// </summary>
    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty BubbleWheelToParentProperty =
            DependencyProperty.RegisterAttached(
                "BubbleWheelToParent",
                typeof(bool),
                typeof(ScrollViewerBehavior),
                new PropertyMetadata(false, OnBubbleWheelToParentChanged));

        public static bool GetBubbleWheelToParent(DependencyObject obj) =>
            (bool)obj.GetValue(BubbleWheelToParentProperty);

        public static void SetBubbleWheelToParent(DependencyObject obj, bool value) =>
            obj.SetValue(BubbleWheelToParentProperty, value);

        public static readonly DependencyProperty ScrollComboBoxDropDownOnWheelProperty =
            DependencyProperty.RegisterAttached(
                "ScrollComboBoxDropDownOnWheel",
                typeof(bool),
                typeof(ScrollViewerBehavior),
                new PropertyMetadata(false, OnScrollComboBoxDropDownOnWheelChanged));

        public static bool GetScrollComboBoxDropDownOnWheel(DependencyObject obj) =>
            (bool)obj.GetValue(ScrollComboBoxDropDownOnWheelProperty);

        public static void SetScrollComboBoxDropDownOnWheel(DependencyObject obj, bool value) =>
            obj.SetValue(ScrollComboBoxDropDownOnWheelProperty, value);

        private static void OnBubbleWheelToParentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer scrollViewer)
            {
                return;
            }

            scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
            if ((bool)e.NewValue)
            {
                scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
            }
        }

        private static void OnScrollComboBoxDropDownOnWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox comboBox)
            {
                return;
            }

            comboBox.DropDownOpened -= ComboBox_DropDownOpened;
            comboBox.DropDownClosed -= ComboBox_DropDownClosed;
            if ((bool)e.NewValue)
            {
                comboBox.DropDownOpened += ComboBox_DropDownOpened;
                comboBox.DropDownClosed += ComboBox_DropDownClosed;
            }
        }

        private static void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is not ComboBox comboBox)
            {
                return;
            }

            comboBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                Popup popup = FindVisualChild<Popup>(comboBox);
                if (popup?.Child is UIElement popupChild)
                {
                    popupChild.PreviewMouseWheel -= ComboBoxDropDown_PreviewMouseWheel;
                    popupChild.PreviewMouseWheel += ComboBoxDropDown_PreviewMouseWheel;
                }
            }), DispatcherPriority.Loaded);
        }

        private static void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (sender is not ComboBox comboBox)
            {
                return;
            }

            Popup popup = FindVisualChild<Popup>(comboBox);
            if (popup?.Child is UIElement popupChild)
            {
                popupChild.PreviewMouseWheel -= ComboBoxDropDown_PreviewMouseWheel;
            }
        }

        private static void ComboBoxDropDown_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            ScrollViewer scrollViewer = FindVisualAncestor<ScrollViewer>(e.OriginalSource as DependencyObject)
                ?? FindVisualChild<ScrollViewer>(sender as DependencyObject);
            if (scrollViewer == null || scrollViewer.ScrollableHeight <= 0)
            {
                return;
            }

            if (e.Delta > 0)
            {
                scrollViewer.LineUp();
            }
            else
            {
                scrollViewer.LineDown();
            }

            e.Handled = true;
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            bool scrollingDown = e.Delta < 0;
            bool canScrollFurther = scrollingDown
                ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
                : scrollViewer.VerticalOffset > 0;

            if (canScrollFurther)
            {
                // This ScrollViewer still has room to move, let its normal
                // (bubbling) MouseWheel handling scroll it.
                return;
            }

            // Nothing left to scroll here - forward the wheel input up to the
            // next scrollable ancestor instead of letting it dead-end.
            e.Handled = true;

            if (VisualTreeHelper.GetParent(scrollViewer) is UIElement parent)
            {
                var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent
                };
                parent.RaiseEvent(forwarded);
            }
        }

        private static ScrollViewer FindScrollViewer(DependencyObject start)
        {
            for (DependencyObject current = start; current != null; current = GetParent(current))
            {
                if (current is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
            }

            return null;
        }

        private static T FindVisualAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            for (DependencyObject current = start; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is T match)
                {
                    return match;
                }
            }

            return null;
        }

        private static DependencyObject GetParent(DependencyObject current)
        {
            if (current is FrameworkElement element && element.Parent != null)
            {
                return element.Parent;
            }

            if (current is FrameworkContentElement contentElement && contentElement.Parent != null)
            {
                return contentElement.Parent;
            }

            return VisualTreeHelper.GetParent(current);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                T descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }
}
