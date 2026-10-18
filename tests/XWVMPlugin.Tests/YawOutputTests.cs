using System.Collections.ObjectModel;
using Xunit;
using Xunit.Abstractions;
using YawGLAPI;
using YawVR_Game_Engine.Plugin;
using static XWVMPlugin.Tests.Packets;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace XWVMPlugin.Tests;

public sealed class YawOutputTests(ITestOutputHelper log)
{
    private const float StepDegrees = 0.25f;
    private const float Tolerance = 0.02f;
    private static readonly DeviceLimitTests.Limits Limits = new(25, 40, 20);

    [Fact]
    public void ProfileKeepsYawAtInput22AndOutputZero()
    {
        var yaw = Assert.Single(LoadProfile(), c => c.Enabled && c.Output_index == 0);
        Assert.Equal(Yaw, yaw.Input_index);
        Assert.False(yaw.Inverse);
        Assert.False(yaw.Constant);
        Assert.Equal(ProfileComponentType.VALUE, yaw.Type);
        Assert.Equal(1f, yaw.Smoothing);
        Assert.Equal(-1f, yaw.Limit);
        Assert.Equal(1f, yaw.MultiplierPos);
        Assert.Equal(1f, yaw.MultiplierNeg);
        Assert.Empty(yaw.Math);
        Assert.Empty(yaw.Condition);
        Assert.False(yaw.Spikeflatter.Enabled);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(1, 35, -25)]
    [InlineData(-1, -35, 25)]
    [InlineData(1, 89, 80)]
    [InlineData(-1, -89, -80)]
    public void MultipleFullTurnsHaveSmoothFinalAngularOutput(int direction, float pitch, float roll)
    {
        dynamic pipeline = NewPipeline();
        var flight = new Flight();
        float? previous = null;
        float maxRawStep = 0, maxAngularStep = 0;
        for (int i = 0; i <= 5760; i++)
        {
            float yaw = direction * (-720 + i * StepDegrees);
            float output = Output(pipeline, flight, yaw, pitch, roll, i);
            Close(AngularDelta(output, yaw), 0, $"yaw={yaw}, pitch={pitch}, roll={roll}");
            if (previous.HasValue)
            {
                float delta = AngularDelta(output, previous.Value);
                Close(delta, direction * StepDegrees, $"final yaw step at input {yaw}");
                maxRawStep = Math.Max(maxRawStep, Math.Abs(output - previous.Value));
                maxAngularStep = Math.Max(maxAngularStep, Math.Abs(delta));
            }
            previous = output;
        }
        log.WriteLine($"Max numeric step: {maxRawStep:F4} deg; max shortest-arc step: {maxAngularStep:F4} deg.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(179.9f)]
    [InlineData(359.9f)]
    public void SimultaneousSmoothYawPitchAndRollStaySmooth(float startHeading)
    {
        dynamic pipeline = NewPipeline();
        var flight = new Flight();
        float? previous = null;
        for (int i = 0; i <= 14400; i++)
        {
            float t = i * MathF.PI / 1800;
            float yaw = startHeading + i * StepDegrees;
            float output = Output(pipeline, flight, yaw, 80 * MathF.Sin(t), 170 * MathF.Sin(t * 0.7f), i);
            Close(AngularDelta(output, yaw), 0, $"combined rotations at sample {i}");
            if (previous.HasValue)
                Close(AngularDelta(output, previous.Value), StepDegrees, $"combined final step {i}");
            previous = output;
        }
    }

    [Theory]
    [InlineData("pitch", 1)]
    [InlineData("pitch", -1)]
    [InlineData("roll", 1)]
    [InlineData("roll", -1)]
    public void FullPitchOrRollRotationsDoNotChangeFinalYaw(string axis, int direction)
    {
        dynamic pipeline = NewPipeline();
        var flight = new Flight();
        for (int i = 0; i <= 2880; i++)
        {
            float angle = direction * (-360 + i * StepDegrees);
            float output = Output(pipeline, flight, 37, axis == "pitch" ? angle : 20, axis == "roll" ? angle : 0, i);
            Close(AngularDelta(output, 37), 0, $"{axis}={angle}");
        }
    }

    [Fact]
    public void SeededSmoothOrientationWalkHasNoUnexpectedFinalYawSteps()
    {
        var random = new Random(25151);
        dynamic pipeline = NewPipeline();
        var flight = new Flight();
        float yaw = 0, pitch = 0, roll = 0;
        float previous = Output(pipeline, flight, yaw, pitch, roll, 0);
        for (int i = 1; i <= 20000; i++)
        {
            float dy = (float)(random.NextDouble() - 0.5);
            yaw += dy;
            pitch = Math.Clamp(pitch + (float)(random.NextDouble() - 0.5), -85, 85);
            roll += (float)(random.NextDouble() - 0.5);
            float output = Output(pipeline, flight, yaw, pitch, roll, i);
            Close(AngularDelta(output, previous), dy, $"random sample {i}");
            previous = output;
        }
    }

    [Fact]
    public void AllHeadingDegreesAtAGridOfTiltsProduceTheExpectedFinalHeading()
    {
        int samples = 0;
        foreach (int pitch in new[] { -89, -60, -20, 0, 20, 60, 89 })
        {
            for (int roll = -180; roll <= 180; roll += 45)
            {
                dynamic pipeline = NewPipeline();
                var flight = new Flight();
                float? previous = null;
                for (int yaw = 0; yaw <= 720; yaw++)
                {
                    float output = Output(pipeline, flight, yaw, pitch, roll, samples++);
                    Close(AngularDelta(output, yaw), 0, $"heading grid: yaw={yaw}, pitch={pitch}, roll={roll}");
                    if (previous.HasValue)
                        Close(AngularDelta(output, previous.Value), 1, $"heading grid final step at {yaw}");
                    previous = output;
                }
            }
        }
        log.WriteLine($"Checked {samples} final headings across the heading/tilt grid.");
    }

    [Fact]
    public void UnityHeadingWrapIsANumericJumpButNotAnAngularReversal()
    {
        dynamic pipeline = NewPipeline();
        var flight = new Flight();
        // rot.y 359.75 -> 0.25 on the wire.
        float before = Output(pipeline, flight, 359.75f, 10, 15, 0);
        float after = Output(pipeline, flight, 360.25f, 10, 15, 1);
        Close(AngularDelta(after, before), 0.5f, "final shortest-arc motion across Unity 0/360");
        // Core's own +/-180 wrap.
        before = Output(pipeline, flight, 179.75f, 10, 15, 2);
        after = Output(pipeline, flight, 180.25f, 10, 15, 3);
        Assert.True(Math.Abs(after - before) > 359, $"Expected wrapped output; got {before} -> {after}.");
        Close(AngularDelta(after, before), 0.5f, "final shortest-arc motion across Core +/-180");
    }

    [Theory]
    [InlineData(125, 1)]
    [InlineData(235, -1)]
    public void ResetYawOffsetKeepsSubsequentFinalHeadingSmooth(float start, int direction)
    {
        dynamic pipeline = NewPipeline();
        var flight = new Flight();
        pipeline.RequestYawReset();
        float previous = Output(pipeline, flight, start, 15, -10, 0);
        Close(AngularDelta(previous, 0), 0, "first output after reset");
        for (int i = 1; i <= 2880; i++)
        {
            float output = Output(pipeline, flight, start + direction * i * StepDegrees, 15, -10, i);
            Close(AngularDelta(output, previous), direction * StepDegrees, $"reset sample {i}");
            previous = output;
        }
    }

    [Fact]
    public void CapturedJsonFlightProducesSmoothFinalYaw()
    {
        dynamic pipeline = NewPipeline();
        var converter = new XwvmConverter();
        float? previousOutput = null, previousInput = null;
        int frame = 0;
        foreach (var line in File.ReadLines(DataFile("json-capture.jsonl")))
        {
            var inputs = Inputs(Decode(converter, System.Text.Encoding.UTF8.GetBytes(line)), Limits);
            float output = pipeline.Step(inputs);
            Close(AngularDelta(output, inputs[Yaw]), 0, $"captured frame {frame}");
            if (previousOutput.HasValue)
                Close(AngularDelta(output, previousOutput.Value), AngularDelta(inputs[Yaw], previousInput!.Value),
                    $"captured final step {frame}");
            previousOutput = output;
            previousInput = inputs[Yaw];
            frame++;
        }
    }

    [Theory]
    [InlineData("Shake", 50f)]
    [InlineData("WarheadFired", 50f)]
    public void ImpactsDriveVibrationAmplitude(string input, float expected)
    {
        dynamic pipeline = NewPipeline();
        var inputs = new float[InputCount];
        inputs[Array.IndexOf(new YawVR_Game_Engine.Plugin.XWVMPlugin().GetInputData(), input)] = 1;
        pipeline.Step(inputs);
        Close(pipeline.GetCapturedOutput(3), expected, $"{input} amplitude");
        Close(pipeline.GetCapturedOutput(4), 1, "constant vibration frequency");
    }

    [Fact]
    public void VibrationAmplitudeIsClampedTo254()
    {
        dynamic pipeline = NewPipeline();
        var inputs = new float[InputCount];
        inputs[8] = 10;
        inputs[13] = 10;
        pipeline.Step(inputs);
        Close(pipeline.GetCapturedOutput(3), 254, "clamped amplitude");
    }

    // These contracts intentionally expose defects in the current profile/Core combination.
    // They must not be weakened or skipped to make the suite green.
    [Fact]
    public void LaserFireMustProduceVibration()
    {
        dynamic pipeline = NewPipeline();
        var inputs = new float[InputCount];
        float maximum = 0;
        for (int i = 0; i < 20; i++)
        {
            inputs[12] = i % 10 < 5 ? 1 : 0;
            pipeline.Step(inputs);
            maximum = Math.Max(maximum, pipeline.GetCapturedOutput(3));
        }
        Assert.True(maximum > 0, $"LaserFired never produced vibration amplitude (maximum={maximum}). "
            + "The default profile inverts input 12, so Core's 0..254 amplitude clamp discards it.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AntijumpMustNotDiscardASmoothTurnAtTheHeadingBoundary(int direction)
    {
        dynamic pipeline = NewPipeline(antijump: true);
        var flight = new Flight();
        float previous = Output(pipeline, flight, 0, 0, 0, 0);
        float maximumError = 0;
        string worst = "";
        for (int i = 1; i <= 1600; i++)
        {
            float yaw = direction * i * StepDegrees;
            float output = Output(pipeline, flight, yaw, 0, 0, i);
            float error = Math.Abs(AngularDelta(output, previous) - direction * StepDegrees);
            if (!float.IsFinite(output) || error > maximumError)
            {
                maximumError = float.IsFinite(output) ? error : float.PositiveInfinity;
                worst = $"anti-jump input={yaw}, previous final={previous}, final={output}";
            }
            previous = output;
        }
        Assert.True(maximumError <= Tolerance, $"{worst}: largest final-step error={maximumError} deg.");
    }

    [Theory]
    [InlineData(1, 0.02f)]
    [InlineData(-1, 0.02f)]
    [InlineData(1, 0.5f)]
    [InlineData(-1, 0.5f)]
    public void WashoutMustNotCreateALargeFinalAngularJumpDuringASmoothTurn(int direction, float washout)
    {
        dynamic pipeline = NewPipeline(washout: washout);
        var flight = new Flight();
        float previous = Output(pipeline, flight, 0, 10, -15, 0);
        float maximumStep = 0;
        string worst = "";
        for (int i = 1; i <= 1600; i++)
        {
            float yaw = direction * i * StepDegrees;
            float output = Output(pipeline, flight, yaw, 10, -15, i);
            float delta = AngularDelta(output, previous);
            if (!float.IsFinite(output) || Math.Abs(delta) > maximumStep)
            {
                maximumStep = float.IsFinite(output) ? Math.Abs(delta) : float.PositiveInfinity;
                worst = $"Washout={washout}, input={yaw}: final yaw {previous} -> {output}, angular step={delta} deg.";
            }
            previous = output;
        }
        Assert.True(maximumStep <= StepDegrees + Tolerance, worst);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void IsolatedYawEqualsDecodedYawOneToOne(int direction)
    {
        dynamic pipeline = CoreSourceCompiler.Create();
        pipeline.Configure(LoadIsolatedProfile(), false, 30, 1f);
        var flight = new Flight();
        for (int i = 0; i <= 5760; i++)
        {
            float yaw = direction * i * StepDegrees;
            float output = Output(pipeline, flight, yaw, 30 * MathF.Sin(i / 100f), 45 * MathF.Cos(i / 70f), i, assertOneToOne: true);
            Close(AngularDelta(output, yaw), 0, $"isolated yaw {yaw}");
        }
    }

    [Fact]
    public void IsolatedProfileEnablesOnlyUnfilteredOneToOneYaw()
    {
        var yaw = Assert.Single(LoadIsolatedProfile(), c => c.Enabled);
        Assert.Equal(Yaw, yaw.Input_index);
        Assert.Equal(0, yaw.Output_index);
        Assert.False(yaw.Inverse);
        Assert.Equal(1f, yaw.MultiplierPos);
        Assert.Equal(1f, yaw.Smoothing);
        Assert.False(yaw.Spikeflatter.Enabled);
    }

    private static dynamic NewPipeline(bool antijump = false, float washout = 1)
    {
        dynamic manager = CoreSourceCompiler.Create();
        manager.Configure(LoadProfile(), antijump, 30, washout);
        return manager;
    }

    private static ObservableCollection<Profile_Component> LoadIsolatedProfile()
    {
        var profile = LoadProfile();
        var yaw = profile.Single(c => !c.Constant && c.Input_index == Yaw && c.Output_index == 0);
        foreach (var component in profile)
            component.Enabled = ReferenceEquals(component, yaw);
        yaw.Inverse = false;
        yaw.Type = ProfileComponentType.VALUE;
        yaw.MultiplierPos = yaw.MultiplierNeg = 1;
        yaw.Smoothing = 1;
        yaw.Limit = -1;
        yaw.Offset = yaw.Deadzone = 0;
        yaw.Spikeflatter.Enabled = false;
        yaw.Math.Clear();
        yaw.Condition.Clear();
        return profile;
    }

    private static float Output(dynamic manager, Flight flight, float yaw, float pitch, float roll, int frame,
        bool assertOneToOne = false)
    {
        var inputs = Inputs(flight.Next(yaw, pitch, roll), Limits);
        Assert.Equal(InputCount, inputs.Length);
        float output = manager.Step(inputs);
        if (assertOneToOne)
        {
            float wrapped = (inputs[Yaw] + 180f) % 360f - 180f;
            Close(AngularDelta(output, inputs[Yaw]), 0, $"isolated final yaw must equal decoded yaw at frame {frame}");
            Assert.True(Math.Abs(output - wrapped) <= Tolerance || Math.Abs(Math.Abs(output - wrapped) - 360) <= Tolerance,
                $"frame {frame}: output {output}, wrapped input {wrapped}");
            for (int i = 1; i < 5; i++)
                Close((float)manager.GetCapturedOutput(i), 0, $"disabled output channel {i}");
        }
        return output;
    }

    private static float AngularDelta(float to, float from) =>
        (float)Math.IEEERemainder((double)to - from, 360);

    private static void Close(float actual, float expected, string context) =>
        Assert.True(float.IsFinite(actual) && Math.Abs(actual - expected) <= Tolerance,
            $"{context}: expected {expected:F6}, got {actual:F6} (tolerance {Tolerance}).");
}
