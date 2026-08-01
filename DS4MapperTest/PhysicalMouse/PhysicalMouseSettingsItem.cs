using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DS4MapperTest.PhysicalMouse
{
    public sealed class PhysicalMouseSettingsItem
    {
        public string StableId { get; }
        public string DisplayName { get; }
        public bool IsAvailable { get; }

        public PhysicalMouseSettingsItem(string stableId, string displayName, bool isAvailable)
        {
            StableId = stableId;
            DisplayName = displayName;
            IsAvailable = isAvailable;
        }
    }

    public static class PhysicalMouseSettingsItems
    {
        private static readonly Regex SteamControllerPuckSlotRegex = new Regex(
            @"VID_28DE&PID_1304(?:&[^#]*)?&MI_0([2-5])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<PhysicalMouseSettingsItem> Create(
            IEnumerable<PhysicalMouseDevice> devices, string savedStableId)
        {
            List<PhysicalMouseDevice> usable = devices.Where(d => !d.IsLikelyVirtual).ToList();
            List<PhysicalMouseSettingsItem> items = usable.Select((d, index) =>
            {
                string label = TryGetSteamControllerPuckSlot(d.DevicePath, out int controllerSlot)
                    ? $"Steam Controller Puck - controller slot {controllerSlot} - VID 28DE, PID 1304"
                    : d.HasVendorProductId
                        ? $"{d.FriendlyName} - VID {d.VendorId:X4}, PID {d.ProductId:X4}"
                        : $"{d.FriendlyName} - device {index + 1}";
                return new PhysicalMouseSettingsItem(d.StableId, label, true);
            }).ToList();

            if (!string.IsNullOrEmpty(savedStableId) && !items.Any(i =>
                string.Equals(i.StableId, savedStableId, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(new PhysicalMouseSettingsItem(savedStableId,
                    "Saved physical mouse - unavailable", false));
            }

            return items;
        }

        internal static bool TryGetSteamControllerPuckSlot(string devicePath, out int controllerSlot)
        {
            controllerSlot = 0;
            Match match = SteamControllerPuckSlotRegex.Match(devicePath ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            controllerSlot = int.Parse(match.Groups[1].Value) - 1;
            return true;
        }
    }
}
