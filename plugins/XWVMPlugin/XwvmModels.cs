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

        public float Paused;

        public float WorldYaw;
        public float WorldPitch;
        public float WorldRoll;

        private const float FireWindow = 0.2f;
        private const float ShakeWindow = 0.5f;
        private const float DefaultMaxRate = 80f;

        /// <summary>
        /// XWVM yaws about normalized (Up - Forward) (Ship.RotateFromInput), so each degree of yaw input is
        /// cos 45 deg of yaw about the ship's up axis plus the same amount of roll to the right.
        /// </summary>
        internal const float YawAxisComponent = 0.70710678f;

        /// <summary>
        /// Builds the inputs from the merged (sparse) XWVM JSON dictionary.
        /// </summary>
        public static Telemetry From(IReadOnlyDictionary<string, object> d, Heading heading = null)
        {
            var t = Num(d, "t");
            var rot = Vec(d, "rot");
            var vel = Vec(d, "vel");
            var acc = Vec(d, "acc");
            var aVel = Vec(d, "aVel");
            var hit = Vec(d, "hitD");
            var dmgT = Num(d, "dmgT", -1);
            var ejcT = Num(d, "ejcT", -1);
            var maxAVel = Vec(d, "maxAVel");
            var hasRot = d != null && d.ContainsKey("rot");

            // Body rates (deg/s): pitch nose-down +, yaw about the ship's up axis right +, roll left +.
            var pitchRate = aVel.x;
            var yawRate = YawAxisComponent * aVel.y;
            var rollRate = -(aVel.z + YawAxisComponent * aVel.y);

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
                Shake = dmgT > 0 && t >= dmgT && t - dmgT < ShakeWindow ? 1f - (t - dmgT) / ShakeWindow : 0f,

                HitX = hit.x,
                HitY = hit.y,
                HitZ = hit.z,
                LaserFired = Recent(t, Max(d, "canFT", Num(d, "canN")), FireWindow),
                WarheadFired = Recent(t, Max(d, "lauFT", Num(d, "lauN")), FireWindow),

                // aVel = (pitch nose-down +, yaw right +, roll right +) deg/s; acc = (lateral, vertical, longitudinal). Pitch flipped so nose-up is +.
                PitchInertia = -aVel.x,
                YawInertia = aVel.y,
                RollInertia = aVel.z,
                LateralAccel = acc.x,
                VerticalAccel = -acc.y,
                AccelInertia = acc.z,

                HyperspacePhase = Num(d, "hypM"),
                Ejected = ejcT >= 0 ? 1f : 0f,

                // World Euler angles corkscrew during XWVM's banked yaw, so the rig uses the ship's own rotation:
                // Yaw is the heading accumulated from the body yaw rate, Pitch/Roll tilt with the body rates
                // (full rate = 90, nose-down + / left +) and return to level when the stick is centered.
                Yaw = heading?.Update(Time(d), yawRate, hasRot ? rot.y : null) ?? (hasRot ? rot.y : 0f),
                Pitch = pitchRate / Max(maxAVel.x) * 90f,
                Roll = rollRate / Max(maxAVel.z) * 90f,

                Paused = Num(d, "paused") > 0 ? 1f : 0f,

                // Unity eulerAngles (0..360): x = pitch (nose down +), y = yaw, z = roll (left +)
                WorldYaw = rot.y,
                WorldPitch = Wrap(rot.x),
                WorldRoll = Wrap(rot.z),
            };
        }

        private static float Max(float rate) => rate > 1f ? rate : DefaultMaxRate;

        private static double Time(IReadOnlyDictionary<string, object> d)
        {
            if (d == null || !d.TryGetValue("t", out var v)) return 0d;
            if (v is JValue jv) v = jv.Value;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch { return 0d; }
        }

        private static float Wrap(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
        }

        private static float Recent(float now, float last, float window)
            => last > 0 && now >= last && now - last < window ? 1f : 0f;

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

    /// <summary>Ship heading (0..360) accumulated from the body yaw rate, so a held yaw always turns the same way.</summary>
    public sealed class Heading
    {
        private const float MaxGap = 1f;

        private double value;
        private double lastTime = double.NaN;

        public void Reset() => lastTime = double.NaN;

        public float Update(double time, float yawRate, float? seed)
        {
            if (double.IsNaN(lastTime))
                value = seed ?? 0f;
            else if (time > lastTime && time - lastTime <= MaxGap)
                value += yawRate * (time - lastTime);

            lastTime = time;
            value %= 360d;
            if (value < 0) value += 360d;
            return (float)value;
        }
    }
}
