using Newtonsoft.Json;

using SharedLib;
using SharedLib.TelemetryHelper;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
// 
namespace YawVR_Game_Engine.Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "X-Wing Alliance")]
    [ExportMetadata("Version", "1.0")]
    public class XwauPlugin : Game
    {
        

        #region Standard Properties
        public int STEAM_ID => 0;
        public string PROCESS_NAME => "xwingalliance";
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

        public string[] GetInputData() => InputHelper.GetValues<Telemetry>().Keys();


        private volatile bool running = false;
        private UdpTelemetry<XwaPlayer> telem;
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
            this.settings = dispatcher.GetConfigObject<Config>();
           
            running = true;
            readThread = new Thread(new ThreadStart(ReadThread));
            readThread.Start();

            _yaw = 0.0f;
            _pitch = 0.0f;
            _roll = 0.0f;
        }

        private void ReadThread()
        {
            var options = new JsonSerializerSettings()
            {
                // Keep defaults; add our converter
            };
            // Use Newtonsoft converter for "0"/"1" boolean handling
            options.Converters.Add(new ZeroOneBooleanNewtonsoftConverter());
            
            telem = new UdpTelemetry<XwaPlayer>(new UdpTelemetryConfig
            {
                ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port)
            }, new NewtonsoftJsonConverter<XwaPlayer>(options));
