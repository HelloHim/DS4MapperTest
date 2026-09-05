using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS4MapperTest.StickModifiers
{
    public struct Vector2
    {
        public double x;
        public double y;

        public Vector2(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public class SquareStick
    {
        public Vector2 current;
        public Vector2 squared;

        public SquareStick()
        {
            current = new Vector2(0.0, 0.0);
            squared = new Vector2(0.0, 0.0);
        }

        // Modification of squared stick routine documented
        // at http://theinstructionlimit.com/squaring-the-thumbsticks
        public void CircleToSquare(double roundness)
        {
            const double PiOverFour = Math.PI / 4.0;

            // Determine the theta angle
            double angle = Math.Atan2(current.y, -current.x);
            angle += Math.PI;
            double cosAng = Math.Cos(angle);
            // Scale according to which wall we're clamping to
            // X+ wall
            if (angle <= PiOverFour || angle > 7.0 * PiOverFour)
            {
                double tempVal = 1.0 / cosAng;
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            // Y+ wall
            else if (angle > PiOverFour && angle <= 3.0 * PiOverFour)
            {
                double tempVal = 1.0 / Math.Sin(angle);
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            // X- wall
            else if (angle > 3.0 * PiOverFour && angle <= 5.0 * PiOverFour)
            {
                double tempVal = -1.0 / cosAng;
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            // Y- wall
            else if (angle > 5.0 * PiOverFour && angle <= 7.0 * PiOverFour)
            {
                double tempVal = -1.0 / Math.Sin(angle);
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            else return;

            double length = current.x / cosAng;
            double factor = Math.Pow(length, roundness);
            current.x += (squared.x - current.x) * factor;
            current.y += (squared.y - current.y) * factor;
        }
    }
}
