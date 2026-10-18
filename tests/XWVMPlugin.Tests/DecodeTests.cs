using SharedLib;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using YawVR_Game_Engine.Plugin;
using static XWVMPlugin.Tests.Packets;

namespace XWVMPlugin.Tests;

public sealed class DecodeTests(ITestOutputHelper log)
{
    private static readonly string[] ExpectedInputs =
    [
        "Speed", "Throttle", "MaxSpeed", "BeamEnergy", "SFoils", "ShieldFront", "ShieldBack", "Hull", "Shake",
        "HitX", "HitY", "HitZ", "LaserFired", "WarheadFired",
        "YawInertia", "PitchInertia", "RollInertia", "AccelInertia", "LateralAccel", "VerticalAccel",
        "HyperspacePhase", "Ejected", "Yaw", "Pitch", "Roll", "Paused",
    ];

    [Fact]
    public void InputOrderMatchesDefaultProfileIndices()
    {
        var plugin = new YawVR_Game_Engine.Plugin.XWVMPlugin();
        Assert.Equal(ExpectedInputs, plugin.GetInputData());
        Assert.Equal(InputCount, ExpectedInputs.Length);
        Assert.Equal(Yaw, Array.IndexOf(ExpectedInputs, "Yaw"));
        Assert.Equal(Pitch, Array.IndexOf(ExpectedInputs, "Pitch"));
        Assert.Equal(Roll, Array.IndexOf(ExpectedInputs, "Roll"));

        var profile = LoadProfile();
        Assert.All(profile, c => Assert.InRange(c.Input_index, 0, InputCount - 1));
        Assert.Equal(Yaw, Assert.Single(profile, c => c.Enabled && c.Output_index == 0).Input_index);
        Assert.Equal(Pitch, Assert.Single(profile, c => c.Enabled && c.Output_index == 1).Input_index);
        Assert.Equal(Roll, Assert.Single(profile, c => c.Enabled && c.Output_index == 2).Input_index);
        Assert.Equal(new[] { 8, 12, 13 },
            profile.Where(c => c.Enabled && c.Output_index == 3).Select(c => c.Input_index).Order());
        Assert.True(Assert.Single(profile, c => c.Enabled && c.Output_index == 4).Constant);
    }

    [Fact]
    public void AllResourcesAreEmbeddedUnderTheAssemblyName()
    {
        var names = typeof(YawVR_Game_Engine.Plugin.XWVMPlugin).Assembly.GetManifestResourceNames();
        foreach (var file in new[] { "description.html", "Default.yawglprofile", "logo.png", "recent.png", "wide.png" })
            Assert.Contains($"XWVMPlugin.Resources.{file}", names);
        var export = typeof(YawVR_Game_Engine.Plugin.XWVMPlugin)
            .GetCustomAttributes<System.ComponentModel.Composition.ExportMetadataAttribute>()
            .Single(a => a.Name == "Name");
        Assert.Equal("X-Wing Virtual Machine", export.Value);
    }

    [Fact]
    public void DefaultConfigListensOnPort10555()
    {
        var config = new Config();
        Assert.Equal(10555, config.Port);
    }

    [Fact]
    public void FirstCapturedJsonPacketDecodesEveryMappedField()
    {
        var line = File.ReadLines(DataFile("json-capture.jsonl")).First();
        var t = Decode(new XwvmConverter(), System.Text.Encoding.UTF8.GetBytes(line));
        // rot "3.59 0 349.75": Unity eulers, x = pitch (nose-down +), z = roll (left +).
        Close(t.Yaw, 0);
        Close(t.Pitch, 3.59f);
        Close(t.Roll, -10.25f);
        Close(t.MaxSpeed, 100);
        Close(t.ShieldFront, 200);
        Close(t.Hull, 100);
        Assert.Equal(0, t.Paused);
        Assert.Equal(0, t.Ejected);
        Assert.Equal(0, t.Shake);
    }

