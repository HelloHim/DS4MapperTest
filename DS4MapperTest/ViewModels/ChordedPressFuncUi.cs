using System.Collections.Generic;
using System.Linq;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ViewModels
{
    internal static class ChordedPressFuncUi
    {
        public static List<ActionTriggerItem> BuildTriggerItems(Mapper mapper)
        {
            List<ActionTriggerItem> items = new List<ActionTriggerItem>
            {
                new ActionTriggerItem("Unbound", JoypadActionCodes.Empty),
            };

            if (mapper != null)
            {
                items.AddRange(mapper.ActionTriggerItems
                    .Where(item => item.Code != JoypadActionCodes.AlwaysOn));
            }

            return items;
        }
    }
}
