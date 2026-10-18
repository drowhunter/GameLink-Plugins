using SharedLib;
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLib.TelemetryHelper;
using YawGLAPI;
using YawVR_Game_Engine.Plugin;

namespace XWVMPlugin.Tests;

/// <summary>Builds wire packets and runs them through the plugin's own decode path.</summary>
internal static class Packets
{
    public const int InputCount = 26;
    public const int Yaw = 22, Pitch = 23, Roll = 24;

    public static string DataFile(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

    public static byte[] Json(object packet) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet));

    public static string Vec(float x, float y, float z) =>
        string.Create(CultureInfo.InvariantCulture, $"{x} {y} {z}");

    /// <summary>Minimal full XWVM JSON packet with Unity Euler angles (0..360).</summary>
    public static byte[] Orientation(long packet, float yaw, float pitch, float roll) => Json(new Dictionary<string, object>
    {
        ["p"] = packet,
        ["t"] = 10 + packet * 0.02,
        ["rot"] = Vec(Unity(pitch), Unity(yaw), Unity(roll)),
        ["vel"] = "0 0 50",
        ["aVel"] = "0 0 0",
        ["acc"] = "0 0 0",
        ["thr"] = 50,
        ["maxSpd"] = 100,
        ["canN"] = 0, ["canFT"] = new float[8],
        ["lauN"] = 0, ["lauFT"] = new float[8],
        ["dmgT"] = -1, ["ejcT"] = -1,
    });

    public static float Unity(float degrees) => ((degrees % 360) + 360) % 360;

    public static Telemetry Decode(XwvmConverter converter, byte[] packet) => Telemetry.From(converter.FromBytes(packet));

    public static float[] Inputs(Telemetry telemetry, IDeviceParameters limits) =>
        InputHelper.GetValues(telemetry)
            .Select(input => YawVR_Game_Engine.Plugin.XWVMPlugin.ScaleInput(input.key, input.value, limits))
            .ToArray();

    public static ObservableCollection<Profile_Component> LoadProfile()
    {
        using var stream = typeof(YawVR_Game_Engine.Plugin.XWVMPlugin).Assembly
            .GetManifestResourceStream("XWVMPlugin.Resources.Default.yawglprofile")
            ?? throw new InvalidOperationException("Missing embedded default profile.");
        using var reader = new StreamReader(stream);
        return JObject.Parse(reader.ReadToEnd())["Components"]!.ToObject<ObservableCollection<Profile_Component>>()
            ?? throw new JsonSerializationException("Invalid default profile components.");
    }

    /// <summary>
    /// SHTelemetryPacket written at independently computed absolute offsets (Pack = 1, little-endian).
    /// </summary>
    internal sealed class SimHub
    {
        public const int Size = 1251;
        public readonly byte[] Bytes = new byte[Size];

        public const int Counter = 21, Running = 29, Paused = 30, Time = 43;
        public const int YawRate = 55, PitchRate = 59, RollRate = 63, Surge = 67, Sway = 71, Heave = 75,
            VelFwd = 79, VelUp = 83, North = 87, East = 95, Up = 103;
        public const int Throttle = 883, TimeLeft = 887, Ejection = 895, MaxSpeed = 899;
        public const int CannonCount = 903, CannonFire = 944, LauncherCount = 1072, LauncherFire = 1089;
        public const int Beam = 1217, SFoil = 1221, Hyper = 1222, DamageTime = 1223,
            HitX = 1227, HitY = 1231, HitZ = 1235, Front = 1239, Rear = 1243, Hull = 1247;

        public SimHub()
        {
            BinaryPrimitives.WriteUInt32LittleEndian(Bytes, 0x4D565758); // any non-'{' signature
            F32(Ejection, -1);
            F32(DamageTime, -1);
        }

        public SimHub U8(int offset, byte value) { Bytes[offset] = value; return this; }
        public SimHub U64(int offset, ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(Bytes.AsSpan(offset), value); return this; }
        public SimHub F32(int offset, float value) { BinaryPrimitives.WriteSingleLittleEndian(Bytes.AsSpan(offset), value); return this; }
        public SimHub F64(int offset, double value) { BinaryPrimitives.WriteDoubleLittleEndian(Bytes.AsSpan(offset), value); return this; }

        public SimHub Cannons(params float[] fireTimes)
        {
            U8(CannonCount, (byte)fireTimes.Length);
            for (int i = 0; i < fireTimes.Length; i++) F32(CannonFire + i * 4, fireTimes[i]);
            return this;
        }

        public SimHub Launchers(params float[] fireTimes)
        {
            U8(LauncherCount, (byte)fireTimes.Length);
            for (int i = 0; i < fireTimes.Length; i++) F32(LauncherFire + i * 4, fireTimes[i]);
            return this;
        }
    }
}
