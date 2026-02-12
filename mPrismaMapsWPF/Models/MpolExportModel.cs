using System.Text.Json.Serialization;

namespace mPrismaMapsWPF.Models;

public class MpolMap
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("units")]
    public List<MpolUnit> Units { get; set; } = new();

    [JsonPropertyName("background")]
    public List<List<double[]>> Background { get; set; } = new();
}

public class MpolUnit
{
    [JsonPropertyName("unitnumber")]
    public string UnitNumber { get; set; } = "";

    [JsonPropertyName("polygons")]
    public List<List<double[]>> Polygons { get; set; } = new();

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    [JsonPropertyName("isVertical")]
    public bool IsVertical { get; set; }

    [JsonPropertyName("area")]
    public double Area { get; set; }

    [JsonPropertyName("path")]
    public List<double[]> Path { get; set; } = new();

    [JsonPropertyName("distance")]
    public double Distance { get; set; }
}
