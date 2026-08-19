using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DS4MapperTest.Universal
{
    public static class UniversalInputCatalog
    {
        private static readonly IReadOnlyList<UniversalInputMetadata> all = BuildMetadata();
        private static readonly IReadOnlyDictionary<UniversalInputId, UniversalInputMetadata> byId =
            new ReadOnlyDictionary<UniversalInputId, UniversalInputMetadata>(
                all.ToDictionary(item => item.Id));

        public static IReadOnlyList<UniversalInputMetadata> All => all;

        public static UniversalInputMetadata GetMetadata(UniversalInputId id)
        {
            if (!byId.TryGetValue(id, out UniversalInputMetadata metadata))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown universal input id.");
            }

            return metadata;
        }

        public static bool TryGetMetadata(UniversalInputId id, out UniversalInputMetadata metadata)
        {
            return byId.TryGetValue(id, out metadata);
        }

        public static bool IsDefined(UniversalInputId id)
        {
            return byId.ContainsKey(id);
        }

        private static IReadOnlyList<UniversalInputMetadata> BuildMetadata()
        {
            UniversalInputMetadata[] items =
            {
                Button(UniversalInputId.FaceButtonNorth, UniversalInputCategory.FaceButton, "Face Button North"),
                Button(UniversalInputId.FaceButtonEast, UniversalInputCategory.FaceButton, "Face Button East"),
                Button(UniversalInputId.FaceButtonSouth, UniversalInputCategory.FaceButton, "Face Button South"),
                Button(UniversalInputId.FaceButtonWest, UniversalInputCategory.FaceButton, "Face Button West"),

                Button(UniversalInputId.DPadUp, UniversalInputCategory.DPad, "D-Pad Up"),
                Button(UniversalInputId.DPadDown, UniversalInputCategory.DPad, "D-Pad Down"),
                Button(UniversalInputId.DPadLeft, UniversalInputCategory.DPad, "D-Pad Left"),
                Button(UniversalInputId.DPadRight, UniversalInputCategory.DPad, "D-Pad Right"),

                Button(UniversalInputId.LeftShoulder, UniversalInputCategory.Shoulder, "Left Shoulder"),
                Button(UniversalInputId.RightShoulder, UniversalInputCategory.Shoulder, "Right Shoulder"),
                Axis(UniversalInputId.LeftTrigger, UniversalInputCategory.Trigger, "Left Trigger"),
                Axis(UniversalInputId.RightTrigger, UniversalInputCategory.Trigger, "Right Trigger"),
                Button(UniversalInputId.LeftTriggerFullPull, UniversalInputCategory.Trigger, "Left Trigger Full Pull"),
                Button(UniversalInputId.RightTriggerFullPull, UniversalInputCategory.Trigger, "Right Trigger Full Pull"),

                Stick(UniversalInputId.LeftStick, UniversalInputCategory.Stick, "Left Stick"),
                Stick(UniversalInputId.RightStick, UniversalInputCategory.Stick, "Right Stick"),
                Button(UniversalInputId.LeftStickClick, UniversalInputCategory.StickClick, "Left Stick Click"),
                Button(UniversalInputId.RightStickClick, UniversalInputCategory.StickClick, "Right Stick Click"),
                Button(UniversalInputId.LeftStickTouch, UniversalInputCategory.StickTouch, "Left Stick Touch"),
                Button(UniversalInputId.RightStickTouch, UniversalInputCategory.StickTouch, "Right Stick Touch"),

                Button(UniversalInputId.Menu, UniversalInputCategory.Menu, "Menu"),
                Button(UniversalInputId.View, UniversalInputCategory.Menu, "View"),
                Button(UniversalInputId.System, UniversalInputCategory.System, "System"),
                Button(UniversalInputId.NavigationPrimary, UniversalInputCategory.System, "Navigation Primary"),
                Button(UniversalInputId.NavigationSecondary, UniversalInputCategory.System, "Navigation Secondary"),
                Button(UniversalInputId.Capture, UniversalInputCategory.Capture, "Capture"),
                Button(UniversalInputId.Mute, UniversalInputCategory.Mute, "Mute"),
                Button(UniversalInputId.QuickAccessMenu, UniversalInputCategory.QuickAccess, "Quick Access Menu"),

                Button(UniversalInputId.LeftRearPrimary, UniversalInputCategory.RearControl, "Left Rear Primary"),
                Button(UniversalInputId.RightRearPrimary, UniversalInputCategory.RearControl, "Right Rear Primary"),
                Button(UniversalInputId.LeftRearSecondary, UniversalInputCategory.RearControl, "Left Rear Secondary"),
                Button(UniversalInputId.RightRearSecondary, UniversalInputCategory.RearControl, "Right Rear Secondary"),
                Button(UniversalInputId.LeftRearTertiary, UniversalInputCategory.RearControl, "Left Rear Tertiary"),
                Button(UniversalInputId.RightRearTertiary, UniversalInputCategory.RearControl, "Right Rear Tertiary"),
                Button(UniversalInputId.LeftGripTouch, UniversalInputCategory.RearControl, "Left Grip Touch"),
                Button(UniversalInputId.RightGripTouch, UniversalInputCategory.RearControl, "Right Grip Touch"),

                Button(UniversalInputId.LeftSidePrimary, UniversalInputCategory.SideControl, "Left Side Primary"),
                Button(UniversalInputId.LeftSideSecondary, UniversalInputCategory.SideControl, "Left Side Secondary"),
                Button(UniversalInputId.RightSidePrimary, UniversalInputCategory.SideControl, "Right Side Primary"),
                Button(UniversalInputId.RightSideSecondary, UniversalInputCategory.SideControl, "Right Side Secondary"),

                TouchSurface(UniversalInputId.PrimaryTouchSurface, UniversalInputCategory.TouchSurface, "Primary Touch Surface"),
                TouchSurface(UniversalInputId.LeftTouchSurface, UniversalInputCategory.TouchSurface, "Left Touch Surface"),
                TouchSurface(UniversalInputId.RightTouchSurface, UniversalInputCategory.TouchSurface, "Right Touch Surface"),
                Button(UniversalInputId.PrimaryTouchSurfaceClick, UniversalInputCategory.TouchSurfaceClick, "Primary Touch Surface Click"),
                Button(UniversalInputId.LeftTouchSurfaceClick, UniversalInputCategory.TouchSurfaceClick, "Left Touch Surface Click"),
                Button(UniversalInputId.RightTouchSurfaceClick, UniversalInputCategory.TouchSurfaceClick, "Right Touch Surface Click"),
                // Touch contacts are the capacitive "a finger rests here" sensors,
                // not the movement surface itself. They are digital so they can
                // carry an ordinary button binding, the same way the capacitive
                // stick touch sensors do.
                Button(UniversalInputId.PrimaryTouchContact, UniversalInputCategory.TouchSurface, "Primary Touch Contact"),
                Button(UniversalInputId.LeftTouchContact, UniversalInputCategory.TouchSurface, "Left Touch Contact"),
                Button(UniversalInputId.RightTouchContact, UniversalInputCategory.TouchSurface, "Right Touch Contact"),

                Gyroscope(UniversalInputId.Gyroscope),
                Accelerometer(UniversalInputId.Accelerometer),

                Button(UniversalInputId.MiscButton1, UniversalInputCategory.Miscellaneous, "Misc Button 1"),
                Button(UniversalInputId.MiscButton2, UniversalInputCategory.Miscellaneous, "Misc Button 2"),
                Button(UniversalInputId.MiscButton3, UniversalInputCategory.Miscellaneous, "Misc Button 3"),
                Button(UniversalInputId.MiscButton4, UniversalInputCategory.Miscellaneous, "Misc Button 4"),
                Button(UniversalInputId.MiscButton5, UniversalInputCategory.Miscellaneous, "Misc Button 5"),
                Button(UniversalInputId.MiscButton6, UniversalInputCategory.Miscellaneous, "Misc Button 6"),
                Button(UniversalInputId.MiscButton7, UniversalInputCategory.Miscellaneous, "Misc Button 7"),
                Button(UniversalInputId.MiscButton8, UniversalInputCategory.Miscellaneous, "Misc Button 8"),
                Button(UniversalInputId.MiscButton9, UniversalInputCategory.Miscellaneous, "Misc Button 9"),
                Button(UniversalInputId.MiscButton10, UniversalInputCategory.Miscellaneous, "Misc Button 10"),
                Button(UniversalInputId.MiscButton11, UniversalInputCategory.Miscellaneous, "Misc Button 11"),
                Button(UniversalInputId.MiscButton12, UniversalInputCategory.Miscellaneous, "Misc Button 12"),
                Button(UniversalInputId.MiscButton13, UniversalInputCategory.Miscellaneous, "Misc Button 13"),
                Button(UniversalInputId.MiscButton14, UniversalInputCategory.Miscellaneous, "Misc Button 14"),
                Button(UniversalInputId.MiscButton15, UniversalInputCategory.Miscellaneous, "Misc Button 15"),
                Button(UniversalInputId.MiscButton16, UniversalInputCategory.Miscellaneous, "Misc Button 16"),

                Axis(UniversalInputId.MiscAxis1, UniversalInputCategory.Miscellaneous, "Misc Axis 1"),
                Axis(UniversalInputId.MiscAxis2, UniversalInputCategory.Miscellaneous, "Misc Axis 2"),
                Axis(UniversalInputId.MiscAxis3, UniversalInputCategory.Miscellaneous, "Misc Axis 3"),
                Axis(UniversalInputId.MiscAxis4, UniversalInputCategory.Miscellaneous, "Misc Axis 4"),
                Axis(UniversalInputId.MiscAxis5, UniversalInputCategory.Miscellaneous, "Misc Axis 5"),
                Axis(UniversalInputId.MiscAxis6, UniversalInputCategory.Miscellaneous, "Misc Axis 6"),
                Axis(UniversalInputId.MiscAxis7, UniversalInputCategory.Miscellaneous, "Misc Axis 7"),
                Axis(UniversalInputId.MiscAxis8, UniversalInputCategory.Miscellaneous, "Misc Axis 8"),

                TouchSurface(UniversalInputId.MiscTouchSurface1, UniversalInputCategory.Miscellaneous, "Misc Touch Surface 1"),
                TouchSurface(UniversalInputId.MiscTouchSurface2, UniversalInputCategory.Miscellaneous, "Misc Touch Surface 2"),
            };

            UniversalInputId[] declaredIds = Enum.GetValues(typeof(UniversalInputId))
                .Cast<UniversalInputId>()
                .ToArray();

            UniversalInputId[] metadataIds = items.Select(item => item.Id).ToArray();
            UniversalInputId[] missing = declaredIds.Except(metadataIds).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException($"Universal input metadata is missing: {string.Join(", ", missing)}");
            }

            UniversalInputId[] extra = metadataIds.Except(declaredIds).ToArray();
            if (extra.Length > 0)
            {
                throw new InvalidOperationException($"Universal input metadata has unknown ids: {string.Join(", ", extra)}");
            }

            if (metadataIds.Distinct().Count() != metadataIds.Length)
            {
                throw new InvalidOperationException("Universal input metadata contains duplicate ids.");
            }

            return Array.AsReadOnly(items);
        }

        private static UniversalInputMetadata Button(
            UniversalInputId id,
            UniversalInputCategory category,
            string displayName)
        {
            return new UniversalInputMetadata(id, UniversalInputValueKind.DigitalButton, category, displayName);
        }

        private static UniversalInputMetadata Axis(
            UniversalInputId id,
            UniversalInputCategory category,
            string displayName)
        {
            return new UniversalInputMetadata(id, UniversalInputValueKind.AnalogAxis1D, category, displayName);
        }

        private static UniversalInputMetadata Stick(
            UniversalInputId id,
            UniversalInputCategory category,
            string displayName)
        {
            return new UniversalInputMetadata(id, UniversalInputValueKind.Stick2D, category, displayName);
        }

        private static UniversalInputMetadata TouchSurface(
            UniversalInputId id,
            UniversalInputCategory category,
            string displayName)
        {
            return new UniversalInputMetadata(id, UniversalInputValueKind.TouchSurface, category, displayName);
        }

        private static UniversalInputMetadata Gyroscope(UniversalInputId id)
        {
            return new UniversalInputMetadata(id, UniversalInputValueKind.Gyroscope, UniversalInputCategory.MotionSensor, "Gyroscope");
        }

        private static UniversalInputMetadata Accelerometer(UniversalInputId id)
        {
            return new UniversalInputMetadata(id, UniversalInputValueKind.Accelerometer, UniversalInputCategory.MotionSensor, "Accelerometer");
        }
    }
}
