using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using SharedLib;
using SharedLib.TelemetryHelper;
using Xunit;
using Xunit.Abstractions;
using YawGLAPI;
using YawVR_Game_Engine.Plugin;
using Quaternion = System.Numerics.Quaternion;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RushOfBloodPlugin.Tests;

public sealed class YawOutputTests(ITestOutputHelper log)
{
    private const float StepDegrees = 0.25f;
    private const float Tolerance = 0.02f;

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, -1)]
    [InlineData(-1, 1)]
    [InlineData(-1, -1)]
    public void IsolatedYawStaysSmoothWithPitchLoopsAndLimitedRoll(int yawDirection, int pitchDirection)
    {
        dynamic pipeline = CoreSourceCompiler.Create();
        pipeline.Configure(LoadIsolatedProfile(), false, 30, 1f);
        float? previous = null;
        float maximumError = 0, maximumAngularStep = 0;
        string worst = "";
        for (int i = 0; i <= 28800; i++)
        {
            float yaw = yawDirection * i * StepDegrees;
            float pitch = pitchDirection * i * StepDegrees;
            float roll = 45f * MathF.Sin(i * MathF.PI / 2880f);
            Assert.InRange(roll, -45f, 45f);
            float output = Output(pipeline, yaw, pitch, roll, i, assertOneToOne: true);
            Assert.True(float.IsFinite(output), $"Non-finite final yaw at sample {i}.");
            if (previous.HasValue)
            {
                float delta = AngularDelta(output, previous.Value);
                float error = Math.Abs(delta - yawDirection * StepDegrees);
                maximumAngularStep = Math.Max(maximumAngularStep, Math.Abs(delta));
                if (error > maximumError)
                {
                    maximumError = error;
                    worst = $"Sample {i}: yaw={yaw}, pitch={pitch}, roll={roll}, "
                        + $"final yaw {previous.Value} -> {output}, angular step={delta:F6} deg";
                }
            }
            previous = output;
        }
        log.WriteLine($"28,801 final outputs, yaw direction={yawDirection}, pitch direction={pitchDirection}, "
            + $"20 yaw turns and 20 pitch loops, roll +/-45: max angular step={maximumAngularStep:F6} deg, "
            + $"max signed-step error={maximumError:F6} deg.");
        Assert.True(maximumError <= Tolerance,
            $"{worst}. Expected signed yaw step={yawDirection * StepDegrees} deg, "
            + $"maximum error={maximumError:F6} deg, allowed={Tolerance} deg. "
            + "Yaw-only profile; optional filters disabled; vertical-pitch singularities are included.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void IsolatedYawStaysSmoothWithinNonLoopingCoasterLimits(int direction)
    {
        dynamic pipeline = CoreSourceCompiler.Create();
        pipeline.Configure(LoadIsolatedProfile(), false, 30, 1f);
        float? previous = null;
        float maximumAngularStep = 0, maximumNumericStep = 0;
        int wrapCrossings = 0;
        for (int i = 0; i <= 28800; i++)
        {
            // Twenty continuous heading turns while tilt oscillates without looping.
            float yaw = direction * i * StepDegrees;
            float phase = i * MathF.PI / 1440f;
            float pitch = 70f * MathF.Sin(phase);
            float roll = 45f * MathF.Sin(phase * 0.5f);
            Assert.InRange(pitch, -70f, 70f);
            Assert.InRange(roll, -45f, 45f);
            float output = Output(pipeline, yaw, pitch, roll, i, assertOneToOne: true);
            Close(AngularDelta(output, yaw), 0, $"non-looping coaster sample {i}");
            if (previous.HasValue)
            {
                float delta = AngularDelta(output, previous.Value);
                Close(delta, direction * StepDegrees,
                    $"non-looping coaster sample {i}: yaw={yaw}, pitch={pitch}, roll={roll}, "
                    + $"previous final={previous.Value}, final={output}");
                float numericStep = Math.Abs(output - previous.Value);
                maximumAngularStep = Math.Max(maximumAngularStep, Math.Abs(delta));
                maximumNumericStep = Math.Max(maximumNumericStep, numericStep);
                if (numericStep > 180f)
                {
                    wrapCrossings++;
                    // A numeric wrap is valid only when it is the same small forward rotation.
                    Assert.InRange(Math.Abs(output), 179f, 180.01f);
                    Assert.InRange(Math.Abs(previous.Value), 179f, 180.01f);
                }
            }
            previous = output;
        }
        Assert.Equal(20, wrapCrossings);
        log.WriteLine($"28,801 final outputs, direction={direction}, pitch +/-70, roll +/-45: "
            + $"max angular step={maximumAngularStep:F6} deg, "
            + $"max numeric step={maximumNumericStep:F6} deg, normal heading wraps={wrapCrossings}.");
    }

    [Fact]
    public void IsolatedProfileEnablesOnlyUnfilteredOneToOneYaw()
    {
        var profile = LoadIsolatedProfile();
        var yaw = Assert.Single(profile, c => c.Enabled);
        Assert.Equal(1, yaw.Input_index);
        Assert.Equal(0, yaw.Output_index);
        Assert.False(yaw.Inverse);
        Assert.False(yaw.Constant);
        Assert.Equal(ProfileComponentType.VALUE, yaw.Type);
        Assert.Equal(1f, yaw.MultiplierPos);
        Assert.Equal(1f, yaw.MultiplierNeg);
        Assert.Equal(1f, yaw.Smoothing);
        Assert.Equal(-1f, yaw.Limit);
        Assert.Equal(0f, yaw.Offset);
        Assert.Equal(0f, yaw.Deadzone);
        Assert.False(yaw.Spikeflatter.Enabled);
        Assert.Empty(yaw.Math);
        Assert.Empty(yaw.Condition);
    }

    [Theory]
    [InlineData("yaw", 1)]
    [InlineData("yaw", -1)]
    [InlineData("pitch", 1)]
    [InlineData("pitch", -1)]
    [InlineData("roll", 1)]
    [InlineData("roll", -1)]
    [InlineData("combined", 1)]
    [InlineData("combined", -1)]
    public void IsolatedYawHasNoAngularJumpsDuringFullRotations(string axis, int direction) =>
        CheckIsolatedRotation(axis, direction, numeric: false);

    [Theory]
    [InlineData("yaw", 1)]
    [InlineData("yaw", -1)]
    [InlineData("pitch", 1)]
    [InlineData("pitch", -1)]
    [InlineData("roll", 1)]
    [InlineData("roll", -1)]
    [InlineData("combined", 1)]
    [InlineData("combined", -1)]
    public void IsolatedYawHasNoNumericJumpsDuringFullRotations(string axis, int direction) =>
        CheckIsolatedRotation(axis, direction, numeric: true);

    private void CheckIsolatedRotation(string axis, int direction, bool numeric)
    {
        dynamic pipeline = CoreSourceCompiler.Create();
        pipeline.Configure(LoadIsolatedProfile(), false, 30, 1f);
        float? previous = null;
        float maximumNumeric = 0, maximumAngular = 0;
        string worstNumeric = "", worstAngular = "";
        for (int i = 0; i <= 5760; i++)
        {
            float angle = direction * i * StepDegrees;
            float yaw = axis is "yaw" or "combined" ? angle : 37;
            float pitch = axis is "pitch" or "combined" ? angle : 0;
            float roll = axis is "roll" or "combined" ? angle : 0;
            float output = Output(pipeline, yaw, pitch, roll, i, assertOneToOne: true);
            Assert.True(float.IsFinite(output), $"Non-finite final yaw at {axis}={angle}.");
            if (previous.HasValue)
            {
                float numericStep = Math.Abs(output - previous.Value);
                float angularStep = Math.Abs(AngularDelta(output, previous.Value));
                string context = $"{axis} rotation={angle}: final yaw {previous.Value} -> {output}";
                if (numericStep > maximumNumeric)
                {
                    maximumNumeric = numericStep;
                    worstNumeric = context;
                }
                if (angularStep > maximumAngular)
                {
                    maximumAngular = angularStep;
                    worstAngular = context;
                }
            }
            previous = output;
        }
        log.WriteLine($"Unfiltered 1:1 yaw, {axis}, direction={direction}, 5761 samples: "
            + $"maximum numeric step={maximumNumeric:F6}, maximum angular step={maximumAngular:F6} deg.");
        // Four full turns, including every vertical-pitch and heading-wrap crossing.
        // One degree permits numerical error without accepting a spurious heading flip.
        float maximum = numeric ? maximumNumeric : maximumAngular;
        Assert.True(maximum <= 1f,
            $"{(numeric ? worstNumeric : worstAngular)}; maximum {(numeric ? "numeric" : "angular")} "
            + $"step={maximum:F6} deg, allowed=1 deg. Other channels and all optional yaw processing are disabled.");
    }

    [Fact]
    public void PacketLayoutAndProfileKeepYawAtInputOneAndOutputZero()
    {
        Assert.Equal(52, Marshal.SizeOf<RushOfBloodTelemetryData>());
        Assert.Equal(["Speed", "Yaw", "Pitch", "Roll", "GForceLateral", "GForceVertical",
            "GForceLongitudinal", "PosX", "PosY", "PosZ", "InPlay", "Braking", "Paused", "VR", "Frame"],
            InputHelper.GetValues<RushOfBloodTelemetryData>().Keys());
        var profile = LoadProfile();
        var yaw = Assert.Single(profile, c => c.Enabled && c.Output_index == 0);
        Assert.Equal(1, yaw.Input_index);
        Assert.True(yaw.Inverse);
        Assert.Equal(ProfileComponentType.VALUE, yaw.Type);
        Assert.Equal(1f, yaw.Smoothing);
        Assert.Equal(-1f, yaw.Limit);
        Assert.Equal(1f, yaw.MultiplierPos);
        Assert.Equal(1f, yaw.MultiplierNeg);
        Assert.Empty(yaw.Math);
        Assert.Empty(yaw.Condition);
        Assert.False(yaw.Spikeflatter.Enabled);
        Assert.Equal(1, Assert.Single(profile, c => c.Enabled && c.Input_index == 2).Output_index);
        Assert.Equal(2, Assert.Single(profile, c => c.Enabled && c.Input_index == 3).Output_index);
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
        float? previous = null;
        float maxRawStep = 0, maxAngularStep = 0;
        for (int i = 0; i <= 5760; i++)
        {
            float yaw = direction * (-720 + i * StepDegrees);
            float output = Output(pipeline, yaw, pitch, roll, i);
            Close(AngularDelta(output, -yaw), 0, $"yaw={yaw}, pitch={pitch}, roll={roll}");
            if (previous.HasValue)
            {
                float delta = AngularDelta(output, previous.Value);
                Close(delta, -direction * StepDegrees, $"final yaw step at input {yaw}");
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
    [InlineData(-179.9f)]
    public void SimultaneousSmoothYawPitchAndRollStaySmoothBelowVertical(float startHeading)
    {
        dynamic pipeline = NewPipeline();
        float? previous = null;
        for (int i = 0; i <= 14400; i++)
        {
            float t = i * MathF.PI / 1800;
            float yaw = startHeading + i * StepDegrees;
            float output = Output(pipeline, yaw, 80 * MathF.Sin(t), 170 * MathF.Sin(t * 0.7f), i);
            Close(AngularDelta(output, -yaw), 0, $"combined rotations at sample {i}");
            if (previous.HasValue)
                Close(AngularDelta(output, previous.Value), -StepDegrees, $"combined final step {i}");
            previous = output;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void FullRollRotationsDoNotChangeFinalYaw(int direction)
    {
        dynamic pipeline = NewPipeline();
        for (int i = 0; i <= 2880; i++)
        {
            float roll = direction * (-360 + i * StepDegrees);
            float output = Output(pipeline, 37, 20, roll, i);
            Close(AngularDelta(output, -37), 0, $"roll={roll}");
        }
    }

    [Fact]
    public void SeededSmoothOrientationWalkHasNoUnexpectedFinalYawSteps()
    {
        var random = new Random(25151);
        dynamic pipeline = NewPipeline();
        float yaw = 0, pitch = 0, roll = 0;
        float previous = Output(pipeline, yaw, pitch, roll, 0);
        for (int i = 1; i <= 20000; i++)
        {
            float dy = (float)(random.NextDouble() - 0.5);
            yaw += dy;
            pitch = Math.Clamp(pitch + (float)(random.NextDouble() - 0.5), -85, 85);
            roll += (float)(random.NextDouble() - 0.5);
            float output = Output(pipeline, yaw, pitch, roll, i);
            Close(AngularDelta(output, previous), -dy, $"random sample {i}");
            previous = output;
        }
    }

    [Fact]
    public void AllHeadingDegreesAtAGridOfTiltsProduceTheExpectedFinalHeading()
    {
        int samples = 0;
        foreach (int pitch in new[] { -89, -80, -60, -40, -20, 0, 20, 40, 60, 80, 89 })
        {
            for (int roll = -180; roll <= 180; roll += 30)
            {
                dynamic pipeline = NewPipeline();
                float? previous = null;
                for (int yaw = -360; yaw <= 360; yaw++)
                {
                    float output = Output(pipeline, yaw, pitch, roll, samples++);
                    Close(AngularDelta(output, -yaw), 0, $"heading grid: yaw={yaw}, pitch={pitch}, roll={roll}");
                    if (previous.HasValue)
                        Close(AngularDelta(output, previous.Value), -1, $"heading grid final step at {yaw}");
                    previous = output;
                }
            }
        }
        log.WriteLine($"Checked {samples} final headings across the heading/tilt grid.");
    }

    [Fact]
    public void NormalBoundaryWrapIsANumericJumpButNotAnAngularReversal()
    {
        dynamic pipeline = NewPipeline();
        float before = Output(pipeline, -179.75f, 10, 15, 0);
        float after = Output(pipeline, -180.25f, 10, 15, 1);
        Assert.True(Math.Abs(after - before) > 359, $"Expected wrapped output; got {before} -> {after}.");
        Close(AngularDelta(after, before), 0.5f, "final shortest-arc motion across numeric wrap");
        log.WriteLine($"Final yaw {before} -> {after}: numeric step={after - before}, angular step={AngularDelta(after, before)}.");
    }

    [Theory]
    [InlineData(125, 1)]
    [InlineData(-125, -1)]
    public void ResetYawOffsetKeepsSubsequentFinalHeadingSmooth(float start, int direction)
    {
        dynamic pipeline = NewPipeline();
        pipeline.RequestYawReset();
        float previous = Output(pipeline, start, 15, -10, 0);
        Close(AngularDelta(previous, 0), 0, "first output after reset");
        for (int i = 1; i <= 2880; i++)
        {
            float output = Output(pipeline, start + direction * i * StepDegrees, 15, -10, i);
            Close(AngularDelta(output, previous), -direction * StepDegrees, $"reset sample {i}");
            previous = output;
        }
    }

    // These contracts intentionally expose discontinuities in the current plugin/Core.
    // They must not be weakened to circular-only checks or marked skipped to make the suite green.
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AntijumpMustNotDiscardASmoothTurnAtTheHeadingBoundary(int direction)
    {
        dynamic pipeline = NewPipeline(antijump: true);
        float previous = Output(pipeline, 0, 0, 0, 0);
        float maximumError = 0;
        string worst = "";
        for (int i = 1; i <= 1600; i++)
        {
            float yaw = direction * i * StepDegrees;
            float output = Output(pipeline, yaw, 0, 0, i);
            float error = Math.Abs(AngularDelta(output, previous) + direction * StepDegrees);
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
        float previous = Output(pipeline, 0, 10, -15, 0);
        float maximumStep = 0;
        string worst = "";
        for (int i = 1; i <= 1600; i++)
        {
            float yaw = direction * i * StepDegrees;
            float output = Output(pipeline, yaw, 10, -15, i);
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
    public void FullPitchRotationMustNotCreateASuddenFinalYawReversal(int direction)
    {
        dynamic pipeline = NewPipeline();
        float previous = Output(pipeline, 37, 0, 15, 0);
        float maximumStep = 0;
        string worst = "";
        for (int i = 1; i <= 1440; i++)
        {
            float pitch = direction * i * StepDegrees;
            float output = Output(pipeline, 37, pitch, 15, i);
            float delta = AngularDelta(output, previous);
            if (!float.IsFinite(output) || Math.Abs(delta) > maximumStep)
            {
                maximumStep = float.IsFinite(output) ? Math.Abs(delta) : float.PositiveInfinity;
                worst = $"Smooth pitch={pitch}: final yaw {previous} -> {output}, angular step={delta} deg.";
            }
            previous = output;
        }
        Assert.True(maximumStep <= 1, worst + " Euler heading is singular when the cart points vertically.");
    }

    private static dynamic NewPipeline(bool antijump = false, float washout = 1)
    {
        dynamic manager = CoreSourceCompiler.Create();
        manager.Configure(LoadProfile(), antijump, 30, washout);
        return manager;
    }

    private static ObservableCollection<Profile_Component> LoadProfile()
    {
        using var stream = typeof(YawVR_Game_Engine.Plugin.RushOfBloodPlugin).Assembly
            .GetManifestResourceStream("RushOfBloodPlugin.Resources.Default.yawglprofile")
            ?? throw new InvalidOperationException("Missing embedded default profile.");
        using var reader = new StreamReader(stream);
        var json = Newtonsoft.Json.Linq.JObject.Parse(reader.ReadToEnd());
        return json["Components"]!.ToObject<ObservableCollection<Profile_Component>>()
            ?? throw new JsonSerializationException("Invalid default profile components.");
    }

    private static ObservableCollection<Profile_Component> LoadIsolatedProfile()
    {
        var profile = LoadProfile();
        var yaw = profile.Single(c => !c.Constant && c.Input_index == 1 && c.Output_index == 0);
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

    private static float Output(dynamic manager, float yaw, float pitch, float roll, int frame,
        bool assertOneToOne = false)
    {
        var q = Quaternion.CreateFromYawPitchRoll(yaw * MathF.PI / 180,
            pitch * MathF.PI / 180, roll * MathF.PI / 180);
        // q and -q are the same orientation; matrix-to-quaternion branch changes can flip signs.
        if (frame % 2 != 0) q = Quaternion.Negate(q);
        var packet = new byte[frame % 2 == 0 ? 52 : 56];
        float[] wire = [12, 10, 20, 30, q.X, q.Y, q.Z, q.W, 0, 1, 0];
        for (int i = 0; i < wire.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(i * 4), wire[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(44), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(48), (uint)frame);
        var data = new MarshalByteConverter<RushOfBloodTelemetryData>().FromBytes(packet);
        var limits = new DeviceLimitTests.Limits(25, 40, 20);
        var inputs = InputHelper.GetValues(data).Select(input =>
            YawVR_Game_Engine.Plugin.RushOfBloodPlugin.ScaleInput(input.key, input.value, limits)).ToArray();
        float output = manager.Step(inputs);
        if (assertOneToOne)
        {
            // Core always wraps yaw, even with every optional filter disabled.
            // Permit only the equivalent +/-180 endpoint, not a different heading.
            float difference = output - inputs[1];
            if (Math.Abs(difference) > 359.9f)
                Close(AngularDelta(output, inputs[1]), 0, $"Core endpoint normalization at frame {frame}");
            else
                Close(output, inputs[1], $"isolated final yaw must equal decoded input yaw at frame {frame}");
            for (int i = 1; i < 6; i++)
                Close((float)manager.GetCapturedOutput(i), 0, $"disabled output channel {i}");
        }
        return output;
    }

    private static float AngularDelta(float to, float from) =>
        (float)Math.IEEERemainder((double)to - from, 360);

    private static void Close(float actual, float expected, string context) =>
        Assert.True(float.IsFinite(actual) && Math.Abs(actual - expected) <= Tolerance,
            $"{context}: expected {expected:F6} deg, got {actual:F6} deg (tolerance {Tolerance}).");
}
