using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WhitewaterPlugin
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TelemetryPacket
    {
        public float Speed;
        public float Rpm;
        public float MaxRpm;
        public int Gear;
        public float Roll;
        public float Pitch;
        public float Yaw;
        public float LatVel;
        public float LatAccel;
        public float VertAccel;
        public float LongAccel;
        public float SuspensionFL;
        public float SuspensionFR;
        public float SuspensionRL;
        public float SuspensionRR;

        public int WheelTerraionFL;
        public int WheelTerraionFR;
        public int WheelTerraionRL;
        public int WheelTerraionRR;
      
    }
}
