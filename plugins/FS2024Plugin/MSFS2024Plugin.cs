using Microsoft.FlightSimulator.SimConnect;
using FS2024Plugin.Properties;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using YawGLAPI;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System;
using System.Reflection.Metadata;
using System.Drawing;
using System.Security.Cryptography;
namespace MSFS2024
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Microsoft Flight Simulator 2024")]
    [ExportMetadata("Version", "3.1")]
    class FS2024Plugin : Game
    {
        private bool isSimRunning = false;          // False is the user in menu, true if the user is in control
        private bool isSimRunningPrevious = false;
        private int simPauseState;
        private float[] targetValues = null;        // Stores the target values to transition to
        private float[] initialValues = null;       // Values at the start of the transition
        private bool isTransitioning = false;       // Indicates if a transition is active
        private float transitionDuration = 1.5f;    // Transition duration in seconds
        private double transitionStartTime;
        private Stopwatch stopwatch = Stopwatch.StartNew();
        private double lastTime = 0.0;
        private double elapsedTime = 0.0;

        private double previous_yaw = 0.0f;
        private double previous_yaw_normalized = 0.0f;
        private double previous_roll_constrained = 0.0f;

        // SimConnect object
        enum DEFINITIONS
        {
            Struct1
        }

        enum DATA_REQUESTS
        {
            REQUEST_1,
        };

        private enum SimEvent
        {
            SIM_START,
            SIM_STOP,
            PAUSE_EX1
        }

        // Pause state flags
        private const int PAUSE_STATE_FLAG_OFF = 0;
        private const int PAUSE_STATE_FLAG_PAUSE = 1;
        private const int PAUSE_STATE_FLAG_ACTIVE_PAUSE = 4;
        private const int PAUSE_STATE_FLAG_SIM_PAUSE = 8;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct Struct1
        {
            // --- Begin variables returned from SimConnect:
            public double latitude;
            public double longitude;
            public double altitude;
            public double pitch;
            public double roll;
            public double heading;
            public double ground_velocity;
            public double center_wheel_rpm;
            public Int64  sim_on_ground;                // strange: can't fetch this variable as an int!?
            public double turn_coordinator_ball;
            public double delta_heading_rate;
            public double elevator_position;
            public double aileron_position;

            public double gForce;
            public double rotationAccelerationBodyX;
            public double rotationAccelerationBodyY;
            public double rotationAccelerationBodyZ;
            public double rotationVelocityBodyX;
            public double rotationVelocityBodyY;
            public double rotationVelocityBodyZ;
            public double accelerationBodyX;
            public double accelerationBodyY;
            public double accelerationBodyZ;
            public double velocityBodyX;
            public double velocityBodyY;
            public double velocityBodyZ;
            public double verticalSpeed;
            public double variometerRate;
            public double totalWeightYawMOI;
            public double planePitchRadians;
            public double planeBankRadians;
            public double planeHeadingDegreesGyro;
            public double planeHeadingDegreesMagnetic;

            public double airspeedTrue;
            public double aircraftWindX;
            public double aircraftWindY;
            public double aircraftWindZ;
            public double relativeWindVelocityBodyX;
            public double relativeWindVelocityBodyY;
            public double relativeWindVelocityBodyZ;

            public double contactPointCompression0;
            public double contactPointCompression1;
            public double contactPointCompression2;
            public double contactPointCompression3;
            public double contactPointCompression4;
            public double contactPointCompression5;
            public int    contactPointIsOnGround0;
            public int    contactPointIsOnGround1;
            public int    contactPointIsOnGround2;
            public int    contactPointIsOnGround3;
            public int    contactPointIsOnGround4;
            public int    contactPointIsOnGround5;

            public double engine_rpm_1;
            public double engine_rpm_2;
            public double engine_rpm_3;
            public double engine_rpm_4;
            public double engineVibration1;
            public double engineVibration2;
            public double engineVibration3;
            public double engineVibration4;
            public double engRotorRpm1;
            public double engRotorRpm2;
            public double engRotorRpm3;
            public double engRotorRpm4;
            public int    engineType;
            public int    engineCount;
            public int    cameraState;        // was previously converting camera state to pause state

            // --- End variables returned from SimConnect (66 currently)

            // --- Begin McFredward's additional computed fields
            public double pitch_multiplier;
            public double roll_normalized;
            public double yaw_normalized;
            public double yaw_normalized_twist;

            // --- Begin CCrim's additional computed fields
            public double roll_dynamic;
            public double pitch_dynamic;
            public double yaw_dynamic;
            public double heave_dynamic;
            public double surge_dynamic;

            public double unused;             // no longer used but keeping placeholder so that old profiles don't get reset by Game Link
        };

        // Field index offsets for input variables that are not zeroed when pasued:
        const int kHeading = 5;
        const int kCameraState = 66;
        const int kYawNormalized = 69;
        const int kYawNormalizedTwist = 70;
        const int kLastSimVariable = 70;

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;
        public string PROCESS_NAME => "FlightSimulator2024";
        public int STEAM_ID => 2537590;
        public string AUTHOR => "YawVR";
        public bool PATCH_AVAILABLE => true;

        public string Description => Resources.description;
        public Stream Logo => GetStream("logo.png");
        public Stream SmallLogo => GetStream("recent.png");
        public Stream Background => GetStream("wide.png");

        private Thread readThread;

        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;
        private volatile bool stop = false;

        public IntPtr Handle { get; private set; }

        public List<Profile_Component> DefaultProfile()
        {
            return dispatcher.JsonToComponents(Resources.defProfile);
        }
        public LedEffect DefaultLED()
        {
            return dispatcher.JsonToLED(Resources.defProfile);
        }

        public void Exit()
        {
            stop = true;
        }

        // Returns array of available input variable names.
        public string[] GetInputData()
        {
            Type f = typeof(Struct1);
            FieldInfo[] fields = f.GetFields();

            string[] inputs = new string[fields.Length];

            for (int i = 0; i < fields.Length; i++)
            {
                inputs[i] = fields[i].Name;
            }

            return inputs;
        }
        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }
        public void Init()
        {
            try
            {
                stop = false;
                readThread = new Thread(new ThreadStart(ReadFunction));
                readThread.Start();
            }
            catch (Exception)
            {
                dispatcher.ShowNotification(NotificationType.ERROR, "Cannot load Simconnect. Make sure to click patch first!");
            }
        }

        private SimConnect ConnectToSimconnect()
        {
            try
            {
                Debug.WriteLine("Trying to conect to MSFS..");
                SimConnect simconnect = new SimConnect("Managed Data Request", this.Handle, WM_USER_SIMCONNECT, null, 0);

                // define a data structure
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.00f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.00f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.00f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GROUND VELOCITY", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CENTER WHEEL RPM", "rpm", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "SIM ON GROUND", "bool", SIMCONNECT_DATATYPE.INT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "TURN COORDINATOR BALL", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "DELTA HEADING RATE", "radians per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ELEVATOR POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AILERON POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "G FORCE", "G force", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY X", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Y", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Z", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY X", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Y", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Z", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ACCELERATION BODY X", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ACCELERATION BODY Y", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ACCELERATION BODY Z", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VELOCITY BODY X", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VELOCITY BODY Y", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VELOCITY BODY Z", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VERTICAL SPEED", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VARIOMETER RATE", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "TOTAL WEIGHT YAW MOI", "slugs feet squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE PITCH DEGREES", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE BANK DEGREES", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES GYRO", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES MAGNETIC", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AIRSPEED TRUE", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AIRCRAFT WIND X", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AIRCRAFT WIND Y", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AIRCRAFT WIND Z", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "RELATIVE WIND VELOCITY BODY X", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "RELATIVE WIND VELOCITY BODY Y", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "RELATIVE WIND VELOCITY BODY Z", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT COMPRESSION:0", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT COMPRESSION:1", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT COMPRESSION:2", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT COMPRESSION:3", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT COMPRESSION:4", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT COMPRESSION:5", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT IS ON GROUND:0", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT IS ON GROUND:1", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT IS ON GROUND:2", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT IS ON GROUND:3", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT IS ON GROUND:4", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CONTACT POINT IS ON GROUND:5", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:1", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:2", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:3", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:4", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:1", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:2", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:3", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:4", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:1", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:2", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:3", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:4", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENGINE TYPE", "enum", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "NUMBER OF ENGINES", "number", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CAMERA STATE", "enum", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simconnect.RegisterDataDefineStruct<Struct1>(DEFINITIONS.Struct1);

                // catch a simobject data request
                simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(simconnect_OnRecvSimobjectDataBytype);

                // Subscribe to SIM_START and SIM_END to determine if the simulation is paused:
                simconnect.SubscribeToSystemEvent(SimEvent.SIM_START, "SimStart");
                simconnect.SubscribeToSystemEvent(SimEvent.SIM_STOP, "SimStop");
                simconnect.SubscribeToSystemEvent(SimEvent.PAUSE_EX1, "Pause_EX1");
                // Hook up event handler
                simconnect.OnRecvEvent += SimConnect_OnRecvEvent;

                return simconnect;
            }
            catch (Exception)
            {
                return null;
                //dispatcher.DialogShow("Could not connect to MSFS", DIALOG_TYPE.INFO);
                //dispatcher.ExitGame();
            }

        }

        public void SimConnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            if ((SimEvent)data.uEventID == SimEvent.SIM_START)
            {
                simPauseState = PAUSE_STATE_FLAG_OFF;
                isSimRunning = true;
                Debug.WriteLine("Simulation started.");
            }
            else if ((SimEvent)data.uEventID == SimEvent.SIM_STOP)
            {
                simPauseState = PAUSE_STATE_FLAG_PAUSE;
                isSimRunning = false;
                Debug.WriteLine("Simulation stopped.");
            }
            else if ((SimEvent)data.uEventID == SimEvent.PAUSE_EX1)
            {
                simPauseState = (int)data.dwData;
                isSimRunning = (simPauseState == PAUSE_STATE_FLAG_OFF);
                HandlePauseState(simPauseState);
            }
        }

        // Handle detailed pause state
        private void HandlePauseState(int pauseState)
        {
            if (pauseState == PAUSE_STATE_FLAG_OFF)              // No pause
            {
                Debug.WriteLine("Simulation resumed.");
            }
            else if (pauseState == PAUSE_STATE_FLAG_PAUSE)      // Full pause
            {
                Debug.WriteLine("Simulation fully paused.");
            }
            else if (pauseState == PAUSE_STATE_FLAG_ACTIVE_PAUSE)    // Active pause
            {
                Debug.WriteLine("Simulation in active pause.");
            }
            else if (pauseState == PAUSE_STATE_FLAG_SIM_PAUSE)   // Sim paused, but other elements still running
            {
                Debug.WriteLine("Simulation paused, but traffic and multiplayer are running.");
            }
            else
            {
                Debug.WriteLine($"Unknown pause state: {pauseState}");
            }
        }

        private void CheckCameraStateForPause(ref Struct1 s1)
        {
            // The MSFS pause flags don't report when we're in the main menu or still loading the flight.
            // Some folks have worked out that you can use the CAMERA STATE variable to detect other times that a flight is not active.
            if (s1.cameraState < 11)
            {
                isSimRunning = (simPauseState == PAUSE_STATE_FLAG_OFF);
            }
            else if (isSimRunning)
            {
                Debug.WriteLine("Simulation in non-flight camera state (treating as paused).");
                isSimRunning = false;
            }
        }

        // Useful constants
        const double kGravity = 32.174;         // feet/sec^2 to match accelerationBody units
        const double kConvertToDegrees = 180.0 / Math.PI;
        const double kConvertToRadians = Math.PI / 180.0;

        private double NormalizeAngle(double angle)
        {
            // Bring the angle within the range [-180, 180]
            while (angle > 180.0f) angle -= 360.0f;
            while (angle < -180.0f) angle += 360.0f;
            return angle;
        }

        private double ConstrainAngle(double angle, double pivotAngle = 90)
        {
            // Linearly constrain roll/bank angle around 90 degree pivot:
            if (Math.Abs(angle) > 90)
            {
                angle = Math.Sign(angle) * (180 - Math.Abs(angle));
            }

            // Alternate methods supporting arbitrary angles...
            // Constrain roll angle with sinusoidal or linear easing back to 0 after pivot point:
/*          if (Math.Abs(angle) > pivotAngle)
            {
                double excessAngle = Math.Abs(angle) - pivotAngle;

                // Apply cosine easing to the excess angle and restore the sign:
                //angle = Math.Sign(angle) * pivotAngle * Math.Cos(Math.PI * excessAngle / (2 * (180 - pivotAngle)));

                // Linear version:
                //angle = Math.Sign(angle) * (pivotAngle - excessAngle * (pivotAngle / (180 - pivotAngle)));
            }
*/
            return angle;
        }


        // Persistent states
        private double smoothedLateralG = 0.0f;
        private double sustainedVerticalG = 0.0f;
        private double rateLimitPitchResult = 0.0f;
        private double rateLimitRollCue = 0.0f;
        private double previousLateralResidualG = 0.0f;
        private double oscillationLevel = 0.0f;
        private double fastHeaveG = 0.0f;
        private double filteredSurge = 0.0f;
        private double surgeTau = 0.0f;

        // Smoothly transitions a value toward a target using exponential smoothing.
        // <prev> is current/previous state value and <input> is value to move towards.
        private static double LowPass(double prev, double input, double dt, double tau)
        {
            // 1. Validate basic finiteness to prevent NaN propagation
            if (!double.IsFinite(prev)) return double.IsFinite(input) ? input : 0.0;
            if (!double.IsFinite(input)) return prev;

            // 2. Handle time edge cases: If no time passed or tau is invalid, 
            // we either stay put or jump to input immediately.
            if (!(dt > 0.0)) return prev;
            if (!(tau > 0.0)) return input;

            // 3. Cap dt to prevent huge leaps during frame spikes (e.g., 50ms cap)
            // and calculate the smoothing factor.
            double alpha = 1.0 - Math.Exp(-Math.Min(dt, 0.05) / tau);

            // 4. Calculate next step. Using 'prev + alpha * (target - prev)' 
            // is numerically more stable for small alpha values.
            double nextValue = prev + alpha * (input - prev);

            // 5. Final safety check for the result
            return double.IsFinite(nextValue) ? nextValue : input;
        }
        private static double RateLimit(double prevValue, double targetValue, double dt, double maxRateValuePerSec)
        {
            if (!(dt > 0.0) || !double.IsFinite(dt)) return prevValue;
            dt = Math.Min(dt, 0.05);    // 50 ms cap

            double maxStep = maxRateValuePerSec * dt;
            double delta = targetValue - prevValue;

            if (delta > maxStep) delta = maxStep;
            else if (delta < -maxStep) delta = -maxStep;

            return prevValue + delta;
        }
        private static double SoftKneeCurve(double x, double knee, double limit)
        {
            double absX = Math.Abs(x);

            // Pass through unchanged below the knee
            if (absX <= knee)
                return x;

            // Map excess above the knee into the remaining headroom with an atan soft-cap
            double headroom = limit - knee;
            double excess = absX - knee;
            return Math.Sign(x) * (knee + headroom * (2.0 / Math.PI) * Math.Atan(excess / headroom));
        }

        private static double SmoothStep(double x)
        {
            return x * x * (3.0 - 2.0 * x);
        }

        private void CalcExtraSimVariables(ref Struct1 s1)
        {
            double planePitchSine = Math.Sin(s1.planePitchRadians);
            double planeBankCosine = Math.Cos(s1.planeBankRadians);
            double planePitchCosine = Math.Cos(s1.planePitchRadians);
            double roll_constrained = ConstrainAngle(s1.roll);

            // --- Begin McFredward's additional computed fields (pitch_multiplier, roll_normalized, yaw_normalized, yaw_normalized_twist)

            // Reduce roll angle by pitch (going to 0 at 90 degree pitch, reduced by about 30% at 45 degrees):
            s1.pitch_multiplier = planePitchCosine;
            s1.roll_normalized = s1.pitch_multiplier * roll_constrained;

            // Fix tipping point problem with raw yaw values:
            double yaw = s1.heading;       // using heading for yaw_normalized
            double yaw_delta = (s1.pitch_multiplier * NormalizeAngle(previous_yaw - yaw));
            s1.yaw_normalized = NormalizeAngle(previous_yaw_normalized - yaw_delta);
            s1.yaw_normalized_twist = NormalizeAngle(s1.yaw_normalized - (yaw_delta + (1 - s1.pitch_multiplier) * NormalizeAngle(previous_roll_constrained - roll_constrained)));

            previous_yaw = yaw;
            previous_yaw_normalized = s1.yaw_normalized;
            previous_roll_constrained = roll_constrained;

            // --- End McFredward's additional computed fields


            // --- Begin CCrim's additional computed fields

            /*  My Yaw 3 MSFS profile makes use of these dynamic force acceleration variables to try to simulate the forces felt in a real aircraft
                and not just reflect the attitude of the aircraft.

                Axis Orientations:                                                    Variable Orientations:       left    right
                    MSFS x axis: lateral         positive is right                            accelerationBodyX:   -       +
                    MSFS y axis: vertical        positive is up                           turn_coordinator_ball:   -       +
                    MSFS z axis: longitudinal    positive is forward                  roll/bank, incidence_beta:   +       -
                    Yaw 3 rig pitch: positive is forward/down
                    Yaw 3 rig roll:  positive is left

                Roll attitude causes unintuitive behavior due to how forces coordinate in an aircraft.
                In a normal coordinated turn, gravity, lift and centrifugal forces balance and combine in such a way to direct
                an increased g force directly perpindicular to the seat. When the forces are balanced, the pilot/ passenger feels no sideways force
                from either the bank angle or centrifugal force of the turn. Instead they feel a force pressing them into their seat, proportional
                to the strength of the turn (determined by airspeed and turn radius).

                During uncoordinated turns, the opposing forces are unbalanced, and you feel a force directed to either the inside or outside
                of the turn depending on the orientation of the plane, with the nose pointing either into or away from the inside of the turning circle.
                In a skid, the nose is pointed to the inside the turn, you feel a centrifugal pull to the outside of the turn and the turn coordinator
                ball moves to the outside. In a sideslip, the nose is pointed to the outside of the turn, you feel a force pulling to the inside
                of the turn and the turn coordinator ball moves to the inside.

                In aircraft that can maintain a static roll position, if you don't apply rudder to maintain altitude then the aircraft will be in free fall
                (can't simulate this) but when you do apply rudder you will once again feel the counterforce of lift to your side.

                A motion sim rig can thus simulate these unbalanced forces by tilting to the inside or outside of the turn direction along the roll axis
                in proportion to the amount of force but scaled down as very little tilt is actually needed to be convincing when your view also
                mimics the banking motion via VR goggles or an enclosed cockpit.

                Aircraft feature a device called a turn/slip indicator or turn coordinator ball to help pilots maintain coordinated turns and perform
                standard rate turns (like two minutes for 360 degrees). We can't use the TURN INDICATOR BALL variable directly because it simulates a
                mechanical device with dampening (too slow) and oscillation (it'll swing from side to side in fast maneuvers).

                To simulate force from slip or skid, we want to tilt in the direction of the ball or opposite incidence_beta's sign.
                Turning to left example: accelerationBodyX goes negative.  When it's coordinated, no tilt.
                Without left rudder or with too much right rudder, coordinator ball goes to left, we want to tilt left.
                With too much rudder left, ball goes to the right, we want to tilt right to simulate centrifugal force.
            */
            
            bool isRotaryAircraft = (s1.engineType == 3) || (s1.engRotorRpm1 > 0);

            // PITCH_DYNAMIC -- Simulates the shift of gravity from the pitch attitude as well as sustained G loads.
            // We want the gravity shift due to pitch attitude to be linear near level flight without feeling too abrupt or violent compared to the visual simulation
            // while also allowing for use of more of our available pitch range for extreme pitch angles.  This result is then enhanced or contradicted by
            // any g loading force from diving, climbing or banking. The G load calculation is able to overcome the pitch cue when appropriate
            // (like pulling out of a dive or climb) but without using up our available range too quickly.
            const double kGLoadScale = 0.40;        // was 0.20-0.50 depending on different curves
            const double kNegativeGCueScale = 0.40;
            const double kNegativeGFadeRange = 1.5;

            // Note that accelerationBodyY includes any coordinated down force from a banked turn.
            // INVESTIGATE: Does it?  Or does it just include the offset of gravity from attitude?
            // It also goes from 0 in level flight to +1G at 90 degree bank and +2G at 180 degrees (inverted) and to -2G pushing the nose up while inverted.
            double verticalAccelG = s1.accelerationBodyY / kGravity;      // convert to G's

            // For aircraft sitting idle on the ground, clamp down noisy sim variables that can cause violent shaking:
            // (This was happening with at least one helicopter in certain weather conditions.)
            if (s1.sim_on_ground != 0 && Math.Abs(s1.ground_velocity) < 0.03)
                verticalAccelG = Math.Clamp(verticalAccelG, -0.03, 0.03);

            // Estimate sustained G loading via a low pass filter of verticalAccelG:
            sustainedVerticalG = LowPass(sustainedVerticalG, verticalAccelG, elapsedTime, 0.60);    // slow filter for capturing sustained forces (banked turns, climbs, dives)

            // Calculate our deviation from the attitude baseline:
            double gravityOffset = 1.0 - (planeBankCosine * planePitchCosine);                      // ranges from 0 to 2 in upright to inverted flight
            double baselineDeviation = sustainedVerticalG - gravityOffset;

            // Calculate a pitch cue based on attitude such that small angles don't feel too violent and large angles use more of our range:
            const double kPitchAttitudeScaleLow = 1.2;
            const double kPitchAttitudeScaleHigh = 1.6;
            double pitchGain = kPitchAttitudeScaleLow + (kPitchAttitudeScaleHigh - kPitchAttitudeScaleLow) * Math.Abs(planePitchSine);
            double pitchAttitudeCue = planePitchSine * pitchGain;

            // If we're inverted, calculate an inverted cue (negative G's, hanging from straps):
            if (planeBankCosine < 0.0)
            {
                double negativeGFadeOut = Math.Clamp(1.0 - (baselineDeviation / kNegativeGFadeRange), 0.0, 1.5);    // fade out based on pulling/pushing G's
                negativeGFadeOut *= negativeGFadeOut;                   // Squaring for a smooth nonlinear fade

                // Inverted cue is scaled by bank and pitch attitude:
                pitchAttitudeCue += -planeBankCosine * kNegativeGCueScale * Math.Abs(planePitchCosine) * negativeGFadeOut;
            }

            // Calculate a dynamic G load from our baseline deviation:
            double dynamicGLoad = baselineDeviation * kGLoadScale;     // g-load is independent of the force of gravity from attitude

            // Combine and let dynamic G load potentially override the pitch cue:
            s1.pitch_dynamic = pitchAttitudeCue - dynamicGLoad;

            // Compress and constrain the high end of forward pitch (positive) while allowing backward pitch to be unconstrained:
            if (s1.pitch_dynamic > 0.0)
                s1.pitch_dynamic = SoftKneeCurve(s1.pitch_dynamic, 1.5, 2.4);
            
            // We need this rate limit to keep fast acrobatic movements from feeling too violent. (But lower limits delay move too much.)
            rateLimitPitchResult = RateLimit(rateLimitPitchResult, s1.pitch_dynamic, elapsedTime, 1.5);
            s1.pitch_dynamic = rateLimitPitchResult;




            // HEAVE_DYNAMIC (bumps/turbulence)
            // In the absence of a heave axis, calculate dynamic cues from vertical acceleration telemetry so that we feel vertical
            // bumps and turbulence via our pitch axis. Any "bump" is the difference between what's happening now and the trend.  It's also a natural washout.
            double bumpHeaveCue = (verticalAccelG - sustainedVerticalG);        // raw minus smooth gives bumps (forward pitch, feels rougher than backward pitch?)

            // Fast impact cue to retain follow-up wheel/gear contact detail:
            fastHeaveG = LowPass(fastHeaveG, verticalAccelG, elapsedTime, 0.08);
            double impactCue = verticalAccelG - fastHeaveG;
            impactCue = Math.Max(0, Math.Abs(impactCue) - 0.03) * Math.Sign(impactCue);     // apply linear coring (deadzone) to avoid sign change inflection
            bumpHeaveCue = (0.90 * bumpHeaveCue) + (0.10 * impactCue);          // use only a little fast impact detail

            s1.heave_dynamic = bumpHeaveCue * 0.12;     // heave up is returned as negative, scaled in profile for Yaw 3's pitch axis (was 0.15, was 0.10)




            // SURGE_DYNAMIC (longitudinal acceleration: thrust/braking/air drag)
            // In the absence of a surge axis, we'll apply a dynamic cue from longitudinal acceleration telemetry to the pitch axis.
            // Convert to G's and remove the included gravity effect coming from pitch angle:
            double rawSurge = - (s1.accelerationBodyZ / kGravity) + planePitchSine;

            // surge_dynamic tends to pile up pitch feedback in twitchy, acrobatic planes so watch for pitch activity to suppress:
            const double kPitchRateStart = 0.5;
            const double kPitchRateFull = 1.0;
            double pitchActivity = Math.Clamp((Math.Abs(s1.rotationVelocityBodyX) - kPitchRateStart) / (kPitchRateFull - kPitchRateStart), 0.0, 1.0);

            // Convert pitch activity into a target low pass filter time:
            double targetSurgeTau = 0.2 + (0.8 * pitchActivity);        // slow to 1.0 second during high pitch activity, was base of 0.25 but reduced to improve surge impact

            // Increase tau immediately so early contaminated surge gets damped,
            // but make tau fall slowly so the filter doesn't suddenly catch up:
            if (targetSurgeTau > surgeTau) surgeTau = targetSurgeTau;
            else surgeTau = LowPass(surgeTau, targetSurgeTau, elapsedTime, 0.50);

            filteredSurge = LowPass(filteredSurge, rawSurge, elapsedTime, surgeTau);
            s1.surge_dynamic = filteredSurge;                           // forward surge is returned as negative, scaled in profile for Yaw 3's pitch axis




            //  ROLL_DYNAMIC -- Calculate the lateral component of uncoordinated force pulling on the pilot.  (See detailed explanation above.)
            double rollAttitudeComponent = Math.Sin(s1.planeBankRadians);
            double lateralGravityComponent = rollAttitudeComponent * planePitchCosine;  // negative to the right
            double lateralAccelG = -s1.accelerationBodyX / kGravity;        // convert to G's

            // In a coordinated turn, lateralAccelG and lateralGravityComponent should oppose and cancel each other out,
            // leaving an approximation of uncoordinated lateral forces:
            smoothedLateralG = LowPass(smoothedLateralG, lateralAccelG, elapsedTime, 0.20);        // fast enough for bumps but suppress noisy telemetry (esp. some helicopters)
            double targetLateralG = (lateralGravityComponent - smoothedLateralG);
            // INVESTIGATE: Sideslip even with full rudder is completely missing in some aircraft like King Air.  Bad telemetry data?

            // In rotary aircraft (helicopters), adjust the amount of estimated lateral force based on forward velocity:
            if (isRotaryAircraft)
            {
                // Some helicopters produce very heavy oscillating telemetry noise. Try to detect this and keep it from shaking the roll axis excessively: 
                double lateralResidualG = lateralAccelG - smoothedLateralG;
                bool signFlip = Math.Sign(lateralResidualG) != Math.Sign(previousLateralResidualG)
                                && Math.Abs(previousLateralResidualG) > 0.05
                                && Math.Abs(lateralResidualG) > 0.05;
                previousLateralResidualG = lateralResidualG;
                if (signFlip) oscillationLevel = Math.Min(1.0, oscillationLevel + 0.15);
                oscillationLevel *= Math.Exp(-elapsedTime / 0.30);

                // Forward airflow increases confidence that bank-related force coordination is meaningful.
                // At low forward speeds, let aircraft attitude dominate over any lateral acceleration (and coordinated force cancellation):
                double forwardFlow = Math.Clamp(Math.Abs(s1.relativeWindVelocityBodyZ) / 200.0, 0.0, 1.0);
                double accelWeight = 0.15 + (0.85 * forwardFlow);
                accelWeight *= 1.0 - (1.0 * oscillationLevel);     // also reduce telemetry oscillation noise
                targetLateralG = (lateralGravityComponent - (smoothedLateralG * accelWeight));

                // Apply a power function to emphasize small bank angles:
                double x = Math.Abs(targetLateralG);
                targetLateralG = Math.Sign(targetLateralG) * (2.0 * Math.Pow(x, 0.8)) / (1.0 + 0.9 * Math.Pow(x, 1.2));     // .06->.20, .10->.24, .30->.63, .50->0.83, 1.0->1.053
                rateLimitRollCue = 0.0;
            }
            // For fixed wing aircraft, calculate an additional component from roll activity and large roll attitudes:
            else
            {
                // Estimate how much roll activity is occuring from our longitudinal rotational velocity.
                // (Since the Yaw 3 has a curved roll axis, we'll generate the desired vestibular roll acceleration cues by actually moving to a new roll attitude.)
                const double kRollRateStart = 0.2;
                const double kRollRateFull = 1.2;
                double rollActivity = Math.Clamp((Math.Abs(s1.rotationVelocityBodyZ) - kRollRateStart) / (kRollRateFull - kRollRateStart), 0.0, 1.0);

                // Create a bank attitude term tempered by pitch angle and squared to emphasize gravity shift at high bank angles:
                double highBankAttitude = Math.Abs(rollAttitudeComponent) * Math.Abs(rollAttitudeComponent);
                // Reduce this term to zero during hard banked turn or otherwise pulling vertical G's:
                double excessVerticalG = Math.Abs(sustainedVerticalG) - Math.Abs(lateralGravityComponent);      // must remove up to 1G from roll attitude to isolate vertical G's
                highBankAttitude *= (1.0 - Math.Clamp(excessVerticalG * 0.3, 0.0, 0.75));

                // Let the amount of roll activity determine how quickly we change roll attitude:
                const double kRollCueRateSlow = 0.5;
                const double kRollCueRateFast = 3.0;
                double rollCueRateLimit = kRollCueRateSlow + (kRollCueRateFast - kRollCueRateSlow) * rollActivity;

                double attitudeWeight = Math.Max(0.25 * rollActivity, 0.75 * highBankAttitude);
                rateLimitRollCue = RateLimit(rateLimitRollCue, rollAttitudeComponent * attitudeWeight, elapsedTime, rollCueRateLimit);
            }

            s1.roll_dynamic = targetLateralG + rateLimitRollCue;




            // YAW_DYNAMIC
            // Calculate an incremental value to add to the yaw axis per frame.
            // For rotary aircraft, just use rotation velocity directly: 
            if (isRotaryAircraft)
            {
                s1.yaw_dynamic = s1.rotationVelocityBodyY / 10.0;
            }
            else
            {
                // For fixed wing aircraft, we'll use rotation acceleration and scale it based on the aircraft's "yaw motion of inertia" since we can't
                // get a measure of the distance between the pilot position and the center of rotation.
                // It's a big range of values: Cessna 172 MOI=3300, King Air C90 MOI=100K, Airbus 320 MOI=2.83 million.
                // Result is scaled by 1.0 in profile and interpreted as degrees added incrementally per frame to the yaw axis 
                double yawScaleFactor = 90.0 + (910.0 / Math.Pow(Math.Max(s1.totalWeightYawMOI, 1.0), 0.30));     // results: Cessna -> 170, King Air -> 119, A320 -> 101
                s1.yaw_dynamic = s1.rotationAccelerationBodyY / yawScaleFactor;

                // Boost only very small cues: 0.10 -> 0.184, 0.20 -> 0.228, 0.30 -> 0.309
                s1.yaw_dynamic *= (1.0 + 1.25 / (1.0 + Math.Pow(Math.Abs(s1.yaw_dynamic) / 0.12, 4.0)));

                // While on the ground, also include some ongoing yaw based on rotational velocity:
                if (s1.sim_on_ground != 0)
                    s1.yaw_dynamic += s1.rotationVelocityBodyY / 10.0;
            }

            // --- End CCrim's additional computed fields
        }

        private void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                    double currentTime = stopwatch.Elapsed.TotalSeconds;
                    elapsedTime = currentTime - lastTime;
                    lastTime = currentTime;

                    // Retrieve the data as the specified structure
                    Struct1 s1 = (Struct1)data.dwData[0];
                    CheckCameraStateForPause(ref s1);
                    CalcExtraSimVariables(ref s1);
                    FieldInfo[] fields = typeof(Struct1).GetFields();

                    // Initialize array if null
                    if (targetValues == null)
                    {
                        targetValues = new float[fields.Length];
                    }

                    // Suppress telemetry while paused/in the menus - still need a way to detect initial loading screen phase though.
                    if (isSimRunning != isSimRunningPrevious)
                    {
                        isSimRunningPrevious = isSimRunning;
                        // Start transitioning to actual data if resumed
                        isTransitioning = true;
                        transitionStartTime = stopwatch.Elapsed.TotalSeconds;
                        initialValues = (float[])targetValues.Clone();
                    }

                    float transitionFactor = 1.0f;
                    if (isTransitioning)
                    {
                        double transitionElapsed = currentTime - transitionStartTime;

                        // Calculate interpolation factor (0.0 to 1.0)
                        transitionFactor = (float)Math.Min(transitionElapsed / transitionDuration, 1.0);
                        isTransitioning = (transitionFactor < 1.0f);
                    }

                    // Load target values if sim is running, otherwise zero everything (w/exceptions):
                    for (int i = 0; i < fields.Length; i++)
                    {
                        // Use current values if sim is running or for heading-based variables (to avoid rotating motion rig):
                        if (isSimRunning || i == kHeading || i == kYawNormalized || i == kYawNormalizedTwist || i == kCameraState)
                        {
                            float value = Convert.ToSingle(fields[i].GetValue(s1));
                            targetValues[i] = float.IsNaN(value) ? 0.0f : (float)Math.Round(value, 3);
                        }
                        // Otherwise go to zero:
                        else
                        {
                            targetValues[i] = 0.0f;
                        }

                        if (isTransitioning)
                        {
                            // Perform a linear interpolation between initialValues and targetValues:
                            targetValues[i] = initialValues[i] + (targetValues[i] - initialValues[i]) * transitionFactor;
                        }
                        controller.SetInput(i, targetValues[i]);
                    }
                    break;

                default:
                    Debug.WriteLine("Unknown request ID: " + data.dwRequestID);
                    break;
            }
        }

        private void ReadFunction()
        {
            SimConnect simconnect;
            do
            {
                Thread.Sleep(500);
                simconnect = ConnectToSimconnect();
            } while (simconnect == null && !stop);

            try
            {
                dispatcher.ShowNotification(NotificationType.INFO, "Connected to Simconnect");
                while (!stop)
                {
                    // The following call returns identical information to:
                    // simconnect.RequestDataOnSimObject(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE);

                    simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                    simconnect.ReceiveMessage();
                    Thread.Sleep(1);
                }

            }
            catch (COMException)
            {
                dispatcher.ExitGame();
            }



        }


        public void PatchGame()
        {
            try
            {
                string tempFile = Path.GetTempFileName() + ".dll";
                string tempFile2 = Path.GetTempFileName() + ".dll";

                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile("http://yaw.one/gameengine/Plugins/Microsoft_Flight_Simulator_XX/Microsoft.FlightSimulator.SimConnect.dll", tempFile);
                    wc.DownloadFile("http://yaw.one/gameengine/Plugins/Microsoft_Flight_Simulator_XX/SimConnect.dll", tempFile2);
                }

                var entryPointLocation = Assembly.GetEntryAssembly().Location;
                File.Copy(tempFile, Path.GetDirectoryName(entryPointLocation) + "/Gameplugins/Microsoft.FlightSimulator.SimConnect.dll", true);
                File.Copy(tempFile2, Path.GetDirectoryName(entryPointLocation) + "/SimConnect.dll", true);


                dispatcher.RestartApp(false);
            }
            catch (IOException e)
            {
                e.ToString();
                dispatcher.DialogShow("Already patched!", DIALOG_TYPE.INFO);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                dispatcher.DialogShow("No permission to install plugin. Restart with admin privileges?", DIALOG_TYPE.QUESTION, delegate {

                    dispatcher.RestartApp(false);
                });
            }
        }


        public Dictionary<string, ParameterInfo[]> GetFeatures()
        {
            return null;
        }
        Stream GetStream(string resourceName)
        {
            var assembly = GetType().Assembly;
            var rr = assembly.GetManifestResourceNames();
            string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";
            return assembly.GetManifestResourceStream(fullResourceName);
        }

        public Type GetConfigBody()
        {
            return null;
        }
    }
}
