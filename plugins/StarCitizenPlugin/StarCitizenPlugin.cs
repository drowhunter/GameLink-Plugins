using SharedLib;
using SharedLib.TelemetryHelper;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Star Citizen")]
    [ExportMetadata("Version", "1.0")]
    public class StarCitizenPlugin : Game
    {
        #region Standard Properties
        public int STEAM_ID => 0;
        public string PROCESS_NAME => "StarCitizen";
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

        /// <summary>Socket timeout so the 100 Hz tick keeps running (stale ramp, seat fade) when no events arrive.</summary>
        private const int ReceiveTimeoutMs = 5;

        private volatile bool running = false;
        private UdpTelemetry<ScPacket> telem;
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
            var bridge = new ScBridge();
            telem = new UdpTelemetry<ScPacket>(new UdpTelemetryConfig
            {
                ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port),
                ReceiveTimeout = ReceiveTimeoutMs
            }, new ScPacketConverter());

            var clock = Stopwatch.StartNew();
            var period = 1.0 / ScBridge.Rate;
            var next = 0.0;
            while (running)
            {
                try
                {
                    bridge.OnPacket(telem.Receive(), clock.Elapsed.TotalSeconds);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }

                var now = clock.Elapsed.TotalSeconds;
                if (now < next) continue;
                next += period;
                if (next < now) next = now + period;

                var telemetry = bridge.Tick(now);
                foreach (var (i, key, value) in InputHelper.GetValues(telemetry).WithIndex())
                    controller.SetInput(i, value);
            }
        }

        public void PatchGame()
        {
        }
    }
}
