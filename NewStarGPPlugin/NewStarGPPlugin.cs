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
using System.Threading.Tasks;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "New Star GP")]
    [ExportMetadata("Version", "1.0")]
    public class NewStarGPPlugin : Game
    {
        #region Standard Properties
        public int STEAM_ID => 2217580;
        public string PROCESS_NAME => "NSGP";
        public string AUTHOR => "Trevor Jones (Drowhunter)";

        public bool PATCH_AVAILABLE => true;

        public string Description => ResourceHelper.Description;
        public Stream Logo => ResourceHelper.Logo;
        public Stream SmallLogo => ResourceHelper.SmallLogo;
        public Stream Background => ResourceHelper.Background;
        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(ResourceHelper.DefaultProfile);

        public string[] GetInputData() => InputHelper.GetValues<Telemetry>(default).Keys();

        public LedEffect DefaultLED() => new LedEffect(EFFECT_TYPE.KNIGHT_RIDER, 0, [YawColor.WHITE], 0);


        #endregion

        

        private Config settings;

        private volatile bool running = false;

        private UdpTelemetry<Telemetry> telem;
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;

        public void Exit()
        {
            running = false;
            telem?.Dispose();
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
            if (!Directory.Exists(installPath))
            {
                dispatcher.DialogShow($"Cant find {name} install directory\nOpen Plugin manager?", DIALOG_TYPE.QUESTION, (yes) =>
                {
                    dispatcher.OpenPluginManager();

                });
                return;
            }

            var doorStop = Path.Combine(installPath, "release\\doorstop_config.ini");
            if (!File.Exists(doorStop))
            {
                dispatcher.DialogShow("Please install UUVR or BepInEx first", DIALOG_TYPE.INFO);
                
                return;
            }

            var doorStopIni = await IniHelper.LoadFileAsync(doorStop);

            if(!doorStopIni.TryGetValue("targetAssembly", out var targetAssembly))
            {
                dispatcher.DialogShow("Can't find targetAssembly in doorstop_config.ini", DIALOG_TYPE.INFO);
                return;
            }

            string modFolder = null;
            var m = Regex.Match(targetAssembly, @"(.+?[\\/]BepInEx)");
            if (m.Success)
            {
                modFolder = m.Groups[1].Value.Replace('\\', '/') + "/plugins/NewStarGPTelemetryMod";
            }
            else
            {
                dispatcher.DialogShow("Can't find BepInEx folder in targetAssembly", DIALOG_TYPE.INFO);                
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

            if(quit)
            {
                return;
            }

            string url = "https://github.com/Unity-Telemetry-Mods/NewStarGP-TelemetryMod/releases/download/v1%2C0%2C0/NewStarGPTelemetryMod-1.0.0.zip";

            string tempPath = await DownloadHelper.DownloadFileAsync(url);

            
            Console.WriteLine("Metadata Name: " + name + ", Installpath:" + installPath);

            try
            {
                dispatcher.ExtractToDirectory(tempPath, modFolder, true);
                dispatcher.DialogShow("Mod Installed!", DIALOG_TYPE.INFO, showChk: true);
            }
            catch (Exception e)
            {
                dispatcher.ShowNotification(NotificationType.ERROR, "Error extracting files: " + e.Message);
            }
        }

        

        
        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }

        public void Init()
        {
            this.settings = dispatcher.GetConfigObject<Config>();
            running = true;
            readThread = new Thread(new ThreadStart(ReadThread));
            readThread.Start();
        }

        private void ReadThread()
        {
            
            telem = new UdpTelemetry<Telemetry>(new UdpTelemetryConfig
            {
                ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port)
            });           
            

            while (running)
            {
                try
                {
                    foreach (var (i, key, value) in InputHelper.GetValues(telem.Receive()).WithIndex())
                        controller.SetInput(i, value);
                }
                catch(SocketException) { }
            }
            
        }

       
        public Dictionary<string, ParameterInfo[]> GetFeatures() => null;

        public Type GetConfigBody() =>typeof(Config);
        
    }


}