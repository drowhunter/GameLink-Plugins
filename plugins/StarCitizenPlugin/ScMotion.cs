using System;
using System.Collections.Generic;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// Motion path of SC-Motion-Haptic-Bridge's lm_simhub3.py (unshaped): vehicle frame (ev3) -> 9 values.
    /// rates  : yaw = -w[2], pitch = -w[0], roll = +w[1] (deg/s, w = id35 rad/s)
    /// linear : surge = a[1], sway = a[0], heave = a[2]       (m/s2, a = id40, unscaled)
    /// vel    : forward = v[1], lateral = v[0], up = v[2]     (m/s,  v = id9)
    /// Landed gate (gear id92 >= 0.99 and |v| below 0.5 m/s -> linear 0), glitch hold (|a| > 300 or non-finite
    /// -> last good frame) and stale ramp (linear to 0 over 0.5 s) handle noise and faults only.
    /// </summary>
    internal sealed class ScPath
    {
        public const int Count = 9;
        public const double Glitch = 300;
        public const double LandedSpeed = 0.5;
        public const double Ramp = 0.5;

        private const double Deg = 180.0 / Math.PI;
        private static readonly double[] Zero = { 0, 0, 0 };

        private double[] prev = new double[Count];
        private double[] good;
        private double[] rampStep;

        public double[] Active(IReadOnlyDictionary<int, object> d)
        {
            rampStep = null;
            prev = (double[])(Raw(d) ?? new double[Count]).Clone();
            return (double[])prev.Clone();
        }

        public double[] Stale(double rate)
        {
            if (rampStep == null)
            {
                var ticks = Math.Max(1.0, Ramp * rate);
                rampStep = Array.ConvertAll(prev, p => Math.Abs(p) / ticks);
                good = null;
            }
            for (var i = 0; i < Count; i++)
                prev[i] = rampStep[i] > 0 ? Math.CopySign(Math.Max(0.0, Math.Abs(prev[i]) - rampStep[i]), prev[i]) : 0.0;
            return (double[])prev.Clone();
        }

        private double[] Raw(IReadOnlyDictionary<int, object> d)
        {
            var a = RawVec(d, 40);
            var w = RawVec(d, 35);
            var v = RawVec(d, 9);
            if (a == null || w == null || v == null || !Finite(a) || !Finite(w) || !Finite(v))
                return good;
            if (Math.Max(Math.Abs(a[0]), Math.Max(Math.Abs(a[1]), Math.Abs(a[2]))) > Glitch)
                return good;

            double surge = a[1], sway = a[0], heave = a[2];
            var gear = ScPacket.Num(d, 92) ?? 0.0;
            if (gear >= 0.99 && ScPacket.Mag(v) < LandedSpeed)
                surge = sway = heave = 0.0;

            good = new[] { -w[2] * Deg, -w[0] * Deg, w[1] * Deg, surge, sway, heave, v[1], v[0], v[2] };
            return good;
        }

        /// <summary>Missing -> zero vector; present but not an XYZ vector -> null (treated as a glitch).</summary>
        private static double[] RawVec(IReadOnlyDictionary<int, object> d, int id)
        {
            if (d == null || !d.TryGetValue(id, out var v)) return Zero;
            return v is double[] a && a.Length == 3 ? a : null;
        }

        private static bool Finite(double[] a) => double.IsFinite(a[0]) && double.IsFinite(a[1]) && double.IsFinite(a[2]);
    }

    /// <summary>
    /// Seat gate of lm_simhub3.py: seated = latest Player Role (ev10) id38 == 1, no older than 0.25 s, and no Leave
    /// Vehicle (ev2) since. Seated: output = g * live, g fades 0 -> 1 over 0.5 s. Not seated: live frames are ignored
    /// and the last seated output fades to 0, so nothing sent after a seat exit can pass.
    /// </summary>
    internal sealed class ScSeat
    {
        public const int SeatedRole = 1;
        public const double Window = 0.25;
        public const double Fade = 0.5;

        private double? t10;
        private double? v10;
        private bool left;
        private double[] held;

        public double G { get; private set; }

        public void OnEvent(int ev, IReadOnlyDictionary<int, object> d, double t)
        {
            if (ev == 10)
            {
                t10 = t;
                v10 = ScPacket.Num(d, 38);
                if (v10 == SeatedRole) left = false;
            }
            else if (ev == 2)
            {
                left = true;
            }
        }

        public bool Seated(double now)
            => !left && t10 != null && now - t10 <= Window && v10 == SeatedRole;

        public double[] Apply(double[] live, double now, double dt)
        {
            var seated = Seated(now);
            var target = seated ? 1.0 : 0.0;
            var step = dt / Fade;
            G = G < target ? Math.Min(target, G + step) : Math.Max(target, G - step);

            if (seated) held = live;
            var src = seated ? live : held ?? new double[live.Length];
            var g = G;
            return Array.ConvertAll(src, x => g * x);
        }
    }
}
