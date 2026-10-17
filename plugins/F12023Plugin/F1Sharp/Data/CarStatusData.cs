using System.Runtime.InteropServices;

namespace F1Sharp.Data
{
    /// <summary>
    /// Car status data
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CarStatusData
    {
        /// <summary>
        /// <para>Traction control level</para>
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
        ///         <description>Medium</description>
        ///     </item>
        ///     <item>
        ///         <term>2</term>
        ///         <description>Full</description>
        ///     </item>
        /// </list>
        /// </summary>
        public byte tractionControl;
        /// <summary>
        /// <para>ABS status</para>
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
        public byte antiLockBrakes;
        /// <summary>
        /// <para>Current fuel mix</para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Value</term>
        ///         <description>Meaning</description>
        ///     </listheader>
        ///     <item>
        ///         <term>0</term>
        ///         <description>Lean</description>
        ///     </item>
        ///     <item>
        ///         <term>1</term>
        ///         <description>Standard</description>
        ///     </item>
        ///     <item>
        ///         <term>2</term>
        ///         <description>Rich</description>
        ///     </item>
        ///     <item>
        ///         <term>3</term>
        ///         <description>Max</description>
        ///     </item>
        /// </list>
        /// </summary>
        public byte fuelMix;
        /// <summary>
        /// Front brake bias (percentage)
        /// </summary>
        public byte frontBrakeBias;
        /// <summary>
        /// <para>Pit limiter status</para>
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
        public byte pitLimiterStatus;
        /// <summary>
        /// Current fuel mass
        /// </summary>
        public float fuelInTank;
        /// <summary>
        /// Fuel capacity
        /// </summary>
        public float fuelCapacity;
        /// <summary>
        /// Fuel remaining in terms of laps (value on MFD)
        /// </summary>
        public float fuelRemainingLaps;
        /// <summary>
        /// Car's max RPM, point of rev limiter
        /// </summary>
        public ushort maxRPM;
        /// <summary>
        /// Car's idle RPM
        /// </summary>
        public ushort idleRPM;
        /// <summary>
        /// Maximum number of gears
        /// </summary>
        public byte maxGears;
        /// <summary>
        /// <para>Whether the DRS is allowed</para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Value</term>
        ///         <description>Meaning</description>
        ///     </listheader>
        ///     <item>
        ///         <term>0</term>
        ///         <description>Not allowed</description>
        ///     </item>
        ///     <item>
        ///         <term>1</term>
        ///         <description>Allowed</description>
        ///     </item>
        /// </list>
        /// </summary>
        public byte drsAllowed;
        /// <summary>
        /// <para>Distance where DRS will be available</para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Value</term>
        ///         <description>Meaning</description>
        ///     </listheader>
        ///     <item>
        ///         <term>0</term>
        ///         <description>Not available</description>
        ///     </item>
        ///     <item>
        ///         <term>Greater than 0</term>
        ///         <description>Distance in meters</description>
        ///     </item>
        /// </list>
        /// </summary>
        public ushort drsActivationDistance;
        /// <summary>
        /// Tyre compound
        /// </summary>
        public TyreCompound actualTyreCompound;
        /// <summary>
        /// Visual tyre compound
        /// </summary>
        public VisualTyreCompound visualTyreCompound;
        /// <summary>
        /// Age in laps of current set of tyres
        /// </summary>
        public byte tyresAgeLaps;
        /// <summary>
        /// Flags shown to the car
        /// </summary>
        public ZoneFlag vehicleFiaFlags;
        /// <summary>
        /// Engine power output of ICE in watts
        /// </summary>
        public float enginePowerICE;
        /// <summary>
        /// Engine power output of MGU-K in watts
        /// </summary>
        public float enginePowerMGUK;
        /// <summary>
        /// ERS energy store in joules
        /// </summary>
        public float ersStoreEnergy;
        /// <summary>
        /// <para>ERS deployment mode</para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Value</term>
        ///         <description>Meaning</description>
        ///     </listheader>
        ///     <item>
        ///         <term>0</term>
        ///         <description>None</description>
        ///     </item>
        ///     <item>
        ///         <term>1</term>
        ///         <description>Medium</description>
        ///     </item>
        ///     <item>
        ///         <term>2</term>
        ///         <description>Hotlap</description>
        ///     </item>
        ///     <item>
        ///         <term>3</term>
        ///         <description>Overtake</description>
        ///     </item>
        /// </list>
        /// </summary>
        public byte ersDeployMode;
        /// <summary>
        /// Energy harvested this lap by the MGU-K
        /// </summary>
        public float ersHarvestedThisLapMGUK;
        /// <summary>
        /// Energy harvested this lap by the MGU-H
        /// </summary>
        public float ersHarvestedThisLapMGUH;
        /// <summary>
        /// ERS energy deployed this lap
        /// </summary>
        public float ersDeployedThisLap;
        /// <summary>
        /// Whether the car is paused in a network game
        /// </summary>
        public byte networkPaused;
    }
}
