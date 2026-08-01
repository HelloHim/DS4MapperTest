using System;
using System.Collections.Generic;
using System.Linq;

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
        public static List<PhysicalMouseSettingsItem> Create(
            IEnumerable<PhysicalMouseDevice> devices, string savedStableId)
        {
            List<PhysicalMouseDevice> usable = devices.Where(d => !d.IsLikelyVirtual).ToList();
            List<PhysicalMouseSettingsItem> items = usable.Select((d, index) =>
            {
                string label = d.HasVendorProductId
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
    }
}
