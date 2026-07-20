using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.ViewModels;

namespace DS4MapperTest.Views
{
    /// <summary>
    /// Reusable click-to-listen binding row control. Clicking the binding value
    /// starts capture of the next keyboard key or mouse button/wheel input and
    /// immediately replaces or creates the underlying ActionFunc slot using the
    /// same action model as the existing advanced editor (QuickBindActionApplier).
    /// Clear and Advanced remain as separate, always-visible controls.
    /// </summary>
    public partial class QuickBindControl : UserControl
    {
        private enum PendingKind
        {
            None,
            Keyboard,
            MouseButton,
            MouseWheel,
            Clear,
        }

        private bool listening;
        private PendingKind pendingKind;
        private VirtualKeys pendingKey;
        private string pendingAlias;
        private int pendingMouseCode;
        private MouseWheelCodes pendingWheelCode;
        private Window subscribedWindow;

        public QuickBindControl()
        {
            InitializeComponent();
            Loaded += QuickBindControl_Loaded;
            Unloaded += QuickBindControl_Unloaded;
            LostMouseCapture += QuickBindControl_LostMouseCapture;
            LostKeyboardFocus += QuickBindControl_LostKeyboardFocus;
        }

        private IQuickBindTarget Target => DataContext as IQuickBindTarget;

        private void QuickBindControl_Loaded(object sender, RoutedEventArgs e)
        {
            subscribedWindow = Window.GetWindow(this);
            if (subscribedWindow != null)
            {
                subscribedWindow.Deactivated += Window_Deactivated;
            }
        }

        private void QuickBindControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (subscribedWindow != null)
            {
                subscribedWindow.Deactivated -= Window_Deactivated;
                subscribedWindow = null;
            }

            if (listening)
            {
                CancelCapture();
            }

            ConfirmPopup.IsOpen = false;
            QuickBindCaptureService.NotifyEnded(this);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (listening) CancelCapture();
            ConfirmPopup.IsOpen = false;
        }

