using IL2Plugin;
using IL2Plugin.Properties;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Threading;
using YawGLAPI;

namespace YawVR_Game_Engine.Plugin {
    [Export(typeof(Game))]
    [ExportMetadata("Name", "IL2")]
    [ExportMetadata("Version", "1.0")]

    class IL2Plugin : Game {

        struct STEEngineSetup
        {
            public short nIndex;
            public short nID;
            public float[] afPos;
            public float fMaxRPM;
        }

        struct STEGunSetup
        {
            public short nIndex;
            public float[] afPos;
            public float fProjectileMass;
            public float fShootVelocity;
        }

        struct STELandingGearSetup
        {
            public short nIndex;
            public short nID;
            public float[] afPos;
        }

        struct STEDropData
        {
            public float[] afPos;
            public float fMass;
            public ushort uFlags;
        }

        struct STERocketLaunch
        {
            public float[] afPos;
            public float fMass;
            public ushort uFlags;
        }

        struct STEHit
        {
            public float[] afPos;
            public float[] afHitF;
        }

        struct STEDamage
        {
            public float[] afPos;
            public float[] afHitF;
        }

        struct STEExplosion
        {
            public float[] afPos;
            public float fExpRad;
        }

        UdpClient udpClient;
        UdpClient telemetryUdpClient;
        private bool stop = false;
        private Thread readThread;
        private Thread telemetryThread;
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 4321);
        IPEndPoint telemetryRemote = new IPEndPoint(IPAddress.Any, 4322);
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;
        private Mutex mtx = new Mutex();

        public int STEAM_ID => 307960;

        public string AUTHOR => "YawVR";
        public string PROCESS_NAME => string.Empty;
        public bool PATCH_AVAILABLE => false;

        public string Description => Resources.description;

        public Stream Logo => GetStream("logo.png");
        public Stream SmallLogo => GetStream("recent.png");
        public Stream Background => GetStream("wide.png");

