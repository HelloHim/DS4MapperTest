using DS4MapperTest.ActionUtil;

namespace DS4MapperTest.GyroActions
{
    public class GyroPassthruAction : GyroMapAction
    {
        public const string ACTION_TYPE_NAME = "GyroPassthruAction";

        public GyroPassthruAction()
        {
            actionTypeName = ACTION_TYPE_NAME;
        }

        public GyroPassthruAction(GyroPassthruAction parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.parentAction = parentAction;
            parentAction.hasLayeredAction = true;
            mappingId = parentAction.mappingId;
        }

        public override void BlankEvent(Mapper mapper)
        {
        }

        public override void Event(Mapper mapper)
        {
        }

        public override void Prepare(Mapper mapper, ref GyroEventFrame gyroFrame, bool alterState = true)
        {
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
        }

        public override GyroMapAction DuplicateAction()
        {
            return new GyroPassthruAction(this);
        }

        public override void SoftCopyFromParent(GyroMapAction parentAction)
        {
            if (parentAction is GyroPassthruAction tempPassthru)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                mappingId = tempPassthru.mappingId;
            }
        }
    }
}
