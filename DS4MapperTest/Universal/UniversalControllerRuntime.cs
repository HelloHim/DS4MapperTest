using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DS4MapperTest.Universal
{
    public static class UniversalControllerBackendIds
    {
        public const string Sdl3 = "sdl3";
        public const string SteamControllerNative = "steam-controller-native";
        public const string DiagnosticObserver = "diagnostic-observer";
        public const string OfflineActionContentEditor = "offline-action-content-editor";
    }

    public enum UniversalControllerConnectionState
    {
        Connected,
        Disconnected,
        Suppressed,
        Faulted,
    }

    public enum UniversalInputValueStatus
    {
        Available,
        TemporarilyUnavailable,
    }

    public readonly struct UniversalVector2
    {
        public double X { get; }
        public double Y { get; }

        public UniversalVector2(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public readonly struct UniversalVector3
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public UniversalVector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public sealed class UniversalTouchContact
    {
        public int ContactId { get; }
        public bool Active { get; }
        public double X { get; }
        public double Y { get; }
        public double? Pressure { get; }

        public UniversalTouchContact(int contactId, bool active, double x, double y, double? pressure)
        {
            ContactId = contactId;
            Active = active;
            X = UniversalValueNormalizer.Clamp01(x);
            Y = UniversalValueNormalizer.Clamp01(y);
            Pressure = pressure.HasValue ? UniversalValueNormalizer.Clamp01(pressure.Value) : null;
        }
    }

    public sealed class UniversalInputValue
    {
        private readonly IReadOnlyList<UniversalTouchContact> contacts;

        public UniversalInputValueKind Kind { get; }
        public UniversalInputValueStatus Status { get; }
        public bool IsActive { get; }
        public bool Pressed { get; }
        public double AxisValue { get; }
        public UniversalVector2 Vector2 { get; }
        public UniversalVector3 Vector3 { get; }
        public bool TouchClickPressed { get; }
        public IReadOnlyList<UniversalTouchContact> Contacts => contacts;

        private UniversalInputValue(
            UniversalInputValueKind kind,
            UniversalInputValueStatus status,
            bool isActive,
            bool pressed,
            double axisValue,
            UniversalVector2 vector2,
            UniversalVector3 vector3,
            bool touchClickPressed,
            IEnumerable<UniversalTouchContact> contacts)
        {
            Kind = kind;
            Status = status;
            IsActive = isActive;
            Pressed = pressed;
            AxisValue = axisValue;
            Vector2 = vector2;
            Vector3 = vector3;
            TouchClickPressed = touchClickPressed;
            this.contacts = new ReadOnlyCollection<UniversalTouchContact>(
                (contacts ?? Enumerable.Empty<UniversalTouchContact>()).ToArray());
        }

        public static UniversalInputValue DigitalButton(bool pressed)
        {
            return new UniversalInputValue(
                UniversalInputValueKind.DigitalButton,
                UniversalInputValueStatus.Available,
                pressed,
                pressed,
                0,
                default,
                default,
                false,
                null);
        }

        public static UniversalInputValue AnalogAxis(double value)
        {
            double clamped = UniversalValueNormalizer.ClampSigned(value);
            return new UniversalInputValue(
                UniversalInputValueKind.AnalogAxis1D,
                UniversalInputValueStatus.Available,
                Math.Abs(clamped) > 0.000001,
                false,
                clamped,
                default,
                default,
                false,
                null);
        }

        public static UniversalInputValue Stick(double x, double y)
        {
            UniversalVector2 vector = new UniversalVector2(
                UniversalValueNormalizer.ClampSigned(x),
                UniversalValueNormalizer.ClampSigned(y));

            return new UniversalInputValue(
                UniversalInputValueKind.Stick2D,
                UniversalInputValueStatus.Available,
                Math.Abs(vector.X) > 0.000001 || Math.Abs(vector.Y) > 0.000001,
                false,
                0,
                vector,
                default,
                false,
                null);
        }

        public static UniversalInputValue TouchSurface(
            IEnumerable<UniversalTouchContact> contacts,
            bool clickPressed = false)
        {
            UniversalTouchContact[] contactArray =
                (contacts ?? Enumerable.Empty<UniversalTouchContact>()).ToArray();

            return new UniversalInputValue(
                UniversalInputValueKind.TouchSurface,
                UniversalInputValueStatus.Available,
                clickPressed || contactArray.Any(item => item.Active),
                false,
                0,
                default,
                default,
                clickPressed,
                contactArray);
        }

        public static UniversalInputValue Gyroscope(double x, double y, double z)
        {
            return Motion(UniversalInputValueKind.Gyroscope, x, y, z);
        }

        public static UniversalInputValue Accelerometer(double x, double y, double z)
        {
            return Motion(UniversalInputValueKind.Accelerometer, x, y, z);
        }

        public static UniversalInputValue TemporarilyUnavailable(UniversalInputValueKind kind)
        {
            return new UniversalInputValue(
                kind,
                UniversalInputValueStatus.TemporarilyUnavailable,
                false,
                false,
                0,
                default,
                default,
                false,
                null);
        }

        private static UniversalInputValue Motion(
            UniversalInputValueKind kind,
            double x,
            double y,
            double z)
        {
            UniversalVector3 vector = new UniversalVector3(x, y, z);
            return new UniversalInputValue(
                kind,
                UniversalInputValueStatus.Available,
                Math.Abs(x) > 0.000001 || Math.Abs(y) > 0.000001 || Math.Abs(z) > 0.000001,
                false,
                0,
                default,
                vector,
                false,
                null);
        }
    }

    public sealed class UniversalControllerStateSnapshot
    {
        private readonly IReadOnlyDictionary<UniversalInputId, UniversalInputValue> valuesByInput;

        public DateTimeOffset TimestampUtc { get; }
        public long Sequence { get; }
        public bool IsConnected { get; }
        public IReadOnlyDictionary<UniversalInputId, UniversalInputValue> Values => valuesByInput;

        public UniversalControllerStateSnapshot(
            DateTimeOffset timestampUtc,
            long sequence,
            bool isConnected,
            IDictionary<UniversalInputId, UniversalInputValue> valuesByInput)
        {
            TimestampUtc = timestampUtc;
            Sequence = sequence;
            IsConnected = isConnected;

            Dictionary<UniversalInputId, UniversalInputValue> copy =
                new Dictionary<UniversalInputId, UniversalInputValue>();

            foreach (KeyValuePair<UniversalInputId, UniversalInputValue> item in valuesByInput ?? new Dictionary<UniversalInputId, UniversalInputValue>())
            {
                UniversalInputMetadata metadata = UniversalInputCatalog.GetMetadata(item.Key);
                if (item.Value == null)
                {
                    throw new ArgumentException("Universal controller state cannot contain null values.", nameof(valuesByInput));
                }

                if (metadata.ValueKind != item.Value.Kind)
                {
                    throw new ArgumentException(
                        $"Input {item.Key} is {metadata.ValueKind}, not {item.Value.Kind}.",
                        nameof(valuesByInput));
                }

                copy.Add(item.Key, item.Value);
            }

            this.valuesByInput =
                new ReadOnlyDictionary<UniversalInputId, UniversalInputValue>(copy);
        }

        public static UniversalControllerStateSnapshot Disconnected(long sequence = 0)
        {
            return new UniversalControllerStateSnapshot(
                DateTimeOffset.UtcNow,
                sequence,
                false,
                new Dictionary<UniversalInputId, UniversalInputValue>());
        }

        public bool TryGetValue(UniversalInputId inputId, out UniversalInputValue value)
        {
            return valuesByInput.TryGetValue(inputId, out value);
        }
    }

    public static class UniversalValueNormalizer
    {
        public const double SteamControllerPadMin = -32768.0;
        public const double SteamControllerPadMax = 32767.0;
        public const double SteamControllerTriggerMax = 255.0;

        public static double NormalizeSignedAxis(short value)
        {
            return ClampSigned(value < 0 ? value / 32768.0 : value / 32767.0);
        }

        public static double NormalizeSdlStickY(short value)
        {
            // Universal stick Y is positive up. SDL reports up as negative.
            return ClampSigned(-NormalizeSignedAxis(value));
        }

        public static double NormalizeSdlTrigger(short value)
        {
            return Clamp01(value / 32767.0);
        }

        public static double NormalizeSteamControllerTrigger(byte value)
        {
            return Clamp01(value / SteamControllerTriggerMax);
        }

        public static double NormalizeSignedTouchAxis(short value)
        {
            return Clamp01((value - SteamControllerPadMin) / (SteamControllerPadMax - SteamControllerPadMin));
        }

        public static double DegreesPerSecondToRadiansPerSecond(double value)
        {
            return value * Math.PI / 180.0;
        }

        public static double GToMetresPerSecondSquared(double value)
        {
            return value * 9.80665;
        }

        public static double ClampSigned(double value)
        {
            if (value < -1.0) return -1.0;
            if (value > 1.0) return 1.0;
            return value;
        }

        public static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }
    }
}