        public LedEffect DefaultLED() {
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

        public List<Profile_Component> DefaultProfile() {
            return new List<Profile_Component>() {
                new Profile_Component(0,0, 1,1,0f,false,false,-1,1f), //yaw
                new Profile_Component(1,1, 1,1,0f,false,false,-1,1f), //pitch
                new Profile_Component(2,2, 1,1,0f,false,true,-1,1f), // roll

                new Profile_Component(3,2, 1,1,0f,false,true,-1,1f), // roll
                new Profile_Component(4,1, 1,1,0f,false,true,-1,1f), // pitch
            };
        }

        public void Exit() {
            udpClient.Close();
            udpClient = null;
            telemetryUdpClient?.Close();
            telemetryUdpClient = null;
            stop = true;
          //  readThread.Abort();
        }

        public string[] GetInputData() {
            return new string[] {
"Yaw","Pitch","Roll","Velocity_X","Velocity_Y","Velocity_Z","Acceleration_X","Acceleration_Y","Acceleration_Z",

"ENG_RPM", "ENG_MP", "ENG_SHAKE_FRQ", "ENG_SHAKE_AMP", "LGEARS_STATE", "LGEARS_PRESS", "EAS", "AOA", "ACCELERATION", "COCKPIT_SHAKE", "AGL", "FLAPS", "AIR_BRAKES",

"SETUP_ENG_PosX","SETUP_ENG_PosY","SETUP_ENG_PosZ","SETUP_ENG_MaxRPM",
"SETUP_GUN_PosX","SETUP_GUN_PosY","SETUP_GUN_PosZ","SETUP_GUN_ProjectileMass","SETUP_GUN_ShootVelocity",
"SETUP_LGEAR_PosX","SETUP_LGEAR_PosY","SETUP_LGEAR_PosZ","SETUP_LGEAR_Distance",
"DROP_BOMB_PosX","DROP_BOMB_PosY","DROP_BOMB_PosZ","DROP_BOMB_Mass","DROP_BOMB_Distance",
"ROCKET_LAUNCH_PosX","ROCKET_LAUNCH_PosY","ROCKET_LAUNCH_PosZ","ROCKET_LAUNCH_Mass","ROCKET_LAUNCH_Distance",
"HIT_PosX","HIT_PosY","HIT_PosZ","HIT_ForceX","HIT_ForceY","HIT_ForceZ", "HIT_ForceLength",
"DAMAGE_PosX","DAMAGE_PosY","DAMAGE_PosZ","DAMAGE_ForceX","DAMAGE_ForceY","DAMAGE_ForceZ", "DAMAGE_ForceLength",
"EXPLOSION_PosX","EXPLOSION_PosY","EXPLOSION_PosZ","EXPLOSION_Radius","EXPLOSION_Distance",

            };
        }

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }
        public void Init() {
            Console.WriteLine("IL2 INIT");
            stop = false;
            var pConfig = dispatcher.GetConfigObject<Config>();
            udpClient = new UdpClient(pConfig.Port);
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();

            telemetryUdpClient = new UdpClient(pConfig.TelemetryPort);
            telemetryThread = new Thread(ReadTelemetryData);
            telemetryThread.Start();
        }


        
        private void ReadFunction() {

            while (!stop) {

                try
                {
                    byte[] rawData = udpClient.Receive(ref remote);

                    float yaw = ReadSingle(rawData, 8, true) * 57.3f;
                    float pitch = ReadSingle(rawData, 12, true) * 57.3f;
                    float roll = ReadSingle(rawData, 16, true) * 57.3f;

                    float velocityX = ReadSingle(rawData, 20, true) * 57.3f;
                    float velocityY = ReadSingle(rawData, 24, true) * 57.3f;
                    float velocityZ = ReadSingle(rawData, 28, true) * 57.3f;


                    float accX = ReadSingle(rawData, 32, true);
                    float accY = ReadSingle(rawData, 36, true);
                    float accZ = ReadSingle(rawData, 40, true);
                
                    if (true == mtx.WaitOne(100)) 
                    {
                        controller.SetInput(0, yaw);
                        controller.SetInput(1, pitch);
                        controller.SetInput(2, roll);

                        controller.SetInput(3, velocityX);
                        controller.SetInput(4, velocityY);
                        controller.SetInput(5, velocityZ);

                        controller.SetInput(6, accX);
                        controller.SetInput(7, accY);
                        controller.SetInput(8, accZ);

                        mtx.ReleaseMutex();
                    }
                }
                catch (SocketException) { }

            }
        }

