using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;
using System;
using System.Linq;

namespace DS4MapperTest.GyroActions
{
    public class GyroPassthruAction : GyroMapAction
    {
        public const string ACTION_TYPE_NAME = "GyroPassthruAction";

        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string TRIGGER_BUTTONS = "Triggers";
            public const string TRIGGER_ACTIVATE = "TriggerActivate";
            public const string ACTIVATION_HOLD_MS = "ActivationHoldMs";
            public const string TRIGGER_EVAL_COND = "TriggersEvalCond";
        }

        public struct GyroPassthruParams
        {
            public JoypadActionCodes[] gyroTriggerButtons;
            public bool andCond;
            public bool triggerActivates;
            public int activationHoldMs;

            public static GyroPassthruParams CreateDefault()
            {
                return new GyroPassthruParams
                {
                    gyroTriggerButtons = new[] { JoypadActionCodes.AlwaysOn },
                    andCond = false,
                    triggerActivates = true,
                    activationHoldMs = 0,
                };
            }
        }

        public GyroPassthruParams passthruParams;
        private readonly GyroActivationHold activationHold = new GyroActivationHold();

        public GyroPassthruAction()
        {
            actionTypeName = ACTION_TYPE_NAME;
            passthruParams = GyroPassthruParams.CreateDefault();
        }

        public GyroPassthruAction(GyroPassthruAction parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.parentAction = parentAction;
            parentAction.hasLayeredAction = true;
            passthruParams = parentAction.passthruParams;
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
            JoypadActionCodes[] triggerButtons = passthruParams.gyroTriggerButtons ??
                Array.Empty<JoypadActionCodes>();
            bool triggerButtonActive = triggerButtons.Contains(JoypadActionCodes.AlwaysOn) ||
                mapper.IsButtonsActiveDraft(triggerButtons, passthruParams.andCond);

            bool triggerActivated = true;
            if (!passthruParams.triggerActivates && triggerButtonActive)
            {
                triggerActivated = false;
            }
            else if (passthruParams.triggerActivates && !triggerButtonActive)
            {
                triggerActivated = false;
            }

            triggerActivated = activationHold.Update(
                triggerActivated,
                passthruParams.activationHoldMs,
                gyroFrame.timeElapsed);

            active = triggerActivated;
            activeEvent = triggerActivated;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            active = false;
            activeEvent = false;
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
                passthruParams = tempPassthru.passthruParams;
                mappingId = tempPassthru.mappingId;
            }
        }
    }
}
