using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS4MapperTest.StickActions
{
    public static class StickMethods
    {
        public static void RotatedCoordinates(int rotation,
            int axisXVal, int axisYVal, StickDefinition stickDefinition,
            out int outXVal, out int outYVal)
        {
            // + => Clockwise, - => Counter-clockwise. Invert initial rotation value
            double radians = (Math.PI * -rotation) / 180.0;
            double sinAngle = Math.Sin(radians), cosAngle = Math.Cos(radians);

            int tempX = axisXVal - stickDefinition.xAxis.mid;
            int tempY = axisYVal - stickDefinition.yAxis.mid;

            int rotX = (int)(tempX * cosAngle - tempY * sinAngle);
            int rotY = (int)(tempX * sinAngle + tempY * cosAngle);
            outXVal = Math.Clamp(rotX + stickDefinition.xAxis.mid, stickDefinition.xAxis.min, stickDefinition.xAxis.max);
            outYVal = Math.Clamp(rotY + stickDefinition.yAxis.mid, stickDefinition.yAxis.min, stickDefinition.yAxis.max);

        }
    }
}