    [Fact]
    public void CapturedJsonStreamDecodesWithFiniteInputsAndExpectedLaserFrames()
    {
        var converter = new XwvmConverter();
        var fired = new List<int>();
        int index = 0;
        foreach (var line in File.ReadLines(DataFile("json-capture.jsonl")))
        {
            var t = Decode(converter, System.Text.Encoding.UTF8.GetBytes(line));
            var values = InputHelper.GetValues(t).ToArray();
            Assert.Equal(InputCount, values.Length);
            Assert.All(values, v => Assert.True(float.IsFinite(v.value), $"line {index}: {v.key}={v.value}"));
            Assert.InRange(t.Yaw, 0, 360);
            Assert.InRange(t.Pitch, -180, 180);
            Assert.InRange(t.Roll, -180, 180);
            Assert.InRange(t.Throttle, 0, 100);
            if (t.LaserFired > 0) fired.Add(index);
            Assert.Equal(0, t.WarheadFired);
            index++;
        }
        Assert.Equal(161, index);
        // Only frames within 0.2 s after a cannon fire time of the first canN cannons.
        Assert.Equal(new[] { 76, 77, 82, 86, 87 }, fired);
    }

    [Fact]
    public void CapturedAngularRatesMatchCapturedOrientationChanges()
    {
        var converter = new XwvmConverter();
        Telemetry? previous = null;
        float previousTime = 0;
        int pitch = 0, yaw = 0, roll = 0;
        foreach (var line in File.ReadLines(DataFile("json-capture.jsonl")))
        {
            var t = Decode(converter, System.Text.Encoding.UTF8.GetBytes(line));
            float time = Convert.ToSingle(converter.Data["t"]);
            if (previous is { } p && time > previousTime)
            {
                float dt = time - previousTime;
                // Current conventions: Pitch nose-down +, PitchInertia nose-up +; Roll left +, RollInertia right +.
                pitch += Agree(Delta(t.Pitch, p.Pitch) / dt, -(t.PitchInertia + p.PitchInertia) / 2);
                yaw += Agree(Delta(t.Yaw, p.Yaw) / dt, (t.YawInertia + p.YawInertia) / 2);
                roll += Agree(Delta(t.Roll, p.Roll) / dt, -(t.RollInertia + p.RollInertia) / 2);
            }
            previous = t;
            previousTime = time;
        }
        log.WriteLine($"Rate/orientation sign agreements: pitch={pitch}, yaw={yaw}, roll={roll}");
        Assert.True(pitch >= 5 && roll >= 5, $"Too few comparable frames: pitch={pitch}, yaw={yaw}, roll={roll}");

        static int Agree(float orientationRate, float reportedRate)
        {
            if (Math.Abs(orientationRate) < 5 || Math.Abs(reportedRate) < 5) return 0;
            Assert.True(orientationRate > 0 == reportedRate > 0,
                $"orientation rate {orientationRate} disagrees with reported rate {reportedRate}");
            return 1;
        }
    }

    [Fact]
    public void SparsePacketsMergeWithEarlierValues()
    {
        var converter = new XwvmConverter();
        Decode(converter, Json(new { p = 1, t = 5.0, rot = "10 20 30", thr = 75, hull = 80, fShd = 150, maxSpd = 120 }));
        var t = Decode(converter, Json(new { p = 2, t = 5.02, rot = "11 21 31" }));
        Close(t.Pitch, 11);
        Close(t.Yaw, 21);
        Close(t.Roll, 31);
        Close(t.Throttle, 75);
        Close(t.Hull, 80);
        Close(t.ShieldFront, 150);
        Close(t.MaxSpeed, 120);

        t = Decode(converter, Json(new { hull = 40 }));
        Close(t.Hull, 40);
        Close(t.Yaw, 21);

        converter.Reset();
        t = Decode(converter, Json(new { p = 1 }));
        Close(t.Hull, 0);
        Close(t.Yaw, 0);
    }

