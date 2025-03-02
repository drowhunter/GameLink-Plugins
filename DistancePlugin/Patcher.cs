using SharedLib;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static DistancePlugin.Patcher;

namespace DistancePlugin
{
    internal enum ModType
    {
        BepInEx5_x64,
        BepInEx6_x64,
        RaiPal
    }

    internal class  PatcherOptions
    {
        public ModType ModType { get; set; }

        public string GameInstallPath { get; set; }

        public string PluginName { get; set; }

        /// <summary>
        /// Path relative to the game install path where the doorstop_config.ini file is located.
        /// </summary>
        public string DoorStopPath { get; set; } = "";

        public GithubOptions Repository { get; set; }        

    }

    internal interface IPatcher
    {
        event EventHandler<PatcherFeedbackEventArgs> OnFeedback;

        event EventHandler<ExtractionEventArgs> OnExtractFiles;

        public event Question OnQuestion;

        Task<bool> IsPatchAvailableAsync(CancellationToken cancellationToken = default);
        Task<bool> IsPatchedAsync(CancellationToken cancellationToken = default);
        Task<bool> PatchAsync(CancellationToken cancellationToken = default);
        Task<bool> UnpatchAsync(CancellationToken cancellationToken = default);
    }

    internal abstract class Patcher : IPatcher
    {
        protected readonly PatcherOptions options;

        public event EventHandler<PatcherFeedbackEventArgs> OnFeedback;

        public event EventHandler<ExtractionEventArgs> OnExtractFiles;

        public delegate bool Question(string message);

        public event Question OnQuestion;


        protected delegate Task<bool> PatcherTask(CancellationToken cancellation);

        protected void Feedback(bool success, string message)
        {
            Log($"{ (success ? "" : "ERROR" )}: {message}");
            OnFeedback?.Invoke(this, new PatcherFeedbackEventArgs { Success = success, Message = message});
        }

        protected bool AskQuestion(string message)
        {
            return OnQuestion?.Invoke(message) ?? false;
        }

        protected void ExtractFiles(string source, string destination, bool overwrite = true)
        {
            Log($"Extract {Path.GetFileName(source)} to {destination}");
            OnExtractFiles?.Invoke(this, new ExtractionEventArgs { Source = source, Destination = destination, OverWrite = overwrite });
        }

        public static T Create<T>( Action<PatcherOptions> optionsAction) where T : class, IPatcher
        {
            
            var options = new PatcherOptions();
            optionsAction?.Invoke(options);

            return Activator.CreateInstance(typeof(T), options) as T;
        }

        protected Patcher(PatcherOptions options)
        {
            this.options = options;
        }

        public abstract Task<bool> IsPatchAvailableAsync(CancellationToken cancellationToken = default);

        public abstract Task<bool> IsPatchedAsync(CancellationToken cancellationToken = default);

