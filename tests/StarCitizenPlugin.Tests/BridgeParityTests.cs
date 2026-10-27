using Newtonsoft.Json.Linq;
using Xunit;
using YawVR_Game_Engine.Plugin;

namespace StarCitizenPlugin.Tests;

/// <summary>
/// Replays Data/events.ndjson through <see cref="ScBridge"/> and compares every 100 Hz tick with
/// Data/expected.ndjson, which SC-Motion-Haptic-Bridge's own lm_simhub4 code (v3 Path + Seat, v4 Haptics)
/// produced from the same events (see README.md).
/// </summary>
public sealed class BridgeParityTests
{
    private const double Tol = 1e-3;

    private static string DataFile(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

    internal static List<(double t, ScPacket p)> LoadEvents()
        => File.ReadLines(DataFile("events.ndjson"))
            .Select(l => (JObject.Parse(l).Value<double>("t"), ScPacket.Parse(l)!))
            .ToList();

    [Fact]
    public void EveryTickMatchesThePythonBridge()
    {
        var events = LoadEvents();
        var expected = File.ReadLines(DataFile("expected.ndjson")).Select(JObject.Parse).ToList();
        Assert.True(expected.Count > 900);

        var bridge = new ScBridge();
        var i = 0;
        foreach (var row in expected)
        {
            var now = row.Value<double>("now");
            while (i < events.Count && events[i].t <= now)
            {
                bridge.OnPacket(events[i].p, events[i].t);
                i++;
            }
            var tel = bridge.Tick(now);
            var at = $"tick t={now}";

            var m = row["motion"]!.Values<double>().ToArray();
            var got = new[] { tel.YawRate, tel.PitchRate, tel.RollRate, tel.Surge, tel.Sway, tel.Heave,
                              tel.VelocityForward, tel.VelocityLateral, tel.VelocityUp };
            for (var k = 0; k < m.Length; k++)
                Near(m[k], got[k], $"{at} motion[{k}]");

            Near(row.Value<double>("g"), tel.Seated, $"{at} g");
            Assert.True(row.Value<int>("active") == tel.Active, $"{at} active");
            Near(row.Value<double>("SpeedKmh"), tel.Speed, $"{at} speed");
            Near(row.Value<double>("EngineRpm"), tel.EngineRpm, $"{at} rpm");
            Near(row.Value<double>("Throttle"), tel.Throttle, $"{at} throttle");
            Assert.True(row.Value<int>("EngineStarted") == tel.EngineOn, $"{at} engine");
            Assert.True(row.Value<int>("FlightIsOnGround") == tel.OnGround, $"{at} ground");
            Near(row.Value<double>("Susp"), tel.Impact, $"{at} impact");
        }
    }

    [Fact]
    public void ScenarioExercisesEveryRule()
    {
        // guards against regenerating the data with a scenario that no longer covers the filters
        var rows = File.ReadLines(DataFile("expected.ndjson")).Select(JObject.Parse).ToList();
        Assert.Contains(rows, r => r.Value<int>("active") == 0 && r["motion"]!.Values<double>().Any(x => x != 0)); // stale ramp
        Assert.Contains(rows, r => r.Value<double>("g") is > 0 and < 1);                                            // seat fade
        Assert.Contains(rows, r => r.Value<double>("Susp") > 0.5);                                                  // impact pulse
        Assert.Contains(rows, r => r.Value<int>("FlightIsOnGround") == 0);
        Assert.Contains(rows, r => r.Value<int>("FlightIsOnGround") == 1);
        Assert.Contains(rows, r => r.Value<double>("Throttle") >= 1.0);                                             // boost
    }

    private static void Near(double expected, double actual, string what)
        => Assert.True(Math.Abs(expected - actual) <= Tol * Math.Max(1.0, Math.Abs(expected)),
            $"{what}: expected {expected}, got {actual}");
}
