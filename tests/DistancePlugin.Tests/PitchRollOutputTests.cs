using System.Collections.ObjectModel;
using Xunit;
using Xunit.Abstractions;
using YawGLAPI;
using Plugin = YawVR_Game_Engine.Plugin.DistancePlugin;

namespace DistancePlugin.Tests;

/// <summary>
/// Pitch and roll from the mod are the angles of the car's forward/right axes to the horizontal
/// plane, so they fold at +/-90 (a loop goes 0 -> 90 -> 0 -> -90 -> 0) instead of wrapping.
/// </summary>
public sealed class PitchRollOutputTests(ITestOutputHelper output)
{
    private const float Rad2Deg = 57.29578f;
    // The mod measures pitch/roll with float acos near 1 (Unity Vector3.Angle), which only resolves
    // ~0.03 deg around level. That noise is real game output, so allow it in continuity bounds.
    private const float Tolerance = 0.05f;

    [Theory]
    [MemberData(nameof(Maneuvers.All), MemberType = typeof(Maneuvers))]
    public void ModPitchAndRollAreBoundedAndContinuous(string name)
    {
        ModPacket? previous = null;
        foreach (var (step, rate, packet, _) in Maneuvers.Get(name).Run())
        {
            Assert.InRange(packet.Pitch, -90f, 90f);
            Assert.InRange(packet.Roll, -90f, 90f);
            if (previous.HasValue)
            {
                // An axis turning at |w| cannot change its angle to the horizontal faster than |w|.
                float bound = rate.Length() * DistanceModSimulator.FixedDeltaTime * Rad2Deg + Tolerance;
                Assert.True(Math.Abs(packet.Pitch - previous.Value.Pitch) <= bound,
                    $"{name} step {step}: pitch jumped {previous.Value.Pitch:F3} -> {packet.Pitch:F3} (bound {bound:F3}).");
                Assert.True(Math.Abs(packet.Roll - previous.Value.Roll) <= bound,
                    $"{name} step {step}: roll jumped {previous.Value.Roll:F3} -> {packet.Roll:F3} (bound {bound:F3}).");
            }
            previous = packet;
        }
    }

    [Theory]
    [MemberData(nameof(Maneuvers.All), MemberType = typeof(Maneuvers))]
    public void FinalOrientationOutputsAreContinuousAndWithinDeviceLimits(string name)
    {
        var limits = new Limits(25, 40, 20);
        dynamic core = Pipeline.CreateCore(LoadOrientationOnlyProfile());
        float? previousPitch = null, previousRoll = null;
        float minPitch = float.MaxValue, maxPitch = float.MinValue, minRoll = float.MaxValue, maxRoll = float.MinValue;
        foreach (var (step, rate, packet, _) in Maneuvers.Get(name).Run())
        {
            core.Step(Pipeline.Inputs(packet, limits));
            float pitch = core.GetCapturedOutput(1);
            float roll = core.GetCapturedOutput(2);
            Assert.InRange(pitch, -limits.PitchLimitB - Tolerance, limits.PitchLimitF + Tolerance);
            Assert.InRange(roll, -limits.RollLimit - Tolerance, limits.RollLimit + Tolerance);
            if (previousPitch.HasValue)
            {
                float degrees = rate.Length() * DistanceModSimulator.FixedDeltaTime * Rad2Deg;
                float pitchBound = degrees * Math.Max(limits.PitchLimitF, limits.PitchLimitB) / 90f + Tolerance;
                float rollBound = degrees * limits.RollLimit / 90f + Tolerance;
                Assert.True(Math.Abs(pitch - previousPitch.Value) <= pitchBound,
                    $"{name} step {step}: final pitch jumped {previousPitch:F3} -> {pitch:F3} (bound {pitchBound:F3}).");
                Assert.True(Math.Abs(roll - previousRoll!.Value) <= rollBound,
                    $"{name} step {step}: final roll jumped {previousRoll:F3} -> {roll:F3} (bound {rollBound:F3}).");
            }
            previousPitch = pitch;
            previousRoll = roll;
            minPitch = Math.Min(minPitch, pitch); maxPitch = Math.Max(maxPitch, pitch);
            minRoll = Math.Min(minRoll, roll); maxRoll = Math.Max(maxRoll, roll);
        }
        output.WriteLine($"{name}: final pitch [{minPitch:F2}, {maxPitch:F2}], final roll [{minRoll:F2}, {maxRoll:F2}].");
    }