        public virtual Task<bool> PatchAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                Log("Patching...");
                Log($"Patching {options.PluginName}");
                Log("Patching complete.");
                return true;
            });
        }

        public virtual Task<bool> UnpatchAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                Log("Unpatching...");
                Log($"Unpatching {options.PluginName}");
                Log("Unpatching complete.");
                return true;
            });
        }

        protected string PathCombine(params string[] segments) 
        {
            return Path.Combine(segments).Replace("\\","/");
        }

        protected void Log(string message)
        {
            Console.WriteLine(message);
        }

    }

    public class ExtractionEventArgs
    {
        /// <summary>
        /// This file needs extracting to <see cref="Destination"/>
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The destination path to extract the file to.
        /// </summary>
        public string Destination { get; set; }
        public bool OverWrite { get; internal set; }
    }

    

    public class PatcherFeedbackEventArgs
    {
        public bool Success { get; set; }

        public string Message { get; set; }
    }



    internal class UnityPatcher : Patcher
    {
        string doorStopPath => PathCombine(options.DoorStopPath, "doorstop_config.ini");

        string modFolder = null;

        List<GithubAsset> downloads = [];

        readonly Dictionary<ModType, (string version, string bitness)> modversion = new()
        {
            { ModType.BepInEx5_x64 , ("v5.4.23.2", "win_x64")}
        };

        public UnityPatcher(PatcherOptions options) : base(options)
        {
        }

        #region Implementation of IPatcher
        public override Task<bool> IsPatchAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public override Task<bool> IsPatchedAsync(CancellationToken cancellationToken = default)
        {
            Dictionary<string, PatcherTask> taskFunctions = new()
            {   
                { "Find BepInEx Plugins Dir",FindBepInExPluginsDirAsync},
                { "Reinstall If Plugin Found Async",AskReinstallAsync},
                
            };

            return RunTasks(taskFunctions, cancellationToken);
        }

        
        public override Task<bool> PatchAsync(CancellationToken cancellationToken = default)
        {
            Dictionary<string, PatcherTask> taskFunctions = new()
            {
                { "Install BepIn Ex",InstallBepInExAsync},
                { "Find BepInEx Plugins Dir",FindBepInExPluginsDirAsync},
                { "Reinstall If Plugin Found Async",AskReinstallAsync},
                { "DownloadPluginAsync",DownloadPluginAsync},
                { "InstallPluginAsync",InstallPluginAsync}
            };

            return RunTasks(taskFunctions, cancellationToken);            
        }

        

        public override Task<bool> UnpatchAsync(CancellationToken cancellationToken = default)
        {
            return base.UnpatchAsync(cancellationToken);
        }

        #endregion

        private async Task<bool> RunTasks(Dictionary<string, PatcherTask> taskFunctions, CancellationToken cancellationToken = default)
        {
            
            foreach (var (name,taskFunction) in taskFunctions)
            {
                Log($"Running task: [{name}]");
                if (!await taskFunction(cancellationToken))
                {
                    Log($"False task: [{name}]");
                    return false;
                }

                Log($"Complete task: [{name}]");
            }

            return true;
            
        }

        private async Task<bool> InstallBepInExAsync(CancellationToken cancellationToken = default)
        {
            Log("Installing BepInEx...");
            try
            {
                var doorStop = Path.Combine(options.GameInstallPath, doorStopPath);

                if (!File.Exists(doorStop))
                {
                    switch (options.ModType)
                    {
                        case ModType.RaiPal:
                            Feedback( false, "Please install UUVR RaiPal first");
                           
                            return false;
                        case ModType.BepInEx5_x64:
                        {
                            using var bepinex = GithubClient.Create(o =>
                            {
                                o.UsernameOrOrganization = "BepInEx";
                                o.Repository = "BepInEx";
                            });


                            var assets = await bepinex.DownloadByTagAsync(
                                modversion[options.ModType].version, 
                                [modversion[options.ModType].bitness], cancellationToken);

                            foreach (var asset in assets)  
                            {
                                ExtractFiles(asset.Location, options.GameInstallPath, true); 
                            }
                            break;
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Log("Error: " + e.Message);
                return false;
            }

            return true;


        }



        private async Task<bool> FindBepInExPluginsDirAsync(CancellationToken cancellationToken = default)
        {
            var doorStopIni = await IniHelper.LoadAsync(PathCombine(options.GameInstallPath, doorStopPath));

            string targetAssembly = doorStopIni["General"]["target_assembly"] ?? doorStopIni["General"]["targetAssembly"];

            if (string.IsNullOrWhiteSpace(targetAssembly))
            {
                Feedback(false, "target_assembly not found in doorstop_config.ini");
                return false;
            }

            
            var m = Regex.Match(targetAssembly, @"(?<modFolder>.*?BepInEx)[\\/]");

            if (m.Success)
            {
                modFolder = m.Groups["modFolder"].Value;
                modFolder = Path.Combine(!Regex.IsMatch(modFolder, @"^[a-zA-Z]:[\\/]") ? options.GameInstallPath : "", modFolder, "plugins/" + options.PluginName).Replace('\\', '/');
            }
            else
            {
                Feedback(false, $"Can't find BepInEx folder inside of \"{targetAssembly}\"");
                return false;
            }

            return true;
        }

        private Task<bool> AskReinstallAsync(CancellationToken cancellationToken = default)
        {
            bool quit = false;
            if (Directory.Exists(modFolder))
            {
                bool reinstall = AskQuestion("Mod already installed. Reinstall?");

                if (reinstall)
                {
                    try
                    {
                        Directory.Delete(modFolder, true);
                    }
                    catch (Exception e)
                    {
                        Feedback(false, "Error deleting existing mod: " + e.Message);
                        quit = true;
                    }
                }
            }

            return Task.FromResult(!quit);
               
        }

        private async Task<bool> DownloadPluginAsync(CancellationToken cancellationToken = default)
        {
            Log("Installing plugin...");
            try
            {
                using var github = GithubClient.Create(o =>
                {
                    o.UsernameOrOrganization = "Unity-Telemetry-Mods";
                    o.Repository = "Distance-TelemetryMod";
                });

                downloads = await github.DownloadLatestAsync(cancellationToken: cancellationToken);

                if(downloads.Count == 0)
                {
                    Feedback(false, "No release found.");
                    return false;
                }

                Log($"Downloaded plugin.");

                return true;
            }
            catch (Exception e)
            {
                Feedback(false, "Error extracting files: " + e.Message);
            }
            return false;
        }

        private Task<bool> InstallPluginAsync(CancellationToken cancellationToken = default)
        {
            Log("Installing plugin...");
            try
            {
                
                foreach (var asset in downloads)
                {
                    ExtractFiles(asset.Location, modFolder, true);                    
                }
                Log($"{downloads.Count} Plugins installed.");
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
               Feedback(false, "Error extracting files: " + e.Message);
            }
            return Task.FromResult(false);
        }

    }
}