#if DEBUG
            using var logFile = new FileStream(
    "XwauPlugin.log",
    FileMode.Create,           // overwrite each run; use FileMode.Append if you prefer
    FileAccess.Write,
    FileShare.Read,            // allow other processes to read while we write
    bufferSize: 4096,
    FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var sw = new StreamWriter(logFile, System.Text.Encoding.UTF8);
            sw.AutoFlush = true;
#endif
            
            while (running)
            {
                try
                {
                    var x = telem.Receive();
                    var telemetry = (Telemetry)x;
                    UpdateHyperspaceState(telemetry);

                    if (g_PrevHyperspacePhaseFSM != HyperspacePhaseEnum.HS_INIT_ST && g_HyperspacePhaseFSM == HyperspacePhaseEnum.HS_INIT_ST)
                    {
                        // We just exited hyperspace, reset our frame counter
                        g_iFramesSinceHyperExit = 0;
                    }
                    else if (g_PrevHyperspacePhaseFSM == HyperspacePhaseEnum.HS_HYPER_ENTER_ST && g_HyperspacePhaseFSM == HyperspacePhaseEnum.HS_HYPER_TUNNEL_ST)
                    {
                        // We just jumped, let's kick the seat
                        g_iFramesSinceHyperTunnel = 0;
                    }
                    g_iFramesSinceHyperExit++;
                    g_iFramesSinceHyperTunnel++;

                    float hyperPitch = 0.0f, yaw = 0.0f, pitch = 0.0f, roll = 0.0f;
                    if (g_HyperspacePhaseFSM == HyperspacePhaseEnum.HS_HYPER_TUNNEL_ST)
                    {
                        float t = (float)g_iFramesSinceHyperTunnel / 180.0f;
                        // To understand this, use graphtoy and plot both curves below. Use "1" instead of "maxPitchFromAccel"
                        if (t < 0.01f)
                            hyperPitch = lerp(0, minPitchFromAccel, t / 0.01f);
                        else
                            hyperPitch = lerp(minPitchFromAccel, 0.0f, (t - 0.01f) / 0.99f);
                        hyperPitch = enableHyperAccel ? MathF.Min(0.0f, hyperPitch) : 0.0f;
                        
                        (yaw, pitch, roll) = CalculateEulerAngles(0.0f, hyperPitch / pitchScale, 0.0f, 0.0f);
                    }
                    else
                    {
                        if (g_iFramesSinceHyperExit > 50)
                        {
                            float finalDistInertia = telemetry.AccelInertia;
                            if (g_iFramesSinceHyperExit < 300)
                            {
                                if (enableHyperAccel)
                                {
                                    // When exiting hyperspace, speed decreases rapidly from 999 MGLT down to
                                    // a more regular speed. This produces a very hard lunge forward that
                                    // is likely to be unpleasant. So let's dampen that effect
                                    finalDistInertia *= 0.25f;
                                }
                                else
                                {
                                    finalDistInertia = 0.0f;
                                }
                            }
                            (yaw, pitch, roll) = CalculateEulerAngles(telemetry.YawInertia, telemetry.PitchInertia, telemetry.RollInertia, finalDistInertia);
                        }
                    }

                    //var (y,p,r) = CalculateEulerAngles(telemetry.YawInertia, telemetry.PitchInertia, telemetry.RollInertia, telemetry.AccelInertia);



                    telemetry.Yaw = yaw;
                    telemetry.Pitch = pitch;
                    telemetry.Roll = roll;

                    var values = InputHelper.GetValues(telemetry);
#if DEBUG
                    //values.ToDictionary(_ => _.key, _ => _.value)
                    var log = JsonConvert.SerializeObject(x);
                    
                    sw.WriteLine(log);
#endif
                    var list = new List<(string key, float value)>(values);
                    for (var i = 0; i < list.Count; i++)
                    {
                        
                        var (k, value) = list[i];
                        controller.SetInput(i, value);
                    }
                }
                catch (SocketException) { }
            }
            
        }

        private float lerp(float x, float y, float s) {
	        return x + s * (y - x);
        }
       
        

        public async void PatchGame()
        {
#if DEBUG
            Debugger.Launch();
#endif

                        
        }

        float _yaw = 0.0f;
        float _pitch = 0.0f;
        float _roll = 0.0f;

        const float yawScale = 7.0f;
        const float pitchScale = -200.0f;
        const float rollScale = 3.0f;
        const float distScale = 100.0f;
        const float maxPitchFromAccel = 15.0f;
        const float minPitchFromAccel = -15.0f;
        bool enableHyperAccel = true;

        #region HyperSpace Variables

        HyperspacePhaseEnum g_HyperspacePhaseFSM ;
        HyperspacePhaseEnum g_PrevHyperspacePhaseFSM;
        bool g_bHyperspaceFirstFrame;
        bool g_bInHyperspace; bool g_bHyperspaceLastFrame; 
        bool g_bHyperspaceTunnelLastFrame;

        int g_iHyperspaceFrame = -1;
        int g_iFramesSinceHyperExit = 60; 
        int g_iFramesSinceHyperTunnel = 60;

        private void UpdateHyperspaceState(Telemetry t)
        {
            g_PrevHyperspacePhaseFSM = g_HyperspacePhaseFSM;

            // Reset the Hyperspace FSM regardless of the previous state. This helps reset the
            // state if we quit on the middle of a movie that is playing back the hyperspace
            // effect. If we do reset the FSM, we need to update the control variables too:
            if (t.HyperspacePhase == 0)
            {
                g_bInHyperspace = false;
                g_bHyperspaceLastFrame = (g_HyperspacePhaseFSM == HyperspacePhaseEnum.HS_HYPER_EXIT_ST);
                g_iHyperspaceFrame = -1;
                g_HyperspacePhaseFSM = HyperspacePhaseEnum.HS_INIT_ST;
                /*if (g_bHyperspaceLastFrame) {
                    log_debug("yaw,pitch at hyper exit: %0.3f, %0.3f",
                        PlayerDataTable[playerIndex].yaw / 65536.0f * 360.0f,
                        PlayerDataTable[playerIndex].pitch / 65536.0f * 360.0f);
                }*/
            }

            switch (g_HyperspacePhaseFSM)
            {
                case HyperspacePhaseEnum.HS_INIT_ST:
                    g_bInHyperspace = false;
                    g_bHyperspaceFirstFrame = false;
                    g_bHyperspaceTunnelLastFrame = false;
                    //g_bHyperspaceLastFrame = false; // No need to update this here, we do it at the beginning of this function
                    g_iHyperspaceFrame = -1;
                    if (t.HyperspacePhase == 2)
                    {
                        // Hyperspace has *just* been engaged. Save the current cockpit camera heading so we can restore it
                        g_bHyperspaceFirstFrame = true;
                        g_bInHyperspace = true;
                        g_iHyperspaceFrame = 0;
                        g_HyperspacePhaseFSM = HyperspacePhaseEnum.HS_HYPER_ENTER_ST;
                    }
                    break;
                case HyperspacePhaseEnum.HS_HYPER_ENTER_ST:
                    g_bInHyperspace = true;
                    g_bHyperspaceFirstFrame = false;
                    g_bHyperspaceTunnelLastFrame = false;
                    g_bHyperspaceLastFrame = false;
                    g_iHyperspaceFrame++;
                    if (t.HyperspacePhase == 4)
                        g_HyperspacePhaseFSM = HyperspacePhaseEnum.HS_HYPER_TUNNEL_ST;
                    break;
                case HyperspacePhaseEnum.HS_HYPER_TUNNEL_ST:
                    g_bInHyperspace = true;
                    g_bHyperspaceFirstFrame = false;
                    g_bHyperspaceTunnelLastFrame = false;
                    g_bHyperspaceLastFrame = false;
                    if (t.HyperspacePhase == 3)
                    {
                        //log_debug("[DBG] [FSM] HS_HYPER_TUNNEL_ST --> HS_HYPER_EXIT_ST");
                        g_bHyperspaceTunnelLastFrame = true;
                        //g_bInHyperspace = true;
                        g_HyperspacePhaseFSM = HyperspacePhaseEnum.HS_HYPER_EXIT_ST;
                        /*log_debug("yaw,pitch at hyper tunnel exit: %0.3f, %0.3f",
                            PlayerDataTable[playerIndex].yaw / 65536.0f * 360.0f,
                            PlayerDataTable[playerIndex].pitch / 65536.0f * 360.0f);
                        */
                    }
                    break;
                case HyperspacePhaseEnum.HS_HYPER_EXIT_ST:
                    g_bInHyperspace = true;
                    g_bHyperspaceFirstFrame = false;
                    g_bHyperspaceTunnelLastFrame = false;
                    g_bHyperspaceLastFrame = false;
                    // If we're playing back a film, we may stop the movie while in hyperspace. In
                    // that case, we need to "hard-reset" the FSM to its initial state as soon as
                    // we see hyperspacePhase == 0 or we'll mess up the state. However, that means
                    // that the final transition must also be done at the beginning of this
                    // function
                    /*
                    if (PlayerDataTable[playerIndex].hyperspacePhase == 0) {
                        log_debug("[DBG] [FSM] HS_HYPER_EXIT_ST --> HS_INIT_ST");
                        g_bInHyperspace = false;
                        g_bHyperspaceLastFrame = true;
                        log_debug("g_bHyperspaceLastFrame <- true");
                        g_iHyperspaceFrame = -1;
                        g_HyperspacePhaseFSM = HS_INIT_ST;
                    }
                    */

                    break;
            }
        }
        #endregion

        private (float yaw, float pitch, float roll) CalculateEulerAngles(float yawInertia, float pitchInertia, float rollInertia, float distInertia)
        {
            float pitchFromAccel = distScale * distInertia;
            if (pitchFromAccel < minPitchFromAccel)
                pitchFromAccel = minPitchFromAccel;
            else if (pitchFromAccel > maxPitchFromAccel)
                pitchFromAccel = maxPitchFromAccel;

            _yaw += yawScale * yawInertia;
            _pitch = pitchScale * pitchInertia + pitchFromAccel;
            _roll = rollScale * rollInertia;
            while (_yaw >= 360.0f)
            {
                _yaw -= 360.0f;
            }
            while (_yaw < 0.0f)
            {
                _yaw += 360.0f;
            }            

            return (_yaw, _pitch, _roll);
        }


        // Custom converter for "0"/"1" to bool
        private sealed class ZeroOneBooleanConverter : System.Text.Json.Serialization.JsonConverter<bool>
        {
            public override bool Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case System.Text.Json.JsonTokenType.True:
                        return true;
                    case System.Text.Json.JsonTokenType.False:
                        return false;
                    case System.Text.Json.JsonTokenType.String:
                        var s = reader.GetString();
                        if (string.Equals(s, "1", StringComparison.Ordinal) || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (string.Equals(s, "0", StringComparison.Ordinal) || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
                            return false;
                        // Fallback: try numeric parse
                        if (int.TryParse(s, out var n))
                            return n != 0;
                        throw new System.Text.Json.JsonException($"Invalid boolean string value: '{s}'.");
                    case System.Text.Json.JsonTokenType.Number:
                        if (reader.TryGetInt32(out var num))
                            return num != 0;
                        throw new System.Text.Json.JsonException("Invalid numeric boolean value.");
                    default:
                        throw new System.Text.Json.JsonException($"Unexpected token parsing boolean: {reader.TokenType}.");
                }
            }

            public override void Write(System.Text.Json.Utf8JsonWriter writer, bool value, System.Text.Json.JsonSerializerOptions options)
            {
                // Write as "1"/"0" strings to match input style
                writer.WriteStringValue(value ? "1" : "0");
            }
        }

        private sealed class ZeroOneBooleanNewtonsoftConverter : JsonConverter<bool>
        {
            public override bool CanRead => true;
            public override bool CanWrite => true;

            public override bool ReadJson(JsonReader reader, Type objectType, bool existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Boolean:
                        return (bool)reader.Value!;
                    case JsonToken.Integer:
                        try
                        {
                            var num = Convert.ToInt32(reader.Value, System.Globalization.CultureInfo.InvariantCulture);
                            return num != 0;
                        }
                        catch
                        {
                            throw new JsonSerializationException("Invalid numeric boolean value.");
                        }
                    case JsonToken.String:
                        var s = (reader.Value as string) ?? string.Empty;
                        if (string.Equals(s, "1", StringComparison.Ordinal) || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (string.Equals(s, "0", StringComparison.Ordinal) || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
                            return false;
                        if (int.TryParse(s, out var n))
                            return n != 0;
                        throw new JsonSerializationException($"Invalid boolean string value: '{s}'.");
                    case JsonToken.Null:
                        // Default to false for nulls; adjust if project requires otherwise
                        return false;
                    default:
                        throw new JsonSerializationException($"Unexpected token parsing boolean: {reader.TokenType}.");
                }
            }

            public override void WriteJson(JsonWriter writer, bool value, JsonSerializer serializer)
            {
                // Write as "1"/"0" strings to match input style
                writer.WriteValue(value ? "1" : "0");
            }
        }
    }
}