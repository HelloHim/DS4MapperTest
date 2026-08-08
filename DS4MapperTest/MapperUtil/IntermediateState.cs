using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS4MapperTest.MapperUtil
{
    public struct IntermediateState
    {
        public uint PacketCounter;
        public double LX;
        public double LY;
        public bool LSDirty;
        public double RX;
        public double RY;
        public bool RSDirty;
        public double LTrigger;
        public double RTrigger;

        public bool BtnNorth;
        public bool BtnWest;
        public bool BtnSouth;
        public bool BtnEast;
        public bool BtnLShoulder;
        public bool BtnRShoulder;
        public bool BtnMode;
        public bool BtnStart;
        public bool BtnSelect;
        public bool BtnHome;
        public bool BtnExtra;
        public bool BtnLGrip;
        public bool BtnRGrip;
        public bool BtnMode2;
        public bool BtnMode3;
        public bool BtnLGrip2;
        public bool BtnRGrip2;
        public bool BtnThumbL;
        public bool BtnThumbR;
        public bool BtnTouchClick;

        public bool DpadUp;
        public bool DpadLeft;
        public bool DpadDown;
        public bool DpadRight;

        public short GyroYaw;
        public short GyroPitch;
        public short GyroRoll;
        public short AngGyroYaw;
        public short AngGyroPitch;
        public short AngGyroRoll;
        public short AccelX;
        public short AccelY;
        public short AccelZ;
        public short AccelXG;
        public short AccelYG;
        public short AccelZG;

        public double Touch1XNorm;
        public double Touch1YNorm;
        public bool Touch1Active;
        public double Touch2XNorm;
        public double Touch2YNorm;
        public bool Touch2Active;

        public bool Dirty;
    }
}
