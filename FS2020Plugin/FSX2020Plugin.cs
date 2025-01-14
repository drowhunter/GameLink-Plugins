using FS2020Plugin.Properties;
using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using YawGLAPI;
namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Microsoft Flight Simulator 2020")]
    [ExportMetadata("Version", "1.8")]
    class FSX2020Plugin : Game
    {

        // SimConnect object

       
        enum DEFINITIONS
        {
            Struct1,
        }

        enum DATA_REQUESTS
        {
            REQUEST_1,
        };

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
            public double relavite_wind_acc_body_x;
            public double relavite_wind_acc_body_y;
            public double relavite_wind_acc_body_z;
            public double rotation_acc_body_x;
            public double rotation_acc_body_y;
            public double rotation_acc_body_z;
            public double aircraft_wind_x;
            public double aircraft_wind_y;
            public double aircraft_wind_z;
            public double crenter_wheel_rpm;
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
            public int    contactPointIsOnGround0;
            public int    contactPointIsOnGround1;
            public int    contactPointIsOnGround2;
            public int    contactPointIsOnGround3;
            public int    contactPointIsOnGround4;
            public int    contactPointIsOnGround5;
            public double engineVibration1;
            public double engineVibration2;
            public double engineVibration3;
            public double engineVibration4;

            public double gForce;
            public double accelerationBodyX;
            public double accelerationBodyY;
            public double accelerationBodyZ;
            public double planeBankDegrees;
            public double planeHeadingDegreesGyro;
            public double planeHeadingDegreesMagnetic;
            public double planeHeadingDegreesTrue;
            public double planePitchDegrees;
            public double rotationAccelerationBodyX;
            public double rotationAccelerationBodyY;
            public double rotationAccelerationBodyZ;
            public double rotationVelocityBodyX;
            public double rotationVelocityBodyY;
            public double rotationVelocityBodyZ;
            public double velocityBodyX;
            public double velocityBodyY;
            public double velocityBodyZ;
            public double verticalSpeed;
            public int    stallWarning;
            public double variometerRate;
            public double engRotorRpm1;
            public double engRotorRpm2;
            public double engRotorRpm3;
            public double engRotorRpm4;
        };

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;
        public string PROCESS_NAME => "FlightSimulator";
        public int STEAM_ID => 1250410;
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

        public string[] GetInputData()
        {
            Type f = typeof(Struct1);
            FieldInfo[] fields = f.GetFields();

            string[] inputs = new string[fields.Length];

            for (int i = 0; i < fields.Length; i++) {
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
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "GEAR HYDRAULIC PRESSURE", "Pound-force per square foot", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
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
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES GYRO", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES MAGNETIC", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY X", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Y", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION ACCELERATION BODY Z", "degrees per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY X", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Y", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "ROTATION VELOCITY BODY Z", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
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

                return simconnect;
                }
                catch (Exception ex)
                {
                    return null;
                    //dispatcher.DialogShow("Could not connect to MSFS", DIALOG_TYPE.INFO);
                    //dispatcher.ExitGame();
                }
        
        }
        private void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {

            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                   
                    Struct1 s1 = (Struct1)data.dwData[0];
                    FieldInfo[] fields = typeof(Struct1).GetFields();
                    
                    for (int i = 0; i < fields.Length; i++) {

                        Type t = fields[i].FieldType;
                        controller.SetInput(i, (float)Math.Round( Convert.ToSingle(fields[i].GetValue(s1)),3));
                    }

                    break;
                default:
                    Console.WriteLine("Unknown request ID: " + data.dwRequestID);
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

    
        public void PatchGame() {
            try {
                string tempFile = Path.GetTempFileName() + ".dll";
                string tempFile2 = Path.GetTempFileName() + ".dll";

                using (WebClient wc = new WebClient()) {
                    wc.DownloadFile("http://yaw.one/gameengine/Plugins/Microsoft_Flight_Simulator_XX/Microsoft.FlightSimulator.SimConnect.dll", tempFile);
                    wc.DownloadFile("http://yaw.one/gameengine/Plugins/Microsoft_Flight_Simulator_XX/SimConnect.dll", tempFile2);
                }

                var entryPointLocation = Assembly.GetEntryAssembly().Location;
                File.Copy(tempFile, Path.GetDirectoryName(entryPointLocation) + "/Gameplugins/Microsoft.FlightSimulator.SimConnect.dll", true);
                File.Copy(tempFile2, Path.GetDirectoryName(entryPointLocation) + "/SimConnect.dll", true);


                dispatcher.RestartApp(false);
            }
            catch(IOException e)
            {
                e.ToString();
                dispatcher.DialogShow("Already patched!",DIALOG_TYPE.INFO);
            }  
            catch (Exception e) {
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
