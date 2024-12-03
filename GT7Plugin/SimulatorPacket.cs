using System.Diagnostics;
using System.Net;
using System.Numerics;



namespace GT7Plugin
{
    /// <summary>
    /// Packet from the GT Engine Simulation.
    /// </summary>
    public class SimulatorPacket
    {
        public float PositionX;
        public float PositionY;
        public float PositionZ;

        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;

        public float RotationX;
        public float RotationY;
        public float RotationZ;


        public float RelativeOrientationToNorth;

        public float AngularVelocityX;
        public float AngularVelocityY;
        public float AngularVelocityZ;

        public float BodyHeight;
        public float EngineRPM;
        public float GasLevel;
        public float GasCapacity;
        public float MetersPerSecond;
        public float TurboBoost;
        public float OilPressure;
        public float WaterTemperature;
        public float OilTemperature;
        public float TireFL_SurfaceTemperature;
        public float TireFR_SurfaceTemperature;
        public float TireRL_SurfaceTemperature;
        public float TireRR_SurfaceTemperature;
        public int PacketId;
        public short LapCount;
        public short LapsInRace;
        //public TimeSpan BestLapTime;
        //public TimeSpan LastLapTime;
       //public TimeSpan TimeOfDayProgression;
        public short PreRaceStartPositionOrQualiPos;
        public short NumCarsAtPreRace;
        public short MinAlertRPM;
        public short MaxAlertRPM;
        public short CalculatedMaxSpeed;
        public SimulatorFlags Flags;
        public byte CurrentGear;
        public byte SuggestedGear;
        public byte Throttle;
        public byte Brake;
        public byte Empty_0x93;
        public float RoadPlaneX;
        public float RoadPlaneY;
        public float RoadPlaneZ;
        public float RoadPlaneDistance;
        public float WheelFL_RevPerSecond;
        public float WheelFR_RevPerSecond;
        public float WheelRL_RevPerSecond;
        public float WheelRR_RevPerSecond;
        public float TireFL_TireRadius;
        public float TireFR_TireRadius;
        public float TireRL_TireRadius;
        public float TireRR_TireRadius;
        public float TireFL_SusHeight;
        public float TireFR_SusHeight;
        public float TireRL_SusHeight;
        public float TireRR_SusHeight;
        public float ClutchPedal;
        public float ClutchEngagement;
        public float RPMFromClutchToGearbox;
        public float TransmissionTopSpeed;
        //public float[] GearRatios = new float[7];
        public int CarCode;

