using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DS4MapperTest.Universal.Editor
{
    // Produces controller-native labels only for face buttons. Other controls
    // stay generic unless a small evidence-backed misc lookup handles them.
    public static class ControllerLabelProvider
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<UniversalInputId, string>> FamilyLabels =
            new ReadOnlyDictionary<string, IReadOnlyDictionary<UniversalInputId, string>>(
                new Dictionary<string, IReadOnlyDictionary<UniversalInputId, string>>
                {
                    ["xbox"] = Xbox(),
                    ["playstation"] = PlayStation(),
                    ["nintendo"] = Nintendo(),
                    ["steam-controller"] = SteamController(),
                });

        public static string GetLabel(UniversalInputId inputId, ControllerCapabilities capabilities)
        {
            if (capabilities == null)
            {
                if (Xbox().TryGetValue(inputId, out string defaultFaceLabel))
                {
                    return defaultFaceLabel;
                }

                return UniversalInputCatalog.TryGetMetadata(inputId, out UniversalInputMetadata metadata)
                    ? metadata.DisplayName
                    : inputId.ToString();
            }

            if (IsGenericOnly(inputId))
            {
                return GetGenericLabel(inputId);
            }

            string family = capabilities.DisplayInfo?.GlyphFamily;
            if (!string.IsNullOrWhiteSpace(family) &&
                FamilyLabels.TryGetValue(family, out IReadOnlyDictionary<UniversalInputId, string> table) &&
                table.TryGetValue(inputId, out string nativeLabel))
            {
                return nativeLabel;
            }

            if (Xbox().TryGetValue(inputId, out string fallbackFaceLabel))
            {
                return fallbackFaceLabel;
            }

            return capabilities.GetDisplayLabel(inputId);
        }

        private static IReadOnlyDictionary<UniversalInputId, string> SteamController()
        {
            // Only the face buttons carry a verified physical printing
            // (Xbox-style ABXY). Everything else defers to the descriptor
            // native labels already supplied by the Step 3 adapter.
            return Xbox();
        }

        private static IReadOnlyDictionary<UniversalInputId, string> Xbox()
        {
            return new Dictionary<UniversalInputId, string>
            {
                [UniversalInputId.FaceButtonSouth] = "A",
                [UniversalInputId.FaceButtonEast] = "B",
                [UniversalInputId.FaceButtonWest] = "X",
                [UniversalInputId.FaceButtonNorth] = "Y",
            };
        }

        private static IReadOnlyDictionary<UniversalInputId, string> PlayStation()
        {
            return new Dictionary<UniversalInputId, string>
            {
                [UniversalInputId.FaceButtonSouth] = "Cross",
                [UniversalInputId.FaceButtonEast] = "Circle",
                [UniversalInputId.FaceButtonWest] = "Square",
                [UniversalInputId.FaceButtonNorth] = "Triangle",
            };
        }

        private static IReadOnlyDictionary<UniversalInputId, string> Nintendo()
        {
            return new Dictionary<UniversalInputId, string>
            {
                [UniversalInputId.FaceButtonSouth] = "B",
                [UniversalInputId.FaceButtonEast] = "A",
                [UniversalInputId.FaceButtonWest] = "Y",
                [UniversalInputId.FaceButtonNorth] = "X",
            };
        }

        private static bool IsGenericOnly(UniversalInputId inputId)
        {
            switch (inputId)
            {
                case UniversalInputId.LeftShoulder:
                case UniversalInputId.RightShoulder:
                case UniversalInputId.LeftTrigger:
                case UniversalInputId.RightTrigger:
                case UniversalInputId.LeftRearPrimary:
                case UniversalInputId.RightRearPrimary:
                case UniversalInputId.LeftRearSecondary:
                case UniversalInputId.RightRearSecondary:
                    return true;
                default:
                    return false;
            }
        }

        private static string GetGenericLabel(UniversalInputId inputId)
        {
            switch (inputId)
            {
                case UniversalInputId.LeftShoulder:
                    return "Left Bumper";
                case UniversalInputId.RightShoulder:
                    return "Right Bumper";
                case UniversalInputId.LeftTrigger:
                    return "Left Trigger";
                case UniversalInputId.RightTrigger:
                    return "Right Trigger";
                case UniversalInputId.LeftRearPrimary:
                    return "Left Paddle 1";
                case UniversalInputId.RightRearPrimary:
                    return "Right Paddle 1";
                case UniversalInputId.LeftRearSecondary:
                    return "Left Paddle 2";
                case UniversalInputId.RightRearSecondary:
                    return "Right Paddle 2";
                default:
                    return UniversalInputCatalog.TryGetMetadata(inputId, out UniversalInputMetadata metadata)
                        ? metadata.DisplayName
                        : inputId.ToString();
            }
        }
    }
}
