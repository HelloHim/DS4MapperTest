using System;
using System.Collections.Generic;
using System.Linq;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ViewModels
{
    public class ChordedPressFuncPropViewModel
    {
        private readonly Mapper mapper;
        private readonly ButtonAction action;
        private readonly ChordedPressFunc func;
        private readonly List<ActionTriggerItem> triggerItems;

        public string Name
        {
            get => func.Name;
            set
            {
                func.Name = value;
                MarkFunctionsChanged();
            }
        }

        public string DisplayBind
        {
            get
            {
                string result = func.DescribeOutputActions(mapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        public List<ActionTriggerItem> TriggerItems => triggerItems;

        public JoypadActionCodes TriggerButton
        {
            get => func.TriggerButton;
            set
            {
                if (func.TriggerButton == value) return;
                func.TriggerButton = value;
                MarkFunctionsChanged();
            }
        }

        public ChordedPressFuncPropViewModel(Mapper mapper, ButtonAction action,
            ChordedPressFunc func)
        {
            this.mapper = mapper;
            this.action = action;
            this.func = func;
            triggerItems = ChordedPressFuncUi.BuildTriggerItems(mapper);
        }

        private void MarkFunctionsChanged()
        {
            if (action == null) return;
            if (!action.ChangedProperties.Contains(ButtonAction.PropertyKeyStrings.FUNCTIONS))
            {
                action.ChangedProperties.Add(ButtonAction.PropertyKeyStrings.FUNCTIONS);
            }
        }
    }
}
