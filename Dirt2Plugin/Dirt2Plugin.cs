
using SharedLib;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Xml;

using YawGLAPI;

namespace Dirt2Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Dirt 2")]
    [ExportMetadata("Version", "1.0")]
    class Dirt4Plugin : Game {
        
        

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

        public Type GetConfigBody() => typeof(Config);

        #endregion

        UdpClient udpClient;


        Thread readThread;
        private IPEndPoint remoteIP;

        private bool running = false;

        public void Exit() {
            udpClient.Close();
            udpClient = null;
            running = false;
        }

        public string[] GetInputData() => InputHelper.GetValues<DirtTelemetry>(default).Keys();


        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.controller = controller;
            this.dispatcher = dispatcher;
        }
        public void Init() {

            var pConfig = dispatcher.GetConfigObject<Config>();
            dispatcher.DialogShow($"Using port {pConfig.Port}",DIALOG_TYPE.INFO);
            udpClient = new UdpClient(pConfig.Port);
            readThread = new Thread(new ThreadStart(ReadFunction));
            running = true;
            readThread.Start();

        }
       
        private void ReadFunction() {
            try {
                while (running) {
                    byte[] rawData = udpClient.Receive(ref remoteIP);
                    float speed = ReadSingle(rawData, 28, true);
                    float rpm = ReadSingle(rawData, 148, true) / 30;

                    float VelocityX = (float)(ReadSingle(rawData, 32, true));
                    float VelocityY = (float)(ReadSingle(rawData, 36, true));
                    float VelocityZ = (float)(ReadSingle(rawData, 40, true));



                    float steer = ReadSingle(rawData, 120, true);
                    float g_long = ReadSingle(rawData, 140, true);  // *-5
                    float g_lat = ReadSingle(rawData, 136, true); // *-3
                    float forwardX = ReadSingle(rawData, 56, true);
                    float forwardY = (float)(ReadSingle(rawData, 60, true));
                    float forwardZ = (float)(ReadSingle(rawData, 64, true));

                    float rollX = ReadSingle(rawData, 44, true);
                    float rollY = (float)(ReadSingle(rawData, 48, true));
                    float rollZ = (float)(ReadSingle(rawData, 52, true));

                    float susp_pos_bl = (float)ReadSingle(rawData, 68, true);
                    float susp_pos_br = (float)ReadSingle(rawData, 72, true);
                    float susp_pos_fl = (float)ReadSingle(rawData, 76, true);
                    float susp_pos_fr = (float)ReadSingle(rawData, 80, true);
                    float susp_velo_bl = (float)ReadSingle(rawData, 84, true);
                    float susp_velo_br = (float)ReadSingle(rawData, 88, true);
                    float susp_velo_fl = (float)ReadSingle(rawData, 92, true);
                    float susp_velo_fr = (float)ReadSingle(rawData, 96, true);

                    float wheel_speed_rl = (float)ReadSingle(rawData, 100, true);
                    float wheel_speed_rr = (float)ReadSingle(rawData, 104, true);


                    float pitch = (float)(Math.Asin(-forwardY) * 57.3);
                    float roll = -(float)(Math.Asin(-rollY) * 57.3);
                    float yaw = (float)Math.Atan2(forwardY + forwardX, forwardZ) * 57.3f;


                    controller.SetInput(0, speed);
                    controller.SetInput(1, rpm);
                    controller.SetInput(2, steer);

                    controller.SetInput(3, g_long);
                    controller.SetInput(4, g_lat);

                    controller.SetInput(5, pitch);
                    controller.SetInput(6, roll);
                    controller.SetInput(7, yaw);
                    controller.SetInput(8, susp_pos_bl);
                    controller.SetInput(9, susp_pos_br);
                    controller.SetInput(10, susp_pos_fl);
                    controller.SetInput(11, susp_pos_fr);
                    controller.SetInput(12, susp_velo_bl);
                    controller.SetInput(13, susp_velo_br);
                    controller.SetInput(14, susp_velo_fl);
                    controller.SetInput(15, susp_velo_fr);
                    controller.SetInput(16, VelocityX);
                    controller.SetInput(17, VelocityY);
                    controller.SetInput(18, VelocityZ);

                }

            }
            catch (SocketException) {
            }
            catch (ThreadAbortException) { }
        }

      
        public void PatchGame() {
            byte filePatched = 0;
            string[] path = new string[] {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/My Games/DiRT 4/hardwaresettings/hardware_settings_config.xml",
             Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/My Games/DiRT 4/hardwaresettings/hardware_settings_config_vr.xml",
        };

            for (int i = 0; i < path.Length; i++) {

                if (File.Exists(path[i])) {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(path[1]);
                    XmlNode root = doc.DocumentElement;
                    XmlNode node = root.SelectSingleNode("motion_platform");
                    node.SelectSingleNode("dbox").Attributes["enabled"].Value = "true";
                    node.SelectSingleNode("udp").Attributes["enabled"].Value = "true";
                    node.SelectSingleNode("udp").Attributes["ip"].Value = "127.0.0.1";
                    node.SelectSingleNode("udp").Attributes["extradata"].Value = "1";
                    node.SelectSingleNode("udp").Attributes["port"].Value = "20777";
                    node.SelectSingleNode("udp").Attributes["delay"].Value = "1";


                    doc.Save(path[i]);
                    filePatched++;
                    dispatcher.ShowNotification(NotificationType.INFO,path[i] + " patched!");
                }
            }

            if (filePatched == 0) {
                dispatcher.DialogShow("Could not patch dirt rally 4. Make sure to start the game at least once before patching!",DIALOG_TYPE.INFO);
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
