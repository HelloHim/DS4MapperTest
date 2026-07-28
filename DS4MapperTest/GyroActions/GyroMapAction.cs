using DS4MapperTest.ActionUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS4MapperTest.GyroActions
{
    internal sealed class GyroActivationHold
    {
        private bool initialized;
        private bool appliedState;
        private bool pendingState;
        private double pendingElapsedMs;

        public bool Update(bool requestedState, int holdMs, double elapsedSeconds)
        {
            if (!initialized)
            {
                initialized = true;
                appliedState = pendingState = requestedState;
                return appliedState;
            }

            if (requestedState == appliedState)
            {
                pendingState = requestedState;
                pendingElapsedMs = 0.0;
                return appliedState;
            }

            if (pendingState != requestedState)
            {
                pendingState = requestedState;
                pendingElapsedMs = 0.0;
            }

            pendingElapsedMs += Math.Max(0.0, elapsedSeconds) * 1000.0;
            if (pendingElapsedMs >= Math.Max(0, holdMs))
            {
                appliedState = requestedState;
                pendingElapsedMs = 0.0;
            }

            return appliedState;
        }
    }

    public struct GyroEventFrame
    {
        public short GyroYaw;
        public short GyroPitch;
        public short GyroRoll;
        public double AngGyroYaw;
        public double AngGyroPitch;
        public double AngGyroRoll;
        public short AccelX;
        public short AccelY;
        public short AccelZ;
        public double AccelXG, AccelYG, AccelZG;
        public double timeElapsed;

        public double elapsedReference;

        public double CurrentRate
        {
            get
            {
                double result = 1.0;
                if (timeElapsed > 0.0)
                {
                    result = 1.0 / timeElapsed;
                }

                return result;
            }
        }
    }

    public abstract class GyroMapAction : MapAction
    {
        public bool active;
        public bool activeEvent;
        protected GyroSensDefinition gyroSensDefinition = new GyroSensDefinition();
        public GyroSensDefinition GyroSensDefinition
        {
            get => gyroSensDefinition;
            set => gyroSensDefinition = value;
        }

        protected event EventHandler<NotifyPropertyChangeArgs> NotifyPropertyChanged;

        public abstract void Prepare(Mapper mapper, ref GyroEventFrame gyroFrame, bool alterState = true);

        public abstract void BlankEvent(Mapper mapper);

        public abstract GyroMapAction DuplicateAction();

        public void CopyBaseMapProps(GyroMapAction sourceAction)
        {
            mappingId = sourceAction.mappingId;
            gyroSensDefinition = new GyroSensDefinition(sourceAction.GyroSensDefinition);
        }

        public virtual void SoftCopyFromParent(GyroMapAction parentAction)
        {
        }

        protected virtual void CascadePropertyChange(Mapper mapper, string propertyName)
        {
        }

        public virtual void RaiseNotifyPropertyChange(Mapper mapper, string propertyName)
        {
            NotifyPropertyChanged?.Invoke(this,
                new NotifyPropertyChangeArgs(mapper, propertyName));
        }
    }
}
