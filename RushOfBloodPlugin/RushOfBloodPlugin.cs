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
    [ExportMetadata("Name", "Until Dawn: Rush of Blood")]
    [ExportMetadata("Version", "1.0")]
    public class RushOfBloodPlugin : Game
    {
        #region Standard Properties

        public int STEAM_ID => 0; // Not a Steam game (runs on the shadPS4 emulator)
        public string PROCESS_NAME => "shadps4";
        public string AUTHOR => "Trevor Jones (Drowhunter)";

        public bool PATCH_AVAILABLE => false;

        public string Description => ResourceHelper.Description;
        public Stream Logo => ResourceHelper.Logo;
        public Stream SmallLogo => ResourceHelper.SmallLogo;
        public Stream Background => ResourceHelper.Background;

        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(ResourceHelper.DefaultProfile);

        public string[] GetInputData() => InputHelper.GetValues<RushOfBloodTelemetryData>(default).Keys();

        public LedEffect DefaultLED() => new(EFFECT_TYPE.KNIGHT_RIDER, 0, [new YawColor(180, 0, 0)], 0);

        #endregion

        private Config settings;
        private IDeviceParameters deviceParameters;

        private UdpTelemetry<RushOfBloodTelemetryData> telemetry;
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;

        private volatile bool running = false;
        private bool isResetting = false;

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
            readThread = new Thread(new ThreadStart(ReadThread));
            readThread.Start();
        }

        private void ReadThread()
        {
            try
            {
                telemetry = new UdpTelemetry<RushOfBloodTelemetryData>(new UdpTelemetryConfig
                {
                    ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port),
                    ReceiveTimeout = 1000
                }, new MarshalByteConverter<RushOfBloodTelemetryData>());
            }
            catch (Exception x)
            {
                dispatcher.ShowNotification(NotificationType.ERROR, x.Message);
                Exit();
                return;
            }

            while (running)
            {
                try
                {
                    var data = telemetry.Receive();

                    if (data.InPlay && !data.Paused)
                    {
                        if (!isResetting)
                        {
                            foreach (var (i, key, value) in InputHelper.GetValues(data).WithIndex())
                            {
                                var nv = ScaleInput(key, value, deviceParameters);

                                controller.SetInput(i, nv);
                            }
                        }
                        else
                        {
                            isResetting = false;
                            Thread.Sleep(1000);
                        }
                    }
                    else if (!isResetting)
                    {
                        isResetting = true;
                    }
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
            }
        }

        internal static float ScaleInput(string key, float value, IDeviceParameters limits) => key switch
        {
            // GameLink calibration drives final pitch +F forward and -B backward. The default
            // profile inverts pitch, so negative input becomes +F and positive input becomes -B.
            nameof(RushOfBloodTelemetryData.Pitch) => MathsF.ScalePitchRoll(value, -90, 90, -limits.PitchLimitF, limits.PitchLimitB),
            nameof(RushOfBloodTelemetryData.Roll) => MathsF.ScalePitchRoll(value, -90, 90, -limits.RollLimit, limits.RollLimit),
            _ => value
        };

        public Dictionary<string, ParameterInfo[]> GetFeatures() => null;

        public Type GetConfigBody() => typeof(Config);

        public void Exit()
        {
            running = false;
            telemetry?.Dispose();
        }

        public void PatchGame()
        {
            // Nothing to patch: the BloodRush shadPS4 build emits cart telemetry natively.
            Debugger.Launch();
        }
    }
}
