using Gta5Plugin.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using YawGEAPI;

namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Grand Theft Auto V")]
    [ExportMetadata("Version", "1.0")]
    class Gta5Plugin : Game {

        [StructLayout(LayoutKind.Explicit)]
        private class Packets {

            //[FieldOffset(0)]
            //public float Time;

            //[FieldOffset(4)]
            //public float LapTime;

            //[FieldOffset(8)]
            //public float LapDistance;
            //[FieldOffset(12)]
           // public float Distance;
            //[FieldOffset(16)]
            //public float X;
            //[FieldOffset(20)]
            //public float Y;
            //[FieldOffset(24)]
            //public float Z;
            [FieldOffset(28)]
            public float Speed;
            //[FieldOffset(32)]
            //public float WorldSpeedX;
            //[FieldOffset(36)]
            //public float WorldSpeedY;
            //[FieldOffset(40)]
            //public float WorldSpeedZ;
            [FieldOffset(44)]
            public float Pitch;
            [FieldOffset(48)]
            public float Roll;
            [FieldOffset(52)]
            public float Vehicle_Heading;
          
            //[FieldOffset(56)]
            //public float XD;
            //[FieldOffset(60)]
            //public float YD;
            [FieldOffset(64)]
            public float PlayerHeading;
            //[FieldOffset(68)]
            //public float SuspensionPositionRearLeft;
            //[FieldOffset(72)]
            //public float SuspensionPositionRearRight;
            //[FieldOffset(76)]
            //public float SuspensionPositionFrontLeft;
            //[FieldOffset(80)]
            //public float SuspensionPositionFrontRight;
            //[FieldOffset(84)]
            //public float SuspensionVelocityRearLeft;
            //[FieldOffset(88)]
            //public float SuspensionVelocityRearRight;
            //[FieldOffset(92)]
            //public float SuspensionVelocityFrontLeft;
            //[FieldOffset(96)]
            //public float SuspensionVelocityFrontRight;
            //[FieldOffset(100)]
            //public float WheelSpeedReadLeft;
            //[FieldOffset(104)]
            //public float WheelSpeedRearRight;
            //[FieldOffset(108)]
            //public float WheelSpeedFrontLeft;
            //[FieldOffset(112)]
            //public float WheelSpeedFrontRight;
            //[FieldOffset(116)]
            //public float Throttle;
            //[FieldOffset(120)]
            //public float Steer;
            //[FieldOffset(124)]
            //public float Brake;
            //[FieldOffset(128)]
            //public float Clutch;
            //[FieldOffset(132)]
            //public float Gear;
            //[FieldOffset(136)]
            //public float LateralAcceleration;
            //[FieldOffset(140)]
            //public float LongitudinalAcceleration;
           // [FieldOffset(144)]
            //public float Speed;
            [FieldOffset(148)]
            public float RPM;
            //[FieldOffset(152)]
            //public float SliProNativeSupport;
            //[FieldOffset(156)]
            //public float RacePosition;
            //[FieldOffset(160)]
            //public float KersRemaining;
            //[FieldOffset(164)]
            //public float KersMaxLevel;
            //[FieldOffset(168)]
            //public float DrsStatus;
            //[FieldOffset(172)]
            //public float TractionControl;
            //[FieldOffset(176)]
            //public float AntiLock;
            //[FieldOffset(180)]
            //public float FuelRemaining;
            //[FieldOffset(184)]
            //public float FuelCapacity;
            [FieldOffset(188)]
            public float Shoot;
            [FieldOffset(192)]
            public float IsinWater;
            [FieldOffset(196)]
            public float IsinAir;
            [FieldOffset(200)]
            public float Brakepower;
            //[FieldOffset(204)]
            //public float BrakeTemperatureRearLeft;
            //[FieldOffset(208)]
            //public float BrakeTemperatureRearRight;
            //[FieldOffset(212)]
            //public float BrakeTemperatureFrontLeft;
            //[FieldOffset(216)]
            //public float BrakeTemperatureFrontRight;
            //[FieldOffset(220)]
            //public float WheelPressureRearLeft;
            //[FieldOffset(224)]
            //public float WheelPressureRearRight;
            //[FieldOffset(228)]
            //public float WheelPressureFrontLeft;
            //[FieldOffset(232)]
            //public float WheelPressureFrontRight;
            //[FieldOffset(236)]
            //public float CompletedLapsInRace;
            //[FieldOffset(240)]
            //public float TotalLapsInRace;
            //[FieldOffset(244)]
            //public float TrackLength;
            //[FieldOffset(248)]
            //public float PreviousLapTime;
            //[FieldOffset(252)]
            //public float MaxRpm;
            //[FieldOffset(256)]
            //public float IdleRpm;
            //[FieldOffset(260)]
            //public float MaxGears;
            //[FieldOffset(264)]
            //public float SessionType;
            //[FieldOffset(268)]
            //public float DrsAllowed;
            [FieldOffset(272)]
            public float Acceleration;
            //[FieldOffset(276)]
            //public float FIAFlags;

            public float SpeedInKmPerHour {
                get {
                    return this.Speed * 3.6f;
                }
            }

            /*public bool IsSittingInPits {
                get {
                    return Math.Abs(this.LapTime - 0f) < 1E-05f && Math.Abs(this.Speed - 0f) < 1E-05f;
                }
            }

            public bool IsInPitLane {
                get {
                    return Math.Abs(this.LapTime - 0f) < 1E-05f;
                }
            }

            public string SessionTypeName {
                get {
                    if (Math.Abs(this.SessionType - 9.5f) < 0.0001f) {
                        return "Race";
                    }
                    if (Math.Abs(this.SessionType - 10f) < 0.0001f) {
                        return "Time Trial";
                    }
                    if (Math.Abs(this.SessionType - 170f) < 0.0001f) {
                        return "Qualifying or Practice";
                    }
                    return "Other";
                }
            }
            */
            public float GetPropertyValueAt(int index) {
                var prop = this.GetType().GetProperties()[index];
                return (float)prop.GetValue(this, null);
            }
        }

        private IPEndPoint senderIP = new IPEndPoint(IPAddress.Any, 0);
        UdpClient udpClient;
        Thread readThread;
        public int STEAM_ID => 271590;
        public bool PATCH_AVAILABLE => true;
        public string AUTHOR => "YawVR";
        public string PROCESS_NAME => string.Empty;

        public Stream Logo => GetStream("logo.png");
        public Stream SmallLogo => GetStream("recent.png");

        public Stream Background => GetStream("wide.png");

        public string Description => Resources.description;

        public int port = 20777;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;

        private volatile bool running = false;

        public LedEffect DefaultLED() {

            return new LedEffect(

           EFFECT_TYPE.KNIGHT_RIDER,
           0,
           new YawColor[] {
                    new YawColor(255, 255, 255),
                    new YawColor(80, 80, 80),
                    new YawColor(255, 255, 0),
                    new YawColor(0, 0, 255),
           },
           0.5f);
        }

        public List<Profile_Component> DefaultProfile() {
            return new List<Profile_Component>() {

                new Profile_Component(1,1, 1.0f,1.0f,0f,false,true,-1f,1.0f),
                new Profile_Component(2,2, 0.7f,0.7f,0f,false,true,-1f,1.0f),
                new Profile_Component(10,1, 30.0f,30.0f,0f,false,true,-1f,0.08f),
                new Profile_Component(3,0, 1.0f,1.0f,0f,false,true,-1f,1.0f),
                new Profile_Component(5,4, 23.0f,23.0f,0f,false,false,-1f,1.0f),
                new Profile_Component(0,3, 8.0f,8.0f,0f,true,false,-1f,1.0f)
            };
        }

        public void Exit() {

            udpClient.Close();
            udpClient = null;
            running = false;
            //readThread.Abort();
        }
     
        public string[] GetInputData() {

            Type t = typeof(Packets);
            FieldInfo[] fields = t.GetFields();

            string[] inputs = new string[fields.Length];

            for(int i =0;i<fields.Length;i++) {
                inputs[i] = fields[i].Name;
            }
            return inputs;
        }
        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }
        public void Init() {

            udpClient = new UdpClient(20777);
            udpClient.Client.ReceiveTimeout = 5000;
            running = true;
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();
        }

        private void ReadFunction() {

            Packets sende = new Packets();
            FieldInfo[] fields =  typeof(Packets).GetFields();
            while (running) {
                try
                {
                    byte[] rawData = udpClient.Receive(ref senderIP);



                    IntPtr unmanagedPointer =
                        Marshal.AllocHGlobal(rawData.Length);
                    Marshal.Copy(rawData, 0, unmanagedPointer, rawData.Length);
                    // Call unmanaged code
                    Marshal.FreeHGlobal(unmanagedPointer);
                    Marshal.PtrToStructure(unmanagedPointer, sende);


                    for (int i = 0; i < fields.Length; i++)
                    {
                        controller.SetInput(i, (float)fields[i].GetValue(sende));
                    }
                } catch(SocketException) { }
            }
        }



        public void PatchGame() {
            string name = "gta";
            System.Reflection.MemberInfo info = typeof(Gta5Plugin);
            foreach (object meta in info.GetCustomAttributes(true)) {
                if (meta is ExportMetadataAttribute) {
                    if (((ExportMetadataAttribute)meta).Name == "Name") {
                        name = (string)((ExportMetadataAttribute)meta).Value;
                    }

                }
            }
          


                string tempPath = Path.GetTempFileName();
            string installPath = dispatcher.GetInstallPath(name);
            if (!Directory.Exists(installPath)) {
                dispatcher.DialogShow("Cant find GTA install directory\nOpen Plugin manager?", DIALOG_TYPE.QUESTION, delegate {
                    dispatcher.OpenPluginManager();
                });
                return;
            }

            Console.WriteLine("Downloading http://yaw.one/gameengine/Plugins/Grand_Theft_Auto_V/Gta5files.zip to " + tempPath);
            using (WebClient wc = new WebClient()) {
                wc.DownloadFile("http://yaw.one/gameengine/Plugins/Grand_Theft_Auto_V/Gta5files.zip", tempPath);
            }


          
            Console.WriteLine("Metadata Name: " + name + ", Installpath:" + installPath);


            dispatcher.ExtractToDirectory(tempPath,installPath,true);
         

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
