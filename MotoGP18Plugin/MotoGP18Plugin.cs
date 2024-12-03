using MotoGP18Plugin.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using YawGEAPI;
using MotoGP18Plugin.Properties;
using System.IO;
namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "MotoGP 18")]
    [ExportMetadata("Version", "1.1")]
    class MotoGP18Plugin : Game
    {
        [StructLayout(LayoutKind.Explicit)]
        private class Packets {

            //[FieldOffset(0)]
            //public Int32 Header;

            //[FieldOffset(4)]
            //public UInt32 packetid;

            //[FieldOffset(8)]
            //[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            //public string Track;

            //[FieldOffset(40)]
            //[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            //public string Model;

            //[FieldOffset(104)]
            //public byte CurrentLap;

            //[FieldOffset(105)]
            //public byte Position;

            //[FieldOffset(106)]
            //public Single LapTime;

            //[FieldOffset(110)]
            //public Single LastLapTime;

            //[FieldOffset(114)]
            //public Single SessionTime;

            //[FieldOffset(118)]
            //public byte GameMode;

            [FieldOffset(119)]
            public Single S1;

            [FieldOffset(123)]
            public Single S2;

            [FieldOffset(127)]
            public Single S3;

            //[FieldOffset(136)]
            //public Single LapDistance;

            //[FieldOffset(140)]
            //public Single TotalDistance;

            //[FieldOffset(150)]
            //public Single CoordinatesX;

            //[FieldOffset(154)]
            //public Single CoordinatesY;

            //[FieldOffset(158)]
            //public Single CoordinatesZ;

            //        [FieldOffset(162)]
            //        public Single?;

            //[FieldOffset(166)]
            //        public Single?;

            [FieldOffset(170)]
            public Single Pitch;

            [FieldOffset(174)]
            public Single Yaw;

            [FieldOffset(187)]
            public Single MaxRpm;

            //[FieldOffset(191)]
            //public byte gear;

            [FieldOffset(192)]
            public Single RPM;

            [FieldOffset(196)]
            public Single Steering;

            [FieldOffset(200)]
            public Single Throttle;

            [FieldOffset(204)]
            public Single Clutch;

            [FieldOffset(208)]
            public Single BrakeF;

            [FieldOffset(212)]
            public Single BrakeR;

            [FieldOffset(216)]
            public Single SuspensionPositionF;

            [FieldOffset(220)]
            public Single SuspensionPositionR;

            [FieldOffset(232)]
            public Single WheelSpeedF;

            [FieldOffset(236)]
            public Single WheelSpeedR;

            [FieldOffset(250)]
            public Single WheelSlipF;

            [FieldOffset(254)]
            public Single WheelSlipR;

            //[FieldOffset(266)]
            //public byte FrontTyreCompound;

            //[FieldOffset(267)]
            //public byte RearTyreCompound;

            //[FieldOffset(268)]
            //public Single BrakeTempF;

            [FieldOffset(276)]
            public Single LongitudinalBodyPosition;

            [FieldOffset(284)]
            public Single LateralGforce;

            [FieldOffset(288)]
            public Single LongitudinalGforce;

            [FieldOffset(292)]
            public Single Speed;

            public float SpeedInKmPerHour {
                get {
                    return this.Speed * 3.6f;
                }
            }
            public float GetPropertyValueAt(int index) {
                var prop = this.GetType().GetProperties()[index];
                return (float)prop.GetValue(this, null);
            }
        }

        private IPEndPoint senderIP = new IPEndPoint(IPAddress.Any, 0);



        public string PROCESS_NAME => "motogp18";
        public string AUTHOR => "YawVR";
        public int STEAM_ID => 775900;
        public bool PATCH_AVAILABLE => false;

        public string Description => Resources.description;

        public Stream Logo => GetStream("logo.png");
        public Stream SmallLogo => GetStream("recent.png");
        public Stream Background => GetStream("wide.png");



        UdpClient udpClient;
        Thread readThread;



        public int port = 7100;
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
            return dispatcher.JsonToComponents(Resources.defProfile);
        }

        public void Exit() {

            udpClient.Close();
            udpClient = null;
            running = false;
            //readThread.Abort();
        }
        public void PatchGame()
        {
            return;
        }
        public string[] GetInputData() {

            Type t = typeof(Packets);
            FieldInfo[] fields = t.GetFields();

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

      
        public void Init() {

            udpClient = new UdpClient(7100);
            udpClient.Client.ReceiveTimeout = 5000;
            running = true;
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();
        }

        private void ReadFunction() {

            Packets sende = new Packets();
            FieldInfo[] fields = typeof(Packets).GetFields();
        

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
                            Console.WriteLine(i);
                            controller.SetInput(i, (float)fields[i].GetValue(sende));
                        }
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine(e);

                    }
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


