using System.Runtime.InteropServices;

namespace F1Sharp.Data
{
    /// <summary>
    /// Car telemetry data
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CarTelemetryData
    {
        /// <summary>
        /// Speed in kilometers per hour
        /// </summary>
        public ushort speed;
        /// <summary>
        /// Amount of throttle applied (0..1)
        /// </summary>
        public float throttle;
        /// <summary>
        /// <para>Amount of steering applied (-1..1)</para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Value</term>
        ///         <description>Meaning</description>
        ///     </listheader>
        ///     <item>
        ///         <term>-1</term>
        ///         <description>Full lock left</description>
        ///     </item>
        ///     <item>
        ///         <term>0</term>
        ///         <description>Neutral</description>
        ///     </item>
        ///     <item>
        ///         <term>1</term>
        ///         <description>Full lock right</description>
        ///     </item>
        /// </list>
        /// </summary>
        public float steer;
        /// <summary>
        /// Amount of brake applied (0..1)
        /// </summary>
        public float brake;
        /// <summary>
        /// Amount of clutch applied (0..100)
        /// </summary>
        public byte clutch;
        /// <summary>
        /// <para>Gear selected</para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Value</term>
        ///         <description>Meaning</description>
        ///     </listheader>
        ///     <item>
        ///         <term>-1</term>
        ///         <description>Reverse</description>
        ///     </item>
        ///     <item>
        ///         <term>0</term>
        ///         <description>Neutral</description>
        ///     </item>
        ///     <item>
        ///         <term>1-8</term>
        ///         <description>Gears</description>
        ///     </item>
        /// </list>
        /// </summary>
        public sbyte gear;
        /// <summary>
        /// Engine RPM
        /// </summary>
        public ushort engineRPM;
        /// <summary>
        /// <para>DRS active</para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Value</term>
        ///         <description>Meaning</description>
        ///     </listheader>
        ///     <item>
        ///         <term>0</term>
        ///         <description>Off</description>
        ///     </item>
        ///     <item>
        ///         <term>1</term>
        ///         <description>On</description>
        ///     </item>
        /// </list>
        /// </summary>
        public byte drs;
        /// <summary>
        /// Rev lights indictor (percentage)
        /// </summary>
        public byte revLightsPercent;
        /// <summary>
        /// Rev lights indicator (0-14)
        /// </summary>
        public ushort revLightsBitValue;
        /// <summary>
        /// Brakes temperature in celsius
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public ushort[] brakesTemperature;
        /// <summary>
        /// Tyres surface temperature in celsius
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] tyresSurfaceTemperature;
        /// <summary>
        /// Tyres inner temperature in celsius
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] tyresInnerTemperature;
        /// <summary>
        /// Engine temperature in celsius
        /// </summary>
        public ushort engineTemperature;
        /// <summary>
        /// Tyres pressure (PSI)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] tyresPressure;
        /// <summary>
        /// <para>Driving surface for each wheel</para>
        /// <para>See <see cref="F1Sharp.SurfaceType"/> for surface types.</para>
        /// <para>See <see cref="Wheel"/> for wheel index.</para>
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public SurfaceType[] surfaceType;
    }
}
