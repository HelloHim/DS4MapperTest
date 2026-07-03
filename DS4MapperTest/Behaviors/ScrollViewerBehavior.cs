using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
    }
}
