using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YawGLAPI
{
    public class Quaternion
    {
        public double x;
        public double y;
        public double z;
        public double w;
        public Quaternion() { }
        public Quaternion(double x, double y, double z, double w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public double toPitchFromYUp()
        {
            double vx = 2 * (x * y + w * y);
            double vy = 2 * (w * x - y * z);
            double vz = 1.0 - 2 * (x * x + y * y);

            return Math.Atan2(vy, Math.Sqrt(vx * vx + vz * vz));
        }

        public double toYawFromYUp()
        {
            return Math.Atan2(2 * (x * y + w * y), 1.0 - 2 * (x * x + y * y));
        }

        public double toRollFromYUp()
        {
            return Math.Atan2(2 * (x * y + w * z), 1.0 - 2 * (x * x + z * z));
        }
    }
}
