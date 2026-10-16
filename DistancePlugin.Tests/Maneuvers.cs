using System.Numerics;

namespace DistancePlugin.Tests;

/// <summary>
/// Extreme Distance manoeuvres expressed as the car's local angular velocity (rad/s) per physics step.
/// Unity axes: X = right (pitch, +X is nose down), Y = up (yaw), Z = forward (roll).
/// </summary>
internal static class Maneuvers
{
    private const float Pi = MathF.PI;

    public sealed record Segment(float Seconds, Vector3 AngularVelocity);

    public sealed record Maneuver(string Name, Quaternion Start, Func<int, Vector3> Rate, int Steps)
    {
        public override string ToString() => Name;
    }

    public static IEnumerable<object[]> All => Library.Select(m => new object[] { m.Name });

    public static Maneuver Get(string name) => Library.Single(m => m.Name == name);

    private static readonly Maneuver[] Library =
    [
        FromSegments("flat-spin-right", new Segment(4, new(0, Pi, 0))),
        FromSegments("flat-spin-left", new Segment(4, new(0, -Pi, 0))),
        FromSegments("barrel-roll-right", new Segment(4, new(0, 0, -Pi))),
        FromSegments("barrel-roll-left", new Segment(4, new(0, 0, Pi))),
        FromSegments("loop-nose-up", new Segment(4, new(-Pi, 0, 0))),
        FromSegments("loop-nose-down", new Segment(4, new(Pi, 0, 0))),
        FromSegments("loop-while-turning", new Segment(6, new(-Pi, Pi / 3, 0))),
        FromSegments("corkscrew", new Segment(8, new(0, Pi / 2, Pi))),
        FromSegments("wall-ride-two-turns",
            new Segment(0.5f, new(0, 0, -Pi)), new Segment(8, new(0, Pi / 2, 0)), new Segment(0.5f, new(0, 0, Pi))),
        FromSegments("upside-down-two-turns",
            new Segment(1, new(0, 0, Pi)), new Segment(8, new(0, -Pi / 2, 0)), new Segment(1, new(0, 0, -Pi))),
        // Nose exactly vertical (forward has no horizontal component), then steer on the vertical wall.
        FromSegments("vertical-wall-steer",
            new Segment(0.5f, new(-Pi, 0, 0)), new Segment(8, new(0, Pi / 2, 0)), new Segment(0.5f, new(Pi, 0, 0))),
        FromSegments("long-spin-60-turns", new Segment(60, new(0, 2 * Pi, 0))),
        Tumble("random-tumble-1", 1),
        Tumble("random-tumble-2", 2),
        Tumble("random-tumble-3", 3),
        Tumble("random-tumble-4", 4),
    ];

    private static Maneuver FromSegments(string name, params Segment[] segments)
    {
        var rates = segments.SelectMany(s =>
            Enumerable.Repeat(s.AngularVelocity, (int)MathF.Round(s.Seconds / DistanceModSimulator.FixedDeltaTime)))
            .ToArray();
        // Start from a non-trivial heading so nothing depends on beginning at yaw 0.
        return new(name, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f), i => rates[i], rates.Length);
    }

    // Smooth, seeded random rotation about all axes at up to ~2 turns per second per axis.
    private static Maneuver Tumble(string name, int seed)
    {
        var random = new Random(seed);
        var waves = Enumerable.Range(0, 3).Select(_ => Enumerable.Range(0, 3).Select(_ => (
            amplitude: (float)random.NextDouble() * 1.5f * Pi,
            frequency: 0.05f + (float)random.NextDouble() * 0.6f,
            phase: (float)random.NextDouble() * 2 * Pi)).ToArray()).ToArray();
        float Axis(int axis, float t) => waves[axis].Sum(w => w.amplitude * MathF.Sin(2 * Pi * w.frequency * t + w.phase)) / 1.5f;
        var start = Quaternion.CreateFromYawPitchRoll(
            (float)random.NextDouble() * 2 * Pi, (float)random.NextDouble() * 2 * Pi, (float)random.NextDouble() * 2 * Pi);
        return new(name, start, i =>
        {
            float t = i * DistanceModSimulator.FixedDeltaTime;
            return new(Axis(0, t), Axis(1, t), Axis(2, t));
        }, (int)(60 / DistanceModSimulator.FixedDeltaTime));
    }

    /// <summary>Runs the manoeuvre through the mod and yields each packet with the rate that produced it.</summary>
    public static IEnumerable<(int Step, Vector3 Rate, ModPacket Packet, DistanceModSimulator Car)> Run(this Maneuver maneuver)
    {
        var car = new DistanceModSimulator(maneuver.Start);
        for (int i = 0; i < maneuver.Steps; i++)
        {
            var rate = maneuver.Rate(i);
            yield return (i, rate, car.Step(rate), car);
        }
    }
}
