using System;

namespace YawVR_Game_Engine.Plugin
{
    /// <summary>
    /// Flat list of plugin inputs (field order = GameLink input index).
    /// The motion values are the ones SC-Motion-Haptic-Bridge sends to SimHub, except that the linear
    /// accelerations are not multiplied by the bridge's LM_SCALE (0.1): they are m/s2.
    /// </summary>
    public struct Telemetry
    {
        /// <summary>Heading 0..360 accumulated from YawRate (the game sends no orientation).</summary>
        public float Yaw;

        // deg/s, same signs as the bridge's SimHub feed: yaw = -w.z, pitch = -w.x, roll = +w.y (id35)
        public float YawRate;
        public float PitchRate;
        public float RollRate;

        // m/s2, body axes, gravity and drag included
        public float Surge;
        public float Sway;
        public float Heave;

        // m/s, body axes
        public float VelocityForward;
        public float VelocityLateral;
        public float VelocityUp;

        public float Speed;
        public float EngineRpm;
        public float EngineMaxRpm;
        public float Throttle;
        public float EngineOn;
        public float Boost;
        public float OnGround;

        /// <summary>Collision / touchdown / hit pulse, 0..1.</summary>
        public float Impact;

        /// <summary>Seat gate gain 0..1: every seat-gated input is already multiplied by it.</summary>
        public float Seated;

        /// <summary>1 while vehicle frames arrive (none for 0.3 s -> 0).</summary>
        public float Active;
    }

    /// <summary>
    /// lm_simhub4.py's send loop without the sockets: feed every event with <see cref="OnPacket"/> and call
    /// <see cref="Tick"/> at <see cref="Rate"/> Hz.
    /// </summary>
    internal sealed class ScBridge
    {
        public const double Rate = 100;
        public const double Stale = 0.3;
        private const double MaxHeadingGap = 0.1;

        private readonly ScPath path = new();
        private readonly ScSeat seat = new();
        private readonly ScHaptics haptics = new();
        private System.Collections.Generic.IReadOnlyDictionary<int, object> d3;
        private double? tRx;
        private double heading;
        private double? lastTick;

        public ScHaptics Haptics => haptics;

        public void OnPacket(ScPacket p, double now)
        {
            if (p == null) return;
            haptics.OnEvent(p.Ev, p.Data, now);
            if (p.Ev == 3)
            {
                d3 = p.Data;
                tRx = now;
            }
            else if (p.Ev == 10 || p.Ev == 2)
            {
                seat.OnEvent(p.Ev, p.Data, now);
            }
        }

        public Telemetry Tick(double now)
        {
            var seated = seat.Seated(now);
            var active = tRx != null && now - tRx < Stale;
            var f = active ? path.Active(d3) : path.Stale(Rate);
            f = seat.Apply(f, now, 1.0 / Rate);
            var g = seat.G;
            // stale: everything 0 except the ground state, which is held
            var ex = active ? haptics.Values(now, seated, g) : haptics.Values(now, false, 0.0);

            if (lastTick != null && now > lastTick && now - lastTick <= MaxHeadingGap)
            {
                heading = (heading + f[0] * (now - lastTick.Value)) % 360.0;
                if (heading < 0) heading += 360.0;
            }
            lastTick = now;

            return new Telemetry
            {
                Yaw = (float)heading,
                YawRate = (float)f[0],
                PitchRate = (float)f[1],
                RollRate = (float)f[2],
                Surge = (float)f[3],
                Sway = (float)f[4],
                Heave = (float)f[5],
                VelocityForward = (float)f[6],
                VelocityLateral = (float)f[7],
                VelocityUp = (float)f[8],
                Speed = (float)ex.SpeedKmh,
                EngineRpm = (float)ex.EngineRpm,
                EngineMaxRpm = (float)ex.EngineMaxRpm,
                Throttle = (float)ex.Throttle,
                EngineOn = ex.EngineOn ? 1f : 0f,
                Boost = ex.Boost ? 1f : 0f,
                OnGround = ex.OnGround ? 1f : 0f,
                Impact = (float)ex.Impact,
                Seated = (float)g,
                Active = active ? 1f : 0f,
            };
        }
    }
}
