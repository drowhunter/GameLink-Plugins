using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using SharedLib.TelemetryHelper;

using System;
using System.Collections.Generic;
using System.Text;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// One LiveMotion event as SC_Motion_Server.py forwards it: {"t", "ev", "name", "m": [field ids], "v": [values]}.
    /// Values are a double, a double[] (XYZ vector or quad) or null.
    /// </summary>
    internal sealed class ScPacket
    {
        public int Ev;
        public Dictionary<int, object> Data = new();

        public static ScPacket Parse(string json)
        {
            JObject o;
            try { o = JObject.Parse(json); }
            catch (JsonException) { return null; }

            // "mark" lines in capture files and anything without a numeric event key are ignored
            if (o["ev"] is not JValue ev || ev.Type != JTokenType.Integer)
                return null;

            var p = new ScPacket { Ev = ev.Value<int>() };
            if (o["m"] is JArray m && o["v"] is JArray v && m.Count == v.Count)
            {
                for (var i = 0; i < m.Count; i++)
                {
                    if (m[i].Type != JTokenType.Integer) continue;
                    p.Data[m[i].Value<int>()] = Value(v[i]);
                }
            }
            return p;
        }

        private static object Value(JToken t)
        {
            switch (t.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return t.Value<double>();
                case JTokenType.Boolean:
                    return t.Value<bool>() ? 1d : 0d;
                case JTokenType.Array:
                    var a = (JArray)t;
                    var r = new double[a.Count];
                    for (var i = 0; i < a.Count; i++)
                    {
                        if (a[i].Type != JTokenType.Integer && a[i].Type != JTokenType.Float) return null;
                        r[i] = a[i].Value<double>();
                    }
                    return r;
                default:
                    return null;
            }
        }

        /// <summary>Scalar field, or null when missing or not a number.</summary>
        public static double? Num(IReadOnlyDictionary<int, object> d, int id)
            => d != null && d.TryGetValue(id, out var v) && v is double x ? x : null;

        /// <summary>Finite XYZ vector field, or null.</summary>
        public static double[] Vec(IReadOnlyDictionary<int, object> d, int id)
        {
            if (d == null || !d.TryGetValue(id, out var v) || v is not double[] a || a.Length != 3)
                return null;
            foreach (var x in a)
                if (!double.IsFinite(x)) return null;
            return a;
        }

        public static double Mag(double[] a) => a == null ? 0d : Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
    }

    internal sealed class ScPacketConverter : IByteConverter<ScPacket>
    {
        public ScPacket FromBytes(byte[] data) => ScPacket.Parse(Encoding.UTF8.GetString(data));

        public byte[] ToBytes(ScPacket data) => throw new NotSupportedException();
    }
}
