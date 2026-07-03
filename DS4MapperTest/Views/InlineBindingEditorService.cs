using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    internal static class InlineBindingEditorService
    {
        private static ContentControl activeHost;
        private static InlineOutputBindingEditorControl activeEditor;

        public static void Open(ContentControl host, EditFaceBindingContext context,
            string title, Action refresh)
        {
            if (host == null || context == null) return;

            if (activeHost == host)
            {
                CloseActive(refresh, cancel: true);
                return;
            }

            CloseActive(null, cancel: true);

            InlineOutputBindingEditorControl editor = new InlineOutputBindingEditorControl();
            editor.PostInit(context.Mapper, context.Action, context.Func, title);
            editor.Applied += (_, _) => CloseHost(host, refresh);
            editor.Cancelled += (_, _) => CloseHost(host, refresh);

            host.Content = editor;
            host.Visibility = Visibility.Visible;
            activeHost = host;
            activeEditor = editor;
        }

        public static void CloseAny()
        {
            CloseActive(null, cancel: true);
        }

        public static ContentControl FindInlineHost(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (current is Border border)
                {
                    ContentControl host = FindVisualChild<ContentControl>(border, "InlineEditorHost");
                    if (host != null) return host;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static void CloseActive(Action refresh, bool cancel)
        {
            if (activeEditor != null && cancel)
            {
                InlineOutputBindingEditorControl editor = activeEditor;
                activeEditor = null;
                editor.CancelEdit();
            }
            else if (activeHost != null)
            {
                CloseHost(activeHost, refresh);
            }
        }

        private static void CloseHost(ContentControl host, Action refresh)
        {
            if (host != null)
            {
                host.Content = null;
                host.Visibility = Visibility.Collapsed;
            }

            if (activeHost == host)
            {
                activeHost = null;
                activeEditor = null;
            }

            refresh?.Invoke();
        }

        private static T FindVisualChild<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }

                T result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }

            return null;
        }
    }
}
