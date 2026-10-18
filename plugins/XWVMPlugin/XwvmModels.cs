using Newtonsoft.Json.Linq;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// Flat list of plugin inputs (field order = GameLink input index).
    /// </summary>
    public struct Telemetry
    {
        public float Speed;
        public float Throttle;
        public float MaxSpeed;
        public float BeamEnergy;
        public float SFoils;
        public float ShieldFront;
        public float ShieldBack;
        public float Hull;
        public float Shake;

        public float HitX;
        public float HitY;
        public float HitZ;
        public float LaserFired;
        public float WarheadFired;

        public float YawInertia;
        public float PitchInertia;
        public float RollInertia;
        public float AccelInertia;
        public float LateralAccel;
        public float VerticalAccel;

        public float HyperspacePhase;
        public float Ejected;

        public float Yaw;
        public float Pitch;
        public float Roll;

        private const float FireWindow = 0.2f;
        private const float ShakeWindow = 0.5f;

        /// <summary>
        /// Builds the inputs from the merged (sparse) XWVM JSON dictionary.
        /// </summary>
        public static Telemetry From(IReadOnlyDictionary<string, object> d)
        {
            var t = Num(d, "t");
            var rot = Vec(d, "rot");
            var vel = Vec(d, "vel");
            var acc = Vec(d, "acc");
            var aVel = Vec(d, "aVel");
            var hit = Vec(d, "hitD");
            var dmgT = Num(d, "dmgT", -1);
            var ejcT = Num(d, "ejcT", -1);

            return new Telemetry
            {
                Speed = vel.z,
                Throttle = Num(d, "thr"),
                MaxSpeed = Num(d, "maxSpd"),
                BeamEnergy = Num(d, "beamE"),
                SFoils = Num(d, "sfoil"),
                ShieldFront = Num(d, "fShd"),
                ShieldBack = Num(d, "rShd"),
                Hull = Num(d, "hull"),
                Shake = dmgT >= 0 && t >= dmgT && t - dmgT < ShakeWindow ? 1f - (t - dmgT) / ShakeWindow : 0f,

                HitX = hit.x,
                HitY = hit.y,
                HitZ = hit.z,
                LaserFired = Recent(t, Max(d, "canFT", Num(d, "canN")), FireWindow),
                WarheadFired = Recent(t, Max(d, "lauFT", Num(d, "lauN")), FireWindow),

                // aVel = (pitch, yaw, roll) deg/s; acc = (lateral, vertical, longitudinal)
                PitchInertia = aVel.x,
                YawInertia = aVel.y,
                RollInertia = aVel.z,
                LateralAccel = acc.x,
                VerticalAccel = acc.y,
                AccelInertia = acc.z,

                HyperspacePhase = Num(d, "hypM"),
                Ejected = ejcT >= 0 ? 1f : 0f,

                // Unity eulerAngles (0..360): x = pitch (nose down +), y = yaw, z = roll (left +)
                Yaw = rot.y,
                Pitch = -Wrap(rot.x),
                Roll = -Wrap(rot.z),
            };
        }

        private static float Wrap(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
        }

        private static float Recent(float now, float last, float window)
            => last >= 0 && now >= last && now - last < window ? 1f : 0f;

        private static float ToFloat(object v, float def = 0f)
        {
            switch (v)
            {
                case null: return def;
                case double db: return (float)db;
                case float f: return f;
                case long l: return l;
                case int i: return i;
                case bool b: return b ? 1f : 0f;
                case string s: return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : def;
                case JValue jv: return ToFloat(jv.Value, def);
                default:
                    try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
                    catch { return def; }
            }
        }

        private static float Num(IReadOnlyDictionary<string, object> d, string key, float def = 0f)
            => d != null && d.TryGetValue(key, out var v) ? ToFloat(v, def) : def;

        private static (float x, float y, float z) Vec(IReadOnlyDictionary<string, object> d, string key)
        {
            if (d == null || !d.TryGetValue(key, out var v) || v == null)
                return default;

            if (v is JValue jv)
                v = jv.Value;

            if (v is string s)
            {
                var parts = s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                return (Part(parts, 0), Part(parts, 1), Part(parts, 2));
            }

            if (v is IList list)
                return (list.Count > 0 ? ToFloat(list[0]) : 0, list.Count > 1 ? ToFloat(list[1]) : 0, list.Count > 2 ? ToFloat(list[2]) : 0);

            return default;

            static float Part(string[] p, int i)
                => i < p.Length && float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0f;
        }

        /// <summary>Largest value of the first <paramref name="count"/> entries of a numeric array.</summary>
        private static float Max(IReadOnlyDictionary<string, object> d, string key, float count)
        {
            if (d == null || !d.TryGetValue(key, out var v) || v is not IEnumerable items || v is string)
                return -1f;

            var max = -1f;
            var n = 0;
            foreach (var item in items)
            {
                if (n++ >= count) break;
                max = Math.Max(max, ToFloat(item, -1f));
            }
            return max;
        }
    }
}