        public void Read(Span<byte> data)
        {
            SpanReader sr = new SpanReader(data);
            int magic = sr.ReadInt32();

            PositionX = sr.ReadSingle();
            PositionY = sr.ReadSingle();
            PositionZ = sr.ReadSingle();

            VelocityX = sr.ReadSingle();
            VelocityY = sr.ReadSingle();
            VelocityZ = sr.ReadSingle();

            RotationX = sr.ReadSingle();
            RotationY = sr.ReadSingle();
            RotationZ = sr.ReadSingle();

            RelativeOrientationToNorth = sr.ReadSingle();

            AngularVelocityX = sr.ReadSingle();
            AngularVelocityY = sr.ReadSingle();
            AngularVelocityZ = sr.ReadSingle();

            BodyHeight = sr.ReadSingle();
            EngineRPM = sr.ReadSingle();
            sr.Position += sizeof(int); // Skip IV
            GasLevel = sr.ReadSingle();
            GasCapacity = sr.ReadSingle();
            MetersPerSecond = sr.ReadSingle();
            TurboBoost = sr.ReadSingle();
            OilPressure = sr.ReadSingle();
            WaterTemperature = sr.ReadSingle();
            OilTemperature = sr.ReadSingle();
            TireFL_SurfaceTemperature = sr.ReadSingle();
            TireFR_SurfaceTemperature = sr.ReadSingle();
            TireRL_SurfaceTemperature = sr.ReadSingle();
            TireRR_SurfaceTemperature = sr.ReadSingle();
            PacketId = sr.ReadInt32();
            LapCount = sr.ReadInt16();
            LapsInRace = sr.ReadInt16();

            // These are laptimes, we dont care
            sr.ReadInt32();
            sr.ReadInt32();
            sr.ReadInt32();
            ////////////////////////////////////

            PreRaceStartPositionOrQualiPos = sr.ReadInt16();
            NumCarsAtPreRace = sr.ReadInt16();
            MinAlertRPM = sr.ReadInt16();
            MaxAlertRPM = sr.ReadInt16();
            CalculatedMaxSpeed = sr.ReadInt16();
            Flags = (SimulatorFlags)sr.ReadInt16();

            int bits = sr.ReadByte();
            CurrentGear = (byte)(bits & 0b1111); // 4 bits
            SuggestedGear = (byte)(bits >> 4); // Also 4 bits

            Throttle = sr.ReadByte();
            Brake = sr.ReadByte();
            Empty_0x93 = sr.ReadByte();

            RoadPlaneX = sr.ReadSingle();
            RoadPlaneY = sr.ReadSingle();
            RoadPlaneZ = sr.ReadSingle();

            RoadPlaneDistance = sr.ReadSingle();

            WheelFL_RevPerSecond = sr.ReadSingle();
            WheelFR_RevPerSecond = sr.ReadSingle();
            WheelRL_RevPerSecond = sr.ReadSingle();
            WheelRR_RevPerSecond = sr.ReadSingle();
            TireFL_TireRadius = sr.ReadSingle();
            TireFR_TireRadius = sr.ReadSingle();
            TireRL_TireRadius = sr.ReadSingle();
            TireRR_TireRadius = sr.ReadSingle();
            TireFL_SusHeight = sr.ReadSingle();
            TireFR_SusHeight = sr.ReadSingle();
            TireRL_SusHeight = sr.ReadSingle();
            TireRR_SusHeight = sr.ReadSingle();

            sr.Position += sizeof(int) * 8; // Seems to be reserved - server does not set that

            ClutchPedal = sr.ReadSingle();
            ClutchEngagement = sr.ReadSingle();
            RPMFromClutchToGearbox = sr.ReadSingle();

            TransmissionTopSpeed = sr.ReadSingle();

            // Always read as a fixed 7 gears
            for (var i = 0; i < 7; i++) sr.ReadSingle();

            // Normally this one is not set at all. The game memcpy's the gear ratios without bound checking
            // The LC500 which has 10 gears even overrides the car code 😂
            float empty_or_gearRatio8 = sr.ReadSingle();

            CarCode = sr.ReadInt32();
        }

    }

    /// <summary>
    /// Flags/States of the simulation.
    /// </summary>
    [Flags]
    public enum SimulatorFlags : short
    {
        None = 0,

        /// <summary>
        /// The car is on the track or paddock, with data available.
        /// </summary>
        CarOnTrack = 1 << 0,

        /// <summary>
        /// The game's simulation is paused. 
        /// Note: The simulation will not be paused while in the pause menu in online modes.
        /// </summary>
        Paused = 1 << 1,

        /// <summary>
        /// Track or car is currently being loaded onto the track.
        /// </summary>
        LoadingOrProcessing = 1 << 2,

        /// <summary>
        /// Needs more investigation
        /// </summary>
        InGear = 1 << 3,

        /// <summary>
        /// Current car has a Turbo.
        /// </summary>
        HasTurbo = 1 << 4,

        /// <summary>
        /// Rev Limiting is active.
        /// </summary>
        RevLimiterBlinkAlertActive = 1 << 5,

        /// <summary>
        /// Hand Brake is active.
        /// </summary>
        HandBrakeActive = 1 << 6,

        /// <summary>
        /// Lights are active.
        /// </summary>
        LightsActive = 1 << 7,

        /// <summary>
        /// High Beams are turned on.
        /// </summary>
        HighBeamActive = 1 << 8,

        /// <summary>
        /// Low Beams are turned on.
        /// </summary>
        LowBeamActive = 1 << 9,

        /// <summary>
        /// ASM is active.
        /// </summary>
        ASMActive = 1 << 10,

        /// <summary>
        /// Traction Control is active.
        /// </summary>
        TCSActive = 1 << 11,
    }
}