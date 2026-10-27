using Newtonsoft.Json.Linq;
using System.Reflection;
using Xunit;
using YawVR_Game_Engine.Plugin;

namespace StarCitizenPlugin.Tests;

public sealed class DecodeTests
{
    private static readonly string[] ExpectedInputs =
    [
        "Yaw", "YawRate", "PitchRate", "RollRate", "Surge", "Sway", "Heave",
        "VelocityForward", "VelocityLateral", "VelocityUp",
        "Speed", "EngineRpm", "EngineMaxRpm", "Throttle", "EngineOn", "Boost", "OnGround", "Impact",
        "Seated", "Active",
    ];

    [Fact]
    public void InputOrderMatchesDefaultProfileIndices()
    {
        var plugin = new YawVR_Game_Engine.Plugin.StarCitizenPlugin();
        Assert.Equal(ExpectedInputs, plugin.GetInputData());

        var profile = JObject.Parse(Resource("Default.yawglprofile"));
        var comps = profile["Components"]!.ToList();
        Assert.All(comps, c => Assert.InRange(c.Value<int>("Input_index"), 0, ExpectedInputs.Length - 1));

        string[] To(int output) => comps.Where(c => c.Value<bool>("Enabled") && c.Value<int>("Output_index") == output)
            .Select(c => ExpectedInputs[c.Value<int>("Input_index")]).Order().ToArray();
        Assert.Equal(["Yaw"], To(0));
        Assert.Equal(["PitchRate", "Surge"], To(1));
        Assert.Equal(["RollRate", "Sway"], To(2));
        Assert.Equal(["Impact"], To(3));
    }

    [Fact]
    public void AllResourcesAreEmbeddedUnderTheAssemblyName()
    {
        var names = typeof(YawVR_Game_Engine.Plugin.StarCitizenPlugin).Assembly.GetManifestResourceNames();
        foreach (var file in new[] { "description.html", "Default.yawglprofile", "logo.png", "recent.png", "wide.png" })
            Assert.Contains($"StarCitizenPlugin.Resources.{file}", names);
        var export = typeof(YawVR_Game_Engine.Plugin.StarCitizenPlugin)
            .GetCustomAttributes<System.ComponentModel.Composition.ExportMetadataAttribute>()
            .Single(a => a.Name == "Name");
        Assert.Equal("Star Citizen", export.Value);
    }

    [Fact]
    public void DefaultConfigListensOnPort61703()
    {
        var config = new Config();
        Assert.Equal("127.0.0.1", config.IP);
        Assert.Equal(61703, config.Port);
    }

    [Fact]
    public void ParsesTheCaptureServerFormat()
    {
        var p = ScPacket.Parse("""{"t": 1.5, "ev": 3, "name": "VehicleFrame", "m": [4, 9, 92, 7, 8], "v": [1.0, [1, 2, 3], true, null, [1, 2, 3, 4]]}""");
        Assert.NotNull(p);
        Assert.Equal(3, p!.Ev);
        Assert.Equal(1.0, ScPacket.Num(p.Data, 4));
        Assert.Equal(new double[] { 1, 2, 3 }, ScPacket.Vec(p.Data, 9));
        Assert.Equal(1.0, ScPacket.Num(p.Data, 92));
        Assert.Null(ScPacket.Num(p.Data, 7));
        Assert.Null(ScPacket.Vec(p.Data, 8));        // quad, not XYZ
        Assert.Equal(4, ((double[])p.Data[8]).Length);
    }

    [Theory]
    [InlineData("""{"t": 1, "ev": "mark", "label": "takeoff"}""")]
    [InlineData("""{"t": 1}""")]
    [InlineData("not json")]
    public void IgnoresMarksAndGarbage(string line) => Assert.Null(ScPacket.Parse(line));

    [Fact]
    public void MismatchedFieldListsGiveNoFields()
    {
        var p = ScPacket.Parse("""{"ev": 3, "m": [4, 9], "v": [1.0]}""");
        Assert.NotNull(p);
        Assert.Empty(p!.Data);
    }

    private static string Resource(string file)
    {
        using var s = typeof(YawVR_Game_Engine.Plugin.StarCitizenPlugin).Assembly
            .GetManifestResourceStream($"StarCitizenPlugin.Resources.{file}")!;
        return new StreamReader(s).ReadToEnd();
    }
}
