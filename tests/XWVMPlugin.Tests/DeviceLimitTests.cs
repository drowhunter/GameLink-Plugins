using Xunit;
using YawGLAPI;
using static XWVMPlugin.Tests.Packets;
using Plugin = YawVR_Game_Engine.Plugin.XWVMPlugin;

namespace XWVMPlugin.Tests;

public sealed class DeviceLimitTests
{
    internal sealed class Limits(byte forward, byte backward, byte roll) : IDeviceParameters
    {
        public byte PitchLimitF => forward;
        public byte PitchLimitB => backward;
        public byte RollLimit => roll;
        public byte Power => 100;
        public byte MaxRoll => 70;
        public byte MaxPitchFW => 75;
        public byte MaxPitchBW => 80;
        public uint YawLimit => 360;
        public float YawReturn => 0;
        public bool HasYawLimit => false;
        public bool HasVibration => false;
        public byte VibrationLimit => 0;
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
            // XWVM pitch is nose-down positive (forward tilt) and the default profile does not invert it.
            float expectedPitch = angle > 0
                ? forward * Math.Min(angle / 90f, 1)
                : -backward * Math.Min(-angle / 90f, 1);
            float expectedRoll = Math.Clamp(angle / 90f, -1, 1) * roll;
            AssertClose(Plugin.ScaleInput("Pitch", angle, limits), expectedPitch);
            AssertClose(Plugin.ScaleInput("Roll", angle, limits), expectedRoll);
            AssertClose(Plugin.ScaleInput("Yaw", angle, limits), angle);
            AssertClose(Plugin.ScaleInput("PitchInertia", angle, limits), angle);
            AssertClose(Plugin.ScaleInput("RollInertia", angle, limits), angle);
        }
    }

    [Theory]
    [InlineData(15, 55, 20)]
    [InlineData(40, 10, 7)]
    public void FinalDefaultProfileOutputsRespectDirectionalDeviceLimits(int forward, int backward, int roll)
    {
        var limits = new Limits((byte)forward, (byte)backward, (byte)roll);
        dynamic manager = CoreSourceCompiler.Create();
        manager.Configure(LoadProfile(), false, 30, 1f);
        var flight = new Flight();
        // Ramp in from level so the pitch spike filter (limit 10) sees no initial jump.
        for (int sample = 0; sample >= -360; sample--)
            manager.Step(Inputs(flight.Next(37, sample / 4f, sample / 4f), limits));
        float? previousPitch = null, previousRoll = null;
        for (int sample = -360; sample <= 360; sample++)
        {
            float angle = sample / 4f;
            var inputs = Inputs(flight.Next(37, angle, angle), limits);
            manager.Step(inputs);
            float pitchOutput = manager.GetCapturedOutput(1);
            float rollOutput = manager.GetCapturedOutput(2);
            // GameLink calibration drives final +F forward, -B backward. Nose-down (+) is forward.
            float expectedPitch = angle > 0 ? forward * (angle / 90f) : backward * (angle / 90f);
            Assert.True(Math.Abs(pitchOutput - expectedPitch) < 0.001f,
                $"Input pitch={angle}, F={forward}, B={backward}: final pitch={pitchOutput}, expected={expectedPitch}.");
            AssertClose(rollOutput, angle / 90f * roll);
            Assert.InRange(pitchOutput, -backward, forward);
            Assert.InRange(rollOutput, -roll, roll);
            if (previousPitch.HasValue)
            {
                Assert.InRange(pitchOutput - previousPitch.Value, -0.001f, Math.Max(forward, backward) / 360f + 0.001f);
                Assert.InRange(rollOutput - previousRoll!.Value, -0.001f, roll / 360f + 0.001f);
            }
            previousPitch = pitchOutput;
            previousRoll = rollOutput;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void PitchAndRollStaySmoothThroughLevel(int direction)
    {
        // Rate-based tilt crosses zero when the stick reverses; output must not jump at level.
        var limits = new Limits(30, 30, 30);
        dynamic manager = CoreSourceCompiler.Create();
        manager.Configure(LoadProfile(), false, 30, 1f);
        var flight = new Flight();
        for (int i = 0; i <= 80; i++)
        {
            float start = -direction * i * 0.25f;
            manager.Step(Inputs(flight.Next(90, start, start), limits));
        }
        float? previousPitch = null, previousRoll = null;
        for (int i = 0; i <= 160; i++)
        {
            float angle = direction * (-20 + i * 0.25f);
            manager.Step(Inputs(flight.Next(90, angle, angle), limits));
            float pitch = manager.GetCapturedOutput(1);
            float roll = manager.GetCapturedOutput(2);
            AssertClose(pitch, angle / 90f * 30);
            AssertClose(roll, angle / 90f * 30);
            if (previousPitch.HasValue)
            {
                Assert.True(Math.Abs(pitch - previousPitch.Value) <= 30 / 360f + 0.001f, $"pitch {previousPitch} -> {pitch}");
                Assert.True(Math.Abs(roll - previousRoll!.Value) <= 30 / 360f + 0.001f, $"roll {previousRoll} -> {roll}");
            }
            previousPitch = pitch;
            previousRoll = roll;
        }
    }

    private static void AssertClose(float actual, float expected) =>
        Assert.True(float.IsFinite(actual) && Math.Abs(actual - expected) < 0.001f, $"Expected {expected}, got {actual}.");
}
