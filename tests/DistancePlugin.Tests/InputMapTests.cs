using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using PluginData = YawVR_Game_Engine.Plugin.DistanceTelemetryData;

namespace DistancePlugin.Tests;

/// <summary>The mod packet, the plugin struct and the default profile indices must agree.</summary>
public sealed class InputMapTests(ITestOutputHelper output)
{
    [Fact]
    public void PluginStructHasTheSameSizeAsTheModPacket() =>
        Assert.Equal(Marshal.SizeOf<ModPacket>(), Marshal.SizeOf<PluginData>());

    [Fact]
    public void DefaultProfileInputIndicesPointAtTheIntendedTelemetry()
    {
        for (int i = 0; i < Pipeline.InputNames.Length; i++)
            output.WriteLine($"{i,2}: {Pipeline.InputNames[i]}");

        string[] expected =
        [
            "GamePaused", "IsRacing", "KPH", "Pitch", "Yaw", "Roll",
            "AngularVelocity.X", "AngularVelocity.Y", "AngularVelocity.Z", "sway",
            "Velocity.X", "Velocity.Y", "Velocity.Z", "Accel.X", "Accel.Y", "Accel.Z",
            "Boost", "Grip", "WingsOpen", "IsCarEnabled", "IsCarIsActive", "IsCarDestroyed",
            "AllWheelsOnGround", "IsGrav", "TireFL", "TireFR", "TireBL", "TireBR",
            "Rot.x", "Rot.y", "Rot.z", "Rot.w",
        ];
        Assert.Equal(expected, Pipeline.InputNames);

        var profile = Pipeline.LoadProfile();
        Assert.Contains(profile, c => c.Enabled && c.Input_index == Pipeline.Index("Yaw") && c.Output_index == 0);
        Assert.Contains(profile, c => c.Enabled && c.Input_index == Pipeline.Index("Pitch") && c.Output_index == 1);
        Assert.Contains(profile, c => c.Enabled && c.Input_index == Pipeline.Index("Roll") && c.Output_index == 2);
        Assert.All(profile.SelectMany(c => c.Condition), c => Assert.Equal("IsCarEnabled", Pipeline.InputNames[c.OtherIndex]));
    }

    [Fact]
    public void ModFieldsArriveInTheMatchingPluginInputs()
    {
        var packet = new ModPacket
        {
            KPH = 123, Pitch = -12.5f, Yaw = 170.25f, Roll = 33, AngularVelocityY = 1.5f, cForce = -7,
            Boost = true, IsCarEnabled = true, IsCarIsActive = true,
            TireFL = 0.1f, TireFR = 0.2f, TireBL = 0.3f, TireBR = 0.4f,
        };
        var data = Pipeline.Decode(packet);
        Assert.Equal(123, data.KPH);
        Assert.Equal(-12.5f, data.Pitch);
        Assert.Equal(170.25f, data.Yaw);
        Assert.Equal(33, data.Roll);
        Assert.Equal(1.5f, data.AngularVelocity.Y);
        Assert.Equal(-7, data.sway);
        Assert.True(data.Boost && data.IsCarEnabled && data.IsCarIsActive && !data.IsCarDestroyed);
        Assert.Equal(0.4f, data.TireBR);

        var inputs = Pipeline.Inputs(packet);
        Assert.Equal(170.25f, inputs[Pipeline.Index("Yaw")]);
        Assert.Equal(1, inputs[Pipeline.Index("Boost")]);
        Assert.Equal(1, inputs[Pipeline.Index("IsCarEnabled")]);
        Assert.Equal(0.1f, inputs[Pipeline.Index("TireFL")]);
    }
}
