using System;
using System.Linq;

namespace GravitreX
{
    internal class TelemetryPacket
    {
        public float roll = 0; //Read from UDP!
        public float rollSpeed = 0; //Read from UDP!
        public float rollAcceleration = 0; //Read from UDP!
        public float XSpeed = 0; //Read from UDP!
        public float YSpeed = 0; //Read from UDP!
        public float XAcceleration = 0; //Read from UDP!
        public float YAcceleration = 0; //Read from UDP!
        public float XAcceleration_noGravity = 0; //Read from UDP!
        public float YAcceleration_noGravity = 0; //Read from UDP!

        public static TelemetryPacket FromBytes(byte[] bytes)
        {
            var packet = new TelemetryPacket()
            {
                roll = BitConverter.ToSingle(bytes, 0),
                rollSpeed = BitConverter.ToSingle(bytes, 4),
                rollAcceleration = BitConverter.ToSingle(bytes, 8),
                XSpeed = BitConverter.ToSingle(bytes, 12),
                YSpeed = BitConverter.ToSingle(bytes, 16),
                XAcceleration = BitConverter.ToSingle(bytes, 20),
                YAcceleration = BitConverter.ToSingle(bytes, 24),
                XAcceleration_noGravity = BitConverter.ToSingle(bytes, 28),
                YAcceleration_noGravity = BitConverter.ToSingle(bytes, 32),
            };
            return packet;
        }

        public byte[] ToBytes()
        {
            return BitConverter.GetBytes(roll)
                .Concat(BitConverter.GetBytes(rollSpeed))
                .Concat(BitConverter.GetBytes(rollAcceleration))
                .Concat(BitConverter.GetBytes(XSpeed))
                .Concat(BitConverter.GetBytes(YSpeed))
                .Concat(BitConverter.GetBytes(XAcceleration))
                .Concat(BitConverter.GetBytes(YAcceleration))
                .Concat(BitConverter.GetBytes(XAcceleration_noGravity))
                .Concat(BitConverter.GetBytes(YAcceleration_noGravity)).ToArray();
        }
    }
}
