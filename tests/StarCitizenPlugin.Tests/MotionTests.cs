using Xunit;
using YawVR_Game_Engine.Plugin;

namespace StarCitizenPlugin.Tests;

public sealed class MotionTests
{
    private static ScPacket Frame(double[] w, double[]? a = null, double[]? v = null, double gear = 0)
        => new() { Ev = 3, Data = new() { [35] = w, [40] = a ?? [0, 0, 0], [9] = v ?? [0, 50, 0], [92] = gear, [4] = 1.0 } };

    private static ScPacket Seated => new() { Ev = 10, Data = new() { [38] = 1.0 } };

    /// <summary>Seated and faded in, then one tick per 10 ms with a fresh frame and role each tick.</summary>
    private static (ScBridge bridge, double now) Fly(ScPacket frame, double seconds = 1.0)
    {
        var bridge = new ScBridge();
        var now = 1.0;
        for (var t = 0.0; t < seconds; t += 0.01)
        {
            now = 1.0 + t;
            bridge.OnPacket(Seated, now);
            bridge.OnPacket(frame, now);
            bridge.Tick(now);
        }
        return (bridge, now);
    }

    [Fact]
    public void MapsAxesLikeTheBridge()
    {
        var (bridge, now) = Fly(Frame(w: [0.1, 0.2, 0.3], a: [1, 2, 3], v: [4, 5, 6]));
        var t = bridge.Tick(now + 0.001);
        const double deg = 180 / Math.PI;
        Assert.Equal(1f, t.Seated);
        Assert.Equal(-0.3 * deg, t.YawRate, 3);
        Assert.Equal(-0.1 * deg, t.PitchRate, 3);
        Assert.Equal(0.2 * deg, t.RollRate, 3);
        Assert.Equal(2f, t.Surge, 3);
        Assert.Equal(1f, t.Sway, 3);
        Assert.Equal(3f, t.Heave, 3);
        Assert.Equal(5f, t.VelocityForward, 3);
        Assert.Equal(4f, t.VelocityLateral, 3);
        Assert.Equal(6f, t.VelocityUp, 3);
    }

    [Fact]
    public void GlitchFramesHoldTheLastGoodFrame()
    {
        var (bridge, now) = Fly(Frame(w: [0, 0, 0], a: [0, 5, 0]));
        bridge.OnPacket(Seated, now + 0.01);
        bridge.OnPacket(Frame(w: [0, 0, 0], a: [0, 400, 0]), now + 0.01);
        Assert.Equal(5f, bridge.Tick(now + 0.01).Surge, 3);
    }

    [Fact]
    public void LandedGateZeroesLinearMotion()
    {
        var (bridge, now) = Fly(Frame(w: [0, 0, 0], a: [1, 2, 9.8], v: [0, 0.1, 0], gear: 1));
        var t = bridge.Tick(now);
        Assert.Equal(0f, t.Surge);
        Assert.Equal(0f, t.Sway);
        Assert.Equal(0f, t.Heave);
    }

    [Fact]
    public void NoMotionWithoutASeat()
    {
        var bridge = new ScBridge();
        for (var t = 1.0; t < 2.0; t += 0.01)
        {
            bridge.OnPacket(Frame(w: [0.5, 0.5, 0.5], a: [5, 5, 5]), t);
            var tel = bridge.Tick(t);
            Assert.Equal(0f, tel.YawRate);
            Assert.Equal(0f, tel.Surge);
            Assert.Equal(0f, tel.Seated);
        }
    }

    [Fact]
    public void LeavingTheSeatFadesOutAndIgnoresNewFrames()
    {
        var (bridge, now) = Fly(Frame(w: [0, 0, 0], a: [0, 4, 0]));
        bridge.OnPacket(new ScPacket { Ev = 2 }, now + 0.01);
        var last = 4f;
        for (var t = now + 0.01; t < now + 0.6; t += 0.01)
        {
            bridge.OnPacket(Frame(w: [0, 0, 0], a: [0, 50, 0]), t);
            var surge = bridge.Tick(t).Surge;
            Assert.True(surge <= last + 1e-6, $"surge rose to {surge}");
            last = surge;
        }
        Assert.Equal(0f, last);
    }

    [Fact]
    public void StaleDataRampsToZeroAndClearsActive()
    {
        var (bridge, now) = Fly(Frame(w: [0, 0, 0], a: [0, 4, 0]));
        var t = now;
        ScTelemetryTick first = default;
        for (var k = 1; k <= 100; k++)
        {
            t = now + k * 0.01;
            bridge.OnPacket(Seated, t);
            var tel = bridge.Tick(t);
            if (k == 31) first = new(tel.Active, tel.Surge);
        }
        Assert.Equal(0f, first.Active);
        Assert.InRange(first.Surge, 0.01f, 3.99f);
        Assert.Equal(0f, bridge.Tick(t + 0.01).Surge);
    }

    [Fact]
    public void YawHeadingFollowsTheYawRate()
    {
        // -w.z = 0.5 rad/s = 28.65 deg/s for 2 s
        var (bridge, now) = Fly(Frame(w: [0, 0, -0.5]), seconds: 2.0);
        var heading = bridge.Tick(now + 0.01).Yaw;
        Assert.InRange(heading, 45f, 57.3f);   // less than 57.3 while the seat fades in
    }

    private readonly record struct ScTelemetryTick(float Active, float Surge);
}
