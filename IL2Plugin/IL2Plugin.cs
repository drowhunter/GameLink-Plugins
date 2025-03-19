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

"ENG_RPM_1","ENG_RPM_2","ENG_RPM_3","ENG_RPM_4",
"ENG_MP_1","ENG_MP_2","ENG_MP_3","ENG_MP_4",
"ENG_SHAKE_FRQ_1","ENG_SHAKE_FRQ_2","ENG_SHAKE_FRQ_3","ENG_SHAKE_FRQ_4",
"ENG_SHAKE_AMP_1","ENG_SHAKE_AMP_2","ENG_SHAKE_AMP_3","ENG_SHAKE_AMP_4",
"LGEARS_STATE_1","LGEARS_STATE_2","LGEARS_STATE_3","LGEARS_STATE_4",
"LGEARS_PRESS_1","LGEARS_PRESS_2","LGEARS_PRESS_3","LGEARS_PRESS_4",
"EAS",
"AOA",
"ACCELERATION",
"COCKPIT_SHAKE_Hz","COCKPIT_SHAKE_Amplitude",
"AGL",
"FLAPS",
"AIR_BRAKES",

"DROP_BOMB_Event","DROP_BOMB_Distance",
"ROCKET_LAUNCH_Event","ROCKET_LAUNCH_Distance",
"HIT_Event","HIT_Force",
"DAMAGE_Event","DAMAGE_Force",
"EXPLOSION_Event","EXPLOSION_Distance",
"Gun_Event",
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

            m_fElapsedYaw = 0.0f;
            m_fCurrentYaw = 0.0f;
            m_fYawDt = 0.0f;
            m_fYaw2 = 0.0f;
            m_bFirst = true;

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



        private float m_fElapsedYaw = 0.0f;
        private float m_fCurrentYaw = 0.0f;
        private float m_fYawDt = 0.0f;
        private bool m_bFirst = true;
        private float m_fYaw2 = 0.0f;

        float ToRadian(float fDegree) 
        {
            float fRandian = (fDegree / 180.0f) * 3.141592654f;
            return fRandian;
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

                    // Jump Limit
                    if (true == m_bFirst) 
                    {
                        m_fElapsedYaw = m_fCurrentYaw = yaw;
                        m_bFirst = false;
                    }
                    m_fElapsedYaw = m_fCurrentYaw;
                    m_fCurrentYaw = yaw;
                    m_fYawDt = m_fCurrentYaw - m_fElapsedYaw;
                    if (Math.Abs(m_fYawDt) > pConfig.YawJumpLimit) 
                    {
                        m_fYawDt = Math.Sign(m_fYawDt) * pConfig.YawJumpLimit;
                    }
                    m_fYaw2 += m_fYawDt;

                    ;

                    // Roll Limit
                    if (roll < pConfig.RollLimitMin) { roll = pConfig.RollLimitMin; }
                    if (roll > pConfig.RollLimitMax) { roll = pConfig.RollLimitMax; }

                    float roll2 = roll;
                    if (roll > 0.0f)
                    {
                        float t = roll / pConfig.RollLimitMax; // -> [0.0 .. 1.0]
                        float x = t * 1.0f; // -> [0.0 .. 1.0]
                        float y = 0.6f * (float)Math.Sin((double)x * 1.55);
                        float weight = y / 1.0f;
                        roll2 = weight * pConfig.RollLimitMax;
                    }
                    else if (roll < 0.0f)
                    {
                        float t = roll / pConfig.RollLimitMin; // -> [0.0 .. 1.0]
                        float x = t * 1.0f; // -> [0.0 .. 1.0]
                        float y = 0.6f * (float)Math.Sin((double)x * 1.55);
                        float weight = y / 1.0f;
                        roll2 = weight * pConfig.RollLimitMin;
                    }

                    ;

                    float velocityX = ReadSingle(rawData, 20, true) * 57.3f;
                    float velocityY = ReadSingle(rawData, 24, true) * 57.3f;
                    float velocityZ = ReadSingle(rawData, 28, true) * 57.3f;

                    float accX = ReadSingle(rawData, 32, true);
                    float accY = ReadSingle(rawData, 36, true);
                    float accZ = ReadSingle(rawData, 40, true);

                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(0, /*yaw*/m_fYaw2);
                        controller.SetInput(1, pitch);
                        controller.SetInput(2, /*roll*/roll2);

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
        bool bIsValidGunPos = false;
        Vector3 v3GunPos = new Vector3();

        bool bIsValidEnginePos = false;
        Vector3 v3LocalEnginePos = new Vector3();

        public void ReadTelemetryData()
        {
            startDropData = new DateTime(1970, 1, 1);
            startRocketLaunchData = new DateTime(1970, 1, 1);
            startHitData = new DateTime(1970, 1, 1);
            startDamageData = new DateTime(1970, 1, 1);
            startExplosionData = new DateTime(1970, 1, 1);
            startGunIndex = new DateTime(1970, 1, 1);
            startGunPos = new DateTime(1970, 1, 1);
            bIsValidGunPos = false;
            bIsValidEnginePos = false;
            Plane planeX = new Plane(new Vector3(0, 0, 0), new Vector3(1, 0, 0));
            Plane planeY = new Plane(new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            Plane planeZ = new Plane(new Vector3(0, 0, 0), new Vector3(0, 0, 1));

            while (!stop)
            {

                try
                {
                    int nId = 9;

                    byte[] rawData = telemetryUdpClient.Receive(ref telemetryRemote);
                    int indicatorsCount = rawData[10];
                    int offset = 11;

                    //mtx.WaitOne(100);

                    Vector4 v4ENG_RPM = new Vector4(0, 0, 0, 0);
                    Vector4 v4ENG_MP = new Vector4(0, 0, 0, 0);
                    Vector4 v4ENG_SHAKE_FRQ = new Vector4(0, 0, 0, 0);
                    Vector4 v4ENG_SHAKE_AMP = new Vector4(0, 0, 0, 0);
                    Vector4 v4LGEARS_STATE = new Vector4(0, 0, 0, 0);
                    Vector4 v4LGEARS_PRESS = new Vector4(0, 0, 0, 0);
                    float fEAS = 0.0f;
                    float fAOA = 0.0f;
                    Vector3 v3Acceleration = new Vector3(0, 0, 0);
                    Vector2 v2CockpitShake = new Vector2(0, 0);
                    float fAGL = 0.0f;
                    float fFLAPS = 0.0f;
                    float fAIR_BRAKES = 0.0f;

                    for (int i = 0; i < indicatorsCount; i++)
                    {
                        int indicatorId = BitConverter.ToUInt16(rawData, offset);

                        int valuesCount = rawData[offset + 2];
                        offset += 3;

                        
                        for (int j = 0; j < valuesCount; j++)
                        {
                            float value = ReadSingle(rawData, offset, true);
                            offset += 4;

                            /*if (true == mtx.WaitOne(100))
                            {
                                controller.SetInput(nId++, value);

                                mtx.ReleaseMutex();
                            }*/

                            // ENG_RPM
                            if (0 == indicatorId)
                            {
                                if (0 == j) { v4ENG_RPM.X = value; }
                                if (1 == j) { v4ENG_RPM.Y = value; }
                                if (2 == j) { v4ENG_RPM.Z = value; }
                                if (3 == j) { v4ENG_RPM.W = value; }
                            }

                            // ENG_MP
                            if (1 == indicatorId)
                            {
                                if (0 == j) { v4ENG_MP.X = value; }
                                if (1 == j) { v4ENG_MP.Y = value; }
                                if (2 == j) { v4ENG_MP.Z = value; }
                                if (3 == j) { v4ENG_MP.W = value; }
                            }

                            // ENG_SHAKE_FRQ
                            if (2 == indicatorId)
                            {
                                if (0 == j) { v4ENG_SHAKE_FRQ.X = value; }
                                if (1 == j) { v4ENG_SHAKE_FRQ.Y = value; }
                                if (2 == j) { v4ENG_SHAKE_FRQ.Z = value; }
                                if (3 == j) { v4ENG_SHAKE_FRQ.W = value; }
                            }

                            // ENG_SHAKE_AMP
                            if (3 == indicatorId)
                            {
                                if (0 == j) { v4ENG_SHAKE_AMP.X = value; }
                                if (1 == j) { v4ENG_SHAKE_AMP.Y = value; }
                                if (2 == j) { v4ENG_SHAKE_AMP.Z = value; }
                                if (3 == j) { v4ENG_SHAKE_AMP.W = value; }
                            }

                            // LGEARS_STATE
                            if (4 == indicatorId)
                            {
                                if (0 == j) { v4LGEARS_STATE.X = value; }
                                if (1 == j) { v4LGEARS_STATE.Y = value; }
                                if (2 == j) { v4LGEARS_STATE.Z = value; }
                                if (3 == j) { v4LGEARS_STATE.W = value; }
                            }

                            // LGEARS_PRESS
                            if (5 == indicatorId)
                            {
                                if (0 == j) { v4LGEARS_PRESS.X = value; }
                                if (1 == j) { v4LGEARS_PRESS.Y = value; }
                                if (2 == j) { v4LGEARS_PRESS.Z = value; }
                                if (3 == j) { v4LGEARS_PRESS.W = value; }
                            }

                            // EAS
                            if (6 == indicatorId) 
                            {
                                if (0 == j) { fEAS = value; }
                            }

                            // AOA
                            if (7 == indicatorId)
                            {
                                if (0 == j) { fAOA = value; }
                            }

                            // ACCELERATION
                            if (8 == indicatorId) 
                            {
                                if (0 == j) { v3Acceleration.X = value; }
                                if (1 == j) { v3Acceleration.Y = value; v3Acceleration.Y -= 9.81f; }
                                if (2 == j) { v3Acceleration.Z = value; }
                            }

                            // COCKPIT SHAKE
                            if (9 == indicatorId) 
                            {
                                if (0 == j) { v2CockpitShake.X = value; }
                                if (1 == j) { v2CockpitShake.Y = value; }
                            }

                            // AGL
                            if (10 == indicatorId)
                            {
                                if (0 == j) { fAGL = value; }
                            }

                            // FLAPS
                            if (11 == indicatorId)
                            {
                                if (0 == j) { fFLAPS = value; }
                            }

                            // AIR_BRAKES
                            if (12 == indicatorId)
                            {
                                if (0 == j) { fAIR_BRAKES = value; }
                            }
                        }
                    }
                    //mtx.ReleaseMutex();

                    // ENG_RPM
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, v4ENG_RPM.X);
                        controller.SetInput(nId++, v4ENG_RPM.Y);
                        controller.SetInput(nId++, v4ENG_RPM.Z);
                        controller.SetInput(nId++, v4ENG_RPM.W);

                        mtx.ReleaseMutex();
                    }

                    // ENG_MP
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, v4ENG_MP.X);
                        controller.SetInput(nId++, v4ENG_MP.Y);
                        controller.SetInput(nId++, v4ENG_MP.Z);
                        controller.SetInput(nId++, v4ENG_MP.W);

                        mtx.ReleaseMutex();
                    }

                    // ENG_SHAKE_FRQ
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, v4ENG_SHAKE_FRQ.X);
                        controller.SetInput(nId++, v4ENG_SHAKE_FRQ.Y);
                        controller.SetInput(nId++, v4ENG_SHAKE_FRQ.Z);
                        controller.SetInput(nId++, v4ENG_SHAKE_FRQ.W);

                        mtx.ReleaseMutex();
                    }

                    // ENG_SHAKE_AMP
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, v4ENG_SHAKE_AMP.X);
                        controller.SetInput(nId++, v4ENG_SHAKE_AMP.Y);
                        controller.SetInput(nId++, v4ENG_SHAKE_AMP.Z);
                        controller.SetInput(nId++, v4ENG_SHAKE_AMP.W);

                        mtx.ReleaseMutex();
                    }

                    // LGEARS_STATE
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, v4LGEARS_STATE.X);
                        controller.SetInput(nId++, v4LGEARS_STATE.Y);
                        controller.SetInput(nId++, v4LGEARS_STATE.Z);
                        controller.SetInput(nId++, v4LGEARS_STATE.W);

                        mtx.ReleaseMutex();
                    }

                    // LGEARS_PRESS
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, v4LGEARS_PRESS.X);
                        controller.SetInput(nId++, v4LGEARS_PRESS.Y);
                        controller.SetInput(nId++, v4LGEARS_PRESS.Z);
                        controller.SetInput(nId++, v4LGEARS_PRESS.W);

                        mtx.ReleaseMutex();
                    }

                    // EAS
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, fEAS);

                        mtx.ReleaseMutex();
                    }

                    // AOA
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, fAOA);

                        mtx.ReleaseMutex();
                    }

                    // ACCELERATION
                    {
                        float fAcceleration = v3Acceleration.Length();
                        if (true == mtx.WaitOne(100))
                        {
                            controller.SetInput(nId++, fAcceleration);

                            mtx.ReleaseMutex();
                        }
                    }

                    // COCKPIT SHAKE
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, v2CockpitShake.X);
                        controller.SetInput(nId++, v2CockpitShake.Y);

                        mtx.ReleaseMutex();
                    }

                    // AGL
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, fAGL);

                        mtx.ReleaseMutex();
                    }

                    // FLAPS
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, fFLAPS);

                        mtx.ReleaseMutex();
                    }

                    // AIR_BRAKES
                    if (true == mtx.WaitOne(100))
                    {
                        controller.SetInput(nId++, fAIR_BRAKES);

                        mtx.ReleaseMutex();
                    }

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

                                v3LocalEnginePos = new Vector3(engineSetup.afPos[0], engineSetup.afPos[1], engineSetup.afPos[2]);
                                bIsValidEnginePos = true;

                                /*if (true == mtx.WaitOne(100)) 
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
                                bIsValidGunPos = true;
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

                    // Hit_Yaw/Hit_Pitch/Hit_Roll
                    if (true == bIsValidEnginePos && true == bIsValidGunPos && nDamageDataMilliseconds <= (UInt64)pConfig.EventTime)
                    {
                        float fHitYaw = 0.0f;
                        float fHitPitch = 0.0f;
                        float fHitRoll = 0.0f;

                        Vector3 v3LocalDamagePos = new Vector3(damageData.afPos[0], damageData.afPos[1], damageData.afPos[2]);
                        Vector3 v3ToDamage = v3LocalDamagePos - v3LocalEnginePos;
                        float fForce = v3ToDamage.Length();

                        bool bIsPositiveX = (v3ToDamage.X >= 0.0f) ? true : false;
                        bool bIsPositiveY = (v3ToDamage.Y >= 0.0f) ? true : false;
                        bool bIsPositiveZ = (v3ToDamage.Z >= 0.0f) ? true : false;

                        // Yaw
                        {
                            // PlaneX
                            float fDistanceX = Math.Abs(planeX.GetDistance(v3ToDamage));
                            // PlaneZ
                            float fDistanceZ = Math.Abs(planeZ.GetDistance(v3ToDamage));

                            if (true == bIsPositiveX && true == bIsPositiveZ) // 1
                            {
                                float fSign = 0.0f;
                                if (fDistanceX < fDistanceZ) { fSign = 1.0f; }
                                else { fSign = -1.0f; }

                                fHitYaw = fForce * fSign;
                            }
                            else if (false == bIsPositiveX && true == bIsPositiveZ) // 2
                            {
                                float fSign = 0.0f;
                                if (fDistanceX > fDistanceZ) { fSign = 1.0f; }
                                else { fSign = -1.0f; }

                                fHitYaw = fForce * fSign;
                            }
                            else if (false == bIsPositiveX && false == bIsPositiveZ) // 3
                            {
                                float fSign = 0.0f;
                                if (fDistanceX < fDistanceZ) { fSign = 1.0f; }
                                else { fSign = -1.0f; }

                                fHitYaw = fForce * fSign;
                            }
                            else if (true == bIsPositiveX && false == bIsPositiveZ) // 4
                            {
                                float fSign = 0.0f;
                                if (fDistanceX > fDistanceZ) { fSign = 1.0f; }
                                else { fSign = -1.0f; }

                                fHitYaw = fForce * fSign;
                            }

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

