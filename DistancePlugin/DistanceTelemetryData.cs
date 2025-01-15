﻿using System.Numerics;
using System.Runtime.InteropServices;

namespace YawVR_Game_Engine.Plugin
{

    [StructLayout(LayoutKind.Sequential)]
    internal struct DistanceTelemetryData
    {
        public bool GamePaused;
        public bool IsRacing;
        public float KPH;

        public float Pitch;
        public float Yaw;
        public float Roll;

        public Vector3 AngularVelocity;

        public float cForce;

        public Vector3 Velocity;
        public Vector3 Accel;

        public bool Boost;
        public bool Grip;
        public bool WingsOpen;

        public bool IsCarEnabled;
        public bool IsCarIsActive;
        public bool IsCarDestroyed;
        public bool AllWheelsOnGround;
        public bool IsGrav;

        public float TireFL;
        public float TireFR;
        public float TireBL;
        public float TireBR;

        public Quat Rot;
    }

    internal struct Quat
    {
        public float w;
        public float x;
        public float y;
        public float z;
    }

}
