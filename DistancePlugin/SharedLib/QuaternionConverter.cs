 
using System;
namespace SharedLib
{
    public class Quaternion
    {
        public double w, x, y, z;

        public Quaternion(double w, double x, double y, double z)
        {
            this.w = w;
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public class EulerAngles
    {
        public double x, y, z;

        public EulerAngles(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public class QuaternionConverter
    {
        #region Intrinsic Rotation
        public static EulerAngles Quat2EulerXYZ(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.x + q.y * q.z);
            double t1 = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);
            double t2 = 2.0 * (q.w * q.y - q.z * q.x);
            double t3 = 2.0 * (q.w * q.z + q.x * q.y);
            double t4 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);

            double EulerX = Math.Atan2(t0, t1);
            double EulerY = Math.Asin(t2);
            double EulerZ = Math.Atan2(t3, t4);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        public static EulerAngles Quat2EulerXZY(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.z + q.x * q.y);
            double t1 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);
            double t2 = 2.0 * (q.w * q.x - q.y * q.z);
            double t3 = 2.0 * (q.w * q.y + q.x * q.z);
            double t4 = 1.0 - 2.0 * (q.x * q.x + q.z * q.z);

            double EulerX = Math.Atan2(t0, t1);
            double EulerY = Math.Atan2(t3, t4);
            double EulerZ = Math.Asin(t2);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        public static EulerAngles Quat2EulerYZX(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.y - q.x * q.z);
            double t1 = 2.0 * (q.w * q.z + q.x * q.y);
            double t2 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);
            double t3 = 2.0 * (q.w * q.x + q.y * q.z);
            double t4 = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);

            double EulerX = Math.Atan2(t1, t2);
            double EulerY = Math.Asin(t0);
            double EulerZ = Math.Atan2(t3, t4);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        public static EulerAngles Quat2EulerYXZ(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.x - q.y * q.z);
            double t1 = 2.0 * (q.w * q.y + q.x * q.z);
            double t2 = 1.0 - 2.0 * (q.x * q.x + q.z * q.z);
            double t3 = 2.0 * (q.w * q.z + q.y * q.x);
            double t4 = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);

            double EulerX = Math.Asin(t1);
            double EulerY = Math.Atan2(t0, t4);
            double EulerZ = Math.Atan2(t3, t2);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        public static EulerAngles Quat2EulerZXY(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.y + q.x * q.z);
            double t1 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);
            double t2 = 2.0 * (q.w * q.z - q.x * q.y);
            double t3 = 2.0 * (q.w * q.x + q.y * q.z);
            double t4 = 1.0 - 2.0 * (q.x * q.x + q.z * q.z);

            double EulerX = Math.Asin(t2);
            double EulerY = Math.Atan2(t3, t4);
            double EulerZ = Math.Atan2(t0, t1);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        public static EulerAngles Quat2EulerZYX(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.z + q.x * q.y);
            double t1 = 1.0 - 2.0 * (q.z * q.z + q.y * q.y);
            double t2 = 2.0 * (q.w * q.y - q.z * q.x);
            double t3 = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);
            double t4 = 1.0 - 2.0 * (q.z * q.z + q.x * q.x);

            double EulerX = Math.Atan2(t0, t1);
            double EulerY = Math.Asin(t2);
            double EulerZ = Math.Atan2(t3, t4);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        #endregion

        #region Extrinsic Rotation

        // ZXZ
        public static EulerAngles Quat2EulerZXZ(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.x - q.y * q.z);
            double t1 = 2.0 * (q.w * q.y + q.x * q.z);
            double t2 = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);
            double t3 = 2.0 * (q.w * q.z + q.x * q.y);
            double t4 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);

            double EulerX = Math.Atan2(t0, t1);
            double EulerY = Math.Acos(t2);
            double EulerZ = Math.Atan2(t3, t4);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        // XYX
        public static EulerAngles Quat2EulerXYX(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.x + q.y * q.z);
            double t1 = 1.0 - 2.0 * (q.x * q.x + q.z * q.z);
            double t2 = 2.0 * (q.w * q.y - q.z * q.x);
            double t3 = 2.0 * (q.w * q.z + q.x * q.y);
            double t4 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);

            double EulerX = Math.Atan2(t3, t4);
            double EulerY = Math.Acos(t1);
            double EulerZ = Math.Atan2(t0, t2);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        // YZY
        public static EulerAngles Quat2EulerYZY(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.y + q.x * q.z);
            double t1 = 1.0 - 2.0 * (q.x * q.x + q.z * q.z);
            double t2 = 2.0 * (q.w * q.z - q.x * q.y);
            double t3 = 2.0 * (q.w * q.x + q.y * q.z);
            double t4 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);

            double EulerX = Math.Atan2(t3, t4);
            double EulerY = Math.Acos(t1);
            double EulerZ = Math.Atan2(t0, t2);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        // XZX
        public static EulerAngles Quat2EulerXZX(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.x - q.y * q.z);
            double t1 = 2.0 * (q.w * q.y + q.x * q.z);
            double t2 = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);
            double t3 = 2.0 * (q.w * q.z + q.x * q.y);
            double t4 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);

            double EulerX = Math.Atan2(t3, t4);
            double EulerY = Math.Acos(t2);
            double EulerZ = Math.Atan2(t0, t1);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        // YXY
        public static EulerAngles Quat2EulerYXY(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.y + q.x * q.z);
            double t1 = 1.0 - 2.0 * (q.x * q.x + q.z * q.z);
            double t2 = 2.0 * (q.w * q.x - q.z * q.y);
            double t3 = 2.0 * (q.w * q.z + q.x * q.y);
            double t4 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);

            double EulerX = Math.Atan2(t0, t1);
            double EulerY = Math.Acos(t4);
            double EulerZ = Math.Atan2(t2, t3);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }

        // ZYZ
        public static EulerAngles Quat2EulerZYZ(Quaternion q)
        {
            double t0 = 2.0 * (q.w * q.z - q.x * q.y);
            double t1 = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);
            double t2 = 2.0 * (q.w * q.x + q.y * q.z);
            double t3 = 2.0 * (q.w * q.y - q.x * q.z);
            double t4 = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);

            double EulerX = Math.Atan2(t2, t3);
            double EulerY = Math.Acos(t1);
            double EulerZ = Math.Atan2(t0, t4);

            return new EulerAngles(EulerX, EulerY, EulerZ);
        }


        #endregion

    }
}      