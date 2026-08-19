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

        // Opt-in per TabControl rather than set on the shared pill tab style:
        // switching a page's top-level section is a navigation change and should
        // start at the top, but the same style is reused for tab strips nested
        // inside a page (eg. the cog button's advanced binding editor), where
        // scrolling the page away from what the user just clicked is never
        // wanted.
        public static readonly DependencyProperty ResetScrollOnSelectionProperty =
            DependencyProperty.RegisterAttached(
                "ResetScrollOnSelection",
                typeof(bool),
                typeof(ScrollViewerBehavior),
                new PropertyMetadata(false, OnResetScrollOnSelectionChanged));

        public static bool GetResetScrollOnSelection(DependencyObject obj) =>
            (bool)obj.GetValue(ResetScrollOnSelectionProperty);

        public static void SetResetScrollOnSelection(DependencyObject obj, bool value) =>
            obj.SetValue(ResetScrollOnSelectionProperty, value);

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

        private static void OnResetScrollOnSelectionChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not TabControl tabControl) return;

            tabControl.SelectionChanged -= TabControl_SelectionChanged;
            if ((bool)e.NewValue)
            {
                tabControl.SelectionChanged += TabControl_SelectionChanged;
            }
        }

        private static void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not TabControl tabControl || e.Source != tabControl ||
                tabControl.SelectedItem is not TabItem selectedTab)
            {
                return;
            }

            if (e.RemovedItems.Count == 0)
            {
                // Nothing was previously selected, so this is the TabControl
                // picking its own default tab as it first loads (eg. a brand
                // new inline binding editor appearing under the cog button),
                // not the user actually switching tabs - don't yank the page
                // back to the top for that.
                return;
            }

            tabControl.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Reset only the nearest enclosing ScrollViewer, not every
                // ancestor - a TabControl nested inside a settings panel
                // (eg. the cog button's advanced binding editor) can sit
                // inside more than one ScrollViewer, and resetting outer
                // ones too would yank the whole page back to the top for
                // what the user sees as a local tab switch.
                ScrollViewer nearest = FindScrollViewer(GetParent(tabControl));
                nearest?.ScrollToTop();

                ResetDescendantScrollViewers(selectedTab);
            }), DispatcherPriority.Loaded);
        }

        private static void ResetDescendantScrollViewers(DependencyObject parent)
        {
            if (parent == null) return;
            if (parent is ScrollViewer scrollViewer) scrollViewer.ScrollToTop();

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                ResetDescendantScrollViewers(VisualTreeHelper.GetChild(parent, i));
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

            // PreviewMouseWheel tunnels from the outermost ScrollViewer down to
            // the innermost one under the cursor. If a more deeply nested
            // ScrollViewer sits between this one and the actual event source,
            // let that inner ScrollViewer decide first - otherwise the outer
            // ScrollViewer's own boundary state (eg. already at the top) would
            // swallow the wheel input before the inner one ever sees it.
            ScrollViewer nearest = FindVisualAncestor<ScrollViewer>(e.OriginalSource as DependencyObject);
            if (nearest != null && nearest != scrollViewer)
            {
                return;
            }

            bool scrollingDown = e.Delta < 0;
            if (CanScroll(scrollViewer, scrollingDown))
            {
                // This ScrollViewer still has room to move, let its normal
                // (bubbling) MouseWheel handling scroll it.
                return;
            }

            // Nothing left to scroll here. Walk up through any further
            // nested ScrollViewers (eg. a settings panel with its own
            // ScrollViewer embedded inside another scrollable panel) and
            // hand the wheel input to the first ancestor that still has
            // room, by re-raising it directly on that ancestor so its own
            // default MouseWheel handling scrolls it at the normal
            // multi-line wheel speed. A single bubbling event raised on
            // just the immediate parent only works for one level - the
            // very next ScrollViewer up the chain swallows the event
            // unconditionally even when it has nothing left to scroll
            // either, so anything nested three or more ScrollViewers deep
            // would dead-end before reaching the outermost page scroller.
            e.Handled = true;
            for (DependencyObject current = GetParent(scrollViewer); current != null; current = GetParent(current))
            {
                if (current is not ScrollViewer ancestor || !CanScroll(ancestor, scrollingDown))
                {
                    continue;
                }

                var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent
                };
                ancestor.RaiseEvent(forwarded);
                return;
            }
        }

        private static bool CanScroll(ScrollViewer scrollViewer, bool scrollingDown) =>
            scrollingDown
                ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
                : scrollViewer.VerticalOffset > 0;

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
            for (DependencyObject current = start; current != null; current = GetVisualParent(current))
            {
                if (current is T match)
                {
                    return match;
                }
            }

            return null;
        }

        // A wheel event's OriginalSource is often a content element rather than a
        // visual - a Run inside a TextBlock, for instance. VisualTreeHelper throws
        // outright on those, which took the whole wheel handler down, so step out
        // of the content tree first and only then walk visuals.
        private static DependencyObject GetVisualParent(DependencyObject current)
        {
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
            {
                return VisualTreeHelper.GetParent(current);
            }

            if (current is FrameworkContentElement contentElement)
            {
                return (DependencyObject)contentElement.Parent ??
                    LogicalTreeHelper.GetParent(contentElement);
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

            return GetVisualParent(current);
        }

        internal static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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
