using RaceRoomPlugin;
using RaceRoomPlugin.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using YawGLAPI;
using R3E;
using System.Threading;
using System.Net.Sockets;
using R3E.Data;
using System.Runtime.InteropServices;
using static System.Net.WebRequestMethods;
using System.IO.MemoryMappedFiles;

namespace YawVR_Game_Engine.Plugin {
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Race Room")]
    [ExportMetadata("Version", "1.0")]

    class RaceRoomPlugin : Game
    {
        public int STEAM_ID => 211500;

        public string PROCESS_NAME => string.Empty;

        public bool PATCH_AVAILABLE => false;

        public string AUTHOR => "YawVR";

        public Stream Logo => GetStream("logo.png");

        public Stream SmallLogo => GetStream("recent.png");

        public Stream Background => GetStream("wide.png");

        public string Description => Resources.description;

        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;

        private bool stop = false;
        private Thread readThread;

        private bool Mapped
        {
            get { return (_file != null); }
        }
        private Shared _data;
        private MemoryMappedFile _file;
        private byte[] _buffer;
        private readonly TimeSpan _timeInterval = TimeSpan.FromMilliseconds(20);

        public LedEffect DefaultLED()
        {
            return new LedEffect(

           EFFECT_TYPE.FLOW_LEFTRIGHT,
           2,
           new YawColor[] {
                new YawColor(66, 135, 245),
                 new YawColor(80,80,80),
                new YawColor(128, 3, 117),
                new YawColor(110, 201, 12),
                },
           0.7f);
        }

        public List<Profile_Component> DefaultProfile()
        {
            return new List<Profile_Component>() {
                new Profile_Component(0,0, 1,1,0f,false,false,-1,1f), //yaw
                new Profile_Component(1,1, 1,1,0f,false,false,-1,1f), //pitch
                new Profile_Component(2,2, 1,1,0f,false,true,-1,1f), // roll
            };
        }

        public void Exit()
        {
            stop = true;
        }

        public Type GetConfigBody()
        {
            return typeof(Config);
        }

        public Dictionary<string, ParameterInfo[]> GetFeatures()
        {
            return null;
        }

        public string[] GetInputData()
        {
            return new string[] 
            { 
                "Yaw", "Pitch", "Roll" 
            };
        }

        public void Init()
        {
            Console.WriteLine("RaceRoom INIT");

            var pConfig = dispatcher.GetConfigObject<Config>();

            stop = false;
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();
        }

        private void ReadFunction()
        {
            var timeLast = DateTime.UtcNow;

            while (!stop)
            {
                var timeNow = DateTime.UtcNow;

                if (timeNow.Subtract(timeLast) < _timeInterval)
                {
                    Thread.Sleep(1);
                    continue;
                }

                timeLast = timeNow;

                if (Utilities.IsRrreRunning() && !Mapped)
                {
                    //Console.WriteLine("Found RRRE.exe, mapping shared memory...");

                    if (Map())
                    {
                        //Console.WriteLine("Memory mapped successfully");

                        _buffer = new Byte[Marshal.SizeOf(typeof(Shared))];
                    }
                }

                if (Mapped)
                {
                    Print();
                }
            }

            Dispose();
        }

        private bool Map()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) 
                {
                    _file = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }

            return false;
        }

        private bool Read()
        {
            try
            {
                var _view = _file.CreateViewStream();
                BinaryReader _stream = new BinaryReader(_view);
                _buffer = _stream.ReadBytes(Marshal.SizeOf(typeof(Shared)));
                GCHandle _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
                _data = (Shared)Marshal.PtrToStructure(_handle.AddrOfPinnedObject(), typeof(Shared));
                _handle.Free();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Print()
        {
            if (Read())
            {
                controller.SetInput(0, ToDegrees(_data.CarOrientation.Yaw));
                controller.SetInput(1, ToDegrees(_data.CarOrientation.Pitch));
                controller.SetInput(2, ToDegrees(_data.CarOrientation.Roll));
            }
        }

        float ToDegrees(float radians)
        {
            return (radians * (180.0f / (float)Math.PI));
        }

        public void Dispose()
        {
            _file.Dispose();
        }

        public void PatchGame()
        {
            return;
        }

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }

        Stream GetStream(string resourceName)
        {
            var assembly = GetType().Assembly;
            var rr = assembly.GetManifestResourceNames();
            string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";
            return assembly.GetManifestResourceStream(fullResourceName);
        }
    }
}