        public void ReadTelemetryData()
        {
            while (!stop)
            {
                try
                {
                    int nId = 9;

                    byte[] rawData = telemetryUdpClient.Receive(ref telemetryRemote);
                    int indicatorsCount = rawData[10];
                    int offset = 11;

                    mtx.WaitOne(100);
                    for (int i = 0; i < indicatorsCount; i++)
                    {
                        int indicatorId = BitConverter.ToUInt16(rawData, offset);
                        int valuesCount = rawData[offset + 2];
                        offset += 3;

                        for (int j = 0; j < valuesCount; j++)
                        {
                            float value = ReadSingle(rawData, offset, true);
                            offset += 4;

                            if (true == mtx.WaitOne(100))
                            {
                                controller.SetInput(nId++, value);

                                mtx.ReleaseMutex();
                            }
                        }
                    }
                    mtx.ReleaseMutex();

                    int eventsCount = rawData[offset];
                    offset += 1;

                    for (int i = 0; i < eventsCount; i++)
                    {
                        int eventId = BitConverter.ToUInt16(rawData, offset);
                        int eventSize = rawData[offset + 2];
                        offset += 3;

                        switch (eventId)
                        {
                            case 1: // SETUP_ENG
                                STEEngineSetup engineSetup = new STEEngineSetup
                                {
                                    nIndex = BitConverter.ToInt16(rawData, offset),
                                    nID = BitConverter.ToInt16(rawData, offset + 2),
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true),
                                        ReadSingle(rawData, offset + 12, true)
                                    },
                                    fMaxRPM = ReadSingle(rawData, offset + 16, true)
                                };

                                if (true == mtx.WaitOne(100)) 
                                {
                                    controller.SetInput(nId++, engineSetup.afPos[0]);
                                    controller.SetInput(nId++, engineSetup.afPos[1]);
                                    controller.SetInput(nId++, engineSetup.afPos[2]);
                                    controller.SetInput(nId++, engineSetup.fMaxRPM);

                                    mtx.ReleaseMutex();
                                }
                                    
                                break;
                            case 2: // SETUP_GUN
                                STEGunSetup gunSetup = new STEGunSetup
                                {
                                    nIndex = BitConverter.ToInt16(rawData, offset),
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset + 2, true),
                                        ReadSingle(rawData, offset + 6, true),
                                        ReadSingle(rawData, offset + 10, true)
                                    },
                                    fProjectileMass = ReadSingle(rawData, offset + 14, true),
                                    fShootVelocity = ReadSingle(rawData, offset + 18, true)
                                };

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, gunSetup.afPos[0]);
                                    controller.SetInput(nId++, gunSetup.afPos[1]);
                                    controller.SetInput(nId++, gunSetup.afPos[2]);
                                    controller.SetInput(nId++, gunSetup.fProjectileMass);
                                    controller.SetInput(nId++, gunSetup.fShootVelocity);