    [Fact]
    public void EmptyAndNonObjectPacketsKeepCurrentData()
    {
        var converter = new XwvmConverter();
        Decode(converter, Json(new { rot = "1 2 3" }));
        Close(Decode(converter, "[]"u8.ToArray()).Yaw, 2);
        Close(Decode(converter, "{}"u8.ToArray()).Yaw, 2);
    }

    [Theory]
    [InlineData("1e-05 -2.5E+02 3", 1e-05f, -250f, 3f)]
    [InlineData("  4   5.5\t-6 ", 4f, 5.5f, -6f)]
    [InlineData("7", 7f, 0f, 0f)]
    [InlineData("x y z", 0f, 0f, 0f)]
    public void VectorStringsParseInvariantAndExponentForms(string vector, float x, float y, float z)
    {
        var t = Decode(new XwvmConverter(), Json(new { acc = vector, aVel = vector, vel = vector, hitD = vector }));
        Close(t.LateralAccel, x);
        Close(t.VerticalAccel, -y);
        Close(t.AccelInertia, z);
        Close(t.PitchInertia, -x);
        Close(t.YawInertia, y);
        Close(t.RollInertia, z);
        Close(t.Speed, z);
        Close(t.HitX, x);
        Close(t.HitY, y);
        Close(t.HitZ, z);
    }

    [Fact]
    public void VectorArraysAreAccepted()
    {
        var t = Decode(new XwvmConverter(), Json(new { rot = new[] { 350.0, 90.0, 190.0 }, vel = new[] { 0, 0, 42 } }));
        Close(t.Pitch, -10);
        Close(t.Yaw, 90);
        Close(t.Roll, -170);
        Close(t.Speed, 42);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(180.5f, -179.5f)]
    [InlineData(270, -90)]
    [InlineData(359.75f, -0.25f)]
    [InlineData(360, 0)]
    [InlineData(-90, -90)]
    [InlineData(720 + 45, 45)]
    public void PitchAndRollWrapToSigned180(float unity, float expected)
    {
        var t = Decode(new XwvmConverter(), Json(new { rot = Vec(unity, unity, unity) }));
        Close(t.Pitch, expected);
        Close(t.Roll, expected);
        Close(t.Yaw, unity);
    }

    [Theory]
    [InlineData(10.0, 10.0, 1)]
    [InlineData(10.1, 10.0, 1)]
    [InlineData(10.18, 10.0, 1)]
    [InlineData(10.21, 10.0, 0)]
    [InlineData(9.9, 10.0, 0)]
    [InlineData(10.0, 0.0, 0)]
    [InlineData(10.0, -1.0, 0)]
    public void LaserAndWarheadFireWindowIsPointTwoSeconds(double now, double fired, int expected)
    {
        var t = Decode(new XwvmConverter(), Json(new
        {
            t = now,
            canN = 2, canFT = new[] { -1, fired, 9.95 + 100 },
            lauN = 1, lauFT = new[] { fired, 9.99 + 100 },
        }));
        Assert.Equal(expected, t.LaserFired);
        Assert.Equal(expected, t.WarheadFired);
    }

    [Fact]
    public void FireTimesBeyondTheReportedCountAreIgnored()
    {
        var t = Decode(new XwvmConverter(), Json(new { t = 10.0, canN = 1, canFT = new[] { -1, 10.0 }, lauN = 0, lauFT = new[] { 10.0 } }));
        Assert.Equal(0, t.LaserFired);
        Assert.Equal(0, t.WarheadFired);
    }

