using System;

namespace DS4MapperTest.PhysicalMouse
{
    public enum RawMouseButton
    {
        Left,
        Right,
        Middle,
        Button4,
        Button5,
    }

    /// <summary>
    /// Raw relative movement counts, exactly as reported by RAWMOUSE. No
    /// sensitivity, acceleration, smoothing or scaling applied.
    /// </summary>
    public class RawMouseMoveEventArgs : EventArgs
    {
        public int DeltaX { get; }
        public int DeltaY { get; }

        public RawMouseMoveEventArgs(int deltaX, int deltaY)
        {
            DeltaX = deltaX;
            DeltaY = deltaY;
        }
    }

    public class RawMouseButtonEventArgs : EventArgs
    {
        public RawMouseButton Button { get; }
        public bool IsPressed { get; }

        public RawMouseButtonEventArgs(RawMouseButton button, bool isPressed)
        {
            Button = button;
            IsPressed = isPressed;
        }
    }

    /// <summary>
    /// Raw wheel delta, in the same signed WHEEL_DELTA-multiple units RAWMOUSE
    /// reports (typically +/-120 per notch). Not normalised to +/-1 clicks.
    /// </summary>
    public class RawMouseWheelEventArgs : EventArgs
    {
        public int Delta { get; }
        public bool Horizontal { get; }

        public RawMouseWheelEventArgs(int delta, bool horizontal)
        {
            Delta = delta;
            Horizontal = horizontal;
        }
    }
}