        private void QuickBindControl_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (listening) CancelCapture();
        }

        private void QuickBindControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (listening) CancelCapture();
        }

        // Called by QuickBindCaptureService when another row starts capturing.
        internal void CancelCapture()
        {
            if (!listening) return;
            EndListening();
        }

        private void BindButton_Click(object sender, RoutedEventArgs e)
        {
            if (Target == null) return;

            if (listening)
            {
                EndListening();
                return;
            }

            if (!QuickBindCaptureService.RequestCapture(this)) return;

            StartListening();
        }

        private void StartListening()
        {
            ConfirmPopup.IsOpen = false;
            listening = true;

            BindButton.Content = "Press a key…";
            BindButton.ToolTip = $"Bind {Target.RowLabel} — {Target.SlotLabel}\nPress a key or mouse button. Esc to cancel";
            BindButton.SetResourceReference(Control.BorderBrushProperty, "JsmccAccentBrush");
            BindButton.BorderThickness = new Thickness(2);

            ClearButton.IsEnabled = false;
            AdvancedButton.IsEnabled = false;

            Keyboard.Focus(BindButton);
        }

        private void EndListening()
        {
            listening = false;

            if (Mouse.Captured == this)
            {
                Mouse.Capture(null);
            }

            BindButton.SetBinding(ContentControl.ContentProperty, new Binding(nameof(IQuickBindTarget.DisplayBind)));
            BindButton.ClearValue(Control.BorderBrushProperty);
            BindButton.ClearValue(Control.BorderThicknessProperty);
            BindButton.ToolTip = "Click, then press a key or mouse button";

            ClearButton.IsEnabled = true;
            AdvancedButton.IsEnabled = true;

            QuickBindCaptureService.NotifyEnded(this);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!listening)
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            e.Handled = true;
            if (e.IsRepeat) return;

            Key effectiveKey = e.Key == Key.System ? e.SystemKey : e.Key;

            if (effectiveKey == Key.Escape)
            {
                EndListening();
                return;
            }

            if (QuickBindKeyMapper.IsReservedClearKey(effectiveKey))
            {
                RequestClear();
                return;
            }

            if (QuickBindKeyMapper.TryMapKey(effectiveKey, out VirtualKeys code, out string alias))
            {
                CommitCapture(PendingKind.Keyboard, code, alias, 0, MouseWheelCodes.None);
            }
            else
            {
                EndListening();
                ShowMessage("This input cannot be used as a binding.");
            }
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            if (!listening)
            {
                base.OnPreviewMouseDown(e);
                return;
            }

            e.Handled = true;

            if (QuickBindKeyMapper.TryMapMouseButton(e.ChangedButton, out int code, out string alias))
            {
                CommitCapture(PendingKind.MouseButton, default, alias, code, MouseWheelCodes.None);
            }
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (!listening)
            {
                base.OnPreviewMouseWheel(e);
                return;
            }

            e.Handled = true;
            if (e.Delta == 0) return;

            (MouseWheelCodes wheelCode, string alias) = QuickBindKeyMapper.MapWheelDelta(e.Delta);
            CommitCapture(PendingKind.MouseWheel, default, alias, 0, wheelCode);
        }

        private void CommitCapture(PendingKind kind, VirtualKeys key, string alias, int mouseCode, MouseWheelCodes wheelCode)
        {
            IQuickBindTarget target = Target;
            if (target == null)
            {
                EndListening();
                return;
            }

            bool complex = target.IsComplexBinding;
            EndListening();

            if (!complex)
            {
                ApplyPending(kind, key, alias, mouseCode, wheelCode);
                return;
            }

            pendingKind = kind;
            pendingKey = key;
            pendingAlias = alias;
            pendingMouseCode = mouseCode;
            pendingWheelCode = wheelCode;

            ShowConfirm(
                $"This binding contains an advanced action.\n\nReplace it with {DescribePending(kind, key, mouseCode, wheelCode)}?",
                "Replace");
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            RequestClear();
        }

        private void RequestClear()
        {
            IQuickBindTarget target = Target;
            if (target == null) return;

            if (listening) EndListening();

            if (!target.IsComplexBinding)
            {
                ApplyPending(PendingKind.Clear, default, null, 0, MouseWheelCodes.None);
                return;
            }

            pendingKind = PendingKind.Clear;
            ShowConfirm("This binding contains an advanced action.\n\nClear this binding?", "Clear");
        }

        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            if (listening) EndListening();
            OpenAdvancedEditor();
        }

        private void OpenAdvancedEditor()
        {
            ConfirmPopup.IsOpen = false;
            IQuickBindTarget target = Target;
            if (target == null) return;

            EditFaceBindingContext context = target.GetEditContext();
            if (context == null) return;

            ContentControl host = InlineBindingEditorService.FindInlineHost(this);
            InlineBindingEditorService.Open(host, context,
                $"{target.RowLabel} - {target.SlotLabel}",
                target.NotifyBindingChanged);
        }

        private void ApplyPending(PendingKind kind, VirtualKeys key, string alias, int mouseCode, MouseWheelCodes wheelCode)
        {
            IQuickBindTarget target = Target;
            if (target == null) return;

            EditFaceBindingContext context = target.GetEditContext();
            if (context == null) return;

            switch (kind)
            {
                case PendingKind.Keyboard:
                    QuickBindActionApplier.ApplyKeyboard(context, key, alias);
                    break;
                case PendingKind.MouseButton:
                    QuickBindActionApplier.ApplyMouseButton(context, mouseCode, alias);
                    break;
                case PendingKind.MouseWheel:
                    QuickBindActionApplier.ApplyMouseWheel(context, wheelCode, alias);
                    break;
                case PendingKind.Clear:
                    QuickBindActionApplier.ApplyUnbound(context);
                    break;
                default:
                    return;
            }

            target.NotifyBindingChanged();
        }

        private static string DescribePending(PendingKind kind, VirtualKeys key, int mouseCode, MouseWheelCodes wheelCode)
        {
            return kind switch
            {
                PendingKind.Keyboard => OutputDataAliasUtil.GetDisplayStringForKeyboardKey((uint)key),
                PendingKind.MouseButton => OutputDataAliasUtil.GetStringForMouseButton(mouseCode),
                PendingKind.MouseWheel => OutputDataAliasUtil.GetStringForMouseWheelBtn((int)wheelCode),
                _ => "",
            };
        }

        private void ShowConfirm(string message, string actionLabel)
        {
            ConfirmText.Text = message;
            ConfirmActionButton.Content = actionLabel;
            ConfirmActionButton.Visibility = Visibility.Visible;
            ConfirmAdvancedButton.Visibility = Visibility.Visible;
            ConfirmPopup.IsOpen = true;
        }

        private void ShowMessage(string message)
        {
            pendingKind = PendingKind.None;
            ConfirmText.Text = message;
            ConfirmActionButton.Visibility = Visibility.Collapsed;
            ConfirmAdvancedButton.Visibility = Visibility.Collapsed;
            ConfirmPopup.IsOpen = true;
        }

        private void ConfirmCancel_Click(object sender, RoutedEventArgs e)
        {
            pendingKind = PendingKind.None;
            ConfirmPopup.IsOpen = false;
        }

        private void ConfirmAdvanced_Click(object sender, RoutedEventArgs e)
        {
            pendingKind = PendingKind.None;
            ConfirmPopup.IsOpen = false;
            OpenAdvancedEditor();
        }

        private void ConfirmAction_Click(object sender, RoutedEventArgs e)
        {
            PendingKind kind = pendingKind;
            pendingKind = PendingKind.None;
            ConfirmPopup.IsOpen = false;

            if (kind == PendingKind.None) return;
            ApplyPending(kind, pendingKey, pendingAlias, pendingMouseCode, pendingWheelCode);
        }

        private void ConfirmPopup_Closed(object sender, EventArgs e)
        {
            pendingKind = PendingKind.None;
        }
    }
}
