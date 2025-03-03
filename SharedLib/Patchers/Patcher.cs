using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using YawGLAPI;

#nullable disable

namespace SharedLib
{
    internal enum ModType
    {
        BepInEx5_x64,
        //BepInEx6_x64,
        RaiPal
    }

    internal class  PatcherOptions
    {
        public ModType ModType { get; set; }

        public string GameInstallPath { get; set; }

        public string PluginName { get; set; }

        public GithubOptions  Repository { get; set; }        

    }

    internal delegate Task<bool> PatcherTask(CancellationToken cancellation);

    internal class TaskList : Dictionary<string, PatcherTask>
    {      
        public new TaskList Add(string name, PatcherTask task)
        {
            Add(name, task);
            return this;
        }
    }

    internal interface IPatcher
    {
        Task<bool> PatchAsync(CancellationToken cancellationToken = default);
    }

    internal abstract class Patcher<TOptions> : IPatcher where TOptions:PatcherOptions,new()
    {
        protected readonly IMainFormDispatcher dispatcher;

        /// <summary>
        /// Options for the patcher
        /// </summary>
        protected readonly TOptions options;

        /// <summary>
        /// List if task functions to run in order. Returning false will stop the patching process.
        /// </summary>
        protected abstract TaskList taskList { get; }

        public static IPatcher Create<T>(IMainFormDispatcher dispatcher, Action<TOptions> optionsAction) where T : Patcher<TOptions>
        {
            var options = new TOptions();
            optionsAction?.Invoke(options);

            return Activator.CreateInstance(typeof(T), dispatcher, options) as T;
        }

        protected void Feedback(bool success, string message)
        {
            Log($"{ (success ? "" : "ERROR" )}: {message}");
            dispatcher.DialogShow(message, DIALOG_TYPE.INFO);
        }

        protected bool AskQuestion(string message)
        {
            bool result = false;
            dispatcher.DialogShow(message, DIALOG_TYPE.QUESTION, (yes) => result = yes, no => result = no);
            return result;
        }

        protected void ExtractFiles(string source, string destination, bool overwrite = true)
        {
            Log($"Extract {Path.GetFileName(source)} to {destination}");
            dispatcher.ExtractToDirectory(source, destination, overwrite);
        }

        

        protected Patcher(IMainFormDispatcher dispatcher, TOptions options)
        {
            this.dispatcher = dispatcher;
            this.options = options;
        }

        async Task<bool> IPatcher.PatchAsync(CancellationToken cancellationToken = default)
        {
            Log("Patching...");

            bool success = await RunTasks(taskList, cancellationToken);
            
            if(success)
                Feedback(success, "Patching complete.");
            else
                Feedback(success, "Patching failed.");

            return success;
        }

        private async Task<bool> RunTasks(Dictionary<string, PatcherTask> taskFunctions, CancellationToken cancellationToken = default)
        {

            foreach (var (name, taskFunction) in taskFunctions)
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
        protected string PathCombine(params string[] segments) 
        {
            return Path.Combine(segments).Replace("\\","/");
        }

        protected void Log(string message)
        {
            Console.WriteLine(message);
        }

        
    }
}
