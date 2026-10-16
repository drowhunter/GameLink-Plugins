using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLib;
using SharedLib.TelemetryHelper;
using YawGLAPI;
using Plugin = YawVR_Game_Engine.Plugin.DistancePlugin;
using PluginData = YawVR_Game_Engine.Plugin.DistanceTelemetryData;

namespace DistancePlugin.Tests;

internal sealed class Limits(byte forward, byte backward, byte roll) : IDeviceParameters
{
    public byte PitchLimitF => forward;
    public byte PitchLimitB => backward;
    public byte RollLimit => roll;
    public byte Power => 100;
    public byte MaxRoll => 70;
    public byte MaxPitchFW => 75;
    public byte MaxPitchBW => 80;
    public uint YawLimit => 360;
    public float YawReturn => 0;
    public bool HasYawLimit => false;
    public bool HasVibration => false;
    public byte VibrationLimit => 0;
}

/// <summary>Runs a mod packet through the plugin exactly as DistancePlugin.ReadThread does.</summary>
internal static class Pipeline
{
    public static readonly Limits DefaultLimits = new(25, 40, 20);

    private static readonly MarshalByteConverter<PluginData> Converter = new();

    public static string[] InputNames { get; } = new Plugin().GetInputData();

    public static int Index(string name) => Array.IndexOf(InputNames, name) is var i and >= 0
        ? i : throw new InvalidOperationException($"Plugin has no input named '{name}'.");

    public static PluginData Decode(ModPacket packet) => Converter.FromBytes(packet.ToBytes());

    public static float[] Inputs(ModPacket packet, IDeviceParameters? limits = null) =>
        InputHelper.GetValues(Decode(packet))
            .Select(kv => Plugin.ScaleInput(kv.key, kv.value, limits ?? DefaultLimits))
            .ToArray();

    public static ObservableCollection<Profile_Component> LoadProfile()
    {
        using var stream = typeof(Plugin).Assembly
            .GetManifestResourceStream("DistancePlugin.Resources.Default.yawglprofile")
            ?? throw new InvalidOperationException("Missing embedded default profile.");
        using var reader = new StreamReader(stream);
        return JObject.Parse(reader.ReadToEnd())["Components"]!
            .ToObject<ObservableCollection<Profile_Component>>()
            ?? throw new JsonSerializationException("Invalid default profile components.");
    }

    /// <summary>
    /// Default profile with every channel disabled except input Yaw -> output yaw,
    /// which is made a 1:1 pass-through (no smoothing, limits, offsets, maths or conditions).
    /// </summary>
    public static ObservableCollection<Profile_Component> LoadIsolatedYawProfile()
    {
        var profile = LoadProfile();
        int yawInput = Index("Yaw");
        var yaw = profile.Single(c => c.Enabled && !c.Constant && c.Input_index == yawInput && c.Output_index == 0);
        foreach (var component in profile)
            component.Enabled = ReferenceEquals(component, yaw);
        yaw.Inverse = false;
        yaw.Type = ProfileComponentType.VALUE;
        yaw.MultiplierPos = yaw.MultiplierNeg = 1;
        yaw.Smoothing = 1;
        yaw.Limit = -1;
        yaw.Offset = yaw.Deadzone = 0;
        yaw.Spikeflatter.Enabled = false;
        yaw.Math.Clear();
        yaw.Condition.Clear();
        return profile;
    }

    public static dynamic CreateCore(ObservableCollection<Profile_Component> profile,
        bool antijump = false, float washout = 1f)
    {
        dynamic core = CoreSourceCompiler.Create();
        core.Configure(profile, antijump, 30, washout);
        return core;
    }

    public static float AngularDelta(float to, float from) =>
        (float)Math.IEEERemainder((double)to - from, 360);
}