    [Theory]
    [InlineData(15, 55, 20)]
    [InlineData(40, 10, 7)]
    [InlineData(0, 35, 0)]
    public void ScalingUsesConfiguredAxisLimitsNotMaximums(int forward, int backward, int roll)
    {
        var limits = new Limits((byte)forward, (byte)backward, (byte)roll);
        for (int angle = -180; angle <= 180; angle++)
        {
            // Mod pitch is negative nose-down. Nose-down maps to the forward limit, nose-up to backward.
            float expectedPitch = angle < 0
                ? -forward * Math.Min(-angle / 90f, 1)
                : backward * Math.Min(angle / 90f, 1);
            float expectedRoll = Math.Clamp(angle / 90f, -1, 1) * roll;
            AssertClose(Plugin.ScaleInput("Pitch", angle, limits), expectedPitch, $"pitch {angle}");
            AssertClose(Plugin.ScaleInput("Roll", angle, limits), expectedRoll, $"roll {angle}");
            AssertClose(Plugin.ScaleInput("Yaw", angle, limits), angle, $"yaw {angle}");
        }
    }

    /// <summary>
    /// The default profile inverts pitch, so nose-down reaches the device as +PitchLimitF and
    /// nose-up as -PitchLimitB. This is the direction convention of the (playable) Distance profile.
    /// </summary>
    [Theory]
    [InlineData(15, 55, 20)]
    [InlineData(40, 10, 7)]
    public void FinalDefaultOrientationChannelsUseTheMatchingDirectionalLimit(int forward, int backward, int roll)
    {
        var limits = new Limits((byte)forward, (byte)backward, (byte)roll);
        dynamic core = Pipeline.CreateCore(LoadOrientationOnlyProfile());
        for (int sample = -360; sample <= 360; sample++)
        {
            float angle = sample / 4f;
            var packet = new ModPacket { Pitch = angle, Roll = angle, IsCarEnabled = true, IsCarIsActive = true };
            core.Step(Pipeline.Inputs(packet, limits));
            float pitch = core.GetCapturedOutput(1);
            float rollOutput = core.GetCapturedOutput(2);
            float expectedPitch = angle < 0 ? forward * (-angle / 90f) : -backward * (angle / 90f);
            AssertClose(pitch, expectedPitch, $"pitch input {angle} (F={forward}, B={backward})");
            AssertClose(rollOutput, angle / 90f * roll, $"roll input {angle} (R={roll})");
        }
    }

    /// <summary>
    /// The default profile with only the orientation angle channels (yaw, pitch, roll) enabled, each
    /// with its shipped settings. Suspension, boost and sway channels add motion cues on top of these.
    /// </summary>
    private static ObservableCollection<Profile_Component> LoadOrientationOnlyProfile()
    {
        var profile = Pipeline.LoadProfile();
        int[] angles = [Pipeline.Index("Yaw"), Pipeline.Index("Pitch"), Pipeline.Index("Roll")];
        // angles[i] is the input that drives output i (0 = yaw, 1 = pitch, 2 = roll).
        foreach (var component in profile)
            component.Enabled &= component.Output_index == Array.IndexOf(angles, component.Input_index);
        Assert.Equal(3, profile.Count(c => c.Enabled));
        return profile;
    }

    private static void AssertClose(float actual, float expected, string context) =>
        Assert.True(float.IsFinite(actual) && Math.Abs(actual - expected) < 0.001f,
            $"{context}: expected {expected}, got {actual}.");
}
