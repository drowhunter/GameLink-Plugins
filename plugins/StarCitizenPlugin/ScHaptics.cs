using System;
using System.Collections.Generic;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// The effects of sc_fx.py that lm_simhub4.py turns into its impact (suspension-velocity) pulse:
    /// shock (collisions, explosions), touchdown (ground contact) and hull / shield hits (ev7). Values are 0..100.
    /// </summary>
    internal sealed class ScImpacts
    {
        // shock: |id40| minus the largest own thrust |id38| over the last 100 ms, or rotational |id34 - id41|
        private const double ShockThreshold = 300, ShockFull = 1000, ShockRotThreshold = 10, ShockRotFull = 150;
        private const double ShockMs = 300, ShockGain = 100, ThrustWindow = 0.1;
        // touchdown: vertical contact acceleration cHP (m/s2) with gear down and engines on
        public const double ContactThreshold = 20;
        private const double ContactFull = 90, ContactMs = 250, ContactGain = 90;
        // ev7 Vehicle Impact: id39 hull damage, id40 shield damage
        private const double HitOffset = -0.0045, HitScale = 60;
        private const double HullMs = 220, HullGain = 85, ShieldMs = 150, ShieldGain = 60;

        private readonly Pulse shock = new(ShockMs), contact = new(ContactMs), hull = new(HullMs), shield = new(ShieldMs);
        private readonly List<(double t, double m)> thrust = new();
        private double dt;
        private double[] prevV;
        private double? lp;

        /// <summary>Contact (vertical) acceleration not explained by the game's forces, m/s2.</summary>
        public double Chp { get; private set; }

        public void OnEvent(int ev, IReadOnlyDictionary<int, object> d, double now)
        {
            if (ev == 0)
            {
                var x = ScPacket.Num(d, 93);
                if (x > 0 && x < 0.5) dt = x.Value;
            }
            else if (ev == 3)
            {
                Contact(d, now);
                Shock(d, now);
            }
            else if (ev == 7)
            {
                Hit(hull, d, 39, HullGain, now);
                Hit(shield, d, 40, ShieldGain, now);
            }
        }

        /// <summary>Strongest active pulse, 0..100.</summary>
        public double Value(double now)
            => Math.Max(Math.Max(shock.Value(now), contact.Value(now)), Math.Max(hull.Value(now), shield.Value(now)));

        private void Contact(IReadOnlyDictionary<int, object> d, double now)
        {
            var v = ScPacket.Vec(d, 9);
            var w = ScPacket.Vec(d, 35);
            var a = ScPacket.Vec(d, 40);
            if (v == null || w == null || a == null) return;

            if (prevV != null && dt > 0)
            {
                var dvz = (v[2] - prevV[2]) / dt;
                var crz = w[0] * v[1] - w[1] * v[0];
                var cz = dvz + crz - a[2];
                lp = lp == null ? cz : lp + (cz - lp.Value) * Math.Min(1.0, dt / 0.5);
                Chp = Math.Max(0.0, cz - lp.Value);

                var gear = ScPacket.Num(d, 92);
                var eng = ScPacket.Num(d, 4);
                if (gear >= 0.99 && eng >= 0.5 && Chp > ContactThreshold)
                    contact.Raise(now, ContactGain * Math.Min(1.0, Chp / ContactFull));
            }
            prevV = v;
        }

        private void Shock(IReadOnlyDictionary<int, object> d, double now)
        {
            var a40 = ScPacket.Vec(d, 40);
            var a38 = ScPacket.Vec(d, 38);
            var w34 = ScPacket.Vec(d, 34);
            var w41 = ScPacket.Vec(d, 41);

            if (a38 != null) thrust.Add((now, ScPacket.Mag(a38)));
            thrust.RemoveAll(x => now - x.t > ThrustWindow);

            var rot = w34 != null && w41 != null
                ? ScPacket.Mag(new[] { w34[0] - w41[0], w34[1] - w41[1], w34[2] - w41[2] })
                : 0.0;
            var ext = 0.0;
            if (a40 != null && a38 != null)
            {
                var own = 0.0;
                foreach (var x in thrust) own = Math.Max(own, x.m);
                ext = Math.Max(0.0, ScPacket.Mag(a40) - own);
            }

            if (ext > ShockThreshold || rot > ShockRotThreshold)
                shock.Raise(now, ShockGain * Math.Min(1.0, Math.Max(ext / ShockFull, rot / ShockRotFull)));
        }

        private static void Hit(Pulse p, IReadOnlyDictionary<int, object> d, int id, double gain, double now)
        {
            var x = Scalar(d, id);
            if (x == null || x < 0.000001) return;
            var k = Math.Max(0.0, Math.Min(1.0, (x.Value - HitOffset) * HitScale));
            if (k > 0) p.Start(now, gain * k);
        }

        /// <summary>Number, or the magnitude of an XYZ vector.</summary>
        private static double? Scalar(IReadOnlyDictionary<int, object> d, int id)
        {
            var n = ScPacket.Num(d, id);
            if (n != null) return n;
            var v = ScPacket.Vec(d, id);
            return v != null ? ScPacket.Mag(v) : null;
        }

        /// <summary>Linear decay from peak to 0 over its length.</summary>
        private sealed class Pulse
        {
            private readonly double length;
            private double start = -1e9, peak;

            public Pulse(double ms) => length = ms / 1000.0;

            public void Start(double now, double p) { start = now; peak = p; }

            /// <summary>Restart only when stronger than what is still running.</summary>
            public void Raise(double now, double p) { if (p > Value(now)) Start(now, p); }

            public double Value(double now)
            {
                var el = now - start;
                return el >= 0 && el < length ? peak * (1.0 - el / length) : 0.0;
            }
        }
    }

    /// <summary>
    /// The extra (haptics) values of lm_simhub4.py, built from the raw event stream.
    /// </summary>
    internal sealed class ScHaptics
    {
        public const double RpmIdle = 1000, RpmMax = 8000, SuspFull = 1.0;
        public const double GroundV = 0.6, GroundA = 1.0, HoverA = 0.5;
        private const double MinThrustRef = 20.0;

        public readonly ScImpacts Impacts = new();
        private IReadOnlyDictionary<int, object> d3 = new Dictionary<int, object>();
        private double? boostT;
        private double thrustRef = MinThrustRef;
        private bool? ground;           // unknown until the first ev3 frame (read as "on ground")
        private double? stillT, hoverT, climbT;

        public int Touchdowns { get; private set; }

        public void OnEvent(int ev, IReadOnlyDictionary<int, object> d, double t)
        {
            Impacts.OnEvent(ev, d, t);
            if (ev == 1)
            {
                thrustRef = MinThrustRef;   // new ship: thrust reference starts again
            }
            else if (ev == 4)
            {
                if (ScPacket.Num(d, 41) >= 0.5) boostT = t;
            }
            else if (ev == 3)
            {
                d3 = d;
                Ground(d, t);
            }
        }

        private double Thrust() => ScPacket.Mag(ScPacket.Vec(d3, 38));

        private double Speed() => ScPacket.Mag(ScPacket.Vec(d3, 9));

        /// <summary>
        /// Landed = gear down and the ship stands still although the game's own forces (id40 = thrust + gravity +
        /// drag, no ground reaction) do not balance: something holds it. Hovering = still with balanced forces.
        /// </summary>
        private void Ground(IReadOnlyDictionary<int, object> d, double t)
        {
            var gearDown = ScPacket.Num(d, 92) >= 0.99;
            var spd = Speed();
            var v = ScPacket.Vec(d, 9) ?? new double[3];
            var acc = ScPacket.Mag(ScPacket.Vec(d, 40));
            var still = spd < GroundV;

            stillT = still && acc > GroundA ? stillT ?? t : null;
            hoverT = spd < 0.3 && acc < HoverA ? hoverT ?? t : null;
            climbT = v[2] > 1.0 ? climbT ?? t : null;

            if (ground == null)
            {
                // first frame: take the state as it is (no touchdown when data starts while parked)
                ground = gearDown && still && acc > GroundA;
                return;
            }
            if (ground == false)
            {
                var contact = Impacts.Chp > ScImpacts.ContactThreshold;
                if (gearDown && ((stillT != null && t - stillT >= 0.3) || contact))
                {
                    ground = true;
                    Touchdowns++;
                }
            }
            else if (!gearDown || spd > 30.0 || (climbT != null && t - climbT >= 0.5) || (hoverT != null && t - hoverT >= 1.0))
            {
                ground = false;
            }
        }

        /// <summary>Extra values; seat-gated with gain g except the ground state, which is the ship's.</summary>
        public ScExtras Values(double now, bool seated, double g)
        {
            var engOn = ScPacket.Num(d3, 4) >= 0.5;
            var thr = Thrust();
            var boosting = boostT != null && now - boostT <= 0.25 && thr >= 20.0;
            if (!boosting) thrustRef = Math.Max(thrustRef, thr);
            var share = boosting ? 1.0 : Math.Min(1.0, thr / thrustRef);
            var rpm = engOn ? RpmIdle + (RpmMax - RpmIdle) * share : 0.0;
            var on = seated && g > 0.0;

            return new ScExtras
            {
                SpeedKmh = on ? Speed() * 3.6 * g : 0.0,
                EngineOn = on && engOn,
                EngineRpm = on ? rpm * g : 0.0,
                EngineMaxRpm = RpmMax,
                Throttle = on ? (engOn ? share : 0.0) * g : 0.0,
                Boost = on && boosting,
                OnGround = ground != false,
                Impact = on ? SuspFull * Impacts.Value(now) / 100.0 * g : 0.0,
            };
        }
    }

    internal struct ScExtras
    {
        public double SpeedKmh;
        public bool EngineOn;
        public double EngineRpm;
        public double EngineMaxRpm;
        public double Throttle;
        public bool Boost;
        public bool OnGround;
        public double Impact;
    }
}
