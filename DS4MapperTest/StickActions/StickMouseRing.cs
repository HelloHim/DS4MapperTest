using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.AxisModifiers;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.ActionUtil;

namespace DS4MapperTest.StickActions
{
    // Port of JoyShockMapper's MOUSE_RING stick mode: the stick's angle
    // (direction only, not magnitude) places the cursor at a fixed point on a
    // circle around screen center. Cursor position only updates while the
    // stick is outside its deadzone; it is left in place on release, exactly
    // matching JSM's behaviour.
    //
    // JSM expresses the ring radius in screen pixels plus separate
    // SCREEN_RESOLUTION_X/Y settings, so the ring stays a true circle
    // regardless of monitor aspect ratio. This app's absolute-mouse channel
    // (mapper.AbsMouseX/Y, see StickAbsMouse) is already resolution-agnostic
    // and works purely in normalized 0-1 screen-fraction space, so RingRadius
    // here is a normalized fraction applied uniformly to X/Y instead.
    public class StickMouseRing : StickMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string DEAD_ZONE = "DeadZone";
            public const string MAX_ZONE = "MaxZone";
            public const string RING_RADIUS = "RingRadius";
        }

        private HashSet<string> fullPropertySet = new HashSet<string>()
        {
            PropertyKeyStrings.NAME,
            PropertyKeyStrings.DEAD_ZONE,
            PropertyKeyStrings.MAX_ZONE,
            PropertyKeyStrings.RING_RADIUS,
        };

        public const string ACTION_TYPE_NAME = "StickMouseRingAction";

        public const double DefaultRingRadius = 0.15;
        public const double MaxRingRadius = 1.0;

        private StickDeadZone deadMod;
        private double ringRadius = DefaultRingRadius;

        private double xNorm = 0.0, yNorm = 0.0;
        private double xMotion;
        private double yMotion;

        public StickDeadZone DeadMod { get => deadMod; }

        public double RingRadius
        {
            get => ringRadius;
            set => ringRadius = Math.Clamp(value, 0.0, MaxRingRadius);
        }

        public StickMouseRing()
        {
            actionTypeName = ACTION_TYPE_NAME;
            deadMod = new StickDeadZone(0.10, 1.0, 0.0);
            deadMod.CircleDead = true;
        }

        public StickMouseRing(StickDefinition stickDefinition)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.stickDefinition = stickDefinition;
            deadMod = new StickDeadZone(0.10, 1.0, 0.0);
            deadMod.CircleDead = true;
        }

        public StickMouseRing(StickMouseRing parentAction)
        {
            actionTypeName = ACTION_TYPE_NAME;
            this.parentAction = parentAction;
            parentAction.hasLayeredAction = true;
            mappingId = parentAction.mappingId;
            this.stickDefinition = new StickDefinition(parentAction.stickDefinition);
            deadMod = new StickDeadZone(parentAction.deadMod);
            ringRadius = parentAction.ringRadius;
        }

        public override void Prepare(Mapper mapper, int axisXVal, int axisYVal,
            bool alterState = true)
        {
            active = false;
            activeEvent = false;

            xNorm = 0.0; yNorm = 0.0;
            int axisXMid = stickDefinition.xAxis.mid, axisYMid = stickDefinition.yAxis.mid;
            int axisXDir = axisXVal - axisXMid, axisYDir = axisYVal - axisYMid;
            bool xNegative = axisXDir < 0;
            bool yNegative = axisYDir < 0;
            int maxDirX = (!xNegative ? stickDefinition.xAxis.max : stickDefinition.xAxis.min) - axisXMid;
            int maxDirY = (!yNegative ? stickDefinition.yAxis.max : stickDefinition.yAxis.min) - axisYMid;
            deadMod.CalcOutValues(axisXDir, axisYDir, maxDirX,
                    maxDirY, out xNorm, out yNorm);

            if (xNorm != 0.0 || yNorm != 0.0)
            {
                double length = Math.Sqrt(xNorm * xNorm + yNorm * yNorm);
                double normX = xNorm / length;
                double normY = yNorm / length;

                double ringX = 0.5 + normX * ringRadius;
                double ringY = 0.5 - normY * ringRadius;

                xMotion = Math.Clamp(ringX, 0.0, 1.0);
                yMotion = Math.Clamp(ringY, 0.0, 1.0);

                active = true;
                activeEvent = true;
            }
        }

        public override void Event(Mapper mapper)
        {
            if (activeEvent)
            {
                mapper.AbsMouseX = xMotion;
                mapper.AbsMouseY = yMotion;
                mapper.AbsMouseSync = true;
            }

            active = xNorm != 0.0 || yNorm != 0.0;
            activeEvent = false;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            xNorm = yNorm = 0.0;
            xMotion = yMotion = 0.0;
            active = false;
            activeEvent = false;
        }

        public override void SoftRelease(Mapper mapper, MapAction _, bool resetState = true)
        {
            xNorm = yNorm = 0.0;
            xMotion = yMotion = 0.0;
            active = false;
            activeEvent = false;
        }

        public override StickMapAction DuplicateAction()
        {
            return new StickMouseRing(this);
        }

        public override void SoftCopyFromParent(StickMapAction parentAction)
        {
            if (parentAction is StickMouseRing tempRingAction)
            {
                base.SoftCopyFromParent(parentAction);

                this.parentAction = parentAction;
                tempRingAction.hasLayeredAction = true;
                mappingId = tempRingAction.mappingId;

                this.stickDefinition =
                    new StickDefinition(tempRingAction.stickDefinition);

                tempRingAction.NotifyPropertyChanged += TempRingAction_NotifyPropertyChanged;

                IEnumerable<string> useParentProList =
                    fullPropertySet.Except(changedProperties);

                foreach (string parentPropType in useParentProList)
                {
                    ApplyParentProperty(tempRingAction, parentPropType);
                }
            }
        }

        private void TempRingAction_NotifyPropertyChanged(object sender, NotifyPropertyChangeArgs e)
        {
            CascadePropertyChange(e.Mapper, e.PropertyName);
        }

        protected override void CascadePropertyChange(Mapper mapper, string propertyName)
        {
            if (changedProperties.Contains(propertyName))
            {
                // Property already overridden in action. Leave
                return;
            }
            else if (parentAction == null)
            {
                // No parent action. Leave
                return;
            }

            StickMouseRing tempRingAction = parentAction as StickMouseRing;
            ApplyParentProperty(tempRingAction, propertyName);
        }

        private void ApplyParentProperty(StickMouseRing tempRingAction, string propertyType)
        {
            switch (propertyType)
            {
                case PropertyKeyStrings.NAME:
                    name = tempRingAction.name;
                    break;
                case PropertyKeyStrings.DEAD_ZONE:
                    deadMod.DeadZone = tempRingAction.deadMod.DeadZone;
                    break;
                case PropertyKeyStrings.MAX_ZONE:
                    deadMod.MaxZone = tempRingAction.deadMod.MaxZone;
                    break;
                case PropertyKeyStrings.RING_RADIUS:
                    ringRadius = tempRingAction.ringRadius;
                    break;
                default:
                    break;
            }
        }
    }
}
