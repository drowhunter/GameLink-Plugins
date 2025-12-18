using SharedLib;
using SharedLib.TelemetryHelper;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using YawGLAPI;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TelemetryConsole")]
namespace RFactor2Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "RFactor2")]
    [ExportMetadata("Version", "0.1")]

    public class RFactor2Plugin : Game
    {

        #region Standard Properties

        public int STEAM_ID => 365960;
        public string PROCESS_NAME => "RFactor2";
        public string AUTHOR => "Trevor Jones (Drowhunter)";

        public bool PATCH_AVAILABLE => true;

        public string Description => ResourceHelper.Description;
        public Stream Logo => ResourceHelper.Logo;
        public Stream SmallLogo => ResourceHelper.SmallLogo;
        public Stream Background => ResourceHelper.Background;

        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(ResourceHelper.DefaultProfile);

        public string[] GetInputData() => InputHelper.GetValues<RFactor2TelemetryData>(default).Keys();

        public LedEffect DefaultLED() => new(EFFECT_TYPE.KNIGHT_RIDER, 0, [YawColor.WHITE], 0);


        #endregion

        public const string MM_TELEMETRY_FILE_NAME = "$rFactor2SMMP_Telemetry$";


        private Config settings;
        private IDeviceParameters deviceParameters;


        
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;


        private volatile bool running = false;

        private MmfTelemetry<rF2Telemetry> telemetry;
        MappedBuffer<rF2Telemetry> telemetryBuffer;

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }


        public void Init()
        {
            this.settings = dispatcher.GetConfigObject<Config>();
            deviceParameters = dispatcher.GetDeviceParameters();

            telemetryBuffer = new MappedBuffer<rF2Telemetry>(MM_TELEMETRY_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);

            running = true;
            readThread = new Thread(new ThreadStart(ReadThread));
            readThread.Start();
        }

        private void ReadThread()
        {
            try
            {
                telemetry = new MmfTelemetry<rF2Telemetry>(new MmfTelemetryConfig
                {
                    Name = MM_TELEMETRY_FILE_NAME
                });

            }
            catch (Exception x)
            {
                dispatcher.ShowNotification(NotificationType.ERROR, x.Message);
                Exit();
            }

            while (running)
            {
                try
                {
                    rF2Telemetry data = telemetry.Receive();

                    if (data.IsCarIsActive)
                    {
                        if (!isRestting)
                        {
                            foreach (var (i, key, value) in InputHelper.GetValues(data).WithIndex())
                            {
                                var nv = 0f;

                                switch (key)
                                {
                                    case nameof(RFactor2TelemetryData.Pitch):
                                        nv = MathsF.ScalePitchRoll(value, -90, 90, -deviceParameters.PitchLimitF, deviceParameters.PitchLimitB);
                                        break;
                                    case nameof(RFactor2TelemetryData.Roll):
                                        nv = MathsF.ScalePitchRoll(value, -90, 90, -deviceParameters.RollLimit, deviceParameters.RollLimit);
                                        break;
                                    default:
                                        nv = value;
                                        break;
                                }

                                controller.SetInput(i, nv);
                            }
                        }
                        else
                        {
                            isRestting = false;
                            Thread.Sleep(1000);
                        }
                    }
                    else if (!isRestting)
                    {
                        isRestting = true;
                    }
                }
                catch (SocketException) { }
            }

        }

        bool isRestting = false;


        CancellationTokenSource cts = new CancellationTokenSource();

        public Dictionary<string, ParameterInfo[]> GetFeatures() => null;

        public Type GetConfigBody() => typeof(Config);

        public void Exit()
        {
            running = false;
            cts.Cancel();
            telemetry?.Dispose();
        }

       

        public async void PatchGame()
        {
#if DEBUG
            Debugger.Launch();
#endif

            await UnityPatcher.Create<UnityPatcher>(this, dispatcher, options =>
            {
                //options.GameInstallPath = installPath;
                options.ModType = ModType.BepInEx5_x64;
                options.PluginName = "RFactor2TelemetryMod";
                options.DoorStopPath = "";
                options.Repository = new GithubOptions
                {
                    UsernameOrOrganization = "Unity-Telemetry-Mods",
                    Repository = "RFactor2-TelemetryMod"
                };                
            }).PatchAsync(cts.Token);
            
            
           
        }

    }

    

}