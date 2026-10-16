using System;
using System.Runtime.InteropServices;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// Cart sample sent by the BloodRush shadPS4 build over UDP (52 bytes, little-endian).
    /// Fields are marshaled; public properties are the plugin inputs (in declaration order).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct RushOfBloodTelemetryData
    {
        private float speed;
        private float posX, posY, posZ;
        private float quatX, quatY, quatZ, quatW;
        private float gForceX, gForceY, gForceZ;
        private uint flags;
        private uint frame;

        private const float Rad2Deg = 180f / MathF.PI;

        public float Speed => speed;

        public float Yaw
        {
            get
            {
                // Y-up, Z-forward: yaw about Y, pitch about X, roll about Z (Y-X-Z order).
                var siny = 2f * (quatW * quatY + quatX * quatZ);
                var cosy = 1f - 2f * (quatX * quatX + quatY * quatY);
                return MathF.Atan2(siny, cosy) * Rad2Deg;
            }
        }

        public float Pitch
        {
            get
            {
                var sinp = Math.Clamp(2f * (quatW * quatX - quatY * quatZ), -1f, 1f);
                return MathF.Asin(sinp) * Rad2Deg;
            }
        }

        public float Roll
        {
            get
            {
                var sinr = 2f * (quatW * quatZ + quatX * quatY);
                var cosr = 1f - 2f * (quatX * quatX + quatZ * quatZ);
                return MathF.Atan2(sinr, cosr) * Rad2Deg;
            }
        }

        public float GForceLateral => gForceX;
        public float GForceVertical => gForceY;
        public float GForceLongitudinal => gForceZ;

        public float PosX => posX;
        public float PosY => posY;
        public float PosZ => posZ;

        public bool InPlay => (flags & 1) != 0;
        public bool Braking => (flags & 2) != 0;
        public bool Paused => (flags & 4) != 0;
        public bool VR => (flags & 8) != 0;

        public uint Frame => frame;
    }
}
