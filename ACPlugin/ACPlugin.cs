using ACPlugin.Properties;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using YawGLAPI;
namespace YawVR_Game_Engine.Plugin {
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Assetto Corsa")]
    [ExportMetadata("Version", "1.2")]
    class ACPlugin : Game {
       
        #region handshaker

        
        struct Handshaker {
            int identifier;
            int version;
            int operationId;


            
            public Handshaker(int identifier, int version, int operationId) {
                this.identifier = identifier;
                this.version = version;
                this.operationId = operationId;
              
            }
        
             public byte[] toByte() {
                List<byte> byteList = new List<byte>();

                byteList.AddRange(BitConverter.GetBytes(identifier));
                byteList.AddRange(BitConverter.GetBytes(version));
                byteList.AddRange(BitConverter.GetBytes(operationId));
                return byteList.ToArray();
            }
        };

        private IProfileManager controller;
        private IMainFormDispatcher dispatcher;
        #endregion


        private UdpClient udpClient;

        private IPEndPoint remoteIP = new IPEndPoint(IPAddress.Loopback,9996);

        private Thread readThread;
        private bool running = false;
        private string input = @"127.0.0.1";
        public string PROCESS_NAME => String.Empty;
        public int STEAM_ID => 244210;
        public string AUTHOR => "YawVR";
        public bool PATCH_AVAILABLE => false;

        public Stream Logo => GetStream("logo.png");
        public Stream SmallLogo => GetStream("recent.png");
        public Stream Background => GetStream("wide.png");
        public string Description => String.Empty;

        public void PatchGame()
        {
            return;
        }

        public void Exit() {
            udpClient.Close();
            udpClient = null;
            running = false;
        }
        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.controller = controller;
            this.dispatcher = dispatcher;
        }
        public void Init() {
           
            input = Interaction.InputBox("Enter the host address of Assetto Corsa\nLeave default value if its running on this PC", "Endpoint", "127.0.0.1");
            HandShake();

            running = true;

            readThread = new Thread(ReadFunction);
            readThread.Start();

        }
        private void ReadFunction()
        {
            try
            {
                IPEndPoint endp = new IPEndPoint(IPAddress.Any, 9996);
                while (running)
                {
                    try
                    {
                        byte[] rawData = udpClient.Receive(ref endp);

                        float SpeedKMH = BitConverter.ToSingle(rawData, 8);
                        float rpm = BitConverter.ToSingle(rawData, 68);
                        float steer = (BitConverter.ToSingle(rawData, 72) / 41) * (SpeedKMH / 100);
                        float G_vert = BitConverter.ToSingle(rawData, 28);
                        float G_horiz = BitConverter.ToSingle(rawData, 32);
                        float G_lon = BitConverter.ToSingle(rawData, 36);


                        controller.SetInput(0, SpeedKMH);
                        controller.SetInput(1, rpm);
                        controller.SetInput(2, steer);
                        controller.SetInput(3, G_vert);
                        controller.SetInput(4, G_horiz);
                        controller.SetInput(5, G_lon);
                    } catch(SocketException ex)
                    {
                        for(int i = 0;i<GetInputData().Length;i++)
                        {
                            controller.SetInput(i, 0);
                        }
                        Thread.Sleep(1000);
                        HandShake();
                    }
                }
            }
            catch (ThreadAbortException)
            {
            }
        }
        private void HandShake() {
            udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 1000;
            udpClient.Connect(input, 9996);

            byte[] toSend = new Handshaker(1,1,0).toByte();
            Thread.Sleep(250);
            udpClient.Send(toSend, toSend.Length);
            
            toSend = new Handshaker(1, 1, 1).toByte();
            udpClient.Send(toSend, toSend.Length);
     

        }

        public string[] GetInputData() {
            return new string[] {
                "Speed","RPM","Steer","Acceleration_vert","Acceleration_horiz","Acceleration_lon"
            };
        }
        public LedEffect DefaultLED() {

            return new LedEffect(
               
                EFFECT_TYPE.FLOW_LEFTRIGHT,
                2,
                new YawColor[] {
                new YawColor(255,40,0),
                new YawColor(80,80,80),
                new YawColor(255, 100, 0),
                new YawColor(140, 0, 255),
                },
                -20f);
        }
        public List<Profile_Component> DefaultProfile() {

            return dispatcher.JsonToComponents(Resources.defProfile);
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
