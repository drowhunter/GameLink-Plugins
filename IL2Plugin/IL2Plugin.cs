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
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Linq;
using YawGLAPI;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        public bool PATCH_AVAILABLE => true;

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
            };
        }

        public void Exit() 
        {
            stop = true;

            readThread?.Join();
            readThread = null;
            telemetryThread?.Join();
            telemetryThread = null;

            udpClient?.Close();
            udpClient?.Dispose();
            udpClient = null;
            telemetryUdpClient?.Close();
            telemetryUdpClient?.Dispose();
            telemetryUdpClient = null;
        }

        public string[] GetInputData() {
            return new string[] {
"Yaw","Pitch","Roll","PlayerRotateVelocity_X","PlayerRotateVelocity_Y","PlayerRotateVelocity_Z","PlayerAcceleration_X","PlayerAcceleration_Y","PlayerAcceleration_Z",

"ACCELERATION",
//"ENG_RPM", "ENG_MP", "ENG_SHAKE_FRQ", "ENG_SHAKE_AMP", "LGEARS_STATE", "LGEARS_PRESS", "EAS", "AOA", "ACCELERATION", "COCKPIT_SHAKE", "AGL", "FLAPS", "AIR_BRAKES",

//"SETUP_ENG_PosX","SETUP_ENG_PosY","SETUP_ENG_PosZ","SETUP_ENG_MaxRPM",
//"SETUP_GUN_PosX","SETUP_GUN_PosY","SETUP_GUN_PosZ","SETUP_GUN_ProjectileMass","SETUP_GUN_ShootVelocity",
//"SETUP_LGEAR_PosX","SETUP_LGEAR_PosY","SETUP_LGEAR_PosZ","SETUP_LGEAR_Distance",

"DROP_BOMB_Event",/*"DROP_BOMB_PosX","DROP_BOMB_PosY","DROP_BOMB_PosZ","DROP_BOMB_Mass",*/"DROP_BOMB_Distance",
"ROCKET_LAUNCH_Event",/*"ROCKET_LAUNCH_PosX","ROCKET_LAUNCH_PosY","ROCKET_LAUNCH_PosZ","ROCKET_LAUNCH_Mass",*/"ROCKET_LAUNCH_Distance",
"HIT_Event",/*"HIT_PosX","HIT_PosY","HIT_PosZ","HIT_ForceX","HIT_ForceY","HIT_ForceZ",*/ "HIT_Force",
"DAMAGE_Event",/*"DAMAGE_PosX","DAMAGE_PosY","DAMAGE_PosZ","DAMAGE_ForceX","DAMAGE_ForceY","DAMAGE_ForceZ",*/ "DAMAGE_Force",
"EXPLOSION_Event",/*"EXPLOSION_PosX","EXPLOSION_PosY","EXPLOSION_PosZ","EXPLOSION_Radius",*/"EXPLOSION_Distance",
"Gun_Event",/*"GunIndex"*/
"SETUP_GUN_PosX","SETUP_GUN_PosY","SETUP_GUN_PosZ"
            };
        }

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }

        Config pConfig;
        public void Init() {
            Console.WriteLine("IL2 INIT");
            stop = false;
            pConfig = dispatcher.GetConfigObject<Config>();
            udpClient = new UdpClient(pConfig.Port);
            udpClient.Client.ReceiveTimeout = 500;
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();

            telemetryUdpClient = new UdpClient(pConfig.TelemetryPort);
            telemetryUdpClient.Client.ReceiveTimeout = 500;
            telemetryThread = new Thread(ReadTelemetryData);
            telemetryThread.Start();
        }



        private void ReadFunction()
        {

            while (!stop)
            {

                try
                {
                    byte[] rawData = udpClient.Receive(ref remote);

                    float yaw = ReadSingle(rawData, 8, true) * 57.3f;
                    float pitch = ReadSingle(rawData, 12, true) * 57.3f;
                    float roll = ReadSingle(rawData, 16, true) * 57.3f;

                    yaw *= -1.0f;

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
                catch (Exception ex) { }

            }
        }

        DateTime startDropData;
        STEDropData dropData;

        DateTime startRocketLaunchData;
        STERocketLaunch rocketLaunchData;

        DateTime startHitData;
        STEHit hitData;

        DateTime startDamageData;
        STEDamage damageData;

        DateTime startExplosionData;
        STEExplosion explosionData;

        DateTime startGunIndex;
        byte gunIndex;

        DateTime startGunPos;
        Vector3 v3GunPos = new Vector3();

        public void ReadTelemetryData()
        {
            startDropData = new DateTime(1970, 1, 1);
            startRocketLaunchData = new DateTime(1970, 1, 1);
            startHitData = new DateTime(1970, 1, 1);
            startDamageData = new DateTime(1970, 1, 1);
            startExplosionData = new DateTime(1970, 1, 1);
            startGunIndex = new DateTime(1970, 1, 1);
            startGunPos = new DateTime(1970, 1, 1);

            while (!stop)
            {

                try
                {
                    int nId = 9;

                    byte[] rawData = telemetryUdpClient.Receive(ref telemetryRemote);
                    int indicatorsCount = rawData[10];
                    int offset = 11;

                    //mtx.WaitOne(100);
                    for (int i = 0; i < indicatorsCount; i++)
                    {
                        int indicatorId = BitConverter.ToUInt16(rawData, offset);

                        int valuesCount = rawData[offset + 2];
                        offset += 3;

                        Vector3 v3Acceleration = new Vector3(0, 0, 0);
                        for (int j = 0; j < valuesCount; j++)
                        {
                            float value = ReadSingle(rawData, offset, true);
                            offset += 4;

                            /*if (true == mtx.WaitOne(100))
                            {
                                controller.SetInput(nId++, value);

                                mtx.ReleaseMutex();
                            }*/

                            // ACCELERATION
                            if (8 == indicatorId) 
                            {
                                if (0 == j) { v3Acceleration.X = value; }
                                if (1 == j) { v3Acceleration.Y = value; }
                                if (2 == j) { v3Acceleration.Z = value; }
                            }
                        }

                        // ACCELERATION
                        if (8 == indicatorId) 
                        {
                            v3Acceleration.Y -= 9.81f;
                            float fAcceleration = v3Acceleration.Length();
                            if (true == mtx.WaitOne(100)) 
                            {
                                controller.SetInput(nId++, fAcceleration);

                                mtx.ReleaseMutex();
                            }
                        }

                    }
                    //mtx.ReleaseMutex();

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
                                /*STEEngineSetup engineSetup = new STEEngineSetup
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
                                }*/
                                    
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

                                v3GunPos = new Vector3(gunSetup.afPos[0], gunSetup.afPos[1], gunSetup.afPos[2]);

                                startGunPos = DateTime.Now;

                                /*if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, gunSetup.afPos[0]);
                                    controller.SetInput(nId++, gunSetup.afPos[1]);
                                    controller.SetInput(nId++, gunSetup.afPos[2]);
                                    controller.SetInput(nId++, gunSetup.fProjectileMass);
                                    controller.SetInput(nId++, gunSetup.fShootVelocity);

                                    mtx.ReleaseMutex();
                                }*/

                                break;
                            case 3: // SETUP_LGEAR
                                /*STELandingGearSetup landingGearSetup = new STELandingGearSetup
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
                                }*/

                                break;
                            case 4: // DROP_BOMB
                                dropData = new STEDropData
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    fMass = ReadSingle(rawData, offset + 12, true),
                                    uFlags = BitConverter.ToUInt16(rawData, offset + 16)
                                };

                                startDropData = DateTime.Now;

                                break;
                            case 5: // ROCKET_LAUNCH
                                rocketLaunchData = new STERocketLaunch
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    fMass = ReadSingle(rawData, offset + 12, true),
                                    uFlags = BitConverter.ToUInt16(rawData, offset + 16)
                                };

                                startRocketLaunchData = DateTime.Now;

                                break;
                            case 6: // HIT
                                hitData = new STEHit
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

                                startHitData = DateTime.Now;

                                break;
                            case 7: // DAMAGE
                                damageData = new STEDamage
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

                                startDamageData = DateTime.Now;

                                break;
                            case 8: // EXPLOSION
                                explosionData = new STEExplosion
                                {
                                    afPos = new float[] {
                                        ReadSingle(rawData, offset, true),
                                        ReadSingle(rawData, offset + 4, true),
                                        ReadSingle(rawData, offset + 8, true)
                                    },
                                    fExpRad = ReadSingle(rawData, offset + 12, true)
                                };

                                startExplosionData = DateTime.Now;

                                break;
                            case 9: // GUN_FIRE
                                gunIndex = rawData[offset];

                                startGunIndex = DateTime.Now;

                                /*if (true == mtx.WaitOne(100))
                                {
                                    controller.SetInput(nId++, (float)gunIndex);

                                    mtx.ReleaseMutex();
                                }*/

                                break;
                        }
                        offset += eventSize;
                    }

                    ;

                    UInt64 nDropDataMilliseconds = (UInt64)(DateTime.Now - startDropData).TotalMilliseconds;
                    UInt64 nRocketLaunchDataMilliseconds = (UInt64)(DateTime.Now - startRocketLaunchData).TotalMilliseconds;
                    UInt64 nHitDataMilliseconds = (UInt64)(DateTime.Now - startHitData).TotalMilliseconds;
                    UInt64 nDamageDataMilliseconds = (UInt64)(DateTime.Now - startDamageData).TotalMilliseconds;
                    UInt64 nExplosionDataMilliseconds = (UInt64)(DateTime.Now - startExplosionData).TotalMilliseconds;
                    UInt64 nGunIndexMilliseconds = (UInt64)(DateTime.Now - startGunIndex).TotalMilliseconds;
                    UInt64 nGunPosMilliseconds = (UInt64)(DateTime.Now - startGunPos).TotalMilliseconds;

                    // DropDataMilliseconds
                    if (nDropDataMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 1.0f);

                            //controller.SetInput(nId++, dropData.afPos[0]);
                            //controller.SetInput(nId++, dropData.afPos[1]);
                            //controller.SetInput(nId++, dropData.afPos[2]);
                            //controller.SetInput(nId++, dropData.fMass);

                            // saját
                            controller.SetInput(nId++, new Vector3(dropData.afPos[0], dropData.afPos[1], dropData.afPos[2]).Length());

                            mtx.ReleaseMutex();
                        }
                    }
                    else
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 0.0f);

                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);

                            // saját
                            controller.SetInput(nId++, 0.0f);

                            mtx.ReleaseMutex();
                        }
                    }

                    // RocketLaunchData
                    if (nRocketLaunchDataMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 1.0f);

                            //controller.SetInput(nId++, rocketLaunchData.afPos[0]);
                            //controller.SetInput(nId++, rocketLaunchData.afPos[1]);
                            //controller.SetInput(nId++, rocketLaunchData.afPos[2]);
                            //controller.SetInput(nId++, rocketLaunchData.fMass);

                            // saját
                            controller.SetInput(nId++, new Vector3(rocketLaunchData.afPos[0], rocketLaunchData.afPos[1], rocketLaunchData.afPos[2]).Length());

                            mtx.ReleaseMutex();
                        }
                    }
                    else
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 0.0f);

                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);

                            // saját
                            controller.SetInput(nId++, 0.0f);

                            mtx.ReleaseMutex();
                        }
                    }

                    // HitDataMilliseconds
                    if (nHitDataMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 1.0f);

                            //controller.SetInput(nId++, hitData.afPos[0]);
                            //controller.SetInput(nId++, hitData.afPos[1]);
                            //controller.SetInput(nId++, hitData.afPos[2]);
                            //controller.SetInput(nId++, hitData.afHitF[0]);
                            //controller.SetInput(nId++, hitData.afHitF[1]);
                            //controller.SetInput(nId++, hitData.afHitF[2]);

                            // saját
                            controller.SetInput(nId++, new Vector3(hitData.afHitF[0], hitData.afHitF[1], hitData.afHitF[2]).Length());

                            mtx.ReleaseMutex();
                        }
                    }
                    else
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 0.0f);

                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);

                            // saját
                            controller.SetInput(nId++, 0.0f);

                            mtx.ReleaseMutex();
                        }
                    }

                    // DamageDataMilliseconds
                    if (nDamageDataMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 1.0f);

                            //controller.SetInput(nId++, damageData.afPos[0]);
                            //controller.SetInput(nId++, damageData.afPos[1]);
                            //controller.SetInput(nId++, damageData.afPos[2]);
                            //controller.SetInput(nId++, damageData.afHitF[0]);
                            //controller.SetInput(nId++, damageData.afHitF[1]);
                            //controller.SetInput(nId++, damageData.afHitF[2]);

                            // saját
                            controller.SetInput(nId++, new Vector3(damageData.afHitF[0], damageData.afHitF[1], damageData.afHitF[2]).Length());

                            mtx.ReleaseMutex();
                        }
                    }
                    else
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 0.0f);

                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);

                            // saját
                            controller.SetInput(nId++, 0.0f);

                            mtx.ReleaseMutex();
                        }
                    }

                    // ExplosionDataMilliseconds
                    if (nExplosionDataMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 1.0f);

                            //controller.SetInput(nId++, explosionData.afPos[0]);
                            //controller.SetInput(nId++, explosionData.afPos[1]);
                            //controller.SetInput(nId++, explosionData.afPos[2]);
                            //controller.SetInput(nId++, explosionData.fExpRad);

                            // saját
                            controller.SetInput(nId++, new Vector3(explosionData.afPos[0], explosionData.afPos[1], explosionData.afPos[2]).Length());

                            mtx.ReleaseMutex();
                        }
                    }
                    else
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 0.0f);

                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);
                            //controller.SetInput(nId++, 0.0f);

                            // saját
                            controller.SetInput(nId++, 0.0f);

                            mtx.ReleaseMutex();
                        }
                    }

                    // GunIndexMilliseconds
                    if (nGunIndexMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 1.0f);

                            //controller.SetInput(nId++, (float)gunIndex);

                            mtx.ReleaseMutex();
                        }
                    }
                    else
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, 0.0f);

                            //controller.SetInput(nId++, 0.0f);

                            mtx.ReleaseMutex();
                        }
                    }

                    // GunPosMilliseconds
                    if (nGunPosMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, v3GunPos.X);
                            controller.SetInput(nId++, v3GunPos.Y);
                            controller.SetInput(nId++, v3GunPos.Z);

                            mtx.ReleaseMutex();
                        }
                    }
                    else
                    {
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, v3GunPos.X);
                            controller.SetInput(nId++, v3GunPos.Y);
                            controller.SetInput(nId++, v3GunPos.Z);

                            mtx.ReleaseMutex();
                        }
                    }

                }
                catch (Exception ex) { }
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
            string name = "IL-2 Sturmovik Battle of Stalingrad";

            string installPath = dispatcher.GetInstallPath(name);
            if (!Directory.Exists(installPath))
            {
                dispatcher.DialogShow("Cant find 'IL-2 Sturmovik Battle of Stalingrad' install directory\nOpen Plugin manager?", DIALOG_TYPE.QUESTION, delegate {
                    dispatcher.OpenPluginManager();
                });

                return;
            }

            string strFilename = installPath + "/data/startup.cfg";

            if (true == File.Exists(strFilename)) 
            {
                // Fájl beolvasása
                string fileContent = File.ReadAllText(strFilename);

                bool bIsUpdated = false;

                // 1. motiondevice szekció hozzáadása, ha nem létezik
                if (!fileContent.Contains("[KEY = motiondevice]"))
                {
                    string motionDeviceSection = "[KEY = motiondevice]\n" +
                                                 "  addr = \"127.0.0.1\"\n" +
                                                 "  decimation = 1\n" +
                                                 "  enable = true\n" +
                                                 "  port = 4321\n" +
                                                 "[END]\n\n";
                    fileContent += motionDeviceSection; // hozzáadjuk a fájl végére

                    bIsUpdated = true;
                }

                // 2. telemetrydevice szekció hozzáadása, ha nem létezik
                if (!fileContent.Contains("[KEY = telemetrydevice]"))
                {
                    string telemetryDeviceSection = "[KEY = telemetrydevice]\n" +
                                                    "   addr = \"127.0.0.1\"\n" +
                                                    "   decimation = 1\n" +
                                                    "   enable = true\n" +
                                                    "   port = 4322\n" +
                                                    "[END]\n\n";
                    fileContent += telemetryDeviceSection; // hozzáadjuk a fájl végére

                    bIsUpdated = true;
                }

                if (true == bIsUpdated)
                {
                    // A fájl újra mentése
                    File.WriteAllText(strFilename, fileContent);
                }
            }

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