    [Theory]
    [InlineData(10.0, 10.0, 1f)]
    [InlineData(10.25, 10.0, 0.5f)]
    [InlineData(10.49, 10.0, 0.02f)]
    [InlineData(10.5, 10.0, 0f)]
    [InlineData(9.9, 10.0, 0f)]
    [InlineData(0.0, 0.0, 0f)]
    [InlineData(0.1, -1.0, 0f)]
    public void ShakeDecaysLinearlyOverHalfASecondAfterDamage(double now, double damage, float expected)
    {
        var t = Decode(new XwvmConverter(), Json(new { t = now, dmgT = damage }));
        Close(t.Shake, expected);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(-0.01, 0)]
    [InlineData(0, 1)]
    [InlineData(12.5, 1)]
    public void EjectedWhenEjectionTimeIsNotNegative(double ejection, int expected) =>
        Assert.Equal(expected, Decode(new XwvmConverter(), Json(new { ejcT = ejection })).Ejected);

    [Fact]
    public void MissingFieldsDecodeToNeutralValues()
    {
        var t = Decode(new XwvmConverter(), Json(new { p = 1 }));
        Assert.All(InputHelper.GetValues(t), v => Assert.Equal(0, v.value));
    }

    [Fact]
    public void SyntheticSimHubPacketMapsEveryField()
    {
        var packet = new SimHub()
            .U64(SimHub.Counter, 123456789).U8(SimHub.Running, 1).U8(SimHub.Paused, 0).F64(SimHub.Time, 42.5)
            .F32(SimHub.YawRate, 11).F32(SimHub.PitchRate, 12).F32(SimHub.RollRate, 13)
            .F32(SimHub.Surge, 21).F32(SimHub.Sway, 22).F32(SimHub.Heave, 23)
            .F32(SimHub.VelFwd, 88).F32(SimHub.VelUp, 3)
            .F64(SimHub.North, 1000).F64(SimHub.East, 2000).F64(SimHub.Up, 3000)
            .F32(SimHub.Throttle, 0.75f).F64(SimHub.TimeLeft, 600).F32(SimHub.Ejection, -1).F32(SimHub.MaxSpeed, 115)
            .Cannons(-1, 42.4f, 30).Launchers(42.45f)
            .F32(SimHub.Beam, 0.6f).U8(SimHub.SFoil, 1).U8(SimHub.Hyper, 2)
            .F32(SimHub.DamageTime, 42.25f).F32(SimHub.HitX, 0.1f).F32(SimHub.HitY, 0.2f).F32(SimHub.HitZ, 0.3f)
            .F32(SimHub.Front, 180).F32(SimHub.Rear, 170).F32(SimHub.Hull, 65);

        var converter = new XwvmConverter();
        var t = Decode(converter, packet.Bytes);
        var d = converter.Data;

        Assert.Equal(123456789L, d["p"]);
        Assert.Equal(42.5, d["t"]);
        Assert.Equal(600.0, d["tL"]);
        Assert.Equal("2000 3000 1000", d["pos"]);

        Close(t.Speed, 88);
        Close(t.Throttle, 75);
        Close(t.MaxSpeed, 115);
        Close(t.BeamEnergy, 0.6f);
        Close(t.SFoils, 1);
        Close(t.HyperspacePhase, 2);
        Close(t.ShieldFront, 180);
        Close(t.ShieldBack, 170);
        Close(t.Hull, 65);
        Close(t.Shake, 0.5f);
        Close(t.HitX, 0.1f);
        Close(t.HitY, 0.2f);
        Close(t.HitZ, 0.3f);
        Close(t.LaserFired, 1);
        Close(t.WarheadFired, 1);
        Close(t.PitchInertia, -12);
        Close(t.YawInertia, 11);
        Close(t.RollInertia, 13);
        Close(t.LateralAccel, 22);
        Close(t.VerticalAccel, -23);
        Close(t.AccelInertia, 21);
        Close(t.Ejected, 0);
        Close(t.Paused, 0);
        // SimHub carries rates only; no orientation.
        Close(t.Yaw, 0);
        Close(t.Pitch, 0);
        Close(t.Roll, 0);
    }

