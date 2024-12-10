using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Reflection;
using YawGLAPI;
using System.Net;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace FlyDangerous
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Fly Dangerous")]
    [ExportMetadata("Version", "1.0")]

    public class FlyDangerous : Game
    {
        public int STEAM_ID => 1781750;
        public string PROCESS_NAME => string.Empty;
        public bool PATCH_AVAILABLE => false;
        public string AUTHOR => "DannyDan";
        public Stream Logo => GetStream("logo.jpg");
        public Stream SmallLogo => GetStream("logo_small.jpg");
        public Stream Background => GetStream("logo.jpg");
        public string Description => "Go to 'Options -> Integrations -> enable 'Raw Telemetry' and set 'Telemetry Mode' to 'BYTES'.";

        private volatile bool running = false;
        private IProfileManager? controller;
        private Thread? readThread;
        private UdpClient? receivingUdpClient;
        private IMainFormDispatcher? dispatcher;
        private IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private float yaw = 0.0f;
        private float pitch = 0.0f;
        private float roll = 0.0f;
        private float throttle = 0.0f;
        private float boost = 0.0f;

        private readonly float offSet = 1.0f;

        public LedEffect DefaultLED() => new LedEffect((EFFECT_TYPE)1, 7, new YawColor[4]
        {
            new YawColor(255, 255, 50),
            new YawColor(80, 80, 80),
            new YawColor(255, 0, 255),
            new YawColor(255, 213, 0)
        }, 25f);

        public List<Profile_Component> DefaultProfile()
        {
            List<Profile_Component> profileComponentList = new List<Profile_Component>
            {
                new Profile_Component(0, 0, 0.25f, 0.25f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(1, 1, 0.50f, 0.50f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(2, 2, 0.25f, 0.25f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(3, 3, 10.0f, 10.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(3, 4, 8.0f, 8.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(4, 3, 25.0f, 25.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(4, 4, 25.0f, 25.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null)
            };

            return profileComponentList;
        }

        public UdpClient? GetReceivingUdpClient()
        {
            return receivingUdpClient;
        }

        public void Exit()
        {
            receivingUdpClient.Close();
            receivingUdpClient = null;
            running = false;
        }

        public string GetDescription()
        {
            return Description;
        }

        public string[] GetInputData()
        {
            /*Type t = typeof(FlyDangerousTelemetryBytes);

            FieldInfo[] fields = t.GetFields();                       

            string[] inputs = new string[fields.Length];

            for (int i = 0; i<fields.Length; i++)
            {
                inputs[i] = fields[i].Name;
            }
            return inputs;*/

            return new string[] { "Yaw", "Pitch", "Roll", "Throttle", "Boost" };
        }

        public void Init()
        {
            running = true;
            receivingUdpClient = new UdpClient(11000);
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();
        }
        private void ReadFunction()
        {
            /*using (StreamWriter sw = File.AppendText(@"D:\1\DL\output.txt"))
            {
                sw.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + ": " + running);
            }*/

            while (running)
            {
                try
                {
                    var timeToWait = TimeSpan.FromSeconds(2);

                    var asyncResult = receivingUdpClient.BeginReceive(null, null);
                    asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
                    if (asyncResult.IsCompleted)
                    {
                        try
                        {
                            byte[] buffer = receivingUdpClient.EndReceive(asyncResult, ref RemoteIpEndPoint);

                            var size = Marshal.SizeOf(telemetryBytes);
                            var ptr = IntPtr.Zero;
                            try
                            {
                                ptr = Marshal.AllocHGlobal(size);
                                Marshal.Copy(buffer, 0, ptr, size);
                                telemetryBytes = (FlyDangerousTelemetryBytes)Marshal.PtrToStructure(ptr, telemetryBytes.GetType());
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(ptr);
                            }

                            telemetry.SetFromFlyDangerousTelemetryBytes(ref telemetryBytes);

                            if (telemetry.shipWorldRotationEuler.y > 180)
                                yaw = (telemetry.shipWorldRotationEuler.y - 360) / 2;
                            else
                                yaw = telemetry.shipWorldRotationEuler.y;

                            if (telemetry.shipWorldRotationEuler.z > 180)
                                roll = (telemetry.shipWorldRotationEuler.z - 360) / 2;
                            else
                                roll = telemetry.shipWorldRotationEuler.z;

                            if (telemetry.shipWorldRotationEuler.x > 180)
                                pitch = (telemetry.shipWorldRotationEuler.x - 360) / 2;
                            else
                                pitch = telemetry.shipWorldRotationEuler.x;

                            if (telemetry.throttlePosition > 0.5f)
                                throttle = telemetry.throttlePosition;
                            else
                                throttle = 0.0f;

                            if (telemetry.isBoostThrustActive == true)
                                boost = 1.0f;
                            else
                                boost = 0.0f;

                            controller.SetInput(0, yaw);
                            controller.SetInput(1, pitch);
                            controller.SetInput(2, roll);
                            controller.SetInput(3, throttle);
                            controller.SetInput(4, boost);
                        }
                        catch (Exception ex)
                        {
                            controller.SetInput(0, 0.0f);
                            controller.SetInput(1, 0.0f);
                            controller.SetInput(2, 0.0f);
                            controller.SetInput(3, 0.0f);
                            controller.SetInput(4, 0.0f);
                        }
                    }
                    else
                    {
                        controller.SetInput(0, 0.0f);
                        controller.SetInput(1, 0.0f);
                        controller.SetInput(2, 0.0f);
                        controller.SetInput(3, 0.0f);
                        controller.SetInput(4, 0.0f);
                    }


                }
                catch (Exception)
                {
                    running = false;

                    controller.SetInput(0, 0.0f);
                    controller.SetInput(1, 0.0f);
                    controller.SetInput(2, 0.0f);
                    controller.SetInput(3, 0.0f);
                    controller.SetInput(4, 0.0f);
                }
            }

            while (!running)
            {
                controller.SetInput(0, 0.0f);
                controller.SetInput(1, 0.0f);
                controller.SetInput(2, 0.0f);
                controller.SetInput(3, 0.0f);
                controller.SetInput(4, 0.0f);
            }
        }

        private static FlyDangerousTelemetryBytes telemetryBytes;
        private static FlyDangerousTelemetry telemetry;

        public void PatchGame()
        {
            return;
        }

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.controller = controller;
            this.dispatcher = dispatcher;
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
    }
    public struct FlyDangerousTelemetry
    {
        // Meta
        public uint flyDangerousTelemetryId;
        public uint packetId;

        // Game State
        public string gameVersion;
        public string currentGameMode;
        public string currentLevelName;
        public string currentMusicTrackName;
        public string currentShipName;
        public string playerName;
        public string playerFlagIso;

        public int currentPlayerCount;

        // Instrument Data
        public SerializableVector3 shipWorldPosition;
        public float shipAltitude;
        public float shipHeightFromGround;
        public float shipSpeed;
        public float accelerationMagnitudeNormalised;
        public float gForce;
        public float pitchPosition;
        public float rollPosition;
        public float yawPosition;
        public float throttlePosition;
        public float lateralHPosition;
        public float lateralVPosition;
        public float boostCapacitorPercent;
        public bool boostTimerReady;
        public bool boostChargeReady;
        public bool underWater;
        public bool lightsActive;
        public bool velocityLimiterActive;
        public bool vectorFlightAssistActive;
        public bool rotationalFlightAssistActive;
        public bool proximityWarning;
        public float proximityWarningSeconds;

        // Feedback Data
        public bool collisionThisFrame;
        public bool collisionStartedThisFrame;
        public float collisionImpactNormalised;
        public SerializableVector3 collisionDirection;
        public bool isBoostSpooling;
        public bool boostSpoolStartedThisFrame;
        public bool isBoostThrustActive;
        public bool boostThrustStartedThisFrame;
        public float boostSpoolTotalDurationSeconds;
        public float boostThrustTotalDurationSeconds;
        public float boostThrustProgressNormalised;
        public float shipShakeNormalised;

        // Motion Data
        public SerializableVector3 currentLateralVelocity;
        public SerializableVector3 currentLateralForce;
        public SerializableVector3 currentAngularVelocity;
        public SerializableVector3 currentAngularTorque;
        public SerializableVector3 currentLateralVelocityNormalised;
        public SerializableVector3 currentLateralForceNormalised;
        public SerializableVector3 currentAngularVelocityNormalised;
        public SerializableVector3 currentAngularTorqueNormalised;
        public SerializableVector3 shipWorldRotationEuler;
        public float maxSpeed;

        public void SetFromFlyDangerousTelemetryBytes(ref FlyDangerousTelemetryBytes telemetry)
        {
            // string values
            gameVersion = new string(telemetry.gameVersion).TrimEnd();
            currentLevelName = new string(telemetry.currentLevelName).TrimEnd();
            currentGameMode = new string(telemetry.currentGameMode).TrimEnd();
            currentMusicTrackName = new string(telemetry.currentMusicTrackName).TrimEnd();
            currentShipName = new string(telemetry.currentShipName).TrimEnd();
            playerName = new string(telemetry.playerName).TrimEnd();
            playerFlagIso = new string(telemetry.playerFlagIso).TrimEnd();

            // safe values
            flyDangerousTelemetryId = telemetry.flyDangerousTelemetryId;
            packetId = telemetry.packetId;

            currentPlayerCount = telemetry.currentPlayerCount;
            shipWorldPosition = telemetry.shipWorldPosition;
            shipAltitude = telemetry.shipAltitude;
            shipHeightFromGround = telemetry.shipHeightFromGround;
            shipSpeed = telemetry.shipSpeed;
            accelerationMagnitudeNormalised = telemetry.accelerationMagnitudeNormalised;
            gForce = telemetry.gForce;
            pitchPosition = telemetry.pitchPosition;
            rollPosition = telemetry.rollPosition;
            yawPosition = telemetry.yawPosition;
            throttlePosition = telemetry.throttlePosition;
            lateralHPosition = telemetry.lateralHPosition;
            lateralVPosition = telemetry.lateralVPosition;
            boostCapacitorPercent = telemetry.boostCapacitorPercent;
            boostTimerReady = telemetry.boostTimerReady;
            boostChargeReady = telemetry.boostChargeReady;
            lightsActive = telemetry.lightsActive;
            underWater = telemetry.underWater;
            velocityLimiterActive = telemetry.velocityLimiterActive;
            vectorFlightAssistActive = telemetry.vectorFlightAssistActive;
            rotationalFlightAssistActive = telemetry.rotationalFlightAssistActive;
            proximityWarning = telemetry.proximityWarning;
            proximityWarningSeconds = telemetry.proximityWarningSeconds;
            collisionThisFrame = telemetry.collisionThisFrame;
            collisionStartedThisFrame = telemetry.collisionStartedThisFrame;
            collisionImpactNormalised = telemetry.collisionImpactNormalised;
            collisionDirection = telemetry.collisionDirection;
            isBoostSpooling = telemetry.isBoostSpooling;
            boostSpoolStartedThisFrame = telemetry.boostSpoolStartedThisFrame;
            isBoostThrustActive = telemetry.isBoostThrustActive;
            boostThrustStartedThisFrame = telemetry.boostThrustStartedThisFrame;
            boostSpoolTotalDurationSeconds = telemetry.boostSpoolTotalDurationSeconds;
            boostThrustTotalDurationSeconds = telemetry.boostThrustTotalDurationSeconds;
            boostThrustProgressNormalised = telemetry.boostThrustProgressNormalised;
            shipShakeNormalised = telemetry.shipShakeNormalised;
            currentLateralVelocity = telemetry.currentLateralVelocity;
            currentLateralForce = telemetry.currentLateralForce;
            currentAngularVelocity = telemetry.currentAngularVelocity;
            currentAngularTorque = telemetry.currentAngularTorque;
            currentLateralVelocityNormalised = telemetry.currentLateralVelocityNormalised;
            currentLateralForceNormalised = telemetry.currentLateralForceNormalised;
            currentAngularVelocityNormalised = telemetry.currentAngularVelocityNormalised;
            currentAngularTorqueNormalised = telemetry.currentAngularTorqueNormalised;
            shipWorldRotationEuler = telemetry.shipWorldRotationEuler;
            maxSpeed = telemetry.maxSpeed;
        }
    }

    // byte encoding version of FlyDangerousTelemetry with fixed padded char fields for strings (consistent sized packets)
    [StructLayout(LayoutKind.Sequential)]
    public struct FlyDangerousTelemetryBytes
    {
        // Meta
        public uint flyDangerousTelemetryId;
        public uint packetId;

        // Game State
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public char[] gameVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
        public char[] currentGameMode;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
        public char[] currentLevelName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
        public char[] currentMusicTrackName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
        public char[] currentShipName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
        public char[] playerName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public char[] playerFlagIso;

        public int currentPlayerCount;

        // Instrument Data
        public SerializableVector3 shipWorldPosition;
        public float shipAltitude;
        public float shipHeightFromGround;
        public float shipSpeed;
        public float accelerationMagnitudeNormalised;
        public float gForce;
        public float pitchPosition;
        public float rollPosition;
        public float yawPosition;
        public float throttlePosition;
        public float lateralHPosition;
        public float lateralVPosition;
        public float boostCapacitorPercent;
        public bool boostTimerReady;
        public bool boostChargeReady;
        public bool lightsActive;
        public bool underWater;
        public bool velocityLimiterActive;
        public bool vectorFlightAssistActive;
        public bool rotationalFlightAssistActive;
        public bool proximityWarning;
        public float proximityWarningSeconds;

        // Feedback Data
        public bool collisionThisFrame;
        public bool collisionStartedThisFrame;
        public float collisionImpactNormalised;
        public SerializableVector3 collisionDirection;
        public bool isBoostSpooling;
        public bool boostSpoolStartedThisFrame;
        public bool isBoostThrustActive;
        public bool boostThrustStartedThisFrame;
        public float boostSpoolTotalDurationSeconds;
        public float boostThrustTotalDurationSeconds;
        public float boostThrustProgressNormalised;
        public float shipShakeNormalised;

        // Motion Data
        public SerializableVector3 currentLateralVelocity;
        public SerializableVector3 currentLateralForce;
        public SerializableVector3 currentAngularVelocity;
        public SerializableVector3 currentAngularTorque;
        public SerializableVector3 currentLateralVelocityNormalised;
        public SerializableVector3 currentLateralForceNormalised;
        public SerializableVector3 currentAngularVelocityNormalised;
        public SerializableVector3 currentAngularTorqueNormalised;
        public SerializableVector3 shipWorldRotationEuler;
        public float maxSpeed;

        // string helpers for char[] handling
        public string GameVersion => new string(gameVersion).TrimEnd();
        public string CurrentGameMode => new string(currentGameMode).TrimEnd();
        public string CurrentLevelName => new string(currentLevelName).TrimEnd();
        public string CurrentMusicTrackName => new string(currentMusicTrackName).TrimEnd();


    }

    [StructLayout(LayoutKind.Explicit, Size = 12, CharSet = CharSet.Ansi)]
    public class SerializableVector3
    {
        [FieldOffset(0)] public float x;
        [FieldOffset(4)] public float y;
        [FieldOffset(8)] public float z;

        public SerializableVector3()
        {
        }

        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

}

