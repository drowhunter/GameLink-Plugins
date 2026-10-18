using SharedLib.TelemetryHelper;
using SharedLib.TelemetryHelper.Convertors;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// Accepts both XWVM telemetry formats and merges them into one sparse dictionary keyed by the JSON field names.
    /// XWVM ignores the saved Format setting on startup and sends SimHub until the dropdown is changed in-session,
    /// so both formats must be handled. SimHub packets carry no orientation ("rot"), only rates and accelerations.
    /// </summary>
    internal class XwvmConverter : IByteConverter<Dictionary<string, object>>
    {
        public const int SimHubPacketSize = 1251;

        private readonly SparseJsonDictConverter json = new();

        public Dictionary<string, object> Data => json.Data;

        public void Reset() => json.Reset();

        public byte[] ToBytes(Dictionary<string, object> data) => json.ToBytes(data);

        public Dictionary<string, object> FromBytes(byte[] data)
        {
            if (data.Length >= SimHubPacketSize && data[0] != (byte)'{')
            {
                MergeSimHub(data, json.Data);
                return json.Data;
            }

            json.Data.Remove("paused");
            return json.FromBytes(data);
        }

        /// <summary>SHTelemetryPacket (Pack = 1, little-endian, 1251 bytes).</summary>
        internal static void MergeSimHub(ReadOnlySpan<byte> p, Dictionary<string, object> d)
        {
            var o = 0;
            o += 4 + 4 + 2 + 2 + 8 + 1;                 // signatures, layout version, emitter id, packet id
            var counter = (long)U64(p, ref o);
            o += 1;                                     // IsSessionRunning
            var paused = p[o++];
            o += 8 + 1 + 1 + 1 + 1;                     // session id, replay, user/AI control, spectator
            var time = F64(p, ref o);
            o += 4;                                     // physics discontinuity counter
            var yawRate = F32(p, ref o);
            var pitchRate = F32(p, ref o);
            var rollRate = F32(p, ref o);
            var surge = F32(p, ref o);
            var sway = F32(p, ref o);
            var heave = F32(p, ref o);
            var velFwd = F32(p, ref o);
            var velUp = F32(p, ref o);
            var north = F64(p, ref o);
            var east = F64(p, ref o);
            var up = F64(p, ref o);
            o += 4;                                     // SpeedKmh
            o += 256 + 128 + 256 + 128;                 // vehicle / environment names and ids
            var throttle = F32(p, ref o);
            var timeLeft = F64(p, ref o);
            var ejection = F32(p, ref o);
            var maxSpeed = F32(p, ref o);

            var cannonCount = p[o++];
            o += 8;                                     // cannon types
            o += 8 * 4;                                 // cannon energies
            var cannonFire = new List<object>();
            for (var i = 0; i < 8; i++)
            {
                var v = F32(p, ref o);
                if (i < cannonCount) cannonFire.Add((double)v);
            }
            o += 24 * 4;                                // cannon positions

            var launcherCount = p[o++];
            o += 8 + 8;                                 // warhead types, warhead counts
            var launcherFire = new List<object>();
            for (var i = 0; i < 8; i++)
            {
                var v = F32(p, ref o);
                if (i < launcherCount) launcherFire.Add((double)v);
            }
            o += 24 * 4;                                // launcher positions

            var beam = F32(p, ref o);
            var sfoil = p[o++];
            var hyper = p[o++];
            var damageTime = F32(p, ref o);
            var hitX = F32(p, ref o);
            var hitY = F32(p, ref o);
            var hitZ = F32(p, ref o);
            var front = F32(p, ref o);
            var rear = F32(p, ref o);
            var hull = F32(p, ref o);

            d["p"] = counter;
            d["paused"] = (long)paused;
            d["t"] = time;
            d["tL"] = timeLeft;
            d["ejcT"] = (double)ejection;
            d["pos"] = Vec(east, up, north);
            d["vel"] = Vec(0, velUp, velFwd);
            d["aVel"] = Vec(pitchRate, yawRate, rollRate);
            d["acc"] = Vec(sway, heave, surge);
            d["maxSpd"] = (double)maxSpeed;
            d["thr"] = (double)(throttle * 100f);
            d["canN"] = (long)cannonCount;
            d["canFT"] = cannonFire;
            d["lauN"] = (long)launcherCount;
            d["lauFT"] = launcherFire;
            d["beamE"] = (double)beam;
            d["sfoil"] = (long)sfoil;
            d["hypM"] = (long)hyper;
            d["dmgT"] = (double)damageTime;
            d["hitD"] = Vec(hitX, hitY, hitZ);
            d["fShd"] = (double)front;
            d["rShd"] = (double)rear;
            d["hull"] = (double)hull;
        }

        private static string Vec(double x, double y, double z)
            => string.Create(CultureInfo.InvariantCulture, $"{x} {y} {z}");

        private static ulong U64(ReadOnlySpan<byte> p, ref int o) { var v = BinaryPrimitives.ReadUInt64LittleEndian(p[o..]); o += 8; return v; }
        private static float F32(ReadOnlySpan<byte> p, ref int o) { var v = BinaryPrimitives.ReadSingleLittleEndian(p[o..]); o += 4; return v; }
        private static double F64(ReadOnlySpan<byte> p, ref int o) { var v = BinaryPrimitives.ReadDoubleLittleEndian(p[o..]); o += 8; return v; }
    }
}
