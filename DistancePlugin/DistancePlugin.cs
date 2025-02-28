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
using System.Text.RegularExpressions;
using System.Threading;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Distance")]
    [ExportMetadata("Version", "1.6")]

    public class DistancePlugin : Game
    {

        #region Standard Properties

        public int STEAM_ID => 233610;
        public string PROCESS_NAME => "Distance";
        public string AUTHOR => "Trevor Jones (Drowhunter)";

        public bool PATCH_AVAILABLE => true;

        public string Description => ResourceHelper.Description;
        public Stream Logo => ResourceHelper.Logo;
        public Stream SmallLogo => ResourceHelper.SmallLogo;
        public Stream Background => ResourceHelper.Background;

        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(ResourceHelper.DefaultProfile);

        public string[] GetInputData() => InputHelper.GetValues<DistanceTelemetryData>(default).Keys();

        public LedEffect DefaultLED() => new(EFFECT_TYPE.KNIGHT_RIDER, 0, [YawColor.WHITE], 0);


        #endregion
        private Config settings;
        private IDeviceParameters deviceParameters;


        private UdpTelemetry<DistanceTelemetryData> telemetry;
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;
        
        
        private volatile bool running = false;



        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }


        public void Init()
        {
            this.settings = dispatcher.GetConfigObject<Config>();
            deviceParameters = dispatcher.GetDeviceParameters();
            running = true;
            readThread = new Thread(new ThreadStart(ReadThread));
            readThread.Start();
        }

        private void ReadThread()
        {
            try
            {
                telemetry = new UdpTelemetry<DistanceTelemetryData>(new UdpTelemetryConfig
                {
                    ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port)
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
                    var data = telemetry.Receive();

                    if (data.IsCarIsActive)
                    {
                        if (!isRestting)
                        {
                            foreach (var (i, key, value) in InputHelper.GetValues(data).WithIndex())
                            {
                                var nv = 0f;

                                switch (key)
                                {
                                    case nameof(DistanceTelemetryData.Pitch):
                                        nv = MathsF.ScalePitchRoll(value, -90, 90, -deviceParameters.PitchLimitF, deviceParameters.PitchLimitB);
                                        break;
                                    case nameof(DistanceTelemetryData.Roll):
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
            string name = "";
            MemberInfo info = this.GetType();
            foreach (object meta in info.GetCustomAttributes(true))
            {
                if (meta is ExportMetadataAttribute)
                {
                    if (((ExportMetadataAttribute)meta).Name == "Name")
                    {
                        name = (string)((ExportMetadataAttribute)meta).Value;
                    }

                }
            }



            string installPath = dispatcher.GetInstallPath(name);
            if (!string.IsNullOrWhiteSpace(installPath) && !Directory.Exists(installPath))
            {
                dispatcher.DialogShow($"Cant find {name} install directory\n\n{installPath}\n\nOpen Plugin manager to set it?", DIALOG_TYPE.QUESTION, (yes) =>
                {
                    dispatcher.OpenPluginManager();

                });
                return;
            }

            var modType = "BepInEx";
            var doorStopPath = "doorstop_config.ini";
            var outputPath = "DistanceTelemetryMod";

            var doorStop = Path.Combine(installPath, doorStopPath);
            if (!File.Exists(doorStop))
            {
                switch (modType)
                {
                    case "RaiPal":
                        dispatcher.DialogShow("Please install UUVR or BepInEx first", DIALOG_TYPE.INFO);
                        return;
                    case "BepInEx":
                        {
                            //dispatcher.DialogShow("Please install BepInEx first", DIALOG_TYPE.INFO);
                            using var github = new GithubHelper("BepInEx", "BepInEx");

                            var dl = await github.DownloadAssetsByTagAsync("v5.4.23.2", ["win_x64"], cts.Token);

                            foreach()

                        }
                        break;
                }
                 
                return;
            }

            var doorStopIni = await IniHelper.LoadFileAsync(doorStop);

            if (!doorStopIni.TryGetValue("target_assembly", out var targetAssembly)  &&
                !doorStopIni.TryGetValue("targetAssembly", out targetAssembly)
                )
            {
                dispatcher.DialogShow("Can't find targetAssembly in doorstop_config.ini", DIALOG_TYPE.INFO);
                return;
            }

            string modFolder = null;
            var m = Regex.Match(targetAssembly, @"(?<modFolder>.*?BepInEx)[\\/]");
            
            if (m.Success)
            {
                modFolder = m.Groups["modFolder"].Value;


                modFolder = Path.Combine(!Regex.IsMatch(modFolder, @"^[a-zA-Z]:[\\/]") ? installPath : "", modFolder, "plugins/"+ outputPath).Replace('\\', '/');
               
                
            }
            else
            {
                dispatcher.DialogShow($"Can't find BepInEx folder inside of \"{targetAssembly}\"", DIALOG_TYPE.INFO);
                return;
            }

            
            bool quit = false;
            if (Directory.Exists(modFolder))
            {
                dispatcher.DialogShow("Mod already installed. Reinstall?", DIALOG_TYPE.QUESTION, (yes) =>
                {
                    try
                    {
                        Directory.Delete(modFolder, true);
                    }
                    catch (Exception e)
                    {
                        dispatcher.ShowNotification(NotificationType.ERROR, "Error deleting existing mod: " + e.Message);
                        quit = true;

                    }
                }, (no) =>
                {
                    quit = true;
                });
            }

            if (quit)
            {
                return;
            }

            //string url = "https://github.com/Unity-Telemetry-Mods/NewStarGP-TelemetryMod/releases/download/v1%2C0%2C0/NewStarGPTelemetryMod-1.0.0.zip";

            //string tempPath = await DownloadHelper.DownloadFileAsync(url);

            //using var github = new GithubHelper("Unity-Telemetry-Mods", "Distance-TelemetryMod");
            

            //praydog/REFramework-nightly

            //using var github = new GithubHelper("praydog", "REFramework-nightly");

            //var dl = await github.GetAssetsByLatestAsync(["TDB"], cts.Token);

            Console.WriteLine("Metadata Name: " + name + ", Installpath:" + installPath);

            try
            {
                var release = dl.FirstOrDefault();
                if (dl == null || dl.Count == 0)
                {
                    dispatcher.DialogShow("No releases found", DIALOG_TYPE.INFO);
                    return;
                }
                if(File.Exists(release.Href))
                {
                    try
                    {
                        dispatcher.ExtractToDirectory(release.Href, modFolder, true);
                        //dispatcher.DialogShow("File already downloaded", DIALOG_TYPE.INFO);
                        dispatcher.DialogShow($"{release.Name} ({release.Version}) Installed!", DIALOG_TYPE.INFO, showChk: true);
                    }
                    catch (Exception e)
                    {
                        dispatcher.ShowNotification(NotificationType.ERROR, "Error extracting files: " + e.Message);
                    }

                    return;
                }
                
               
            }
            catch (Exception e)
            {
                dispatcher.ShowNotification(NotificationType.ERROR, "Error extracting files: " + e.Message);
            }
        }

    }

    

}