    [Fact]
    public void SimHubFireTimesAreTruncatedToTheReportedCount()
    {
        var packet = new SimHub().F64(SimHub.Time, 10).Cannons(1, 2).Launchers();
        BinaryWrite(packet, SimHub.CannonFire + 2 * 4, 10f);
        BinaryWrite(packet, SimHub.LauncherFire, 10f);
        var converter = new XwvmConverter();
        var t = Decode(converter, packet.Bytes);
        Assert.Equal(2, ((List<object>)converter.Data["canFT"]).Count);
        Assert.Empty((List<object>)converter.Data["lauFT"]);
        Assert.Equal(0, t.LaserFired);
        Assert.Equal(0, t.WarheadFired);

        static void BinaryWrite(SimHub p, int offset, float value) => p.F32(offset, value);
    }

    [Fact]
    public void SimHubPausedAndEjectionFlags()
    {
        var converter = new XwvmConverter();
        var t = Decode(converter, new SimHub().U8(SimHub.Paused, 1).F32(SimHub.Ejection, 3).Bytes);
        Assert.Equal(1, t.Paused);
        Assert.Equal(1, t.Ejected);

        // Switching the game to JSON must not leave a stale SimHub pause flag behind.
        t = Decode(converter, Json(new { p = 1, rot = "0 45 0" }));
        Assert.Equal(0, t.Paused);
        Close(t.Yaw, 45);
        Assert.Equal(1, t.Ejected);
    }

    [Fact]
    public void SimHubAfterJsonKeepsLastJsonOrientation()
    {
        var converter = new XwvmConverter();
        Decode(converter, Json(new { rot = "5 90 10" }));
        var t = Decode(converter, new SimHub().F32(SimHub.YawRate, 7).Bytes);
        Close(t.Yaw, 90);
        Close(t.YawInertia, 7);
    }

    [Fact]
    public void ShortBinaryPacketsAreNotTreatedAsSimHub()
    {
        var converter = new XwvmConverter();
        var bytes = new SimHub().U8(SimHub.Paused, 1).Bytes[..(SimHub.Size - 1)];
        Assert.ThrowsAny<Newtonsoft.Json.JsonException>(() => converter.FromBytes(bytes));
        Assert.False(converter.Data.ContainsKey("paused"));
    }

    [Fact]
    public void CapturedSimHubPacketsDecodeConsistently()
    {
        var converter = new XwvmConverter();
        long previousCounter = long.MinValue;
        double previousTime = double.MinValue;
        int count = 0;
        foreach (var line in File.ReadLines(DataFile("simhub-capture.b64")))
        {
            var bytes = Convert.FromBase64String(line);
            Assert.Equal(XwvmConverter.SimHubPacketSize, bytes.Length);
            var t = Decode(converter, bytes);
            var counter = (long)converter.Data["p"];
            var time = (double)converter.Data["t"];
            Assert.True(counter > previousCounter, $"packet {count}: counter {counter} <= {previousCounter}");
            Assert.True(time >= previousTime, $"packet {count}: time {time} < {previousTime}");
            Assert.All(InputHelper.GetValues(t), v => Assert.True(float.IsFinite(v.value), $"{v.key}={v.value}"));
            Assert.InRange(t.Throttle, 0, 100);
            Assert.InRange(t.Hull, 0, 100);
            Assert.InRange((long)converter.Data["canN"], 0, 8);
            Assert.InRange((long)converter.Data["lauN"], 0, 8);
            Assert.True(t.MaxSpeed > 0);
            Assert.Equal(0, t.Paused);
            previousCounter = counter;
            previousTime = time;
            count++;
        }
        Assert.Equal(40, count);
    }

    private static float Delta(float to, float from) => (float)Math.IEEERemainder((double)to - from, 360);

    private static void Close(float actual, float expected) =>
        Assert.True(float.IsFinite(actual) && Math.Abs(actual - expected) <= 0.001f, $"Expected {expected}, got {actual}.");
}
