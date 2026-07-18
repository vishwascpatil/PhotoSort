namespace PhotoSort.Models;

public sealed class SpatialSignal
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? AdminArea { get; set; }
    public string? GeoHash { get; set; }
    public double Weight { get; set; }
    public bool IsHome { get; set; }
    public double DistanceFromHomeKm { get; set; }
}
