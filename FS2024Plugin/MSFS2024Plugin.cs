using Microsoft.FlightSimulator.SimConnect;
using FS2024Plugin.Properties;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using YawGLAPI;
namespace MSFS2024
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Microsoft Flight Simulator 2024")]
    [ExportMetadata("Version", "1.0")]
    class FS2024Plugin : Game
    {
        private bool isSimRunning = false;          // False is the user in menu, true if the user is in control
        private bool isSimRunningPrevious = false;
        private float[] targetValues = null;        // Stores the target values to transition to
        private float[] initialValues = null;       // Values at the start of the transition
        private bool isTransitioning = false;       // Indicates if a transition is active
        private float transitionDuration = 1.5f;    // Transition duration in seconds
        private DateTime transitionStartTime;

        private double previous_yaw = 0.0f;
        private double previous_yaw_immersive = 0.0f;
        private double previous_roll_immersive_temp = 0.0f;

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
            // this is how you declare a fixed size string
            public double latitude;
            public double longitude;
            public double altitude;
            public double pitch;
            public double roll;
            public double heading;
            public double vel_x;
            public double vel_y;
            public double vel_z;
            public double velocity;
            public double engine_rpm_1;
            public double engine_rpm_2;
            public double engine_rpm_3;
            public double engine_rpm_4;
            public double relative_wind_acc_body_x;
            public double relative_wind_acc_body_y;
            public double relative_wind_acc_body_z;
            public double rotation_acc_body_x;
            public double rotation_acc_body_y;
            public double rotation_acc_body_z;
            public double aircraft_wind_x;
            public double aircraft_wind_y;
            public double aircraft_wind_z;
            public double center_wheel_rpm;
            public double gear_hydraulic_pressure;
            public double turn_coordinator_ball;
            public double elevator_position;
            public double aileron_position;

            public double ContactPointCompression0;
            public double ContactPointCompression1;
            public double ContactPointCompression2;
            public double ContactPointCompression3;
            public double ContactPointCompression4;
            public double ContactPointCompression5;
            public int contactPointIsOnGround0;
            public int contactPointIsOnGround1;
            public int contactPointIsOnGround2;
            public int contactPointIsOnGround3;
            public int contactPointIsOnGround4;
            public int contactPointIsOnGround5;
            public double engineVibration1;
            public double engineVibration2;
            public double engineVibration3;
            public double engineVibration4;

            public double gForce;
            public double accelerationBodyX;
            public double accelerationBodyY;
            public double accelerationBodyZ;
            public double planeBankDegrees;             // duplicate of heading
            public double planeHeadingDegreesGyro;
            public double planeHeadingDegreesMagnetic;
            public double planeHeadingDegreesTrue;      // duplicate of heading and planeBankDegrees
            public double planePitchDegrees;            // duplicate of pitch
            public double rotationAccelerationBodyX;    // duplicate of rotation_acc_body_x
            public double rotationAccelerationBodyY;    // duplicate of rotation_acc_body_y
            public double rotationAccelerationBodyZ;    // duplicate of rotation_acc_body_z
            public double rotationVelocityBodyX;        // duplicate of vel_x
            public double rotationVelocityBodyY;        // duplicate of vel_y
            public double rotationVelocityBodyZ;        // duplicate of vel_z
            public double velocityBodyX;
            public double velocityBodyY;
            public double velocityBodyZ;
            public double verticalSpeed;
            public int stallWarning;
            public double variometerRate;
            public double engRotorRpm1;
            public double engRotorRpm2;
            public double engRotorRpm3;
            public double engRotorRpm4;

            // --- Begin McFredward's additional computed fields (not returned from SimConnect):
            public double pitch_multiplier;
            public double roll_immersive;
            public double yaw_immersive;
            public double yaw_immersive_twist;
            // --- End McFredward's additional computed fields
        };

        const int kHeading = 5;
        // Indexes defined for extra fields (based on original 68 fields in Struct1):
        const int kPitchMultiplier = 69;
        const int kRollImmersive = 70;
        const int kYawImmersive = 71;
        const int kYawImmersiveTwist = 72;

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

        //WIND GENERATOR
        float t = 0;
        float periodScale = 1f;
        Random r = new Random();
        private IMainFormDispatcher dispatcher = null;
        private IProfileManager controller = null;
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
                SimConnect c;
                stop = false;
                readThread = new Thread(new ThreadStart(ReadFunction));
                readThread.Start();
            }
            catch (Exception ex)
            {
                dispatcher.ShowNotification(NotificationType.ERROR, "Cannot load Simconnect. Make sure to click patch first!");
            }
        }

        private SimConnect ConnectToSimconnect()
        {
            try
            {
                Debug.WriteLine("Trying to conect to MSFS..");
                SimConnect simconnect = null;
                simconnect = new SimConnect("Managed Data Request", this.Handle, WM_USER_SIMCONNECT, null, 0);

                // define a data structure
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.00f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.00f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.00f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY X", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Y", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Z", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GROUND VELOCITY", "Knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:1", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:2", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:3", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GENERAL ENG RPM:4", "RPM", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "RELATIVE WIND VELOCITY BODY X", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "RELATIVE WIND VELOCITY BODY Y", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "RELATIVE WIND VELOCITY BODY Z", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY X", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Y", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Z", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AIRCRAFT WIND X", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AIRCRAFT WIND Y", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AIRCRAFT WIND Z", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "CENTER WHEEL RPM", "rpm", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GEAR HYDRAULIC PRESSURE", "Pound force per square foot", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "TURN COORDINATOR BALL", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ELEVATOR POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "AILERON POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

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
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:1", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:2", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:3", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG VIBRATION:4", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "G FORCE", "G force", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ACCELERATION BODY X", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ACCELERATION BODY Y", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ACCELERATION BODY Z", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);                                  // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES GYRO", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES MAGNETIC", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);                          // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);                                 // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY X", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);     // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Y", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);     // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Z", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);     // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY X", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);                    // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Y", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);                    // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Z", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);                    // duplicate
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VELOCITY BODY X", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VELOCITY BODY Y", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VELOCITY BODY Z", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VERTICAL SPEED", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "STALL WARNING", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "VARIOMETER RATE", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:1", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:2", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:3", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ENG ROTOR RPM:4", "percent scaler 16k", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

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
            catch (Exception ex)
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
                isSimRunning = true;
                Debug.WriteLine("Simulation started.");
            }
            else if ((SimEvent)data.uEventID == SimEvent.SIM_STOP)
            {
                isSimRunning = false;
                Debug.WriteLine("Simulation stopped.");
            }
            else if ((SimEvent)data.uEventID == SimEvent.PAUSE_EX1)
            {
                HandlePauseState((int)data.dwData);
            }
        }

        // Handle detailed pause state
        private void HandlePauseState(int pauseState)
        {
            if (pauseState == PAUSE_STATE_FLAG_OFF)
            {
                isSimRunning = true; // No pause
                Debug.WriteLine("Simulation resumed.");
            }
            else if (pauseState == PAUSE_STATE_FLAG_PAUSE)
            {
                isSimRunning = false; // Full pause
                Debug.WriteLine("Simulation fully paused.");
            }
            else if (pauseState == PAUSE_STATE_FLAG_ACTIVE_PAUSE)
            {
                isSimRunning = true; // Active pause - hold motion simulator in position
                Debug.WriteLine("Simulation in active pause.");
            }
            else if (pauseState == PAUSE_STATE_FLAG_SIM_PAUSE)
            {
                isSimRunning = false; // Sim paused, but other elements still running
                Debug.WriteLine("Simulation paused, but traffic and multiplayer are running.");
            }
            else
            {
                Debug.WriteLine($"Unknown pause state: {pauseState}");
            }
        }

        private double NormalizeAngle(double angle)
        {
            // Bring the angle within the range [-180, 180]
            while (angle > 180.0f) angle -= 360.0f;
            while (angle < -180.0f) angle += 360.0f;
            return angle;
        }

        private void CalcExtraSimVariables(ref Struct1 s1)
        {
            // --- Begin McFredward's additional computed fields (pitch_multiplier, roll_immersive, yaw_immersive, yaw_immersive_twist)
            // Fix tipping point problem with the raw roll and yaw values:
            double yaw = s1.heading;       // using heading for yaw
            double roll_immersive_temp;
            if (s1.roll < -90)
            {
                roll_immersive_temp = -180 - s1.roll;
            }
            else if (s1.roll > 90)
            {
                roll_immersive_temp = 180 - s1.roll;
            }
            else
            {
                roll_immersive_temp = s1.roll;
            }

            s1.pitch_multiplier = isSimRunning ? Math.Cos(s1.pitch * Math.PI / 180.0) : 0.0f;
            s1.roll_immersive = s1.pitch_multiplier * roll_immersive_temp;
            double yaw_delta = (s1.pitch_multiplier * NormalizeAngle(previous_yaw - yaw));
            s1.yaw_immersive = NormalizeAngle(previous_yaw_immersive - yaw_delta);
            s1.yaw_immersive_twist = NormalizeAngle(s1.yaw_immersive - (yaw_delta + (1 - s1.pitch_multiplier) * NormalizeAngle(previous_roll_immersive_temp - roll_immersive_temp)));

            previous_yaw = yaw;
            previous_yaw_immersive = s1.yaw_immersive;
            previous_roll_immersive_temp = roll_immersive_temp;
            // --- End McFredward's additional computed fields
        }

        private void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                    // Retrieve the data as the specified structure
                    Struct1 s1 = (Struct1)data.dwData[0];
                    CalcExtraSimVariables(ref s1);
                    FieldInfo[] fields = typeof(Struct1).GetFields();

                    // Initialize array if null
                    if (targetValues == null)
                    {
                        targetValues = new float[fields.Length];
                    }

                    // Suppress telemtry during loading screens -> The altitude is higher than 53819 ín this false data!
                    isSimRunning = isSimRunning && s1.altitude < 53819f;
                    if (isSimRunning != isSimRunningPrevious)
                    {
                        isSimRunningPrevious = isSimRunning;
                        // Start transitioning to actual data if resumed
                        isTransitioning = true;
                        transitionStartTime = DateTime.Now;
                        initialValues = (float[])targetValues.Clone();
                    }

                    float transitionFactor = 1.0f;
                    if (isTransitioning)
                    {
                        DateTime currentTime = DateTime.Now;
                        float elapsedTime = (float)(currentTime - transitionStartTime).TotalSeconds;

                        // Calculate interpolation factor (0.0 to 1.0)
                        transitionFactor = Math.Min(elapsedTime / transitionDuration, 1.0f);
                        isTransitioning = (transitionFactor < 1.0f);
                    }

                    // Load target values if sim is running, otherwise zero everything (w/exceptions):
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (isSimRunning || i == kHeading || i == kYawImmersive || i == kYawImmersiveTwist)      // always maintain heading and added yaw variables, even if sim is not running
                        {
                            float value = Convert.ToSingle(fields[i].GetValue(s1));
                            targetValues[i] = float.IsNaN(value) ? 0.0f : (float)Math.Round(value, 3);      // ?? why was test for NaN added?
                        }
                        else
                        {
                            targetValues[i] = 0.0f;
                        }

                        if (isTransitioning)
                        {
                            //Perform a linear interpolation between initialValues and targetValues:
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
    }
}
