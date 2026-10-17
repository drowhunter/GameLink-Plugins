using Newtonsoft.Json;
using SharedLib.TelemetryHelper.Convertors;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace SharedLib.Tests.TelemetryHelper.Convertors;

public class SparseJsonConverterTests
{
    private sealed class TelemetryState
    {
        public int Speed { get; set; }
        public int Gear { get; set; }
        public string? Track { get; set; }
    }
    // [JsonProperty("XWA.player.shipname")]
    private sealed class TelemetryState2
    {
        [JsonProperty("XWA.player.speed")]
        public int Speed { get; set; }
        [JsonProperty("XWA.player.gear")]
        public int Gear { get; set; }
        [JsonProperty("XWA.player.track")]
        public string? Track { get; set; }
    }

    [Fact]
    public void FromBytes_Dictionary_MergesSparsePayloads_AndRetainsPreviousValues()
    {
        var converter = new SparseJsonConverter<Dictionary<string, object>>();

        var firstPayload = Encoding.UTF8.GetBytes("{\"speed\":100,\"gear\":3}");
        var secondPayload = Encoding.UTF8.GetBytes("{\"speed\":120}");
        var thirdPayload = Encoding.UTF8.GetBytes("{\"gear\":4}");

        var firstResult = converter.FromBytes(firstPayload);
        var secondResult = converter.FromBytes(secondPayload);
        var thirdResult = converter.FromBytes(thirdPayload);

        Assert.Same(firstResult, secondResult);
        Assert.Same(firstResult, thirdResult);

        Assert.Equal(120L, Convert.ToInt64(thirdResult["speed"]));
        Assert.Equal(4L, Convert.ToInt64(thirdResult["gear"]));
    }

    [Fact]
    public void FromBytes_NonDictionary_PopulatesExistingData()
    {
        var converter = new SparseJsonConverter<TelemetryState>();
        converter.Data = new TelemetryState
        {
            Speed = 100,
            Gear = 3,
            Track = "Imola"
        };

        var payload = Encoding.UTF8.GetBytes("{\"speed\":130}");
        var result = converter.FromBytes(payload);

        Assert.Same(converter.Data, result);
        Assert.Equal(130, result.Speed);
        Assert.Equal(3, result.Gear);
        Assert.Equal("Imola", result.Track);
    }

    [Fact]
    public void FromBytes_NonDictionary_PopulatesExistingData_withJsonPropertyAttributes()
    {
        var converter = new SparseJsonConverter<TelemetryState2>();
        converter.Data = new TelemetryState2
        {
            Speed = 100,
            Gear = 3,
            Track = "Imola"
        };

        var payload = Encoding.UTF8.GetBytes("{\"XWA.player.speed\":130}");
        var result = converter.FromBytes(payload);

        Assert.Same(converter.Data, result);
        Assert.Equal(130, result.Speed);
        Assert.Equal(3, result.Gear);
        Assert.Equal("Imola", result.Track);
    }

    [Fact]
    public void FromBytes_NonDictionary_MergesMultipleSparsePayloads_IntoExistingCopy()
    {
        var converter = new SparseJsonConverter<TelemetryState>();
        var existing = new TelemetryState
        {
            Speed = 100,
            Gear = 3,
            Track = "Imola"
        };
        converter.Data = existing;

        var firstPayload = Encoding.UTF8.GetBytes("{\"speed\":130}");
        var secondPayload = Encoding.UTF8.GetBytes("{\"track\":\"Spa\"}");

        var firstResult = converter.FromBytes(firstPayload);
        var secondResult = converter.FromBytes(secondPayload);

        Assert.Same(existing, firstResult);
        Assert.Same(existing, secondResult);
        Assert.Equal(130, secondResult.Speed);
        Assert.Equal(3, secondResult.Gear);
        Assert.Equal("Spa", secondResult.Track);
    }

    [Fact]
    public void FromBytes_NonDictionary_WhenDataIsNull_DeserializesWholeObject()
    {
        var converter = new SparseJsonConverter<TelemetryState>
        {
            Data = null!
        };

        var payload = Encoding.UTF8.GetBytes("{\"speed\":90,\"gear\":2,\"track\":\"Spa\"}");
        var result = converter.FromBytes(payload);

        Assert.NotNull(result);
        Assert.Equal(90, result.Speed);
        Assert.Equal(2, result.Gear);
        Assert.Equal("Spa", result.Track);
    }

    [Fact]
    public void FromBytes_WhenRootIsNotObject_ReturnsExistingDataUnchanged()
    {
        var converter = new SparseJsonConverter<TelemetryState>();
        var existing = new TelemetryState
        {
            Speed = 75,
            Gear = 5,
            Track = "Monza"
        };
        converter.Data = existing;

        var payload = Encoding.UTF8.GetBytes("[1,2,3]");
        var result = converter.FromBytes(payload);

        Assert.Same(existing, result);
        Assert.Equal(75, result.Speed);
        Assert.Equal(5, result.Gear);
        Assert.Equal("Monza", result.Track);
    }
}
