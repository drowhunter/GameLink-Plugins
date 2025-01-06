using SharedLib;
using SharedLib.TelemetryHelper;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Distance")]
    [ExportMetadata("Version", "1.0")]

    public class DistancePlugin : Game
    {



        private UdpTelemetry<DistanceTelemetryData> telem;
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;
        private volatile bool running = false;
        public int STEAM_ID => 233610;
        public string PROCESS_NAME => "Distance";
        public string AUTHOR => "Trevor Jones";

        public bool PATCH_AVAILABLE => false;

        public string Description => Resource.GetString("description.html");
        public Stream Logo => Resource.GetStream("logo.png");
        public Stream SmallLogo => Resource.GetStream("recent.png");
        public Stream Background => Resource.GetStream("wide.png");
        private string defProfile => Resource.GetString("Default.yawglprofile");




        public void Exit()
        {
            running = false;
        }
        public void PatchGame()
        {
            return;
        }


        public string[] GetInputData() => Helper.GetInputs<DistanceTelemetryData>(default).Select(_ => _.key).ToArray();

        public LedEffect DefaultLED() => dispatcher.JsonToLED(defProfile);
        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(defProfile);

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }

        public void Init()
        {
            running = true;
            readThread = new Thread(new ThreadStart(ReadThread));
            readThread.Start();
        }
        private void ReadThread()
        {
            try
            {
                telem = new UdpTelemetry<DistanceTelemetryData>(new UdpTelemetryConfig
                {
                    ReceiveAddress = new IPEndPoint(IPAddress.Any, 12345)
                });
            
            }
            catch(Exception x)
            {
                dispatcher.ShowNotification(NotificationType.ERROR, x.Message);
                Exit();
            }

            while (running)
            {
                var data = telem.Receive();

                foreach (var (i, (key, value)) in Helper.GetInputs(data).WithIndex())
                {
                    controller.SetInput(i, value);
                }
            }
            
        }





        public Dictionary<string, ParameterInfo[]> GetFeatures() => null;

        //Stream GetStream(string resourceName)
        //{
        //    var assembly = GetType().Assembly;
        //    var rr = assembly.GetManifestResourceNames();
        //    string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";
        //    return assembly.GetManifestResourceStream(fullResourceName);
        //}
    }

    static class Extensions
    {
        public static IEnumerable<(int index, T value)> WithIndex<T>(this IEnumerable<T> source)
        {
            int index = 0;
            foreach (var item in source)
            {
                yield return (index, item);
                index++;
            }
        }
    }

}