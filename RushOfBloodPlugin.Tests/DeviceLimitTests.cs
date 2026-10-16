using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using Xunit;
using YawGLAPI;
using Plugin = YawVR_Game_Engine.Plugin.RushOfBloodPlugin;

namespace RushOfBloodPlugin.Tests;

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
            float expectedPitch = angle < 0
                ? -forward * Math.Min(-angle / 90f, 1)
                : backward * Math.Min(angle / 90f, 1);
            float expectedRoll = Math.Clamp(angle / 90f, -1, 1) * roll;
            AssertClose(Plugin.ScaleInput("Pitch", angle, limits), expectedPitch);
            AssertClose(Plugin.ScaleInput("Roll", angle, limits), expectedRoll);
            AssertClose(Plugin.ScaleInput("Yaw", angle, limits), angle);
        }
    }

    [Theory]
    [InlineData(15, 55, 20)]
    [InlineData(40, 10, 7)]
    public void FinalDefaultProfileOutputsRespectDirectionalDeviceLimits(int forward, int backward, int roll)
    {
        var limits = new Limits((byte)forward, (byte)backward, (byte)roll);
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream(
            "RushOfBloodPlugin.Resources.Default.yawglprofile")!;
        using var reader = new StreamReader(stream);
        var components = JObject.Parse(reader.ReadToEnd())["Components"]!
            .ToObject<ObservableCollection<Profile_Component>>()!;
        dynamic manager = CoreSourceCompiler.Create();
        manager.Configure(components, false, 30, 1f);
        float? previousPitch = null, previousRoll = null;
        for (int sample = -360; sample <= 360; sample++)
        {
            float angle = sample / 4f;
            var inputs = new float[15];
            inputs[2] = Plugin.ScaleInput("Pitch", angle, limits);
            inputs[3] = Plugin.ScaleInput("Roll", angle, limits);
            manager.Step(inputs);
            float pitchOutput = manager.GetCapturedOutput(1);
            float rollOutput = manager.GetCapturedOutput(2);
            // Default profile inverts both axes. GameLink calibration drives final +F forward, -B backward.
            float expectedPitch = angle < 0 ? forward * (-angle / 90f) : -backward * (angle / 90f);
            Assert.True(Math.Abs(pitchOutput - expectedPitch) < 0.001f,
                $"Input pitch={angle}, F={forward}, B={backward}: final pitch={pitchOutput}, "
                + $"expected={expectedPitch}. Profile inversion must not exchange physical forward/backward limits.");
            AssertClose(rollOutput, -angle / 90f * roll);
            Assert.InRange(pitchOutput, -backward, forward);
            Assert.InRange(rollOutput, -roll, roll);
            if (previousPitch.HasValue)
            {
                float pitchStep = previousPitch.Value - pitchOutput;
                float rollStep = previousRoll!.Value - rollOutput;
                Assert.InRange(pitchStep, -0.001f, Math.Max(forward, backward) / 360f + 0.001f);
                Assert.InRange(rollStep, -0.001f, roll / 360f + 0.001f);
            }
            previousPitch = pitchOutput;
            previousRoll = rollOutput;
        }
    }

    private static void AssertClose(float actual, float expected) =>
        Assert.True(float.IsFinite(actual) && Math.Abs(actual - expected) < 0.001f,
            $"Expected {expected}, got {actual}.");
}
