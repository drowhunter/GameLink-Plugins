
using SharedLib;
using SharedLib.TelemetryHelper;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Xml;

using YawGLAPI;

namespace Dirt2Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Dirt 2")]
    [ExportMetadata("Version", "1.0")]
    class Dirt2Plugin : Game {
        
        

        #region Standard Properties

        private IProfileManager controller;
        private IMainFormDispatcher dispatcher;

        public string PROCESS_NAME => "dirt2_game";
        public int STEAM_ID => 321040;
        public string AUTHOR => "Trevor Jones (Drowhunter)";

        public bool PATCH_AVAILABLE => true;
        public string Description => ResourceHelper.Description;
        public Stream Logo => ResourceHelper.Logo;
        public Stream SmallLogo => ResourceHelper.SmallLogo;
        public Stream Background => ResourceHelper.Background;
        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(ResourceHelper.DefaultProfile);

        public LedEffect DefaultLED() => new LedEffect(EFFECT_TYPE.KNIGHT_RIDER, 0, [YawColor.WHITE], 0);
        public Dictionary<string, ParameterInfo[]> GetFeatures() => null;

        private Config settings;
        public Type GetConfigBody() => typeof(Config);

        #endregion

        private UdpClient udpClient;
        CancellationTokenSource cts = new();

        Thread readThread;
        private IPEndPoint remoteIP;

        private bool running = false;

        public void Exit() {
            running = false;
            cts.Cancel();
            udpClient.Close();
            udpClient = null;
        }

        public string[] GetInputData() => InputHelper.GetValues<TelemetryOut>(default).Keys();


        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.controller = controller;
            this.dispatcher = dispatcher;
        }
        public void Init()
        {
            this.settings = dispatcher.GetConfigObject<Config>();


            udpClient = new UdpClient(settings.Port);
            udpClient.Client.ReceiveTimeout = 2000;

            running = true;


#if DEBUG
            Debugger.Launch();
#endif
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();
        }

        

        private void ReadFunction() {

            

            try {
                while (running) {

                    var t = new TelemetryOut();

                    byte[] rawData = udpClient.Receive(ref remoteIP);

                    t.Speed = ReadSingle(rawData, 28, true);
                    t.RPM = ReadSingle(rawData, 148, true) / 30;
                    t.Steer = ReadSingle(rawData, 120, true);
                    t.Force_long = ReadSingle(rawData, 140, true);  // *-5
                    t.Force_lat = ReadSingle(rawData, 136, true); // *-3

                    t.Velocity = new Vector3(
                        ReadSingle(rawData, 32, true),
                        ReadSingle(rawData, 36, true),
                        ReadSingle(rawData, 40, true)
                    );

                    Vector3 right = new Vector3(
                        ReadSingle(rawData, 44, true),
                        ReadSingle(rawData, 48, true),
                        ReadSingle(rawData, 52, true)
                    );


                    Vector3 forward = new Vector3(
                        ReadSingle(rawData, 56, true),
                        ReadSingle(rawData, 60, true),
                        ReadSingle(rawData, 64, true)
                    );

                    

                    t.suspen_pos_bl = ReadSingle(rawData, 68, true);
                    t.suspen_pos_br = ReadSingle(rawData, 72, true);
                    t.suspen_pos_fl = ReadSingle(rawData, 76, true);
                    t.suspen_pos_fr = ReadSingle(rawData, 80, true);

                    t.suspen_vel_bl = ReadSingle(rawData, 84, true);
                    t.suspen_vel_br = ReadSingle(rawData, 88, true);
                    t.suspen_vel_fl = ReadSingle(rawData, 92, true);
                    t.suspen_vel_fr = ReadSingle(rawData, 96, true);

                    


                    t.Pitch = MathF.Asin(-forward.Y) * 57.3f;
                    t.Roll = -MathF.Asin(-right.Y) * 57.3f;
                    t.Yaw = MathF.Atan2(forward.Y + forward.X, forward.Z) * 57.3f;

                    foreach (var (i, key, value) in InputHelper.GetValues(t).WithIndex())
                        controller.SetInput(i, value);

                }

            }
            catch (SocketException) {
            }
            catch (ThreadAbortException) { }
        }


        public void PatchGame()
        {
            
            bool patched = false;

            var file = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/My Games/DiRT2/hardwaresettings/hardware_settings_config.xml";

            if (File.Exists(file))
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(file);
                XmlNode documentElement = xmlDocument.DocumentElement;
                XmlNode motionNode = documentElement.SelectSingleNode("motion");
                motionNode.Attributes["enabled"].Value = "true";
                motionNode.Attributes["ip"].Value = "127.0.0.1";
                motionNode.Attributes["extradata"].Value = "1";
                motionNode.Attributes["port"].Value = "20777";
                motionNode.Attributes["delay"].Value = "1";
                xmlDocument.Save(file);
                patched = true;
                dispatcher.ShowNotification(NotificationType.INFO, file + " patched!");
            }

            if (!patched)
            {
                dispatcher.DialogShow("Could not patch dirt 2. Make sure to start the game at least once before patching!", DIALOG_TYPE.INFO);
            }
        }

        float ReadSingle(byte[] data, int offset, bool littleEndian)
        {
            if (BitConverter.IsLittleEndian != littleEndian)
            {   // other-endian; reverse this portion of the data (4 bytes)
                byte tmp = data[offset];
                data[offset] = data[offset + 3];
                data[offset + 3] = tmp;
                tmp = data[offset + 1];
                data[offset + 1] = data[offset + 2];
                data[offset + 2] = tmp;
            }
            return BitConverter.ToSingle(data, offset);
        }


        
    }
}
