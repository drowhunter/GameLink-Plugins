using System.Numerics;
using System.Runtime.InteropServices;

namespace YawVR_Game_Engine.Plugin
{
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DistanceTelemetryData
    {
        public int PacketId;
        public float KPH;
        public float Mass;
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float Sway;
        public Vector3 Velocity;
        public Vector3 Accel;
        public Inputs Inputs;
        public bool Finished;
        public bool AllWheelsOnGround;
        public bool isActiveAndEnabled;
        public bool Grav;
        public float AngularDrag;
        public Tire TireFL;
        public Tire TireFR;
        public Tire TireBL;
        public Tire TireBR;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Tire
    {
        public bool Contact;
        public float Position;
        public float Suspension;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Inputs
    {
        public float Gas;
        public float Brake;
        public float Steer;
        public bool Boost;
        public bool Grip;
        public bool Wings;
    }
    
}
