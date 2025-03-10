
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

        public string[] GetInputData()
        {
            return new string[19]
            {
                "Speed",
                "RPM",
                "Steer",
                "Force_long",
                "Force_lat",
                "Pitch",
                "Roll",
                "Yaw",
                "suspen_pos_bl",
                "suspen_pos_br",
                "suspen_pos_fl",
                "suspen_pos_fr",
                "suspen_vel_bl",
                "suspen_vel_br",
                "suspen_vel_fl",
                "suspen_vel_fr",
                "VelocityX",
                "VelocityY",
                "VelocityZ"
            };
        }



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


            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();
        }


        private void ReadFunction()
        {
            try
            {
                while (running)
                {
                    try
                    {
                        byte[] data = udpClient.Receive(ref remoteIP);
                        float speed = BitConverter.ToSingle(data, 28);
                        float rpm = BitConverter.ToSingle(data, 148) / 30f;
                        float velocityX = (float)(Math.Asin(BitConverter.ToSingle(data, 32)) * 57.3);
                        float velocityY = (float)(Math.Asin(BitConverter.ToSingle(data, 36)) * 57.3);
                        float velocityZ = (float)(Math.Asin(BitConverter.ToSingle(data, 40)) * 57.3);
                        float steer = BitConverter.ToSingle(data, 120);
                        float Gforce_lon = BitConverter.ToSingle(data, 140);
                        float Gforce_lat = BitConverter.ToSingle(data, 136);
                        float pitchX = BitConverter.ToSingle(data, 56);
                        float pitchY = BitConverter.ToSingle(data, 60);
                        float pitchZ = BitConverter.ToSingle(data, 64);
                        float rollX = BitConverter.ToSingle(data, 44);
                        float rollY = BitConverter.ToSingle(data, 48);
                        float rollZ = BitConverter.ToSingle(data, 52);
                        float Susp_pos_bl = BitConverter.ToSingle(data, 68);
                        float Susp_pos_br = BitConverter.ToSingle(data, 72);
                        float Susp_pos_fl = BitConverter.ToSingle(data, 76);
                        float Susp_pos_fr = BitConverter.ToSingle(data, 80);
                        float Susp_vel_bl = BitConverter.ToSingle(data, 84);
                        float Susp_vel_br = BitConverter.ToSingle(data, 88);
                        float Susp_vel_fl = BitConverter.ToSingle(data, 92);
                        float Susp_vel_fr = BitConverter.ToSingle(data, 96);
                        float Wheel_speed_bl = BitConverter.ToSingle(data, 100);
                        float Wheel_speed_br = BitConverter.ToSingle(data, 104);
                        float pitch = (float)(Math.Asin(0f - pitchY) * 57.3);
                        float roll = 0f - (float)(Math.Asin(0f - rollY) * 57.3);
                        float yaw = (float)Math.Atan2(pitchY + pitchX, pitchZ) * 57.3f;

                        

                        controller.SetInput(0, speed);
                        controller.SetInput(1, rpm);
                        controller.SetInput(2, steer);
                        controller.SetInput(3, Gforce_lon);
                        controller.SetInput(4, Gforce_lat);
                        controller.SetInput(5, pitch);
                        controller.SetInput(6, roll);
                        controller.SetInput(7, yaw);
                        controller.SetInput(8, Susp_pos_bl);
                        controller.SetInput(9, Susp_pos_br);
                        controller.SetInput(10, Susp_pos_fl);
                        controller.SetInput(11, Susp_pos_fr);
                        controller.SetInput(12, Susp_vel_bl);
                        controller.SetInput(13, Susp_vel_br);
                        controller.SetInput(14, Susp_vel_fl);
                        controller.SetInput(15, Susp_vel_fr);
                        controller.SetInput(16, velocityX);
                        controller.SetInput(17, velocityY);
                        controller.SetInput(18, velocityZ);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (ThreadAbortException)
            {
            }
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
