namespace PhotoSort.Models;

public sealed class HeatmapPoint
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double Intensity { get; set; }

    public int PhotoCount { get; set; }

    public DateTime? Date { get; set; }

    public string? LocationName { get; set; }
}
