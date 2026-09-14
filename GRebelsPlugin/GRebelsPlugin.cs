using SharedLib;
using SharedLib.TelemetryHelper;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "G-Rebels")]
    [ExportMetadata("Version", "1.0")]
    public class GRebelsPlugin : Game
    {


        #region Standard Properties
        public int STEAM_ID => 2445980;
        public string PROCESS_NAME => "G_Rebels-Win64-Shipping";
        public string AUTHOR => "Trevor Jones (Drowhunter)";

        public bool PATCH_AVAILABLE => false;


        public string Description => ResourceHelper.Description;
        public Stream Logo => ResourceHelper.Logo;
        public Stream SmallLogo => ResourceHelper.SmallLogo;
        public Stream Background => ResourceHelper.Background;
        public List<Profile_Component> DefaultProfile() => dispatcher.JsonToComponents(ResourceHelper.DefaultProfile);


        public LedEffect DefaultLED() => new LedEffect(EFFECT_TYPE.KNIGHT_RIDER, 0, [YawColor.WHITE], 0);

        public Dictionary<string, ParameterInfo[]> GetFeatures() => null;

        public Type GetConfigBody() => typeof(Config);


        private Config settings;
        #endregion

        public string[] GetInputData() => InputHelper.GetValues<Telemetry>(default).Keys();

        private IDeviceParameters deviceParameters;
        private volatile bool running = false;
        private UdpTelemetry<Telemetry> telem;
        private Thread readThread;
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;
        CancellationTokenSource cts = new();

        public void Exit()
        {
            running = false;
            cts.Cancel();
            telem?.Dispose();
        }


        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;
        }

        public void Init()
        {

            deviceParameters = dispatcher.GetDeviceParameters();
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
            }, new TelemetryConverter());

            // G-Rebels does not send angular velocity, so derive it from the rotation delta
            var clock = Stopwatch.StartNew();
            Telemetry previous = default;
            double previousTime = -1;

            while (running)
            {
                try
                {
                    var data = telem.Receive();

                    // Absolute heading drives the 360° yaw axis; keep it in -180..180
                    data.Yaw = NormalizeAngle(data.Yaw);
                    var now = clock.Elapsed.TotalSeconds;
                    var dt = (float)(now - previousTime);

                    if (previousTime >= 0 && dt > 0.001f && dt < MaxRateInterval)
                    {
                        data.PitchVelocity = NormalizeAngle(data.Pitch - previous.Pitch) / dt;
                        data.YawVelocity = NormalizeAngle(data.Yaw - previous.Yaw) / dt;
                        data.RollVelocity = NormalizeAngle(data.Roll - previous.Roll) / dt;
                    }

                    data.Bump = Math.Abs(data.Heave);
                    data.Slide = Math.Abs(data.SpeedSideways);

                    previous = data;
                    previousTime = now;

                    // Scaled values use the raw angles, so compute them before flipping signs
                    //data.ScaledPitch = -MathsF.ScalePitchRoll(FoldDegree(data.Pitch, ease: true), -90, 90, deviceParameters.PitchLimitF, deviceParameters.PitchLimitB);
                    data.ScaledRoll = -MathsF.ScalePitchRoll(FoldDegree(data.Roll, ease: true), -90, 90, deviceParameters.RollLimit, deviceParameters.RollLimit);

                    data.Pitch = -data.Pitch;
                    data.Roll = -data.Roll;
                    data.Surge = -data.Surge;

                    // GetValues reads a copy of data, so every change must be made before this point
                    foreach (var (i, key, value) in InputHelper.GetValues(data).WithIndex())
                        controller.SetInput(i, value);
                }
                catch(SocketException) { }
            }

        }

        /// <summary>Packet gaps longer than this (pause, loading) produce zero angular speed instead of a spike.</summary>
        private const float MaxRateInterval = 0.25f;

        /// <summary>Wraps an angle difference into -180..180 degrees.</summary>
        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            else if (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// Folds an angle into -90..90 so being upside down reads as level:
        /// 0 → 0, ±90 → ±90, ±180 → 0.
        /// </summary>
        /// <param name="ease">
        /// false: straight-line fold (±135 → ±45) with a sharp direction change at ±90.
        /// true: sine wave, 90·sin(angle) (±135 → ±63.6), which eases smoothly through ±90.
        /// Small angles are about 1.57× larger than the straight-line fold (10 → 15.6).
        /// </param>
        private static float FoldDegree(float angle, bool ease = false)
        {
            angle = NormalizeAngle(angle);

            if (ease)
                return 90f * MathF.Sin(angle * MathF.PI / 180f);

            return MathF.CopySign(90f - MathF.Abs(MathF.Abs(angle) - 90f), angle);
        }



        // Telemetry is built into G-Rebels (Options > Enable Telemetry); nothing to patch
        public void PatchGame() { }

    }


}
