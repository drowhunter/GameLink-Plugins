using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace DistancePlugin.Tests;

/// <summary>
/// Distance lets the car loop, barrel roll, drive on walls and upside down. These tests run those
/// manoeuvres through the mod's real telemetry maths, the plugin and the attached GameLink.Core
/// profile maths, and require the final yaw sent to the device to stay smooth.
/// </summary>
public sealed class YawOutputTests(ITestOutputHelper output)
{
    private const float Rad2Deg = 57.29578f;
    private const float Tolerance = 0.01f;

    [Theory]
    [MemberData(nameof(Maneuvers.All), MemberType = typeof(Maneuvers))]
    public void ModYawIsTheIntegralOfTheLocalYawRateSoItHasNoEulerSingularity(string name)
    {
        float? previous = null;
        foreach (var (step, rate, packet, _) in Maneuvers.Get(name).Run())
        {
            Assert.InRange(packet.Yaw, -360f, 180f);
            if (previous.HasValue)
            {
                float expected = rate.Y * DistanceModSimulator.FixedDeltaTime * Rad2Deg;
                float actual = Pipeline.AngularDelta(packet.Yaw, previous.Value);
                Assert.True(Math.Abs(actual - expected) <= Tolerance,
                    $"{name} step {step}: mod yaw moved {actual:F4} deg, local yaw rate implies {expected:F4} deg.");
            }
            previous = packet.Yaw;
        }
    }

    [Theory]
    [MemberData(nameof(Maneuvers.All), MemberType = typeof(Maneuvers))]
    public void IsolatedYawOutputIsSmoothDuringExtremeManeuvers(string name) =>
        AssertFinalYawFollowsLocalYawRate(name, Pipeline.LoadIsolatedYawProfile());

    [Theory]
    [MemberData(nameof(Maneuvers.All), MemberType = typeof(Maneuvers))]
    public void DefaultProfileYawOutputIsSmoothDuringExtremeManeuvers(string name) =>
        AssertFinalYawFollowsLocalYawRate(name, Pipeline.LoadProfile());

    [Theory]
    [MemberData(nameof(Maneuvers.All), MemberType = typeof(Maneuvers))]
    public void NumericYawDiscontinuitiesAreOnlyWholeTurns(string name)
    {
        dynamic core = Pipeline.CreateCore(Pipeline.LoadIsolatedYawProfile());
        float? previous = null;
        float min = float.MaxValue, max = float.MinValue;
        int wraps = 0;
        foreach (var (step, _, packet, _) in Maneuvers.Get(name).Run())
        {
            float yaw = core.Step(Pipeline.Inputs(packet));
            min = Math.Min(min, yaw);
            max = Math.Max(max, yaw);
            if (previous.HasValue)
            {
                float numeric = yaw - previous.Value;
                float angular = Pipeline.AngularDelta(yaw, previous.Value);
                float wrap = numeric - angular;
                Assert.True(Math.Abs(wrap) < Tolerance || Math.Abs(Math.Abs(wrap) - 360) < Tolerance,
                    $"{name} step {step}: numeric step {numeric:F4} deg is not a whole-turn wrap.");
                if (Math.Abs(wrap) >= Tolerance) wraps++;
            }
            previous = yaw;
        }
        output.WriteLine($"{name}: final yaw range [{min:F2}, {max:F2}] with {wraps} whole-turn wraps.");
    }

    /// <summary>
    /// Why the mod's Coaster yaw matters: a conventional Unity heading (eulerAngles.y, the YXZ
    /// decomposition) of the same rotation flips by ~180 deg as the nose passes vertical in a loop.
    /// </summary>
    [Theory]
    [InlineData("loop-nose-up")]
    [InlineData("loop-nose-down")]
    [InlineData("vertical-wall-steer")]
    public void ConventionalEulerHeadingWouldFlipDuringTheSameManeuver(string name)
    {
        float? previousEuler = null, previousMod = null;
        float worstEuler = 0, worstMod = 0;
        foreach (var (_, _, packet, car) in Maneuvers.Get(name).Run())
        {
            var q = car.Rotation;
            float euler = MathF.Atan2(2 * (q.W * q.Y + q.X * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y)) * Rad2Deg;
            if (previousEuler.HasValue)
            {
                worstEuler = Math.Max(worstEuler, Math.Abs(Pipeline.AngularDelta(euler, previousEuler.Value)));
                worstMod = Math.Max(worstMod, Math.Abs(Pipeline.AngularDelta(packet.Yaw, previousMod!.Value)));
            }
            previousEuler = euler;
            previousMod = packet.Yaw;
        }
        output.WriteLine($"{name}: worst Euler heading step {worstEuler:F2} deg, worst mod yaw step {worstMod:F2} deg.");
        Assert.True(worstEuler > 90, $"Expected the Euler heading to flip; worst step was {worstEuler:F2} deg.");
        Assert.True(worstMod <= 3.6f + Tolerance, $"Mod yaw must stay smooth; worst step was {worstMod:F2} deg.");
    }

    private void AssertFinalYawFollowsLocalYawRate(string name,
        System.Collections.ObjectModel.ObservableCollection<YawGLAPI.Profile_Component> profile)
    {
        dynamic core = Pipeline.CreateCore(profile);
        float? previous = null;
        float worst = 0;
        foreach (var (step, rate, packet, _) in Maneuvers.Get(name).Run())
        {
            float yaw = core.Step(Pipeline.Inputs(packet));
            Assert.True(float.IsFinite(yaw), $"{name} step {step}: final yaw is {yaw}.");
            if (previous.HasValue)
            {
                float expected = rate.Y * DistanceModSimulator.FixedDeltaTime * Rad2Deg;
                float actual = Pipeline.AngularDelta(yaw, previous.Value);
                worst = Math.Max(worst, Math.Abs(actual));
                Assert.True(Math.Abs(actual - expected) <= Tolerance,
                    $"{name} step {step}: final yaw moved {actual:F4} deg, expected {expected:F4} deg "
                    + $"(pitch {packet.Pitch:F2}, roll {packet.Roll:F2}).");
            }
            previous = yaw;
        }
        output.WriteLine($"{name}: worst final yaw step {worst:F4} deg.");
    }
}
