using System.Text.Json;
using FluentAssertions;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Tests.Services;

public class MpolExportServiceTests
{
    [Fact]
    public void SerializeToString_ProducesValidJson()
    {
        var map = new MpolMap { Name = "TestStore" };
        map.Units.Add(new MpolUnit
        {
            UnitNumber = "101",
            Width = 10,
            Height = 5,
            Area = 50
        });

        var json = MpolExportService.SerializeToString(map);

        json.Should().NotBeNullOrEmpty();
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("name").GetString().Should().Be("TestStore");
        parsed.RootElement.GetProperty("units").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void SerializeToString_EmptyMap_ProducesValidJson()
    {
        var map = new MpolMap { Name = "" };
        var json = MpolExportService.SerializeToString(map);

        json.Should().NotBeNullOrEmpty();
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("units").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void MpolUnit_DefaultValues_AreCorrect()
    {
        var unit = new MpolUnit();
        unit.UnitNumber.Should().Be("");
        unit.Polygons.Should().BeEmpty();
        unit.Path.Should().BeEmpty();
        unit.Distance.Should().Be(0);
    }

    [Fact]
    public void SerializeToString_IncludesBackgroundContours()
    {
        var map = new MpolMap { Name = "Test" };
        map.Background.Add(new List<double[]>
        {
            new[] { 0.0, 0.0 },
            new[] { 10.0, 0.0 },
            new[] { 10.0, 10.0 }
        });

        var json = MpolExportService.SerializeToString(map);
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("background").GetArrayLength().Should().Be(1);
    }
}