                                    mtx.ReleaseMutex();
                                }

                                break;
                            case 3: // SETUP_LGEAR
                                STELandingGearSetup landingGearSetup = new STELandingGearSetup
                                {
                                    nIndex = BitConverter.ToInt16(rawData, offset),
                                    nID = BitConverter.ToInt16(rawData, offset + 2),
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true),
                                        ReadSingle(rawData, offset + 12, true)
                                    }
                                };

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, landingGearSetup.afPos[0]);
                                    controller.SetInput(nId++, landingGearSetup.afPos[1]);
                                    controller.SetInput(nId++, landingGearSetup.afPos[2]);

                                    // saját
                                    controller.SetInput(nId++, new Vector3(landingGearSetup.afPos[0], landingGearSetup.afPos[1], landingGearSetup.afPos[2]).Length());

                                    mtx.ReleaseMutex();
                                }

                                break;
                            case 4: // DROP_BOMB
                                STEDropData dropData = new STEDropData
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    fMass = ReadSingle(rawData, offset + 12, true),
                                    uFlags = BitConverter.ToUInt16(rawData, offset + 16)
                                };

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, dropData.afPos[0]);
                                    controller.SetInput(nId++, dropData.afPos[1]);
                                    controller.SetInput(nId++, dropData.afPos[2]);
                                    controller.SetInput(nId++, dropData.fMass);

                                    // saját
                                    controller.SetInput(nId++, new Vector3(dropData.afPos[0], dropData.afPos[1], dropData.afPos[2]).Length());

                                    mtx.ReleaseMutex();
                                }
                                break;
                            case 5: // ROCKET_LAUNCH
                                STERocketLaunch rocketLaunchData = new STERocketLaunch
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    fMass = ReadSingle(rawData, offset + 12, true),
                                    uFlags = BitConverter.ToUInt16(rawData, offset + 16)
                                };

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, rocketLaunchData.afPos[0]);
                                    controller.SetInput(nId++, rocketLaunchData.afPos[1]);
                                    controller.SetInput(nId++, rocketLaunchData.afPos[2]);
                                    controller.SetInput(nId++, rocketLaunchData.fMass);

                                    // saját
                                    controller.SetInput(nId++, new Vector3(rocketLaunchData.afPos[0], rocketLaunchData.afPos[1], rocketLaunchData.afPos[2]).Length());

                                    mtx.ReleaseMutex();
                                }

                                break;
                            case 6: // HIT
                                STEHit hitData = new STEHit
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    afHitF = new float[] {
                                        ReadSingle(rawData, offset + 12, true),
                                        ReadSingle(rawData, offset + 16, true),
                                        ReadSingle(rawData, offset + 20, true)
                                    }
                                };

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, hitData.afPos[0]);
                                    controller.SetInput(nId++, hitData.afPos[1]);
                                    controller.SetInput(nId++, hitData.afPos[2]);
                                    controller.SetInput(nId++, hitData.afHitF[0]);
                                    controller.SetInput(nId++, hitData.afHitF[1]);
                                    controller.SetInput(nId++, hitData.afHitF[2]);

                                    // saját
                                    controller.SetInput(nId++, new Vector3(hitData.afHitF[0], hitData.afHitF[1], hitData.afHitF[2]).Length());

                                    mtx.ReleaseMutex();
                                }

                                break;
                            case 7: // DAMAGE
                                STEDamage damageData = new STEDamage
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    afHitF = new float[] {
                                        ReadSingle(rawData, offset + 12, true),
                                        ReadSingle(rawData, offset + 16, true),
                                        ReadSingle(rawData, offset + 20, true)
                                    }
                                };

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, damageData.afPos[0]);
                                    controller.SetInput(nId++, damageData.afPos[1]);
                                    controller.SetInput(nId++, damageData.afPos[2]);
                                    controller.SetInput(nId++, damageData.afHitF[0]);
                                    controller.SetInput(nId++, damageData.afHitF[1]);
                                    controller.SetInput(nId++, damageData.afHitF[2]);

                                    // saját
                                    controller.SetInput(nId++, new Vector3(damageData.afHitF[0], damageData.afHitF[1], damageData.afHitF[2]).Length());

                                    mtx.ReleaseMutex();
                                }

                                break;
                            case 8: // EXPLOSION
                                STEExplosion explosionData = new STEExplosion
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    fExpRad = ReadSingle(rawData, offset + 12, true)
                                };

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, explosionData.afPos[0]);
                                    controller.SetInput(nId++, explosionData.afPos[0]);
                                    controller.SetInput(nId++, explosionData.afPos[0]);
                                    controller.SetInput(nId++, explosionData.fExpRad);

                                    // saját
                                    controller.SetInput(nId++, new Vector3(explosionData.afPos[0], explosionData.afPos[1], explosionData.afPos[2]).Length());

                                    mtx.ReleaseMutex();
                                }

                                break;
                            case 9: // GUN_FIRE
                                byte gunIndex = rawData[offset];

                                if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, (float)gunIndex);

                                    mtx.ReleaseMutex();
                                }

                                break;
                        }
                        offset += eventSize;
                    }
                }
                catch (SocketException) { }
            }
        }

        public static float ReadSingle(byte[] data, int offset, bool littleEndian)
        {
            if (BitConverter.IsLittleEndian != littleEndian)
            {   // other-endian; reverse this portion of the data (4 bytes)
                byte tmp = data[offset];
                data[offset] = data[offset + 3];
                data[offset + 3] = tmp;
                tmp = data[offset + 1];
                data[offset + 1] = data[offset + 2];
                data[offset + 2] = tmp;
            }
            return BitConverter.ToSingle(data, offset);
        }


        public void PatchGame()
        {
            return;
        }
        public Dictionary<string, ParameterInfo[]> GetFeatures()
        {
            return null;
        }

        Stream GetStream(string resourceName)
        {
            var assembly = GetType().Assembly;
            var rr = assembly.GetManifestResourceNames();
            string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";
            return assembly.GetManifestResourceStream(fullResourceName);
        }

        public Type GetConfigBody()
        {
            return typeof(Config);
        }
    }

}

