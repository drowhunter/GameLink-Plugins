using SharedLib.TelemetryHelper;

using System;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// G-Rebels telemetry (SimRacingStudio "api" packet v102, 236 bytes, little-endian).
    /// Sent by the Unreal "Motion Platform Linker" plugin to UDP 33001.
    ///
    /// 0x00 char[4]  "api\0"
    /// 0x04 uint32   version (102)
    /// 0x08 char[50] game name
    /// 0x3A char[50] vehicle name
    /// 0x6C char[50] location
    /// 0x9E 2 bytes  padding
    /// 0xA0 float    speed, rpm, maxRpm / int gear
    /// 0xB0 float    pitch, roll, yaw (degrees)
    /// 0xBC float    lateral velocity, lateral/vertical/longitudinal acceleration    
    /// Field order here defines the input indexes used by Default.yawglprofile.
    /// </summary>
    internal struct Telemetry
    {
        public float Pitch;
        public float Yaw;
        public float Roll;

        /// <summary>
        /// Derived in the plugin from the change in rotation between packets (deg/s).
        /// Unity axes: X = right (pitch), Y = up (yaw), Z = forward (roll).
        /// </summary>
        public float PitchVelocity;
        public float YawVelocity;
        public float RollVelocity;

        public float Speed;
        public float SpeedSideways;

        public float Sway;
        public float Heave;
        public float Surge;

        /// <summary>Derived in the plugin: |Heave|, for rumble on bumps and landings.</summary>
        public float Bump;

        /// <summary>Derived in the plugin: |Sway|, for rumble while sliding.</summary>
        public float Slide;

        //public float ScaledPitch;

        public float ScaledRoll;
    }

    internal class TelemetryConverter : IByteConverter<Telemetry>
    {
        private const int PacketSize = 0xEC;

        public Telemetry FromBytes(byte[] data)
        {
            if (data.Length < PacketSize || data[0] != 'a' || data[1] != 'p' || data[2] != 'i')
                return default;

            return new Telemetry
            {
                Speed = BitConverter.ToSingle(data, 0xA0),
                SpeedSideways = BitConverter.ToSingle(data, 0xBC),

                Pitch = BitConverter.ToSingle(data, 0xB0),
                Roll = BitConverter.ToSingle(data, 0xB4),
                Yaw = BitConverter.ToSingle(data, 0xB8),

                Sway = BitConverter.ToSingle(data, 0xC0),
                Heave = BitConverter.ToSingle(data, 0xC4),
                Surge = BitConverter.ToSingle(data, 0xC8),

            };
        }

        public byte[] ToBytes(Telemetry data) => Array.Empty<byte>();
    }
}
