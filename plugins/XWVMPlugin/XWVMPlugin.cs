using SharedLib;
using SharedLib.TelemetryHelper;

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
        private readonly XwvmConverter converter = new();
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;
        private IDeviceParameters deviceParameters;

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
            deviceParameters = dispatcher.GetDeviceParameters();

            running = true;
            readThread = new Thread(new ThreadStart(ReadThread)) { IsBackground = true };
            readThread.Start();
        }

        private void ReadThread()
        {
            converter.Reset();
            telem = new UdpTelemetry<Dictionary<string, object>>(new UdpTelemetryConfig
            {
                ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port),
                ReceiveTimeout = 1000
            }, converter);

            var lastTime = float.NaN;
            var lastPacket = long.MinValue;
            while (running)
            {
                try
                {
                    var data = telem.Receive();

                    // packet counter restarts on a new mission: drop stale keys
                    var packet = data.TryGetValue("p", out var pv) ? Convert.ToInt64(pv) : 0L;
                    if (packet < lastPacket)
                    {
                        var fresh = new Dictionary<string, object>(data);
                        converter.Reset();
                        foreach (var kv in fresh) converter.Data[kv.Key] = kv.Value;
                        data = converter.Data;
                        lastTime = float.NaN;
                    }
                    lastPacket = packet;

                    var telemetry = Telemetry.From(data, converter.Heading);

                    // JSON has no pause flag; the mission clock stops while paused
                    var time = data.TryGetValue("t", out var tv) ? Convert.ToSingle(tv) : 0f;
                    if (time == lastTime)
                        telemetry.Paused = 1f;
                    lastTime = time;

                    foreach (var (i, key, value) in InputHelper.GetValues(telemetry).WithIndex())
                        controller.SetInput(i, ScaleInput(key, value, deviceParameters));
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
                catch (Newtonsoft.Json.JsonException) { }
            }
        }

        internal static float ScaleInput(string key, float value, IDeviceParameters limits) => key switch
        {
            // Pitch is nose-down positive and the default profile does not invert it, so nose-down maps to +F (forward).
            nameof(Telemetry.Pitch) => MathsF.ScalePitchRoll(value, -90, 90, -limits.PitchLimitB, limits.PitchLimitF),
            nameof(Telemetry.Roll) => MathsF.ScalePitchRoll(value, -90, 90, -limits.RollLimit, limits.RollLimit),
            _ => value
        };

        public void PatchGame()
        {
        }
    }
}
