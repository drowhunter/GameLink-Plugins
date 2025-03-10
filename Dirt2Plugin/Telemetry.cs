using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Dirt2Plugin
{
    internal struct TelemetryOut
    {
        public float Speed;
        public float RPM;
        public float Steer;
        public float Force_long;
        public float Force_lat;
        public float Pitch;
        public float Roll;
        public float Yaw;
        public float suspen_pos_bl;
        public float suspen_pos_br;
        public float suspen_pos_fl;
        public float suspen_pos_fr;
        public float suspen_vel_bl;
        public float suspen_vel_br;
        public float suspen_vel_fl;
        public float suspen_vel_fr;
        public Vector3 Velocity;
    }

    internal struct TelemetryIn
    {
        public float Speed;
        public float RPM;
        public float Steer;
        public float Force_long;
        public float Force_lat;
        public float Pitch;
        public float Roll;
        public float Yaw;
        public float suspen_pos_bl;
        public float suspen_pos_br;
        public float suspen_pos_fl;
        public float suspen_pos_fr;
        public float suspen_vel_bl;
        public float suspen_vel_br;
        public float suspen_vel_fl;
        public float suspen_vel_fr;
        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;
    }
}
