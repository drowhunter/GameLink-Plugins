using SharedLib;
using SharedLib.TelemetryHelper;
using SharedLib.TelemetryHelper.Convertors;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "X-Wing Virtual Machine")]
    [ExportMetadata("Version", "1.0")]
    public class XWVMPlugin : Game
    {
        #region Standard Properties
        public int STEAM_ID => 0;
        public string PROCESS_NAME => "xwvm";
        public string AUTHOR => "Trevor Jones (Drowhunter)";

        public bool PATCH_AVAILABLE => false;

        public string Description => ResourceHelper.Description;
        public Stream Logo => ResourceHelper.Logo;
        public Stream SmallLogo => ResourceHelper.SmallLogo;
        public Stream Background => ResourceHelper.Background;
        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(ResourceHelper.DefaultProfile);

        public LedEffect DefaultLED() => new LedEffect(EFFECT_TYPE.KNIGHT_RIDER, 0, [YawColor.WHITE], 0);

        public Dictionary<string, ParameterInfo[]> GetFeatures() => null;

        public Type GetConfigBody() => typeof(Config);

        private Config settings;
        #endregion

        public string[] GetInputData() => InputHelper.GetValues<Telemetry>().Keys();

        private volatile bool running = false;
        private UdpTelemetry<Dictionary<string, object>> telem;
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;

        public void Exit()
        {
            running = false;
            telem?.Dispose();
        }

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }

        public void Init()
        {
            settings = dispatcher.GetConfigObject<Config>();

            running = true;
            readThread = new Thread(new ThreadStart(ReadThread)) { IsBackground = true };
            readThread.Start();
        }

        private void ReadThread()
        {
            telem = new UdpTelemetry<Dictionary<string, object>>(new UdpTelemetryConfig
            {
                ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port),
                ReceiveTimeout = 1000
            }, new SparseJsonDictConverter());

            while (running)
            {
                try
                {
                    var telemetry = Telemetry.From(telem.Receive());

                    var i = 0;
                    foreach (var (_, value) in InputHelper.GetValues(telemetry))
                        controller.SetInput(i++, value);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
                catch (Newtonsoft.Json.JsonException) { }
            }
        }

        public void PatchGame()
        {
        }
    }
}
