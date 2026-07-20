using System.Collections.Generic;
using System.Windows.Input;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.Views
{
    /// <summary>
    /// Converts a captured WPF keyboard/mouse input into the same VirtualKeys /
    /// MouseButtonCodes / MouseWheelCodes values and profile alias strings the
    /// advanced editor's combo box lists already use (ButtonActionEditViewModel.
    /// PopulateComboBoxAliases), so a Quick Bind capture round-trips identically
    /// to one made through the advanced editor.
    /// </summary>
    internal static class QuickBindKeyMapper
    {
        // (VirtualKeys, profile alias string) pairs, mirroring the literal
        // strings baked into KeyboardCodeItem construction in
        // ButtonActionEditViewModel.PopulateComboBoxAliases. Numpad0-9 are a
        // Quick-Bind-only addition; the backend (VirtualKeys, serialization)
        // already supports them even though the old dropdown list never did.
        private static readonly Dictionary<Key, (VirtualKeys Code, string Alias)> keyMap =
            new Dictionary<Key, (VirtualKeys, string)>
        {
            [Key.A] = (VirtualKeys.A, "A"),
            [Key.B] = (VirtualKeys.B, "B"),
            [Key.C] = (VirtualKeys.C, "C"),
            [Key.D] = (VirtualKeys.D, "D"),
            [Key.E] = (VirtualKeys.E, "E"),
            [Key.F] = (VirtualKeys.F, "F"),
            [Key.G] = (VirtualKeys.G, "G"),
            [Key.H] = (VirtualKeys.H, "H"),
            [Key.I] = (VirtualKeys.I, "I"),
            [Key.J] = (VirtualKeys.J, "J"),
            [Key.K] = (VirtualKeys.K, "K"),
            [Key.L] = (VirtualKeys.L, "L"),
            [Key.M] = (VirtualKeys.M, "M"),
            [Key.N] = (VirtualKeys.N, "N"),
            [Key.O] = (VirtualKeys.O, "O"),
            [Key.P] = (VirtualKeys.P, "P"),
            [Key.Q] = (VirtualKeys.Q, "Q"),
            [Key.R] = (VirtualKeys.R, "R"),
            [Key.S] = (VirtualKeys.S, "S"),
            [Key.T] = (VirtualKeys.T, "T"),
            [Key.U] = (VirtualKeys.U, "U"),
            [Key.V] = (VirtualKeys.V, "V"),
            [Key.W] = (VirtualKeys.W, "W"),
            [Key.X] = (VirtualKeys.X, "X"),
            [Key.Y] = (VirtualKeys.Y, "Y"),
            [Key.Z] = (VirtualKeys.Z, "Z"),

            [Key.D0] = (VirtualKeys.N0, "N0"),
            [Key.D1] = (VirtualKeys.N1, "N1"),
            [Key.D2] = (VirtualKeys.N2, "N2"),
            [Key.D3] = (VirtualKeys.N3, "N3"),
            [Key.D4] = (VirtualKeys.N4, "N4"),
            [Key.D5] = (VirtualKeys.N5, "N5"),
            [Key.D6] = (VirtualKeys.N6, "N6"),
            [Key.D7] = (VirtualKeys.N7, "N7"),
            [Key.D8] = (VirtualKeys.N8, "N8"),
            [Key.D9] = (VirtualKeys.N9, "N9"),

            [Key.NumPad0] = (VirtualKeys.Numpad0, "Numpad0"),
            [Key.NumPad1] = (VirtualKeys.Numpad1, "Numpad1"),
            [Key.NumPad2] = (VirtualKeys.Numpad2, "Numpad2"),
            [Key.NumPad3] = (VirtualKeys.Numpad3, "Numpad3"),
            [Key.NumPad4] = (VirtualKeys.Numpad4, "Numpad4"),
            [Key.NumPad5] = (VirtualKeys.Numpad5, "Numpad5"),
            [Key.NumPad6] = (VirtualKeys.Numpad6, "Numpad6"),
            [Key.NumPad7] = (VirtualKeys.Numpad7, "Numpad7"),
            [Key.NumPad8] = (VirtualKeys.Numpad8, "Numpad8"),
            [Key.NumPad9] = (VirtualKeys.Numpad9, "Numpad9"),

            [Key.Escape] = (VirtualKeys.Escape, "Escape"),
            [Key.Space] = (VirtualKeys.Space, "Space"),
            [Key.Tab] = (VirtualKeys.Tab, "Tab"),
            [Key.OemTilde] = (VirtualKeys.OEM3, "Grave"),
            [Key.CapsLock] = (VirtualKeys.CapsLock, "CapsLock"),
            [Key.OemMinus] = (VirtualKeys.OEMMinus, "Minus"),
            [Key.OemPlus] = (VirtualKeys.OEMPlus, "Equal"),
            [Key.OemOpenBrackets] = (VirtualKeys.OEM4, "LeftBracket"),
            [Key.OemCloseBrackets] = (VirtualKeys.OEM6, "RightBracket"),
            [Key.OemBackslash] = (VirtualKeys.OEM5, "Backslash"),
            [Key.OemPipe] = (VirtualKeys.OEM5, "Backslash"),
            [Key.OemSemicolon] = (VirtualKeys.OEM1, "Semicolon"),
            [Key.OemQuotes] = (VirtualKeys.OEM7, "Quote"),
            [Key.OemComma] = (VirtualKeys.OEMComma, "Comma"),
            [Key.OemPeriod] = (VirtualKeys.OEMPeriod, "Period"),
            [Key.OemQuestion] = (VirtualKeys.OEM2, "Slash"),

            [Key.Insert] = (VirtualKeys.Insert, "Insert"),
            [Key.Home] = (VirtualKeys.Home, "Home"),
            [Key.End] = (VirtualKeys.End, "End"),
            [Key.PageUp] = (VirtualKeys.Prior, "PageUp"),
            [Key.PageDown] = (VirtualKeys.Next, "PageDown"),
            [Key.Enter] = (VirtualKeys.Return, "Enter"),
            [Key.PrintScreen] = (VirtualKeys.Snapshot, "PrintScreen"),
            [Key.Scroll] = (VirtualKeys.ScrollLock, "ScrollLock"),
            [Key.Pause] = (VirtualKeys.Pause, "Pause"),

            [Key.LeftAlt] = (VirtualKeys.LeftMenu, "LeftAlt"),
            [Key.RightAlt] = (VirtualKeys.RightMenu, "RightAlt"),
            [Key.LeftShift] = (VirtualKeys.LeftShift, "LeftShift"),
            [Key.RightShift] = (VirtualKeys.RightShift, "RightShift"),
            [Key.LeftCtrl] = (VirtualKeys.LeftControl, "LeftControl"),
            [Key.RightCtrl] = (VirtualKeys.RightControl, "RightControl"),
            [Key.LWin] = (VirtualKeys.LeftWindows, "LeftWindows"),
            [Key.RWin] = (VirtualKeys.RightWindows, "RightWindows"),

            [Key.Up] = (VirtualKeys.Up, "Up"),
            [Key.Down] = (VirtualKeys.Down, "Down"),
            [Key.Left] = (VirtualKeys.Left, "Left"),
            [Key.Right] = (VirtualKeys.Right, "Right"),

            [Key.F1] = (VirtualKeys.F1, "F1"),
            [Key.F2] = (VirtualKeys.F2, "F2"),
            [Key.F3] = (VirtualKeys.F3, "F3"),
            [Key.F4] = (VirtualKeys.F4, "F4"),
            [Key.F5] = (VirtualKeys.F5, "F5"),
            [Key.F6] = (VirtualKeys.F6, "F6"),
            [Key.F7] = (VirtualKeys.F7, "F7"),
            [Key.F8] = (VirtualKeys.F8, "F8"),
            [Key.F9] = (VirtualKeys.F9, "F9"),
            [Key.F10] = (VirtualKeys.F10, "F10"),
            [Key.F11] = (VirtualKeys.F11, "F11"),
            [Key.F12] = (VirtualKeys.F12, "F12"),
        };

        // Delete/Backspace are reserved for "clear the binding" and are never
        // offered as capturable outputs, matching the spec's single clear path.
        public static bool IsReservedClearKey(Key key) => key == Key.Delete || key == Key.Back;

        public static bool TryMapKey(Key key, out VirtualKeys code, out string alias)
        {
            if (keyMap.TryGetValue(key, out (VirtualKeys Code, string Alias) entry))
            {
                code = entry.Code;
                alias = entry.Alias;
                return true;
            }

            code = default;
            alias = null;
            return false;
        }

        public static bool TryMapMouseButton(MouseButton button, out int code, out string alias)
        {
            switch (button)
            {
                case MouseButton.Left:
                    code = MouseButtonCodes.MOUSE_LEFT_BUTTON;
                    alias = OutputActionDataSerializer.MouseButtonOutputAliases.LeftButton.ToString();
                    return true;
                case MouseButton.Right:
                    code = MouseButtonCodes.MOUSE_RIGHT_BUTTON;
                    alias = OutputActionDataSerializer.MouseButtonOutputAliases.RightButton.ToString();
                    return true;
                case MouseButton.Middle:
                    code = MouseButtonCodes.MOUSE_MIDDLE_BUTTON;
                    alias = OutputActionDataSerializer.MouseButtonOutputAliases.MiddleButton.ToString();
                    return true;
                case MouseButton.XButton1:
                    code = MouseButtonCodes.MOUSE_XBUTTON1;
                    alias = OutputActionDataSerializer.MouseButtonOutputAliases.XButton1.ToString();
                    return true;
                case MouseButton.XButton2:
                    code = MouseButtonCodes.MOUSE_XBUTTON2;
                    alias = OutputActionDataSerializer.MouseButtonOutputAliases.XButton2.ToString();
                    return true;
                default:
                    code = 0;
                    alias = null;
                    return false;
            }
        }

        public static (MouseWheelCodes Code, string Alias) MapWheelDelta(int delta)
        {
            MouseWheelCodes code = delta > 0 ? MouseWheelCodes.WheelUp : MouseWheelCodes.WheelDown;
            string alias = delta > 0
                ? OutputActionDataSerializer.MouseWheelAliases.WheelUp.ToString()
                : OutputActionDataSerializer.MouseWheelAliases.WheelDown.ToString();
            return (code, alias);
        }
    }
}
