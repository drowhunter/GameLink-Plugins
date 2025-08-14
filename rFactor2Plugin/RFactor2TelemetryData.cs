using System.Numerics;
using System.Runtime.InteropServices;

namespace RFactor2Plugin
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2Vec3
    {
        public double x, y, z;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Telemetry
    {
        public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
        public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

        public int mBytesUpdatedHint;             // How many bytes of the structure were written during the last update.
                                                  // 0 means unknown (whole buffer should be considered as updated).

        public int mNumVehicles;                  // current number of vehicles
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 128)]
        public rF2VehicleTelemetry[] mVehicles;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2VehicleTelemetry
    {
        // Time
        public int mID;                      // slot ID (note that it can be re-used in multiplayer after someone leaves)
        public double mDeltaTime;             // time since last update (seconds)
        public double mElapsedTime;           // game session time
        public int mLapNumber;               // current lap number
        public double mLapStartET;            // time this lap was started
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mVehicleName;         // current vehicle name
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mTrackName;           // current track name

        // Position and derivatives
        public rF2Vec3 mPos;                  // world position in meters
        public rF2Vec3 mLocalVel;             // velocity (meters/sec) in local vehicle coordinates
        public rF2Vec3 mLocalAccel;           // acceleration (meters/sec^2) in local vehicle coordinates

        // Orientation and derivatives
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public rF2Vec3[] mOri;               // rows of orientation matrix (use TelemQuat conversions if desired), also converts local
                                             // vehicle vectors into world X, Y, or Z using dot product of rows 0, 1, or 2 respectively
        public rF2Vec3 mLocalRot;             // rotation (radians/sec) in local vehicle coordinates
        public rF2Vec3 mLocalRotAccel;        // rotational acceleration (radians/sec^2) in local vehicle coordinates

        // Vehicle status
        public int mGear;                    // -1=reverse, 0=neutral, 1+=forward gears
        public double mEngineRPM;             // engine RPM
        public double mEngineWaterTemp;       // Celsius
        public double mEngineOilTemp;         // Celsius
        public double mClutchRPM;             // clutch RPM

        // Driver input
        public double mUnfilteredThrottle;    // ranges  0.0-1.0
        public double mUnfilteredBrake;       // ranges  0.0-1.0
        public double mUnfilteredSteering;    // ranges -1.0-1.0 (left to right)
        public double mUnfilteredClutch;      // ranges  0.0-1.0

        // Filtered input (various adjustments for rev or speed limiting, TC, ABS?, speed sensitive steering, clutch work for semi-automatic shifting, etc.)
        public double mFilteredThrottle;      // ranges  0.0-1.0
        public double mFilteredBrake;         // ranges  0.0-1.0
        public double mFilteredSteering;      // ranges -1.0-1.0 (left to right)
        public double mFilteredClutch;        // ranges  0.0-1.0

        // Misc
        public double mSteeringShaftTorque;   // torque around steering shaft (used to be mSteeringArmForce, but that is not necessarily accurate for feedback purposes)
        public double mFront3rdDeflection;    // deflection at front 3rd spring
        public double mRear3rdDeflection;     // deflection at rear 3rd spring

        // Aerodynamics
        public double mFrontWingHeight;       // front wing height
        public double mFrontRideHeight;       // front ride height
        public double mRearRideHeight;        // rear ride height
        public double mDrag;                  // drag
        public double mFrontDownforce;        // front downforce
        public double mRearDownforce;         // rear downforce

        // State/damage info
        public double mFuel;                  // amount of fuel (liters)
        public double mEngineMaxRPM;          // rev limit
        public byte mScheduledStops; // number of scheduled pitstops
        public byte mOverheating;            // whether overheating icon is shown
        public byte mDetached;               // whether any parts (besides wheels) have been detached
        public byte mHeadlights;             // whether headlights are on
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] mDentSeverity;// dent severity at 8 locations around the car (0=none, 1=some, 2=more)
        public double mLastImpactET;          // time of last impact
        public double mLastImpactMagnitude;   // magnitude of last impact
        public rF2Vec3 mLastImpactPos;        // location of last impact

        // Expanded
        public double mEngineTorque;          // current engine torque (including additive torque) (used to be mEngineTq, but there's little reason to abbreviate it)
        public int mCurrentSector;           // the current sector (zero-based) with the pitlane stored in the sign bit (example: entering pits from third sector gives 0x80000002)
        public byte mSpeedLimiter;   // whether speed limiter is on
        public byte mMaxGears;       // maximum forward gears
        public byte mFrontTireCompoundIndex;   // index within brand
        public byte mRearTireCompoundIndex;    // index within brand
        public double mFuelCapacity;          // capacity in liters
        public byte mFrontFlapActivated;       // whether front flap is activated
        public byte mRearFlapActivated;        // whether rear flap is activated
        public byte mRearFlapLegalStatus;      // 0=disallowed, 1=criteria detected but not allowed quite yet, 2=allowed
        public byte mIgnitionStarter;          // 0=off 1=ignition 2=ignition+starter

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 18)]
        public byte[] mFrontTireCompoundName;         // name of front tire compound
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 18)]
        public byte[] mRearTireCompoundName;          // name of rear tire compound

        public byte mSpeedLimiterAvailable;    // whether speed limiter is available
        public byte mAntiStallActivated;       // whether (hard) anti-stall is activated
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] mUnused;                //
        public float mVisualSteeringWheelRange;         // the *visual* steering wheel range

        public double mRearBrakeBias;                   // fraction of brakes on rear
        public double mTurboBoostPressure;              // current turbo boost pressure if available
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mPhysicsToGraphicsOffset;       // offset from static CG to graphical center
        public float mPhysicalSteeringWheelRange;       // the *physical* steering wheel range


        public double mBatteryChargeFraction;                        // Battery charge as fraction [0.0-1.0]

        // electric boost motor
        public double mElectricBoostMotorTorque;                     // current torque of boost motor (can be negative when in regenerating mode)
        public double mElectricBoostMotorRPM;                        // current rpm of boost motor
        public double mElectricBoostMotorTemperature;                // current temperature of boost motor
        public double mElectricBoostWaterTemperature;                // current water temperature of boost motor cooler if present (0 otherwise)
        public byte mElectricBoostMotorState;                        // 0=unavailable 1=inactive, 2=propulsion, 3=regeneration

        // Future use
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 111)]
        public byte[] mExpansion;           // for future use (note that the slot ID has been moved to mID above)

        // keeping this at the end of the structure to make it easier to replace in future versions
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
        public rF2Wheel[] mWheels;                      // wheel info (front left, front right, rear left, rear right)
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Wheel
    {
        public double mSuspensionDeflection;  // meters
        public double mRideHeight;            // meters
        public double mSuspForce;             // pushrod load in Newtons
        public double mBrakeTemp;             // Celsius
        public double mBrakePressure;         // currently 0.0-1.0, depending on driver input and brake balance; will convert to true brake pressure (kPa) in future

        public double mRotation;              // radians/sec
        public double mLateralPatchVel;       // lateral velocity at contact patch
        public double mLongitudinalPatchVel;  // longitudinal velocity at contact patch
        public double mLateralGroundVel;      // lateral velocity at contact patch
        public double mLongitudinalGroundVel; // longitudinal velocity at contact patch
        public double mCamber;                // radians (positive is left for left-side wheels, right for right-side wheels)
        public double mLateralForce;          // Newtons
        public double mLongitudinalForce;     // Newtons
        public double mTireLoad;              // Newtons

        public double mGripFract;             // an approximation of what fraction of the contact patch is sliding
        public double mPressure;              // kPa (tire pressure)
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] mTemperature;         // Kelvin (subtract 273.15 to get Celsius), left/center/right (not to be confused with inside/center/outside!)
        public double mWear;                  // wear (0.0-1.0, fraction of maximum) ... this is not necessarily proportional with grip loss
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] mTerrainName;           // the material prefixes from the TDF file
        public byte mSurfaceType;             // 0=dry, 1=wet, 2=grass, 3=dirt, 4=gravel, 5=rumblestrip, 6=special
        public byte mFlat;                    // whether tire is flat
        public byte mDetached;                // whether wheel is detached
        public byte mStaticUndeflectedRadius; // tire radius in centimeters

        public double mVerticalTireDeflection;// how much is tire deflected from its (speed-sensitive) radius
        public double mWheelYLocation;        // wheel's y location relative to vehicle y location
        public double mToe;                   // current toe angle w.r.t. the vehicle

        public double mTireCarcassTemperature;       // rough average of temperature samples from carcass (Kelvin)
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] mTireInnerLayerTemperature;  // rough average of temperature samples from innermost layer of rubber (before carcass) (Kelvin)

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 24)]
        byte[] mExpansion;                    // for future use
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RFactor2TelemetryData
    {
        public bool GamePaused;
        public bool IsRacing;
        public float KPH;

        public float Pitch;
        public float Yaw;
        public float Roll;

        public Vector3 AngularVelocity;

        public float sway;

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
        public float x;
        public float y;
        public float z;
        public float w;
    }

}
