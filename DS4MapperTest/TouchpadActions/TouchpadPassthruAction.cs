using System;
using DS4MapperTest.ButtonActions;

namespace DS4MapperTest.TouchpadActions
{
    public class TouchpadPassthruAction : TouchpadMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string VIRTUAL_CLICK_PRESSURE_THRESHOLD = "VirtualClickPressureThreshold";
        }

        public const string ACTION_TYPE_NAME = "TouchPassthruAction";
        public const int DEFAULT_VIRTUAL_CLICK_PRESSURE_THRESHOLD =
            TouchpadPressureDualStageAction.DEFAULT_FULL_THRESHOLD;

        public override bool OutputsNativeTouch => true;

        private int virtualClickPressureThreshold = DEFAULT_VIRTUAL_CLICK_PRESSURE_THRESHOLD;
        public int VirtualClickPressureThreshold
        {
            get => virtualClickPressureThreshold;
            set => virtualClickPressureThreshold = Math.Clamp(value, 0, TouchpadPressureDualStageAction.MAX_PRESSURE);
        }

        public TouchpadPassthruAction()
        {
            actionTypeName = ACTION_TYPE_NAME;
        }

        public TouchpadPassthruAction(TouchpadPassthruAction parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.parentAction = parentAction;
            parentAction.hasLayeredAction = true;
            mappingId = parentAction.mappingId;
        }

        public override void Prepare(Mapper mapper, ref TouchEventFrame touchFrame, bool alterState = true)
        {
        }

        public override void Event(Mapper mapper)
        {
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
        }

        public override void SoftCopyFromParent(TouchpadMapAction parentAction)
        {
            if (parentAction is TouchpadPassthruAction tempPassthru)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                mappingId = tempPassthru.mappingId;
                virtualClickPressureThreshold = tempPassthru.virtualClickPressureThreshold;
            }
        }
    }
}
