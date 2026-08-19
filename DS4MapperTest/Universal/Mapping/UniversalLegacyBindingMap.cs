using DS4MapperTest.DPadActions;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.TriggerActions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DS4MapperTest.Universal.Mapping
{
    public sealed class UniversalRuntimeBinding
    {
        public UniversalRuntimeBinding(
            UniversalInputId universalInput,
            string legacyBindingId,
            InputBindingMeta.InputControlType controlType,
            string displayName)
        {
            UniversalInput = universalInput;
            LegacyBindingId = legacyBindingId ?? throw new ArgumentNullException(nameof(legacyBindingId));
            ControlType = controlType;
            DisplayName = displayName ?? legacyBindingId;
        }

        public UniversalInputId UniversalInput { get; }
        public string LegacyBindingId { get; }
        public InputBindingMeta.InputControlType ControlType { get; }
        public string DisplayName { get; }
    }

    public static class UniversalLegacyBindingMap
    {
        private static readonly IReadOnlyDictionary<UniversalInputId, UniversalRuntimeBinding> byInput =
            new ReadOnlyDictionary<UniversalInputId, UniversalRuntimeBinding>(BuildBindings()
                .ToDictionary(item => item.UniversalInput));

        private static readonly IReadOnlyDictionary<string, UniversalInputId> inputByLegacyId =
            new ReadOnlyDictionary<string, UniversalInputId>(byInput.Values
                .GroupBy(item => item.LegacyBindingId, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.First().UniversalInput, StringComparer.Ordinal));

        public static IReadOnlyCollection<UniversalRuntimeBinding> Bindings => byInput.Values.ToArray();

        public static bool TryGetBinding(UniversalInputId input, out UniversalRuntimeBinding binding)
        {
            return byInput.TryGetValue(input, out binding);
        }

        public static bool TryGetUniversalInput(string legacyBindingId, out UniversalInputId input)
        {
            return inputByLegacyId.TryGetValue(legacyBindingId ?? string.Empty, out input);
        }

        public static IReadOnlyList<InputBindingMeta> CreateBindingList()
        {
            return Bindings
                .GroupBy(item => item.LegacyBindingId, StringComparer.Ordinal)
                .Select(item => item.First())
                .OrderBy(item => item.LegacyBindingId, StringComparer.Ordinal)
                .Select(item => new InputBindingMeta(item.LegacyBindingId, item.DisplayName, item.ControlType))
                .ToArray();
        }

        public static IReadOnlyDictionary<string, StickDefinition> CreateStickDefinitions()
        {
            return new ReadOnlyDictionary<string, StickDefinition>(
                new Dictionary<string, StickDefinition>(StringComparer.Ordinal)
                {
                    ["LeftStick"] = CreateStickDefinition(StickActionCodes.LS),
                    ["RightStick"] = CreateStickDefinition(StickActionCodes.RS),
                });
        }

        public static IReadOnlyDictionary<string, TriggerDefinition> CreateTriggerDefinitions()
        {
            return new ReadOnlyDictionary<string, TriggerDefinition>(
                new Dictionary<string, TriggerDefinition>(StringComparer.Ordinal)
                {
                    ["LeftTrigger"] = new TriggerDefinition(
                        new TriggerDefinition.TriggerAxisData
                        {
                            min = 0,
                            max = 255,
                            hasClickButton = true,
                            fullClickBtnCode = JoypadActionCodes.LTFullPull,
                        },
                        TriggerActionCodes.LeftTrigger),
                    ["RightTrigger"] = new TriggerDefinition(
                        new TriggerDefinition.TriggerAxisData
                        {
                            min = 0,
                            max = 255,
                            hasClickButton = true,
                            fullClickBtnCode = JoypadActionCodes.RTFullPull,
                        },
                        TriggerActionCodes.RightTrigger),
                });
        }

        public static IReadOnlyDictionary<string, TouchpadDefinition> CreateTouchpadDefinitions()
        {
            return new ReadOnlyDictionary<string, TouchpadDefinition>(
                new Dictionary<string, TouchpadDefinition>(StringComparer.Ordinal)
                {
                    ["PrimaryTouchSurface"] = CreateTouchpadDefinition(TouchpadActionCodes.TouchCenterWhole),
                    ["LeftTouchSurface"] = CreateTouchpadDefinition(TouchpadActionCodes.TouchL),
                    ["RightTouchSurface"] = CreateTouchpadDefinition(TouchpadActionCodes.TouchR),
                });
        }

        public static IReadOnlyDictionary<string, GyroSensDefinition> CreateGyroDefinitions()
        {
            return new ReadOnlyDictionary<string, GyroSensDefinition>(
                new Dictionary<string, GyroSensDefinition>(StringComparer.Ordinal)
                {
                    ["Gyroscope"] = new GyroSensDefinition
                    {
                        elapsedReference = 125.0,
                        mouseCoefficient = 0.025,
                        mouseOffset = 0.3,
                        accelMinLeanX = -16384,
                        accelMaxLeanX = 16384,
                        accelMinLeanY = -16384,
                        accelMaxLeanY = 16384,
                        accelMinLeanZ = -16384,
                        accelMaxLeanZ = 16384,
                    },
                });
        }

        private static IEnumerable<UniversalRuntimeBinding> BuildBindings()
        {
            yield return Button(UniversalInputId.FaceButtonSouth, "FaceButtonSouth", "Face Button South");
            yield return Button(UniversalInputId.FaceButtonEast, "FaceButtonEast", "Face Button East");
            yield return Button(UniversalInputId.FaceButtonWest, "FaceButtonWest", "Face Button West");
            yield return Button(UniversalInputId.FaceButtonNorth, "FaceButtonNorth", "Face Button North");
            yield return Button(UniversalInputId.LeftShoulder, "LeftShoulder", "Left Shoulder");
            yield return Button(UniversalInputId.RightShoulder, "RightShoulder", "Right Shoulder");
            yield return Button(UniversalInputId.LeftStickClick, "LeftStickClick", "Left Stick Click");
            yield return Button(UniversalInputId.RightStickClick, "RightStickClick", "Right Stick Click");
            yield return Button(UniversalInputId.Menu, "Menu", "Menu");
            yield return Button(UniversalInputId.View, "View", "View");
            yield return Button(UniversalInputId.System, "System", "System");
            yield return Button(UniversalInputId.NavigationPrimary, "NavigationPrimary", "Navigation Primary");
            yield return Button(UniversalInputId.NavigationSecondary, "NavigationSecondary", "Navigation Secondary");
            yield return Button(UniversalInputId.Capture, "Capture", "Capture");
            yield return Button(UniversalInputId.Mute, "Mute", "Mute");
            yield return Button(UniversalInputId.QuickAccessMenu, "QuickAccessMenu", "Quick Access Menu");
            yield return Button(UniversalInputId.LeftRearPrimary, "LeftRearPrimary", "Left Rear Primary");
            yield return Button(UniversalInputId.RightRearPrimary, "RightRearPrimary", "Right Rear Primary");
            yield return Button(UniversalInputId.LeftRearSecondary, "LeftRearSecondary", "Left Rear Secondary");
            yield return Button(UniversalInputId.RightRearSecondary, "RightRearSecondary", "Right Rear Secondary");
            yield return Button(UniversalInputId.LeftGripTouch, "LeftGripTouch", "Left Grip Touch");
            yield return Button(UniversalInputId.RightGripTouch, "RightGripTouch", "Right Grip Touch");
            yield return Button(UniversalInputId.LeftTriggerFullPull, "LeftTriggerFullPull", "Left Trigger Full Pull");
            yield return Button(UniversalInputId.RightTriggerFullPull, "RightTriggerFullPull", "Right Trigger Full Pull");
            yield return Button(UniversalInputId.PrimaryTouchSurfaceClick, "PrimaryTouchSurfaceClick", "Primary Touch Surface Click");
            yield return Button(UniversalInputId.LeftTouchSurfaceClick, "LeftTouchSurfaceClick", "Left Touch Surface Click");
            yield return Button(UniversalInputId.RightTouchSurfaceClick, "RightTouchSurfaceClick", "Right Touch Surface Click");
            yield return Button(UniversalInputId.PrimaryTouchContact, "PrimaryPadTouch", "Primary Pad Touch");
            yield return Button(UniversalInputId.LeftTouchContact, "LeftPadTouch", "Left Pad Touch");
            yield return Button(UniversalInputId.RightTouchContact, "RightPadTouch", "Right Pad Touch");
            yield return Button(UniversalInputId.LeftStickTouch, "LeftStickTouch", "Left Stick Touch");
            yield return Button(UniversalInputId.RightStickTouch, "RightStickTouch", "Right Stick Touch");
            yield return Button(UniversalInputId.MiscButton1, "MiscButton1", "Misc Button 1");
            yield return Button(UniversalInputId.MiscButton2, "MiscButton2", "Misc Button 2");
            yield return Button(UniversalInputId.MiscButton3, "MiscButton3", "Misc Button 3");
            yield return Button(UniversalInputId.MiscButton4, "MiscButton4", "Misc Button 4");
            yield return Button(UniversalInputId.MiscButton5, "MiscButton5", "Misc Button 5");
            yield return Button(UniversalInputId.MiscButton6, "MiscButton6", "Misc Button 6");
            yield return Button(UniversalInputId.MiscButton7, "MiscButton7", "Misc Button 7");
            yield return Button(UniversalInputId.MiscButton8, "MiscButton8", "Misc Button 8");
            yield return Button(UniversalInputId.MiscButton9, "MiscButton9", "Misc Button 9");
            yield return Button(UniversalInputId.MiscButton10, "MiscButton10", "Misc Button 10");
            yield return Button(UniversalInputId.MiscButton11, "MiscButton11", "Misc Button 11");
            yield return Button(UniversalInputId.MiscButton12, "MiscButton12", "Misc Button 12");
            yield return Button(UniversalInputId.MiscButton13, "MiscButton13", "Misc Button 13");
            yield return Button(UniversalInputId.MiscButton14, "MiscButton14", "Misc Button 14");
            yield return Button(UniversalInputId.MiscButton15, "MiscButton15", "Misc Button 15");
            yield return Button(UniversalInputId.MiscButton16, "MiscButton16", "Misc Button 16");
            yield return Control(UniversalInputId.DPadUp, "DPad", InputBindingMeta.InputControlType.DPad, "D-Pad");
            yield return Control(UniversalInputId.DPadDown, "DPad", InputBindingMeta.InputControlType.DPad, "D-Pad");
            yield return Control(UniversalInputId.DPadLeft, "DPad", InputBindingMeta.InputControlType.DPad, "D-Pad");
            yield return Control(UniversalInputId.DPadRight, "DPad", InputBindingMeta.InputControlType.DPad, "D-Pad");
            yield return Control(UniversalInputId.LeftTrigger, "LeftTrigger", InputBindingMeta.InputControlType.Trigger, "Left Trigger");
            yield return Control(UniversalInputId.RightTrigger, "RightTrigger", InputBindingMeta.InputControlType.Trigger, "Right Trigger");
            yield return Control(UniversalInputId.LeftStick, "LeftStick", InputBindingMeta.InputControlType.Stick, "Left Stick");
            yield return Control(UniversalInputId.RightStick, "RightStick", InputBindingMeta.InputControlType.Stick, "Right Stick");
            yield return Control(UniversalInputId.PrimaryTouchSurface, "PrimaryTouchSurface", InputBindingMeta.InputControlType.Touchpad, "Primary Touch Surface");
            yield return Control(UniversalInputId.LeftTouchSurface, "LeftTouchSurface", InputBindingMeta.InputControlType.Touchpad, "Left Touch Surface");
            yield return Control(UniversalInputId.RightTouchSurface, "RightTouchSurface", InputBindingMeta.InputControlType.Touchpad, "Right Touch Surface");
            yield return Control(UniversalInputId.Gyroscope, "Gyroscope", InputBindingMeta.InputControlType.Gyro, "Gyroscope");
        }

        private static UniversalRuntimeBinding Button(
            UniversalInputId input,
            string legacyBindingId,
            string displayName)
        {
            return Control(input, legacyBindingId, InputBindingMeta.InputControlType.Button, displayName);
        }

        private static UniversalRuntimeBinding Control(
            UniversalInputId input,
            string legacyBindingId,
            InputBindingMeta.InputControlType controlType,
            string displayName)
        {
            return new UniversalRuntimeBinding(input, legacyBindingId, controlType, displayName);
        }

        private static StickDefinition CreateStickDefinition(StickActionCodes code)
        {
            StickDefinition.StickAxisData axis = new StickDefinition.StickAxisData
            {
                min = -30000,
                max = 30000,
                mid = 0,
                hard_min = -32768,
                hard_max = 32767,
            };
            axis.PostInit();
            return new StickDefinition(axis, axis, code);
        }

        private static TouchpadDefinition CreateTouchpadDefinition(TouchpadActionCodes code)
        {
            TouchpadDefinition.TouchAxisData axis = new TouchpadDefinition.TouchAxisData
            {
                min = -32768,
                max = 32767,
                mid = 0,
                hard_min = -32768,
                hard_max = 32767,
            };
            axis.PostInit();
            return new TouchpadDefinition(axis, axis, code, 8.0, 0.012 * 1.1, 0.4, 0.000023);
        }

        public static short ScaleAxisToByte(double value)
        {
            return (short)Math.Round(UniversalValueNormalizer.Clamp01(value) * 255.0);
        }

        public static int ScaleStickAxis(double value)
        {
            return (int)Math.Round(UniversalValueNormalizer.ClampSigned(value) * 30000.0);
        }

        public static short ScaleTouchAxis(double value)
        {
            return (short)Math.Round((UniversalValueNormalizer.Clamp01(value) * 65535.0) - 32768.0);
        }

        public static DpadDirections ComposeDpad(UniversalControllerStateSnapshot state)
        {
            DpadDirections result = DpadDirections.Centered;
            if (IsPressed(state, UniversalInputId.DPadUp)) result |= DpadDirections.Up;
            if (IsPressed(state, UniversalInputId.DPadDown)) result |= DpadDirections.Down;
            if (IsPressed(state, UniversalInputId.DPadLeft)) result |= DpadDirections.Left;
            if (IsPressed(state, UniversalInputId.DPadRight)) result |= DpadDirections.Right;
            return result;
        }

        public static bool IsPressed(UniversalControllerStateSnapshot state, UniversalInputId input)
        {
            return state != null &&
                state.TryGetValue(input, out UniversalInputValue value) &&
                value.Status == UniversalInputValueStatus.Available &&
                value.Kind == UniversalInputValueKind.DigitalButton &&
                value.Pressed;
        }
    }
}
